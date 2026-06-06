using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Temporalio.Client;
using WorkflowService.Models;
using WorkflowService.Services;
using WorkflowService.Workers;
using Xunit;

namespace WorkflowService.Tests;

public class OnboardingWorkflowServiceTests
{
    private static readonly Guid EmployeeId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly StartWorkflowRequest SampleRequest = new(
        EmployeeId, "EMP-0001", "Alice", "Tan", "alice@htx.gov.sg", "Engineering", "Engineer");

    private static (OnboardingWorkflowService svc, Mock<ITemporalGateway> gateway) Build(bool clientReady = true)
    {
        var holder = new TemporalClientHolder();
        if (clientReady)
        {
            var mockClient = new Mock<ITemporalClient>();
            holder.Client = mockClient.Object;
        }

        var gateway = new Mock<ITemporalGateway>();
        gateway.Setup(g => g.StartWorkflowAsync(It.IsAny<OnboardingInput>(), It.IsAny<Temporalio.Client.WorkflowOptions>()))
               .Returns(Task.CompletedTask);
        gateway.Setup(g => g.SignalRetryAsync(It.IsAny<string>()))
               .Returns(Task.CompletedTask);

        var svc = new OnboardingWorkflowService(holder, gateway.Object, NullLogger<OnboardingWorkflowService>.Instance);
        return (svc, gateway);
    }

    [Fact]
    public void IsReady_ReturnsFalse_WhenClientNotSet()
    {
        var (svc, _) = Build(clientReady: false);
        Assert.False(svc.IsReady);
    }

    [Fact]
    public void IsReady_ReturnsTrue_WhenClientSet()
    {
        var (svc, _) = Build(clientReady: true);
        Assert.True(svc.IsReady);
    }

    [Fact]
    public async Task StartAsync_ReturnsCorrectWorkflowId()
    {
        var (svc, _) = Build();

        var workflowId = await svc.StartAsync(SampleRequest);

        Assert.Equal($"onboarding-{EmployeeId}", workflowId);
    }

    [Fact]
    public async Task StartAsync_CallsGatewayWithCorrectInput()
    {
        var (svc, gateway) = Build();

        await svc.StartAsync(SampleRequest);

        gateway.Verify(g => g.StartWorkflowAsync(
            It.Is<OnboardingInput>(i => i.EmployeeId == EmployeeId && i.Email == "alice@htx.gov.sg"),
            It.IsAny<Temporalio.Client.WorkflowOptions>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithNullOptionals_UsesEmptyStringFallbacks()
    {
        var request = new StartWorkflowRequest(EmployeeId, null!, "Alice", "Tan", "alice@htx.gov.sg", null, null);
        var (svc, gateway) = Build();

        await svc.StartAsync(request);

        gateway.Verify(g => g.StartWorkflowAsync(
            It.Is<OnboardingInput>(i => i.Department == null && i.Position == null),
            It.IsAny<Temporalio.Client.WorkflowOptions>()), Times.Once);
    }

    [Fact]
    public async Task RetryAsync_SendsSignal_WhenWorkflowIsRunning()
    {
        var (svc, gateway) = Build();

        var workflowId = await svc.RetryAsync(EmployeeId.ToString(), SampleRequest);

        Assert.Equal($"onboarding-{EmployeeId}", workflowId);
        gateway.Verify(g => g.SignalRetryAsync($"onboarding-{EmployeeId}"), Times.Once);
        gateway.Verify(g => g.StartWorkflowAsync(It.IsAny<OnboardingInput>(), It.IsAny<Temporalio.Client.WorkflowOptions>()), Times.Never);
    }

    [Fact]
    public async Task RetryAsync_StartsNewWorkflow_WhenSignalFails()
    {
        var (svc, gateway) = Build();
        gateway.Setup(g => g.SignalRetryAsync(It.IsAny<string>())).ThrowsAsync(new Exception("not running"));

        var workflowId = await svc.RetryAsync(EmployeeId.ToString(), SampleRequest);

        Assert.Equal($"onboarding-{EmployeeId}", workflowId);
        gateway.Verify(g => g.StartWorkflowAsync(
            It.Is<OnboardingInput>(i => i.EmployeeId == EmployeeId),
            It.IsAny<Temporalio.Client.WorkflowOptions>()), Times.Once);
    }
}
