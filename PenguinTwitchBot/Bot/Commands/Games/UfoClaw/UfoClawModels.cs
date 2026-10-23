using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Bot.Commands.Games.UfoClaw;

public sealed record UfoClawEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "Prize";
    public string Value { get; set; } = "";
    public decimal Weight { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public string Color { get; set; } = "#2ec4b6";
    public string? ImageUrl { get; set; }
    public string? UserId { get; init; }
    public string? Login { get; init; }
}

public sealed record UfoClawPrizeSet
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Prizes";
    public List<UfoClawEntry> Entries { get; set; } = [];
}

public sealed record UfoClawSettings
{
    public bool Enabled { get; set; } = true;
    public List<UfoClawPrizeSet> PrizeSets { get; set; } = [];
    public bool RemoveWinners { get; set; }
    public bool Tease { get; set; } = true;
    public int SlipChancePercent { get; set; } = 20;
    public int MaxSuspenseDrops { get; set; } = 3;
    public double DurationSeconds { get; set; } = 20;
    public double RevealSeconds { get; set; } = 4;
    public double MovementSpeed { get; set; } = 1;
    public double Volume { get; set; } = 0.5;
    public bool SoundEnabled { get; set; } = true;
    public bool AutoHide { get; set; }
    public bool PitBackgroundEnabled { get; set; } = true;
    public int LeftSafeGapPixels { get; set; }
    public int RightSafeGapPixels { get; set; }
    public string? VesselImageUrl { get; set; }
    public string? GrabSoundUrl { get; set; }
    public string? RevealSoundUrl { get; set; }
}

public sealed record UfoClawGrab
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset StartedAtUtc { get; init; }
    public string Mode { get; init; } = "prizes";
    public string PrizeSetId { get; init; } = "";
    public string PrizeSetName { get; init; } = "";
    public List<UfoClawEntry> Entries { get; init; } = [];
    public int WinningIndex { get; init; }
    public UfoClawSettings Rules { get; init; } = new();
    public bool OverlayAnimationAcknowledged { get; set; }
    public DateTimeOffset? OverlayAnimationAcknowledgedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public bool Completed { get; set; }
    public bool DispatchClaimed { get; set; }
    public string CallerUserId { get; init; } = "";
    public string CallerLogin { get; init; } = "system";
    public string CallerDisplayName { get; init; } = "System";
    [JsonIgnore] public UfoClawEntry Winner => Entries[WinningIndex];
}

public sealed record UfoClawDocument
{
    public int Version { get; init; } = 1;
    public long Revision { get; set; }
    public UfoClawSettings Settings { get; set; } = new();
    public string Mode { get; set; } = "prizes";
    public string PrizeSetId { get; set; } = "";
    public bool Visible { get; set; }
    public bool EntriesOpen { get; set; }
    public List<UfoClawEntry> Viewers { get; set; } = [];
    public Dictionary<string, HashSet<string>> Removed { get; set; } = [];
    public UfoClawGrab? Grab { get; set; }
}

public static class UfoClawDraw
{
    public static int Select(IReadOnlyList<UfoClawEntry> entries, decimal sample)
    {
        if (sample is < 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(sample));
        if (entries.Any(entry => entry.Weight < 0 || entry.Weight > 1_000_000))
            throw new ArgumentException("Weights must be between zero and 1,000,000.");
        var total = entries.Where(entry => entry.Enabled).Sum(entry => entry.Weight);
        if (total <= 0) throw new InvalidOperationException("No eligible entries remain.");
        var target = sample * total;
        decimal cumulative = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            if (!entries[index].Enabled) continue;
            cumulative += entries[index].Weight;
            if (target < cumulative) return index;
        }
        throw new InvalidOperationException("Unable to select an entry.");
    }
}