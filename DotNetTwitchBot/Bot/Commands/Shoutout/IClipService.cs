using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Shoutout
{
    public interface IClipService
    {
        Task OnCommand(object? sender, CommandEventArgs e);
        Task PlayRandomClipForStreamer(string streamer);
        Task Register();
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }
}