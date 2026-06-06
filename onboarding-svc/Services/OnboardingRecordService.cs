using OnboardingService.DTOs;
using OnboardingService.Models;
using OnboardingService.Repositories;

namespace OnboardingService.Services;

// thin service layer — delegates to repository; assembles the full status response
public class OnboardingRecordService : IOnboardingRecordService
{
    private readonly IOnboardingRepository _repo;
    private readonly IOnboardingPublisher _publisher;

    public OnboardingRecordService(IOnboardingRepository repo, IOnboardingPublisher publisher)
    {
        _repo = repo;
        _publisher = publisher;
    }

    public Task<IEnumerable<OnboardingRecord>> GetAllAsync()
    {
        return _repo.GetAllAsync();
    }

    // fetches onboarding record + all related tasks + attempt history in 4 queries,
    // then assembles the full OnboardingStatusResponse returned to hr-web
    public async Task<OnboardingStatusResponse?> GetByEmployeeAsync(Guid employeeId)
    {
        var record = await _repo.GetByEmployeeIdAsync(employeeId);
        if (record is null)
        {
            return null;
        }

        var accounts     = await _repo.GetAccountTasksAsync(record.OnboardingId);
        var equipment    = await _repo.GetEquipmentTasksAsync(record.OnboardingId);
        var retryHistory = await _repo.GetRetryHistoryAsync(record.OnboardingId);

        return new OnboardingStatusResponse(
            record.OnboardingId,
            record.EmployeeId,
            record.Status,
            record.StartedAt,
            record.CompletedAt,
            record.CreatedAt,
            record.RetryCount,
            accounts.Select(a => new AccountTaskResponse(a.TaskId, a.AccountType, a.Username, a.Status, a.ErrorMessage, a.CompletedAt)),
            equipment.Select(e => new EquipmentTaskResponse(e.TaskId, e.ItemType, e.ItemDetails, e.Status, e.ErrorMessage, e.IssuedAt)),
            retryHistory.Select(h => new AttemptHistoryResponse(h.HistoryId, h.Attempt, h.Status, h.AttemptedAt, h.ErrorMessage))
        );
    }

    // throws OnboardingAlreadyExistsException if a record already exists for this employee
    // the activity catches this as 409 and resets status instead of creating a duplicate
    public async Task<OnboardingRecord> StartAsync(Guid employeeId)
    {
        var existing = await _repo.GetByEmployeeIdAsync(employeeId);
        if (existing is not null)
        {
            throw new OnboardingAlreadyExistsException(existing.OnboardingId);
        }

        return await _repo.StartOnboardingAsync(employeeId);
    }

    public async Task CreateAccountTasksAsync(Guid onboardingId, string employeeEmail, string employeeNumber)
    {
        await _repo.CreateAccountTasksAsync(onboardingId, employeeEmail, employeeNumber);
    }

    public async Task IssueEquipmentAsync(Guid onboardingId, string? department)
    {
        await _repo.CreateEquipmentTasksAsync(onboardingId, department);
    }

    public async Task UpdateStatusAsync(Guid onboardingId, string status, bool incrementRetryCount = false, string? errorMessage = null)
    {
        await _repo.UpdateOnboardingStatusAsync(onboardingId, status, incrementRetryCount, errorMessage);

        var record = await _repo.GetByIdAsync(onboardingId);

        // after updating the status in the database, publish a message to Redis so hr-web can update in real time
        if (record is not null)
        {
            await _publisher.PublishStatusChangedAsync(record.EmployeeId, status);
        }

    }
}
