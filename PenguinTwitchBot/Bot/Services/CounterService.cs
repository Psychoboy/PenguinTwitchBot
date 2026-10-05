using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Helpers;

namespace PenguinTwitchBot.Bot.Services;

public class CounterService(
    IServiceScopeFactory scopeFactory,
    IHubContext<MainHub> hubContext,
    ILogger<CounterService> logger) : ICounterService
{
    private static readonly KeyedSemaphore _counterLocks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<List<Counter>> GetAllCountersAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var counters = await unitOfWork.Counters.GetAsync();
        return counters.OrderBy(c => c.CounterName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<Counter?> GetCounterByNameAsync(string counterName)
    {
        if (string.IsNullOrWhiteSpace(counterName))
        {
            return null;
        }

        var normalized = counterName.Trim().ToLowerInvariant();
        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await unitOfWork.Counters
            .Find(c => c.CounterName.ToLower() == normalized)
            .FirstOrDefaultAsync();
    }

    public async Task<Counter?> GetCounterByIdAsync(int id)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await unitOfWork.Counters
            .Find(c => c.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<Counter> CreateCounterAsync(Counter counter)
    {
        ArgumentNullException.ThrowIfNull(counter);

        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        counter.CounterName = counter.CounterName.Trim().ToLowerInvariant();
        var existing = await unitOfWork.Counters
            .Find(c => c.CounterName.ToLower() == counter.CounterName)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            throw new InvalidOperationException($"A counter named '{counter.CounterName}' already exists.");
        }

        var min = counter.Min ?? int.MinValue;
        var max = counter.Max ?? int.MaxValue;
        counter.Amount = Math.Clamp(counter.Amount, min, max);

        await unitOfWork.Counters.AddAsync(counter);
        await unitOfWork.SaveChangesAsync();
        await ExportToFileAsync(counter);
        await hubContext.Clients.All.SendAsync("CounterUpdated", counter);
        return counter;
    }

    public async Task<Counter> CreateCounterWithActionAsync(Counter counter, CounterActionCreationOptions? options)
    {
        ArgumentNullException.ThrowIfNull(counter);

        if (options == null || !options.CreateActionAndCommand)
        {
            return await CreateCounterAsync(counter);
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        counter.CounterName = counter.CounterName.Trim().ToLowerInvariant();
        var normalizedCounterName = counter.CounterName;

        var existingCounter = await unitOfWork.Counters
            .Find(c => c.CounterName.ToLower() == normalizedCounterName)
            .FirstOrDefaultAsync();

        if (existingCounter != null)
        {
            throw new InvalidOperationException($"A counter named '{counter.CounterName}' already exists.");
        }

        var min = counter.Min ?? int.MinValue;
        var max = counter.Max ?? int.MaxValue;
        counter.Amount = Math.Clamp(counter.Amount, min, max);

        var rawCommandName = string.IsNullOrWhiteSpace(options.CommandName) ? counter.CounterName : options.CommandName;
        var commandName = rawCommandName.Trim().TrimStart('!').ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(commandName))
        {
            commandName = counter.CounterName;
        }

        var existingCommand = await unitOfWork.ActionCommands
            .Find(c => c.CommandName.ToLower() == commandName)
            .FirstOrDefaultAsync();

        if (existingCommand != null)
        {
            throw new InvalidOperationException($"Command '!{commandName}' already exists.");
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync();
        try
        {
            // 1. Add Counter
            await unitOfWork.Counters.AddAsync(counter);
            await unitOfWork.SaveChangesAsync();

            // 2. Add ActionCommand
            var groupName = string.IsNullOrWhiteSpace(options.Group) ? "Counters" : options.Group.Trim();
            var actionCommand = new ActionCommand
            {
                CommandName = commandName,
                Category = groupName,
                Description = $"Counter command for {counter.CounterName}",
                Disabled = false,
                UserCooldown = Math.Max(0, options.UserCooldown),
                UserCooldownMax = 0,
                GlobalCooldown = Math.Max(0, options.GlobalCooldown),
                GlobalCooldownMax = 0,
                Cost = 0,
                MinimumRank = options.MinimumRank,
                SayCooldown = true,
                SayRankRequirement = false,
                ExcludeFromUi = false,
                SourceOnly = true
            };
            await unitOfWork.ActionCommands.AddAsync(actionCommand);
            await unitOfWork.SaveChangesAsync();

            // 3. Prepare SubActions
            var subActions = new List<SubActionType>
            {
                new MultiCounterType
                {
                    Index = 0,
                    Name = counter.CounterName,
                    Operation = CounterOperation.CommandArgs,
                    Min = counter.Min,
                    Max = counter.Max,
                    Enabled = true,
                    SubActionTypes = SubActionTypes.MultiCounter
                }
            };

            if (!string.IsNullOrWhiteSpace(options.ResponseMessage))
            {
                subActions.Add(new SendMessageType
                {
                    Index = 1,
                    Text = options.ResponseMessage.Trim(),
                    UseBot = true,
                    FallBack = true,
                    StreamOnly = true,
                    Enabled = true,
                    SubActionTypes = SubActionTypes.SendMessage
                });
            }

            // 4. Create Action with Command Trigger
            var actionName = counter.CounterName;
            var existingAction = await unitOfWork.Actions
                .Find(a => a.Name.ToLower() == actionName)
                .FirstOrDefaultAsync();

            if (existingAction != null)
            {
                actionName = $"{counter.CounterName} Counter";
            }

            var action = new ActionType
            {
                Name = actionName,
                Group = groupName,
                Enabled = true,
                QueueName = "Default",
                RandomAction = false,
                ConcurrentAction = false,
                OnlineOnly = false,
                SubActions = subActions,
                Triggers =
                [
                    new TriggerType
                    {
                        Name = $"!{commandName}",
                        Type = TriggerTypes.Command,
                        Enabled = true,
                        CommandId = actionCommand.Id,
                        Configuration = JsonSerializer.Serialize(new
                        {
                            CommandId = actionCommand.Id,
                            CommandName = commandName
                        })
                    }
                ]
            };

            await unitOfWork.Actions.CreateActionAsync(action);
            await transaction.CommitAsync();

            await ExportToFileAsync(counter);
            await hubContext.Clients.All.SendAsync("CounterUpdated", counter);

            return counter;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task CreateActionForCounterAsync(Counter counter, CounterActionCreationOptions options)
    {
        ArgumentNullException.ThrowIfNull(counter);
        ArgumentNullException.ThrowIfNull(options);

        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var rawCommandName = string.IsNullOrWhiteSpace(options.CommandName) ? counter.CounterName : options.CommandName;
        var commandName = rawCommandName.Trim().TrimStart('!').ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(commandName))
        {
            commandName = counter.CounterName;
        }

        var existingCommand = await unitOfWork.ActionCommands
            .Find(c => c.CommandName.ToLower() == commandName)
            .FirstOrDefaultAsync();

        if (existingCommand != null)
        {
            throw new InvalidOperationException($"Command '!{commandName}' already exists.");
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync();
        try
        {
            var groupName = string.IsNullOrWhiteSpace(options.Group) ? "Counters" : options.Group.Trim();
            var actionCommand = new ActionCommand
            {
                CommandName = commandName,
                Category = groupName,
                Description = $"Counter command for {counter.CounterName}",
                Disabled = false,
                UserCooldown = Math.Max(0, options.UserCooldown),
                UserCooldownMax = 0,
                GlobalCooldown = Math.Max(0, options.GlobalCooldown),
                GlobalCooldownMax = 0,
                Cost = 0,
                MinimumRank = options.MinimumRank,
                SayCooldown = true,
                SayRankRequirement = false,
                ExcludeFromUi = false,
                SourceOnly = true
            };
            await unitOfWork.ActionCommands.AddAsync(actionCommand);
            await unitOfWork.SaveChangesAsync();

            var subActions = new List<SubActionType>
            {
                new MultiCounterType
                {
                    Index = 0,
                    Name = counter.CounterName,
                    Operation = CounterOperation.CommandArgs,
                    Min = counter.Min,
                    Max = counter.Max,
                    Enabled = true,
                    SubActionTypes = SubActionTypes.MultiCounter
                }
            };

            if (!string.IsNullOrWhiteSpace(options.ResponseMessage))
            {
                subActions.Add(new SendMessageType
                {
                    Index = 1,
                    Text = options.ResponseMessage.Trim(),
                    UseBot = true,
                    FallBack = true,
                    StreamOnly = true,
                    Enabled = true,
                    SubActionTypes = SubActionTypes.SendMessage
                });
            }

            var actionName = counter.CounterName;
            var existingAction = await unitOfWork.Actions
                .Find(a => a.Name.ToLower() == actionName)
                .FirstOrDefaultAsync();

            if (existingAction != null)
            {
                actionName = $"{counter.CounterName} Counter";
            }

            var action = new ActionType
            {
                Name = actionName,
                Group = groupName,
                Enabled = true,
                QueueName = "Default",
                RandomAction = false,
                ConcurrentAction = false,
                OnlineOnly = false,
                SubActions = subActions,
                Triggers =
                [
                    new TriggerType
                    {
                        Name = $"!{commandName}",
                        Type = TriggerTypes.Command,
                        Enabled = true,
                        CommandId = actionCommand.Id,
                        Configuration = JsonSerializer.Serialize(new
                        {
                            CommandId = actionCommand.Id,
                            CommandName = commandName
                        })
                    }
                ]
            };

            await unitOfWork.Actions.CreateActionAsync(action);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Counter> UpdateCounterAsync(Counter counter)
    {
        ArgumentNullException.ThrowIfNull(counter);

        var normalizedName = counter.CounterName.Trim().ToLowerInvariant();
        using var counterLock = await _counterLocks.AcquireAsync(normalizedName, CancellationToken.None);

        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var existing = await unitOfWork.Counters
            .Find(c => (counter.Id.HasValue && c.Id == counter.Id) || c.CounterName.ToLower() == normalizedName)
            .FirstOrDefaultAsync();

        if (existing == null)
        {
            return await CreateCounterAsync(counter);
        }

        existing.CounterName = counter.CounterName.Trim().ToLowerInvariant();
        existing.DisplayName = counter.DisplayName;
        existing.InitialValue = counter.InitialValue;
        existing.Step = counter.Step <= 0 ? 1 : counter.Step;
        existing.Min = counter.Min;
        existing.Max = counter.Max;
        existing.IncrementRank = counter.IncrementRank;
        existing.DecrementRank = counter.DecrementRank;
        existing.ResetRank = counter.ResetRank;
        existing.SetRank = counter.SetRank;

        var min = existing.Min ?? int.MinValue;
        var max = existing.Max ?? int.MaxValue;
        existing.Amount = Math.Clamp(counter.Amount, min, max);

        unitOfWork.Counters.Update(existing);
        await unitOfWork.SaveChangesAsync();
        await ExportToFileAsync(existing);
        await hubContext.Clients.All.SendAsync("CounterUpdated", existing);
        return existing;
    }

    public async Task<bool> DeleteCounterAsync(int id)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var existing = await unitOfWork.Counters
            .Find(c => c.Id == id)
            .FirstOrDefaultAsync();

        if (existing == null)
        {
            return false;
        }

        unitOfWork.Counters.Remove(existing);
        await unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<CounterResult> AdjustCounterAsync(
        string counterName,
        CounterOperation operation,
        int? value = null,
        int? minOverride = null,
        int? maxOverride = null,
        CommandEventArgs? callerArgs = null)
    {
        if (string.IsNullOrWhiteSpace(counterName))
        {
            return new CounterResult { Success = false, ErrorMessage = "Counter name cannot be empty." };
        }

        var normalized = counterName.Trim().ToLowerInvariant();
        using var counterLock = await _counterLocks.AcquireAsync(normalized, CancellationToken.None);

        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var counter = await unitOfWork.Counters
            .Find(c => c.CounterName.ToLower() == normalized || c.CounterName == counterName)
            .FirstOrDefaultAsync();

        if (counter == null)
        {
            return new CounterResult
            {
                Success = false,
                CounterName = normalized,
                ErrorMessage = $"Counter '{normalized}' does not exist."
            };
        }

        var requiredRank = operation switch
        {
            CounterOperation.Increment => counter.IncrementRank,
            CounterOperation.Decrement => counter.DecrementRank,
            CounterOperation.Reset => counter.ResetRank,
            CounterOperation.Set => counter.SetRank,
            CounterOperation.Get => Rank.Viewer,
            _ => Rank.Viewer
        };

        if (!HasPermission(requiredRank, callerArgs))
        {
            return new CounterResult
            {
                Success = false,
                CounterName = counter.CounterName,
                DisplayName = counter.DisplayName,
                OldValue = counter.Amount,
                NewValue = counter.Amount,
                Operation = operation,
                ErrorMessage = $"Insufficient permission: requires {requiredRank} rank for {operation} operation."
            };
        }

        var oldValue = counter.Amount;
        var min = minOverride ?? counter.Min ?? int.MinValue;
        var max = maxOverride ?? counter.Max ?? int.MaxValue;
        var step = value ?? (counter.Step <= 0 ? 1 : counter.Step);

        switch (operation)
        {
            case CounterOperation.Increment:
                counter.Amount = Math.Clamp(counter.Amount + step, min, max);
                break;
            case CounterOperation.Decrement:
                counter.Amount = Math.Clamp(counter.Amount - step, min, max);
                break;
            case CounterOperation.Reset:
                counter.Amount = Math.Clamp(counter.InitialValue, min, max);
                break;
            case CounterOperation.Set:
                if (value.HasValue)
                {
                    counter.Amount = Math.Clamp(value.Value, min, max);
                }
                break;
            case CounterOperation.Get:
                // No modification
                break;
        }

        if (operation != CounterOperation.Get)
        {
            unitOfWork.Counters.Update(counter);
            await unitOfWork.SaveChangesAsync();
            await ExportToFileAsync(counter);
            await hubContext.Clients.All.SendAsync("CounterUpdated", counter);
        }

        return new CounterResult
        {
            Success = true,
            CounterName = counter.CounterName,
            DisplayName = counter.DisplayName,
            OldValue = oldValue,
            NewValue = counter.Amount,
            Operation = operation
        };
    }

    public async Task<CounterResult> EvaluateCommandArgsAsync(
        string counterName,
        CommandEventArgs? callerArgs,
        int? minOverride = null,
        int? maxOverride = null)
    {
        var rawArg = callerArgs?.Arg?.Trim() ?? string.Empty;
        var operation = CounterOperation.Get;
        int? directValue = null;

        if (string.IsNullOrEmpty(rawArg) ||
            string.Equals(rawArg, "get", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawArg, "show", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawArg, "check", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawArg, "value", StringComparison.OrdinalIgnoreCase))
        {
            operation = CounterOperation.Get;
        }
        else if (string.Equals(rawArg, "+", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rawArg, "++", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rawArg, "add", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rawArg, "inc", StringComparison.OrdinalIgnoreCase))
        {
            operation = CounterOperation.Increment;
        }
        else if (string.Equals(rawArg, "-", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rawArg, "--", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rawArg, "sub", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rawArg, "dec", StringComparison.OrdinalIgnoreCase))
        {
            operation = CounterOperation.Decrement;
        }
        else if (string.Equals(rawArg, "reset", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rawArg, "clear", StringComparison.OrdinalIgnoreCase))
        {
            operation = CounterOperation.Reset;
        }
        else if (rawArg.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = rawArg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedVal))
            {
                operation = CounterOperation.Set;
                directValue = parsedVal;
            }
        }
        else if (rawArg.StartsWith('+') && int.TryParse(rawArg[1..].Trim(), out var plusNum))
        {
            operation = CounterOperation.Increment;
            directValue = plusNum == int.MinValue ? int.MaxValue : Math.Abs(plusNum);
        }
        else if (rawArg.StartsWith('-') && int.TryParse(rawArg[1..].Trim(), out var minusNum))
        {
            operation = CounterOperation.Decrement;
            directValue = minusNum == int.MinValue ? int.MaxValue : Math.Abs(minusNum);
        }
        else if (int.TryParse(rawArg, out var directNum))
        {
            operation = CounterOperation.Set;
            directValue = directNum;
        }

        return await AdjustCounterAsync(
            counterName,
            operation,
            directValue,
            minOverride,
            maxOverride,
            callerArgs);
    }

    public void PopulateVariables(
        ConcurrentDictionary<string, string> variables,
        CounterResult result,
        string? destinationVariable = null)
    {
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(result);

        var safeName = result.CounterName.ToLowerInvariant();
        variables[$"counter_{safeName}"] = result.NewValue.ToString();
        variables["counter_value"] = result.NewValue.ToString();
        variables["counter_old_value"] = result.OldValue.ToString();
        variables["counter_name"] = result.CounterName;
        variables["counter_display_name"] = !string.IsNullOrWhiteSpace(result.DisplayName) ? result.DisplayName : result.CounterName;
        variables["counter_operation"] = result.Operation.ToString().ToLowerInvariant();
        variables["counter_success"] = result.Success ? "true" : "false";

        if (!string.IsNullOrWhiteSpace(destinationVariable))
        {
            var varKey = destinationVariable.Trim().Trim('%');
            variables[varKey] = result.NewValue.ToString();
        }
    }

    private static bool HasPermission(Rank requiredRank, CommandEventArgs? args)
    {
        if (args == null) return true;
        return requiredRank switch
        {
            Rank.Viewer or Rank.Regular => true,
            Rank.Follower => true,
            Rank.Subscriber => args.IsSubOrHigher(),
            Rank.Vip => args.IsVipOrHigher(),
            Rank.Moderator => args.IsModOrHigher(),
            Rank.Streamer => args.IsBroadcaster,
            _ => false
        };
    }

    private async Task ExportToFileAsync(Counter counter)
    {
        try
        {
            var countersDir = Path.Combine(AppContext.BaseDirectory, "Data", "counters");
            Directory.CreateDirectory(countersDir);

            var simplePath = Path.Combine(countersDir, $"{counter.CounterName}.txt");
            var fullPath = Path.Combine(countersDir, $"{counter.CounterName}-full.txt");

            await File.WriteAllTextAsync(simplePath, counter.Amount.ToString());
            var label = !string.IsNullOrWhiteSpace(counter.DisplayName) ? counter.DisplayName : counter.CounterName;
            await File.WriteAllTextAsync(fullPath, $"{label}: {counter.Amount}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write counter files for {CounterName}", counter.CounterName);
        }
    }
}
