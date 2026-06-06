using Temporalio.Client;
using WorkflowService.Models;

namespace WorkflowService.Services;

// abstraction over Temporal SDK calls — excluded from coverage in TemporalGateway impl
// mocked in tests so OnboardingWorkflowService tests don't need a live Temporal server
public interface ITemporalGateway
{
    Task StartWorkflowAsync(OnboardingInput input, WorkflowOptions options); // schedule workflow execution
    Task SignalRetryAsync(string workflowId);                                // send RetryAsync signal to paused workflow
}
