using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Services;

public sealed record RaidRewardConfig(
    bool Enabled,
    int PointTypeId,
    long PointsToAward,
    int TimeWindowMinutes,
    string Message,
    string? SubscriberMessage,
    string AnnouncementTemplate,
    bool PostAnnouncement);

public interface IRaidRewardSettingsService
{
    Task<RaidRewardConfig> GetConfigAsync();
    Task SaveConfigAsync(RaidRewardConfig config);
}

/// <summary>
/// Stores Raid Reward configuration in the Settings table.
/// Booleans are stored as Int (0/1) since the Setting model has no bool type.
/// </summary>
public class RaidRewardSettingsService(IServiceScopeFactory scopeFactory) : IRaidRewardSettingsService
{
    public const string EnabledName = "RaidRewardEnabled";
    public const string PointTypeIdName = "RaidRewardPointTypeId";
    public const string PointsToAwardName = "RaidRewardPointsToAward";
    public const string TimeWindowMinutesName = "RaidRewardTimeWindowMinutes";
    public const string MessageName = "RaidRewardMessage";
    public const string SubscriberMessageName = "RaidRewardSubscriberMessage";
    public const string AnnouncementTemplateName = "RaidRewardAnnouncementTemplate";
    public const string PostAnnouncementName = "RaidRewardPostAnnouncement";

    public const string DefaultMessage = "TombRaid twitchRaid";

    public const string DefaultAnnouncementTemplate =
        "We're raiding {target}! Type \"{message}\" in their chat within {minutes} minutes to earn {points} {pointtype}!";

    public async Task<RaidRewardConfig> GetConfigAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var settings = await db.Settings.GetAsync(x =>
            x.Name == EnabledName || x.Name == PointTypeIdName || x.Name == PointsToAwardName ||
            x.Name == TimeWindowMinutesName || x.Name == MessageName || x.Name == SubscriberMessageName ||
            x.Name == AnnouncementTemplateName || x.Name == PostAnnouncementName);
        var map = settings.ToDictionary(x => x.Name, x => x);

        return new RaidRewardConfig(
            Enabled: GetInt(map, EnabledName) == 1,
            PointTypeId: GetInt(map, PointTypeIdName),
            PointsToAward: GetLong(map, PointsToAwardName, 100),
            TimeWindowMinutes: GetInt(map, TimeWindowMinutesName, 5),
            Message: string.IsNullOrWhiteSpace(GetString(map, MessageName))
                ? DefaultMessage
                : GetString(map, MessageName),
            SubscriberMessage: NullIfEmpty(GetString(map, SubscriberMessageName)),
            AnnouncementTemplate: string.IsNullOrWhiteSpace(GetString(map, AnnouncementTemplateName))
                ? DefaultAnnouncementTemplate
                : GetString(map, AnnouncementTemplateName),
            PostAnnouncement: GetInt(map, PostAnnouncementName, 1) == 1);
    }

    public async Task SaveConfigAsync(RaidRewardConfig config)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await UpsertInt(db, EnabledName, config.Enabled ? 1 : 0);
        await UpsertInt(db, PointTypeIdName, config.PointTypeId);
        await UpsertLong(db, PointsToAwardName, config.PointsToAward);
        await UpsertInt(db, TimeWindowMinutesName, config.TimeWindowMinutes);
        await UpsertString(db, MessageName, config.Message);
        await UpsertString(db, SubscriberMessageName, config.SubscriberMessage ?? string.Empty);
        await UpsertString(db, AnnouncementTemplateName, config.AnnouncementTemplate);
        await UpsertInt(db, PostAnnouncementName, config.PostAnnouncement ? 1 : 0);

        await db.SaveChangesAsync();
    }

    private static int GetInt(Dictionary<string, Setting> map, string name, int defaultValue = 0)
        => map.TryGetValue(name, out var s) ? s.IntSetting : defaultValue;

    private static long GetLong(Dictionary<string, Setting> map, string name, long defaultValue = 0)
        => map.TryGetValue(name, out var s) ? s.LongSetting : defaultValue;

    private static string GetString(Dictionary<string, Setting> map, string name)
        => map.TryGetValue(name, out var s) ? s.StringSetting : string.Empty;

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static async Task UpsertInt(IUnitOfWork db, string name, int value)
    {
        var setting = (await db.Settings.GetAsync(x => x.Name == name)).FirstOrDefault();
        if (setting == null)
            await db.Settings.AddAsync(new Setting { Name = name, DataType = Setting.DataTypeEnum.Int, IntSetting = value });
        else { setting.DataType = Setting.DataTypeEnum.Int; setting.IntSetting = value; db.Settings.Update(setting); }
    }

    private static async Task UpsertLong(IUnitOfWork db, string name, long value)
    {
        var setting = (await db.Settings.GetAsync(x => x.Name == name)).FirstOrDefault();
        if (setting == null)
            await db.Settings.AddAsync(new Setting { Name = name, DataType = Setting.DataTypeEnum.Long, LongSetting = value });
        else { setting.DataType = Setting.DataTypeEnum.Long; setting.LongSetting = value; db.Settings.Update(setting); }
    }

    private static async Task UpsertString(IUnitOfWork db, string name, string value)
    {
        var setting = (await db.Settings.GetAsync(x => x.Name == name)).FirstOrDefault();
        if (setting == null)
            await db.Settings.AddAsync(new Setting { Name = name, DataType = Setting.DataTypeEnum.String, StringSetting = value });
        else { setting.DataType = Setting.DataTypeEnum.String; setting.StringSetting = value; db.Settings.Update(setting); }
    }
}
