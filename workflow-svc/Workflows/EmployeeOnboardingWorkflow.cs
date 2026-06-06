using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using WorkflowService.Activities;
using WorkflowService.Models;

namespace WorkflowService.Workflows;

[Workflow]
public class EmployeeOnboardingWorkflow
{
    private bool _retrySignaled;

    // signal handler — called by Temporal when hr-svc sends a retry request
    // must be fast and must NOT await anything that can fail; only mutates workflow state
    [WorkflowSignal]
    public Task RetryAsync()
    {
        _retrySignaled = true;
        return Task.CompletedTask;
    }

    [WorkflowRun]
    public async Task RunAsync(OnboardingInput input)
    {
        // 3 attempts, exponential backoff 2s→30s
        var activityOptions = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(5),
            RetryPolicy = new RetryPolicy
            {
                MaximumAttempts    = 3,
                InitialInterval    = TimeSpan.FromSeconds(2),
                BackoffCoefficient = 2.0f,
                MaximumInterval    = TimeSpan.FromSeconds(30),
            }
        };

        // Step 1: create the onboarding record — failure here terminates immediately (outside retry loop)
        var onboardingId = await Workflow.ExecuteActivityAsync(
            (OnboardingActivities act) => act.StartOnboardingRecordAsync(input),
            activityOptions);

        // step flags prevent re-running completed steps when the loop retries
        var accountsDone  = false;
        var equipmentDone = false;

        try
        {
            while (true)
            {
                try
                {
                    if (!accountsDone)
                    {
                        await Workflow.ExecuteActivityAsync(
                            (OnboardingActivities act) => act.CreateAccountsAsync(input, onboardingId),
                            activityOptions);
                        accountsDone = true;
                    }

                    if (!equipmentDone)
                    {
                        await Workflow.ExecuteActivityAsync(
                            (OnboardingActivities act) => act.IssueEquipmentAsync(input, onboardingId),
                            activityOptions);
                        equipmentDone = true;
                    }

                    await Workflow.ExecuteActivityAsync(
                        (OnboardingActivities act) => act.CompleteOnboardingAsync(onboardingId),
                        new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

                    return;
                }
                catch (ActivityFailureException ex)
                {
                    await HandleFailureAndWaitForRetryAsync(onboardingId, ex.GetBaseException().Message);
                }
            }
        }
        catch (Exception ex)
        {
            // FailOnboardingAsync exhausted its own retries, or the 30-min timeout fired.
            // Best-effort: write a terminal failed status so the DB is not left in a stale state.
            var message = ex is ApplicationFailureException afe ? afe.Message : ex.GetBaseException().Message;
            try
            {
                await Workflow.ExecuteActivityAsync(
                    (OnboardingActivities act) => act.FailOnboardingAsync(onboardingId, message),
                    new ActivityOptions
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(1),
                        RetryPolicy = new RetryPolicy { MaximumAttempts = 1 }
                    });
            }
            catch
            {
                // best-effort — swallow so the original exception propagates cleanly
            }
            throw;
        }
    }

    // Marks the workflow as failed, suspends until a retry signal arrives (or 30-min timeout),
    // then resets status so the main loop can continue.
    private async Task HandleFailureAndWaitForRetryAsync(Guid onboardingId, string errorMessage)
    {
        // reset BEFORE any await — a signal arriving during FailOnboardingAsync would
        // set _retrySignaled = true before we reset it, and we'd miss it
        _retrySignaled = false;

        var failOptions = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(2),
            RetryPolicy = new RetryPolicy
            {
                MaximumAttempts    = 5,
                InitialInterval    = TimeSpan.FromSeconds(2),
                BackoffCoefficient = 2.0f,
                MaximumInterval    = TimeSpan.FromSeconds(30),
            }
        };

        await Workflow.ExecuteActivityAsync(
            (OnboardingActivities act) => act.FailOnboardingAsync(onboardingId, errorMessage),
            failOptions);

        // suspends here consuming zero resources — resumes when RetryAsync signal arrives or timeout expires
        var retried = await Workflow.WaitConditionAsync(() => _retrySignaled, TimeSpan.FromMinutes(30));
        if (!retried)
        {
            throw new ApplicationFailureException("Not retried within 30 minutes — workflow failed.", nonRetryable: true);
        }

        await Workflow.ExecuteActivityAsync(
            (OnboardingActivities act) => act.ResetOnboardingStatusAsync(onboardingId),
            failOptions);
    }
}
