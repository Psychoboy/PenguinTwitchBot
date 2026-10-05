using System.Collections.Concurrent;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Bot.Services;

public class CounterActionCreationOptions
{
    public bool CreateActionAndCommand { get; set; } = true;
    public string CommandName { get; set; } = string.Empty;
    public string? ResponseMessage { get; set; } = "%counter_display_name%: %counter_value%";
    public Rank MinimumRank { get; set; } = Rank.Viewer;
    public int UserCooldown { get; set; } = 5;
    public int GlobalCooldown { get; set; } = 0;
    public string Group { get; set; } = "Counters";
}

public class CreateCounterDialogResult
{
    public Counter Counter { get; set; } = new();
    public CounterActionCreationOptions? ActionOptions { get; set; }
}

public class CounterResult
{
    public bool Success { get; set; }
    public string CounterName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int OldValue { get; set; }
    public int NewValue { get; set; }
    public CounterOperation Operation { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface ICounterService
{
    Task<List<Counter>> GetAllCountersAsync();
    Task<Counter?> GetCounterByNameAsync(string counterName);
    Task<Counter?> GetCounterByIdAsync(int id);
    Task<Counter> CreateCounterAsync(Counter counter);
    Task<Counter> CreateCounterWithActionAsync(Counter counter, CounterActionCreationOptions? options);
    Task CreateActionForCounterAsync(Counter counter, CounterActionCreationOptions options);
    Task<Counter> UpdateCounterAsync(Counter counter);
    Task<bool> DeleteCounterAsync(int id);
    Task<CounterResult> AdjustCounterAsync(
        string counterName,
        CounterOperation operation,
        int? value = null,
        int? minOverride = null,
        int? maxOverride = null,
        CommandEventArgs? callerArgs = null);
    Task<CounterResult> EvaluateCommandArgsAsync(
        string counterName,
        CommandEventArgs? callerArgs,
        int? minOverride = null,
        int? maxOverride = null);
    void PopulateVariables(
        ConcurrentDictionary<string, string> variables,
        CounterResult result,
        string? destinationVariable = null);
}

