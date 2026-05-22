using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Markov
{
    public interface IMarkovChat
    {
        Task LearnMessage(ChatMessageEventArgs e);
        Task UpdateBots();
        Task Relearn();
    }
}