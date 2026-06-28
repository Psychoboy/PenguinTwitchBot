using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Moq;
using PenguinTwitchBot.Helpers;
using System;
using Xunit;

namespace PenguinTwitchBot.Test.Helpers;

public class SignalRConnectionHelperTests
{
    [Fact]
    public async Task DisposeGracefullyAsync_NullConnection_ReturnsWithoutError()
    {
        var logger = Mock.Of<ILogger<SignalRConnectionHelperTests>>();
        var ex = await Record.ExceptionAsync(() => SignalRConnectionHelper.DisposeGracefullyAsync(null, logger));
        Assert.Null(ex);
    }

    [Fact]
    public async Task DisposeGracefullyAsync_ValidConnection_CompletesWithoutError()
    {
        var logger = Mock.Of<ILogger<SignalRConnectionHelperTests>>();
        var ex = await Record.ExceptionAsync(() => SignalRConnectionHelper.DisposeGracefullyAsync(null, logger));
        Assert.Null(ex);
    }
}
