using Microsoft.AspNetCore.Mvc;
using OnboardingService.DTOs;
using OnboardingService.Services;
using StackExchange.Redis;
using System.Text.Json;

namespace OnboardingService.Controllers;

// called by Temporal activities (via OnboardingActivities in workflow-svc)
// and by hr-svc BFF proxies for status reads
[ApiController]
[Route("api/onboarding")]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingRecordService _service;
    private readonly ILogger<OnboardingController> _logger;

    public OnboardingController(IOnboardingRecordService service, ILogger<OnboardingController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET /api/onboarding — admin / debug use
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    // GET /api/onboarding/employee/{employeeId}
    // returns full status including account tasks, equipment tasks, and attempt history
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<IActionResult> GetByEmployee(Guid employeeId)
    {
        var response = await _service.GetByEmployeeAsync(employeeId);
        if (response is null)
        {
            return NotFound(new { message = "Onboarding record not found" });
        }
        return Ok(response);
    }

    // POST /api/onboarding/start
    // called by StartOnboardingRecordAsync activity — creates the onboarding record
    // 409 if record already exists (fresh retry execution after timeout)
    [HttpPost("start")]
    public async Task<IActionResult> Start(StartOnboardingRequest request)
    {
        try
        {
            var record = await _service.StartAsync(request.EmployeeId);
            return Created($"/api/onboarding/employee/{request.EmployeeId}", record);
        }
        catch (OnboardingAlreadyExistsException ex)
        {
            return Conflict(new { message = ex.Message, onboardingId = ex.OnboardingId });
        }
    }

    // POST /api/onboarding/{onboardingId}/accounts/create
    // called by CreateAccountsAsync activity — creates email, VPN, and HR portal tasks
    [HttpPost("{onboardingId:guid}/accounts/create")]
    public async Task<IActionResult> CreateAccountTasks(Guid onboardingId, CreateAccountTasksRequest request)
    {
        await _service.CreateAccountTasksAsync(onboardingId, request.EmployeeEmail, request.EmployeeNumber);
        return Ok(new { message = "Account tasks created", onboardingId });
    }

    // POST /api/onboarding/{onboardingId}/equipment/issue
    // called by IssueEquipmentAsync activity — creates laptop, staff pass, and welcome kit tasks
    [HttpPost("{onboardingId:guid}/equipment/issue")]
    public async Task<IActionResult> IssueEquipment(Guid onboardingId, IssueEquipmentRequest request)
    {
        await _service.IssueEquipmentAsync(onboardingId, request.Department);
        return Ok(new { message = "Equipment tasks created", onboardingId });
    }

    // PATCH /api/onboarding/{onboardingId}/status
    // called by FailOnboardingAsync, CompleteOnboardingAsync, and ResetOnboardingStatusAsync activities
    [HttpPatch("{onboardingId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid onboardingId, UpdateOnboardingStatusRequest request)
    {
        await _service.UpdateStatusAsync(onboardingId, request.Status, request.IncrementRetryCount, request.ErrorMessage);
        return Ok(new { message = "Status updated", onboardingId, status = request.Status });
    }

    // GET /api/onboarding/employee/{employeeId}/stream
    // Long-lived SSE endpoint — stays open until the browser disconnects.
    // Sends the current status immediately, then forwards every Valkey pub/sub
    // event on channel "onboarding:{employeeId}" as an SSE message.
    [HttpGet("employee/{employeeId:guid}/stream")]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public async Task StreamStatus(Guid employeeId, [FromServices] IConnectionMultiplexer mux, CancellationToken ct)
    {
        // Tell the browser this is an SSE stream, not a regular JSON response.
        // X-Accel-Buffering: no tells nginx not to buffer this response.
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers["X-Accel-Buffering"] = "no";
        await Response.Body.FlushAsync(ct); // send headers to browser immediately

        // Send current status right away — browser shouldn't have to wait for
        // the next status change before it sees anything.
        var current = await _service.GetByEmployeeAsync(employeeId);
        var initial = JsonSerializer.Serialize(new { status = current?.Status ?? "not_started" });
        await Response.WriteAsync($"data: {initial}\n\n", ct);
        await Response.Body.FlushAsync(ct);

        // Subscribe to the Valkey channel for this employee.
        // "done" is a signal — we complete it when the browser disconnects (ct is cancelled).
        var sub = mux.GetSubscriber();
        var channel = RedisChannel.Literal($"onboarding:{employeeId}");
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => done.TrySetResult()); // browser disconnect → complete the task

        await sub.SubscribeAsync(channel, async (_, message) =>
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }
            try
            {
                // Forward each Valkey message as an SSE event.
                // "data: {...}\n\n" is the SSE wire format — two newlines end an event.
                await Response.WriteAsync($"data: {message}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SSE write failed for employee {EmployeeId}", employeeId);
                done.TrySetResult();
            }
        });

        await done.Task; // hold the HTTP connection open until browser disconnects
        await sub.UnsubscribeAsync(channel); // clean up the Valkey subscription
    }
}
