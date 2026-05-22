
namespace PenguinTwitchBot.Bot.Commands.TicketGames
{
    public interface IBonusTickets
    {
        Task<bool> DidUserRedeemBonus(string username);
        Task RedeemBonus(string username);
        Task Setup();
        Task Reset();
    }
}