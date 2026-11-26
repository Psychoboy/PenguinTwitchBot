using Microsoft.AspNetCore.SignalR;

namespace PenguinTwitchBot.Bot.Hubs;

public sealed class InProcessBroadcastingHubContext<THub> : IHubContext<THub>
    where THub : Hub
{
    private readonly IHubContext<THub> _inner;
    private readonly InProcessBroadcastingHubClients _clients;

    public InProcessBroadcastingHubContext(IHubContext<THub> inner, IInProcessHubEventBus eventBus)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ArgumentNullException.ThrowIfNull(eventBus);
        _clients = new InProcessBroadcastingHubClients(inner.Clients, eventBus, typeof(THub).Name);
    }

    public IHubClients Clients => _clients;

    public IGroupManager Groups => _inner.Groups;

    private sealed class InProcessBroadcastingHubClients : IHubClients
    {
        private readonly IHubClients _inner;
        private readonly IInProcessHubEventBus _eventBus;
        private readonly string _hubName;

        public InProcessBroadcastingHubClients(IHubClients inner, IInProcessHubEventBus eventBus, string hubName)
        {
            _inner = inner;
            _eventBus = eventBus;
            _hubName = hubName;
        }

        public IClientProxy All => new InProcessBroadcastingClientProxy(_inner.All, _eventBus, _hubName);

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) =>
            new InProcessBroadcastingClientProxy(_inner.AllExcept(excludedConnectionIds), _eventBus, _hubName);

        public IClientProxy Client(string connectionId) => _inner.Client(connectionId);

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _inner.Clients(connectionIds);

        public IClientProxy Group(string groupName) => _inner.Group(groupName);

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) =>
            _inner.GroupExcept(groupName, excludedConnectionIds);

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _inner.Groups(groupNames);

        public IClientProxy User(string userId) => _inner.User(userId);

        public IClientProxy Users(IReadOnlyList<string> userIds) => _inner.Users(userIds);
    }

    private sealed class InProcessBroadcastingClientProxy : IClientProxy
    {
        private readonly IClientProxy _inner;
        private readonly IInProcessHubEventBus _eventBus;
        private readonly string _hubName;

        public InProcessBroadcastingClientProxy(IClientProxy inner, IInProcessHubEventBus eventBus, string hubName)
        {
            _inner = inner;
            _eventBus = eventBus;
            _hubName = hubName;
        }

        public async Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            await _inner.SendCoreAsync(method, args, cancellationToken);
            await _eventBus.PublishAsync(_hubName, method, args, cancellationToken);
        }
    }
}

public static class InProcessHubBridgeServiceCollectionExtensions
{
    public static IServiceCollection AddInProcessHubBridge<THub>(this IServiceCollection services)
        where THub : Hub
    {
        ArgumentNullException.ThrowIfNull(services);

        var openHubContextDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IHubContext<>) && d.ImplementationType is not null);

        if (openHubContextDescriptor?.ImplementationType is null)
        {
            throw new InvalidOperationException(
                "SignalR services must be registered via AddSignalR() before calling AddInProcessHubBridge<THub>().");
        }

        var closedHubContextType = openHubContextDescriptor.ImplementationType.MakeGenericType(typeof(THub));

        services.AddSingleton<IHubContext<THub>>(sp =>
        {
            var inner = (IHubContext<THub>)ActivatorUtilities.CreateInstance(sp, closedHubContextType);
            var eventBus = sp.GetRequiredService<IInProcessHubEventBus>();
            return new InProcessBroadcastingHubContext<THub>(inner, eventBus);
        });

        return services;
    }
}
