using Microsoft.AspNetCore.SignalR.Client;
using PenguinTwitchBot.Bot.Hubs;
using System;
using System.Threading.Tasks;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Hubs;

public class SignalRHubConnectionFactoryTests
{
    [Fact]
    public void CreateMainHubConnection_ReturnsNonNullWrapper()
    {
        var factory = new SignalRHubConnectionFactory();
        var uri = new Uri("http://localhost/mainHub");

        var result = factory.CreateMainHubConnection(uri);

        Assert.NotNull(result);
    }

    [Fact]
    public void CreateMainHubConnection_ReturnsSignalRHubConnectionWrapper()
    {
        var factory = new SignalRHubConnectionFactory();
        var uri = new Uri("http://localhost/mainHub");

        var result = factory.CreateMainHubConnection(uri);

        Assert.IsType<SignalRHubConnectionWrapper>(result);
    }

    [Fact]
    public void CreateMainHubConnection_ReturnsObjectImplementingISignalRHubConnection()
    {
        var factory = new SignalRHubConnectionFactory();
        var uri = new Uri("http://localhost/mainHub");

        var result = factory.CreateMainHubConnection(uri);

        Assert.IsAssignableFrom<ISignalRHubConnection>(result);
    }
}
