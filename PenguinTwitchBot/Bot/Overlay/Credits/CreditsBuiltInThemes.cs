using PenguinTwitchBot.Database.Bot.Models.Credits;

namespace PenguinTwitchBot.Bot.Overlay.Credits
{
    public sealed record BuiltInCreditThemeDefinition(
        string BuiltInKey,
        string Name,
        string Description,
        CreditsThemeConfig Config);

    public static class CreditsBuiltInThemes
    {
        public const string DefaultThemeKey = "classic-cinema";
        public const string GalacticCrawlKey = "galactic-crawl";

        public static IReadOnlyList<BuiltInCreditThemeDefinition> All { get; } =
        [
            new(
                DefaultThemeKey,
                "Classic Cinema",
                "Traditional smooth-rolling end credits with crisp typography, circular Twitch avatars, and subtle gold accents.",
                new CreditsThemeConfig
                {
                    AnimationMode = CreditAnimationModes.SmoothScroll,
                    DefaultLayout = CreditLayouts.SingleColumn,
                    BackgroundType = CreditBackgroundTypes.Gradient,
                    BackgroundColor = "#07090e",
                    BackgroundGradient = "radial-gradient(ellipse at center, rgba(16, 20, 32, 0.94) 0%, rgba(5, 7, 11, 0.98) 100%)",
                    BackgroundScrimColor = "#000000",
                    BackgroundScrimOpacity = 0.25,
                    FontFamilyHeading = "Cinzel",
                    FontFamilyBody = "Montserrat",
                    HeaderFontSizePx = 68,
                    SubtitleFontSizePx = 28,
                    SectionTitleFontSizePx = 36,
                    EntryFontSizePx = 26,
                    DetailFontSizePx = 18,
                    FooterFontSizePx = 44,
                    TextColor = "#F4F6FA",
                    SecondaryTextColor = "#B8C2D4",
                    AccentColor = "#F5C451",
                    HeaderColor = "#FFFFFF",
                    SectionTitleColor = "#F5C451",
                    TextShadow = "0 2px 10px rgba(0, 0, 0, 0.75)",
                    HeadingItalic = false,
                    TextAlign = "center",
                    ContainerMaxWidthPx = 960,
                    SectionGapPx = 56,
                    EntryGapPx = 14,
                    EdgeFadeHeightPct = 12,
                    ShowAvatars = true,
                    AvatarSizePx = 40,
                    AvatarShape = "circle",
                    AvatarBorderWidthPx = 2,
                    AvatarBorderColor = "rgba(245, 196, 81, 0.65)",
                    AvatarFallbackMode = CreditAvatarFallbacks.ColoredSilhouette,
                    CustomCss = """
                        .credits-section-header::after {
                            content: "";
                            display: block;
                            width: 140px;
                            height: 2px;
                            margin: 10px auto 0;
                            background: linear-gradient(90deg, transparent, var(--credits-accent), transparent);
                        }
                        """
                }),

            new(
                GalacticCrawlKey,
                "Galactic Crawl",
                "Iconic 3D tilted space crawl over a twinkling starfield with golden text and circular avatars.",
                new CreditsThemeConfig
                {
                    AnimationMode = CreditAnimationModes.Crawl3D,
                    DefaultLayout = CreditLayouts.SingleColumn,
                    BackgroundType = CreditBackgroundTypes.Starfield,
                    BackgroundColor = "#000000",
                    BackgroundGradient = "#000000",
                    BackgroundScrimColor = "#000000",
                    BackgroundScrimOpacity = 0,
                    FontFamilyHeading = "News Cycle",
                    FontFamilyBody = "News Cycle",
                    HeaderFontSizePx = 58,
                    SubtitleFontSizePx = 30,
                    SectionTitleFontSizePx = 42,
                    EntryFontSizePx = 30,
                    DetailFontSizePx = 22,
                    FooterFontSizePx = 54,
                    TextColor = "#FEDA4A",
                    SecondaryTextColor = "#FFE885",
                    AccentColor = "#FEDA4A",
                    HeaderColor = "#FEDA4A",
                    SectionTitleColor = "#FEDA4A",
                    TextShadow = "0 0 10px rgba(254, 218, 74, 0.25)",
                    HeadingItalic = true,
                    TextAlign = "center",
                    ContainerMaxWidthPx = 860,
                    SectionGapPx = 48,
                    EntryGapPx = 14,
                    EdgeFadeHeightPct = 22,
                    ShowAvatars = true,
                    AvatarSizePx = 38,
                    AvatarShape = "circle",
                    AvatarBorderWidthPx = 0,
                    AvatarBorderColor = "transparent",
                    AvatarFallbackMode = CreditAvatarFallbacks.ColoredSilhouette,
                    CrawlPerspectivePx = 360,
                    CrawlTiltDegrees = 26,
                    CrawlFadeTopPct = 30,
                    CustomCss = """
                        .credits-entry-text {
                            letter-spacing: 0.03em;
                            font-weight: 600;
                        }
                        .credits-section-title {
                            letter-spacing: 0.06em;
                            font-weight: 700;
                        }
                        .credits-footer-title {
                            font-style: italic;
                            margin-top: 24px;
                        }
                        """
                }),

            new(
                "lumia-spotlight",
                "Lumia Spotlight",
                "Modern streaming overlay featuring glowing card pills, accented avatar rings, and vibrant gradients.",
                new CreditsThemeConfig
                {
                    AnimationMode = CreditAnimationModes.SmoothScroll,
                    DefaultLayout = CreditLayouts.Grid2,
                    BackgroundType = CreditBackgroundTypes.Gradient,
                    BackgroundColor = "#0b0d17",
                    BackgroundGradient = "linear-gradient(160deg, rgba(16, 14, 34, 0.94) 0%, rgba(10, 22, 42, 0.96) 100%)",
                    FontFamilyHeading = "Poppins",
                    FontFamilyBody = "Poppins",
                    HeaderFontSizePx = 62,
                    SubtitleFontSizePx = 24,
                    SectionTitleFontSizePx = 32,
                    EntryFontSizePx = 22,
                    DetailFontSizePx = 16,
                    FooterFontSizePx = 38,
                    TextColor = "#F8FAFC",
                    SecondaryTextColor = "#CBD5E1",
                    AccentColor = "#38BDF8",
                    HeaderColor = "#FFFFFF",
                    SectionTitleColor = "#38BDF8",
                    TextShadow = "0 2px 12px rgba(56, 189, 248, 0.28)",
                    TextAlign = "center",
                    ContainerMaxWidthPx = 1100,
                    SectionGapPx = 48,
                    EntryGapPx = 14,
                    EdgeFadeHeightPct = 12,
                    ShowAvatars = true,
                    AvatarSizePx = 42,
                    AvatarShape = "circle",
                    AvatarBorderWidthPx = 2,
                    AvatarBorderColor = "#A855F7",
                    AvatarFallbackMode = CreditAvatarFallbacks.ColoredSilhouette,
                    CustomCss = """
                        .credits-entry {
                            background: rgba(255, 255, 255, 0.06);
                            border: 1px solid rgba(56, 189, 248, 0.25);
                            border-radius: 999px;
                            padding: 8px 18px 8px 10px;
                            box-shadow: 0 4px 16px rgba(0, 0, 0, 0.35);
                        }
                        .credits-section-title {
                            text-transform: uppercase;
                            letter-spacing: 0.12em;
                        }
                        """
                }),

            new(
                "neon-cyber",
                "Neon Cyber",
                "Synthwave dark backdrop with electric cyan & magenta neon text glow and sharp cyber frames.",
                new CreditsThemeConfig
                {
                    AnimationMode = CreditAnimationModes.SmoothScroll,
                    DefaultLayout = CreditLayouts.SingleColumn,
                    BackgroundType = CreditBackgroundTypes.Gradient,
                    BackgroundColor = "#070415",
                    BackgroundGradient = "linear-gradient(180deg, rgba(10, 4, 28, 0.96) 0%, rgba(22, 6, 46, 0.96) 50%, rgba(6, 14, 30, 0.98) 100%)",
                    FontFamilyHeading = "Orbitron",
                    FontFamilyBody = "Rajdhani",
                    HeaderFontSizePx = 60,
                    SubtitleFontSizePx = 26,
                    SectionTitleFontSizePx = 34,
                    EntryFontSizePx = 26,
                    DetailFontSizePx = 18,
                    FooterFontSizePx = 40,
                    TextColor = "#ECFEFF",
                    SecondaryTextColor = "#A5F3FC",
                    AccentColor = "#00F5D4",
                    HeaderColor = "#FF2A85",
                    SectionTitleColor = "#00F5D4",
                    TextShadow = "0 0 12px rgba(0, 245, 212, 0.55)",
                    TextAlign = "center",
                    ContainerMaxWidthPx = 980,
                    SectionGapPx = 54,
                    EntryGapPx = 14,
                    EdgeFadeHeightPct = 12,
                    ShowAvatars = true,
                    AvatarSizePx = 40,
                    AvatarShape = "rounded",
                    AvatarBorderWidthPx = 2,
                    AvatarBorderColor = "#FF2A85",
                    AvatarFallbackMode = CreditAvatarFallbacks.ColoredSilhouette,
                    CustomCss = """
                        .credits-header-title {
                            text-shadow: 0 0 18px rgba(255, 42, 133, 0.8), 0 0 36px rgba(255, 42, 133, 0.35);
                            text-transform: uppercase;
                            letter-spacing: 0.1em;
                        }
                        .credits-section-header {
                            border-bottom: 1px solid rgba(0, 245, 212, 0.45);
                            padding-bottom: 8px;
                            margin-bottom: 18px;
                        }
                        """
                }),

            new(
                "cozy-frosted",
                "Cozy Frosted Glass",
                "Warm frosted-glass panels with soft rounded avatars designed to float over cams or ending art.",
                new CreditsThemeConfig
                {
                    AnimationMode = CreditAnimationModes.SmoothScroll,
                    DefaultLayout = CreditLayouts.AvatarCards,
                    BackgroundType = CreditBackgroundTypes.Transparent,
                    BackgroundColor = "transparent",
                    BackgroundGradient = "none",
                    FontFamilyHeading = "Nunito",
                    FontFamilyBody = "Nunito",
                    HeaderFontSizePx = 58,
                    SubtitleFontSizePx = 24,
                    SectionTitleFontSizePx = 30,
                    EntryFontSizePx = 22,
                    DetailFontSizePx = 16,
                    FooterFontSizePx = 36,
                    TextColor = "#FFFDF9",
                    SecondaryTextColor = "#FDE68A",
                    AccentColor = "#FDA4AF",
                    HeaderColor = "#FFF1F2",
                    SectionTitleColor = "#FDE68A",
                    TextShadow = "0 2px 8px rgba(15, 10, 25, 0.7)",
                    TextAlign = "center",
                    ContainerMaxWidthPx = 1040,
                    SectionGapPx = 44,
                    EntryGapPx = 12,
                    EdgeFadeHeightPct = 10,
                    ShowAvatars = true,
                    AvatarSizePx = 42,
                    AvatarShape = "rounded",
                    AvatarBorderWidthPx = 2,
                    AvatarBorderColor = "#FDA4AF",
                    AvatarFallbackMode = CreditAvatarFallbacks.ColoredSilhouette,
                    CustomCss = """
                        .credits-section {
                            background: rgba(24, 18, 38, 0.56);
                            backdrop-filter: blur(12px);
                            -webkit-backdrop-filter: blur(12px);
                            border: 1px solid rgba(255, 255, 255, 0.16);
                            border-radius: 20px;
                            padding: 24px 28px;
                            box-shadow: 0 12px 32px rgba(0, 0, 0, 0.35);
                        }
                        """
                }),

            new(
                "retro-arcade",
                "Retro Arcade",
                "High-score arcade terminal aesthetic with monospace typography, vibrant neon yellow/cyan, and scanlines.",
                new CreditsThemeConfig
                {
                    AnimationMode = CreditAnimationModes.SmoothScroll,
                    DefaultLayout = CreditLayouts.SingleColumn,
                    BackgroundType = CreditBackgroundTypes.Solid,
                    BackgroundColor = "#060812",
                    BackgroundGradient = "#060812",
                    FontFamilyHeading = "Press Start 2P",
                    FontFamilyBody = "Share Tech Mono",
                    HeaderFontSizePx = 42,
                    SubtitleFontSizePx = 22,
                    SectionTitleFontSizePx = 24,
                    EntryFontSizePx = 26,
                    DetailFontSizePx = 18,
                    FooterFontSizePx = 28,
                    TextColor = "#E2E8F0",
                    SecondaryTextColor = "#38BDF8",
                    AccentColor = "#FACC15",
                    HeaderColor = "#FACC15",
                    SectionTitleColor = "#22D3EE",
                    TextShadow = "3px 3px 0 #1E1B4B",
                    TextAlign = "center",
                    ContainerMaxWidthPx = 920,
                    SectionGapPx = 56,
                    EntryGapPx = 14,
                    EdgeFadeHeightPct = 10,
                    ShowAvatars = true,
                    AvatarSizePx = 36,
                    AvatarShape = "square",
                    AvatarBorderWidthPx = 2,
                    AvatarBorderColor = "#FACC15",
                    AvatarFallbackMode = CreditAvatarFallbacks.ColoredSilhouette,
                    CustomCss = """
                        .credits-backdrop::after {
                            content: "";
                            position: absolute;
                            inset: 0;
                            background: repeating-linear-gradient(
                                to bottom,
                                rgba(255, 255, 255, 0.03) 0px,
                                rgba(255, 255, 255, 0.03) 1px,
                                transparent 1px,
                                transparent 4px
                            );
                            pointer-events: none;
                        }
                        """
                }),

            new(
                "minimal-transparent",
                "Minimal Transparent",
                "Transparent overlay with bold high-contrast text and avatars ready to layer over any OBS ending scene.",
                new CreditsThemeConfig
                {
                    AnimationMode = CreditAnimationModes.SmoothScroll,
                    DefaultLayout = CreditLayouts.SingleColumn,
                    BackgroundType = CreditBackgroundTypes.Transparent,
                    BackgroundColor = "transparent",
                    BackgroundGradient = "none",
                    FontFamilyHeading = "Montserrat",
                    FontFamilyBody = "Montserrat",
                    HeaderFontSizePx = 64,
                    SubtitleFontSizePx = 26,
                    SectionTitleFontSizePx = 34,
                    EntryFontSizePx = 26,
                    DetailFontSizePx = 18,
                    FooterFontSizePx = 40,
                    TextColor = "#FFFFFF",
                    SecondaryTextColor = "#E2E8F0",
                    AccentColor = "#FFFFFF",
                    HeaderColor = "#FFFFFF",
                    SectionTitleColor = "#FFFFFF",
                    TextShadow = "0 2px 6px rgba(0, 0, 0, 0.95), 0 0 16px rgba(0, 0, 0, 0.85)",
                    TextAlign = "center",
                    ContainerMaxWidthPx = 900,
                    SectionGapPx = 48,
                    EntryGapPx = 12,
                    EdgeFadeHeightPct = 12,
                    ShowAvatars = true,
                    AvatarSizePx = 38,
                    AvatarShape = "circle",
                    AvatarBorderWidthPx = 2,
                    AvatarBorderColor = "rgba(255, 255, 255, 0.8)",
                    AvatarFallbackMode = CreditAvatarFallbacks.ColoredSilhouette,
                    CustomCss = string.Empty
                })
        ];

