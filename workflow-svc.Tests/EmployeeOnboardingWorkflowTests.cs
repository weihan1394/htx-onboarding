using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;
using WorkflowService.Models;
using WorkflowService.Workflows;
using Xunit;

namespace WorkflowService.Tests;

public sealed class WorkflowEnvironmentFixture : IAsyncLifetime
{
    public WorkflowEnvironment Environment { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Environment = await WorkflowEnvironment.StartTimeSkippingAsync();
    }

    public async Task DisposeAsync()
    {
        await Environment.DisposeAsync();
    }
}

public class EmployeeOnboardingWorkflowTests : IClassFixture<WorkflowEnvironmentFixture>
{
    private readonly WorkflowEnvironment _env;

    private static readonly OnboardingInput TestInput = new(
        EmployeeId: Guid.NewGuid(),
        EmployeeNumber: "EMP001",
        FirstName: "Alice",
        LastName: "Tan",
        Email: "alice.tan@htx.gov.sg",
        Department: "Engineering",
        Position: "Engineer"
    );

    public EmployeeOnboardingWorkflowTests(WorkflowEnvironmentFixture fixture)
    {
        _env = fixture.Environment;
    }

    private TemporalWorker BuildWorker(MockActivities activities, string taskQueue)
    {
        return new(_env.Client, new TemporalWorkerOptions(taskQueue)
            .AddWorkflow<EmployeeOnboardingWorkflow>()
            .AddAllActivities(activities));
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_HappyPath_CallsAllActivitiesInOrder()
    {
        var activities = new MockActivities();
        var taskQueue = Guid.NewGuid().ToString();

        using var worker = BuildWorker(activities, taskQueue);
        await worker.ExecuteAsync(async () =>
        {
            await _env.Client.ExecuteWorkflowAsync(
                (EmployeeOnboardingWorkflow wf) => wf.RunAsync(TestInput),
                new WorkflowOptions { Id = Guid.NewGuid().ToString(), TaskQueue = taskQueue });
        });

        var calls = activities.Calls.ToList();
        Assert.Equal(4, calls.Count);
        Assert.Equal(nameof(MockActivities.StartOnboardingRecordAsync), calls[0]);
        Assert.Equal(nameof(MockActivities.CreateAccountsAsync), calls[1]);
        Assert.Equal(nameof(MockActivities.IssueEquipmentAsync), calls[2]);
        Assert.Equal(nameof(MockActivities.CompleteOnboardingAsync), calls[3]);
    }

    // ── Signal retry — resumes from failed step, skips completed steps ────────

    [Fact]
    public async Task RunAsync_AfterRetrySignal_SkipsCompletedStepsAndResumes()
    {
        var activities = new MockActivities { FailAt = nameof(MockActivities.IssueEquipmentAsync), FailOnce = true };
        var taskQueue = Guid.NewGuid().ToString();
        var workflowId = Guid.NewGuid().ToString();

        using var worker = BuildWorker(activities, taskQueue);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync(
                (EmployeeOnboardingWorkflow wf) => wf.RunAsync(TestInput),
                new WorkflowOptions { Id = workflowId, TaskQueue = taskQueue });

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!activities.Calls.Contains(nameof(MockActivities.FailOnboardingAsync)))
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("FailOnboarding was not called within 30 seconds");
                await Task.Delay(50);
            }

