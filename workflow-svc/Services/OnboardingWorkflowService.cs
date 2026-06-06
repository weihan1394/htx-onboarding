using Temporalio.Client;
using WorkflowService.Models;
using WorkflowService.Workers;

namespace WorkflowService.Services;

public class OnboardingWorkflowService : IOnboardingWorkflowService
{
    private readonly TemporalClientHolder _holder;
    private readonly ITemporalGateway _gateway;
    private readonly ILogger<OnboardingWorkflowService> _logger;

    public OnboardingWorkflowService(
        TemporalClientHolder holder,
        ITemporalGateway gateway,
        ILogger<OnboardingWorkflowService> logger)
    {
        _holder = holder;
        _gateway = gateway;
        _logger = logger;
    }

    // true once TemporalWorkerService has connected — controllers check before calling
    public bool IsReady
    {
        get { return _holder.IsReady; }
    }

    // builds workflow options (memo + search attributes) and schedules the workflow via Temporal SDK
    // workflowId = "onboarding-{employeeId}" — deterministic; prevents duplicate parallel runs
    public async Task<string> StartAsync(StartWorkflowRequest request)
    {
        var workflowId = $"onboarding-{request.EmployeeId}";

        var input = new OnboardingInput(
            request.EmployeeId,
            request.EmployeeNumber,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Department,
            request.Position
        );

        // memo — human-readable metadata visible in Temporal UI without opening the workflow
        var memo = new Dictionary<string, object>
        {
            ["employeeName"]   = $"{request.FirstName} {request.LastName}",
            ["employeeNumber"] = request.EmployeeNumber ?? "",
            ["email"]          = request.Email,
            ["department"]     = request.Department ?? ""
        };

        // typed search attributes — enable filtering in Temporal UI (e.g. Department = "Engineering")
        var searchAttributes = new Temporalio.Common.SearchAttributeCollection.Builder()
            .Set(Temporalio.Common.SearchAttributeKey.CreateText("EmployeeName"),
                $"{request.FirstName} {request.LastName}")
            .Set(Temporalio.Common.SearchAttributeKey.CreateKeyword("EmployeeNumber"),
                request.EmployeeNumber ?? "")
            .Set(Temporalio.Common.SearchAttributeKey.CreateKeyword("Department"),
                request.Department ?? "")
            .ToSearchAttributeCollection();

        var options = new WorkflowOptions(workflowId, TemporalWorkerService.TaskQueue)
        {
            Memo = memo,
            TypedSearchAttributes = searchAttributes
        };

        await _gateway.StartWorkflowAsync(input, options);

        _logger.LogInformation("Workflow started: {WorkflowId}", workflowId);
        return workflowId;
    }

    // Step 1: try to signal the paused workflow — resumes from the failed step, no duplicate work
    // Step 2: fallback — workflow not running (timed out or never started); start a fresh execution
    //         StartOnboardingRecordAsync handles the 409 if the record already exists
    public async Task<string> RetryAsync(string employeeId, StartWorkflowRequest request)
    {
        var workflowId = $"onboarding-{employeeId}";

        try
        {
            await _gateway.SignalRetryAsync(workflowId);
            _logger.LogInformation("Retry signal sent to workflow {WorkflowId}", workflowId);
        }
        catch (Exception)
        {
            // workflow not running (e.g. timed out) — start fresh
            _logger.LogWarning("Workflow {WorkflowId} not running — starting fresh execution", workflowId);

            var input = new OnboardingInput(
                request.EmployeeId,
                request.EmployeeNumber,
                request.FirstName,
                request.LastName,
                request.Email,
                request.Department,
                request.Position
            );

            await _gateway.StartWorkflowAsync(
                input,
                new WorkflowOptions(workflowId, TemporalWorkerService.TaskQueue)
            );
        }

        return workflowId;
    }
}
