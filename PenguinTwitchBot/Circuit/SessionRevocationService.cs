using System.Collections.Concurrent;
using System.Security.Claims;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Circuit
{
    public interface ISessionRevocationService
    {
        /// <summary>Raised with the user ID when that user's sessions are revoked.</summary>
        event Action<string>? SessionRevoked;

        Task RevokeAsync(string userId);

        /// <summary>True if the login at <paramref name="issuedAt"/> predates a revocation for the user.</summary>
        bool IsRevoked(string userId, DateTimeOffset? issuedAt);
    }

    public static class SessionRevocationExtensions
    {
        public const string SessionIssuedClaim = "SessionIssued";

        // Seconds-based claims from older logins are below this; millisecond claims are far above it.
        private const long MillisecondThreshold = 100_000_000_000;

        public static bool IsRevoked(this ISessionRevocationService service, ClaimsPrincipal? principal)
        {
            var userId = principal?.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userId)) return false;

            DateTimeOffset? issuedAt = null;
            if (long.TryParse(principal!.FindFirst(SessionIssuedClaim)?.Value, out var value))
            {
                issuedAt = value < MillisecondThreshold
                    ? DateTimeOffset.FromUnixTimeSeconds(value)
                    : DateTimeOffset.FromUnixTimeMilliseconds(value);
            }

            // Cookies without the claim predate revocation support.
            return service.IsRevoked(userId, issuedAt);
        }
    }

    public class SessionRevocationService(IServiceScopeFactory scopeFactory, ILogger<SessionRevocationService> logger)
        : ISessionRevocationService, IHostedService
    {
        private const string SettingPrefix = "session_revoked:";
        // Cookies default to a 30-day lifetime, so older revocations can no longer match a live cookie.
        private static readonly TimeSpan RevocationRetention = TimeSpan.FromDays(35);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _revokedAt = new();

        public event Action<string>? SessionRevoked;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var settings = await db.Settings.GetAsync(x => x.Name.StartsWith(SettingPrefix));
            var cutoff = DateTimeOffset.UtcNow - RevocationRetention;
            var expired = false;
            foreach (var setting in settings)
            {
                var revokedAt = DateTimeOffset.FromUnixTimeMilliseconds(setting.LongSetting);
                if (revokedAt < cutoff)
                {
                    db.Settings.Remove(setting);
                    expired = true;
                    continue;
                }

                _revokedAt[setting.Name[SettingPrefix.Length..]] = revokedAt;
            }

            if (expired) await db.SaveChangesAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task RevokeAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;

            var now = DateTimeOffset.UtcNow;
            _revokedAt[userId] = now;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var name = SettingPrefix + userId;
                var setting = (await db.Settings.GetAsync(x => x.Name == name)).FirstOrDefault();
                if (setting == null)
                {
                    await db.Settings.AddAsync(new Setting
                    {
                        Name = name,
                        DataType = Setting.DataTypeEnum.Long,
                        LongSetting = now.ToUnixTimeMilliseconds()
                    });
                }
                else
                {
                    setting.LongSetting = now.ToUnixTimeMilliseconds();
                    db.Settings.Update(setting);
                }
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist session revocation for {userId}", userId);
            }

            SessionRevoked?.Invoke(userId);
        }

        public bool IsRevoked(string userId, DateTimeOffset? issuedAt)
        {
            if (string.IsNullOrEmpty(userId) || !_revokedAt.TryGetValue(userId, out var revokedAt))
                return false;

            return issuedAt == null || issuedAt <= revokedAt;
        }
    }
}
