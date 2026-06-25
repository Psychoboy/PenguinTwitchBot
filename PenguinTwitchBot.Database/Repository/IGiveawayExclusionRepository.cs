using PenguinTwitchBot.Database.Bot.Models.Giveaway;

using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IGiveawayExclusionRepository : IGenericRepository<GiveawayExclusion>
    {
    }
}
