using Dapper;
using Npgsql;
using OnboardingService.DTOs;
using OnboardingService.Models;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace OnboardingService.Repositories;

// all SQL queries for the onboarding schema — uses Dapper + Npgsql
[ExcludeFromCodeCoverage]
public class OnboardingRepository : IOnboardingRepository
{
    private readonly string _connectionString;

    public OnboardingRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not configured.");
    }

    // ── Onboarding Records ───────────────────────────────────────────────────

    // newest records first
    public async Task<IEnumerable<OnboardingRecord>> GetAllAsync()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<OnboardingRecord>(@"
            SELECT onboarding_id, employee_id, status, started_at, completed_at, created_at, updated_at, retry_count
            FROM onboarding.onboarding_records
            ORDER BY created_at DESC");
    }

    public async Task<OnboardingRecord?> GetByEmployeeIdAsync(Guid employeeId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<OnboardingRecord>(@"
            SELECT onboarding_id, employee_id, status, started_at, completed_at, created_at, updated_at, retry_count
            FROM onboarding.onboarding_records
            WHERE employee_id = @EmployeeId", new { EmployeeId = employeeId });
    }

    public async Task<OnboardingRecord?> GetByIdAsync(Guid onboardingId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<OnboardingRecord>(@"
            SELECT onboarding_id, employee_id, status, started_at, completed_at, created_at, updated_at, retry_count
            FROM onboarding.onboarding_records
            WHERE onboarding_id = @OnboardingId", new { OnboardingId = onboardingId });
    }

    public async Task<OnboardingRecord> StartOnboardingAsync(Guid employeeId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleAsync<OnboardingRecord>(@"
            INSERT INTO onboarding.onboarding_records (employee_id, status, started_at)
            VALUES (@EmployeeId, 'in_progress', NOW())
            RETURNING onboarding_id, employee_id, status, started_at, completed_at, created_at, updated_at, retry_count",
            new { EmployeeId = employeeId });
    }

    // transaction so the status UPDATE and history INSERT are atomic
    // RETURNING retry_count in one round-trip — needed to compute the attempt number
    public async Task UpdateOnboardingStatusAsync(Guid onboardingId, string status, bool incrementRetryCount = false, string? errorMessage = null)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        var retryCount = await conn.QuerySingleAsync<int>(@"
            UPDATE onboarding.onboarding_records
            SET status       = @Status,
                completed_at = CASE WHEN @Status = 'completed' THEN NOW() ELSE NULL END,
                retry_count  = CASE WHEN @IncrementRetryCount THEN retry_count + 1 ELSE retry_count END
            WHERE onboarding_id = @OnboardingId
            RETURNING retry_count",
            new { Status = status, OnboardingId = onboardingId, IncrementRetryCount = incrementRetryCount },
            tx);

        // record every terminal outcome — even first-run completions get a history row
        if (status == "failed" || status == "completed")
        {
            await conn.ExecuteAsync(@"
                INSERT INTO onboarding.onboarding_transactions (onboarding_id, attempt, status, error_message)
                VALUES (@OnboardingId, @Attempt, @Status, @ErrorMessage)",
                new { OnboardingId = onboardingId, Attempt = incrementRetryCount ? retryCount : retryCount + 1, Status = status, ErrorMessage = status == "failed" ? errorMessage : null },
                tx);
        }

        await tx.CommitAsync();
    }

    // newest attempt first
    public async Task<IEnumerable<AttemptHistoryEntry>> GetRetryHistoryAsync(Guid onboardingId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<AttemptHistoryEntry>(@"
            SELECT history_id, onboarding_id, attempt, status, attempted_at, error_message
            FROM onboarding.onboarding_transactions
            WHERE onboarding_id = @OnboardingId
            ORDER BY attempt DESC",
            new { OnboardingId = onboardingId });
    }

    // ── Account Tasks ────────────────────────────────────────────────────────

    public async Task<IEnumerable<AccountCreationTask>> GetAccountTasksAsync(Guid onboardingId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<AccountCreationTask>(@"
            SELECT task_id, onboarding_id, account_type, username, status, error_message, created_at, completed_at
            FROM onboarding.tasks_accounts
            WHERE onboarding_id = @OnboardingId", new { OnboardingId = onboardingId });
    }

    // creates email, VPN, and HR portal account tasks
    // ON CONFLICT DO NOTHING — idempotent; safe to call on retry
    public async Task CreateAccountTasksAsync(Guid onboardingId, string employeeEmail, string employeeNumber)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var baseName = employeeEmail.Split('@')[0];
        var email = $"{baseName}@htx.gov.sg";
        var accountTypes = new[]
        {
            new { Type = "email",     Username = email },
            new { Type = "vpn",       Username = email },
            new { Type = "hr_portal", Username = email }
        };

        foreach (var account in accountTypes)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO onboarding.tasks_accounts
                    (onboarding_id, account_type, username, status, completed_at)
                VALUES (@OnboardingId, @AccountType, @Username, 'completed', NOW())
                ON CONFLICT (onboarding_id, account_type) DO UPDATE
                    SET username      = EXCLUDED.username,
                        status        = 'completed',
                        completed_at  = NOW(),
                        error_message = NULL",
                new { OnboardingId = onboardingId, AccountType = account.Type, account.Username });
        }
    }

    // ── Equipment Tasks ──────────────────────────────────────────────────────

    // item_details is stored as JSONB — cast to text so Dapper reads it as a string
    public async Task<IEnumerable<EquipmentIssuanceTask>> GetEquipmentTasksAsync(Guid onboardingId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<EquipmentIssuanceTask>(@"
            SELECT task_id, onboarding_id, item_type, item_details::text, status, error_message, issued_at, created_at
            FROM onboarding.tasks_equipment
            WHERE onboarding_id = @OnboardingId", new { OnboardingId = onboardingId });
    }

    // creates laptop, staff pass, and welcome kit tasks
    // laptop model varies by department; ON CONFLICT DO NOTHING — idempotent on retry
    public async Task CreateEquipmentTasksAsync(Guid onboardingId, string? department)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var laptopModel = department == "Engineering" ? "MacBook Pro 14" : "Dell XPS 15";

        var items = new[]
        {
            new { Type = "laptop",      Details = JsonSerializer.Serialize(new { model = laptopModel, os = "Windows 11" }) },
            new { Type = "staff_pass",  Details = JsonSerializer.Serialize(new { access_level = "L2" }) },
            new { Type = "welcome_kit", Details = JsonSerializer.Serialize(new { items = new[] { "notebook", "pen", "lanyard", "htx_tshirt" } }) }
        };

        foreach (var item in items)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO onboarding.tasks_equipment
                    (onboarding_id, item_type, item_details, status, issued_at)
                VALUES (@OnboardingId, @ItemType, @ItemDetails::jsonb, 'issued', NOW())
                ON CONFLICT (onboarding_id, item_type) DO UPDATE
                    SET item_details  = EXCLUDED.item_details,
                        status        = 'issued',
                        issued_at     = NOW(),
                        error_message = NULL",
                new { OnboardingId = onboardingId, ItemType = item.Type, ItemDetails = item.Details });
        }
    }
}
