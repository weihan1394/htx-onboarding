using System.Net;
using System.Text;
using System.Text.Json;
using Temporalio.Activities;
using Temporalio.Exceptions;
using WorkflowService.Models;

namespace WorkflowService.Activities;

// each [Activity] method makes one HTTP call to onboarding-svc
// Temporal auto-retries on exception — non-retryable exceptions stop retries immediately
public class OnboardingActivities
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OnboardingActivities> _logger;

    // explicit camelCase — don't rely on global serializer settings
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OnboardingActivities(HttpClient httpClient, ILogger<OnboardingActivities> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // POST /api/onboarding/start
    // 201 = new record created; 409 = record already exists (fresh retry after timeout) — reset status instead
    [Activity]
    public async Task<Guid> StartOnboardingRecordAsync(OnboardingInput input)
    {
        _logger.LogInformation("Starting onboarding record for employee {EmployeeId}", input.EmployeeId);

        var content = new StringContent(
            JsonSerializer.Serialize(new { employeeId = input.EmployeeId }, JsonOptions),
            Encoding.UTF8,
            "application/json"
        );
        var response = await _httpClient.PostAsync("/api/onboarding/start", content);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            if (TryReadOnboardingId(doc.RootElement, out var id))
            {
                return id;
            }
        }

        // 409 → record exists from a prior execution; reset to in_progress and reuse the same record
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            if (TryReadOnboardingId(doc.RootElement, out var existingId))
            {
                await PatchAsync($"/api/onboarding/{existingId}/status",
                    new { status = "in_progress", incrementRetryCount = true });
                return existingId;
            }
        }

        EnsureRetryable(response);
        throw new InvalidOperationException($"Unexpected response {(int)response.StatusCode} from /api/onboarding/start.");
    }

    // reads onboardingId from JSON body — handles both camelCase and snake_case keys
    // onboarding-svc returns camelCase; defensive to snake_case in case serialiser varies
    private static bool TryReadOnboardingId(JsonElement root, out Guid onboardingId)
    {
        if (root.TryGetProperty("onboardingId", out var camelCaseId) && camelCaseId.ValueKind == JsonValueKind.String)
        {
            return Guid.TryParse(camelCaseId.GetString(), out onboardingId);
        }

        if (root.TryGetProperty("onboarding_id", out var snakeCaseId) && snakeCaseId.ValueKind == JsonValueKind.String)
        {
            return Guid.TryParse(snakeCaseId.GetString(), out onboardingId);
        }

        onboardingId = default;
        return false;
    }

    // POST /api/onboarding/{id}/accounts/create — creates email, VPN, hr_portal tasks
    [Activity]
    public async Task CreateAccountsAsync(OnboardingInput input, Guid onboardingId)
    {
        _logger.LogInformation("Creating accounts for employee {EmployeeId}", input.EmployeeId);

        await PostAsync($"/api/onboarding/{onboardingId}/accounts/create", new
        {
            onboardingId,
            employeeEmail  = input.Email,
            employeeNumber = input.EmployeeNumber
        });

        _logger.LogInformation("Accounts created for employee {EmployeeId}", input.EmployeeId);
    }

    // POST /api/onboarding/{id}/equipment/issue — creates laptop, staff_pass, welcome_kit tasks
    [Activity]
    public async Task IssueEquipmentAsync(OnboardingInput input, Guid onboardingId)
    {
        _logger.LogInformation("Issuing equipment for employee {EmployeeId}", input.EmployeeId);

        await PostAsync($"/api/onboarding/{onboardingId}/equipment/issue", new
        {
            onboardingId,
            department = input.Department
        });

        _logger.LogInformation("Equipment issued for employee {EmployeeId}", input.EmployeeId);
    }

    // PATCH /api/onboarding/{id}/status → in_progress, increment retry_count
    // called after WaitConditionAsync returns true — before looping back to retry steps
    [Activity]
    public async Task ResetOnboardingStatusAsync(Guid onboardingId)
    {
        _logger.LogInformation("Resetting onboarding {OnboardingId} to in_progress for retry", onboardingId);
        await PatchAsync($"/api/onboarding/{onboardingId}/status", new { status = "in_progress", incrementRetryCount = true });
    }

    // PATCH /api/onboarding/{id}/status → completed
    [Activity]
    public async Task CompleteOnboardingAsync(Guid onboardingId)
    {
        _logger.LogInformation("Completing onboarding record {OnboardingId}", onboardingId);
        await PatchAsync($"/api/onboarding/{onboardingId}/status", new { status = "completed" });
    }

    // PATCH /api/onboarding/{id}/status → failed
    // called before WaitConditionAsync — workflow pauses here waiting for RetryAsync signal
    [Activity]
    public async Task FailOnboardingAsync(Guid onboardingId, string errorMessage)
    {
        _logger.LogWarning("Marking onboarding {OnboardingId} as failed: {ErrorMessage}", onboardingId, errorMessage);
        await PatchAsync($"/api/onboarding/{onboardingId}/status", new { status = "failed", errorMessage });
    }

    // ── HTTP helpers ─────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> PostAsync(string path, object payload)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json"
        );
        var response = await _httpClient.PostAsync(path, content);
        EnsureRetryable(response);
        return response;
    }

    private async Task<HttpResponseMessage> PatchAsync(string path, object payload)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json"
        );
        var response = await _httpClient.PatchAsync(path, content);
        EnsureRetryable(response);
        return response;
    }

    // 4xx (except 429) = bad request — retrying won't help, throw non-retryable to stop Temporal retries
    // 429 and 5xx bubble up as regular exceptions so Temporal's retry policy handles them
    private static void EnsureRetryable(HttpResponseMessage response)
    {
        if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500
            && response.StatusCode != HttpStatusCode.TooManyRequests)
        {
            throw new ApplicationFailureException(
                $"Non-retryable HTTP {(int)response.StatusCode} from onboarding-svc: {response.RequestMessage?.RequestUri?.PathAndQuery}",
                nonRetryable: true);
        }
        response.EnsureSuccessStatusCode();
    }
}
