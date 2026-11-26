using PenguinTwitchBot.Bot.TwitchServices.TwitchModels;
using PenguinTwitchBot.TwitchApi.Models.Channels;
using PenguinTwitchBot.TwitchApi.Models.Clips;
using PenguinTwitchBot.TwitchApi.Models.Games;
using PenguinTwitchBot.TwitchApi.Models.Schedule;

namespace PenguinTwitchBot.Bot.TwitchServices
{
    public partial class TwitchService
    {
        public async Task<ChannelStreamSchedule?> GetStreamSchedule()
        {
            try
            {
                var broadcasterId = await GetBroadcasterUserId();
                var result = await _scheduleClient.GetChannelStreamScheduleAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    broadcasterId!);
                if (result?.Schedule != null)
                {
                    return result.Schedule;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stream schedule.");
            }
            return null;
        }

            private sealed record StreamSnapshot(
                string UserId,
                string UserLogin,
                string UserName,
                string GameName,
                int ViewerCount,
                DateTime StartedAt,
                string ThumbnailUrl);

        private async Task<List<StreamSnapshot>> GetStreams(List<string> userIds)
        {
            var response = await _streamsClient.GetStreamsAsync(
                _configuration["twitchClientId"]!,
                _accessToken,
                userIds: userIds);

            return response.Data
                .Select(s => new StreamSnapshot(s.UserId, s.UserLogin, s.UserName, s.GameName, s.ViewerCount, s.StartedAt, s.ThumbnailUrl))
                .ToList();
        }

        public async Task<bool> IsStreamOnline()
        {
            try
            {
                var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
                return await IsStreamOnline(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error doing IsStreamOnline()");
            }
            return false;
        }

        public async Task<bool> IsStreamOnline(string userId)
        {
            try
            {
                var streams = await GetStreams([userId]);
                return streams.Count > 0;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing IsStreamOnline(userId): {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing IsStreamOnline(userId): {error}", error);
            }
            return false;
        }

        public async Task<List<TwitchModels.OnlineStream>> AreStreamsOnline(List<string> userIds)
        {
            return await TryAreStreamsOnlineAsync(userIds) ?? [];
        }

        public async Task<List<TwitchModels.OnlineStream>?> TryAreStreamsOnlineAsync(List<string> userIds)
        {
            if (userIds == null || userIds.Count == 0)
            {
                return [];
            }

            try
            {
                var results = new List<TwitchModels.OnlineStream>();
                foreach (var batch in userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).Chunk(100))
                {
                    var streams = await GetStreams(batch.ToList());
                    results.AddRange(streams.Select(x => new TwitchModels.OnlineStream
                    {
                        UserId = x.UserId,
                        UserLogin = x.UserLogin,
                        UserName = x.UserName,
                        DisplayName = x.UserName,
                        Game = x.GameName
                    }));
                }

                return results;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing AreStreamsOnline: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing AreStreamsOnline: {error}", error);
            }
            return null;
        }

        public async Task<List<TwitchModels.StreamTeamMember>> GetStreamTeamMembersAsync()
        {
            try
            {
                var broadcasterUserId = await GetBroadcasterUserId();
                if (string.IsNullOrWhiteSpace(broadcasterUserId))
                {
                    return [];
                }

                var clientId = _configuration["twitchClientId"];
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    return [];
                }

                var channelTeamsResponse = await _teamsClient.GetChannelTeamsAsync(clientId, _accessToken, broadcasterUserId);
                if (channelTeamsResponse.Data.Count == 0)
                {
                    return [];
                }

                var membersById = new Dictionary<string, TwitchModels.StreamTeamMember>(StringComparer.OrdinalIgnoreCase);

                foreach (var channelTeam in channelTeamsResponse.Data)
                {
                    if (string.IsNullOrWhiteSpace(channelTeam.Id))
                    {
                        continue;
                    }

                    var teamResponse = await _teamsClient.GetTeamByIdAsync(clientId, _accessToken, channelTeam.Id);
                    foreach (var team in teamResponse.Data)
                    {
                        var teamLabel = !string.IsNullOrWhiteSpace(team.TeamDisplayName)
                            ? team.TeamDisplayName
                            : (!string.IsNullOrWhiteSpace(team.TeamName) ? team.TeamName : channelTeam.TeamDisplayName);

                        foreach (var member in team.Users)
                        {
                            if (string.IsNullOrWhiteSpace(member.UserId) ||
                                string.Equals(member.UserId, broadcasterUserId, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (!membersById.TryGetValue(member.UserId, out var existing))
                            {
                                existing = new TwitchModels.StreamTeamMember
                                {
                                    UserId = member.UserId,
                                    UserLogin = member.UserLogin,
                                    DisplayName = !string.IsNullOrWhiteSpace(member.UserName) ? member.UserName : member.UserLogin,
                                    TeamNames = []
                                };
                                membersById[member.UserId] = existing;
                            }

                            if (!string.IsNullOrWhiteSpace(teamLabel) &&
                                !existing.TeamNames.Contains(teamLabel, StringComparer.OrdinalIgnoreCase))
                            {
                                existing.TeamNames.Add(teamLabel);
                            }
                        }
                    }
                }

                return membersById.Values.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stream team members.");
                return [];
            }
        }

        public async Task<DateTime> StreamStartedAt()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var streams = await GetStreams([userId]);
                if (streams.Count == 0)
                {
                    return DateTime.MinValue;
                }
                var stream = streams.First();
                var startTime = stream.StartedAt;
                return startTime;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing getting stream started at: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing StreamStartedAt(): {error}", error);
            }
            return DateTime.MinValue;
        }

