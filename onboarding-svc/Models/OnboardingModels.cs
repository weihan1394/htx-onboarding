namespace OnboardingService.Models;

// maps to onboarding.onboarding_records
public class OnboardingRecord
{
    public Guid OnboardingId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Status { get; set; } = "pending";     // pending | in_progress | completed | failed
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int RetryCount { get; set; }                 // incremented each time a retry signal is processed
}

// maps to onboarding.tasks_accounts
public class AccountCreationTask
{
    public Guid TaskId { get; set; }
    public Guid OnboardingId { get; set; }
    public string AccountType { get; set; } = string.Empty; // email | vpn | hr_portal
    public string? Username { get; set; }
    public string Status { get; set; } = "pending";         // pending | completed | failed
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// maps to onboarding.tasks_equipment
public class EquipmentIssuanceTask
{
    public Guid TaskId { get; set; }
    public Guid OnboardingId { get; set; }
    public string ItemType { get; set; } = string.Empty;    // laptop | staff_pass | welcome_kit
    public string? ItemDetails { get; set; }                // JSON string (model, access_level, kit contents)
    public string Status { get; set; } = "pending";         // pending | issued | failed
    public string? ErrorMessage { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// maps to onboarding.onboarding_transactions — one row per terminal attempt
public class AttemptHistoryEntry
{
    public Guid HistoryId { get; set; }
    public Guid OnboardingId { get; set; }
    public int Attempt { get; set; }
    public string Status { get; set; } = "failed";          // failed | completed
    public DateTime AttemptedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
