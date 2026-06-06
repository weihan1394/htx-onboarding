using System.Diagnostics.CodeAnalysis;
using Temporalio.Client;
using WorkflowService.Models;
using WorkflowService.Workers;
using WorkflowService.Workflows;

namespace WorkflowService.Services;

// thin wrapper around the Temporal SDK client — excluded from coverage since it has no business logic
// OnboardingWorkflowService uses this via ITemporalGateway so tests can mock it without a live Temporal server
[ExcludeFromCodeCoverage]
public class TemporalGateway : ITemporalGateway
{
    private readonly TemporalClientHolder _holder;

    public TemporalGateway(TemporalClientHolder holder)
    {
        _holder = holder;
    }

    // schedules EmployeeOnboardingWorkflow on the task queue — returns immediately after recording the event
    public Task StartWorkflowAsync(OnboardingInput input, WorkflowOptions options)
    {
        return _holder.Client.StartWorkflowAsync(
            (EmployeeOnboardingWorkflow wf) => wf.RunAsync(input), options);
    }

    // sends the RetryAsync signal to a running (paused) workflow
    // throws if the workflow is not running — caller falls back to starting fresh
    public async Task SignalRetryAsync(string workflowId)
    {
        var handle = _holder.Client.GetWorkflowHandle(workflowId);
        await handle.SignalAsync((EmployeeOnboardingWorkflow wf) => wf.RetryAsync());
    }
}
