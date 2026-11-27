using PenguinTwitchBot.TwitchApi.Models.ChannelPoints;
using PenguinTwitchBot.TwitchApi.Models.Subscriptions;

namespace PenguinTwitchBot.Bot.TwitchServices
{
    public partial class TwitchService
    {
        public async Task<ChannelPointReward?> GetCustomReward(string id)
        {
            if(!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                return null;
            }
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return null;
                }

                var result = await _channelPointsClient.GetCustomRewardAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    [id]);
                return result.Data.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting custom reward.");
            }
            return null;
        }

        public async Task CreateChannelPointReward(CreateChannelPointRewardRequest request)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return;
                }

                await _channelPointsClient.CreateCustomRewardsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating channel point reward");
                throw;
            }
        }

        public async Task<IEnumerable<ChannelPointReward>> GetChannelPointRewards()
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return [];
                }

                var rewards = await _channelPointsClient.GetCustomRewardAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId);
                return rewards.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting channel point rewards.");
            }
            return [];
        }

        public async Task<IEnumerable<ChannelPointReward>> GetChannelPointRewards(bool onlyManageable)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return [];
                }

                var rewards = await _channelPointsClient.GetCustomRewardAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    onlyManageableRewards: onlyManageable);
                return rewards.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting channel point rewards.");
            }
            return [];
        }

        public async Task UpdateChannelPointReward(string rewardId, UpdateCustomRewardRequest request)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return;
                }

                await _channelPointsClient.UpdateCustomRewardAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    rewardId,
                    request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating channel point rewards.");
            }
        }

        public async Task DeleteChannelPointReward(string rewardId)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return;
                }

                await _channelPointsClient.DeleteCustomRewardAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    rewardId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting channel point rewards.");
            }
        }

        public async Task<List<Subscription>> GetAllSubscriptions()
        {
            var snapshot = await GetBroadcasterSubscriptionsSnapshotAsync();
            return snapshot.Subscriptions;
        }

        public async Task<BroadcasterSubscriptionsSnapshot> GetBroadcasterSubscriptionsSnapshotAsync(CancellationToken cancellationToken = default)
        {
            if (!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                throw new InvalidOperationException("Failed to refresh token");
            }
            var userId = await GetBroadcasterUserId() ?? throw new InvalidOperationException("Error getting broadcaster user id.");
            List<Subscription> subs = [];
            string? after = null;
            var seenCursors = new HashSet<string>(StringComparer.Ordinal);
            var total = 0;
            var points = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var curSubs = await _subscriptionsClient.GetBroadcasterSubscriptionsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    userId,
                    100,
                    after);
                if (curSubs == null)
                {
                    break;
                }

                subs.AddRange(curSubs.Data);
                total = curSubs.Total;
                points = curSubs.Points;

                if (string.IsNullOrEmpty(curSubs.Cursor))
                {
                    break;
                }

                if (!seenCursors.Add(curSubs.Cursor))
                {
                    throw new InvalidOperationException($"Twitch subscription pagination returned a repeated cursor ('{curSubs.Cursor}').");
                }

                after = curSubs.Cursor;
            }

            return new BroadcasterSubscriptionsSnapshot(subs, total, points);
        }

        public async Task<List<Follower>> GetAllFollowersAsync(
            IProgress<Core.FollowerSyncProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                throw new InvalidOperationException("Failed to refresh token");
            }

            var broadcasterId = await GetBroadcasterUserId() ?? throw new InvalidOperationException("Error getting broadcaster user id.");
            List<Follower> followers = [];
            string? after = null;
            var seenCursors = new HashSet<string>(StringComparer.Ordinal);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var response = await _channelsClient.GetChannelFollowersAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    userId: null,
                    first: 100,
                    after: after);

                if (response == null)
                {
                    break;
                }

                foreach (var item in response.Data)
                {
                    var followDate = DateTimeOffset.TryParse(
                        item.FollowedAt,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out var parsed)
                        ? parsed.UtcDateTime
                        : DateTime.UtcNow;

                    followers.Add(new Follower
                    {
                        UserId = item.UserId,
                        Username = item.UserLogin,
                        DisplayName = item.UserName,
                        FollowDate = followDate
                    });
                }

                progress?.Report(new Core.FollowerSyncProgress(followers.Count, response.Total));

                if (string.IsNullOrEmpty(response.Cursor))
                {
                    break;
                }

                if (!seenCursors.Add(response.Cursor))
                {
                    throw new InvalidOperationException($"Twitch follower pagination returned a repeated cursor ('{response.Cursor}').");
                }

                after = response.Cursor;
            }

            return followers;
        }
    }
}
