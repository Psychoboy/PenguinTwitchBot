using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using PenguinTwitchBot.Bot.TwitchServices;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.TwitchServices;

public class ChatMessageIdTrackerTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly ChatMessageIdTracker _tracker;

    public ChatMessageIdTrackerTests()
    {
        _memoryCache = Substitute.For<IMemoryCache>();
        _tracker = new ChatMessageIdTracker(_memoryCache);
    }

    [Fact]
    public void AddMessageId_DoesNotThrow()
    {
        _tracker.AddMessageId("msg-123");
    }

    [Fact]
    public void IsSelfMessage_WhenCacheHit_ReturnsTrue()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()).Returns(x =>
        {
            x[1] = "cached-value";
            return true;
        });

        bool result = _tracker.IsSelfMessage("msg-123");

        Assert.True(result);
    }

    [Fact]
    public void IsSelfMessage_WhenCacheMiss_ReturnsFalse()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()).Returns(false);

        bool result = _tracker.IsSelfMessage("msg-123");

        Assert.False(result);
    }

    [Fact]
    public void IsSelfMessage_WithNullMessageId_ReturnsFalse()
    {
        _memoryCache.TryGetValue(Arg.Any<object>(), out Arg.Any<object>()).Returns(false);

        bool result = _tracker.IsSelfMessage(null!);

        Assert.False(result);
    }
}
