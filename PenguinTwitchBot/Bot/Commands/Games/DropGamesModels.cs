using System.Text.Json.Serialization;
using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Games
{
    public enum DropGameType
    {
        Parachute,
        Plinko,
        Balloon
    }

    public class DropGamesSettings
    {
        public ParachuteGameSettings Parachute { get; set; } = new();
        public PlinkoGameSettings Plinko { get; set; } = new();
        public BalloonAscentGameSettings Balloon { get; set; } = new();
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
        public string PlinkoWinningSlotImageUrl { get; set; } = "";
        public string PlinkoDividerImageUrl { get; set; } = "";
        public string BalloonCanopyImageUrl { get; set; } = "";
        public string BalloonCloudPlatformImageUrl { get; set; } = "";
        public string BalloonDebrisImageUrl { get; set; } = "";
        public string BalloonDartImageUrl { get; set; } = "";
        public string BalloonAnvilImageUrl { get; set; } = "";
        public string BalloonNudgeImageUrl { get; set; } = "";
        public string BalloonWindGustImageUrl { get; set; } = "";

        // Parachute Sound Overrides
        public DropGameSoundOverride ParachuteStartSound { get; set; } = new();
        public DropGameSoundOverride ParachuteEndSound { get; set; } = new();
        public DropGameSoundOverride ParachuteDropSound { get; set; } = new();
        public DropGameSoundOverride ParachuteFlapSound { get; set; } = new();
        public DropGameSoundOverride ParachuteWinSound { get; set; } = new();
        public DropGameSoundOverride ParachuteRocketLaunchSound { get; set; } = new();
        public DropGameSoundOverride ParachuteRocketExplodeSound { get; set; } = new();
        public DropGameSoundOverride ParachutePopSound { get; set; } = new();
        public DropGameSoundOverride ParachuteCrashLandSound { get; set; } = new();
        public DropGameSoundOverride ParachuteCannonballSound { get; set; } = new();

        // Plinko Sound Overrides
        public DropGameSoundOverride PlinkoStartSound { get; set; } = new();
        public DropGameSoundOverride PlinkoEndSound { get; set; } = new();
        public DropGameSoundOverride PlinkoDropSound { get; set; } = new();
        public DropGameSoundOverride PlinkoWinSound { get; set; } = new();
        public DropGameSoundOverride PlinkoPegCollisionSound { get; set; } = new();

        // Balloon Sound Overrides
        public DropGameSoundOverride BalloonStartSound { get; set; } = new();
        public DropGameSoundOverride BalloonEndSound { get; set; } = new();
        public DropGameSoundOverride BalloonLaunchSound { get; set; } = new();
        public DropGameSoundOverride BalloonPopSound { get; set; } = new();
        public DropGameSoundOverride BalloonBonkSound { get; set; } = new();
        public DropGameSoundOverride BalloonNudgeSound { get; set; } = new();
        public DropGameSoundOverride BalloonWinSound { get; set; } = new();

        // Legacy sound properties for database JSON backward compatibility
        public string DropSoundUrl { get; set; } = "";
        public string DropletHitSoundUrl { get; set; } = "/media/droplet_hit.wav";
        public string WinSoundUrl { get; set; } = "";
        public string LoseSoundUrl { get; set; } = "/media/miss_land.wav";

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

    public enum DropGameSoundOverrideMode
    {
        Inherit,
        Custom,
        Mute
    }

    public class DropGameSoundConfig
    {
        public bool Enabled { get; set; } = false;
        public string AudioFile { get; set; } = "";

        [JsonIgnore]
        public string? SoundFile
        {
            get => AudioFile;
            set => AudioFile = value ?? "";
        }

        private int _volume = 50;
        public int Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0, 100);
        }
    }

    public class DropGameSoundOverride
    {
        public bool Override { get; set; } = false;
        public bool Enabled { get; set; } = true;
        public string AudioFile { get; set; } = "";

        [JsonIgnore]
        public string? SoundFile
        {
            get => AudioFile;
            set => AudioFile = value ?? "";
        }

        private int _volume = 50;
        public int Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0, 100);
        }

        [JsonIgnore]
        public DropGameSoundOverrideMode OverrideMode
        {
            get
            {
                if (!Override) return DropGameSoundOverrideMode.Inherit;
                return Enabled ? DropGameSoundOverrideMode.Custom : DropGameSoundOverrideMode.Mute;
            }
            set
            {
                switch (value)
                {
                    case DropGameSoundOverrideMode.Inherit:
                        Override = false;
                        break;
                    case DropGameSoundOverrideMode.Custom:
                        Override = true;
                        Enabled = true;
                        break;
                    case DropGameSoundOverrideMode.Mute:
                        Override = true;
                        Enabled = false;
                        break;
                }
            }
        }
    }

    public class DropGameEffectiveSoundDto
    {
        public bool Enabled { get; set; }
        public string? Url { get; set; }
        public int Volume { get; set; }
    }

    public class DropGameSoundBundleDto
    {
        public int MaxConcurrentSounds { get; set; } = 1;
        public DropGameEffectiveSoundDto? Start { get; set; }
        public DropGameEffectiveSoundDto? End { get; set; }
        public DropGameEffectiveSoundDto? Drop { get; set; }
        public DropGameEffectiveSoundDto? ParachuteFlap { get; set; }
        public DropGameEffectiveSoundDto? Win { get; set; }
        public DropGameEffectiveSoundDto? PegCollision { get; set; }
        public DropGameEffectiveSoundDto? RocketLaunch { get; set; }
        public DropGameEffectiveSoundDto? RocketExplode { get; set; }
        public DropGameEffectiveSoundDto? ParachutePop { get; set; }
        public DropGameEffectiveSoundDto? CrashLand { get; set; }
        public DropGameEffectiveSoundDto? Cannonball { get; set; }
        public DropGameEffectiveSoundDto? BalloonLaunch { get; set; }
        public DropGameEffectiveSoundDto? BalloonPop { get; set; }
        public DropGameEffectiveSoundDto? BalloonBonk { get; set; }
        public DropGameEffectiveSoundDto? BalloonNudge { get; set; }
        public DropGameEffectiveSoundDto? BalloonWin { get; set; }
    }

    public interface IDropGameHeaderSettings
    {
        bool AutoAvoidOverlap { get; set; }
        bool ShowHeaderBanner { get; set; }
        bool ShowTimer { get; set; }
        bool ShowItemCount { get; }
        bool ShowFreePlayCounter { get; set; }
        string HeaderPosition { get; set; }
        int HeaderCustomYOffset { get; set; }
        int NameFontSize { get; set; }
    }

    public interface IDropGameSessionSettings
    {
        bool SessionModeEnabled { get; set; }
        bool AutoStartOnCommand { get; set; }
        int DefaultSessionDurationSeconds { get; set; }
        int MaxDropsPerUserPerSession { get; set; }
        bool AnnounceSessionStatusInChat { get; set; }
        string SessionStartChatMessageTemplate { get; set; }
        string SessionEndChatMessageTemplate { get; set; }
    }

    public class BalloonAscentGameSettings : IDropGameHeaderSettings, IDropGameSessionSettings
    {
        public string ActiveThemeId { get; set; } = "spring";
        public double TargetCenterX { get; set; } = 0.50;
        public double TargetWidthPercent { get; set; } = 18.0;
        public double TargetSpeed { get; set; } = 0.5;

        private double _targetAlpha = 1.0;
        public double TargetAlpha
        {
            get => _targetAlpha;
            set => _targetAlpha = Math.Clamp(value, 0.05, 1.0);
        }

        public double Buoyancy { get; set; } = 1.0;
        public double Bounciness { get; set; } = 0.85;
        public double Mass { get; set; } = 1.0;
        public double SwayAmount { get; set; } = 1.0;

        private int _maxConcurrentSounds = 2;
        public int MaxConcurrentSounds
        {
            get => _maxConcurrentSounds;
            set => _maxConcurrentSounds = Math.Clamp(value, 1, 5);
        }

        // Sound Effects (Default enabled)
        public DropGameSoundConfig StartSound { get; set; } = new() { Enabled = true, AudioFile = "/media/round_start.wav", Volume = 65 };
        public DropGameSoundConfig EndSound { get; set; } = new() { Enabled = true, AudioFile = "/media/round_end.wav", Volume = 65 };
        public DropGameSoundConfig LaunchSound { get; set; } = new() { Enabled = true, AudioFile = "/media/balloon_launch.wav", Volume = 60 };
        public DropGameSoundConfig PopSound { get; set; } = new() { Enabled = true, AudioFile = "/media/balloon_pop.wav", Volume = 70 };
        public DropGameSoundConfig BonkSound { get; set; } = new() { Enabled = true, AudioFile = "/media/balloon_anvil.wav", Volume = 65 };
        public DropGameSoundConfig NudgeSound { get; set; } = new() { Enabled = true, AudioFile = "/media/balloon_nudge.wav", Volume = 55 };
        public DropGameSoundConfig WinSound { get; set; } = new() { Enabled = true, AudioFile = "/media/balloon_win.wav", Volume = 70 };

        // Hazard Settings
        public bool EnableHazards { get; set; } = true;
        public bool EnableDarts { get; set; } = true;
        public double DartPopChance { get; set; } = 1.0;
        public bool EnableAnvils { get; set; } = true;
        public double AnvilKnockbackMultiplier { get; set; } = 1.0;
        public bool EnableNudges { get; set; } = true;
        public bool EnableWindGusts { get; set; } = true;
        public string DefaultHazardEmote { get; set; } = "";
        public string DefaultDartEmote { get; set; } = "";
        public string DefaultAnvilEmote { get; set; } = "";
        public string DefaultNudgeEmote { get; set; } = "";
        public string DefaultWindGustEmote { get; set; } = "";
        public CrashLandingScoreMode PoppedScoreMode { get; set; } = CrashLandingScoreMode.ZeroScore;

        public string WinActionName { get; set; } = "";
        public string BullseyeActionName { get; set; } = "";
        public bool AnnounceWinsInChat { get; set; } = true;
        public string WinChatMessageTemplate { get; set; } = "{user} reached the cloud platform with {score} pts!{bullseye}";

        // Timed Session / Round settings
        public bool SessionModeEnabled { get; set; } = false;
        public bool AutoStartOnCommand { get; set; } = true;
        public int DefaultSessionDurationSeconds { get; set; } = 100;
        public int MaxDropsPerUserPerSession { get; set; } = 1; // 0 = unlimited
        public bool AnnounceSessionStatusInChat { get; set; } = true;
        public string SessionStartChatMessageTemplate { get; set; } = "Balloon Ascent is now OPEN for {duration}s! Type !balloon [emote] to join!";
        public string SessionEndChatMessageTemplate { get; set; } = "Balloon Ascent round is now CLOSED! {count} total balloons launched!";

        // Overlay Header & Display settings
        public bool AutoAvoidOverlap { get; set; } = true;
        public bool ShowHeaderBanner { get; set; } = true;
        public bool ShowTimer { get; set; } = true;
        public bool ShowDropCount { get; set; } = true;

        [JsonIgnore]
        public bool ShowItemCount => ShowDropCount;

        public bool ShowFreePlayCounter { get; set; } = true;
        public string HeaderPosition { get; set; } = "TopCenter"; // TopCenter, TopLeft, TopRight
        public int HeaderCustomYOffset { get; set; } = 18;

        private int _nameFontSize = 18;
        public int NameFontSize
        {
            get => _nameFontSize;
            set => _nameFontSize = Math.Clamp(value, 12, 36);
        }
    }

    public enum CrashLandingScoreMode
    {
        PreserveScore = 0,
        ZeroScore = 1,
        HalfScore = 2
    }

    public class ParachuteGameSettings : IDropGameHeaderSettings, IDropGameSessionSettings
    {
        public string ActiveThemeId { get; set; } = "spring";
        public double TargetCenterX { get; set; } = 0.50;
        public double TargetWidthPercent { get; set; } = 18.0;
        public double Gravity { get; set; } = 1.0;
        public double Bounciness { get; set; } = 0.85;
        public double Mass { get; set; } = 1.0;

        private int _maxConcurrentSounds = 1;
        public int MaxConcurrentSounds
        {
            get => _maxConcurrentSounds;
            set => _maxConcurrentSounds = Math.Clamp(value, 1, 5);
        }

        // Sound Effects (Default enabled)
        public DropGameSoundConfig StartSound { get; set; } = new() { Enabled = true, AudioFile = "/media/round_start.wav", Volume = 65 };
        public DropGameSoundConfig EndSound { get; set; } = new() { Enabled = true, AudioFile = "/media/round_end.wav", Volume = 65 };
        public DropGameSoundConfig DropSound { get; set; } = new() { Enabled = true, AudioFile = "/media/parachute_drop.wav", Volume = 60 };
        public DropGameSoundConfig ParachuteFlapSound { get; set; } = new() { Enabled = true, AudioFile = "/media/parachute_pop.wav", Volume = 65 };
        public DropGameSoundConfig WinSound { get; set; } = new() { Enabled = true, AudioFile = "/media/droplet.wav", Volume = 65 };

        // Sky Rockets & Sabotage Sound Effects (Default enabled)
        public DropGameSoundConfig RocketLaunchSound { get; set; } = new() { Enabled = true, AudioFile = "/media/rocket_launch.wav", Volume = 60 };
        public DropGameSoundConfig RocketExplodeSound { get; set; } = new() { Enabled = true, AudioFile = "/media/rocket_explode.wav", Volume = 70 };
        public DropGameSoundConfig ParachutePopSound { get; set; } = new() { Enabled = true, AudioFile = "/media/parachute_pop.wav", Volume = 70 };
        public DropGameSoundConfig CrashLandSound { get; set; } = new() { Enabled = true, AudioFile = "/media/splat.mp3", Volume = 60 };
        public DropGameSoundConfig CannonballSound { get; set; } = new() { Enabled = true, AudioFile = "/media/cannonball.wav", Volume = 75 };

        // Sky Rockets & Sabotage Settings
        public bool EnableRockets { get; set; } = true;
        public double RocketKnockbackMultiplier { get; set; } = 1.0;
        public bool DirectHitKillsParachute { get; set; } = true;
        public double CanopyKillChance { get; set; } = 1.0; // 100% chance default
        public string RocketLaunchPattern { get; set; } = "Targeted"; // Targeted, Random, Barrage
        public CrashLandingScoreMode CrashLandingScoreMode { get; set; } = CrashLandingScoreMode.PreserveScore;

        // Legacy database JSON field; ignored on write and marked for deletion later
        public double DescentDurationSeconds
        {
            get => Math.Round(Math.Clamp(6.2 / Math.Pow(Math.Clamp(Gravity, 0.1, 10.0), 0.25), 3.5, 8.5), 2);
            set { /* Ignored - superseded by Gravity, Bounciness, and Mass */ }
        }

        // Legacy database JSON field; wind drift removed in #1371 in favor of clean launch angles and collisions
        public double WindStrength
        {
            get => 0.0;
            set { /* Ignored - wind removed */ }
        }
        public string WinActionName { get; set; } = "";
        public bool AnnounceWinsInChat { get; set; } = true;
        public string WinChatMessageTemplate { get; set; } = "{user} landed on the target with {score} pts!{bullseye}";

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
        public string SessionStartChatMessageTemplate { get; set; } = "Parachute Drop is now OPEN for {duration}s! Type !drop [emote] to join!";
        public string SessionEndChatMessageTemplate { get; set; } = "Parachute Drop round is now CLOSED! {count} total parachutists dropped!";

        // Overlay Header & Display settings
        public bool AutoAvoidOverlap { get; set; } = true;
        public bool ShowHeaderBanner { get; set; } = true;
        public bool ShowTimer { get; set; } = true;
        public bool ShowDropCount { get; set; } = true;

        [JsonIgnore]
        public bool ShowItemCount => ShowDropCount;

        public bool ShowFreePlayCounter { get; set; } = true;
        public string HeaderPosition { get; set; } = "TopCenter"; // TopCenter, TopLeft, TopRight
        public int HeaderCustomYOffset { get; set; } = 18;

        private int _nameFontSize = 18;
        public int NameFontSize
        {
            get => _nameFontSize;
            set => _nameFontSize = Math.Clamp(value, 12, 36);
        }
    }

    public class PlinkoGameSettings : IDropGameHeaderSettings, IDropGameSessionSettings
    {
        private int _pegTransparency = 15;

        public string ActiveThemeId { get; set; } = "spring";
        public int RowCount { get; set; } = 4;
        private int _pegRadius = 12;
        public int PegRadius
        {
            get => _pegRadius;
            set => _pegRadius = Math.Clamp(value, 4, 30);
        }
        private int _puckRadius = 36;
        public int PuckRadius
        {
            get => _puckRadius;
            set => _puckRadius = Math.Clamp(value, 10, 50);
        }
        private int _bucketHeight = 120;
        public int BucketHeight
        {
            get => _bucketHeight;
            set => _bucketHeight = Math.Clamp(value, 60, 220);
        }
        public double Gravity { get; set; } = 1.0;
        public double Bounciness { get; set; } = 0.75;
        public double Mass { get; set; } = 1.0;

        private int _maxConcurrentSounds = 1;
        public int MaxConcurrentSounds
        {
            get => _maxConcurrentSounds;
            set => _maxConcurrentSounds = Math.Clamp(value, 1, 5);
        }

        // Sound Effects (Default enabled)
        public DropGameSoundConfig StartSound { get; set; } = new() { Enabled = true, AudioFile = "/media/round_start.wav", Volume = 65 };
        public DropGameSoundConfig EndSound { get; set; } = new() { Enabled = true, AudioFile = "/media/round_end.wav", Volume = 65 };
        public DropGameSoundConfig DropSound { get; set; } = new() { Enabled = true, AudioFile = "/media/plinko_drop.wav", Volume = 60 };
        public DropGameSoundConfig WinSound { get; set; } = new() { Enabled = true, AudioFile = "/media/plinko_win.wav", Volume = 70 };
        public DropGameSoundConfig PegCollisionSound { get; set; } = new() { Enabled = true, AudioFile = "/media/plinko_peg.wav", Volume = 50 };

        // Legacy database JSON field; ignored on write and marked for deletion later
        public double DropDurationSeconds
        {
            get => Math.Round(Math.Clamp(4.5 / Math.Clamp(Gravity, 0.1, 10.0), 0.45, 45.0), 2);
            set { /* Ignored - superseded by Gravity, Bounciness, and Mass */ }
        }

        public int PegTransparency
        {
            get => _pegTransparency;
            set => _pegTransparency = Math.Clamp(value, 0, 100);
        }
        public string WinActionName { get; set; } = "";
        public bool AnnounceWinsInChat { get; set; } = true;
        public string WinChatMessageTemplate { get; set; } = "{user}'s Plinko token landed in [{bucket}]!";
        public List<PlinkoBucket> Buckets { get; set; } = [];

        // Timed Session / Round settings
        public bool SessionModeEnabled { get; set; } = false;
        public bool AutoStartOnCommand { get; set; } = true;
        public int DefaultSessionDurationSeconds { get; set; } = 100;
        public int MaxDropsPerUserPerSession { get; set; } = 1; // 0 = unlimited
        public bool AnnounceSessionStatusInChat { get; set; } = true;
        public string SessionStartChatMessageTemplate { get; set; } = "Plinko Bounce is now OPEN for {duration}s! Type !plinko [emote] to join!";
        public string SessionEndChatMessageTemplate { get; set; } = "Plinko Bounce round is now CLOSED! {count} total tokens dropped!";

        // Overlay Header & Display settings
        public bool AutoAvoidOverlap { get; set; } = true;
        public bool ShowHeaderBanner { get; set; } = true;
        public bool ShowTimer { get; set; } = true;
        public bool ShowTokenCount { get; set; } = true;

        [JsonIgnore]
        public bool ShowDropCount
        {
            get => ShowTokenCount;
            set => ShowTokenCount = value;
        }

        [JsonIgnore]
        public bool ShowItemCount => ShowTokenCount;

        public bool ShowFreePlayCounter { get; set; } = true;
        public string HeaderPosition { get; set; } = "TopCenter"; // TopCenter, TopLeft, TopRight
        public int HeaderCustomYOffset { get; set; } = 18;

        private int _nameFontSize = 18;
        public int NameFontSize
        {
            get => _nameFontSize;
            set => _nameFontSize = Math.Clamp(value, 12, 36);
        }

        public static List<PlinkoBucket> CreateDefaultBuckets() =>
        [
            new PlinkoBucket { Label = "MISS", Score = 0, IsWin = false, Color = "#64748B", Weight = 25 },
            new PlinkoBucket { Label = "MISS", Score = 0, IsWin = false, Color = "#64748B", Weight = 25 },
            new PlinkoBucket { Label = "WINNER", Score = 0, IsWin = true, Color = "#F59E0B", Weight = 15 },
            new PlinkoBucket { Label = "MISS", Score = 0, IsWin = false, Color = "#64748B", Weight = 25 },
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
        public List<PenguinTwitchBot.Bot.Models.Chat.ChatOverlayFragment>? Fragments { get; set; }
        public string? TriggeredByUser { get; set; }
        public bool AllowDirectUrlOrPath { get; set; } = true;
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

        [JsonPropertyName("targetDropToken")]
        public string? TargetDropToken { get; set; }

        [JsonPropertyName("hazardType")]
        public string HazardType { get; set; } = "Nudge";
    }

    public class RocketFireRequest
    {
        public int Count { get; set; } = 1;
        public string Pattern { get; set; } = "Targeted"; // Targeted, Random, Barrage
        public string? TargetUser { get; set; }
        public double? CanopyKillChance { get; set; }
        public double? KnockbackMultiplier { get; set; }
        public CrashLandingScoreMode? ScoreModeOverride { get; set; }
        public string? TriggeredByUser { get; set; }
        public bool? DirectHitKillsParachute { get; set; }
    }

    public class RocketInstanceDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("spawnTickOffset")]
        public int SpawnTickOffset { get; set; }

        [JsonPropertyName("startX")]
        public double StartX { get; set; }

        [JsonPropertyName("startY")]
        public double StartY { get; set; }

        [JsonPropertyName("targetX")]
        public double TargetX { get; set; }

        [JsonPropertyName("targetY")]
        public double TargetY { get; set; }

        [JsonPropertyName("vx")]
        public double Vx { get; set; }

        [JsonPropertyName("vy")]
        public double Vy { get; set; }

        [JsonPropertyName("accelerationY")]
        public double AccelerationY { get; set; }

        [JsonPropertyName("fuseTicks")]
        public int FuseTicks { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#FF3366";

        [JsonPropertyName("trailType")]
        public string TrailType { get; set; } = "sparks";

        [JsonPropertyName("targetDropToken")]
        public string? TargetDropToken { get; set; }

        [JsonPropertyName("pattern")]
        public string Pattern { get; set; } = "Random";

        [JsonPropertyName("isTargeted")]
        public bool IsTargeted { get; set; }

        [JsonPropertyName("knockbackMultiplier")]
        public double KnockbackMultiplier { get; set; } = 1.0;

        [JsonPropertyName("canopyKillChance")]
        public double CanopyKillChance { get; set; } = 1.0;

        [JsonPropertyName("directHitKillsParachute")]
        public bool DirectHitKillsParachute { get; set; } = true;
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

        [JsonPropertyName("rocketId")]
        public string? RocketId { get; set; }

        [JsonPropertyName("collisionType")]
        public string CollisionType { get; set; } = "droplet"; // droplet, rocket_canopy, rocket_body

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

        [JsonPropertyName("rocketHits")]
        public int RocketHits { get; set; }

        [JsonPropertyName("parachuteDestroyed")]
        public bool ParachuteDestroyed { get; set; }

        [JsonPropertyName("freeFallStartTick")]
        public int FreeFallStartTick { get; set; }

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

        [JsonPropertyName("parachuteDestroyed")]
        public bool ParachuteDestroyed { get; set; }

        [JsonPropertyName("crashLanded")]
        public bool CrashLanded { get; set; }

        [JsonPropertyName("score")]
        public int? Score { get; set; }

        [JsonPropertyName("isBullseye")]
        public bool? IsBullseye { get; set; }

        [JsonPropertyName("isWin")]
        public bool? IsWin { get; set; }

        [JsonPropertyName("isHit")]
        public bool? IsHit { get; set; }

        [JsonPropertyName("balloonPopped")]
        public bool BalloonPopped { get; set; }

        [JsonPropertyName("overlayKey")]
        public string? OverlayKey { get; set; }
    }

    public class DropGamePongMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "pong";

        [JsonPropertyName("game")]
        public string? Game { get; set; }

        [JsonPropertyName("activeTokens")]
        public List<string> ActiveTokens { get; set; } = [];

        [JsonPropertyName("completingTokens")]
        public List<string> CompletingTokens { get; set; } = [];

        [JsonPropertyName("overlayKey")]
        public string? OverlayKey { get; set; }
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
        public string UserColor { get; init; } = "#FFFFFF";
        public required CommandEventArgs EventArgs { get; init; }
        public required DropGameTheme Theme { get; init; }
        public required DateTime CreatedAtUtc { get; init; }
        public required DateTime EarliestCompletionUtc { get; init; }
        public required DateTime ExpiresAtUtc { get; set; }

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
        public double Gravity { get; init; } = 1.0;
        public double Bounciness { get; init; } = 0.85;
        public double Mass { get; init; } = 1.0;

        // Authoritative outcome (updated by server 60Hz simulation if droplets collide)
        public double LandingX { get; set; }
        public int Score { get; set; }
        public bool IsHit { get; set; }
        public bool IsBullseye { get; set; }
        public bool IsWin { get; set; }
        public int DropletHitCount { get; set; }
        public int RocketHitCount { get; set; }
        public bool ParachuteDestroyed { get; set; }
        public bool BalloonPopped { get; set; }
        public int FreeFallStartTick { get; set; }
        public CrashLandingScoreMode CrashLandingScoreMode { get; set; } = CrashLandingScoreMode.PreserveScore;
        public int BucketIndex { get; set; }
        public string BucketLabel { get; set; } = "";
        public bool ForcedOutcome { get; init; }
        public string? RocketAttacker { get; set; }

        public List<ParachuteCollisionEventDto> Collisions { get; set; } = [];
        public List<ParachuteTrajectoryPointDto> Keyframes { get; set; } = [];
        public List<PlinkoBucket>? Buckets { get; init; }

        // Processing & completion state
        public bool IsProcessing { get; set; }
        public bool IsCompleted { get; set; }
        public bool DefaultCommandTriggerExecuted { get; set; }
        public bool WinActionExecuted { get; set; }
        public bool WinChatAnnounced { get; set; }
        public DateTime? LastHeartbeatUtc { get; set; }
        public int MissedHeartbeatCount { get; set; }
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

        [JsonPropertyName("isOtherGameActive")]
        public bool IsOtherGameActive { get; set; }

        [JsonPropertyName("headerConfig")]
        public DropGameHeaderConfigDto? HeaderConfig { get; set; }

        [JsonPropertyName("sounds")]
        public DropGameSoundBundleDto? Sounds { get; set; }
    }

    public class DropGameHeaderConfigDto
    {
        [JsonPropertyName("autoAvoidOverlap")]
        public bool AutoAvoidOverlap { get; set; } = true;

        [JsonPropertyName("showBanner")]
        public bool ShowBanner { get; set; } = true;

        [JsonPropertyName("showTimer")]
        public bool ShowTimer { get; set; } = true;

        [JsonPropertyName("showCount")]
        public bool ShowCount { get; set; } = true;

        [JsonPropertyName("showFreePlayCounter")]
        public bool ShowFreePlayCounter { get; set; } = true;

        [JsonPropertyName("position")]
        public string Position { get; set; } = "TopCenter";

        [JsonPropertyName("yOffset")]
        public int YOffset { get; set; } = 18;

        [JsonPropertyName("nameFontSize")]
        public int NameFontSize { get; set; } = 18;

        public static DropGameHeaderConfigDto FromSettings(IDropGameHeaderSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new DropGameHeaderConfigDto
            {
                AutoAvoidOverlap = settings.AutoAvoidOverlap,
                ShowBanner = settings.ShowHeaderBanner,
                ShowTimer = settings.ShowTimer,
                ShowCount = settings.ShowItemCount,
                ShowFreePlayCounter = settings.ShowFreePlayCounter,
                Position = settings.HeaderPosition,
                YOffset = settings.HeaderCustomYOffset,
                NameFontSize = settings.NameFontSize
            };
        }
    }
}


