using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;

namespace PenguinTwitchBot.Bot.Commands.Misc;

public interface IDeathCounterService
{
    Task<(string Game, int Amount)> GetCurrentDeathCountAsync();
    Task<(string Game, int Amount)> AdjustCurrentDeathCountAsync(CounterOperation operation, int? value = null);
}

