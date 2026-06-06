using Microsoft.AspNetCore.Mvc;
using WorkflowService.Models;
using WorkflowService.Services;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowsController : ControllerBase
{
    private readonly IOnboardingWorkflowService _service;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(IOnboardingWorkflowService service, ILogger<WorkflowsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // POST /api/workflows/onboarding/start
    // called by hr-svc after a new employee is created
    // 503 during startup while TemporalWorkerService is still connecting
    // 409 if a workflow is already running for this employee
    [HttpPost("onboarding/start")]
    public async Task<IActionResult> StartOnboarding(StartWorkflowRequest request)
    {
        if (!_service.IsReady)
        {
            return Problem("Temporal not yet connected — service is starting up", statusCode: 503);
        }

        try
        {
            var workflowId = await _service.StartAsync(request);
            return Accepted($"/api/workflows/{workflowId}/status", new
            {
                workflowId,
                employeeId = request.EmployeeId,
                status = "started"
            });
        }
        catch (Temporalio.Exceptions.WorkflowAlreadyStartedException)
        {
            var workflowId = $"onboarding-{request.EmployeeId}";
            _logger.LogWarning("Workflow already started for employee {EmployeeId}", request.EmployeeId);
            return Conflict(new { message = "Onboarding workflow already running for this employee", workflowId });
        }
    }

    // POST /api/workflows/onboarding/{employeeId}/retry
    // called by hr-svc when HR clicks Retry Workflow
    // sends RetryAsync signal to the paused workflow; starts fresh if workflow not running
    [HttpPost("onboarding/{employeeId}/retry")]
    public async Task<IActionResult> RetryOnboarding(string employeeId, StartWorkflowRequest request)
    {
        if (!_service.IsReady)
        {
            return Problem("Temporal not yet connected — service is starting up", statusCode: 503);
        }

        var workflowId = await _service.RetryAsync(employeeId, request);
        return Accepted($"/api/workflows/{workflowId}/status", new { workflowId, status = "retried" });
    }
}
