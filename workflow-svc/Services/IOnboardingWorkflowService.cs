using WorkflowService.Models;

namespace WorkflowService.Services;

// abstraction over Temporal workflow operations — lets tests mock without a live Temporal server
public interface IOnboardingWorkflowService
{
    bool IsReady { get; }                                                       // true once Temporal client connected
    Task<string> StartAsync(StartWorkflowRequest request);                     // schedule new workflow; throws WorkflowAlreadyStartedException on duplicate
    Task<string> RetryAsync(string employeeId, StartWorkflowRequest request);  // signal paused workflow; start fresh if not running
}
