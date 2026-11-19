using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Bot.Commands.Moderation;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Core;
using PenguinTwitchBot.Database.Bot.Models.Credits;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Overlay.Credits
{
    public sealed class CreditsDataBuilder(
        IServiceScopeFactory scopeFactory,
        ICreditsEventCollector eventCollector,
        CreditsTimeWindowResolver timeWindowResolver,
        CreditsAvatarResolver avatarResolver,
        IServiceBackbone serviceBackbone,
        IKnownBots knownBots,
        ITwitchService twitchService)
    {
        private static readonly string[] FakeSampleUsers =
        [
            "TheNickNZ", "CheckYorSix", "TexasHornyToad", "N8tronic", "Kerzan01",
            "ProtonSter", "NebulaPilot", "PixelPenguin", "StarlightNova", "ValkyrieAce",
            "CobaltFox", "LunarEcho", "ArcadeWizard", "FrostByte99", "AuroraDrift"
        ];

        public async Task<CreditsRenderPayload> BuildPayloadAsync(
            CreditProfile profile,
            CreditTheme? theme,
            string? overrideSampleMode = null,
            string? overrideWindowMode = null,
            int? overrideLookbackMinutes = null,
            CancellationToken cancellationToken = default)
        {
            var profileConfig = DeserializeProfileConfig(profile.ConfigJson);
            var themeConfig = theme != null ? DeserializeThemeConfig(theme.ThemeJson) : new CreditsThemeConfig();

            var sampleMode = NormalizeSampleMode(
                !string.IsNullOrWhiteSpace(overrideSampleMode)
                    ? overrideSampleMode
                    : profileConfig.Playback.TestModeDefault);

            // Resolve effective animation mode
            var effectivePlayback = ClonePlayback(profileConfig.Playback);
            if (string.IsNullOrWhiteSpace(effectivePlayback.AnimationMode) ||
                effectivePlayback.AnimationMode.Equals(CreditAnimationModes.InheritTheme, StringComparison.OrdinalIgnoreCase))
            {
                effectivePlayback.AnimationMode = themeConfig.AnimationMode;
            }

            // Flush any buffered chatters before querying live events
            if (sampleMode == CreditSampleModes.Off)
            {
                await eventCollector.FlushPendingChattersAsync(cancellationToken);
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var window = await timeWindowResolver.ResolveAsync(
                db,
                profileConfig.TimeWindow,
                overrideWindowMode,
                overrideLookbackMinutes,
                cancellationToken);

            var renderedSections = sampleMode switch
            {
                CreditSampleModes.Fake => BuildSampleSections(profileConfig, themeConfig, sampleUsers: null),
                CreditSampleModes.DatabaseViewers => await BuildDatabaseSampleSectionsAsync(db, profileConfig, themeConfig, cancellationToken),
                _ => await BuildLiveSectionsAsync(db, profileConfig, themeConfig, window, cancellationToken)
            };

            // Batch-resolve avatars for all entries + optional broadcaster avatar (skip Twitch lookup for fake sample names)
            await PopulateAvatarsAsync(renderedSections, profileConfig.Header, skipTwitchLookup: sampleMode == CreditSampleModes.Fake, cancellationToken);

            var totalEntries = renderedSections.Sum(s => s.Entries.Count);
            var broadcasterName = string.IsNullOrWhiteSpace(serviceBackbone.BroadcasterName)
                ? "Streamer"
                : serviceBackbone.BroadcasterName;

            var sessionDuration = window.EndUtc > window.StartUtc
                ? window.EndUtc - window.StartUtc
                : TimeSpan.Zero;

            var tokenMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["broadcaster"] = broadcasterName,
                ["streamTitle"] = window.Session?.Title ?? string.Empty,
                ["game"] = window.Session?.GameName ?? string.Empty,
                ["date"] = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["streamDuration"] = FormatDuration(sessionDuration),
                ["totalEntries"] = totalEntries.ToString(CultureInfo.InvariantCulture)
            };

            string? broadcasterAvatar = null;
            if (profileConfig.Header.ShowBroadcasterAvatar)
            {
                try { broadcasterAvatar = await twitchService.GetBroadcasterProfileImageUrl(); }
                catch { /* Non-fatal */ }
            }

            var renderedHeader = new CreditsRenderedHeader
            {
                Enabled = profileConfig.Header.Enabled,
                Title = ReplaceHeaderFooterTokens(profileConfig.Header.Title, tokenMap),
                Subtitle = ReplaceHeaderFooterTokens(profileConfig.Header.Subtitle, tokenMap),
                ImageUrl = profileConfig.Header.ImageUrl,
                ImageMaxHeightPx = profileConfig.Header.ImageMaxHeightPx,
                BroadcasterAvatarUrl = broadcasterAvatar,
                IntroDelaySeconds = profileConfig.Header.IntroDelaySeconds
            };

            var renderedFooter = new CreditsRenderedFooter
            {
                Enabled = profileConfig.Footer.Enabled,
                Title = ReplaceHeaderFooterTokens(profileConfig.Footer.Title, tokenMap),
                Lines = profileConfig.Footer.Lines
                    .Select(line => ReplaceHeaderFooterTokens(line, tokenMap))
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList(),
                ImageUrl = profileConfig.Footer.ImageUrl,
                ImageMaxHeightPx = profileConfig.Footer.ImageMaxHeightPx,
                HoldSeconds = profileConfig.Footer.HoldSeconds
            };

            return new CreditsRenderPayload
            {
                ProfileName = profile.Name,
                ThemeName = theme?.Name ?? "Classic Cinema",
                GeneratedAtUtc = DateTime.UtcNow,
                WindowStartUtc = window.StartUtc,
                WindowEndUtc = window.EndUtc,
                SampleMode = sampleMode,
                TotalEntries = totalEntries,
                Header = renderedHeader,
                Footer = renderedFooter,
                Playback = effectivePlayback,
                Theme = themeConfig,
                Media = profileConfig.Media,
                ProfileCustomCss = profileConfig.CustomCssOverride ?? string.Empty,
                Sections = renderedSections
            };
        }

        private async Task<List<CreditsRenderedSection>> BuildLiveSectionsAsync(
            IUnitOfWork db,
            CreditsProfileConfig profileConfig,
            CreditsThemeConfig themeConfig,
            ResolvedCreditsWindow window,
            CancellationToken cancellationToken)
        {
            var enabledSections = profileConfig.Sections.Where(s => s.Enabled).ToList();
            if (enabledSections.Count == 0) return [];

            var neededSources = enabledSections
                .SelectMany(s => s.SourceTypes)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var events = await db.CreditEvents
                .Find(e => !e.IsHidden && e.OccurredAtUtc >= window.StartUtc && e.OccurredAtUtc <= window.EndUtc)
                .OrderByDescending(e => e.OccurredAtUtc)
                .ToListAsync(cancellationToken);

            List<GiveawayWinnerRow> giveawayWinners = [];
            if (neededSources.Contains(CreditSectionSources.GiveawayWinners))
            {
                giveawayWinners = await db.GiveawayWinners
                    .Find(w => w.WinningDate >= window.StartUtc && w.WinningDate <= window.EndUtc)
                    .OrderByDescending(w => w.WinningDate)
                    .Select(w => new GiveawayWinnerRow(w.Username, w.Prize, w.WinningDate))
                    .ToListAsync(cancellationToken);
            }

            List<FishCatchRow> fishCatches = [];
            if (neededSources.Contains(CreditSectionSources.FishingTopAnglers))
            {
                fishCatches = await db.FishCatches
                    .Find(c => c.CaughtAt >= window.StartUtc && c.CaughtAt <= window.EndUtc)
                    .Select(c => new FishCatchRow(c.UserId, c.Username, c.GoldEarned, c.Weight, c.CaughtAt))
                    .ToListAsync(cancellationToken);
            }

            List<ViewerRoleRow> channelRoleViewers = [];
            if (enabledSections.Any(s =>
                    s.IncludeAllChannelRoleMembers &&
                    (s.SourceTypes.Contains(CreditSectionSources.Moderators, StringComparer.OrdinalIgnoreCase) ||
                     s.SourceTypes.Contains(CreditSectionSources.Vips, StringComparer.OrdinalIgnoreCase))))
            {
                channelRoleViewers = await db.Viewers
                    .Find(v => v.isMod || v.isVip)
                    .Select(v => new ViewerRoleRow(v.UserId, v.Username, v.DisplayName, v.isMod, v.isVip, v.LastSeen))
                    .ToListAsync(cancellationToken);
            }

            var exclusionContext = BuildExclusionContext(profileConfig.Exclusions);
            var renderedSections = new List<CreditsRenderedSection>();

            foreach (var section in enabledSections)
            {
                var candidates = new List<CandidateEntry>();

                foreach (var source in section.SourceTypes.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    CollectCandidatesForSource(
                        source,
                        section,
                        events,
                        giveawayWinners,
                        fishCatches,
                        channelRoleViewers,
                        exclusionContext,
                        candidates);
                }

                var processed = ProcessSectionCandidates(section, candidates);

                if (processed.Count == 0 && section.HideWhenEmpty)
                {
                    continue;
                }

                renderedSections.Add(CreateRenderedSection(section, themeConfig, processed));
            }

            return renderedSections;
        }

        private async Task<List<CreditsRenderedSection>> BuildDatabaseSampleSectionsAsync(
            IUnitOfWork db,
            CreditsProfileConfig profileConfig,
            CreditsThemeConfig themeConfig,
            CancellationToken cancellationToken)
        {
            var exclusionContext = BuildExclusionContext(profileConfig.Exclusions);
            var viewers = await db.Viewers
                .Query()
                .OrderByDescending(v => v.LastSeen)
                .Take(100)
                .ToListAsync(cancellationToken);

            var filteredViewers = viewers
                .Where(v => !string.IsNullOrWhiteSpace(v.Username) && !IsExcludedUser(v.Username, false, exclusionContext))
                .Select(v => new SampleUserSeed(
                    v.UserId,
                    v.Username,
                    string.IsNullOrWhiteSpace(v.DisplayName) ? v.Username : v.DisplayName))
                .ToList();

            return BuildSampleSections(profileConfig, themeConfig, filteredViewers.Count > 0 ? filteredViewers : null);
        }

        private static List<CreditsRenderedSection> BuildSampleSections(
            CreditsProfileConfig profileConfig,
            CreditsThemeConfig themeConfig,
            List<SampleUserSeed>? sampleUsers)
        {
            var seeds = sampleUsers is { Count: > 0 }
                ? sampleUsers
                : FakeSampleUsers.Select(name => new SampleUserSeed(string.Empty, name.ToLowerInvariant(), name)).ToList();

            var renderedSections = new List<CreditsRenderedSection>();
            var cursor = 0;

            foreach (var section in profileConfig.Sections.Where(s => s.Enabled))
            {
                var entries = new List<CreditsRenderedEntry>();

                if (section.SourceTypes.Contains(CreditSectionSources.CustomStatic, StringComparer.OrdinalIgnoreCase) &&
                    section.StaticEntries.Count > 0)
                {
                    foreach (var st in section.StaticEntries.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
                    {
                        var formatted = FormatEntryText(section, st.Name, 1, st.Role, null);
                        entries.Add(new CreditsRenderedEntry
                        {
                            DisplayName = st.Name,
                            Username = st.TwitchUsername ?? string.Empty,
                            AvatarUrl = st.ImageUrl,
                            AvatarColor = CreditsAvatarResolver.GetDeterministicColor(st.Name),
                            FormattedText = formatted,
                            Detail = st.Role,
                            Amount = 1,
                            Role = st.Role,
                            EventType = CreditEventTypes.Manual
                        });
                    }
                }

                var nonStaticSources = section.SourceTypes
                    .Where(src => !src.Equals(CreditSectionSources.CustomStatic, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (nonStaticSources.Count > 0)
                {
                    var sampleCount = Math.Min(
                        section.MaxEntries > 0 ? section.MaxEntries : 4,
                        Math.Max(2, Math.Min(5, seeds.Count)));

                    for (var i = 0; i < sampleCount; i++)
                    {
                        var seed = seeds[(cursor + i) % seeds.Count];
                        var source = nonStaticSources[i % nonStaticSources.Count];
                        var (amount, detail, tier) = CreateSampleMetric(source, i);

                        if (section.MinAmount.HasValue && amount < section.MinAmount.Value)
                        {
                            amount = section.MinAmount.Value;
                        }

                        entries.Add(new CreditsRenderedEntry
                        {
                            DisplayName = seed.DisplayName,
                            Username = seed.Username,
                            AvatarUrl = null,
                            AvatarColor = CreditsAvatarResolver.GetDeterministicColor(!string.IsNullOrEmpty(seed.UserId) ? seed.UserId : seed.Username),
                            FormattedText = FormatEntryText(section, seed.DisplayName, amount, detail, tier),
                            Detail = detail,
                            Amount = amount,
                            EventType = source,
                            InternalUserId = seed.UserId
                        });
                    }
                    cursor = (cursor + sampleCount) % seeds.Count;
                }

                if (entries.Count == 0 && section.HideWhenEmpty)
                    continue;

                renderedSections.Add(CreateRenderedSection(section, themeConfig, entries));
            }

            return renderedSections;
        }

        private void CollectCandidatesForSource(
            string source,
            CreditsSectionConfig section,
            List<CreditEvent> events,
            List<GiveawayWinnerRow> giveawayWinners,
            List<FishCatchRow> fishCatches,
            List<ViewerRoleRow> channelRoleViewers,
            ExclusionContext exclusions,
            List<CandidateEntry> target)
        {
            switch (source)
            {
                case CreditSectionSources.NewSubscribers:
                    AddFilteredCreditEvents(events, CreditEventTypes.Subscribe, section, exclusions, target);
                    break;
                case CreditSectionSources.Resubscribers:
                    AddFilteredCreditEvents(events, CreditEventTypes.Resubscribe, section, exclusions, target);
                    break;
                case CreditSectionSources.SubGifters:
                    AddFilteredCreditEvents(events, CreditEventTypes.SubGift, section, exclusions, target);
                    break;
                case CreditSectionSources.GiftedSubRecipients:
                    AddFilteredCreditEvents(events, CreditEventTypes.GiftedSubRecipient, section, exclusions, target);
                    break;
                case CreditSectionSources.SubUpgrades:
                    AddFilteredCreditEvents(events, CreditEventTypes.SubUpgrade, section, exclusions, target);
                    AddFilteredCreditEvents(events, CreditEventTypes.PayItForward, section, exclusions, target);
                    break;
                case CreditSectionSources.NewFollowers:
                    AddFilteredCreditEvents(events, CreditEventTypes.Follow, section, exclusions, target);
                    break;
                case CreditSectionSources.CheersAndBits:
                    AddFilteredCreditEvents(events, CreditEventTypes.Cheer, section, exclusions, target);
                    break;
                case CreditSectionSources.BitsPowerUps:
                    AddFilteredCreditEvents(events, CreditEventTypes.BitsPowerUp, section, exclusions, target);
                    break;
                case CreditSectionSources.IncomingRaids:
                    AddFilteredCreditEvents(events, CreditEventTypes.IncomingRaid, section, exclusions, target);
                    break;
                case CreditSectionSources.CharityDonations:
                    AddFilteredCreditEvents(events, CreditEventTypes.CharityDonation, section, exclusions, target);
                    break;
                case CreditSectionSources.BitsBadgeMilestones:
                    AddFilteredCreditEvents(events, CreditEventTypes.BitsBadgeTier, section, exclusions, target);
                    break;
                case CreditSectionSources.ChannelPointRedemptions:
                    AddFilteredCreditEvents(events, CreditEventTypes.ChannelPointRedeem, section, exclusions, target);
                    break;
                case CreditSectionSources.WatchStreaks:
                    AddFilteredCreditEvents(events, CreditEventTypes.WatchStreak, section, exclusions, target);
                    break;
                case CreditSectionSources.ActiveChatters:
                    AddFilteredCreditEvents(events, CreditEventTypes.Chatter, section, exclusions, target);
                    break;
                case CreditSectionSources.DropGameWinners:
                    AddFilteredCreditEvents(events, CreditEventTypes.DropGameWin, section, exclusions, target);
                    break;
                case CreditSectionSources.SongRequesters:
                    AddFilteredCreditEvents(events, CreditEventTypes.SongRequest, section, exclusions, target);
                    break;
                case CreditSectionSources.ManualActionEntries:
                    AddFilteredCreditEvents(events, CreditEventTypes.Manual, section, exclusions, target);
                    break;

                case CreditSectionSources.Moderators:
                case CreditSectionSources.Vips:
                {
                    var isModTarget = source == CreditSectionSources.Moderators;
                    foreach (var ev in events.Where(e => e.EventType == CreditEventTypes.Chatter))
                    {
                        if (!MatchesDetailFilter(ev.Detail, section.DetailFilter)) continue;
                        if (IsExcludedUser(ev.Username, ev.IsAnonymous, exclusions)) continue;
                        var (isMod, isVip) = ParseChatterRoleFlags(ev.MetadataJson);
                        if ((isModTarget && isMod) || (!isModTarget && isVip))
                        {
                            target.Add(FromCreditEvent(ev));
                        }
                    }

                    if (section.IncludeAllChannelRoleMembers)
                    {
                        foreach (var viewer in channelRoleViewers)
                        {
                            if ((isModTarget && !viewer.IsMod) || (!isModTarget && !viewer.IsVip))
                                continue;
                            if (IsExcludedUser(viewer.Username, false, exclusions))
                                continue;

                            target.Add(new CandidateEntry(
                                viewer.UserId,
                                UsernameNormalizer.Normalize(viewer.Username),
                                string.IsNullOrWhiteSpace(viewer.DisplayName) ? viewer.Username : viewer.DisplayName,
                                null,
                                1,
                                isModTarget ? "Moderator" : "VIP",
                                null,
                                viewer.LastSeen,
                                source,
                                false,
                                null));
                        }
                    }
                    break;
                }

                case CreditSectionSources.TopSupporters:
                {
                    foreach (var ev in events)
                    {
                        if (IsExcludedUser(ev.Username, ev.IsAnonymous, exclusions)) continue;
                        var score = ev.EventType switch
                        {
                            CreditEventTypes.Subscribe => 5m,
                            CreditEventTypes.Resubscribe => 5m,
                            CreditEventTypes.SubGift => Math.Max(1m, ev.Amount) * 5m,
                            CreditEventTypes.Cheer or CreditEventTypes.BitsPowerUp => Math.Round(ev.Amount / 100m, 2),
                            CreditEventTypes.IncomingRaid => Math.Max(1m, Math.Round(ev.Amount / 5m, 2)),
                            CreditEventTypes.CharityDonation => ev.Amount * 5m,
                            CreditEventTypes.SubUpgrade or CreditEventTypes.PayItForward => 3m,
                            _ => 0m
                        };

                        if (score <= 0) continue;
                        if (!MatchesDetailFilter(ev.Detail, section.DetailFilter)) continue;

                        target.Add(new CandidateEntry(
                            ev.UserId,
                            ev.Username,
                            ResolveDisplayName(ev, exclusions),
                            ev.AvatarUrl,
                            score,
                            ev.Detail,
                            ev.Tier,
                            ev.OccurredAtUtc,
                            ev.EventType,
                            ev.IsAnonymous,
                            null));
                    }
                    break;
                }

                case CreditSectionSources.GiveawayWinners:
                {
                    foreach (var w in giveawayWinners)
                    {
                        var norm = UsernameNormalizer.Normalize(w.Username);
                        if (IsExcludedUser(norm, false, exclusions)) continue;
                        if (!MatchesDetailFilter(w.Prize, section.DetailFilter)) continue;

                        target.Add(new CandidateEntry(
                            string.Empty,
                            norm,
                            w.Username,
                            null,
                            1,
                            string.IsNullOrWhiteSpace(w.Prize) ? "Winner" : w.Prize,
                            null,
                            w.WinningDate,
                            CreditSectionSources.GiveawayWinners,
                            false,
                            null));
                    }
                    break;
                }

                case CreditSectionSources.FishingTopAnglers:
                {
                    foreach (var fc in fishCatches)
                    {
                        var norm = UsernameNormalizer.Normalize(fc.Username);
                        if (IsExcludedUser(norm, false, exclusions)) continue;

                        var metric = section.FishingRankBy switch
                        {
                            "MaxWeight" => (decimal)Math.Round(fc.Weight, 2),
                            "CatchCount" => 1m,
                            _ => fc.GoldEarned
                        };

                        target.Add(new CandidateEntry(
                            fc.UserId,
                            norm,
                            fc.Username,
                            null,
                            metric,
                            section.FishingRankBy switch
                            {
                                "MaxWeight" => $"{fc.Weight:0.##} lbs",
                                "CatchCount" => "Catches",
                                _ => $"{fc.GoldEarned} gold"
                            },
                            null,
                            fc.CaughtAt,
                            CreditSectionSources.FishingTopAnglers,
                            false,
                            null));
                    }
                    break;
                }

                case CreditSectionSources.CustomStatic:
                {
                    foreach (var st in section.StaticEntries.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
                    {
                        if (!MatchesDetailFilter(st.Role, section.DetailFilter)) continue;
                        var login = UsernameNormalizer.Normalize(st.TwitchUsername ?? string.Empty);
                        target.Add(new CandidateEntry(
                            string.Empty,
                            login,
                            st.Name.Trim(),
                            string.IsNullOrWhiteSpace(st.ImageUrl) ? null : st.ImageUrl.Trim(),
                            1,
                            st.Role,
                            null,
                            DateTime.UtcNow,
                            CreditSectionSources.CustomStatic,
                            false,
                            st.Role));
                    }
                    break;
                }
            }
        }

        private void AddFilteredCreditEvents(
            List<CreditEvent> events,
            string targetEventType,
            CreditsSectionConfig section,
            ExclusionContext exclusions,
            List<CandidateEntry> target)
        {
            foreach (var ev in events)
            {
                if (!ev.EventType.Equals(targetEventType, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (IsExcludedUser(ev.Username, ev.IsAnonymous, exclusions))
                    continue;
                if (!MatchesDetailFilter(ev.Detail, section.DetailFilter))
                    continue;

                target.Add(new CandidateEntry(
                    ev.UserId,
                    ev.Username,
                    ResolveDisplayName(ev, exclusions),
                    ev.AvatarUrl,
                    ev.Amount,
                    ev.Detail,
                    ev.Tier,
                    ev.OccurredAtUtc,
                    ev.EventType,
                    ev.IsAnonymous,
                    null));
            }
        }

        private static CandidateEntry FromCreditEvent(CreditEvent ev) => new(
            ev.UserId,
            ev.Username,
            string.IsNullOrWhiteSpace(ev.DisplayName) ? ev.Username : ev.DisplayName,
            ev.AvatarUrl,
            ev.Amount,
            ev.Detail,
            ev.Tier,
            ev.OccurredAtUtc,
            ev.EventType,
            ev.IsAnonymous,
            null);

        private static List<CreditsRenderedEntry> ProcessSectionCandidates(
            CreditsSectionConfig section,
            List<CandidateEntry> candidates)
        {
            if (candidates.Count == 0) return [];

            // Never merge explicit CustomStatic rows with dynamic events unintentionally unless keyed uniquely
            IEnumerable<CandidateEntry> aggregated;
            if (section.AggregationMode == CreditAggregationModes.IndividualEvents)
            {
                aggregated = candidates;
            }
            else
            {
                var groups = candidates.GroupBy(c => BuildGroupKey(c), StringComparer.OrdinalIgnoreCase);
                var list = new List<CandidateEntry>();

                foreach (var g in groups)
                {
                    var latest = g.OrderByDescending(x => x.OccurredAtUtc).First();
                    decimal finalAmount;
                    CandidateEntry chosen = latest;

                    switch (section.AggregationMode)
                    {
                        case CreditAggregationModes.UniqueUsersHighest:
                            chosen = g.OrderByDescending(x => x.Amount).ThenByDescending(x => x.OccurredAtUtc).First();
                            finalAmount = chosen.Amount;
                            break;

                        case CreditAggregationModes.UniqueUsersLatest:
                            finalAmount = latest.Amount;
                            break;

                        case CreditAggregationModes.CombineByUserSum:
                        default:
                            finalAmount = g.Sum(x => x.Amount);
                            break;
                    }

                    list.Add(chosen with { Amount = finalAmount });
                }

                aggregated = list;
            }

            if (section.MinAmount.HasValue)
            {
                aggregated = aggregated.Where(c => c.Amount >= section.MinAmount.Value);
            }

            aggregated = section.SortMode switch
            {
                CreditSortModes.OldestFirst => aggregated.OrderBy(c => c.OccurredAtUtc),
                CreditSortModes.MostRecent => aggregated.OrderByDescending(c => c.OccurredAtUtc),
                CreditSortModes.Alphabetical => aggregated.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase),
                CreditSortModes.Random => aggregated.OrderBy(_ => Random.Shared.Next()),
                CreditSortModes.HighestAmount or _ => aggregated
                    .OrderByDescending(c => c.Amount)
                    .ThenByDescending(c => c.OccurredAtUtc)
            };

            if (section.MaxEntries > 0)
            {
                aggregated = aggregated.Take(section.MaxEntries);
            }

            return aggregated.Select(c => new CreditsRenderedEntry
            {
                DisplayName = c.DisplayName,
                Username = c.Username,
                AvatarUrl = c.AvatarUrl,
                AvatarColor = CreditsAvatarResolver.GetDeterministicColor(!string.IsNullOrEmpty(c.UserId) ? c.UserId : c.DisplayName),
                FormattedText = FormatEntryText(section, c.DisplayName, c.Amount, c.Detail, c.Tier),
                Detail = c.Detail,
                Amount = c.Amount,
                Role = c.Role,
                EventType = c.EventType,
                IsAnonymous = c.IsAnonymous,
                InternalUserId = c.UserId
            }).ToList();
        }

        private async Task PopulateAvatarsAsync(
            List<CreditsRenderedSection> sections,
            CreditsHeaderConfig header,
            bool skipTwitchLookup,
            CancellationToken cancellationToken)
        {
            var anySectionNeedsAvatars = sections.Any(s => s.ShowAvatars);
            if (!anySectionNeedsAvatars) return;

            var lookups = new List<(string? UserId, string? Username, string? KnownAvatarUrl)>();
            foreach (var section in sections.Where(s => s.ShowAvatars))
            {
                foreach (var entry in section.Entries)
                {
                    if (entry.IsAnonymous || !string.IsNullOrWhiteSpace(entry.AvatarUrl))
                        continue;

                    if (skipTwitchLookup && section.SourceTypes.All(t => !t.Equals(CreditSectionSources.CustomStatic, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    lookups.Add((entry.InternalUserId, entry.Username, entry.AvatarUrl));
                }
            }

            if (lookups.Count == 0) return;

            var resolvedMap = await avatarResolver.ResolveAvatarsAsync(lookups);
            foreach (var section in sections.Where(s => s.ShowAvatars))
            {
                foreach (var entry in section.Entries)
                {
                    if (entry.IsAnonymous || !string.IsNullOrWhiteSpace(entry.AvatarUrl))
                        continue;

                    entry.AvatarUrl = CreditsAvatarResolver.LookupAvatar(resolvedMap, entry.InternalUserId, entry.Username, entry.AvatarUrl);
                }
            }
        }

        private static CreditsRenderedSection CreateRenderedSection(
            CreditsSectionConfig section,
            CreditsThemeConfig themeConfig,
            List<CreditsRenderedEntry> entries)
        {
            var layout = string.IsNullOrWhiteSpace(section.LayoutOverride) ||
                         section.LayoutOverride.Equals(CreditLayouts.InheritTheme, StringComparison.OrdinalIgnoreCase)
                ? themeConfig.DefaultLayout
                : section.LayoutOverride;

            var showAvatars = section.AvatarMode switch
            {
                CreditAvatarModes.Show => true,
                CreditAvatarModes.Hide => false,
                _ => themeConfig.ShowAvatars
            };

            return new CreditsRenderedSection
            {
                Id = section.Id,
                Title = section.Title,
                Subtitle = section.Subtitle,
                HeaderImageUrl = section.HeaderImageUrl,
                SourceTypes = [.. section.SourceTypes],
                Layout = layout,
                ShowAvatars = showAvatars,
                AvatarSizePx = section.AvatarSizePxOverride is > 0 ? section.AvatarSizePxOverride.Value : themeConfig.AvatarSizePx,
                EmptyFallbackText = section.EmptyFallbackText,
                CustomCssClass = section.CustomCssClass,
                Entries = entries
            };
        }

        public static string FormatEntryText(
            CreditsSectionConfig section,
            string displayName,
            decimal amount,
            string? detail,
            string? tier)
        {
            var template = string.IsNullOrWhiteSpace(section.EntryFormatTemplate)
                ? "{name}"
                : section.EntryFormatTemplate;

            var amountText = FormatAmount(amount);
            var text = template
                .Replace("{name}", displayName, StringComparison.OrdinalIgnoreCase)
                .Replace("{amount}", section.ShowAmount ? amountText : string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{detail}", detail ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{tier}", tier ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{count}", section.ShowAmount ? amountText : string.Empty, StringComparison.OrdinalIgnoreCase);

            // Clean up dangling separators when {detail} or {amount} is empty
            text = text.Trim();
            while (text.EndsWith("—", StringComparison.Ordinal) || text.EndsWith("-", StringComparison.Ordinal))
            {
                text = text[..^1].TrimEnd();
            }
            if (text.EndsWith("()", StringComparison.Ordinal))
            {
                text = text[..^2].TrimEnd();
            }

            return string.IsNullOrWhiteSpace(text) ? displayName : text;
        }

        public static string FormatAmount(decimal amount)
        {
            if (amount == Math.Truncate(amount))
                return ((long)amount).ToString(CultureInfo.InvariantCulture);

            return amount.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private ExclusionContext BuildExclusionContext(CreditsExclusionConfig exclusions)
        {
            var customExcluded = exclusions.ExcludedUsernames
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u => UsernameNormalizer.Normalize(u))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return new ExclusionContext(
                exclusions,
                UsernameNormalizer.Normalize(serviceBackbone.BroadcasterName),
                UsernameNormalizer.Normalize(serviceBackbone.BotName ?? string.Empty),
                customExcluded);
        }

        private bool IsExcludedUser(string? username, bool isAnonymous, ExclusionContext ctx)
        {
            if (isAnonymous)
                return !ctx.Config.IncludeAnonymous;

            var norm = UsernameNormalizer.Normalize(username ?? string.Empty);
            if (string.IsNullOrWhiteSpace(norm))
                return false;

            if (ctx.Config.ExcludeBroadcaster && !string.IsNullOrEmpty(ctx.Broadcaster) &&
                norm.Equals(ctx.Broadcaster, StringComparison.OrdinalIgnoreCase))
                return true;

            if (ctx.Config.ExcludeBotAccount && !string.IsNullOrEmpty(ctx.BotName) &&
                norm.Equals(ctx.BotName, StringComparison.OrdinalIgnoreCase))
                return true;

            if (ctx.Config.ExcludeKnownBots && knownBots.IsKnownBot(norm))
                return true;

            return ctx.CustomExcluded.Contains(norm);
        }

        private static string ResolveDisplayName(CreditEvent ev, ExclusionContext ctx)
        {
            if (ev.IsAnonymous && string.IsNullOrWhiteSpace(ev.DisplayName))
                return string.IsNullOrWhiteSpace(ctx.Config.AnonymousLabel) ? "Anonymous" : ctx.Config.AnonymousLabel;
            if (ev.IsAnonymous && ev.DisplayName.Equals("Anonymous", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(ctx.Config.AnonymousLabel) ? "Anonymous" : ctx.Config.AnonymousLabel;

            return !string.IsNullOrWhiteSpace(ev.DisplayName)
                ? ev.DisplayName
                : (!string.IsNullOrWhiteSpace(ev.Username) ? ev.Username : "Viewer");
        }

        private static string BuildGroupKey(CandidateEntry c)
        {
            if (c.EventType == CreditSectionSources.CustomStatic)
                return $"static:{c.DisplayName}:{c.Role}";
            if (c.IsAnonymous)
                return $"anon:{c.EventType}:{c.OccurredAtUtc.Ticks}";
            if (!string.IsNullOrWhiteSpace(c.UserId))
                return $"id:{c.UserId}";
            if (!string.IsNullOrWhiteSpace(c.Username))
                return $"user:{c.Username}";
            return $"name:{c.DisplayName}";
        }

        public static bool MatchesDetailFilter(string? detail, string? detailFilter)
        {
            if (string.IsNullOrWhiteSpace(detailFilter)) return true;

            var tokens = detailFilter
                .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (tokens.Count == 0) return true;

            var includeTokens = tokens.Where(t => !t.StartsWith('!')).ToList();
            var excludeTokens = tokens.Where(t => t.StartsWith('!') && t.Length > 1).Select(t => t[1..].Trim()).ToList();

            var value = detail ?? string.Empty;

            if (excludeTokens.Any(ex => value.Contains(ex, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (includeTokens.Count > 0 && !includeTokens.Any(inc => value.Contains(inc, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }

        private static (bool IsMod, bool IsVip) ParseChatterRoleFlags(string? metadataJson)
        {
            if (string.IsNullOrWhiteSpace(metadataJson)) return (false, false);
            try
            {
                using var doc = JsonDocument.Parse(metadataJson);
                var isMod = doc.RootElement.TryGetProperty("isMod", out var m) && m.ValueKind == JsonValueKind.True;
                var isVip = doc.RootElement.TryGetProperty("isVip", out var v) && v.ValueKind == JsonValueKind.True;
                return (isMod, isVip);
            }
            catch
            {
                return (false, false);
            }
        }

        private static (decimal Amount, string? Detail, string? Tier) CreateSampleMetric(string source, int index)
        {
            return source switch
            {
                CreditSectionSources.NewSubscribers => (1, "Tier 1", "1000"),
                CreditSectionSources.Resubscribers => ((index + 2) * 3, $"{(index + 2) * 3} months", "1000"),
                CreditSectionSources.SubGifters => (Math.Max(1, 5 - index), $"{Math.Max(1, 5 - index)} gifted", null),
                CreditSectionSources.CheersAndBits => ((5 - Math.Min(4, index)) * 250, $"{(5 - Math.Min(4, index)) * 250} bits", null),
                CreditSectionSources.BitsPowerUps => (100, "Gigantify an Emote", null),
                CreditSectionSources.IncomingRaids => (Math.Max(4, (4 - index) * 12), $"{Math.Max(4, (4 - index) * 12)} viewers", null),
                CreditSectionSources.CharityDonations => (25m * (index + 1), $"Stream Charity (${25 * (index + 1)})", "USD"),
                CreditSectionSources.WatchStreaks => (3 + index, $"{3 + index} stream streak", null),
                CreditSectionSources.ChannelPointRedemptions => (1, index % 2 == 0 ? "Hydrate!" : "Stretch Break", null),
                CreditSectionSources.BitsBadgeMilestones => (1000, "1000 Bits Badge", null),
                CreditSectionSources.SubUpgrades => (1, "Prime Paid Upgrade", null),
                CreditSectionSources.TopSupporters => (25 - index * 4, "Top Supporter", null),
                CreditSectionSources.FishingTopAnglers => (500 - index * 95, $"{500 - index * 95} gold", null),
                CreditSectionSources.DropGameWinners => (95 - index * 10, index % 2 == 0 ? "Parachute" : "Plinko", null),
                CreditSectionSources.GiveawayWinners => (1, "Steam Key", null),
                CreditSectionSources.SongRequesters => (3 - Math.Min(2, index), "3 requests", null),
                CreditSectionSources.Moderators => (1, "Moderator", null),
                CreditSectionSources.Vips => (1, "VIP", null),
                _ => (1, null, null)
            };
        }

        private static string ReplaceHeaderFooterTokens(string? input, Dictionary<string, string> tokens)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var result = input;
            foreach (var kvp in tokens)
            {
                result = result.Replace($"{{{kvp.Key}}}", kvp.Value, StringComparison.OrdinalIgnoreCase);
            }
            return result.Trim();
        }

        private static string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            return $"{ts.Minutes}m";
        }

        private static string NormalizeSampleMode(string? mode)
        {
            if (string.Equals(mode, CreditSampleModes.Fake, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "1", StringComparison.OrdinalIgnoreCase))
                return CreditSampleModes.Fake;

            if (string.Equals(mode, CreditSampleModes.DatabaseViewers, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "viewers", StringComparison.OrdinalIgnoreCase))
                return CreditSampleModes.DatabaseViewers;

            return CreditSampleModes.Off;
        }

        public static CreditsProfileConfig DeserializeProfileConfig(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return CreditsBuiltInThemes.CreateDefaultProfileConfig();
            try
            {
                var parsed = JsonSerializer.Deserialize<CreditsProfileConfig>(json, JsonOptions);
                return parsed ?? CreditsBuiltInThemes.CreateDefaultProfileConfig();
            }
            catch
            {
                return CreditsBuiltInThemes.CreateDefaultProfileConfig();
            }
        }

        public static CreditsThemeConfig DeserializeThemeConfig(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new CreditsThemeConfig();
            try
            {
                return JsonSerializer.Deserialize<CreditsThemeConfig>(json, JsonOptions) ?? new CreditsThemeConfig();
            }
            catch
            {
                return new CreditsThemeConfig();
            }
        }

        private static CreditsPlaybackConfig ClonePlayback(CreditsPlaybackConfig p) => new()
        {
            AnimationMode = p.AnimationMode,
            SpeedMode = p.SpeedMode,
            ScrollSpeedPxPerSec = p.ScrollSpeedPxPerSec,
            TargetDurationSeconds = p.TargetDurationSeconds,
            PageDurationSeconds = p.PageDurationSeconds,
            StartBehavior = p.StartBehavior,
            EndBehavior = p.EndBehavior,
            TestModeDefault = p.TestModeDefault
        };

        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private sealed record CandidateEntry(
            string UserId,
            string Username,
            string DisplayName,
            string? AvatarUrl,
            decimal Amount,
            string? Detail,
            string? Tier,
            DateTime OccurredAtUtc,
            string EventType,
            bool IsAnonymous,
            string? Role);

        private sealed record ExclusionContext(
            CreditsExclusionConfig Config,
            string Broadcaster,
            string BotName,
            HashSet<string> CustomExcluded);

        private sealed record SampleUserSeed(string UserId, string Username, string DisplayName);
        private sealed record GiveawayWinnerRow(string Username, string Prize, DateTime WinningDate);
        private sealed record FishCatchRow(string UserId, string Username, int GoldEarned, double Weight, DateTime CaughtAt);
        private sealed record ViewerRoleRow(string UserId, string Username, string DisplayName, bool IsMod, bool IsVip, DateTime LastSeen);
    }
}
