using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Features
{
    public interface ITicketsFeature
    {
        Task<long> GetViewerTickets(string viewer);
        Task<ViewerTicketWithRanks?> GetViewerTicketsWithRank(string viewer);
        Task GiveTicketsToActiveAndSubsOnlineWithBonus(long amount, long bonusAmount);
        Task GiveTicketsToActiveUsers(long amount);
        Task<long> GiveTicketsToViewerByUserId(string userid, long amount);
        Task<long> GiveTicketsToViewerByUsername(string username, long amount);
        Task GiveTicketsWithBonusToViewers(IEnumerable<string> viewers, long amount, long subBonusAmount);
        Task OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task<bool> RemoveTicketsFromViewerByUserId(string userid, long amount);
        Task<bool> RemoveTicketsFromViewerByUsername(string username, long amount);
        Task ResetAllPoints();
    }
}