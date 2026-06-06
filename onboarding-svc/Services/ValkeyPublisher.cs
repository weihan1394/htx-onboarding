using StackExchange.Redis;
using System.Text.Json;

namespace OnboardingService.Services;

public class ValkeyPublisher : IOnboardingPublisher
{
    // create a pool of connections to Redis, and reuse it for pub/sub
    private readonly IConnectionMultiplexer _mux;

    public ValkeyPublisher(IConnectionMultiplexer mux) => _mux = mux;

    public Task PublishStatusChangedAsync(Guid employeeId, string status)
    {
        var sub = _mux.GetSubscriber();
        // serialize the status change event as JSON, e.g. { "status": "InProgress" }
        var message = JsonSerializer.Serialize(new { status });

        // publish a message to the channel "onboarding:{employeeId}", with the payload containing the new status
        return sub.PublishAsync(
            RedisChannel.Literal($"onboarding:{employeeId}"),
            message);
    }
}
