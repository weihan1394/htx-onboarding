namespace OnboardingService.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

// POST /api/onboarding/start
public record StartOnboardingRequest(Guid EmployeeId);

// PATCH /api/onboarding/{id}/status
// IncrementRetryCount = true when resetting after a retry signal
public record UpdateOnboardingStatusRequest(
    string Status,
    bool IncrementRetryCount = false,
    string? ErrorMessage = null
);

// POST /api/onboarding/{id}/accounts/create
public record CreateAccountTasksRequest(
    Guid OnboardingId,
    string EmployeeEmail,
    string EmployeeNumber
);

// POST /api/onboarding/{id}/equipment/issue
// department determines which laptop model is assigned
public record IssueEquipmentRequest(
    Guid OnboardingId,
    string? Department
);

// ── Responses ─────────────────────────────────────────────────────────────────

// full onboarding status returned to hr-web — includes tasks and attempt history
public record OnboardingStatusResponse(
    Guid OnboardingId,
    Guid EmployeeId,
    string Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    int RetryCount,
    IEnumerable<AccountTaskResponse> AccountTasks,
    IEnumerable<EquipmentTaskResponse> EquipmentTasks,
    IEnumerable<AttemptHistoryResponse> RetryHistory
);

// one entry per workflow attempt (failed or completed)
public record AttemptHistoryResponse(
    Guid HistoryId,
    int Attempt,
    string Status,
    DateTime AttemptedAt,
    string? ErrorMessage
);

// individual account creation task (email / VPN / hr_portal)
public record AccountTaskResponse(
    Guid TaskId,
    string AccountType,
    string? Username,
    string Status,
    string? ErrorMessage,
    DateTime? CompletedAt
);

// individual equipment issuance task (laptop / staff_pass / welcome_kit)
public record EquipmentTaskResponse(
    Guid TaskId,
    string ItemType,
    string? ItemDetails,    // JSON string with model/access_level/kit contents
    string Status,
    string? ErrorMessage,
    DateTime? IssuedAt
);
