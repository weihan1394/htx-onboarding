namespace HrService.Models;

// maps directly to the hr.employees table via Dapper
public class Employee
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty; // EMP-0001, EMP-0002, …
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Position { get; set; }
    public DateOnly HireDate { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // computed — not stored in DB
    public string FullName
    {
        get { return $"{FirstName} {LastName}"; }
    }
}
