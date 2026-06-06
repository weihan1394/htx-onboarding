using System.Net;
using System.Net.Http.Json;
using HrService.Models;
using HrService.Services;
using HrService.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HrService.Tests;

public class EmployeeServiceTests
{
    [Fact]
    public async Task CreateAsync_Throws_IfEmailExists()
    {
        var repo = new Mock<IEmployeeRepository>();
        repo.Setup(r => r.GetByEmailAsync("alice@htx.gov.sg")).ReturnsAsync(new Employee());
        var wf = new Mock<IWorkflowTriggerService>();
        var factory = new Mock<IHttpClientFactory>();
        var svc = new EmployeeService(repo.Object, wf.Object, factory.Object, NullLogger<EmployeeService>.Instance);

        await Assert.ThrowsAsync<ConflictException>(() => svc.CreateAsync(new DTOs.CreateEmployeeRequest("Alice","Tan","alice@htx.gov.sg","Engineering","Engineer", new DateOnly(2026,6,1))));
    }

    [Fact]
    public async Task CreateAsync_Swallows_WorkflowException_AndReturnsEmployee()
    {
        var newEmployee = new Employee { EmployeeId = Guid.NewGuid(), EmployeeNumber = "EMP-0001", FirstName = "Alice" };
        var repo = new Mock<IEmployeeRepository>();
        repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Employee?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<DTOs.CreateEmployeeRequest>())).ReturnsAsync(newEmployee);

        var wf = new Mock<IWorkflowTriggerService>();
        wf.Setup(w => w.TriggerOnboardingAsync(newEmployee)).ThrowsAsync(new HttpRequestException("fail"));

        var factory = new Mock<IHttpClientFactory>();
        var svc = new EmployeeService(repo.Object, wf.Object, factory.Object, NullLogger<EmployeeService>.Instance);

        var created = await svc.CreateAsync(new DTOs.CreateEmployeeRequest("Alice","Tan","alice@htx.gov.sg","Engineering","Engineer", new DateOnly(2026,6,1)));
        Assert.Equal(newEmployee.EmployeeId, created.EmployeeId);
        // ensure TriggerOnboardingAsync was called
        wf.Verify(w => w.TriggerOnboardingAsync(newEmployee), Times.Once);
    }

    [Fact]
    public async Task GetOnboardingAsync_ProxiesResponse()
    {
        var repo = new Mock<IEmployeeRepository>();
        var wf = new Mock<IWorkflowTriggerService>();

        var handler = new CaptureHandler(HttpStatusCode.OK, "{\"ok\":true}");
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://onboarding-svc") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("onboarding-svc")).Returns(client);

        var svc = new EmployeeService(repo.Object, wf.Object, factory.Object, NullLogger<EmployeeService>.Instance);

        var (body, status) = await svc.GetOnboardingAsync(Guid.NewGuid().ToString());
        Assert.Equal(200, status);
        Assert.Equal("{\"ok\":true}", body);
    }

    [Fact]
    public async Task GetAllAsync_DelegatesToRepository()
    {
        var employees = new[] { new Employee { EmployeeId = Guid.NewGuid() } };
        var repo = new Mock<IEmployeeRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(employees.AsEnumerable());
        var svc = new EmployeeService(repo.Object, new Mock<IWorkflowTriggerService>().Object, new Mock<IHttpClientFactory>().Object, NullLogger<EmployeeService>.Instance);

        var result = await svc.GetAllAsync();

        Assert.Same(employees, result);
    }

    [Fact]
    public async Task GetByIdAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        var employee = new Employee { EmployeeId = id };
        var repo = new Mock<IEmployeeRepository>();
        repo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(employee);
        var svc = new EmployeeService(repo.Object, new Mock<IWorkflowTriggerService>().Object, new Mock<IHttpClientFactory>().Object, NullLogger<EmployeeService>.Instance);

        var result = await svc.GetByIdAsync(id);

        Assert.Equal(employee, result);
    }

    [Fact]
    public async Task UpdateStatusAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        var repo = new Mock<IEmployeeRepository>();
        repo.Setup(r => r.UpdateStatusAsync(id, "inactive")).ReturnsAsync(true);
        var svc = new EmployeeService(repo.Object, new Mock<IWorkflowTriggerService>().Object, new Mock<IHttpClientFactory>().Object, NullLogger<EmployeeService>.Instance);

        var result = await svc.UpdateStatusAsync(id, "inactive");

        Assert.True(result);
    }

    [Fact]
    public async Task CreateAsync_Succeeds_WhenNoConflict()
    {
        var newEmployee = new Employee { EmployeeId = Guid.NewGuid(), EmployeeNumber = "EMP-0002", FirstName = "Bob", LastName = "Lee", Email = "bob@htx.gov.sg" };
        var repo = new Mock<IEmployeeRepository>();
        repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Employee?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<DTOs.CreateEmployeeRequest>())).ReturnsAsync(newEmployee);
        var wf = new Mock<IWorkflowTriggerService>();
        wf.Setup(w => w.TriggerOnboardingAsync(newEmployee)).Returns(Task.CompletedTask);
        var svc = new EmployeeService(repo.Object, wf.Object, new Mock<IHttpClientFactory>().Object, NullLogger<EmployeeService>.Instance);

        var result = await svc.CreateAsync(new DTOs.CreateEmployeeRequest("Bob", "Lee", "bob@htx.gov.sg", null, null, new DateOnly(2026, 6, 1)));

        Assert.Equal(newEmployee.EmployeeId, result.EmployeeId);
    }

    [Fact]
    public async Task RetryOnboardingAsync_ReturnsNull_WhenEmployeeNotFound()
    {
        var repo = new Mock<IEmployeeRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Employee?)null);
        var svc = new EmployeeService(repo.Object, new Mock<IWorkflowTriggerService>().Object, new Mock<IHttpClientFactory>().Object, NullLogger<EmployeeService>.Instance);

        var result = await svc.RetryOnboardingAsync(Guid.NewGuid().ToString());

        Assert.Null(result);
    }

    [Fact]
    public async Task RetryOnboardingAsync_PostsToWorkflowSvc_WhenEmployeeFound()
    {
        var id = Guid.NewGuid();
        var employee = new Employee { EmployeeId = id, EmployeeNumber = "EMP-0001", FirstName = "Alice", LastName = "Tan", Email = "alice@htx.gov.sg", Department = "Engineering", Position = "Engineer" };
        var repo = new Mock<IEmployeeRepository>();
        repo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(employee);
        var handler = new CaptureHandler(System.Net.HttpStatusCode.Accepted, "{\"workflowId\":\"wf-1\"}");
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://workflow-svc") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("workflow-svc")).Returns(client);
        var svc = new EmployeeService(repo.Object, new Mock<IWorkflowTriggerService>().Object, factory.Object, NullLogger<EmployeeService>.Instance);

        var result = await svc.RetryOnboardingAsync(id.ToString());

        Assert.NotNull(result);
        Assert.Equal(202, result!.Value.StatusCode);
    }
}

internal sealed class CaptureHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    public CaptureHandler(HttpStatusCode status, string body) { _status = status; _body = body; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
}
