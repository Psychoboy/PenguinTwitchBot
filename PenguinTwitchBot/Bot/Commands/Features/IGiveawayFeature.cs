using PenguinTwitchBot.Database.Bot.Models.Giveaway;
using System.Collections.Generic;

namespace PenguinTwitchBot.Bot.Commands.Features
{
    public interface IGiveawayFeature
    {
        Task<string> GetPrize();
        bool IsClosed();
        Task<int> GetEntrantsCount();
        Task<int> GetEntriesCount();
        Task<IEnumerable<GiveawayExclusion>> GetAllExclusions();
        Task AddExclusion(GiveawayExclusion exclusion);
        Task DeleteExclusion(GiveawayExclusion exclusion);
        Task<string> GetImageUrl();
        Task<string> GetPrizeTier();
        Task<string> GetPrizeAdditionalDetails();
        Task SetPrizeAdditionalDetails(string? arg);
        Task<string> GetRules();
        Task SetRules(string value);
        Task<string> GetCooldowns();
        Task SetCooldowns(string value);
        Task<string> GetTerms();
        Task SetTerms(string value);
        Task<string> GetPassiveEarnings();
        Task SetPassiveEarnings(string value);
        Task SetPrizeTier(string? arg);
        Task SetPrize(string arg);
        Task SetImageUrl(string? arg);
        Task Close();
        Task Reset();
        Task Draw();
        Task<List<GiveawayWinner>> PastWinners();
        Task UpdateWinner(GiveawayWinner winner);
        Task<string> Enter(string sender, string amount, bool fromUi);
        Task<long> GetEntriesCount(string sender);
    }
}
