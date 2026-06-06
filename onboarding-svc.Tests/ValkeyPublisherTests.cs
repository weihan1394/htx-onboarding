using Moq;
using OnboardingService.Services;
using StackExchange.Redis;
using Xunit;

namespace OnboardingService.Tests;

public class ValkeyPublisherTests
{
    [Fact]
    public async Task PublishStatusChangedAsync_PublishesToCorrectChannel()
    {
        var employeeId = Guid.NewGuid();
        var sub = new Mock<ISubscriber>();
        sub.Setup(s => s.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(0);
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetSubscriber(It.IsAny<object?>())).Returns(sub.Object);

        var publisher = new ValkeyPublisher(mux.Object);
        await publisher.PublishStatusChangedAsync(employeeId, "completed");

        sub.Verify(s => s.PublishAsync(
            It.Is<RedisChannel>(c => c.ToString() == $"onboarding:{employeeId}"),
            It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task PublishStatusChangedAsync_MessageContainsStatus()
    {
        var employeeId = Guid.NewGuid();
        RedisValue capturedMessage = default;
        var sub = new Mock<ISubscriber>();
        sub.Setup(s => s.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
           .Callback<RedisChannel, RedisValue, CommandFlags>((_, msg, _) => capturedMessage = msg)
           .ReturnsAsync(0);
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetSubscriber(It.IsAny<object?>())).Returns(sub.Object);

        var publisher = new ValkeyPublisher(mux.Object);
        await publisher.PublishStatusChangedAsync(employeeId, "accounts_created");

        Assert.Contains("accounts_created", capturedMessage.ToString());
    }
}
