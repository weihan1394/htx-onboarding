using OnboardingService.DTOs;
using OnboardingService.Models;
using Xunit;

namespace OnboardingService.Tests;

public class ModelTests
{
    [Fact]
    public void OnboardingRecord_DefaultStatus_IsPending()
    {
        var r = new OnboardingRecord();
        Assert.Equal("pending", r.Status);
    }

    [Fact]
    public void OnboardingRecord_StoresFields()
    {
        var id = Guid.NewGuid();
        var empId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var r = new OnboardingRecord
        {
            OnboardingId = id,
            EmployeeId = empId,
            Status = "completed",
            StartedAt = now,
            CompletedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        Assert.Equal(id, r.OnboardingId);
        Assert.Equal(empId, r.EmployeeId);
        Assert.Equal("completed", r.Status);
        Assert.Equal(now, r.StartedAt);
        Assert.Equal(now, r.CompletedAt);
    }

    [Fact]
    public void AccountCreationTask_DefaultStatus_IsPending()
    {
        var t = new AccountCreationTask();
        Assert.Equal("pending", t.Status);
        Assert.Equal(string.Empty, t.AccountType);
    }

    [Fact]
    public void AccountCreationTask_StoresFields()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var t = new AccountCreationTask
        {
            TaskId = id,
            AccountType = "email",
            Username = "alice.tan@htx.gov.sg",
            Status = "completed",
            CompletedAt = now,
            CreatedAt = now,
        };
        Assert.Equal(id, t.TaskId);
        Assert.Equal("email", t.AccountType);
        Assert.Equal("alice.tan@htx.gov.sg", t.Username);
        Assert.Equal("completed", t.Status);
        Assert.Equal(now, t.CompletedAt);
    }

    [Fact]
    public void EquipmentIssuanceTask_DefaultStatus_IsPending()
    {
        var t = new EquipmentIssuanceTask();
        Assert.Equal("pending", t.Status);
        Assert.Equal(string.Empty, t.ItemType);
    }

    [Fact]
    public void EquipmentIssuanceTask_StoresFields()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var t = new EquipmentIssuanceTask
        {
            TaskId = id,
            ItemType = "laptop",
            ItemDetails = "Dell XPS",
            Status = "issued",
            IssuedAt = now,
            CreatedAt = now,
        };
        Assert.Equal("laptop", t.ItemType);
        Assert.Equal("Dell XPS", t.ItemDetails);
        Assert.Equal("issued", t.Status);
        Assert.Equal(now, t.IssuedAt);
    }
}

public class DtoTests
{
    [Fact]
    public void StartOnboardingRequest_StoresEmployeeId()
    {
        var id = Guid.NewGuid();
        var req = new StartOnboardingRequest(id);
        Assert.Equal(id, req.EmployeeId);
    }

    [Fact]
    public void CreateAccountTasksRequest_StoresFields()
    {
        var id = Guid.NewGuid();
        var req = new CreateAccountTasksRequest(id, "alice@htx.gov.sg", "EMP-0001");
        Assert.Equal(id, req.OnboardingId);
        Assert.Equal("alice@htx.gov.sg", req.EmployeeEmail);
        Assert.Equal("EMP-0001", req.EmployeeNumber);
    }

    [Fact]
    public void IssueEquipmentRequest_StoresFields()
    {
        var id = Guid.NewGuid();
        var req = new IssueEquipmentRequest(id, "Engineering");
        Assert.Equal(id, req.OnboardingId);
        Assert.Equal("Engineering", req.Department);
    }

    [Fact]
    public void AccountTaskResponse_StoresFields()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var r = new AccountTaskResponse(id, "email", "alice.tan@htx.gov.sg", "completed", null, now);
        Assert.Equal(id, r.TaskId);
        Assert.Equal("email", r.AccountType);
        Assert.Equal("alice.tan@htx.gov.sg", r.Username);
        Assert.Equal("completed", r.Status);
        Assert.Null(r.ErrorMessage);
        Assert.Equal(now, r.CompletedAt);
    }

    [Fact]
    public void EquipmentTaskResponse_StoresFields()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var r = new EquipmentTaskResponse(id, "laptop", "Dell", "issued", null, now);
        Assert.Equal(id, r.TaskId);
        Assert.Equal("laptop", r.ItemType);
        Assert.Equal("issued", r.Status);
        Assert.Equal(now, r.IssuedAt);
    }

    [Fact]
    public void EquipmentTaskResponse_AllFields()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var r = new EquipmentTaskResponse(id, "laptop", "Dell XPS", "issued", "err", now);
        Assert.Equal("Dell XPS", r.ItemDetails);
        Assert.Equal("err", r.ErrorMessage);
        Assert.Equal("issued", r.Status);
    }

    [Fact]
    public void OnboardingStatusResponse_StoresFields()
    {
        var id = Guid.NewGuid();
        var empId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var r = new OnboardingStatusResponse(
            id, empId, "completed", now, now, now, 2,
            Array.Empty<AccountTaskResponse>(),
            Array.Empty<EquipmentTaskResponse>(),
            Array.Empty<AttemptHistoryResponse>());
        Assert.Equal(id, r.OnboardingId);
        Assert.Equal(empId, r.EmployeeId);
        Assert.Equal("completed", r.Status);
        Assert.Equal(now, r.StartedAt);
        Assert.Equal(now, r.CompletedAt);
        Assert.Equal(now, r.CreatedAt);
        Assert.Equal(2, r.RetryCount);
        Assert.Empty(r.AccountTasks);
        Assert.Empty(r.EquipmentTasks);
        Assert.Empty(r.RetryHistory);
    }

    [Fact]
    public void AttemptHistoryResponse_StoresAllFields()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var r = new AttemptHistoryResponse(id, 3, "failed", now, "boom");
        Assert.Equal(id, r.HistoryId);
        Assert.Equal(3, r.Attempt);
        Assert.Equal("failed", r.Status);
        Assert.Equal(now, r.AttemptedAt);
        Assert.Equal("boom", r.ErrorMessage);
    }
}

public class ModelExtraTests
{
    [Fact]
    public void AttemptHistoryEntry_StoresAllFields()
    {
        var id = Guid.NewGuid();
        var onbId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var e = new AttemptHistoryEntry
        {
            HistoryId = id,
            OnboardingId = onbId,
            Attempt = 2,
            Status = "failed",
            AttemptedAt = now,
            ErrorMessage = "timeout"
        };
        Assert.Equal(id, e.HistoryId);
        Assert.Equal(onbId, e.OnboardingId);
        Assert.Equal(2, e.Attempt);
        Assert.Equal("failed", e.Status);
        Assert.Equal(now, e.AttemptedAt);
        Assert.Equal("timeout", e.ErrorMessage);
    }

    [Fact]
    public void EquipmentIssuanceTask_AllFields()
    {
        var onbId = Guid.NewGuid();
        var t = new EquipmentIssuanceTask
        {
            OnboardingId = onbId,
            ErrorMessage = "out of stock"
        };
        Assert.Equal(onbId, t.OnboardingId);
        Assert.Equal("out of stock", t.ErrorMessage);
    }
}
