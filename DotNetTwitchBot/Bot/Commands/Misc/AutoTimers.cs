using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;
using DotNetTwitchBot.Bot.Models.Timers;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Repository;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class AutoTimers : BaseCommandService, IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoTimers> _logger;
        private readonly Timer _intervalTimer;
        private readonly ConcurrentDictionary<int, int> MessageCounters = new();
        private readonly SemaphoreSlim _timerLock = new(1, 1);
        private int MessageCounter = 0;

        public AutoTimers(
            ILogger<AutoTimers> logger,
            IServiceScopeFactory scopeFactory,
            IServiceBackbone serviceBackbone,
            Application.Notifications.IPenguinDispatcher dispatcher,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler, "Timers", dispatcher)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _intervalTimer = new Timer(1000);
            _intervalTimer.Elapsed += ElapseTimer;

            serviceBackbone.CommandEvent += CommandMessage;
            serviceBackbone.StreamStarted += StreamStarted;
        }

        private async Task StreamStarted(object? sender, EventArgs _)
        {
            MessageCounters.Clear();
            MessageCounter = 0;
            var groups = await GetTimerGroupsAsync();
            groups.ForEach(async x => await UpdateNextRun(x));
        }

        private Task CommandMessage(object? sender, CommandEventArgs e)
        {
            MessageCounter++;
            return Task.CompletedTask;
        }

        public Task OnChatMessage()
        {
            MessageCounter++;
            return Task.CompletedTask;
        }

        public async Task<List<TimerGroup>> GetTimerGroupsAsync()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.TimerGroups.GetAsync();
        }

        public async Task<TimerGroup?> GetTimerGroupAsync(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.TimerGroups.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task AddTimerGroup(TimerGroup group)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.TimerGroups.AddAsync(group);
            await db.SaveChangesAsync();
        }

        public async Task UpdateTimerGroup(TimerGroup group, string oldName, string newName)
        {
            if(!group.Id.HasValue)
            {
                _logger.LogWarning("Cannot update timer group: Timer group ID is null");
                return;
            }

            if (!oldName.Equals(newName, StringComparison.OrdinalIgnoreCase))
            {
                // Name has changed - update trigger configurations
                await UpdateTriggerConfigurationsForRenamedTimerGroup(group.Id.Value, oldName, newName);

                // Also update subaction references
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await db.Actions.UpdateTimerGroupNamesForRenamedTimerGroup(group.Id.Value, newName);
            }
        }

        public async Task UpdateTimerGroup(TimerGroup group)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.TimerGroups.Update(group);
            await db.SaveChangesAsync();
        }

        public async Task DeleteTimerGroup(TimerGroup group)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.TimerGroups.Remove(group);
            await db.SaveChangesAsync();
        }

        private async void ElapseTimer(object? sender, ElapsedEventArgs e)
        {
            // Prevent re-entrancy: skip this tick if the previous one is still running
            if (!await _timerLock.WaitAsync(0)) return;

            try
            {
                await RunTimers();
            }
            finally
            {
                _timerLock.Release();
            }
        }

        private async Task RunTimers()
        {
            try
            {
                List<TimerGroup> timerGroups;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    // Get timers that are due to run, filtering by online/offline status
                    if (ServiceBackbone.IsOnline)
                    {
                        // Stream is online - run all active timers that are due
                        timerGroups = await db.TimerGroups.GetAsync(filter: x => x.NextRun < DateTime.Now && x.Active == true);
                    }
                    else
                    {
                        // Stream is offline - only run timers that allow offline execution
                        timerGroups = await db.TimerGroups.GetAsync(filter: x => x.NextRun < DateTime.Now && x.Active == true && x.OnlineOnly == false);
                    }
                }
                if (timerGroups == null || timerGroups.Count != 0 == false) return;
                await RunGroups(timerGroups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error RunTimers");
            }
        }

        private async Task RunGroups(List<TimerGroup> timerGroups)
        {
            foreach (var group in timerGroups)
            {
                await RunGroup(group);
            }
        }

        public async Task RunGroup(TimerGroup group, bool overrideCheck = false)
        {
            if (CheckEnoughMessagesAndUpdate(group) == false && !overrideCheck) return;
            try
            {
                if (group.Id.HasValue)
                {
                    var actions = await GetActionsForTimerGroup(group.Id.Value);
                    var enabledActions = actions.Where(x => x.Enabled == true).ToList();
                    if (enabledActions.Any())
                    {
                        var action = enabledActions.RandomElementOrDefault(_logger);
                        if (action != null)
                        {
                            await ExecuteAction(action, group);
                        }
                    }
                }

                if (group.Repeat == false)
                {
                    group.Active = false;
                    await UpdateTimerGroup(group);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error RunGroup");
            }
            await UpdateNextRun(group);
        }

        public async Task<TimerGroup> UpdateNextRun(TimerGroup group)
        {
            try
            {
                var randomNextSeconds = StaticTools.RandomRange(group.IntervalMinimumSeconds, group.IntervalMaximumSeconds);
                group.NextRun = DateTime.Now.AddSeconds(randomNextSeconds);
                group.LastRun = DateTime.Now;
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.TimerGroups.Update(group);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update Next Run");
            }
            return group;
        }

        private bool CheckEnoughMessagesAndUpdate(TimerGroup group)
        {
            var id = group.Id;
            if (id == null)
            {
                return false;
            }
            if (MessageCounters.TryGetValue((int)id, out var messageCounter))
            {
                if (messageCounter + group.MinimumMessages > MessageCounter)
                {
                    return false;
                }
            }
            MessageCounters[(int)id] = MessageCounter;
            return true;
        }

        public async Task<List<ActionType>> GetActionsForTimerGroup(int timerGroupId)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Use the new efficient query method that uses TimerGroupId column
            var triggers = await db.Triggers.GetByTimerGroupIdAsync(timerGroupId);
            var timerTriggers = triggers.Where(t => t.Action != null).ToList();

            return [.. timerTriggers.Select(t => t.Action!)];
        }

        private async Task ExecuteAction(ActionType action, TimerGroup group)
        {
            try
            {
                var variables = new ConcurrentDictionary<string, string>
                {
                    ["timer_name"] = group.Name,
                    ["timer_id"] = group.Id?.ToString() ?? "0"
                };

                await using var scope = _scopeFactory.CreateAsyncScope();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();
                await actionService.EnqueueAction(variables, action);
                _logger.LogDebug("Enqueued action {ActionName} for timer {TimerName}", action.Name, group.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing action for timer group");
            }
        }

        public async Task AddActionToTimerGroup(int timerGroupId, int actionId)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var timerGroup = await db.TimerGroups.Find(x => x.Id == timerGroupId).FirstOrDefaultAsync();
            if (timerGroup == null)
            {
                _logger.LogWarning("Cannot add action to timer group: Timer group {TimerGroupId} not found", timerGroupId);
                return;
            }

            var triggerName = $"TimerGroup_{timerGroupId}";
            var triggers = await db.Triggers.GetTriggersForActionAsync(actionId);
            var existingTrigger = triggers.FirstOrDefault(t => t.Name == triggerName);

            if (existingTrigger == null)
            {
                var trigger = new TriggerType
                {
                    Name = triggerName,
                    Type = TriggerTypes.Timer,
                    ActionId = actionId,
                    Enabled = true,
                    TimerGroupId = timerGroupId, // Populate reference column for efficient querying
                    Configuration = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        TimerGroupId = timerGroupId,
                        TimerGroupName = timerGroup.Name
                    })
                };

                await db.Triggers.AddAsync(trigger);
            }
        }

        public async Task RemoveActionFromTimerGroup(int timerGroupId, int actionId)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var triggerName = $"TimerGroup_{timerGroupId}";
            var triggers = await db.Triggers.GetTriggersForActionAsync(actionId);
            var trigger = triggers.FirstOrDefault(t => t.Name == triggerName);

            if (trigger != null)
            {
                await db.Triggers.DeleteAsync(trigger.Id);
            }
        }

        /// <summary>
        /// Updates trigger configurations for all triggers associated with a timer group when its name changes.
        /// Uses the old timer group name to reliably find triggers instead of string matching on TimerGroupId.
        /// </summary>
        private async Task UpdateTriggerConfigurationsForRenamedTimerGroup(int timerGroupId, string oldTimerGroupName, string newTimerGroupName)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                // Load all timer triggers
                var timerTriggers = await db.Triggers.GetByTypeAsync(TriggerTypes.Timer);

                var updatedCount = 0;
                foreach (var trigger in timerTriggers)
                {
                    try
                    {
                        var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(trigger.Configuration);
                        if (config == null) continue;

                        // Check if this trigger references the old timer group name
                        if (config.TryGetValue("TimerGroupName", out var timerGroupNameElement))
                        {
                            var timerGroupName = timerGroupNameElement.GetString();
                            if (timerGroupName == oldTimerGroupName)
                            {
                                // Update the configuration with the new name
                                var updatedConfig = new
                                {
                                    TimerGroupId = timerGroupId,
                                    TimerGroupName = newTimerGroupName
                                };
                                trigger.Configuration = System.Text.Json.JsonSerializer.Serialize(updatedConfig);

                                // Update the reference column as well
                                trigger.TimerGroupId = timerGroupId;

                                updatedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update trigger configuration for trigger {TriggerId}", trigger.Id);
                    }
                }

                if (updatedCount > 0)
                {
                    await db.SaveChangesAsync();
                    _logger.LogInformation("Updated {Count} trigger configurations for renamed timer group from '{OldName}' to '{NewName}'", 
                        updatedCount, oldTimerGroupName, newTimerGroupName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update trigger configurations for timer group {TimerGroupId}", timerGroupId);
            }
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override Task Register()
        {
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {moduledname}", ModuleName);
            _intervalTimer.Start();
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);

            var timerLock = _timerLock;

            _intervalTimer.Stop();
            _intervalTimer.Elapsed -= ElapseTimer;
            _intervalTimer.Dispose(); 
            if(timerLock != null)
            {
                await timerLock.WaitAsync(cancellationToken);
                timerLock.Release();
            }
        }
    }
}