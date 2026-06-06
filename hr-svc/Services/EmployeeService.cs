using HrService.DTOs;
using HrService.Models;
using HrService.Repositories;

namespace HrService.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _repo;
    private readonly IWorkflowTriggerService _workflowTrigger;
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(
        IEmployeeRepository repo,
        IWorkflowTriggerService workflowTrigger,
        IHttpClientFactory factory,
        ILogger<EmployeeService> logger)
    {
        _repo = repo;
        _workflowTrigger = workflowTrigger;
        _factory = factory;
        _logger = logger;
    }

    public Task<IEnumerable<Employee>> GetAllAsync()
    {
        return _repo.GetAllAsync();
    }

    public Task<Employee?> GetByIdAsync(Guid id)
    {
        return _repo.GetByIdAsync(id);
    }

    // Step 1: save employee to hr_db
    // Step 2: trigger Temporal workflow via workflow-svc
    // workflow failure is non-fatal — employee record is preserved and HR can retry later
    public async Task<Employee> CreateAsync(CreateEmployeeRequest request)
    {
        var existing = await _repo.GetByEmailAsync(request.Email);
        if (existing is not null)
        {
            throw new ConflictException("An employee with this email already exists.");
        }

        var employee = await _repo.CreateAsync(request);

        try
        {
            await _workflowTrigger.TriggerOnboardingAsync(employee);
        }
        catch (Exception ex)
        {
            // employee already saved — workflow failure is recoverable via the Retry button
            _logger.LogError(ex, "Workflow trigger failed for employee {EmployeeId}, but employee was created.", employee.EmployeeId);
        }

        return employee;
    }

    public Task<bool> UpdateStatusAsync(Guid id, string status)
    {
        return _repo.UpdateStatusAsync(id, status);
    }

    // BFF proxy — hr-web never calls onboarding-svc directly; passes response through as-is
    public async Task<(string Body, int StatusCode)> GetOnboardingAsync(string id, CancellationToken ct = default)
    {
        var client = _factory.CreateClient("onboarding-svc");
        var response = await client.GetAsync($"/api/onboarding/employee/{id}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return (body, (int)response.StatusCode);
    }

    // BFF proxy — forwards retry request to workflow-svc with full employee payload
    // returns null if employee not found
    public async Task<(string Body, int StatusCode)?> RetryOnboardingAsync(string id, CancellationToken ct = default)
    {
        var employee = await _repo.GetByIdAsync(Guid.Parse(id));
        if (employee is null)
        {
            return null;
        }

        var client = _factory.CreateClient("workflow-svc");
        var requestBody = JsonContent.Create(new
        {
            employeeId     = employee.EmployeeId,
            employeeNumber = employee.EmployeeNumber,
            firstName      = employee.FirstName,
            lastName       = employee.LastName,
            email          = employee.Email,
            department     = employee.Department,
            position       = employee.Position
        });

        var response = await client.PostAsync($"/api/workflows/onboarding/{id}/retry", requestBody, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        return (responseBody, (int)response.StatusCode);
    }
}
