using HrService.Controllers;
using HrService.DTOs;
using HrService.Models;
using HrService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http;
using Xunit;

namespace HrService.Tests;

public class EmployeesControllerTests
{
    private static readonly Mock<IHttpClientFactory> HttpFactory = new();

    private static EmployeesController Build(Mock<IEmployeeService> service) =>
        new(service.Object, NullLogger<EmployeesController>.Instance, HttpFactory.Object);

    [Fact]
    public async Task GetAll_ReturnsOk_WithEmployees()
    {
        var employees = new[] { new Employee { EmployeeId = Guid.NewGuid(), FirstName = "Alice", LastName = "Tan" } };
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.GetAllAsync()).ReturnsAsync(employees.AsEnumerable());

        var result = await Build(service).GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        var id = Guid.NewGuid();
        var employee = new Employee { EmployeeId = id, FirstName = "Alice", LastName = "Tan" };
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(employee);

        var result = await Build(service).GetById(id);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Employee?)null);

        var result = await Build(service).GetById(Guid.NewGuid());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenSuccessful()
    {
        var employee = new Employee { EmployeeId = Guid.NewGuid(), FirstName = "Alice", LastName = "Tan" };
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.CreateAsync(It.IsAny<CreateEmployeeRequest>())).ReturnsAsync(employee);

        var result = await Build(service).Create(new CreateEmployeeRequest("Alice", "Tan", "alice@htx.gov.sg", null, null, new DateOnly(2026, 6, 1)));

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenEmailExists()
    {
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.CreateAsync(It.IsAny<CreateEmployeeRequest>())).ThrowsAsync(new ConflictException("duplicate"));

        var result = await Build(service).Create(new CreateEmployeeRequest("Alice", "Tan", "alice@htx.gov.sg", null, null, new DateOnly(2026, 6, 1)));

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsOk_WhenUpdated()
    {
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(true);

        var result = await Build(service).UpdateStatus(Guid.NewGuid(), new UpdateStatusRequest("inactive"));

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsNotFound_WhenServiceReportsMissing()
    {
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(false);

        var result = await Build(service).UpdateStatus(Guid.NewGuid(), new UpdateStatusRequest("inactive"));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetOnboarding_ReturnsContent_WhenProxySucceeds()
    {
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.GetOnboardingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(("{\"status\":\"pending\"}", 200));

        var result = await Build(service).GetOnboarding("some-id", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(200, content.StatusCode);
    }

    [Fact]
    public async Task GetOnboarding_ReturnsProblem_WhenProxyFails()
    {
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.GetOnboardingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));

        var result = await Build(service).GetOnboarding("abc", CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, problem.StatusCode);
    }

    [Fact]
    public async Task RetryOnboarding_ReturnsContent_WhenSucceeds()
    {
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.RetryOnboardingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(("{\"accepted\":true}", 202));

        var result = await Build(service).RetryOnboarding("some-id", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(202, content.StatusCode);
    }

    [Fact]
    public async Task RetryOnboarding_ReturnsNotFound_WhenEmployeeMissing()
    {
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.RetryOnboardingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((ValueTuple<string, int>?)null);

        var result = await Build(service).RetryOnboarding("abc", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RetryOnboarding_ReturnsProblem_WhenWorkflowDown()
    {
        var service = new Mock<IEmployeeService>();
        service.Setup(s => s.RetryOnboardingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));

        var result = await Build(service).RetryOnboarding("some-id", CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, problem.StatusCode);
    }
}
