namespace PenguinTwitchBot.Database.Bot.Models.Credits
{
    public static class CreditTimeWindowModes
    {
        public const string SinceStreamStart = "SinceStreamStart";
        public const string LastCompletedStream = "LastCompletedStream";
        public const string RollingLookback = "RollingLookback";
        public const string SinceManualCheckpoint = "SinceManualCheckpoint";
        public const string CustomDateRange = "CustomDateRange";
        public const string SpecificSession = "SpecificSession";
    }

    public static class CreditLookbackUnits
    {
        public const string Minutes = "Minutes";
        public const string Hours = "Hours";
        public const string Days = "Days";
    }

    public static class CreditAnimationModes
    {
        public const string InheritTheme = "InheritTheme";
        public const string SmoothScroll = "SmoothScroll";
        public const string Crawl3D = "Crawl3D";
        public const string PagedSlideshow = "PagedSlideshow";
    }

    public static class CreditSpeedModes
    {
        public const string FixedSpeed = "FixedSpeed";
        public const string TargetDuration = "TargetDuration";
    }

    public static class CreditStartBehaviors
    {
        public const string WaitForTrigger = "WaitForTrigger";
        public const string AutoStartOnLoad = "AutoStartOnLoad";
    }

    public static class CreditEndBehaviors
    {
        public const string FadeOutAndHide = "FadeOutAndHide";
        public const string HoldFooter = "HoldFooter";
        public const string Loop = "Loop";
        public const string FreezeOnEnd = "FreezeOnEnd";
    }

    public static class CreditLayouts
    {
        public const string InheritTheme = "InheritTheme";
        public const string SingleColumn = "SingleColumn";
        public const string Grid2 = "Grid2";
        public const string Grid3 = "Grid3";
        public const string Grid4 = "Grid4";
        public const string InlinePills = "InlinePills";
        public const string AvatarCards = "AvatarCards";
        public const string ClassicCast = "ClassicCast";
    }

    public static class CreditAvatarModes
    {
        public const string InheritTheme = "InheritTheme";
        public const string Show = "Show";
        public const string Hide = "Hide";
    }

    public static class CreditAvatarFallbacks
    {
        public const string ColoredSilhouette = "ColoredSilhouette";
        public const string Initials = "Initials";
        public const string CustomImage = "CustomImage";
        public const string Hide = "Hide";
    }

    public static class CreditAggregationModes
    {
        public const string CombineByUserSum = "CombineByUserSum";
        public const string UniqueUsersLatest = "UniqueUsersLatest";
        public const string UniqueUsersHighest = "UniqueUsersHighest";
        public const string IndividualEvents = "IndividualEvents";
    }

    public static class CreditSortModes
    {
        public const string HighestAmount = "HighestAmount";
        public const string MostRecent = "MostRecent";
        public const string OldestFirst = "OldestFirst";
        public const string Alphabetical = "Alphabetical";
        public const string Random = "Random";
    }

    public static class CreditBackgroundTypes
    {
        public const string InheritTheme = "InheritTheme";
        public const string Transparent = "Transparent";
        public const string Solid = "Solid";
        public const string Gradient = "Gradient";
        public const string Image = "Image";
        public const string Starfield = "Starfield";
    }

    public static class CreditSampleModes
    {
        public const string Off = "off";
        public const string Fake = "fake";
        public const string DatabaseViewers = "db";
    }

    public sealed class CreditsProfileConfig
    {
        public CreditsTimeWindowConfig TimeWindow { get; set; } = new();
        public CreditsHeaderConfig Header { get; set; } = new();
        public CreditsFooterConfig Footer { get; set; } = new();
        public CreditsPlaybackConfig Playback { get; set; } = new();
        public CreditsExclusionConfig Exclusions { get; set; } = new();
        public CreditsMediaOverrideConfig Media { get; set; } = new();
        public List<CreditsSectionConfig> Sections { get; set; } = [];
        public string CustomCssOverride { get; set; } = string.Empty;
    }

    public sealed class CreditsTimeWindowConfig
    {
        public string Mode { get; set; } = CreditTimeWindowModes.SinceStreamStart;
        public int PreStreamGraceMinutes { get; set; } = 15;
        public int LookbackValue { get; set; } = 6;
        public string LookbackUnit { get; set; } = CreditLookbackUnits.Hours;
        public DateTime? CustomStartUtc { get; set; }
        public DateTime? CustomEndUtc { get; set; }
        public Guid? SpecificSessionId { get; set; }
        public DateTime? ManualCheckpointUtc { get; set; }
    }

