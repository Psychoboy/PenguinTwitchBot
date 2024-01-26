using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.TwitchEvents
{
    public interface ITwitchEventsService
    {
        Task AddTwitchEvent(TwitchEvent twitchEvent);
        Task DeleteTwitchEvent(TwitchEvent twitchEvent);
        Task<IEnumerable<TwitchEvent>> GetTwitchEvents();
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
    }
}