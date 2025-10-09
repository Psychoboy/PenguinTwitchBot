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
        IViewerFeature viewerFeature,
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
                var viewer = viewerFeature.GetViewerByUserName(username);
                if (viewer == null)
                {
                    logger.LogWarning("Could not find viewer {username} when trying to redeem bonus tickets.", username);
                    return;
                }

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
                var amount = await pointSystem.AddPointsByUsernameAndGame(username, "bonus", ticketsWon);
                if(amount == 0)
                {
                    logger.LogWarning("Failed to add {ticketsWon} bonus tickets to {username}.", ticketsWon, username);
                    await mediator.Publish(new SendBotMessage($"{username}, something went wrong when trying to give you bonus tickets. Please contact a moderator."));
                    return;
                }
                logger.LogInformation("Gave {username} {tickets} tickets via website.", username, ticketsWon);
                var message = string.Format(
                    "{0} just got {1} bonus tickets from https://bot.superpenguin.tv and now has {2} tickets.",
                    username, ticketsWon.ToString("N0"), amount.ToString("N0"));
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