        public async Task<int> GetViewerCount()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var streams = await GetStreams([userId]);
                if (streams.Count == 0)
                {
                    return 0;
                }
                return streams.First().ViewerCount;
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing getting viewer count: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetViewerCount(): {error}", error);
            }
            return 0;
        }

        public async Task<string> GetCurrentGame()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            return await GetCurrentGame(userId);
        }

        private async Task<List<ChannelInformation>> GetChannelInformation(string userId)
        {
            var response = await _channelsClient.GetChannelInformationAsync(
                _configuration["twitchClientId"]!,
                _accessToken,
                userId);

            return response.Data.ToList();
        }

        public async Task<string> GetCurrentGame(string userId)
        {
            try
            {
                var channelInfo = await GetChannelInformation(userId);
                if (channelInfo.Count > 0)
                {
                    return channelInfo[0].GameName;
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing Getting current game: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetCurrentGame(): {error}", error);
            }
            return "";
        }

        public async Task<ChannelInformation?> GetChannelInfo(string userId)
        {
            try
            {
                var channelInfo = await GetChannelInformation(userId);
                return channelInfo.FirstOrDefault();
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetChannelInfo: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetChannelInfo(): {error}", error);
            }
            return null;
        }

        public async Task<string> GetUserBio(string userId)
        {
            try
            {
                var user = await GetUserById(userId);
                if (user != null)
                {
                    return user.Description;
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing Getting user bio: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetUserBio(): {error}", error);
            }
            return "";
        }

        public async Task<string> GetUserStreamTitle(string userId)
        {
            try
            {
                var channelInfo = await GetChannelInformation(userId);
                if (channelInfo.Count > 0)
                {
                    return channelInfo[0].Title;
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing Getting user stream title: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetUserStreamTitle(): {error}", error);
            }
            return "";
        }

        public async Task<Game?> GetGameInfo(string gameId)
        {
            try
            {
                var gameInfo = await _gamesClient.GetGamesAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    [gameId]);
                return gameInfo.Data.FirstOrDefault();
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetGameInfo: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetGameInfo(): {error}", error);
            }
            return null;
        }

        public async Task<string> GetStreamTitle()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var channelInfo = await GetChannelInformation(userId);
                if (channelInfo.Count > 0)
                {
                    return channelInfo[0].Title;
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing Getting stream title: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetStreamTitle(): {error}", error);
            }
            return "";
        }

        public async Task<string> GetStreamThumbnail()
        {
            var userId = await GetBroadcasterUserId() ?? throw new Exception("Error getting stream status.");
            try
            {
                var streamInfo = await GetStreams([userId]);
                if (streamInfo.Count > 0)
                {
                    var stream = streamInfo.First();
                    return stream.ThumbnailUrl;
                }
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing Getting Thumbnail: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetStreamThumbnail(): {error}", error);
            }
            return "";
        }

        public async Task<List<Clip>> GetClips(string user)
        {
            var userId = await GetUserId(user);
            try
            {
                var result = await _clipsClient.GetClipsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    null,
                    userId,
                    100,
                    null);
                return result.Data.ToList();
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetClips: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetClips(): {error}", error);
            }
            return [];
        }

        public async Task<List<Clip>> GetFeaturedClips(string user)
        {
            var userId = await GetUserId(user);
            try
            {
                var result = await _clipsClient.GetClipsAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    null,
                    userId,
                    100,
                    true);
                return result.Data.ToList();
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetFeaturedClips: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetFeaturedClips(): {error}", error);
            }
            return [];
        }

        public async Task<List<Clip>> GetClip(string clipId)
        {
            try
            {
                var result = await _clipsClient.GetClipsByIdAsync(
                    _configuration["twitchClientId"]!,
                    _accessToken,
                    [clipId]);
                return result.Data.ToList();
            }
            catch (Exception ex) when (ex.GetType().Name == "HttpResponseException")
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetClip: {error}", error);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                _logger.LogError("Error doing GetClip(): {error}", error);
            }
            return [];
        }
    }
}