    public sealed class CreditsHeaderConfig
    {
        public bool Enabled { get; set; } = true;
        public string Title { get; set; } = "Credits";
        public string Subtitle { get; set; } = "Thank you for the Support!";
        public string? ImageUrl { get; set; }
        public int ImageMaxHeightPx { get; set; } = 160;
        public bool ShowBroadcasterAvatar { get; set; }
        public double IntroDelaySeconds { get; set; } = 0.5;
    }

    public sealed class CreditsFooterConfig
    {
        public bool Enabled { get; set; } = true;
        public string Title { get; set; } = "Thank you for watching!";
        public List<string> Lines { get; set; } = ["See you next stream!"];
        public string? ImageUrl { get; set; }
        public int ImageMaxHeightPx { get; set; } = 140;
        public double HoldSeconds { get; set; } = 4.0;
    }

    public sealed class CreditsPlaybackConfig
    {
        public string AnimationMode { get; set; } = CreditAnimationModes.InheritTheme;
        public string SpeedMode { get; set; } = CreditSpeedModes.FixedSpeed;
        public double ScrollSpeedPxPerSec { get; set; } = 75;
        public double TargetDurationSeconds { get; set; } = 60;
        public double PageDurationSeconds { get; set; } = 6;
        public string StartBehavior { get; set; } = CreditStartBehaviors.WaitForTrigger;
        public string EndBehavior { get; set; } = CreditEndBehaviors.FadeOutAndHide;
        public string TestModeDefault { get; set; } = CreditSampleModes.Off;
    }

    public sealed class CreditsExclusionConfig
    {
        public bool ExcludeKnownBots { get; set; } = true;
        public bool ExcludeBroadcaster { get; set; } = true;
        public bool ExcludeBotAccount { get; set; } = true;
        public List<string> ExcludedUsernames { get; set; } = [];
        public bool IncludeAnonymous { get; set; } = true;
        public string AnonymousLabel { get; set; } = "Anonymous";
    }

    public sealed class CreditsMediaOverrideConfig
    {
        public string BackgroundTypeOverride { get; set; } = CreditBackgroundTypes.InheritTheme;
        public string? BackgroundColorOverride { get; set; }
        public string? BackgroundGradientOverride { get; set; }
        public string? BackgroundImageUrlOverride { get; set; }
        public string? BackgroundAudioUrl { get; set; }
        public double AudioVolume { get; set; } = 0.5;
        public bool AudioLoop { get; set; } = true;
        public double AudioFadeInSeconds { get; set; } = 1.5;
        public double AudioFadeOutSeconds { get; set; } = 2.5;
    }

