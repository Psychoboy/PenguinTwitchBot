using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Markov
{
    public interface IMarkovChat
    {
        void LearnMessage(ChatMessageEventArgs e);
    }
}