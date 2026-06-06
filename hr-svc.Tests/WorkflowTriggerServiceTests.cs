using System.Net;
using System.Text;
using System.Text.Json;
using HrService.Models;
using HrService.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HrService.Tests;

public class WorkflowTriggerServiceTests
{
    private static readonly Employee SampleEmployee = new()
    {
        EmployeeId = new Guid("11111111-1111-1111-1111-111111111111"),
        EmployeeNumber = "EMP001",
        FirstName = "Alice",
        LastName = "Tan",
        Email = "alice.tan@htx.gov.sg",
        Department = "Engineering",
        Position = "Software Engineer",
        HireDate = new DateOnly(2026, 6, 1)
    };

    private static (WorkflowTriggerService svc, CaptureHttpHandler handler) Build(HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new CaptureHttpHandler(status);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://workflow-svc") };
        var svc = new WorkflowTriggerService(client, NullLogger<WorkflowTriggerService>.Instance);
        return (svc, handler);
    }

    [Fact]
    public async Task TriggerOnboardingAsync_PostsToCorrectUrl()
    {
        var (svc, handler) = Build();
        await svc.TriggerOnboardingAsync(SampleEmployee);

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/api/workflows/onboarding/start", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task TriggerOnboardingAsync_SerializesAllEmployeeFields()
    {
        var (svc, handler) = Build();
        await svc.TriggerOnboardingAsync(SampleEmployee);

        var body = handler.Bodies[0];
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(SampleEmployee.EmployeeId.ToString(), root.GetProperty("employeeId").GetString());
        Assert.Equal(SampleEmployee.EmployeeNumber, root.GetProperty("employeeNumber").GetString());
        Assert.Equal(SampleEmployee.FirstName, root.GetProperty("firstName").GetString());
        Assert.Equal(SampleEmployee.LastName, root.GetProperty("lastName").GetString());
        Assert.Equal(SampleEmployee.Email, root.GetProperty("email").GetString());
        Assert.Equal(SampleEmployee.Department, root.GetProperty("department").GetString());
        Assert.Equal(SampleEmployee.Position, root.GetProperty("position").GetString());
    }

    [Fact]
    public async Task TriggerOnboardingAsync_SerializesNullDepartment()
    {
        var (svc, handler) = Build();
        var emp = new Employee
        {
            EmployeeId = Guid.NewGuid(),
            EmployeeNumber = "EMP002",
            FirstName = "Bob",
            LastName = "Lee",
            Email = "bob.lee@htx.gov.sg",
            Department = null,
            Position = null
        };

        await svc.TriggerOnboardingAsync(emp);

        var doc = JsonDocument.Parse(handler.Bodies[0]);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("department").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("position").ValueKind);
    }

    [Fact]
    public async Task TriggerOnboardingAsync_Throws_OnHttpFailure()
    {
        var (svc, _) = Build(HttpStatusCode.InternalServerError);
        await Assert.ThrowsAsync<HttpRequestException>(() => svc.TriggerOnboardingAsync(SampleEmployee));
    }

    [Fact]
    public async Task TriggerOnboardingAsync_Throws_OnNetworkException()
    {
        var handler = new ThrowingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://workflow-svc") };
        var svc = new WorkflowTriggerService(client, NullLogger<WorkflowTriggerService>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => svc.TriggerOnboardingAsync(SampleEmployee));
    }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

internal sealed class CaptureHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> Bodies { get; } = [];

    public CaptureHttpHandler(HttpStatusCode status = HttpStatusCode.OK) => _status = status;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is not null ? await request.Content.ReadAsStringAsync(ct) : string.Empty);
        return new HttpResponseMessage(_status) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
    }
}

internal sealed class ThrowingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => throw new HttpRequestException("Simulated network error");
}
