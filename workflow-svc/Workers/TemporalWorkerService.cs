using System.Diagnostics.CodeAnalysis;
using Temporalio.Client;
using Temporalio.Worker;
using WorkflowService.Activities;
using WorkflowService.Workflows;

namespace WorkflowService.Workers;

// background service that connects to Temporal and runs the worker loop
// excluded from coverage — infrastructure code with no business logic
[ExcludeFromCodeCoverage]
public class TemporalWorkerService : BackgroundService
{
    private readonly TemporalClientConnectOptions _connectOptions;
    private readonly TemporalClientHolder _holder;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TemporalWorkerService> _logger;

    // all workflows and activities registered to this queue — must match what StartWorkflowAsync uses
    public const string TaskQueue = "onboarding-task-queue";

    public TemporalWorkerService(
        TemporalClientConnectOptions connectOptions,
        TemporalClientHolder holder,
        IServiceProvider serviceProvider,
        ILogger<TemporalWorkerService> logger)
    {
        _connectOptions = connectOptions;
        _holder = holder;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // retry loop until Temporal server is reachable (handles slow startup order)
        var client = await ConnectWithRetryAsync(stoppingToken);
        if (client is null)
        {
            return; // cancellation requested during connect — shut down cleanly
        }

        // store client in holder so IsReady flips to true and controllers unblock
        _holder.Client = client;

        _logger.LogInformation("Temporal connected. Starting worker on task queue: {TaskQueue}", TaskQueue);

        var activities = _serviceProvider.GetRequiredService<OnboardingActivities>();

        // outer loop so the worker restarts if the namespace disappears at runtime (e.g. Temporal redeploy)
        while (!stoppingToken.IsCancellationRequested)
        {
            using var worker = new TemporalWorker(
                client,
                new TemporalWorkerOptions(TaskQueue)
                    .AddWorkflow<EmployeeOnboardingWorkflow>()
                    .AddAllActivities(activities)
            );

            try
            {
                _logger.LogInformation("Temporal worker started.");
                await worker.ExecuteAsync(stoppingToken);
                return; // clean shutdown via cancellation
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Temporal worker stopped.");
                return;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found") && !stoppingToken.IsCancellationRequested)
            {
                // namespace bootstrap not complete yet — wait and retry
                _logger.LogWarning("Temporal namespace not found: {Message}. Retrying in 10s...", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    // exponential backoff capped at 30s: 2, 4, 8, 16, 30, 30, …
    private async Task<TemporalClient?> ConnectWithRetryAsync(CancellationToken ct)
    {
        var attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Connecting to Temporal at {Host} (attempt {Attempt})",
                    _connectOptions.TargetHost, ++attempt);
                return await TemporalClient.ConnectAsync(_connectOptions);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                _logger.LogWarning("Temporal connection failed: {Message}. Retrying in {Delay}s...",
                    ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
        return null;
    }
}
