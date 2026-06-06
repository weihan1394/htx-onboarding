using System.ComponentModel.DataAnnotations;

namespace HrService.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

// inbound from hr-web when creating a new employee
public record CreateEmployeeRequest(
    [Required, MinLength(1), MaxLength(100)] string FirstName,
    [Required, MinLength(1), MaxLength(100)] string LastName,
    [Required, EmailAddress, MaxLength(255)] string Email,
    [MaxLength(100)] string? Department,
    [MaxLength(100)] string? Position,
    DateOnly HireDate
);

// outbound to hr-web after successful employee creation
public record EmployeeResponse(
    Guid EmployeeId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Department,
    string? Position,
    DateOnly HireDate,
    string Status,
    DateTime CreatedAt
);

// sent to workflow-svc to start the onboarding workflow
public record TriggerOnboardingRequest(
    Guid EmployeeId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string? Department,
    string? Position
);

// PATCH /api/hr/employees/{id}/status body
public record UpdateStatusRequest(string Status);
