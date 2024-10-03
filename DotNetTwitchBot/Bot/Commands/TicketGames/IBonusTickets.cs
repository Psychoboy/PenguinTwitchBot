
namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public interface IBonusTickets
    {
        Task<bool> DidUserRedeemBonus(string username);
        Task RedeemBonus(string username);
        Task Reset();
    }
}