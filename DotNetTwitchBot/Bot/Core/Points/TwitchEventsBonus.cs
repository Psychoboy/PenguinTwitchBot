using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Models.Points;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public class TwitchEventsBonus(
        ILogger<TwitchEventsBonus> logger,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IGameSettingsService gameSettingsService,
        Application.Notifications.IPenguinDispatcher dispatcher,
        IPointsSystem pointsSystem
        ) : BaseCommandService(serviceBackbone, commandHandler, GAMENAME, dispatcher), IHostedService, ITwitchEventsBonus
    {
        public static readonly string GAMENAME = "TwitchEventBonus";
        public static readonly string POINTSPERSUB = "PointsPerSub";
        public static readonly string BITSPERPOINT = "BitsPerPoint";
        public static readonly string CHEERMESSAGE = "CheerMessage";
        public static readonly string ANONYMOUSCHEERMESSAGE = "AnonymousCheerMessage";
        public static readonly string SUBMESSAGE = "SubMessage";
        public static readonly string SUBGIFTMESSAGE = "SubGiftMessage";
        public static readonly string SUBGIFTTOTALMESSAGE = "SubGiftTotalMessage";

        private readonly ConcurrentDictionary<string, DateTime> SubCache = new();
        static readonly SemaphoreSlim _subscriptionLock = new(1);
        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override Task Register()
        {
            logger.LogInformation("Registered commands for {moduleName}", GAMENAME);
            return pointsSystem.RegisterDefaultPointForGame(GAMENAME);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {moduledname}", ModuleName);
            ServiceBackbone.SubscriptionEvent += OnSubscription;
            ServiceBackbone.SubscriptionGiftEvent += OnSubScriptionGift;
            ServiceBackbone.CheerEvent += OnCheer;
            return Register();
        }

        public async Task SetPointsPerSub(int numberOfPointsPerSub)
        {
            await gameSettingsService.SaveSetting(GAMENAME, POINTSPERSUB, numberOfPointsPerSub);
        }

        public async Task<int> GetPointsPerSub()
        {
            return await gameSettingsService.GetIntSetting(GAMENAME, POINTSPERSUB, 500);
        }

        public async Task SetBitsPerPoint(double numberOfPointsPerBit)
        {
            await gameSettingsService.SaveSetting(GAMENAME, BITSPERPOINT, numberOfPointsPerBit);
        }

        public async Task<double> GetBitsPerPoint()
        {
            return await gameSettingsService.GetDoubleSetting(GAMENAME, BITSPERPOINT, 1.0);
        }

        public async Task SetPointType(PointType pointType)
        {
            await pointsSystem.SetPointTypeForGame(GAMENAME, pointType.GetId());
        }

        public async Task<PointType> GetPointType()
        {
            return await pointsSystem.GetPointTypeForGame(GAMENAME);
        }

        private async Task OnCheer(object sender, CheerEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Name) || e.IsAnonymous || string.IsNullOrWhiteSpace(e.UserId))
            {
                var anonMsg = (await gameSettingsService.GetStringSetting(GAMENAME, ANONYMOUSCHEERMESSAGE, "Someone just cheered {Amount} bits! sptvHype")) ?? string.Empty;
                anonMsg = anonMsg.Replace("{Amount}", e.Amount.ToString("N0"), StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.SendChatMessage(anonMsg, false);
                return;
            }
            try
            {
                var bitsPerPoint = await GetBitsPerPoint();
                if (bitsPerPoint > 0)
                {
                    var pointsToAward = (int)Math.Floor((double)e.Amount * bitsPerPoint);
                    if (pointsToAward < 1) return;
                    var pointType = await pointsSystem.GetPointTypeForGame(GAMENAME);
                    logger.LogInformation("Gave {name} {points} {PointType} for cheering.", e.Name, pointsToAward, pointType.Name);
                    await pointsSystem.AddPointsByUserIdAndGame(e.UserId, GAMENAME, pointsToAward);
                    var cheerMsg = (await gameSettingsService.GetStringSetting(GAMENAME, CHEERMESSAGE, "{Name} just cheered {Amount} bits! sptvHype")) ?? string.Empty;
                    cheerMsg = cheerMsg
                        .Replace("{Name}", e.DisplayName, StringComparison.OrdinalIgnoreCase)
                        .Replace("{Amount}", e.Amount.ToString("N0"), StringComparison.OrdinalIgnoreCase);
                    await ServiceBackbone.SendChatMessage(cheerMsg, false);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error when processing cheer for {user}", e.Name);
            }
        }

        private async Task OnSubScriptionGift(object sender, SubscriptionGiftEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Name) || string.IsNullOrWhiteSpace(args.UserId)) return;
            try
            {
                var subPoints = await GetPointsPerSub() * args.GiftAmount;
                var pointType = await pointsSystem.GetPointTypeForGame(GAMENAME);
                logger.LogInformation("Gave {name} {points} {PointType} for subscribing.", args.Name, subPoints, pointType.Name);
                await pointsSystem.AddPointsByUserIdAndGame(args.UserId, GAMENAME, subPoints);
                var giftMsg = (await gameSettingsService.GetStringSetting(GAMENAME, SUBGIFTMESSAGE, "{Name} gifted {Amount} subscriptions to the channel! sptvHype sptvHype sptvHype")) ?? string.Empty;
                giftMsg = giftMsg
                    .Replace("{Name}", args.DisplayName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Amount}", args.GiftAmount.ToString("N0"), StringComparison.OrdinalIgnoreCase);
                if (args.TotalGifted != null && args.TotalGifted > args.GiftAmount)
                {
                    var totalMsg = (await gameSettingsService.GetStringSetting(GAMENAME, SUBGIFTTOTALMESSAGE, " They have gifted a total of {Total} subs to the channel!")) ?? string.Empty;
                    giftMsg += totalMsg.Replace("{Total}", args.TotalGifted.Value.ToString("N0"), StringComparison.OrdinalIgnoreCase);
                }
                await ServiceBackbone.SendChatMessage(giftMsg, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error when processing subscription for {user}", args.Name);
            }
        }

        private bool CheckIfExistsAndAddSubCache(string name)
        {
            try
            {
                _subscriptionLock.Wait();

                if (string.IsNullOrWhiteSpace(name))
                {
                    logger.LogWarning("Subscriber name was null or white space");
                    return false;
                }
                if (SubCache.TryGetValue(name, out var subTime) && subTime > DateTime.UtcNow.AddDays(-5))
                {
                    logger.LogWarning("{name} Subscriber already in sub cache", name);
                    return true;
                }
                SubCache[name] = DateTime.UtcNow;
                return false;
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }

        private async Task OnSubscription(object sender, SubscriptionEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Name) || string.IsNullOrWhiteSpace(e.UserId)) return;
            if (e.IsGift) return;
            try
            {
                if (!CheckIfExistsAndAddSubCache(e.UserId))
                {
                    var subPoints = await GetPointsPerSub();
                    var pointType = await pointsSystem.GetPointTypeForGame(GAMENAME);
                    logger.LogInformation("Gave {name} {points} {PointType} for subscribing.", e.Name, subPoints, pointType.Name);
                    await pointsSystem.AddPointsByUserIdAndGame(e.UserId, GAMENAME, subPoints);
                }

                if (!e.IsRenewal && e.HadPreviousSub) return;

                var details = string.Empty;
                if (e.Count != null && e.Count > 0)
                    details += $" for a total of {e.Count} months";
                if (e.Streak != null && e.Streak > 0)
                {
                    if (e.Count != null && e.Count > 0) details += " and";
                    details += $" for {e.Streak} months in a row";
                }
                var subMsgTemplate = (await gameSettingsService.GetStringSetting(GAMENAME, SUBMESSAGE, "{Name} just subscribed{Details}! sptvHype")) ?? string.Empty;
                var message = subMsgTemplate
                    .Replace("{Name}", e.DisplayName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Details}", details, StringComparison.OrdinalIgnoreCase);
                await ServiceBackbone.SendChatMessage(message, false);

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error when processing subscription for {user}", e.Name);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            ServiceBackbone.SubscriptionEvent -= OnSubscription;
            ServiceBackbone.SubscriptionGiftEvent -= OnSubScriptionGift;
            ServiceBackbone.CheerEvent -= OnCheer;
            return Task.CompletedTask;
        }
    }
}
