using Temporalio.Client;

namespace WorkflowService.Workers;

// bridges DI's eager singleton registration and the async Temporal connection that completes
// later in TemporalWorkerService — controllers check IsReady before using Client so they can
// return 503 during startup instead of throwing NullReferenceException
public class TemporalClientHolder
{
    private ITemporalClient? _client;

    // throws if accessed before TemporalWorkerService has connected
    public ITemporalClient Client
    {
        get
        {
            if (_client is null)
            {
                throw new InvalidOperationException("Temporal client not yet connected.");
            }
            return _client;
        }
        set
        {
            _client = value;
        }
    }

    // safe to check at any time — no throw
    public bool IsReady
    {
        get { return _client is not null; }
    }
}
