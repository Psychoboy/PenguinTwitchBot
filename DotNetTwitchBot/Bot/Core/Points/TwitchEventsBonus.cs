using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Models.Points;
using System.Collections.Concurrent;
using MediatR;

namespace DotNetTwitchBot.Bot.Core.Points
{
    public class TwitchEventsBonus(
        ILogger<TwitchEventsBonus> logger,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IGameSettingsService gameSettingsService,
        IMediator mediator,
        IPointsSystem pointsSystem
        ) : BaseCommandService(serviceBackbone, commandHandler, "TwitchEventBonus", mediator), IHostedService, ITwitchEventsBonus
    {
        private readonly ConcurrentDictionary<string, DateTime> SubCache = new();
        static readonly SemaphoreSlim _subscriptionLock = new(1);
        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override Task Register()
        {
            logger.LogInformation("Registered commands for {moduleName}", ModuleName);
            return pointsSystem.RegisterDefaultPointForGame(ModuleName);
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
            await gameSettingsService.SaveSetting(ModuleName, "PointsPerSub", numberOfPointsPerSub);
        }

        public async Task<int> GetPointsPerSub()
        {
            return await gameSettingsService.GetIntSetting(ModuleName, "PointsPerSub", 500);
        }

        public async Task SetBitsPerPoint(double numberOfPointsPerBit)
        {
            await gameSettingsService.SaveSetting(ModuleName, "BitsPerPoint", numberOfPointsPerBit);
        }

        public async Task<double> GetBitsPerPoint()
        {
            return await gameSettingsService.GetDoubleSetting(ModuleName, "BitsPerPoint", 1.0);
        }

        public async Task SetPointType(PointType pointType)
        {
            await pointsSystem.SetPointTypeForGame(ModuleName, pointType.GetId());
        }

        public async Task<PointType> GetPointType()
        {
            return await pointsSystem.GetPointTypeForGame(ModuleName);
        }

        private async Task OnCheer(object sender, CheerEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Name) || e.IsAnonymous || string.IsNullOrWhiteSpace(e.UserId))
            {
                await ServiceBackbone.SendChatMessage($"Someone just cheered {e.Amount} bits! sptvHype", PlatformType.Twitch);
                return;
            }
            try
            {
                var bitsPerPoint = await GetBitsPerPoint();
                if (bitsPerPoint > 0)
                {
                    var pointsToAward = (int)Math.Floor((double)e.Amount * bitsPerPoint);
                    if (pointsToAward < 1) return;
                    var pointType = await pointsSystem.GetPointTypeForGame(ModuleName);
                    logger.LogInformation("Gave {name} {points} {PointType} for cheering.", e.Name, pointsToAward, pointType.Name);
                    await pointsSystem.AddPointsByUserIdAndGame(e.UserId, PlatformType.Twitch, ModuleName, pointsToAward);
                    await ServiceBackbone.SendChatMessage($"{e.DisplayName} just cheered {e.Amount} bits! sptvHype", PlatformType.Twitch);
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
                var pointType = await pointsSystem.GetPointTypeForGame(ModuleName);
                logger.LogInformation("Gave {name} {points} {PointType} for subscribing.", args.Name, subPoints, pointType.Name);
                await pointsSystem.AddPointsByUserIdAndGame(args.UserId, PlatformType.Twitch, ModuleName, subPoints);
                var message = $"{args.DisplayName} gifted {args.GiftAmount} subscriptions to the channel! sptvHype sptvHype sptvHype";
                if (args.TotalGifted != null && args.TotalGifted > args.GiftAmount)
                {
                    message += $" They have gifted a total of {args.TotalGifted} subs to the channel!";
                }
                await ServiceBackbone.SendChatMessage(message, PlatformType.Twitch);
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
                if (SubCache.TryGetValue(name, out var subTime) && subTime > DateTime.Now.AddDays(-5))
                {
                    logger.LogWarning("{name} Subscriber already in sub cache", name);
                    return true;
                }
                SubCache[name] = DateTime.Now;
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
                    var pointType = await pointsSystem.GetPointTypeForGame(ModuleName);
                    logger.LogInformation("Gave {name} {points} {PointType} for subscribing.", e.Name, subPoints, pointType.Name);
                    await pointsSystem.AddPointsByUserIdAndGame(e.UserId, PlatformType.Twitch, ModuleName, subPoints);
                }

                if (!e.IsRenewal && e.HadPreviousSub) return;
                
                var message = $"{e.DisplayName} just subscribed";
                if (e.Count != null && e.Count > 0)
                {
                    message += $" for a total of {e.Count} months";
                }

                if (e.Streak != null && e.Streak > 0)
                {
                    if (e.Count != null && e.Count > 0) message += " and";
                    message += $" for {e.Streak} months in a row";
                }

                message += "! sptvHype";
                await ServiceBackbone.SendChatMessage(message, PlatformType.Twitch);

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
