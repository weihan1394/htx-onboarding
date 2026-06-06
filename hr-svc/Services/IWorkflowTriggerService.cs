using HrService.Models;

namespace HrService.Services;

// abstraction over the HTTP call to workflow-svc — lets tests mock it without spinning up workflow-svc
public interface IWorkflowTriggerService
{
    Task TriggerOnboardingAsync(Employee employee); // POST /api/workflows/onboarding/start
}
