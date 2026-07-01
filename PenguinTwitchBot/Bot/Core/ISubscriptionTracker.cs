namespace PenguinTwitchBot.Bot.Core
{
    public interface ISubscriptionTracker
    {
        Task<bool> ExistingSub(string name);
        Task<DateTime?> LastSub(string name);
        Task<List<string>> MissingSubs(IEnumerable<string> names);
        Task AddOrUpdateSubHistory(string name, string userId);
    }
}
