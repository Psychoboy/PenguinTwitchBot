using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Markov
{
    public interface IMarkovChat
    {
        Task LearnMessage(ChatMessageEventArgs e);
        Task UpdateBots();
        Task Relearn();
    }
}