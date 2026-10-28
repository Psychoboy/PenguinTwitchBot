using System.Text.Json;
using System.Text.RegularExpressions;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Commands.Games.UfoClaw;

public interface IUfoClawStore
{
    Task<UfoClawDocument> LoadAsync();
    Task SaveAsync(UfoClawDocument document);
}

public sealed class UfoClawStore(IServiceScopeFactory scopeFactory) : IUfoClawStore
{
    public const string SettingKey = "ufo_claw.v1";
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new PenguinTwitchBot.Database.Bot.DatabaseTools.LegacyIntCompatibleGuidJsonConverter(), new PenguinTwitchBot.Database.Bot.DatabaseTools.LegacyIntCompatibleNullableGuidJsonConverter() }, PropertyNameCaseInsensitive = true };

    public async Task<UfoClawDocument> LoadAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var row = (await database.Settings.GetAsync(setting => setting.Name == SettingKey)).FirstOrDefault();
        var document = row == null ? new UfoClawDocument() : JsonSerializer.Deserialize<UfoClawDocument>(row.StringSetting, JsonOptions)
            ?? throw new InvalidDataException("The saved UFO claw state is empty.");
        Validate(document);
        return document;
    }

    public async Task SaveAsync(UfoClawDocument document)
    {
        Validate(document);
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var row = (await database.Settings.GetAsync(setting => setting.Name == SettingKey)).FirstOrDefault();
        var json = JsonSerializer.Serialize(document, JsonOptions);
        if (row == null)
            await database.Settings.AddAsync(new Setting { Name = SettingKey, DataType = Setting.DataTypeEnum.String, StringSetting = json });
        else
        {
            row.StringSetting = json;
            database.Settings.Update(row);
        }
        await database.SaveChangesAsync();
    }

    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;

    public static void Validate(UfoClawDocument document)
    {
        if (document.Version != 1 || document.Revision < 0 || document.Mode is not ("prizes" or "viewers"))
            throw new ArgumentException("Unsupported UFO claw state.");
        ValidateSettings(document.Settings);
        ValidateEntries(document.Viewers);
        if (document.Grab is { } grab)
        {
            ValidateEntries(grab.Entries);
            ValidateSettings(grab.Rules);
            if (grab.Id == Guid.Empty || grab.StartedAtUtc == default || grab.WinningIndex < 0 || grab.WinningIndex >= grab.Entries.Count)
                throw new ArgumentException("Invalid saved UFO claw grab.");
        }
    }

    public static void ValidateSettings(UfoClawSettings settings)
    {
        if (!double.IsFinite(settings.DurationSeconds) || settings.DurationSeconds is < 3 or > 120 ||
            !double.IsFinite(settings.RevealSeconds) || settings.RevealSeconds is < 1 or > 60 ||
            !double.IsFinite(settings.MovementSpeed) || settings.MovementSpeed is < 0.25 or > 3 ||
            !double.IsFinite(settings.Volume) || settings.Volume is < 0 or > 1)
            throw new ArgumentException("Invalid duration, movement speed or volume.");
        if (settings.LeftSafeGapPixels is < 0 or > 800 || settings.RightSafeGapPixels is < 0 or > 800 ||
            settings.LeftSafeGapPixels + settings.RightSafeGapPixels > 1400)
            throw new ArgumentException("Side safe gaps must be between 0 and 800 pixels, with a combined maximum of 1,400.");
        if (settings.SlipChancePercent is < 0 or > 100)
            throw new ArgumentException("Suspense drop chance must be between 0 and 100 percent.");
        if (settings.MaxSuspenseDrops is < 0 or > 5)
            throw new ArgumentException("Maximum suspense drops must be between zero and five.");
        if (settings.PrizeSets.Count > 100 || settings.PrizeSets.Select(set => set.Id).Distinct(StringComparer.Ordinal).Count() != settings.PrizeSets.Count)
            throw new ArgumentException("Prize sets need unique IDs (maximum 100 sets).");
        foreach (var set in settings.PrizeSets)
        {
            if (string.IsNullOrWhiteSpace(set.Id) || string.IsNullOrWhiteSpace(set.Name) || set.Name.Length > 200)
                throw new ArgumentException("Every prize set needs an ID and a name of at most 200 characters.");
            ValidateEntries(set.Entries);
        }
        ValidateMedia(settings.VesselImageUrl);
        ValidateMedia(settings.GrabSoundUrl);
        ValidateMedia(settings.RevealSoundUrl);
    }

    private static void ValidateEntries(List<UfoClawEntry> entries)
    {
        if (entries.Count > 10000 || entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != entries.Count)
            throw new ArgumentException("Entries need unique IDs (maximum 10,000 per pool).");
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Label) || entry.Label.Length > 300 || entry.Value.Length > 2000 ||
                entry.Weight is < 0 or > 1_000_000 || !Regex.IsMatch(entry.Color, "^#[0-9a-fA-F]{6}$"))
                throw new ArgumentException("Entries need a label, a valid weight, and a six-digit hex color.");
            ValidateMedia(entry.ImageUrl);
        }
    }

    private static void ValidateMedia(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (url.StartsWith('/') && !url.StartsWith("//") && !url.Contains('\\')) return;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") return;
        throw new ArgumentException("Media must use a local absolute path or an HTTP(S) URL.");
    }
}