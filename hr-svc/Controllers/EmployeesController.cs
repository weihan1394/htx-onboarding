using HrService.DTOs;
using HrService.Services;
using Microsoft.AspNetCore.Mvc;

namespace HrService.Controllers;

// single controller for all employee operations + BFF proxy endpoints
[ApiController]
[Route("api/hr/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _service;
    private readonly ILogger<EmployeesController> _logger;
    private readonly IHttpClientFactory _httpClientFactory; 

    public EmployeesController(IEmployeeService service, ILogger<EmployeesController> logger, IHttpClientFactory httpClientFactory)
    {
        _service = service;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    // GET /api/hr/employees
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var employees = await _service.GetAllAsync();
        return Ok(employees);
    }

    // GET /api/hr/employees/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var employee = await _service.GetByIdAsync(id);
        if (employee is null)
        {
            return NotFound(new { message = "Employee not found" });
        }
        return Ok(employee);
    }

    // POST /api/hr/employees
    // creates employee + triggers onboarding workflow; 409 if email already exists
    [HttpPost]
    public async Task<IActionResult> Create(CreateEmployeeRequest request)
    {
        try
        {
            var employee = await _service.CreateAsync(request);
            return Created($"/api/hr/employees/{employee.EmployeeId}", employee);
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // PATCH /api/hr/employees/{id}/status
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateStatusRequest request)
    {
        var updated = await _service.UpdateStatusAsync(id, request.Status);
        if (updated)
        {
            return Ok(new { message = "Status updated" });
        }
        return NotFound();
    }

    // GET /api/hr/employees/{id}/onboarding
    // BFF proxy — forwards to onboarding-svc and passes the response back as-is
    [HttpGet("{id}/onboarding")]
    public async Task<IActionResult> GetOnboarding(string id, CancellationToken ct)
    {
        try
        {
            var (body, statusCode) = await _service.GetOnboardingAsync(id, ct);
            return new ContentResult { Content = body, ContentType = "application/json", StatusCode = statusCode };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Onboarding Service unreachable for employee {EmployeeId}", id);
            return Problem("Onboarding Service is unavailable.", statusCode: 503);
        }
    }

    // POST /api/hr/employees/{id}/onboarding/retry
    // BFF proxy — forwards retry request to workflow-svc
    [HttpPost("{id:guid}/onboarding/retry")]
    public async Task<IActionResult> RetryOnboarding(string id, CancellationToken ct)
    {
        try
        {
            var result = await _service.RetryOnboardingAsync(id, ct);
            if (result is null)
            {
                return NotFound(new { message = "Employee not found" });
            }
            var (body, statusCode) = result.Value;
            return new ContentResult { Content = body, ContentType = "application/json", StatusCode = statusCode };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Workflow Service unreachable for retry of employee {EmployeeId}", id);
            return Problem("Workflow Service is unavailable.", statusCode: 503);
        }
    }

    // GET /api/hr/employees/{id}/onboarding/stream
    // Pipes the SSE stream from onboarding-svc directly to the browser.
    // Uses ResponseHeadersRead so the body is streamed, not buffered in memory.
    [HttpGet("{id}/onboarding/stream")]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public async Task StreamOnboarding(string id, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("onboarding-svc-sse");

        // ResponseHeadersRead = return from GetAsync as soon as we get HTTP headers,
        // without reading the body yet. The body is a stream we then copy.
        using var upstream = await client.GetAsync(
            $"/api/onboarding/employee/{id}/stream",
            HttpCompletionOption.ResponseHeadersRead, ct);

        if (!upstream.IsSuccessStatusCode)
        {
            Response.StatusCode = (int)upstream.StatusCode;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        // CopyToAsync pipes bytes from onboarding-svc's response stream into our
        // response stream in real time. It returns when ct is cancelled (browser disconnect).
        await using var body = await upstream.Content.ReadAsStreamAsync(ct);
        try
        {
            await body.CopyToAsync(Response.Body, ct);
        }
        catch (OperationCanceledException)
        {
            // browser disconnected — not an error
        }
    }
}
