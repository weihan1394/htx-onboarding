using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WorkflowService.Controllers;
using WorkflowService.Models;
using WorkflowService.Services;
using WorkflowService.Workers;
using Xunit;

namespace WorkflowService.Tests;

public class WorkflowsControllerTests
{
    private static readonly Guid EmployeeId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task StartOnboarding_ReturnsProblem_WhenNotReady()
    {
        var holder = new TemporalClientHolder();
        var service = new Mock<IOnboardingWorkflowService>();
        service.SetupGet(s => s.IsReady).Returns(false);
        var controller = new WorkflowsController(service.Object, NullLogger<WorkflowsController>.Instance);

        var result = await controller.StartOnboarding(new StartWorkflowRequest(EmployeeId, "EMP001", "Alice", "Tan", "alice@htx.gov.sg", null, null));

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, problem.StatusCode);
    }

    [Fact]
    public async Task StartOnboarding_ReturnsAccepted_WhenStarted()
    {
        var service = new Mock<IOnboardingWorkflowService>();
        service.SetupGet(s => s.IsReady).Returns(true);
        service.Setup(s => s.StartAsync(It.IsAny<StartWorkflowRequest>())).ReturnsAsync("onboarding-123");
        var controller = new WorkflowsController(service.Object, NullLogger<WorkflowsController>.Instance);

        var result = await controller.StartOnboarding(new StartWorkflowRequest(EmployeeId, "EMP001", "Alice", "Tan", "alice@htx.gov.sg", null, null));

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(202, accepted.StatusCode);
    }

    [Fact]
    public async Task StartOnboarding_ReturnsConflict_WhenAlreadyStarted()
    {
        var service = new Mock<IOnboardingWorkflowService>();
        service.SetupGet(s => s.IsReady).Returns(true);
#pragma warning disable CS0618
        service.Setup(s => s.StartAsync(It.IsAny<StartWorkflowRequest>())).ThrowsAsync(new Temporalio.Exceptions.WorkflowAlreadyStartedException("already started", "wf", "run-1"));
#pragma warning restore CS0618
        var controller = new WorkflowsController(service.Object, NullLogger<WorkflowsController>.Instance);

        var result = await controller.StartOnboarding(new StartWorkflowRequest(EmployeeId, "EMP001", "Alice", "Tan", "alice@htx.gov.sg", null, null));

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task RetryOnboarding_ReturnsProblem_WhenNotReady()
    {
        var service = new Mock<IOnboardingWorkflowService>();
        service.SetupGet(s => s.IsReady).Returns(false);
        var controller = new WorkflowsController(service.Object, NullLogger<WorkflowsController>.Instance);

        var result = await controller.RetryOnboarding(EmployeeId.ToString(), new StartWorkflowRequest(EmployeeId, "EMP001", "Alice", "Tan", "alice@htx.gov.sg", null, null));

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, problem.StatusCode);
    }

    [Fact]
    public async Task RetryOnboarding_ReturnsAccepted_WhenRetrySent()
    {
        var service = new Mock<IOnboardingWorkflowService>();
        service.SetupGet(s => s.IsReady).Returns(true);
        service.Setup(s => s.RetryAsync(It.IsAny<string>(), It.IsAny<StartWorkflowRequest>())).ReturnsAsync("onboarding-123");
        var controller = new WorkflowsController(service.Object, NullLogger<WorkflowsController>.Instance);

        var result = await controller.RetryOnboarding(EmployeeId.ToString(), new StartWorkflowRequest(EmployeeId, "EMP001", "Alice", "Tan", "alice@htx.gov.sg", null, null));

        Assert.IsType<AcceptedResult>(result);
    }
}