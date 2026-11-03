using System.Collections.Concurrent;
using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Overlay;

public static class FireworksQueueDefaults
{
    public const string GlobalMaxQueuedBurstsSettingName = "FireworksQueueMaxQueuedBursts";
    public const int MaxQueuedBursts = 250;
    public const int MaximumQueuedBursts = 2000;
    public static readonly TimeSpan ConnectedWindow = TimeSpan.FromSeconds(15);

    public static int GetMaxQueuedBursts(string? customSettings, int globalDefault = MaxQueuedBursts)
    {
        var fallback = Math.Clamp(globalDefault, 1, MaximumQueuedBursts);
        if (string.IsNullOrWhiteSpace(customSettings)) return fallback;

        try
        {
            using var document = JsonDocument.Parse(customSettings);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return fallback;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("maxQueuedBursts", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.TryGetInt32(out var value))
                {
                    return Math.Clamp(value, 1, MaximumQueuedBursts);
                }
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }
    public static int GetMinLaunchGapMs(string? customSettings)
    {
        if (string.IsNullOrWhiteSpace(customSettings)) return 240;

        try
        {
            using var document = JsonDocument.Parse(customSettings);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Name.Equals("minLaunchGapMs", StringComparison.OrdinalIgnoreCase) &&
                        property.Value.TryGetInt32(out var value))
                    {
                        return Math.Clamp(value, 50, 3000);
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return 240;
    }

    public static async Task<int> GetGlobalMaxQueuedBurstsAsync(IUnitOfWork unitOfWork)
    {
        var setting = (await unitOfWork.Settings.GetAsync(item => item.Name == GlobalMaxQueuedBurstsSettingName)).FirstOrDefault();
        return Math.Clamp(setting?.IntSetting ?? MaxQueuedBursts, 1, MaximumQueuedBursts);
    }

    public static async Task<int> SetGlobalMaxQueuedBurstsAsync(IUnitOfWork unitOfWork, int value)
    {
        var boundedValue = Math.Clamp(value, 1, MaximumQueuedBursts);
        var setting = (await unitOfWork.Settings.GetAsync(item => item.Name == GlobalMaxQueuedBurstsSettingName)).FirstOrDefault();
        if (setting == null)
        {
            unitOfWork.Settings.Add(new Setting
            {
                Name = GlobalMaxQueuedBurstsSettingName,
                DataType = Setting.DataTypeEnum.Int,
                IntSetting = boundedValue
            });
        }
        else
        {
            setting.DataType = Setting.DataTypeEnum.Int;
            setting.IntSetting = boundedValue;
            unitOfWork.Settings.Update(setting);
        }

        await unitOfWork.SaveChangesAsync();
        return boundedValue;
    }
}

public sealed record FireworksQueueJobStatus(string JobId, int PendingBursts);

public sealed record FireworksQueueStatus(
    Guid WidgetId,
    bool IsStandalone,
    int PendingBursts,
    int MaxQueuedBursts,
    int MinLaunchGapMs,
    int RejectedBurstCount,
    IReadOnlyList<FireworksQueueJobStatus> Jobs,
    DateTime UpdatedAtUtc);

public interface IFireworksQueueStatusStore
{
    void Report(Guid widgetId, int pendingBursts, int maxQueuedBursts, int minLaunchGapMs, int rejectedBurstCount, IReadOnlyList<FireworksQueueJobStatus> jobs, bool isStandalone = false);
    FireworksQueueStatus? Get(Guid widgetId);
    IReadOnlyList<FireworksQueueStatus> GetAll();
}

public sealed class FireworksQueueStatusStore : IFireworksQueueStatusStore
{
    private readonly ConcurrentDictionary<Guid, FireworksQueueStatus> _statuses = new();

    public void Report(
        Guid widgetId,
        int pendingBursts,
        int maxQueuedBursts,
        int minLaunchGapMs,
        int rejectedBurstCount,
        IReadOnlyList<FireworksQueueJobStatus> jobs,
        bool isStandalone = false)
    {
        _statuses[widgetId] = new FireworksQueueStatus(
            widgetId,
            isStandalone,
            pendingBursts,
            maxQueuedBursts,
            minLaunchGapMs,
            rejectedBurstCount,
            jobs.ToArray(),
            DateTime.UtcNow);
    }

    public FireworksQueueStatus? Get(Guid widgetId) =>
        _statuses.TryGetValue(widgetId, out var status) ? status : null;

    public IReadOnlyList<FireworksQueueStatus> GetAll() => _statuses.Values.ToArray();
}