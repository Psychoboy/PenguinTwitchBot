using PenguinTwitchBot.Bot.Actions.Recipes;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private List<PenguinTwitchBot.TwitchApi.Models.ChannelPoints.ChannelPointReward> _channelPointRewards = [];
    private List<string> _channelPointRewardTitles = [];

    private bool IsCreatingChannelPointReward =>
        GetParameter("RewardMode").Equals("CreateNew", StringComparison.OrdinalIgnoreCase);

    private async Task LoadChannelPointRewardsAsync()
    {
        _channelPointRewards = [];
        _channelPointRewardTitles = [];
        if (SelectedRecipe is not ChannelPointActionRecipe)
            return;

        try
        {
            _channelPointRewards = (await TwitchService.GetChannelPointRewards(true)).ToList();
            _channelPointRewardTitles = _channelPointRewards
                .Select(reward => reward.Title)
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (_channelPointRewardTitles.Count == 0)
            {
                SetParameter("RewardMode", "CreateNew");
            }
            else if (!IsCreatingChannelPointReward &&
                     !_channelPointRewardTitles.Contains(GetParameter("RewardTitle"), StringComparer.OrdinalIgnoreCase))
            {
                SetParameter("RewardTitle", _channelPointRewardTitles[0]);
            }
        }
        catch (Exception ex)
        {
            _error = $"Could not load channel point rewards: {ex.Message}";
        }
    }

    private async Task<Guid?> CreateChannelPointRewardAsync()
    {
        if (SelectedRecipe is not ChannelPointActionRecipe channelPointRecipe)
            return null;

        var request = channelPointRecipe.BuildRewardRequest(_parameters);
        if (_channelPointRewardTitles.Contains(request.Title, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("A reward with that title already exists.");

        await TwitchService.CreateChannelPointReward(request);
        SetParameter("RewardTitle", request.Title);
        var rewards = (await TwitchService.GetChannelPointRewards(true)).ToList();
        _channelPointRewards = rewards;
        _channelPointRewardTitles = rewards.Select(reward => reward.Title)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var createdReward = rewards.FirstOrDefault(reward => reward.Title.Equals(request.Title, StringComparison.OrdinalIgnoreCase));
        if (createdReward is null || !Guid.TryParse(createdReward.Id, out var rewardId))
            throw new InvalidOperationException("Twitch created the reward, but the new reward ID could not be loaded.");

        return rewardId;
    }

    private Task OnChannelPointRewardModeChanged(string? mode)
    {
        SetParameter("RewardMode", mode);
        if (!IsCreatingChannelPointReward && _channelPointRewardTitles.Count > 0 &&
            !_channelPointRewardTitles.Contains(GetParameter("RewardTitle"), StringComparer.OrdinalIgnoreCase))
        {
            SetParameter("RewardTitle", _channelPointRewardTitles[0]);
        }
        return Task.CompletedTask;
    }

    private Task OnChannelPointRewardTitleChanged(string? title)
    {
        SetParameter("RewardTitle", title);
        return Task.CompletedTask;
    }

    private int GetChannelPointRewardCost() =>
        int.TryParse(GetParameter("RewardCost"), out var cost) ? cost : 100;

    private Task OnChannelPointRewardCostChanged(int cost)
    {
        SetParameter("RewardCost", cost.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    }

    private bool GetRewardBoolean(string key) =>
        bool.TryParse(GetParameter(key), out var value) && value;

    private Task OnRewardBooleanChanged(string key, bool value)
    {
        SetParameter(key, value.ToString());
        return Task.CompletedTask;
    }

    private bool IsTwitchAlertEnabled =>
        bool.TryParse(GetParameter("AlertEnabled"), out var enabled) && enabled;

    private Task OnTwitchAlertEnabledChanged(bool enabled)
    {
        SetParameter("AlertEnabled", enabled.ToString());
        if (enabled)
            SyncAlertRecipeParameters();
        return Task.CompletedTask;
    }

    private int GetTwitchFireworksCount() =>
        int.TryParse(GetParameter("FireworksCount"), out var count) ? count : 0;

    private Task OnTwitchFireworksCountChanged(int count)
    {
        SetParameter("FireworksCount", count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    }
}