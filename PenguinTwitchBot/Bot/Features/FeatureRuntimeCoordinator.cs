using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Features
{
    public sealed record RuntimeFeatureRegistration(
        string Key,
        string DisplayName,
        string ModuleName,
        Type ServiceType,
        bool IsCore,
        string Description = "");

    public sealed record RuntimeFeatureState(
        string Key,
        string DisplayName,
        string ModuleName,
        bool IsCore,
        bool IsEnabled,
        bool IsRunning,
        string Description = "");

    public interface IFeatureRuntimeCoordinator
    {
        event Action? StateChanged;
        event Func<Task>? StateChangedAsync;

        IReadOnlyList<RuntimeFeatureState> GetFeatures();
        bool IsEnabled(string featureKey);
        Task RestartAsync(string featureKey, CancellationToken cancellationToken = default);
        Task SetEnabledAsync(string featureKey, bool enabled, CancellationToken cancellationToken = default);
    }

    public sealed class FeatureRuntimeCoordinator(
        IEnumerable<RuntimeFeatureRegistration> registrations,
        IFeatureStateStore featureStateStore,
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        ICommandHandler commandHandler,
        ILogger<FeatureRuntimeCoordinator> logger) : IFeatureRuntimeCoordinator, IHostedService
    {
        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private readonly Dictionary<string, RuntimeFeatureRegistration> _registrations = registrations
            .DistinctBy(x => x.Key, KeyComparer)
            .ToDictionary(x => x.Key, KeyComparer);
        private readonly ConcurrentDictionary<string, bool> _enabledStates = new(KeyComparer);
        private readonly ConcurrentDictionary<string, bool> _runningStates = new(KeyComparer);
        private readonly SemaphoreSlim _stateLock = new(1, 1);

        public event Action? StateChanged;
        public event Func<Task>? StateChangedAsync;

        public IReadOnlyList<RuntimeFeatureState> GetFeatures()
        {
            return _registrations.Values
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(x => new RuntimeFeatureState(
                    x.Key,
                    x.DisplayName,
                    x.ModuleName,
                    x.IsCore,
                    x.IsCore || _enabledStates.GetValueOrDefault(x.Key, true),
                    _runningStates.GetValueOrDefault(x.Key, false),
                    x.Description))
                .ToList();
        }

        public bool IsEnabled(string featureKey)
        {
            if (!_registrations.TryGetValue(featureKey, out var registration))
            {
                return true;
            }

            return registration.IsCore || _enabledStates.GetValueOrDefault(featureKey, true);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var registration in _registrations.Values
                    .OrderByDescending(x => x.IsCore)
                    .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    var enabled = registration.IsCore || await featureStateStore.GetEnabledAsync(registration.Key, true);
                    _enabledStates[registration.Key] = enabled;
                    await SyncFeatureCommandsAsync(registration, enabled, cancellationToken);

                    if (enabled)
                    {
                        await StartFeatureInternalAsync(registration, cancellationToken);
                    }
                }
            }
            finally
            {
                _stateLock.Release();
            }

            await NotifyStateChangedAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var registration in _registrations.Values
                    .OrderByDescending(x => x.IsCore)
                    .ThenByDescending(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    await StopFeatureInternalAsync(registration, cancellationToken);
                }
            }
            finally
            {
                _stateLock.Release();
            }

            await NotifyStateChangedAsync();
        }

        public async Task SetEnabledAsync(string featureKey, bool enabled, CancellationToken cancellationToken = default)
        {
            if (!_registrations.TryGetValue(featureKey, out var registration))
            {
                throw new InvalidOperationException($"Unknown feature key '{featureKey}'.");
            }

            if (registration.IsCore && !enabled)
            {
                throw new InvalidOperationException($"Feature '{registration.DisplayName}' is core and cannot be disabled.");
            }

            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                var currentEnabled = registration.IsCore || _enabledStates.GetValueOrDefault(featureKey, true);
                if (currentEnabled == enabled)
                {
                    return;
                }

                if (enabled)
                {
                    await StartFeatureInternalAsync(registration, cancellationToken);
                    await SyncFeatureCommandsAsync(registration, true, cancellationToken);
                    await featureStateStore.SetEnabledAsync(registration.Key, true);
                    _enabledStates[registration.Key] = true;
                }
                else
                {
                    await SyncFeatureCommandsAsync(registration, false, cancellationToken);
                    await StopFeatureInternalAsync(registration, cancellationToken);
                    await featureStateStore.SetEnabledAsync(registration.Key, false);
                    _enabledStates[registration.Key] = false;
                }
            }
            finally
            {
                _stateLock.Release();
            }

            await NotifyStateChangedAsync();
        }

        public async Task RestartAsync(string featureKey, CancellationToken cancellationToken = default)
        {
            if (!_registrations.TryGetValue(featureKey, out var registration))
            {
                throw new InvalidOperationException($"Unknown feature key '{featureKey}'.");
            }

            var isEnabled = registration.IsCore || _enabledStates.GetValueOrDefault(featureKey, true);
            if (!isEnabled)
            {
                throw new InvalidOperationException($"Feature '{registration.DisplayName}' is disabled and cannot be restarted.");
            }

            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                await StopFeatureInternalAsync(registration, cancellationToken);
                await StartFeatureInternalAsync(registration, cancellationToken);
            }
            finally
            {
                _stateLock.Release();
            }

            await NotifyStateChangedAsync();
        }

        private async Task StartFeatureInternalAsync(RuntimeFeatureRegistration registration, CancellationToken cancellationToken)
        {
            if (_runningStates.GetValueOrDefault(registration.Key, false))
            {
                return;
            }

            var service = (IHostedService)serviceProvider.GetRequiredService(registration.ServiceType);
            await service.StartAsync(cancellationToken);
            _runningStates[registration.Key] = true;
            logger.LogInformation("Started feature service {FeatureKey}", registration.Key);
        }

        private async Task StopFeatureInternalAsync(RuntimeFeatureRegistration registration, CancellationToken cancellationToken)
        {
            if (!_runningStates.GetValueOrDefault(registration.Key, false))
            {
                return;
            }

            var service = (IHostedService)serviceProvider.GetRequiredService(registration.ServiceType);
            await service.StopAsync(cancellationToken);
            _runningStates[registration.Key] = false;
            logger.LogInformation("Stopped feature service {FeatureKey}", registration.Key);
        }

        private async Task SyncFeatureCommandsAsync(RuntimeFeatureRegistration registration, bool enabled, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var defaultCommands = await db.DefaultCommands.Find(x => x.ModuleName == registration.ModuleName).ToListAsync(cancellationToken);
            if (defaultCommands.Count == 0)
            {
                return;
            }

            var hasChanges = false;
            foreach (var defaultCommand in defaultCommands)
            {
                if (enabled)
                {
                    if (defaultCommand.DisabledByFeature)
                    {
                        defaultCommand.Disabled = false;
                        defaultCommand.DisabledByFeature = false;
                        hasChanges = true;
                    }
                }
                else
                {
                    if (!defaultCommand.Disabled)
                    {
                        defaultCommand.Disabled = true;
                        defaultCommand.DisabledByFeature = true;
                        hasChanges = true;
                    }
                }

                var runtimeCommand = commandHandler.GetCommand(defaultCommand.CustomCommandName);
                if (runtimeCommand?.CommandProperties is DefaultCommand registeredCommand)
                {
                    registeredCommand.Disabled = defaultCommand.Disabled;
                    registeredCommand.DisabledByFeature = defaultCommand.DisabledByFeature;
                }
            }

            if (!hasChanges)
            {
                return;
            }

            await db.SaveChangesAsync();
        }

        private async Task NotifyStateChangedAsync()
        {
            StateChanged?.Invoke();

            var asyncHandlers = StateChangedAsync?.GetInvocationList();
            if (asyncHandlers is null)
            {
                return;
            }

            foreach (var handler in asyncHandlers)
            {
                await ((Func<Task>)handler)();
            }
        }
    }
}