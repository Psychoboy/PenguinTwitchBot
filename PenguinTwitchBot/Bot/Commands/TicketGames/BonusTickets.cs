using PenguinTwitchBot.Application.ChatMessage.Notification;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using System.Security.Cryptography;

namespace PenguinTwitchBot.Bot.Commands.TicketGames
{
    public class BonusTickets(
        IPointsSystem pointSystem,
        IGameSettingsService gameSettingsService,
        Application.Notifications.IPenguinDispatcher dispatcher, 
        IServiceBackbone serviceBackbone, 
        IViewerFeature viewerFeature,
        ILogger<BonusTickets> logger) : IBonusTickets
    {
        public static readonly string GAMENAME = "Bonus";
        public static readonly string MINAMOUNT = "MinAmount";
        public static readonly string MAXAMOUNT = "MaxAmount";
        public static readonly string WINMESSAGE = "WinMessage";
        public static readonly string ERRORMESSAGE = "ErrorMessage";

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
                var minAmount = Math.Clamp(await gameSettingsService.GetIntSetting(GAMENAME, MINAMOUNT, 25), 1, int.MaxValue - 1);
                var maxAmount = Math.Clamp(await gameSettingsService.GetIntSetting(GAMENAME, MAXAMOUNT, 50), minAmount, int.MaxValue - 1);
                var ticketsWon = RandomNumberGenerator.GetInt32(minAmount, maxAmount + 1);
                var amount = await pointSystem.AddPointsByUsernameAndGame(username, GAMENAME, ticketsWon);
                if(amount == 0)
                {
                    logger.LogWarning("Failed to add {ticketsWon} bonus tickets to {username}.", ticketsWon, username);
                    var errorMsg = (await gameSettingsService.GetStringSetting(GAMENAME, ERRORMESSAGE, "{Name}, something went wrong when trying to give you bonus points. Please contact a moderator.")) ?? string.Empty;
                    errorMsg = errorMsg.Replace("{Name}", username, StringComparison.OrdinalIgnoreCase);
                    await dispatcher.Publish(new SendBotMessage(errorMsg, true));
                    return;
                }
                logger.LogInformation("Gave {username} {tickets} tickets via website.", username, ticketsWon);
                var winMessage = (await gameSettingsService.GetStringSetting(GAMENAME, WINMESSAGE, "{Name} just got {Amount} bonus tickets from the bot interface and now has {Total} tickets.")) ?? string.Empty;
                winMessage = winMessage
                    .Replace("{Name}", username, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Amount}", ticketsWon.ToString("N0"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{Total}", amount.ToString("N0"), StringComparison.OrdinalIgnoreCase);
                await dispatcher.Publish(new SendBotMessage(winMessage, true));
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
            return pointSystem.RegisterDefaultPointForGame(GAMENAME);
        }
    }
}
