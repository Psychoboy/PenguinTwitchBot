using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.ChannelPoints
{
    public interface IChannelPointRedeem : IHostedService
    {
        Task AddRedeem(Models.ChannelPointRedeem channelPointRedeem);
        Task DeleteRedeem(Models.ChannelPointRedeem channelPointRedeem);
        Task<List<Models.ChannelPointRedeem>> GetRedeems();
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
    }
}