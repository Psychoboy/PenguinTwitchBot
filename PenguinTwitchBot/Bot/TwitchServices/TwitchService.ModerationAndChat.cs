using PenguinTwitchBot.TwitchApi.Models.Chat;
using PenguinTwitchBot.TwitchApi.Models.Moderation;

namespace PenguinTwitchBot.Bot.TwitchServices
{
    public partial class TwitchService
    {
        public async Task SendMesssageAsStreamer(string message)
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if(string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return;
                }
                var msg = new SendChatMessageRequest
                {
                    BroadcasterId = broadcasterId,
                    SenderId = broadcasterId,
                    Message = message
                };
                var result = await _chatClient.SendChatMessageAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    msg);
                if (result.Data[0].IsSent == false)
                {
                    _logger.LogWarning("Message failed to send: {reason}", result?.Data[0]?.DropReason?.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message.");
            }
        }

        public async Task<IEnumerable<Chatter>> GetCurrentChatters()
        {
            if(!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                throw new InvalidOperationException("Failed to refresh token");
            }
            var broadcasterId = await GetBroadcasterUserId();
            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                _logger.LogError("Error getting broadcaster id.");
                return [];
            }
            var after = "";
            List<Chatter> chatters = [];
            try
            {
                while (true)
                {
                    var curChatters = await _chatClient.GetChattersAsync(
                        _configuration["twitchClientId"]!,
                        _accessToken,
                        broadcasterId,
                        broadcasterId,
                        after);
                    if (curChatters != null)
                    {
                        chatters.AddRange(curChatters.Data);
                        if (string.IsNullOrEmpty(curChatters.Cursor))
                        {
                            break;
                        }
                        after = curChatters.Cursor;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chatters.");
            }
            return chatters;
        }

        public async Task<bool> WillBePermittedByAutomod(string message)
        {
            if(!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                return false;
            }
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    _logger.LogError("Error getting broadcaster id.");
                    return false;
                }

                var result = await _moderationClient.CheckAutoModStatusAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    new()
                {
                    new AutoModMessage(Guid.NewGuid().ToString(), message)
                }, broadcasterId);
                if (result == null)
                {
                    _logger.LogWarning("Failed to check automod message: null response.");
                    return false;
                }
                return result.Data.FirstOrDefault()?.IsPermitted ?? false;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking automod.");
                return false;
            }
        }

        public async Task<List<BannedUser>> GetAllBannedViewers()
        {
            if(!await ValidateAndRefreshToken())
            {
                _logger.LogError("Failed to refresh token");
                throw new InvalidOperationException("Failed to refresh token");
            }    
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting user id.");
            var after = "";
            List<BannedUser> curBannedUsers = [];
            while (true)
            {
                var bannedUsers = await _moderationClient.GetBannedUsersAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    userId,
                    after);
                if (bannedUsers != null)
                {
                    curBannedUsers.AddRange(bannedUsers.Data);
                    if (string.IsNullOrEmpty(bannedUsers.Cursor))
                    {
                        break;
                    }
                    after = bannedUsers.Cursor;
                }
            }
            return curBannedUsers;
        }

        public async Task<bool> RaidStreamer(string userId)
        {
            var broadcasterId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                await _raidsClient.StartRaidAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    userId);
                return true;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing Raid: {error}", error);
                return false;
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing RaidStreamer(): {error}", error);
                return false;
            }
        }

        public async Task<ShoutoutResponseEnum> ShoutoutStreamer(string userId)
        {
            var broadcasterId = await GetBroadcasterUserId() ?? throw new Exception("Error getting broadcaster id.");
            try
            {
                await _chatClient.SendShoutoutAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    userId,
                    broadcasterId);
                return ShoutoutResponseEnum.Success;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing shoutout: {error}", error);
                if (error.Contains("429", StringComparison.OrdinalIgnoreCase) || error.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
                {
                    return ShoutoutResponseEnum.TooManyRequests;
                }
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing ShoutoutStreamer(string): {error}", error);
            }
            return ShoutoutResponseEnum.Failure;
        }

        public async Task TimeoutUser(string name, string reason, int? length)
        {
            var broadcasterId = await GetBroadcasterUserId();
            if (broadcasterId == null)
            {
                _logger.LogError("Error getting broadcaster id.");
                return;
            }
            var userId = await GetUserId(name);
            if (userId == null)
            {
                _logger.LogError("Error getting user id.");
                return;
            }
            try
            {
                await _moderationClient.BanUserAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    broadcasterId,
                    new()
                    {
                        UserId = userId,
                        Reason = reason,
                        Duration = length
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error timing out user");
            }
        }

        public async Task Announcement(string message)
        {
            var broadcasterId = await GetBroadcasterUserId();
            if (broadcasterId == null)
            {
                _logger.LogError("Error getting broadcaster id.");
                return;
            }

            try
            {
                await _chatClient.SendChatAnnouncementAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    broadcasterId,
                    message);
                _logger.LogInformation("Announcement sent: {message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error doing announcement");
            }

        }

        public async Task DeleteMessage(string messageId)
        {
            var broadcasterId = await GetBroadcasterUserId();
            if (broadcasterId == null)
            {
                _logger.LogError("Error getting broadcaster id.");
                return;
            }
            try
            {
                await _moderationClient.DeleteChatMessagesAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    broadcasterId,
                    messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message");
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, string>> GetChatBadgesAsync()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var globalBadges = await _chatClient.GetGlobalChatBadgesAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken);
                if (globalBadges != null)
                {
                    foreach (var set in globalBadges)
                    {
                        foreach (var version in set.Versions)
                        {
                            result[$"{set.SetId}/{version.Id}"] = version.ImageUrl1x;
                        }
                    }
                }

                var broadcasterId = await GetBroadcasterUserId();
                if (!string.IsNullOrEmpty(broadcasterId))
                {
                    var channelBadges = await _chatClient.GetChannelChatBadgesAsync(
                        _configuration["twitchClientId"]!,
                        _accessToken,
                        broadcasterId);
                    if (channelBadges != null)
                    {
                        foreach (var set in channelBadges)
                        {
                            foreach (var version in set.Versions)
                            {
                                // Channel badges override globals for the same key
                                result[$"{set.SetId}/{version.Id}"] = version.ImageUrl1x;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch chat badges");
            }
            return result;
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, string>> GetChatEmotesAsync()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await ValidateAndRefreshToken();

                try
                {
                    var globalEmotes = await _chatClient.GetGlobalEmotesAsync(
                        _configuration["twitchClientId"]!,
                        _accessToken);
                    foreach (var emote in globalEmotes)
                    {
                        var url = !string.IsNullOrEmpty(emote.Id)
                            ? $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.Id}/default/dark/2.0"
                            : (!string.IsNullOrEmpty(emote.ImageUrl1x) ? emote.ImageUrl1x : string.Empty);
                        result[emote.Name] = url;
                    }
                }
                catch (Exception gEx)
                {
                    _logger.LogWarning(gEx, "Failed to fetch global chat emotes");
                }

                var broadcasterId = await GetBroadcasterUserId();
                if (!string.IsNullOrEmpty(broadcasterId))
                {
                    try
                    {
                        var channelEmotes = await _chatClient.GetChannelEmotesAsync(
                            _configuration["twitchClientId"]!,
                            _accessToken,
                            broadcasterId);
                        foreach (var emote in channelEmotes)
                        {
                            // Channel emotes override globals for the same name
                            var url = !string.IsNullOrEmpty(emote.Id)
                                ? $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.Id}/default/dark/2.0"
                                : (!string.IsNullOrEmpty(emote.ImageUrl1x) ? emote.ImageUrl1x : string.Empty);
                            result[emote.Name] = url;
                        }
                    }
                    catch (Exception cEx)
                    {
                        _logger.LogWarning(cEx, "Failed to fetch channel chat emotes for broadcaster {BroadcasterId}", broadcasterId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch chat emotes");
            }
            return result;
        }
    }
}
