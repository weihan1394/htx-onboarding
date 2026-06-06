using WorkflowService.Models;
using WorkflowService.Workers;
using Xunit;

namespace WorkflowService.Tests;

public class ModelTests
{
    [Fact]
    public void OnboardingInput_StoresAllFields()
    {
        var id = Guid.NewGuid();
        var input = new OnboardingInput(id, "EMP-0001", "Alice", "Tan", "alice@htx.gov.sg", "Engineering", "Engineer");
        Assert.Equal(id, input.EmployeeId);
        Assert.Equal("EMP-0001", input.EmployeeNumber);
        Assert.Equal("Alice", input.FirstName);
        Assert.Equal("Tan", input.LastName);
        Assert.Equal("alice@htx.gov.sg", input.Email);
        Assert.Equal("Engineering", input.Department);
        Assert.Equal("Engineer", input.Position);
    }

    [Fact]
    public void OnboardingInput_AllowsNullDepartmentAndPosition()
    {
        var input = new OnboardingInput(Guid.NewGuid(), "EMP-0001", "Alice", "Tan", "alice@htx.gov.sg", null, null);
        Assert.Null(input.Department);
        Assert.Null(input.Position);
    }

    [Fact]
    public void StartWorkflowRequest_StoresAllFields()
    {
        var id = Guid.NewGuid();
        var req = new StartWorkflowRequest(id, "EMP-0001", "Alice", "Tan", "alice@htx.gov.sg", "Engineering", "Engineer");
        Assert.Equal(id, req.EmployeeId);
        Assert.Equal("EMP-0001", req.EmployeeNumber);
        Assert.Equal("Alice", req.FirstName);
        Assert.Equal("Tan", req.LastName);
        Assert.Equal("alice@htx.gov.sg", req.Email);
        Assert.Equal("Engineering", req.Department);
        Assert.Equal("Engineer", req.Position);
    }

}

public class TemporalClientHolderTests
{
    [Fact]
    public void Client_Throws_WhenNotYetSet()
    {
        var holder = new TemporalClientHolder();
        Assert.Throws<InvalidOperationException>(() => _ = holder.Client);
    }

    [Fact]
    public void IsReady_IsFalse_WhenClientNotSet()
    {
        var holder = new TemporalClientHolder();
        Assert.False(holder.IsReady);
    }
}
