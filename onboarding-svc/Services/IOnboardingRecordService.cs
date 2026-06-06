using OnboardingService.DTOs;
using OnboardingService.Models;

namespace OnboardingService.Services;

// business logic contract — implemented by OnboardingRecordService
public interface IOnboardingRecordService
{
    Task<IEnumerable<OnboardingRecord>> GetAllAsync();
    Task<OnboardingStatusResponse?> GetByEmployeeAsync(Guid employeeId);        // null = not found
    Task<OnboardingRecord> StartAsync(Guid employeeId);                          // throws OnboardingAlreadyExistsException on duplicate
    Task CreateAccountTasksAsync(Guid onboardingId, string employeeEmail, string employeeNumber);
    Task IssueEquipmentAsync(Guid onboardingId, string? department);
    Task UpdateStatusAsync(Guid onboardingId, string status, bool incrementRetryCount = false, string? errorMessage = null);
}
