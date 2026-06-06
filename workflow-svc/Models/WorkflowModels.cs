namespace WorkflowService.Models;

// passed into EmployeeOnboardingWorkflow.RunAsync — serialised by Temporal into workflow history
public record OnboardingInput(
    Guid EmployeeId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string? Department,
    string? Position
);

// inbound request body for POST /api/workflows/onboarding/start and retry
// contains full employee details so the workflow has everything it needs without calling hr-svc
public record StartWorkflowRequest(
    Guid EmployeeId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string? Department,
    string? Position
);
