using System.Text.Json.Serialization;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Games
{
    public enum DropGameType
    {
        Parachute,
        Plinko
    }

    public class DropGamesSettings
    {
        public ParachuteGameSettings Parachute { get; set; } = new();
        public PlinkoGameSettings Plinko { get; set; } = new();
        public List<DropGameTheme> Themes { get; set; } = [];
    }

    public class DropGameTheme
    {
        public string Id { get; set; } = "spring";
        public string Name { get; set; } = "Spring (Cherry Blossoms)";
        public bool IsStock { get; set; } = true;
        public string SeasonPreset { get; set; } = "spring"; // spring, summer, fall, winter, halloween, christmas, custom
        
        // Background Ambient Particles
        public bool EnableBackgroundParticles { get; set; } = true;
        public string ParticleType { get; set; } = "petals"; // petals, bubbles, leaves, snow, stars, bats, none
        public int ParticleDensity { get; set; } = 35;

        // Landing Pad Particles & Bursts
        public bool EnableLandingParticles { get; set; } = true;
        public string LandingParticleType { get; set; } = "leaves"; // leaves, petals, snow, splash, candies, confetti, none
        public int LandingParticleDensity { get; set; } = 36;

        public string PrimaryColor { get; set; } = "#FF8FA3";
        public string SecondaryColor { get; set; } = "#FFB7C5";
        public string AccentColor { get; set; } = "#FFD166";
        public string TargetColor { get; set; } = "#7BC67B";
        public string TextColor { get; set; } = "#FFFFFF";

        // Optional custom media URLs via MediaUrlPicker (empty string uses built-in seasonal vector/canvas art)
        public string DefaultCharacterImageUrl { get; set; } = "";
        public string DefaultDropletImageUrl { get; set; } = "";
        public string ParachuteCanopyImageUrl { get; set; } = "";
        public string TargetPlatformImageUrl { get; set; } = "";
        public string PlinkoBoardBackgroundImageUrl { get; set; } = "";
        public string PlinkoPegImageUrl { get; set; } = "";

        public string DropSoundUrl { get; set; } = "";
        public string DropletHitSoundUrl { get; set; } = "";
        public string WinSoundUrl { get; set; } = "";
        public string LoseSoundUrl { get; set; } = "";

        public static List<DropGameTheme> CreateDefaultSeasonalThemes() =>
        [
            new DropGameTheme
            {
                Id = "spring",
                Name = "Spring (Cherry Blossoms)",
                IsStock = true,
                SeasonPreset = "spring",
                EnableBackgroundParticles = true,
                ParticleType = "petals",
                ParticleDensity = 40,
                EnableLandingParticles = true,
                LandingParticleType = "petals",
                LandingParticleDensity = 36,
                PrimaryColor = "#FF8FA3",
                SecondaryColor = "#FFB7C5",
                AccentColor = "#FFE066",
                TargetColor = "#6BCB77",
                TextColor = "#FFFFFF"
            },
            new DropGameTheme
            {
                Id = "summer",
                Name = "Summer (Tropical Pool Party)",
                IsStock = true,
                SeasonPreset = "summer",
                EnableBackgroundParticles = true,
                ParticleType = "bubbles",
                ParticleDensity = 35,
                EnableLandingParticles = true,
                LandingParticleType = "splash",
                LandingParticleDensity = 40,
                PrimaryColor = "#00B4D8",
                SecondaryColor = "#FFD166",
                AccentColor = "#FF6B6B",
                TargetColor = "#F4A261",
                TextColor = "#FFFFFF"
            },
            new DropGameTheme
            {
                Id = "fall",
                Name = "Fall (Autumn Leaf Pile)",
                IsStock = true,
                SeasonPreset = "fall",
                EnableBackgroundParticles = true,
                ParticleType = "leaves",
                ParticleDensity = 38,
                EnableLandingParticles = true,
                LandingParticleType = "leaves",
                LandingParticleDensity = 45,
                PrimaryColor = "#E76F51",
                SecondaryColor = "#F4A261",
                AccentColor = "#E9C46A",
                TargetColor = "#8D5B4C",
                TextColor = "#FFF8E7"
            },
            new DropGameTheme
            {
                Id = "winter",
                Name = "Winter (Snowy Frost)",
                IsStock = true,
                SeasonPreset = "winter",
                EnableBackgroundParticles = true,
                ParticleType = "snow",
                ParticleDensity = 45,
                EnableLandingParticles = true,
                LandingParticleType = "snow",
                LandingParticleDensity = 40,
                PrimaryColor = "#48CAE4",
                SecondaryColor = "#CAF0F8",
                AccentColor = "#FFD166",
                TargetColor = "#90E0EF",
                TextColor = "#FFFFFF"
            },
            new DropGameTheme
            {
                Id = "halloween",
                Name = "Halloween (Spooky Cauldron)",
                IsStock = true,
                SeasonPreset = "halloween",
                EnableBackgroundParticles = true,
                ParticleType = "bats",
                ParticleDensity = 32,
                EnableLandingParticles = true,
                LandingParticleType = "candies",
                LandingParticleDensity = 42,
                PrimaryColor = "#FF7518",
                SecondaryColor = "#7B2CBF",
                AccentColor = "#39FF14",
                TargetColor = "#240046",
                TextColor = "#FFFFFF"
            },
            new DropGameTheme
            {
                Id = "christmas",
                Name = "Christmas (Holiday Celebration)",
                IsStock = true,
                SeasonPreset = "christmas",
                EnableBackgroundParticles = true,
                ParticleType = "snow",
                ParticleDensity = 45,
                EnableLandingParticles = true,
                LandingParticleType = "confetti",
                LandingParticleDensity = 45,
                PrimaryColor = "#D90429",
                SecondaryColor = "#2B9348",
                AccentColor = "#FFD166",
                TargetColor = "#007F5F",
                TextColor = "#FFFFFF"
            }
        ];
    }

    public class ParachuteGameSettings
    {
        public string ActiveThemeId { get; set; } = "spring";
        public double TargetCenterX { get; set; } = 0.50;
        public double TargetWidthPercent { get; set; } = 18.0;
        public double DescentDurationSeconds { get; set; } = 6.5;
        public double WindStrength { get; set; } = 0.25;
        public string WinActionName { get; set; } = "";
        public bool AnnounceWinsInChat { get; set; } = true;
        public string WinChatMessageTemplate { get; set; } = "🪂 {user} landed on the target with {score} pts!{bullseye}";

        // Default Droplets settings
        public int DefaultDropletCount { get; set; } = 5;
        public string DefaultDropletSize { get; set; } = "Medium"; // Small, Medium, Large, Giant, Random
        public List<string> DefaultDropletRandomSizes { get; set; } = ["Small", "Medium", "Large", "Giant"];
        public string DefaultDropletSpeed { get; set; } = "Normal"; // Slow, Normal, Fast, Hyper, Random
        public string DefaultDropletPattern { get; set; } = "TopRain"; // TopRain, Targeted, SideWind, Fountain
        public double DefaultKnockbackMultiplier { get; set; } = 1.0;

        // Timed Session / Round settings
        public bool SessionModeEnabled { get; set; } = false;
        public bool AutoStartOnCommand { get; set; } = true;
        public int DefaultSessionDurationSeconds { get; set; } = 100;
        public int MaxDropsPerUserPerSession { get; set; } = 1; // 0 = unlimited
        public bool AnnounceSessionStatusInChat { get; set; } = true;
        public string SessionStartChatMessageTemplate { get; set; } = "🪂 Parachute Drop is now OPEN for {duration}s! Type !drop [emote] to join!";
        public string SessionEndChatMessageTemplate { get; set; } = "🪂 Parachute Drop round is now CLOSED! {count} total parachutists dropped!";
    }

    public class PlinkoGameSettings
    {
        private int _pegTransparency = 15;

        public string ActiveThemeId { get; set; } = "spring";
        public int RowCount { get; set; } = 8;
        public double DropDurationSeconds { get; set; } = 4.5;
        public int PegTransparency
        {
            get => _pegTransparency;
            set => _pegTransparency = Math.Clamp(value, 0, 100);
        }
        public string WinActionName { get; set; } = "";
        public bool AnnounceWinsInChat { get; set; } = true;
        public string WinChatMessageTemplate { get; set; } = "🪙 {user}'s Plinko token landed in [{bucket}] ({score} pts)!";
        public List<PlinkoBucket> Buckets { get; set; } = [];

        // Timed Session / Round settings
        public bool SessionModeEnabled { get; set; } = false;
        public bool AutoStartOnCommand { get; set; } = true;
        public int DefaultSessionDurationSeconds { get; set; } = 100;
        public int MaxDropsPerUserPerSession { get; set; } = 1; // 0 = unlimited
        public bool AnnounceSessionStatusInChat { get; set; } = true;
        public string SessionStartChatMessageTemplate { get; set; } = "🪙 Plinko Bounce is now OPEN for {duration}s! Type !plinko [emote] to join!";
        public string SessionEndChatMessageTemplate { get; set; } = "🪙 Plinko Bounce round is now CLOSED! {count} total tokens dropped!";

        public static List<PlinkoBucket> CreateDefaultBuckets() =>
        [
            new PlinkoBucket { Label = "MISS", Score = 0, IsWin = false, Color = "#64748B", Weight = 25 },
            new PlinkoBucket { Label = "50 PTS", Score = 50, IsWin = true, Color = "#3B82F6", Weight = 20 },
            new PlinkoBucket { Label = "100 PTS", Score = 100, IsWin = true, Color = "#10B981", Weight = 12 },
            new PlinkoBucket { Label = "JACKPOT", Score = 500, IsWin = true, Color = "#F59E0B", Weight = 6 },
            new PlinkoBucket { Label = "100 PTS", Score = 100, IsWin = true, Color = "#10B981", Weight = 12 },
            new PlinkoBucket { Label = "50 PTS", Score = 50, IsWin = true, Color = "#3B82F6", Weight = 20 },
            new PlinkoBucket { Label = "MISS", Score = 0, IsWin = false, Color = "#64748B", Weight = 25 }
        ];
    }

    public class PlinkoBucket
    {
        public string Label { get; set; } = "100 PTS";
        public int Score { get; set; } = 100;
        public bool IsWin { get; set; } = true;
        public string Color { get; set; } = "#10B981";
        public int Weight { get; set; } = 15;
    }

    public class DropletSpawnRequest
    {
        public int Count { get; set; } = 5;
        public string SizePreset { get; set; } = "Medium";
        public List<string>? AllowedRandomSizes { get; set; }
        public string SpeedPreset { get; set; } = "Normal";
        public string SpawnPattern { get; set; } = "Targeted";
        public double KnockbackMultiplier { get; set; } = 1.0;
        public string? EmoteOrImage { get; set; }
        public string? NativeEmoteUrl { get; set; }
        public string? TriggeredByUser { get; set; }
        public bool AllowDirectUrlOrPath { get; set; }
    }

    public class DropletInstanceDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("spawnTickOffset")]
        public int SpawnTickOffset { get; set; }

        [JsonPropertyName("startX")]
        public double StartX { get; set; }

        [JsonPropertyName("startY")]
        public double StartY { get; set; }

        [JsonPropertyName("vx")]
        public double Vx { get; set; }

        [JsonPropertyName("vy")]
        public double Vy { get; set; }

        [JsonPropertyName("gravity")]
        public double Gravity { get; set; }

        [JsonPropertyName("radius")]
        public double Radius { get; set; }

        [JsonPropertyName("mass")]
        public double Mass { get; set; }

        [JsonPropertyName("sizeScale")]
        public double SizeScale { get; set; }

        [JsonPropertyName("speedScale")]
        public double SpeedScale { get; set; }

        [JsonPropertyName("sizeLabel")]
        public string SizeLabel { get; set; } = "Medium";

        [JsonPropertyName("speedLabel")]
        public string SpeedLabel { get; set; } = "Normal";

        [JsonPropertyName("imageUrl")]
        public string ImageUrl { get; set; } = "";
    }

    public class ParachuteTrajectoryPointDto
    {
        [JsonPropertyName("tick")]
        public int Tick { get; set; }

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("vx")]
        public double Vx { get; set; }

        [JsonPropertyName("vy")]
        public double Vy { get; set; }
    }

    public class ParachuteCollisionEventDto
    {
        [JsonPropertyName("tick")]
        public int Tick { get; set; }

        [JsonPropertyName("dropletId")]
        public string DropletId { get; set; } = "";

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("deltaVx")]
        public double DeltaVx { get; set; }

        [JsonPropertyName("deltaVy")]
        public double DeltaVy { get; set; }

        [JsonPropertyName("sizeScale")]
        public double SizeScale { get; set; }
    }

    public class ParachuteTrajectoryUpdateDto
    {
        [JsonPropertyName("dropToken")]
        public string DropToken { get; set; } = "";

        [JsonPropertyName("landingX")]
        public double LandingX { get; set; }

        [JsonPropertyName("totalTicks")]
        public int TotalTicks { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("isHit")]
        public bool IsHit { get; set; }

        [JsonPropertyName("isBullseye")]
        public bool IsBullseye { get; set; }

        [JsonPropertyName("isWin")]
        public bool IsWin { get; set; }

        [JsonPropertyName("dropletHits")]
        public int DropletHits { get; set; }

        [JsonPropertyName("collisions")]
        public List<ParachuteCollisionEventDto> Collisions { get; set; } = [];

        [JsonPropertyName("keyframes")]
        public List<ParachuteTrajectoryPointDto> Keyframes { get; set; } = [];
    }

    public class PlinkoWaypointDto
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("row")]
        public int Row { get; set; }

        [JsonPropertyName("col")]
        public int Col { get; set; }
    }

    public class DropGameCompleteMessage
    {
        [JsonPropertyName("dropToken")]
        public string DropToken { get; set; } = "";

        [JsonPropertyName("landingX")]
        public double? LandingX { get; set; }

        [JsonPropertyName("winningBucketIndex")]
        public int? WinningBucketIndex { get; set; }
    }

    /// <summary>
    /// Server-authoritative record for an active Parachute or Plinko drop.
    /// Never trusts outcome data from the client; only uses DropToken to synchronize completion timing.
    /// </summary>
    public class PendingDropRecord
    {
        public required string DropToken { get; init; }
        public required DropGameType GameType { get; init; }
        public required string Username { get; init; }
        public required string DisplayName { get; init; }
        public required string UserId { get; init; }
        public required CommandEventArgs EventArgs { get; init; }
        public required DropGameTheme Theme { get; init; }
        public required DateTime CreatedAtUtc { get; init; }
        public required DateTime EarliestCompletionUtc { get; init; }
        public required DateTime ExpiresAtUtc { get; init; }

        // Parachute simulation fields (mutable under lock when droplets hit mid-flight)
        public double StartX { get; init; }
        public double StartY { get; init; } = 0.06;
        public double LandingY { get; init; } = 0.88;
        public double InitialVx { get; init; }
        public double InitialVy { get; init; }
        public double WindAccelX { get; init; }
        public double SwayAmplitude { get; init; }
        public double SwayFrequency { get; init; }
        public double SwayPhase { get; init; }
        public int TotalTicks { get; set; }
        public double TargetCenterX { get; init; }
        public double TargetHalfWidth { get; init; }

        // Authoritative outcome (updated by server 60Hz simulation if droplets collide)
        public double LandingX { get; set; }
        public int Score { get; set; }
        public bool IsHit { get; set; }
        public bool IsBullseye { get; set; }
        public bool IsWin { get; set; }
        public int DropletHitCount { get; set; }
        public int BucketIndex { get; set; }
        public string BucketLabel { get; set; } = "";

        public List<ParachuteCollisionEventDto> Collisions { get; set; } = [];
        public List<ParachuteTrajectoryPointDto> Keyframes { get; set; } = [];
        public List<PlinkoBucket>? Buckets { get; init; }

        // Processing & completion state
        public bool IsProcessing { get; set; }
        public bool IsCompleted { get; set; }
        public bool DefaultCommandTriggerExecuted { get; set; }
        public bool WinActionExecuted { get; set; }
        public bool WinChatAnnounced { get; set; }
    }

    public class DropGameSessionDto
    {
        [JsonPropertyName("gameType")]
        public string GameType { get; set; } = "";

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("isQueued")]
        public bool IsQueued { get; set; }

        [JsonPropertyName("queuePosition")]
        public int QueuePosition { get; set; }

        [JsonPropertyName("totalDurationSeconds")]
        public int TotalDurationSeconds { get; set; }

        [JsonPropertyName("remainingSeconds")]
        public int RemainingSeconds { get; set; }

        [JsonPropertyName("startedAtMs")]
        public long StartedAtMs { get; set; }

        [JsonPropertyName("endsAtMs")]
        public long EndsAtMs { get; set; }

        [JsonPropertyName("totalDrops")]
        public int TotalDrops { get; set; }

        [JsonPropertyName("theme")]
        public DropGameTheme? Theme { get; set; }

        [JsonPropertyName("targetCenterX")]
        public double? TargetCenterX { get; set; }

        [JsonPropertyName("targetHalfWidth")]
        public double? TargetHalfWidth { get; set; }

        [JsonPropertyName("bullseyeHalfWidth")]
        public double? BullseyeHalfWidth { get; set; }
    }
}


