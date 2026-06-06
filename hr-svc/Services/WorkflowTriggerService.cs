using System.Text;
using System.Text.Json;
using HrService.DTOs;
using HrService.Models;

namespace HrService.Services;

// HTTP client for triggering onboarding workflows in workflow-svc
public class WorkflowTriggerService : IWorkflowTriggerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WorkflowTriggerService> _logger;

    // explicit camelCase — workflow-svc expects it; don't rely on global serializer settings
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WorkflowTriggerService(HttpClient httpClient, ILogger<WorkflowTriggerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // POST /api/workflows/onboarding/start
    // rethrows on failure so EmployeeService can decide whether to surface or swallow
    public async Task TriggerOnboardingAsync(Employee employee)
    {
        try
        {
            var payload = new TriggerOnboardingRequest(
                employee.EmployeeId,
                employee.EmployeeNumber,
                employee.FirstName,
                employee.LastName,
                employee.Email,
                employee.Department,
                employee.Position
            );

            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync("/api/workflows/onboarding/start", content);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Onboarding workflow triggered for employee {EmployeeId} ({EmployeeNumber})",
                employee.EmployeeId, employee.EmployeeNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger onboarding workflow for employee {EmployeeId}", employee.EmployeeId);
            throw;
        }
    }
}
