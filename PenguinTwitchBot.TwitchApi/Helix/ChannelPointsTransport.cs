using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.TwitchApi.Helix;

public sealed class ChannelPointsTransport : IChannelPointsTransport
{
    public async Task<GetChannelPointRewardsResponse> GetCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, List<string>? rewardIds = null, bool onlyManageableRewards = false)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = BuildRewardQueryUrl(broadcasterId, rewardIds, onlyManageableRewards);
        using var response = await http.GetAsync(HelixHttp.BuildUrl(url));
        response.EnsureSuccessStatusCode();

        var payload = await HelixJson.DeserializeAsync<GetCustomRewardsApiResponse>(response);
        var rewards = payload?.Data.Select(MapToReward).ToList() ?? [];
        return new GetChannelPointRewardsResponse(rewards);
    }

    public async Task CreateCustomRewardsAsync(string clientId, string? accessToken, string broadcasterId, CreateChannelPointRewardRequest request)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = HelixHttp.BuildUrl($"channel_points/custom_rewards?broadcaster_id={Uri.EscapeDataString(broadcasterId)}");
        using var response = await http.PostAsync(url, HelixJson.CreateJsonContent(MapToCreateRequest(request), ignoreNullValues: true));
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId, UpdateCustomRewardRequest request)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = HelixHttp.BuildUrl($"channel_points/custom_rewards?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&id={Uri.EscapeDataString(rewardId)}");
        using var response = await http.PatchAsync(url, HelixJson.CreateJsonContent(MapToUpdateRequest(request), ignoreNullValues: true));
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCustomRewardAsync(string clientId, string? accessToken, string broadcasterId, string rewardId)
    {
        using var http = HelixHttp.CreateClient(clientId, accessToken);
        var url = HelixHttp.BuildUrl($"channel_points/custom_rewards?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&id={Uri.EscapeDataString(rewardId)}");
        using var response = await http.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }

    private static string BuildRewardQueryUrl(string broadcasterId, List<string>? rewardIds, bool onlyManageableRewards)
    {
        var queryParts = new List<string>
        {
            $"broadcaster_id={Uri.EscapeDataString(broadcasterId)}",
            $"only_manageable_rewards={onlyManageableRewards.ToString().ToLowerInvariant()}"
        };

        if (rewardIds != null)
        {
            foreach (var rewardId in rewardIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                queryParts.Add($"id={Uri.EscapeDataString(rewardId)}");
            }
        }

        return $"channel_points/custom_rewards?{string.Join("&", queryParts)}";
    }

    private static ChannelPointReward MapToReward(CustomRewardApiItem source)
    {
        return new ChannelPointReward(
            Id: source.Id,
            Title: source.Title,
            IsEnabled: source.IsEnabled,
            IsPaused: source.IsPaused,
            Cost: source.Cost,
            Prompt: source.Prompt,
            IsUserInputRequired: source.IsUserInputRequired,
            BackgroundColor: source.BackgroundColor,
            ShouldRedemptionsSkipQueue: source.ShouldRedemptionsSkipRequestQueue,
            IsMaxPerStreamEnabled: source.MaxPerStreamSetting?.IsEnabled,
            MaxPerStream: source.MaxPerStreamSetting?.MaxPerStream,
            IsMaxPerUserPerStreamEnabled: source.MaxPerUserPerStreamSetting?.IsEnabled,
            MaxPerUserPerStream: source.MaxPerUserPerStreamSetting?.MaxPerUserPerStream,
            IsGlobalCooldownEnabled: source.GlobalCooldownSetting?.IsEnabled,
            GlobalCooldownSeconds: source.GlobalCooldownSetting?.GlobalCooldownSeconds);
    }

    private static CreateCustomRewardApiRequest MapToCreateRequest(CreateChannelPointRewardRequest source)
    {
        return new CreateCustomRewardApiRequest(
            Title: source.Title,
            Cost: source.Cost,
            Prompt: source.Prompt,
            IsEnabled: source.IsEnabled,
            BackgroundColor: source.BackgroundColor,
            IsUserInputRequired: source.IsUserInputRequired,
            IsMaxPerStreamEnabled: source.IsMaxPerStreamEnabled,
            MaxPerStream: source.MaxPerStream,
            IsMaxPerUserPerStreamEnabled: source.IsMaxPerUserPerStreamEnabled,
            MaxPerUserPerStream: source.MaxPerUserPerStream,
            IsGlobalCooldownEnabled: source.IsGlobalCooldownEnabled,
            GlobalCooldownSeconds: source.GlobalCooldownSeconds,
            ShouldRedemptionsSkipRequestQueue: source.ShouldRedemptionsSkipRequestQueue);
    }

    private static UpdateCustomRewardApiRequest MapToUpdateRequest(UpdateCustomRewardRequest source)
    {
        return new UpdateCustomRewardApiRequest(
            IsEnabled: source.IsEnabled,
            IsPaused: source.IsPaused,
            Title: source.Title,
            Cost: source.Cost,
            Prompt: source.Prompt,
            BackgroundColor: source.BackgroundColor,
            IsUserInputRequired: source.IsUserInputRequired,
            ShouldRedemptionsSkipRequestQueue: source.ShouldRedemptionsSkipRequestQueue,
            IsMaxPerStreamEnabled: source.IsMaxPerStreamEnabled,
            MaxPerStream: source.MaxPerStream,
            IsMaxPerUserPerStreamEnabled: source.IsMaxPerUserPerStreamEnabled,
            MaxPerUserPerStream: source.MaxPerUserPerStream,
            IsGlobalCooldownEnabled: source.IsGlobalCooldownEnabled,
            GlobalCooldownSeconds: source.GlobalCooldownSeconds);
    }

    private sealed record GetCustomRewardsApiResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<CustomRewardApiItem> Data);

    private sealed record CustomRewardApiItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("cost")] int Cost,
        [property: JsonPropertyName("is_enabled")] bool IsEnabled,
        [property: JsonPropertyName("is_user_input_required")] bool IsUserInputRequired,
        [property: JsonPropertyName("background_color")] string BackgroundColor,
        [property: JsonPropertyName("is_paused")] bool IsPaused,
        [property: JsonPropertyName("should_redemptions_skip_request_queue")] bool ShouldRedemptionsSkipRequestQueue,
        [property: JsonPropertyName("max_per_stream_setting")] MaxPerStreamSettingApiItem? MaxPerStreamSetting,
        [property: JsonPropertyName("max_per_user_per_stream_setting")] MaxPerUserPerStreamSettingApiItem? MaxPerUserPerStreamSetting,
        [property: JsonPropertyName("global_cooldown_setting")] GlobalCooldownSettingApiItem? GlobalCooldownSetting);

    private sealed record MaxPerStreamSettingApiItem(
        [property: JsonPropertyName("is_enabled")] bool IsEnabled,
        [property: JsonPropertyName("max_per_stream")] int MaxPerStream);

    private sealed record MaxPerUserPerStreamSettingApiItem(
        [property: JsonPropertyName("is_enabled")] bool IsEnabled,
        [property: JsonPropertyName("max_per_user_per_stream")] int MaxPerUserPerStream);

    private sealed record GlobalCooldownSettingApiItem(
        [property: JsonPropertyName("is_enabled")] bool IsEnabled,
        [property: JsonPropertyName("global_cooldown_seconds")] int GlobalCooldownSeconds);

    private sealed record CreateCustomRewardApiRequest(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("cost")] int Cost,
        [property: JsonPropertyName("prompt")] string? Prompt,
        [property: JsonPropertyName("is_enabled")] bool IsEnabled,
        [property: JsonPropertyName("background_color")] string? BackgroundColor,
        [property: JsonPropertyName("is_user_input_required")] bool IsUserInputRequired,
        [property: JsonPropertyName("is_max_per_stream_enabled")] bool IsMaxPerStreamEnabled,
        [property: JsonPropertyName("max_per_stream")] int? MaxPerStream,
        [property: JsonPropertyName("is_max_per_user_per_stream_enabled")] bool IsMaxPerUserPerStreamEnabled,
        [property: JsonPropertyName("max_per_user_per_stream")] int? MaxPerUserPerStream,
        [property: JsonPropertyName("is_global_cooldown_enabled")] bool IsGlobalCooldownEnabled,
        [property: JsonPropertyName("global_cooldown_seconds")] int? GlobalCooldownSeconds,
        [property: JsonPropertyName("should_redemptions_skip_request_queue")] bool ShouldRedemptionsSkipRequestQueue);

    private sealed record UpdateCustomRewardApiRequest(
        [property: JsonPropertyName("is_enabled")] bool? IsEnabled,
        [property: JsonPropertyName("is_paused")] bool? IsPaused,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("cost")] int? Cost,
        [property: JsonPropertyName("prompt")] string? Prompt,
        [property: JsonPropertyName("background_color")] string? BackgroundColor,
        [property: JsonPropertyName("is_user_input_required")] bool? IsUserInputRequired,
        [property: JsonPropertyName("should_redemptions_skip_request_queue")] bool? ShouldRedemptionsSkipRequestQueue,
        [property: JsonPropertyName("is_max_per_stream_enabled")] bool? IsMaxPerStreamEnabled,
        [property: JsonPropertyName("max_per_stream")] int? MaxPerStream,
        [property: JsonPropertyName("is_max_per_user_per_stream_enabled")] bool? IsMaxPerUserPerStreamEnabled,
        [property: JsonPropertyName("max_per_user_per_stream")] int? MaxPerUserPerStream,
        [property: JsonPropertyName("is_global_cooldown_enabled")] bool? IsGlobalCooldownEnabled,
        [property: JsonPropertyName("global_cooldown_seconds")] int? GlobalCooldownSeconds);
}
