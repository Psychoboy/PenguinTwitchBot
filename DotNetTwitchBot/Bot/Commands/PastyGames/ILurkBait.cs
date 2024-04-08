
namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    public interface ILurkBait
    {
        Task AwardPoints(LurkBaitTrigger lbtrigger);
    }
}