    public sealed class CreditsSectionConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public bool Enabled { get; set; } = true;
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? HeaderImageUrl { get; set; }
        public List<string> SourceTypes { get; set; } = [];
        public bool HideWhenEmpty { get; set; } = true;
        public string? EmptyFallbackText { get; set; }
        public string LayoutOverride { get; set; } = CreditLayouts.InheritTheme;
        public string AvatarMode { get; set; } = CreditAvatarModes.InheritTheme;
        public int? AvatarSizePxOverride { get; set; }
        public bool ShowAmount { get; set; } = true;
        public string EntryFormatTemplate { get; set; } = "{name}";
        public string AggregationMode { get; set; } = CreditAggregationModes.CombineByUserSum;
        public string SortMode { get; set; } = CreditSortModes.HighestAmount;
        public decimal? MinAmount { get; set; }
        public int MaxEntries { get; set; }
        public string? DetailFilter { get; set; }
        public bool IncludeAllChannelRoleMembers { get; set; }
        public string FishingRankBy { get; set; } = "Gold";
        public string? CustomCssClass { get; set; }
        public List<CreditsStaticEntry> StaticEntries { get; set; } = [];
    }

    public sealed class CreditsStaticEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? TwitchUsername { get; set; }
        public string? ImageUrl { get; set; }
    }

    public sealed class CreditsThemeConfig
    {
        public string AnimationMode { get; set; } = CreditAnimationModes.SmoothScroll;
        public string DefaultLayout { get; set; } = CreditLayouts.SingleColumn;

        public string BackgroundType { get; set; } = CreditBackgroundTypes.Gradient;
        public string BackgroundColor { get; set; } = "#090b10";
        public string BackgroundGradient { get; set; } = "radial-gradient(ellipse at center, rgba(16, 20, 32, 0.94) 0%, rgba(5, 7, 11, 0.98) 100%)";
        public string? BackgroundImageUrl { get; set; }
        public string BackgroundImageFit { get; set; } = "cover";
        public int BackgroundBlurPx { get; set; }
        public string BackgroundScrimColor { get; set; } = "#000000";
        public double BackgroundScrimOpacity { get; set; } = 0.35;

        public string FontFamilyHeading { get; set; } = "Cinzel";
        public string FontFamilyBody { get; set; } = "Montserrat";
        public int HeaderFontSizePx { get; set; } = 64;
        public int SubtitleFontSizePx { get; set; } = 28;
        public int SectionTitleFontSizePx { get; set; } = 36;
        public int EntryFontSizePx { get; set; } = 26;
        public int DetailFontSizePx { get; set; } = 18;
        public int FooterFontSizePx { get; set; } = 42;

        public string TextColor { get; set; } = "#F4F6FA";
        public string SecondaryTextColor { get; set; } = "#B8C2D4";
        public string AccentColor { get; set; } = "#F5C451";
        public string HeaderColor { get; set; } = "#FFFFFF";
        public string SectionTitleColor { get; set; } = "#F5C451";
        public string TextShadow { get; set; } = "0 2px 10px rgba(0, 0, 0, 0.75)";
        public bool HeadingItalic { get; set; }
        public string TextAlign { get; set; } = "center";

        public int ContainerMaxWidthPx { get; set; } = 960;
        public int SectionGapPx { get; set; } = 56;
        public int EntryGapPx { get; set; } = 14;
        public int EdgeFadeHeightPct { get; set; } = 12;

        public bool ShowAvatars { get; set; } = true;
        public int AvatarSizePx { get; set; } = 40;
        public string AvatarShape { get; set; } = "circle";
        public int AvatarBorderWidthPx { get; set; } = 2;
        public string AvatarBorderColor { get; set; } = "rgba(245, 196, 81, 0.65)";
        public string AvatarFallbackMode { get; set; } = CreditAvatarFallbacks.ColoredSilhouette;
        public string? AvatarFallbackImageUrl { get; set; }

        public int CrawlPerspectivePx { get; set; } = 360;
        public int CrawlTiltDegrees { get; set; } = 25;
        public int CrawlFadeTopPct { get; set; } = 30;

        public string CustomCss { get; set; } = string.Empty;
    }

    public sealed class CreditsRenderPayload
    {
        public string ProfileName { get; set; } = "Default";
        public string ThemeName { get; set; } = "Classic Cinema";
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime WindowStartUtc { get; set; }
        public DateTime WindowEndUtc { get; set; }
        public string SampleMode { get; set; } = CreditSampleModes.Off;
        public int TotalEntries { get; set; }
        public CreditsRenderedHeader Header { get; set; } = new();
        public CreditsRenderedFooter Footer { get; set; } = new();
        public CreditsPlaybackConfig Playback { get; set; } = new();
        public CreditsThemeConfig Theme { get; set; } = new();
        public CreditsMediaOverrideConfig Media { get; set; } = new();
        public string ProfileCustomCss { get; set; } = string.Empty;
        public List<CreditsRenderedSection> Sections { get; set; } = [];
    }

    public sealed class CreditsRenderedHeader
    {
        public bool Enabled { get; set; } = true;
        public string Title { get; set; } = "Credits";
        public string Subtitle { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int ImageMaxHeightPx { get; set; } = 160;
        public string? BroadcasterAvatarUrl { get; set; }
        public double IntroDelaySeconds { get; set; } = 0.5;
    }

    public sealed class CreditsRenderedFooter
    {
        public bool Enabled { get; set; } = true;
        public string Title { get; set; } = "Thank you for watching!";
        public List<string> Lines { get; set; } = [];
        public string? ImageUrl { get; set; }
        public int ImageMaxHeightPx { get; set; } = 140;
        public double HoldSeconds { get; set; } = 4.0;
    }

    public sealed class CreditsRenderedSection
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? HeaderImageUrl { get; set; }
        public List<string> SourceTypes { get; set; } = [];
        public string Layout { get; set; } = CreditLayouts.SingleColumn;
        public bool ShowAvatars { get; set; } = true;
        public int AvatarSizePx { get; set; } = 40;
        public string? EmptyFallbackText { get; set; }
        public string? CustomCssClass { get; set; }
        public List<CreditsRenderedEntry> Entries { get; set; } = [];
    }

    public sealed class CreditsRenderedEntry
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string AvatarColor { get; set; } = "#9146FF";
        public string FormattedText { get; set; } = string.Empty;
        public string? Detail { get; set; }
        public decimal Amount { get; set; }
        public string? Role { get; set; }
        public string EventType { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? InternalUserId { get; set; }
    }
}
