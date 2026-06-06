using Moq;
using OnboardingService.DTOs;
using OnboardingService.Models;
using OnboardingService.Repositories;
using OnboardingService.Services;
using Xunit;

namespace OnboardingService.Tests;

public class OnboardingRecordServiceTests
{
    private static readonly Guid EmployeeId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OnboardingId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Mock<IOnboardingPublisher> Publisher = new();

    [Fact]
    public async Task GetByEmployeeAsync_ReturnsNull_WhenMissing()
    {
        var repo = new Mock<IOnboardingRepository>();
        repo.Setup(r => r.GetByEmployeeIdAsync(EmployeeId)).ReturnsAsync((OnboardingRecord?)null);
        var service = new OnboardingRecordService(repo.Object, Publisher.Object);

        var result = await service.GetByEmployeeAsync(EmployeeId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByEmployeeAsync_MapsTasksAndHistory()
    {
        var now = DateTime.UtcNow;
        var repo = new Mock<IOnboardingRepository>();
        repo.Setup(r => r.GetByEmployeeIdAsync(EmployeeId)).ReturnsAsync(new OnboardingRecord
        {
            OnboardingId = OnboardingId,
            EmployeeId = EmployeeId,
            Status = "failed",
            StartedAt = now.AddDays(-1),
            CompletedAt = null,
            CreatedAt = now.AddDays(-2),
            RetryCount = 2
        });
        repo.Setup(r => r.GetAccountTasksAsync(OnboardingId)).ReturnsAsync(new[]
        {
            new AccountCreationTask
            {
                TaskId = Guid.NewGuid(),
                AccountType = "email",
                Username = "alice@htx.gov.sg",
                Status = "completed",
                ErrorMessage = null,
                CompletedAt = now
            }
        });
        repo.Setup(r => r.GetEquipmentTasksAsync(OnboardingId)).ReturnsAsync(new[]
        {
            new EquipmentIssuanceTask
            {
                TaskId = Guid.NewGuid(),
                ItemType = "laptop",
                ItemDetails = "{}",
                Status = "issued",
                ErrorMessage = null,
                IssuedAt = now
            }
        });
        repo.Setup(r => r.GetRetryHistoryAsync(OnboardingId)).ReturnsAsync(new[]
        {
            new AttemptHistoryEntry
            {
                HistoryId = Guid.NewGuid(),
                Attempt = 1,
                Status = "failed",
                AttemptedAt = now,
                ErrorMessage = "boom"
            }
        });

        var service = new OnboardingRecordService(repo.Object, Publisher.Object);

        var result = await service.GetByEmployeeAsync(EmployeeId);

        Assert.NotNull(result);
        Assert.Equal(OnboardingId, result!.OnboardingId);
        Assert.Single(result.AccountTasks);
        Assert.Single(result.EquipmentTasks);
        Assert.Single(result.RetryHistory);
        Assert.Equal("failed", result.Status);
        Assert.Equal(2, result.RetryCount);
    }

    [Fact]
    public async Task StartAsync_Throws_WhenRecordExists()
    {
        var repo = new Mock<IOnboardingRepository>();
        repo.Setup(r => r.GetByEmployeeIdAsync(EmployeeId)).ReturnsAsync(new OnboardingRecord { OnboardingId = OnboardingId });
        var service = new OnboardingRecordService(repo.Object, Publisher.Object);

        var ex = await Assert.ThrowsAsync<OnboardingAlreadyExistsException>(() => service.StartAsync(EmployeeId));

        Assert.Equal(OnboardingId, ex.OnboardingId);
    }

    [Fact]
    public async Task StartAsync_CreatesRecord_WhenMissing()
    {
        var created = new OnboardingRecord { OnboardingId = OnboardingId, EmployeeId = EmployeeId };
        var repo = new Mock<IOnboardingRepository>();
        repo.Setup(r => r.GetByEmployeeIdAsync(EmployeeId)).ReturnsAsync((OnboardingRecord?)null);
        repo.Setup(r => r.StartOnboardingAsync(EmployeeId)).ReturnsAsync(created);
        var service = new OnboardingRecordService(repo.Object, Publisher.Object);

        var result = await service.StartAsync(EmployeeId);

        Assert.Equal(created, result);
        repo.Verify(r => r.StartOnboardingAsync(EmployeeId), Times.Once);
    }

    [Fact]
    public async Task TaskMethods_DelegateToRepository()
    {
        var repo = new Mock<IOnboardingRepository>();
        var service = new OnboardingRecordService(repo.Object, Publisher.Object);

        await service.CreateAccountTasksAsync(OnboardingId, "alice@htx.gov.sg", "EMP001");
        await service.IssueEquipmentAsync(OnboardingId, "Engineering");
        await service.UpdateStatusAsync(OnboardingId, "failed", true, "boom");

        repo.Verify(r => r.CreateAccountTasksAsync(OnboardingId, "alice@htx.gov.sg", "EMP001"), Times.Once);
        repo.Verify(r => r.CreateEquipmentTasksAsync(OnboardingId, "Engineering"), Times.Once);
        repo.Verify(r => r.UpdateOnboardingStatusAsync(OnboardingId, "failed", true, "boom"), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_PublishesEvent_WhenRecordFound()
    {
        var record = new OnboardingRecord { OnboardingId = OnboardingId, EmployeeId = EmployeeId, Status = "completed" };
        var repo = new Mock<IOnboardingRepository>();
        repo.Setup(r => r.GetByIdAsync(OnboardingId)).ReturnsAsync(record);
        var publisher = new Mock<IOnboardingPublisher>();
        var service = new OnboardingRecordService(repo.Object, publisher.Object);

        await service.UpdateStatusAsync(OnboardingId, "completed");

        publisher.Verify(p => p.PublishStatusChangedAsync(EmployeeId, "completed"), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_DoesNotPublish_WhenRecordMissing()
    {
        var repo = new Mock<IOnboardingRepository>();
        repo.Setup(r => r.GetByIdAsync(OnboardingId)).ReturnsAsync((OnboardingRecord?)null);
        var publisher = new Mock<IOnboardingPublisher>();
        var service = new OnboardingRecordService(repo.Object, publisher.Object);

        await service.UpdateStatusAsync(OnboardingId, "completed");

        publisher.Verify(p => p.PublishStatusChangedAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }
}