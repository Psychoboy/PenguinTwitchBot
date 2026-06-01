using Microsoft.Extensions.DependencyInjection;
using TwitchLib.EventSub.Websockets.Extensions;

namespace PenguinTwitchBot.TwitchApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPenguinTwitchApiEventSub(this IServiceCollection services)
    {
        services.AddTwitchLibEventSubWebsockets();
        return services;
    }
}
