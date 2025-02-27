using DotNetTwitchBot.Application.ChatMessage.Notification;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using MediatR;
using System.Security.Cryptography;

namespace DotNetTwitchBot.Bot.Commands.TicketGames
{
    public class BonusTickets(
        IPointsSystem pointSystem,
        IMediator mediator, 
        IServiceBackbone serviceBackbone, 
        ILogger<BonusTickets> logger) : IBonusTickets
    {
        private static readonly List<string> ClaimedBonuses = [];
        private static readonly SemaphoreSlim _semaphoreSlim = new(1);
        public async Task<bool> DidUserRedeemBonus(string username)
        {
            await _semaphoreSlim.WaitAsync();
            try
            {
                return ClaimedBonuses.Contains(username);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public async Task RedeemBonus(string username)
        {
            await _semaphoreSlim.WaitAsync();
            try
            {
                if (ClaimedBonuses.Contains(username))
                {
                    logger.LogWarning("{username} tried to claim tickets twice.", username);
                    return;
                }
                if(serviceBackbone.IsOnline == false)
                {
                    logger.LogWarning("{username} tried to claim tickets while stream offline.", username);
                    return;
                }
                ClaimedBonuses.Add(username);
                var ticketsWon = RandomNumberGenerator.GetInt32(25, 51);
                var amount = await pointSystem.AddPointsByUserIdAndGame(username, "bonus", ticketsWon);
                logger.LogInformation("Gave {username} {tickets} tickets via website.", username, ticketsWon);
                var message = string.Format(
                    "{0} just got {1} bonus tickets from https://bot.superpenguin.tv and now has {2} tickets.",
                    username, ticketsWon, amount);
                await mediator.Publish(new SendBotMessage(message));
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public async Task Reset()
        {
            await _semaphoreSlim.WaitAsync();
            try
            {
                ClaimedBonuses.Clear();
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public Task Setup()
        {
            return pointSystem.RegisterDefaultPointForGame("Bonus");
        }
    }
}
