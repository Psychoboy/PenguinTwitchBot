using System.Collections.Concurrent;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Hubs;

public sealed class InProcessHubEventBus : IInProcessHubEventBus
{
    private readonly ILogger<InProcessHubEventBus> _logger;
    private readonly ConcurrentDictionary<HubEventKey, ConcurrentDictionary<long, Func<object?[], Task>>> _subscribers = new(HubEventKeyComparer.Instance);
    private long _nextSubscriptionId;

    public InProcessHubEventBus(ILogger<InProcessHubEventBus> logger)
    {
        _logger = logger;
    }

    public IDisposable Subscribe(string hubName, string methodName, Func<object?[], Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hubName);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(handler);

        var key = new HubEventKey(hubName, methodName);
        var subscriptionId = Interlocked.Increment(ref _nextSubscriptionId);
        var bucket = _subscribers.GetOrAdd(key, static _ => new ConcurrentDictionary<long, Func<object?[], Task>>());
        bucket[subscriptionId] = handler;

        return new Subscription(this, key, subscriptionId);
    }

    public async Task PublishAsync(string hubName, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hubName) || string.IsNullOrWhiteSpace(methodName))
        {
            return;
        }

        var key = new HubEventKey(hubName, methodName);
        if (!_subscribers.TryGetValue(key, out var bucket) || bucket.IsEmpty)
        {
            return;
        }

        var safeArgs = args ?? [];

        foreach (var entry in bucket)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await entry.Value(safeArgs).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                _logger.LogDebug("In-process hub subscriber for {Hub}.{Method} was disposed.", hubName, methodName);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("In-process hub subscriber for {Hub}.{Method} was canceled.", hubName, methodName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in in-process hub subscriber for {Hub}.{Method}", hubName, methodName);
            }
        }
    }

    private void Unsubscribe(HubEventKey key, long subscriptionId)
    {
        if (_subscribers.TryGetValue(key, out var bucket))
        {
            bucket.TryRemove(subscriptionId, out _);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private InProcessHubEventBus? _bus;
        private readonly HubEventKey _key;
        private readonly long _subscriptionId;

        public Subscription(InProcessHubEventBus bus, HubEventKey key, long subscriptionId)
        {
            _bus = bus;
            _key = key;
            _subscriptionId = subscriptionId;
        }

        public void Dispose()
        {
            var bus = Interlocked.Exchange(ref _bus, null);
            bus?.Unsubscribe(_key, _subscriptionId);
        }
    }

    private readonly record struct HubEventKey(string HubName, string MethodName);

    private sealed class HubEventKeyComparer : IEqualityComparer<HubEventKey>
    {
        public static readonly HubEventKeyComparer Instance = new();

        public bool Equals(HubEventKey x, HubEventKey y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.HubName, y.HubName) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.MethodName, y.MethodName);

        public int GetHashCode(HubEventKey obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.HubName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.MethodName));
    }
}

public static class InProcessHubEventBusExtensions
{
    private static readonly JsonSerializerOptions ConversionJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IDisposable On(this IInProcessHubEventBus bus, string hubName, string methodName, Func<Task> handler)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(handler);
        return bus.Subscribe(hubName, methodName, _ => handler());
    }

    public static IDisposable On(this IInProcessHubEventBus bus, string hubName, string methodName, Action handler)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(handler);
        return bus.Subscribe(hubName, methodName, _ =>
        {
            handler();
            return Task.CompletedTask;
        });
    }

    public static IDisposable On<T1>(this IInProcessHubEventBus bus, string hubName, string methodName, Func<T1, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(handler);
        return bus.Subscribe(hubName, methodName, args =>
        {
            var arg1 = args.Length > 0 ? ConvertArgument<T1>(args[0]) : default!;
            return handler(arg1);
        });
    }

    public static IDisposable On<T1>(this IInProcessHubEventBus bus, string hubName, string methodName, Action<T1> handler)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(handler);
        return bus.Subscribe(hubName, methodName, args =>
        {
            var arg1 = args.Length > 0 ? ConvertArgument<T1>(args[0]) : default!;
            handler(arg1);
            return Task.CompletedTask;
        });
    }

    public static IDisposable On<T1, T2>(this IInProcessHubEventBus bus, string hubName, string methodName, Func<T1, T2, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(handler);
        return bus.Subscribe(hubName, methodName, args =>
        {
            var arg1 = args.Length > 0 ? ConvertArgument<T1>(args[0]) : default!;
            var arg2 = args.Length > 1 ? ConvertArgument<T2>(args[1]) : default!;
            return handler(arg1, arg2);
        });
    }

    public static IDisposable On<T1, T2>(this IInProcessHubEventBus bus, string hubName, string methodName, Action<T1, T2> handler)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(handler);
        return bus.Subscribe(hubName, methodName, args =>
        {
            var arg1 = args.Length > 0 ? ConvertArgument<T1>(args[0]) : default!;
            var arg2 = args.Length > 1 ? ConvertArgument<T2>(args[1]) : default!;
            handler(arg1, arg2);
            return Task.CompletedTask;
        });
    }

    private static T ConvertArgument<T>(object? arg)
    {
        if (arg is T typed)
        {
            return typed;
        }

        if (arg is null)
        {
            return default!;
        }

        var element = JsonSerializer.SerializeToElement(arg, ConversionJsonOptions);
        return element.Deserialize<T>(ConversionJsonOptions)!;
    }
}
