using HrService.DTOs;
using HrService.Models;

namespace HrService.Services;

// business logic contract — implemented by EmployeeService
public interface IEmployeeService
{
    Task<IEnumerable<Employee>> GetAllAsync();
    Task<Employee?> GetByIdAsync(Guid id);
    Task<Employee> CreateAsync(CreateEmployeeRequest request);        // throws ConflictException on duplicate email
    Task<bool> UpdateStatusAsync(Guid id, string status);
    Task<(string Body, int StatusCode)> GetOnboardingAsync(string id, CancellationToken ct = default);          // BFF proxy to onboarding-svc
    Task<(string Body, int StatusCode)?> RetryOnboardingAsync(string id, CancellationToken ct = default);       // BFF proxy to workflow-svc; null = employee not found
}
