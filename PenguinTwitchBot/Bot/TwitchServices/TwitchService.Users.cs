using PenguinTwitchBot.TwitchApi.Models.Channels;
using PenguinTwitchBot.TwitchApi.Models.Users;

namespace PenguinTwitchBot.Bot.TwitchServices
{
    public partial class TwitchService
    {
        public async Task<string?> GetBroadcasterUserId()
        {
            var broadcaster = _configuration["broadcaster"];
            if (broadcaster == null) return null;
            if (string.IsNullOrEmpty(broadcasterId))
            {
                broadcasterId = await GetUserId(broadcaster);
            }
            return broadcasterId;
        }

        public async Task<string?> GetBroadcasterProfileImageUrl()
        {
            var broadcaster = _configuration["broadcaster"];
            if (string.IsNullOrWhiteSpace(broadcaster)) return null;
            var user = await GetUserByName(broadcaster);
            return user?.ProfileImageUrl;
        }

        public async Task<string?> GetBotUserId()
        {
            var bot = _configuration["botName"];
            if (bot == null) return null;
            if (string.IsNullOrEmpty(botId))
            {
                botId = await GetUserId(bot);
            }
            return botId;
        }

        public async Task<string?> GetUserId(string user)
        {
            return (await GetUserByName(user))?.Id;
        }

        public async Task<User?> GetUserByName(string user)
        {
            try
            {
                if (UserCache.TryGetValue(user, out var cachedEntry) &&
                    DateTime.UtcNow - cachedEntry.CachedAtUtc < UserCacheTtl)
                {
                    return cachedEntry.User;
                }
                await ValidateAndRefreshToken();
                var users = await _usersClient.GetUsersAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    null,
                    [user]);
                var userObj = users.Users.FirstOrDefault();
                var now = DateTime.UtcNow;
                if (userObj != null)
                {
                    UserCache[user] = new CachedUserEntry(userObj, now);
                    return userObj;
                }
                UserCache[user] = new CachedUserEntry(null, now);
                return null;
            }
            catch (Exception ex) when (ex.GetType().Name == "TooManyRequestsException")
            {
                return null;
            }
            catch (Exception)
            {
                var safeUser = (user ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
                _logger.LogCritical("GetUserByName - Failed getting user: {user}", safeUser);
                return null;
            }
        }

        public async Task<User?> GetUserById(string userId)
        {
            try
            {
                var users = await _usersClient.GetUsersAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    [userId],
                    null);
                var userObj = users.Users.FirstOrDefault();
                return userObj;
            }
            catch (Exception ex) when (ex.GetType().Name == "TooManyRequestsException")
            {
                return null;
            }
            catch (Exception)
            {
                _logger.LogCritical("GetUserById - Failed getting user: {user}", userId);
                return null;
            }
        }