        public static CreditsProfileConfig CreateDefaultProfileConfig() => new()
        {
            TimeWindow = new CreditsTimeWindowConfig
            {
                Mode = CreditTimeWindowModes.SinceStreamStart,
                PreStreamGraceMinutes = 15,
                LookbackValue = 6,
                LookbackUnit = CreditLookbackUnits.Hours
            },
            Header = new CreditsHeaderConfig
            {
                Enabled = true,
                Title = "Credits",
                Subtitle = "Thank you for the Support!",
                ImageMaxHeightPx = 160,
                ShowBroadcasterAvatar = false,
                IntroDelaySeconds = 0.5
            },
            Footer = new CreditsFooterConfig
            {
                Enabled = true,
                Title = "Thank you for watching!",
                Lines = ["See you next stream!"],
                ImageMaxHeightPx = 140,
                HoldSeconds = 4.0
            },
            Playback = new CreditsPlaybackConfig
            {
                AnimationMode = CreditAnimationModes.InheritTheme,
                SpeedMode = CreditSpeedModes.FixedSpeed,
                ScrollSpeedPxPerSec = 75,
                TargetDurationSeconds = 60,
                PageDurationSeconds = 6,
                StartBehavior = CreditStartBehaviors.WaitForTrigger,
                EndBehavior = CreditEndBehaviors.FadeOutAndHide,
                TestModeDefault = CreditSampleModes.Off
            },
            Exclusions = new CreditsExclusionConfig
            {
                ExcludeKnownBots = true,
                ExcludeBroadcaster = true,
                ExcludeBotAccount = true,
                IncludeAnonymous = true,
                AnonymousLabel = "Anonymous"
            },
            Media = new CreditsMediaOverrideConfig(),
            Sections =
            [
                new CreditsSectionConfig
                {
                    Id = "sec-subscribers",
                    Enabled = true,
                    Title = "Subscribers",
                    SourceTypes = [CreditSectionSources.NewSubscribers, CreditSectionSources.Resubscribers],
                    HideWhenEmpty = true,
                    ShowAmount = true,
                    EntryFormatTemplate = "{name} x{amount}",
                    AggregationMode = CreditAggregationModes.CombineByUserSum,
                    SortMode = CreditSortModes.HighestAmount
                },
                new CreditsSectionConfig
                {
                    Id = "sec-sub-gifters",
                    Enabled = true,
                    Title = "Sub Gifters",
                    SourceTypes = [CreditSectionSources.SubGifters],
                    HideWhenEmpty = true,
                    ShowAmount = true,
                    EntryFormatTemplate = "{name} x{amount}",
                    AggregationMode = CreditAggregationModes.CombineByUserSum,
                    SortMode = CreditSortModes.HighestAmount
                },
                new CreditsSectionConfig
                {
                    Id = "sec-gifted-recipients",
                    Enabled = false,
                    Title = "Gifted Sub Recipients",
                    SourceTypes = [CreditSectionSources.GiftedSubRecipients],
                    HideWhenEmpty = true,
                    ShowAmount = false,
                    EntryFormatTemplate = "{name}",
                    AggregationMode = CreditAggregationModes.UniqueUsersLatest,
                    SortMode = CreditSortModes.MostRecent
                },
                new CreditsSectionConfig
                {
                    Id = "sec-bits",
                    Enabled = true,
                    Title = "Bits & Cheers",
                    SourceTypes = [CreditSectionSources.CheersAndBits, CreditSectionSources.BitsPowerUps],
                    HideWhenEmpty = true,
                    ShowAmount = true,
                    EntryFormatTemplate = "{name} — {amount} bits",
                    AggregationMode = CreditAggregationModes.CombineByUserSum,
                    SortMode = CreditSortModes.HighestAmount
                },
                new CreditsSectionConfig
                {
                    Id = "sec-raids",
                    Enabled = true,
                    Title = "Raids",
                    SourceTypes = [CreditSectionSources.IncomingRaids],
                    HideWhenEmpty = true,
                    ShowAmount = true,
                    EntryFormatTemplate = "{name} x{amount}",
                    AggregationMode = CreditAggregationModes.CombineByUserSum,
                    SortMode = CreditSortModes.HighestAmount
                },
                new CreditsSectionConfig
                {
                    Id = "sec-followers",
                    Enabled = true,
                    Title = "New Followers",
                    SourceTypes = [CreditSectionSources.NewFollowers],
                    HideWhenEmpty = true,
                    ShowAmount = false,
                    EntryFormatTemplate = "{name}",
                    AggregationMode = CreditAggregationModes.UniqueUsersLatest,
                    SortMode = CreditSortModes.MostRecent
                },
                new CreditsSectionConfig
                {
                    Id = "sec-redeems",
                    Enabled = true,
                    Title = "Channel Point Redemptions",
                    SourceTypes = [CreditSectionSources.ChannelPointRedemptions],
                    HideWhenEmpty = true,
                    ShowAmount = false,
                    EntryFormatTemplate = "{name} — {detail}",
                    AggregationMode = CreditAggregationModes.UniqueUsersLatest,
                    SortMode = CreditSortModes.MostRecent
                },
                new CreditsSectionConfig
                {
                    Id = "sec-milestones",
                    Enabled = true,
                    Title = "Charity & Watch Streaks",
                    SourceTypes = [CreditSectionSources.CharityDonations, CreditSectionSources.WatchStreaks, CreditSectionSources.SubUpgrades],
                    HideWhenEmpty = true,
                    ShowAmount = true,
                    EntryFormatTemplate = "{name} — {detail}",
                    AggregationMode = CreditAggregationModes.IndividualEvents,
                    SortMode = CreditSortModes.HighestAmount
                },
                new CreditsSectionConfig
                {
                    Id = "sec-mods-vips",
                    Enabled = true,
                    Title = "Moderators & VIPs",
                    SourceTypes = [CreditSectionSources.Moderators, CreditSectionSources.Vips],
                    HideWhenEmpty = true,
                    LayoutOverride = CreditLayouts.InlinePills,
                    ShowAmount = false,
                    EntryFormatTemplate = "{name}",
                    AggregationMode = CreditAggregationModes.UniqueUsersLatest,
                    SortMode = CreditSortModes.Alphabetical
                },
                new CreditsSectionConfig
                {
                    Id = "sec-chatters",
                    Enabled = true,
                    Title = "Chatters",
                    SourceTypes = [CreditSectionSources.ActiveChatters],
                    HideWhenEmpty = true,
                    LayoutOverride = CreditLayouts.InlinePills,
                    ShowAmount = false,
                    EntryFormatTemplate = "{name}",
                    AggregationMode = CreditAggregationModes.CombineByUserSum,
                    SortMode = CreditSortModes.HighestAmount
                },
                new CreditsSectionConfig
                {
                    Id = "sec-giveaways",
                    Enabled = false,
                    Title = "Giveaway Winners",
                    SourceTypes = [CreditSectionSources.GiveawayWinners],
                    HideWhenEmpty = true,
                    ShowAmount = false,
                    EntryFormatTemplate = "{name} — {detail}",
                    AggregationMode = CreditAggregationModes.IndividualEvents,
                    SortMode = CreditSortModes.MostRecent
                },
                new CreditsSectionConfig
                {
                    Id = "sec-fishing",
                    Enabled = false,
                    Title = "Top Anglers",
                    SourceTypes = [CreditSectionSources.FishingTopAnglers],
                    HideWhenEmpty = true,
                    ShowAmount = true,
                    EntryFormatTemplate = "{name} — {amount}g",
                    AggregationMode = CreditAggregationModes.CombineByUserSum,
                    SortMode = CreditSortModes.HighestAmount,
                    MaxEntries = 10,
                    FishingRankBy = "Gold"
                },
                new CreditsSectionConfig
                {
                    Id = "sec-dropgames",
                    Enabled = false,
                    Title = "Minigame Winners",
                    SourceTypes = [CreditSectionSources.DropGameWinners],
                    HideWhenEmpty = true,
                    ShowAmount = true,
                    EntryFormatTemplate = "{name} ({detail})",
                    AggregationMode = CreditAggregationModes.UniqueUsersHighest,
                    SortMode = CreditSortModes.HighestAmount
                },
                new CreditsSectionConfig
                {
                    Id = "sec-songs",
                    Enabled = false,
                    Title = "Song Requesters",
                    SourceTypes = [CreditSectionSources.SongRequesters],
                    HideWhenEmpty = true,
                    ShowAmount = true,
                    EntryFormatTemplate = "{name} x{amount}",
                    AggregationMode = CreditAggregationModes.CombineByUserSum,
                    SortMode = CreditSortModes.HighestAmount
                },
                new CreditsSectionConfig
                {
                    Id = "sec-special-thanks",
                    Enabled = false,
                    Title = "Special Thanks",
                    SourceTypes = [CreditSectionSources.CustomStatic],
                    HideWhenEmpty = true,
                    ShowAmount = false,
                    EntryFormatTemplate = "{name} — {detail}",
                    StaticEntries =
                    [
                        new CreditsStaticEntry { Name = "Community", Role = "For hanging out!" }
                    ]
                }
            ]
        };
    }
}
