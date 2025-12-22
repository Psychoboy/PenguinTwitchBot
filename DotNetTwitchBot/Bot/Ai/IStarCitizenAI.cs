
namespace DotNetTwitchBot.Bot.Ai
{
    public interface IStarCitizenAI
    {
        Task<string> GetResponseFromPrompt(string userId, string prompt);
    }
}