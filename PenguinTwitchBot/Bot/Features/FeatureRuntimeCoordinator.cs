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
        private readonly Dictionary<string, IReadOnlyList<RuntimeFeatureRegistration>> _registrationsByKey = registrations
            .GroupBy(x => x.Key, KeyComparer)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<RuntimeFeatureRegistration>)x
                    .OrderBy(registration => registration.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(registration => registration.ModuleName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                KeyComparer);
        private readonly ConcurrentDictionary<string, bool> _enabledStates = new(KeyComparer);
        private readonly ConcurrentDictionary<string, bool> _runningStates = new(KeyComparer);
        private readonly SemaphoreSlim _stateLock = new(1, 1);

        public event Action? StateChanged;
        public event Func<Task>? StateChangedAsync;

        public IReadOnlyList<RuntimeFeatureState> GetFeatures()
        {
            return _registrationsByKey.Values
                .Select(x => x[0])
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
            if (!_registrationsByKey.TryGetValue(featureKey, out var registrations))
            {
                return true;
            }

            return registrations[0].IsCore || _enabledStates.GetValueOrDefault(featureKey, true);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var registrations in _registrationsByKey.Values
                    .OrderByDescending(x => x[0].IsCore)
                    .ThenBy(x => x[0].DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    var featureRegistration = registrations[0];
                    var enabled = featureRegistration.IsCore || await featureStateStore.GetEnabledAsync(featureRegistration.Key, true);
                    _enabledStates[featureRegistration.Key] = enabled;

                    foreach (var registration in registrations)
                    {
                        await SyncFeatureCommandsAsync(registration, enabled, cancellationToken);

                        if (enabled)
                        {
                            await StartFeatureInternalAsync(registration, cancellationToken);
                        }
                    }

                    _runningStates[featureRegistration.Key] = enabled;
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
                foreach (var registrations in _registrationsByKey.Values
                    .OrderByDescending(x => x[0].IsCore)
                    .ThenByDescending(x => x[0].DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var registration in registrations)
                    {
                        await StopFeatureInternalAsync(registration, cancellationToken);
                    }

                    _runningStates[registrations[0].Key] = false;
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
            if (!_registrationsByKey.TryGetValue(featureKey, out var registrations))
            {
                throw new InvalidOperationException($"Unknown feature key '{featureKey}'.");
            }

            var registration = registrations[0];

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
                    foreach (var featureRegistration in registrations)
                    {
                        await StartFeatureInternalAsync(featureRegistration, cancellationToken);
                        await SyncFeatureCommandsAsync(featureRegistration, true, cancellationToken);
                    }

                    await featureStateStore.SetEnabledAsync(registration.Key, true);
                    _enabledStates[registration.Key] = true;
                    _runningStates[registration.Key] = true;
                }
                else
                {
                    foreach (var featureRegistration in registrations)
                    {
                        await SyncFeatureCommandsAsync(featureRegistration, false, cancellationToken);
                        await StopFeatureInternalAsync(featureRegistration, cancellationToken);
                    }

                    await featureStateStore.SetEnabledAsync(registration.Key, false);
                    _enabledStates[registration.Key] = false;
                    _runningStates[registration.Key] = false;
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
            if (!_registrationsByKey.TryGetValue(featureKey, out var registrations))
            {
                throw new InvalidOperationException($"Unknown feature key '{featureKey}'.");
            }

            var registration = registrations[0];

            var isEnabled = registration.IsCore || _enabledStates.GetValueOrDefault(featureKey, true);
            if (!isEnabled)
            {
                throw new InvalidOperationException($"Feature '{registration.DisplayName}' is disabled and cannot be restarted.");
            }

            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var featureRegistration in registrations)
                {
                    await StopFeatureInternalAsync(featureRegistration, cancellationToken);
                    await StartFeatureInternalAsync(featureRegistration, cancellationToken);
                }

                _runningStates[registration.Key] = true;
            }
            finally
            {
                _stateLock.Release();
            }

            await NotifyStateChangedAsync();
        }

        private async Task StartFeatureInternalAsync(RuntimeFeatureRegistration registration, CancellationToken cancellationToken)
        {
            if (serviceProvider.GetService(registration.ServiceType) is not IHostedService service)
            {
                return;
            }

            await service.StartAsync(cancellationToken);
            logger.LogInformation("Started feature service {FeatureKey}", registration.Key);
        }

        private async Task StopFeatureInternalAsync(RuntimeFeatureRegistration registration, CancellationToken cancellationToken)
        {
            if (serviceProvider.GetService(registration.ServiceType) is not IHostedService service)
            {
                return;
            }

            await service.StopAsync(cancellationToken);
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