using System.Security.Claims;
using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Services;

public enum AnnouncementSeverity { Info, Success, Warning, Error }

public sealed class Announcement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid Revision { get; set; } = Guid.NewGuid();
    public string Message { get; set; } = "";
    public AnnouncementSeverity Severity { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool IsVisible(DateTime localNow) =>
        (!StartDate.HasValue || localNow.Date >= StartDate.Value.Date) &&
        (!EndDate.HasValue || localNow.Date <= EndDate.Value.Date);
}

public sealed class AnnouncementService(IServiceScopeFactory scopeFactory)
{
    private const string AnnouncementsKey = "announcements.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    public event Action? Changed;

    private static string? UserId(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        ? user.FindFirst("UserId")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        : null;

    public async Task<List<Announcement>> GetAnnouncementsAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(item => item.Name == AnnouncementsKey)).FirstOrDefault();
        return string.IsNullOrWhiteSpace(setting?.StringSetting)
            ? [] : JsonSerializer.Deserialize<List<Announcement>>(setting.StringSetting, JsonOptions) ?? [];
    }

    public async Task<HashSet<Guid>> GetDismissalsAsync(ClaimsPrincipal user)
    {
        var userId = UserId(user);
        if (string.IsNullOrWhiteSpace(userId)) return [];
        var key = DismissalKey(userId);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(item => item.Name == key)).FirstOrDefault();
        return string.IsNullOrWhiteSpace(setting?.StringSetting)
            ? [] : JsonSerializer.Deserialize<HashSet<Guid>>(setting.StringSetting, JsonOptions) ?? [];
    }

    public async Task DismissAsync(ClaimsPrincipal user, Guid revision)
    {
        var userId = UserId(user);
        if (string.IsNullOrWhiteSpace(userId)) return;
        await _gate.WaitAsync();
        try
        {
            var dismissals = await GetDismissalsAsync(user);
            var current = await GetAnnouncementsAsync();
            dismissals.IntersectWith(current.Select(item => item.Revision));
            if (current.Any(item => item.Revision == revision)) dismissals.Add(revision);
            await WriteAsync(DismissalKey(userId), dismissals);
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(ClaimsPrincipal user, Announcement announcement)
    {
        RequireStreamer(user);
        ArgumentNullException.ThrowIfNull(announcement);
        if (string.IsNullOrWhiteSpace(announcement.Message)) throw new ArgumentException("Message is required.");
        if (announcement.StartDate?.Date > announcement.EndDate?.Date) throw new ArgumentException("End date must be on or after the start date.");
        if (!Enum.IsDefined(announcement.Severity)) throw new ArgumentException("Invalid severity.");
        await _gate.WaitAsync();
        try
        {
            var announcements = await GetAnnouncementsAsync();
            announcement.Revision = Guid.NewGuid();
            var index = announcements.FindIndex(item => item.Id == announcement.Id);
            if (index < 0) announcements.Add(announcement);
            else announcements[index] = announcement;
            await WriteAsync(AnnouncementsKey, announcements);
        }
        finally { _gate.Release(); }
        Changed?.Invoke();
    }

    public async Task DeleteAsync(ClaimsPrincipal user, Guid id)
    {
        RequireStreamer(user);
        await _gate.WaitAsync();
        try
        {
            var announcements = await GetAnnouncementsAsync();
            announcements.RemoveAll(item => item.Id == id);
            await WriteAsync(AnnouncementsKey, announcements);
        }
        finally { _gate.Release(); }
        Changed?.Invoke();
    }

    private static void RequireStreamer(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole("Streamer"))
            throw new UnauthorizedAccessException();
    }

    private static string DismissalKey(string userId) => "announcements.dismissed." +
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(userId)));

    private async Task WriteAsync<T>(string key, T value)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var setting = (await db.Settings.GetAsync(item => item.Name == key)).FirstOrDefault();
        if (setting == null)
            await db.Settings.AddAsync(new Setting { Name = key, StringSetting = JsonSerializer.Serialize(value, JsonOptions) });
        else
        {
            setting.StringSetting = JsonSerializer.Serialize(value, JsonOptions);
            db.Settings.Update(setting);
        }
        await db.SaveChangesAsync();
    }
}