        public async Task<List<User?>?> GetUsers(List<string> userNames)
        {
            try
            {
                var users = await _usersClient.GetUsersAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    null,
                    userNames);
                if (users == null) throw new Exception("Got a null response");
                var mappedUsers = users.Users.Select(u => (User?)u).ToList();
                var now = DateTime.UtcNow;
                foreach (var user in users.Users)
                {
                    UserCache[user.Login] = new CachedUserEntry(user, now);
                    UserByIdCache[user.Id] = new CachedUserEntry(user, now);
                }
                return mappedUsers;
            }
            catch (Exception ex) when (ex.GetType().Name == "TooManyRequestsException")
            {
                return null;
            }
            catch (Exception)
            {
                _logger.LogCritical("Failed getting users: {users}", string.Join(", ", userNames));
                return null;
            }
        }

        public async Task<Dictionary<string, User>> GetUsersByIdsAsync(IEnumerable<string> userIds)
        {
            var result = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
            if (userIds == null) return result;

            var distinctIds = userIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctIds.Count == 0) return result;

            var now = DateTime.UtcNow;
            var toFetch = new List<string>();
            foreach (var id in distinctIds)
            {
                if (UserByIdCache.TryGetValue(id, out var cached) &&
                    cached.User != null &&
                    now - cached.CachedAtUtc < UserCacheTtl)
                {
                    result[id] = cached.User;
                }
                else
                {
                    toFetch.Add(id);
                }
            }

            if (toFetch.Count == 0) return result;

            try
            {
                await ValidateAndRefreshToken();
                foreach (var chunk in toFetch.Chunk(100))
                {
                    var response = await _usersClient.GetUsersAsync(
                        _configuration["twitchClientId"]!,
                        _accessToken,
                        chunk.ToList(),
                        null);

                    if (response?.Users != null)
                    {
                        var fetchedAt = DateTime.UtcNow;
                        foreach (var user in response.Users)
                        {
                            var entry = new CachedUserEntry(user, fetchedAt);
                            UserByIdCache[user.Id] = entry;
                            if (!string.IsNullOrWhiteSpace(user.Login))
                                UserCache[user.Login] = entry;
                            result[user.Id] = user;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "TooManyRequestsException")
            {
                _logger.LogWarning("Rate limited while batch-fetching Twitch users by ID.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed batch-fetching Twitch users by ID.");
            }

            return result;
        }

        public async Task<Dictionary<string, User>> GetUsersByLoginsAsync(IEnumerable<string> logins)
        {
            var result = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
            if (logins == null) return result;

            var distinctLogins = logins
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctLogins.Count == 0) return result;

            var now = DateTime.UtcNow;
            var toFetch = new List<string>();
            foreach (var login in distinctLogins)
            {
                if (UserCache.TryGetValue(login, out var cached) &&
                    cached.User != null &&
                    now - cached.CachedAtUtc < UserCacheTtl)
                {
                    result[login] = cached.User;
                }
                else
                {
                    toFetch.Add(login);
                }
            }

            if (toFetch.Count == 0) return result;

            try
            {
                await ValidateAndRefreshToken();
                foreach (var chunk in toFetch.Chunk(100))
                {
                    var response = await _usersClient.GetUsersAsync(
                        _configuration["twitchClientId"]!,
                        _accessToken,
                        null,
                        chunk.ToList());

                    if (response?.Users != null)
                    {
                        var fetchedAt = DateTime.UtcNow;
                        foreach (var user in response.Users)
                        {
                            var entry = new CachedUserEntry(user, fetchedAt);
                            if (!string.IsNullOrWhiteSpace(user.Login))
                            {
                                UserCache[user.Login] = entry;
                                result[user.Login] = user;
                            }
                            if (!string.IsNullOrWhiteSpace(user.Id))
                                UserByIdCache[user.Id] = entry;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "TooManyRequestsException")
            {
                _logger.LogWarning("Rate limited while batch-fetching Twitch users by login.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed batch-fetching Twitch users by login.");
            }

            return result;
        }

        public async Task<Follower?> GetUserFollow(string user)
        {
            var broadcasterId = await GetBroadcasterUserId();
            var userId = await GetUserId(user);
            if (userId == null) return null;
            if (broadcasterId == null) return null;
            if (broadcasterId == userId)
            {
                return new()
                {
                    UserId = userId,
                    DisplayName = user,
                    Username = user,
                    FollowDate = DateTime.UtcNow
                };

            }
            try
            {
                //var response = await _twitchApi.Helix.Users.GetUsersFollowsAsync(null, null, 1, userId, broadcasterId, _accessToken);
                var response = await _channelsClient.GetChannelFollowersAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    userId,
                    1,
                    null);
                if (response.Data.Count == 0)
                {
                    return null;
                }
                var firstFollower = response.Data[0];
                return new Follower()
                {
                    Username = firstFollower.UserLogin,
                    UserId = firstFollower.UserId,
                    DisplayName = firstFollower.UserName,
                    FollowDate = DateTime.Parse( firstFollower.FollowedAt)
                };
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetUserFollow():{user} {error}", user, error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetUserFollow():{user} {error}", user, error);
            }
            return null;
        }

        public async Task<bool> IsUserSub(string user)
        {
            var broadcasterId = await GetBroadcasterUserId();
            var userId = await GetUserId(user);
            if (userId == null) return false;
            try
            {
                var response = await _subscriptionsClient.CheckUserSubscriptionAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId!,
                    userId);
                if (response == null) return false;
                return response.Data.Count != 0;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing IsUserSub(): {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing IsUserSub(): {error}", error);
            }
            return false;
        }

        public async Task<bool> IsUserMod(string user)
        {
            var broadcasterId = await GetBroadcasterUserId();
            var userId = await GetUserId(user);
            if (userId == null || string.IsNullOrWhiteSpace(broadcasterId)) return false;
            try
            {
                var response = await _moderationClient.GetModeratorsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId,
                    [userId]);
                return response.Data.Count != 0;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing IsUserMod(): {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing IsUserMod(): {error}", error);
            }
            return false;
        }

        public async Task<List<ChannelEditor>> GetEditors()
        {
            var broadcasterId = await GetBroadcasterUserId();
            if (broadcasterId == null)
            {
                _logger.LogError("Error getting broadcaster id.");
                return [];
            }
            try
            {
                var editors = await _channelsClient.GetChannelEditorsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId);
                return editors.Data.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting editors");
            }
            return [];
        }
    }
}
