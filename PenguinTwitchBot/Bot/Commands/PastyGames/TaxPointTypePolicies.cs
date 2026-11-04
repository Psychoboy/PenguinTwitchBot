using System.Text.Json;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Bot.Commands.PastyGames;

public sealed class TaxPointTypePolicy
{
    public Guid PointTypeId { get; set; }
    public bool Enabled { get; set; }
    public int InactiveDays { get; set; } = 1;
    public long MinimumBalance { get; set; } = 25_000;
    public decimal Percentage { get; set; } = 1m;
    public long MaximumDeduction { get; set; } = 200_000_069;
}

public sealed record TaxPointTypePolicyEntry(PointType PointType, TaxPointTypePolicy Policy);

public static class TaxPointTypePolicySettings
{
    private const string SettingsGroup = "TaxPointPolicies";

    public static async Task<List<TaxPointTypePolicyEntry>> LoadAsync(
        IPointsSystem pointsSystem,
        IGameSettingsService gameSettingsService)
    {
        await pointsSystem.RegisterDefaultPointForGame("Tax");
        var pointTypes = (await pointsSystem.GetPointTypes()).ToList();
        var legacyPointType = await pointsSystem.GetPointTypeForGame("Tax");
        var entries = new List<TaxPointTypePolicyEntry>(pointTypes.Count);

        foreach (var pointType in pointTypes)
        {
            var stored = await gameSettingsService.GetStringSetting(SettingsGroup, GetSettingName(pointType.Id), string.Empty);
            var policy = TryDeserialize(stored, pointType.Id) ?? new TaxPointTypePolicy
            {
                PointTypeId = pointType.Id,
                Enabled = pointType.Id == legacyPointType.Id
            };
            entries.Add(new TaxPointTypePolicyEntry(pointType, policy));
        }

        return entries;
    }

    public static async Task SaveAsync(IGameSettingsService gameSettingsService, TaxPointTypePolicy policy)
    {
        Validate(policy);
        await gameSettingsService.SetStringSetting(SettingsGroup, GetSettingName(policy.PointTypeId), JsonSerializer.Serialize(policy));
    }

    public static long CalculateDeduction(long balance, TaxPointTypePolicy policy)
    {
        if (!policy.Enabled || balance <= policy.MinimumBalance || policy.Percentage <= 0 || policy.MaximumDeduction <= 0)
        {
            return 0;
        }

        var percentageDeduction = decimal.Floor(balance * policy.Percentage / 100m);
        return (long)Math.Min(percentageDeduction, policy.MaximumDeduction);
    }

    public static void Validate(TaxPointTypePolicy policy)
    {
        if (policy.PointTypeId == Guid.Empty)
        {
            throw new ArgumentException("A point type is required.", nameof(policy));
        }

        if (policy.InactiveDays is < 1 or > 3650)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Inactive days must be between 1 and 3650.");
        }

        if (policy.MinimumBalance < 0 || policy.MaximumDeduction < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Tax amounts cannot be negative.");
        }

        if (policy.Percentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Tax percentage must be between 0 and 100.");
        }
    }

    private static TaxPointTypePolicy? TryDeserialize(string stored, Guid pointTypeId)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        try
        {
            var policy = JsonSerializer.Deserialize<TaxPointTypePolicy>(stored);
            return policy?.PointTypeId == pointTypeId ? policy : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GetSettingName(Guid pointTypeId) => $"PointType{pointTypeId:N}";
}