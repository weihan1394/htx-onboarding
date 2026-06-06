using OnboardingService.Models;

namespace OnboardingService.Repositories;

// data access contract — implemented by OnboardingRepository (Dapper + Postgres)
public interface IOnboardingRepository
{
    Task<IEnumerable<OnboardingRecord>> GetAllAsync();
    Task<OnboardingRecord?> GetByEmployeeIdAsync(Guid employeeId);
    Task<OnboardingRecord?> GetByIdAsync(Guid onboardingId);
    Task<OnboardingRecord> StartOnboardingAsync(Guid employeeId);
    Task UpdateOnboardingStatusAsync(Guid onboardingId, string status, bool incrementRetryCount = false, string? errorMessage = null);
    Task<IEnumerable<AttemptHistoryEntry>> GetRetryHistoryAsync(Guid onboardingId);
    Task<IEnumerable<AccountCreationTask>> GetAccountTasksAsync(Guid onboardingId);
    Task CreateAccountTasksAsync(Guid onboardingId, string employeeEmail, string employeeNumber);
    Task<IEnumerable<EquipmentIssuanceTask>> GetEquipmentTasksAsync(Guid onboardingId);
    Task CreateEquipmentTasksAsync(Guid onboardingId, string? department);
}
