using HrService.DTOs;
using HrService.Models;
using Xunit;

namespace HrService.Tests;

public class ModelAndDtoTests
{
    [Fact]
    public void Employee_FullName_ConcatenatesFirstAndLast()
    {
        var emp = new Employee { FirstName = "Alice", LastName = "Tan" };
        Assert.Equal("Alice Tan", emp.FullName);
    }

    [Fact]
    public void Employee_DefaultStatus_IsActive()
    {
        var emp = new Employee();
        Assert.Equal("active", emp.Status);
    }

    [Fact]
    public void CreateEmployeeRequest_StoresAllFields()
    {
        var hireDate = new DateOnly(2026, 6, 1);
        var req = new CreateEmployeeRequest("Alice", "Tan", "alice@htx.gov.sg", "Engineering", "Engineer", hireDate);
        Assert.Equal("Alice", req.FirstName);
        Assert.Equal("Tan", req.LastName);
        Assert.Equal("alice@htx.gov.sg", req.Email);
        Assert.Equal("Engineering", req.Department);
        Assert.Equal("Engineer", req.Position);
        Assert.Equal(hireDate, req.HireDate);
    }

    [Fact]
    public void EmployeeResponse_StoresAllFields()
    {
        var id = Guid.NewGuid();
        var hireDate = new DateOnly(2026, 6, 1);
        var created = DateTime.UtcNow;
        var resp = new EmployeeResponse(id, "EMP-0001", "Alice", "Tan", "Alice Tan",
            "alice@htx.gov.sg", "Engineering", "Engineer", hireDate, "active", created);
        Assert.Equal(id, resp.EmployeeId);
        Assert.Equal("EMP-0001", resp.EmployeeNumber);
        Assert.Equal("Alice Tan", resp.FullName);
        Assert.Equal("active", resp.Status);
    }

    [Fact]
    public void TriggerOnboardingRequest_AllowsNullDepartmentAndPosition()
    {
        var req = new TriggerOnboardingRequest(Guid.NewGuid(), "EMP-0001", "Alice", "Tan", "alice@htx.gov.sg", null, null);
        Assert.Null(req.Department);
        Assert.Null(req.Position);
    }

    [Fact]
    public void Employee_AllProperties_AreReadable()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var emp = new Employee
        {
            EmployeeId = id,
            EmployeeNumber = "EMP-0001",
            FirstName = "Alice",
            LastName = "Tan",
            Email = "alice@htx.gov.sg",
            Department = "Engineering",
            Position = "Engineer",
            HireDate = new DateOnly(2026, 6, 1),
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now
        };
        Assert.Equal(id, emp.EmployeeId);
        Assert.Equal("EMP-0001", emp.EmployeeNumber);
        Assert.Equal("alice@htx.gov.sg", emp.Email);
        Assert.Equal("Engineering", emp.Department);
        Assert.Equal("Engineer", emp.Position);
        Assert.Equal(new DateOnly(2026, 6, 1), emp.HireDate);
        Assert.Equal(now, emp.CreatedAt);
        Assert.Equal(now, emp.UpdatedAt);
    }

    [Fact]
    public void EmployeeResponse_AllProperties_AreReadable()
    {
        var id = Guid.NewGuid();
        var hireDate = new DateOnly(2026, 6, 1);
        var created = DateTime.UtcNow;
        var resp = new EmployeeResponse(id, "EMP-0001", "Alice", "Tan", "Alice Tan",
            "alice@htx.gov.sg", "Engineering", "Engineer", hireDate, "active", created);
        Assert.Equal(id, resp.EmployeeId);
        Assert.Equal("EMP-0001", resp.EmployeeNumber);
        Assert.Equal("Alice", resp.FirstName);
        Assert.Equal("Tan", resp.LastName);
        Assert.Equal("Alice Tan", resp.FullName);
        Assert.Equal("alice@htx.gov.sg", resp.Email);
        Assert.Equal("Engineering", resp.Department);
        Assert.Equal("Engineer", resp.Position);
        Assert.Equal(hireDate, resp.HireDate);
        Assert.Equal("active", resp.Status);
        Assert.Equal(created, resp.CreatedAt);
    }
}
