using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OnboardingService.Controllers;
using OnboardingService.DTOs;
using OnboardingService.Models;
using OnboardingService.Services;
using Xunit;

namespace OnboardingService.Tests;

public class OnboardingControllerTests
{
    private static readonly Guid EmployeeId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OnboardingId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task GetByEmployee_ReturnsNotFound_WhenMissing()
    {
        var service = new Mock<IOnboardingRecordService>();
        service.Setup(s => s.GetByEmployeeAsync(EmployeeId)).ReturnsAsync((OnboardingStatusResponse?)null);
        var controller = new OnboardingController(service.Object, NullLogger<OnboardingController>.Instance);

        var result = await controller.GetByEmployee(EmployeeId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Start_ReturnsConflict_WhenAlreadyExists()
    {
        var service = new Mock<IOnboardingRecordService>();
        service.Setup(s => s.StartAsync(EmployeeId)).ThrowsAsync(new OnboardingAlreadyExistsException(OnboardingId));
        var controller = new OnboardingController(service.Object, NullLogger<OnboardingController>.Instance);

        var result = await controller.Start(new StartOnboardingRequest(EmployeeId));

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task CreateAccountTasks_ReturnsOk()
    {
        var service = new Mock<IOnboardingRecordService>();
        var controller = new OnboardingController(service.Object, NullLogger<OnboardingController>.Instance);

        var result = await controller.CreateAccountTasks(OnboardingId, new CreateAccountTasksRequest(OnboardingId, "alice@htx.gov.sg", "EMP001"));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var service = new Mock<IOnboardingRecordService>();
        service.Setup(s => s.GetAllAsync()).ReturnsAsync(Enumerable.Empty<OnboardingService.Models.OnboardingRecord>());
        var controller = new OnboardingController(service.Object, NullLogger<OnboardingController>.Instance);

        var result = await controller.GetAll();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetByEmployee_ReturnsOk_WhenFound()
    {
        var service = new Mock<IOnboardingRecordService>();
        service.Setup(s => s.GetByEmployeeAsync(It.IsAny<Guid>())).ReturnsAsync(new OnboardingStatusResponse(
            Guid.NewGuid(), Guid.NewGuid(), "pending", null, null, DateTime.UtcNow, 0,
            Enumerable.Empty<AccountTaskResponse>(),
            Enumerable.Empty<EquipmentTaskResponse>(),
            Enumerable.Empty<AttemptHistoryResponse>()
        ));
        var controller = new OnboardingController(service.Object, NullLogger<OnboardingController>.Instance);

        var result = await controller.GetByEmployee(Guid.NewGuid());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Start_ReturnsCreated_WhenSuccessful()
    {
        var employeeId = Guid.NewGuid();
        var service = new Mock<IOnboardingRecordService>();
        service.Setup(s => s.StartAsync(employeeId)).ReturnsAsync(new OnboardingService.Models.OnboardingRecord { OnboardingId = Guid.NewGuid(), EmployeeId = employeeId });
        var controller = new OnboardingController(service.Object, NullLogger<OnboardingController>.Instance);

        var result = await controller.Start(new StartOnboardingRequest(employeeId));

        Assert.IsType<CreatedResult>(result);
    }

    [Fact]
    public async Task IssueEquipment_ReturnsOk()
    {
        var service = new Mock<IOnboardingRecordService>();
        var controller = new OnboardingController(service.Object, NullLogger<OnboardingController>.Instance);

        var result = await controller.IssueEquipment(OnboardingId, new IssueEquipmentRequest(OnboardingId, "Engineering"));

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_DelegatesIncrementFlag()
    {
        var service = new Mock<IOnboardingRecordService>();
        var controller = new OnboardingController(service.Object, NullLogger<OnboardingController>.Instance);

        var request = new UpdateOnboardingStatusRequest("failed", true, "boom");
        var result = await controller.UpdateStatus(OnboardingId, request);

        Assert.IsType<OkObjectResult>(result);
        service.Verify(s => s.UpdateStatusAsync(OnboardingId, "failed", true, "boom"), Times.Once);
    }
}