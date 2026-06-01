using PenguinTwitchBot.Bot.Twitch.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLibCustomReward = TwitchLib.Api.Helix.Models.ChannelPoints.CustomReward;

namespace PenguinTwitchBot.Bot.Twitch.Helix;

public sealed class ChannelPointsClient(ILogger<ChannelPointsClient> logger, IChannelPointsTransport transport) : TwitchClientRetryBase(logger), IChannelPointsClient
{

    public Task<GetCustomRewardsResponse> GetCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, List<string>? rewardIds = null, bool onlyManageableRewards = false)
    {
        return ExecuteWithRetryAsync(() => transport.GetCustomRewardAsync(clientId, accessToken, broadcasterId, rewardIds, onlyManageableRewards), "fetch custom rewards");
    }

    public Task CreateCustomRewardsAsync(string clientId, string? accessToken, string broadcasterId, CreateChannelPointRewardRequest request)
    {
        return ExecuteWithRetryAsync(() => transport.CreateCustomRewardsAsync(clientId, accessToken, broadcasterId, MapToTwitchRequest(request)), "create custom reward");
    }

    public Task UpdateCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId, UpdateCustomRewardRequest request)
    {
        return ExecuteWithRetryAsync(() => transport.UpdateCustomRewardAsync(clientId, accessToken, broadcasterId, rewardId, request), "update custom reward");
    }

    public Task DeleteCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId)
    {
        return ExecuteWithRetryAsync(() => transport.DeleteCustomRewardAsync(clientId, accessToken, broadcasterId, rewardId), "delete custom reward");
    }

    public static ChannelPointReward MapToChannelPointReward(TwitchLibCustomReward source) =>
        new(
            Id: source.Id,
            Title: source.Title,
            IsEnabled: source.IsEnabled,
            IsPaused: source.IsPaused,
            Cost: source.Cost,
            Prompt: source.Prompt,
            IsUserInputRequired: source.IsUserInputRequired,
            BackgroundColor: source.BackgroundColor,
            ShouldRedemptionsSkipQueue: source.ShouldRedemptionsSkipQueue,
            IsMaxPerStreamEnabled: source.MaxPerStreamSetting?.IsEnabled,
            MaxPerStream: source.MaxPerStreamSetting?.MaxPerStream,
            IsMaxPerUserPerStreamEnabled: source.MaxPerUserPerStreamSetting?.IsEnabled,
            MaxPerUserPerStream: source.MaxPerUserPerStreamSetting?.MaxPerUserPerStream,
            IsGlobalCooldownEnabled: source.GlobalCooldownSetting?.IsEnabled,
            GlobalCooldownSeconds: source.GlobalCooldownSetting?.GlobalCooldownSeconds);

    public static CreateCustomRewardsRequest MapToTwitchRequest(CreateChannelPointRewardRequest source) =>
        new()
        {
            Title = source.Title,
            Cost = source.Cost,
            Prompt = source.Prompt,
            IsEnabled = source.IsEnabled,
            IsUserInputRequired = source.IsUserInputRequired,
            ShouldRedemptionsSkipRequestQueue = source.ShouldRedemptionsSkipRequestQueue,
            IsMaxPerStreamEnabled = source.IsMaxPerStreamEnabled,
            MaxPerStream = source.MaxPerStream,
            IsMaxPerUserPerStreamEnabled = source.IsMaxPerUserPerStreamEnabled,
            MaxPerUserPerStream = source.MaxPerUserPerStream,
            IsGlobalCooldownEnabled = source.IsGlobalCooldownEnabled,
            GlobalCooldownSeconds = source.GlobalCooldownSeconds,
            BackgroundColor = source.BackgroundColor
        };
}