            await handle.SignalAsync((EmployeeOnboardingWorkflow wf) => wf.RetryAsync());
            await handle.GetResultAsync();
        });

        var calls = activities.Calls.ToList();
        // CreateAccounts ran once — not re-run after retry
        Assert.Equal(1, calls.Count(c => c == nameof(MockActivities.CreateAccountsAsync)));
        // IssueEquipment ran twice — first failed, second succeeded
        Assert.Equal(2, calls.Count(c => c == nameof(MockActivities.IssueEquipmentAsync)));
        Assert.Contains(nameof(MockActivities.ResetOnboardingStatusAsync), calls);
        Assert.Contains(nameof(MockActivities.CompleteOnboardingAsync), calls);
    }

    // ── Failure path — FailOnboarding is called ───────────────────────────────

    [Fact]
    public async Task RunAsync_WhenCreateAccountsFails_CallsFailOnboarding()
    {
        var activities = new MockActivities { FailAt = nameof(MockActivities.CreateAccountsAsync) };
        var taskQueue = Guid.NewGuid().ToString();

        using var worker = BuildWorker(activities, taskQueue);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync(
                (EmployeeOnboardingWorkflow wf) => wf.RunAsync(TestInput),
                new WorkflowOptions { Id = Guid.NewGuid().ToString(), TaskQueue = taskQueue });

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!activities.Calls.Contains(nameof(MockActivities.FailOnboardingAsync)))
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("FailOnboarding was not called within 30 seconds");
                await Task.Delay(50);
            }

            await handle.TerminateAsync("test complete");
        });

        Assert.Contains(nameof(MockActivities.FailOnboardingAsync), activities.Calls);
        Assert.DoesNotContain(nameof(MockActivities.CompleteOnboardingAsync), activities.Calls);
    }

    [Fact]
    public async Task RunAsync_WhenIssueEquipmentFails_CallsFailOnboarding()
    {
        var activities = new MockActivities { FailAt = nameof(MockActivities.IssueEquipmentAsync) };
        var taskQueue = Guid.NewGuid().ToString();

        using var worker = BuildWorker(activities, taskQueue);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync(
                (EmployeeOnboardingWorkflow wf) => wf.RunAsync(TestInput),
                new WorkflowOptions { Id = Guid.NewGuid().ToString(), TaskQueue = taskQueue });

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!activities.Calls.Contains(nameof(MockActivities.FailOnboardingAsync)))
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("FailOnboarding was not called within 30 seconds");
                await Task.Delay(50);
            }

            await handle.TerminateAsync("test complete");
        });

        Assert.Contains(nameof(MockActivities.CreateAccountsAsync), activities.Calls);
        Assert.Contains(nameof(MockActivities.FailOnboardingAsync), activities.Calls);
        Assert.DoesNotContain(nameof(MockActivities.CompleteOnboardingAsync), activities.Calls);
    }

    // ── StartOnboarding failure — should NOT call FailOnboarding ─────────────

    [Fact]
    public async Task RunAsync_WhenStartOnboardingFails_DoesNotCallFailOnboarding()
    {
        var activities = new MockActivities { FailAt = nameof(MockActivities.StartOnboardingRecordAsync) };
        var taskQueue = Guid.NewGuid().ToString();

        using var worker = BuildWorker(activities, taskQueue);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync(
                (EmployeeOnboardingWorkflow wf) => wf.RunAsync(TestInput),
                new WorkflowOptions { Id = Guid.NewGuid().ToString(), TaskQueue = taskQueue });

            await Assert.ThrowsAsync<WorkflowFailedException>(() => handle.GetResultAsync());
        });

        Assert.DoesNotContain(nameof(MockActivities.FailOnboardingAsync), activities.Calls);
    }

    // ── FailOnboarding receives the correct onboardingId ─────────────────────

    [Fact]
    public async Task RunAsync_FailOnboarding_IsCalledWithCorrectOnboardingId()
    {
        var activities = new MockActivities { FailAt = nameof(MockActivities.IssueEquipmentAsync) };
        var taskQueue = Guid.NewGuid().ToString();

        using var worker = BuildWorker(activities, taskQueue);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await _env.Client.StartWorkflowAsync(
                (EmployeeOnboardingWorkflow wf) => wf.RunAsync(TestInput),
                new WorkflowOptions { Id = Guid.NewGuid().ToString(), TaskQueue = taskQueue });

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!activities.Calls.Contains(nameof(MockActivities.FailOnboardingAsync)))
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("FailOnboarding was not called within 30 seconds");
                await Task.Delay(50);
            }

            await handle.TerminateAsync("test complete");
        });

        Assert.Equal(activities.OnboardingId, activities.FailedOnboardingId);
    }
}

// ── Mock activities ───────────────────────────────────────────────────────────

internal sealed class MockActivities
{
    public Guid OnboardingId { get; } = Guid.NewGuid();

    private readonly List<string> _calls = [];
    public IReadOnlyList<string> Calls => _calls;

    public string? FailAt { get; set; }
    public bool FailOnce { get; set; }
    public Guid FailedOnboardingId { get; private set; }

    private int _failCount;

    private void MaybeThrow(string activityName)
    {
        if (FailAt != activityName)
        {
            return;
        }

        if (FailOnce && Interlocked.Increment(ref _failCount) > 1)
        {
            return;
        }

        // nonRetryable so Temporal doesn't retry at the activity level —
        // the ActivityFailureException propagates immediately to the workflow's catch block
        throw new ApplicationFailureException("Simulated failure", nonRetryable: true);
    }

    [Activity]
    public Task<Guid> StartOnboardingRecordAsync(OnboardingInput input)
    {
        _calls.Add(nameof(StartOnboardingRecordAsync));
        MaybeThrow(nameof(StartOnboardingRecordAsync));
        return Task.FromResult(OnboardingId);
    }

    [Activity]
    public Task CreateAccountsAsync(OnboardingInput input, Guid onboardingId)
    {
        _calls.Add(nameof(CreateAccountsAsync));
        MaybeThrow(nameof(CreateAccountsAsync));
        return Task.CompletedTask;
    }

    [Activity]
    public Task IssueEquipmentAsync(OnboardingInput input, Guid onboardingId)
    {
        _calls.Add(nameof(IssueEquipmentAsync));
        MaybeThrow(nameof(IssueEquipmentAsync));
        return Task.CompletedTask;
    }

    [Activity]
    public Task ResetOnboardingStatusAsync(Guid onboardingId)
    {
        _calls.Add(nameof(ResetOnboardingStatusAsync));
        return Task.CompletedTask;
    }

    [Activity]
    public Task CompleteOnboardingAsync(Guid onboardingId)
    {
        _calls.Add(nameof(CompleteOnboardingAsync));
        return Task.CompletedTask;
    }

    [Activity]
    public Task FailOnboardingAsync(Guid onboardingId, string errorMessage)
    {
        _calls.Add(nameof(FailOnboardingAsync));
        FailedOnboardingId = onboardingId;
        return Task.CompletedTask;
    }
}
