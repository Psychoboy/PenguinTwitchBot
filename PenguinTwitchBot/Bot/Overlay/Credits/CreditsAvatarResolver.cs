using Microsoft.Extensions.Caching.Memory;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Core;
using PenguinTwitchBot.TwitchApi.Models.Users;

namespace PenguinTwitchBot.Bot.Overlay.Credits
{
    public sealed class CreditsAvatarResolver(
        ITwitchService twitchService,
        IMemoryCache memoryCache)
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

        private static readonly string[] FallbackColors =
        [
            "#FF4D6D", "#F97316", "#FACC15", "#4ADE80", "#2DD4BF",
            "#38BDF8", "#818CF8", "#C084FC", "#F472B6", "#FB7185"
        ];

        private sealed record CachedLoginAvatar(string UserId, string ImageUrl);

        public async Task<Dictionary<string, string?>> ResolveAvatarsAsync(
            IEnumerable<(string? UserId, string? Username, string? KnownAvatarUrl)> users)
        {
            var results = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var idsToFetch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var loginsToFetch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var idFallbackLogins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var loginOnlyList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (rawId, rawLogin, knownUrl) in users)
            {
                var id = rawId?.Trim() ?? string.Empty;
                var login = UsernameNormalizer.Normalize(rawLogin ?? string.Empty);

                if (!string.IsNullOrWhiteSpace(knownUrl))
                {
                    if (!string.IsNullOrWhiteSpace(id)) results[KeyForId(id)] = knownUrl;
                    if (!string.IsNullOrWhiteSpace(login)) results[KeyForLogin(login)] = knownUrl;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(id))
                {
                    if (memoryCache.TryGetValue<string>(CacheKeyId(id), out var cachedById) &&
                        !string.IsNullOrWhiteSpace(cachedById))
                    {
                        results[KeyForId(id)] = cachedById;
                    }
                    else
                    {
                        idsToFetch.Add(id);
                        if (!string.IsNullOrWhiteSpace(login))
                        {
                            idFallbackLogins.TryAdd(id, login);
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(login))
                {
                    loginOnlyList.Add(login);
                }
            }

            if (idsToFetch.Count > 0)
            {
                var byId = await twitchService.GetUsersByIdsAsync(idsToFetch);
                foreach (var kvp in byId)
                {
                    CacheUser(kvp.Value, results);
                }
            }

            foreach (var login in loginOnlyList)
            {
                if (memoryCache.TryGetValue<CachedLoginAvatar>(CacheKeyLogin(login), out var cachedLogin) &&
                    cachedLogin != null &&
                    !string.IsNullOrWhiteSpace(cachedLogin.ImageUrl))
                {
                    results[KeyForLogin(login)] = cachedLogin.ImageUrl;
                }
                else
                {
                    loginsToFetch.Add(login);
                }
            }

            var unresolvedIdFallbacks = new List<(string ExpectedId, string Login)>();
            foreach (var (expectedId, login) in idFallbackLogins)
            {
                if (results.TryGetValue(KeyForId(expectedId), out var resolvedUrl) && !string.IsNullOrWhiteSpace(resolvedUrl))
                    continue;

                if (memoryCache.TryGetValue<CachedLoginAvatar>(CacheKeyLogin(login), out var cachedLogin) &&
                    cachedLogin != null &&
                    !string.IsNullOrWhiteSpace(cachedLogin.ImageUrl) &&
                    string.Equals(cachedLogin.UserId, expectedId, StringComparison.OrdinalIgnoreCase))
                {
                    results[KeyForId(expectedId)] = cachedLogin.ImageUrl;
                }
                else
                {
                    loginsToFetch.Add(login);
                    unresolvedIdFallbacks.Add((expectedId, login));
                }
            }

            if (loginsToFetch.Count > 0)
            {
                var byLogin = await twitchService.GetUsersByLoginsAsync(loginsToFetch);
                foreach (var kvp in byLogin)
                {
                    CacheUser(kvp.Value, results);
                }

                foreach (var (expectedId, login) in unresolvedIdFallbacks)
                {
                    if (byLogin.TryGetValue(login, out var fetched) &&
                        !string.IsNullOrWhiteSpace(fetched.ProfileImageUrl) &&
                        string.Equals(fetched.Id, expectedId, StringComparison.OrdinalIgnoreCase))
                    {
                        memoryCache.Set(CacheKeyId(expectedId), fetched.ProfileImageUrl, CacheTtl);
                        results[KeyForId(expectedId)] = fetched.ProfileImageUrl;
                    }
                }
            }

            return results;
        }

        public static string? LookupAvatar(
            IReadOnlyDictionary<string, string?> map,
            string? userId,
            string? username,
            string? knownAvatarUrl = null)
        {
            if (!string.IsNullOrWhiteSpace(knownAvatarUrl))
                return knownAvatarUrl;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                return map.TryGetValue(KeyForId(userId.Trim()), out var byId) && !string.IsNullOrWhiteSpace(byId)
                    ? byId
                    : null;
            }

            var login = UsernameNormalizer.Normalize(username ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(login) &&
                map.TryGetValue(KeyForLogin(login), out var byLogin) &&
                !string.IsNullOrWhiteSpace(byLogin))
            {
                return byLogin;
            }

            return null;
        }

        public static string GetDeterministicColor(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return FallbackColors[0];

            uint hash = 2166136261;
            foreach (var ch in key.ToLowerInvariant())
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return FallbackColors[hash % (uint)FallbackColors.Length];
        }

        private void CacheUser(User user, Dictionary<string, string?> results)
        {
            if (string.IsNullOrWhiteSpace(user.ProfileImageUrl)) return;

            if (!string.IsNullOrWhiteSpace(user.Id))
            {
                memoryCache.Set(CacheKeyId(user.Id), user.ProfileImageUrl, CacheTtl);
                results[KeyForId(user.Id)] = user.ProfileImageUrl;
            }

            if (!string.IsNullOrWhiteSpace(user.Login))
            {
                var login = UsernameNormalizer.Normalize(user.Login);
                memoryCache.Set(CacheKeyLogin(login), new CachedLoginAvatar(user.Id ?? string.Empty, user.ProfileImageUrl), CacheTtl);
                results[KeyForLogin(login)] = user.ProfileImageUrl;
            }
        }

        private static string KeyForId(string id) => $"id:{id}";
        private static string KeyForLogin(string login) => $"login:{login}";
        private static string CacheKeyId(string id) => $"credits:avatar:id:{id}";
        private static string CacheKeyLogin(string login) => $"credits:avatar:login:{login}";
    }
}
