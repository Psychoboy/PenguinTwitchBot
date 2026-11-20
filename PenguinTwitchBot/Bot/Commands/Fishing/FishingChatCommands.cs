using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Helpers;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public class FishingChatCommands(
        ILogger<FishingChatCommands> logger,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        Application.Notifications.IPenguinDispatcher dispatcher,
        IFishingService fishingService,
        IFishingInventoryService inventoryService,
        IFishingFrenzyService frenzyService,
        IFishingMessageSettingsService? messageSettingsService = null)
        : BaseCommandService(serviceBackbone, commandHandler, "FishingChatCommands", dispatcher), IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Register();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override async Task Register()
        {
            const string moduleName = "Fishing";
            await RegisterDefaultCommand("fishstats", this, moduleName);
            await RegisterDefaultCommand("fishgear", this, moduleName);
            await RegisterDefaultCommand("fishrepair", this, moduleName);
            await RegisterDefaultCommand("fishdex", this, moduleName);
            await RegisterDefaultCommand("fishfrenzy", this, moduleName);
            await RegisterDefaultCommand("fishtourney", this, moduleName);
            await RegisterDefaultCommand("blitz", this, moduleName);
            logger.LogInformation("Registered Fishing convenience chat commands");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;

            switch (command.CommandProperties.CommandName)
            {
                case "fishstats":
                    await HandleFishStatsAsync(e);
                    break;
                case "fishgear":
                    await HandleFishGearAsync(e);
                    break;
                case "fishrepair":
                    await HandleFishRepairAsync(e);
                    break;
                case "fishdex":
                    await HandleFishDexAsync(e);
                    break;
                case "fishfrenzy":
                    await HandleFishFrenzyAsync(e);
                    break;
                case "fishtourney":
                case "blitz":
                    await HandleFishTourneyAsync(e);
                    break;
            }
        }

        private Task<FishingMessageSettings> GetMessagesAsync() =>
            messageSettingsService != null ? messageSettingsService.GetAsync() : Task.FromResult(new FishingMessageSettings());

        private async Task HandleFishStatsAsync(CommandEventArgs e)
        {
            var summary = await fishingService.GetUserDexSummaryAsync(e.UserId);
            var msgs = await GetMessagesAsync();
            var bestPart = !string.IsNullOrWhiteSpace(summary.BestCatchFishName)
                ? $" | Best: {summary.BestCatchStars}★ {summary.BestCatchFishName} ({summary.BestCatchWeight:0.##}kg, {summary.BestCatchGold:N0}g)"
                : "";

            var msg = FishingMessageSettings.Format(msgs.StatsResponse,
                ("user", e.DisplayName),
                ("gold", summary.TotalGold.ToString("N0")),
                ("catches", summary.TotalCatches.ToString("N0")),
                ("caughtSpecies", summary.CaughtSpeciesCount.ToString()),
                ("totalSpecies", summary.TotalSpeciesCount.ToString()),
                ("percent", summary.CompletionPercent.ToString("0.#")),
                ("bestPart", bestPart));
            if (!string.IsNullOrWhiteSpace(msg))
                await ServiceBackbone.ResponseWithMessage(e, msg);
        }

        private async Task HandleFishGearAsync(CommandEventArgs e)
        {
            var msgs = await GetMessagesAsync();
            var equipped = await inventoryService.GetUserEquipmentBySlot(e.UserId);
            if (equipped.Count == 0)
            {
                var emptyMsg = FishingMessageSettings.Format(msgs.GearEmptyResponse, ("user", e.DisplayName));
                if (!string.IsNullOrWhiteSpace(emptyMsg))
                    await ServiceBackbone.ResponseWithMessage(e, emptyMsg);
                return;
            }

            var parts = new List<string>();
            var brokenCount = 0;

            foreach (var (slot, boost) in equipped.OrderBy(k => (int)k.Key))
            {
                var item = boost.ShopItem;
                if (item == null) continue;

                var suffix = "";
                if (boost.IsBroken)
                {
                    brokenCount++;
                    suffix = " [BROKEN]";
                }
                else if (item.MaxDurability.HasValue)
                {
                    var cur = boost.CurrentDurability ?? item.MaxDurability.Value;
                    var pct = item.MaxDurability.Value > 0 ? (int)Math.Round(cur / item.MaxDurability.Value * 100) : 0;
                    suffix = $" ({pct}%)";
                }
                else if (item.MaxUses.HasValue)
                {
                    suffix = $" ({boost.RemainingUses}/{item.MaxUses.Value} uses)";
                }

                parts.Add($"{slot}: {item.Name}{suffix}");
            }

            var repairHint = brokenCount > 0 ? " | Use !fishrepair to fix broken gear!" : "";
            var gearMsg = FishingMessageSettings.Format(msgs.GearResponse,
                ("user", e.DisplayName),
                ("gear", string.Join(" • ", parts)),
                ("repairHint", repairHint));
            if (!string.IsNullOrWhiteSpace(gearMsg))
                await ServiceBackbone.ResponseWithMessage(e, gearMsg);
        }

        private async Task HandleFishRepairAsync(CommandEventArgs e)
        {
            try
            {
                var msgs = await GetMessagesAsync();
                var settings = await fishingService.GetSettings();
                var multiplier = settings?.RepairCostMultiplier ?? 0.0;
                if (multiplier <= 0)
                {
                    await ServiceBackbone.ResponseWithMessage(e, "🔧 Equipment repair is currently disabled.");
                    return;
                }

                var cost = await inventoryService.RepairAllEquippedItems(e.UserId, multiplier, e.DisplayName);
                if (cost <= 0)
                {
                    var noneMsg = FishingMessageSettings.Format(msgs.RepairNoneNeededResponse, ("user", e.DisplayName));
                    if (!string.IsNullOrWhiteSpace(noneMsg))
                        await ServiceBackbone.ResponseWithMessage(e, noneMsg);
                    return;
                }

                var gold = await fishingService.GetUserGold(e.UserId);
                var okMsg = FishingMessageSettings.Format(msgs.RepairSuccessResponse,
                    ("user", e.DisplayName),
                    ("cost", cost.ToString("N0")),
                    ("gold", (gold?.TotalGold ?? 0).ToString("N0")));
                if (!string.IsNullOrWhiteSpace(okMsg))
                    await ServiceBackbone.ResponseWithMessage(e, okMsg);
            }
            catch (InvalidOperationException ex)
            {
                await ServiceBackbone.ResponseWithMessage(e, $"⚠️ {e.DisplayName}: {ex.Message}");
            }
        }

        private async Task HandleFishDexAsync(CommandEventArgs e)
        {
            var msgs = await GetMessagesAsync();
            var query = string.Join(" ", e.Args ?? []).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                var summary = await fishingService.GetUserDexSummaryAsync(e.UserId);
                var sumMsg = FishingMessageSettings.Format(msgs.DexSummaryResponse,
                    ("user", e.DisplayName),
                    ("caughtSpecies", summary.CaughtSpeciesCount.ToString()),
                    ("totalSpecies", summary.TotalSpeciesCount.ToString()),
                    ("percent", summary.CompletionPercent.ToString("0.#")));
                if (!string.IsNullOrWhiteSpace(sumMsg))
                    await ServiceBackbone.ResponseWithMessage(e, sumMsg);
                return;
            }

            var allFish = await fishingService.GetAllFishTypes();
            var match = allFish.FirstOrDefault(f => f.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                ?? allFish.FirstOrDefault(f => f.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                await ServiceBackbone.ResponseWithMessage(e, $"📖 No fish found matching '{query}'.");
                return;
            }

            var count = await fishingService.GetUserCatchCountForFishType(e.UserId, match.Id);
            if (count == 0)
            {
                var cats = match.Categories.Count > 0 ? $" [{string.Join(", ", match.Categories.Select(c => c.Category))}]" : "";
                var uncaughtMsg = FishingMessageSettings.Format(msgs.DexFishUncaughtResponse,
                    ("fish", match.Name),
                    ("rarity", match.Rarity.ToString()),
                    ("categories", cats),
                    ("weight", match.BaseWeight.ToString("0.##")),
                    ("gold", match.BaseGold.ToString("N0")));
                if (!string.IsNullOrWhiteSpace(uncaughtMsg))
                    await ServiceBackbone.ResponseWithMessage(e, uncaughtMsg);
                return;
            }

            var best = await fishingService.GetUserBestCatchForFishType(e.UserId, match.Id);
            var caughtMsg = FishingMessageSettings.Format(msgs.DexFishCaughtResponse,
                ("user", e.DisplayName),
                ("fish", match.Name),
                ("rarity", match.Rarity.ToString()),
                ("count", count.ToString()),
                ("stars", (best?.Stars ?? 1).ToString()),
                ("weight", (best?.Weight ?? 0).ToString("0.##")),
                ("gold", (best?.GoldEarned ?? 0).ToString("N0")));
            if (!string.IsNullOrWhiteSpace(caughtMsg))
                await ServiceBackbone.ResponseWithMessage(e, caughtMsg);
        }

        private async Task HandleFishFrenzyAsync(CommandEventArgs e)
        {
            var msgs = await GetMessagesAsync();
            var args = e.Args ?? [];
            var isElevated = e.IsBroadcaster || e.IsMod;

            if (args.Count > 0 && isElevated)
            {
                if (args[0].Equals("stop", StringComparison.OrdinalIgnoreCase))
                {
                    var stopped = await frenzyService.StopFrenzyAsync(announceInChat: true);
                    if (!stopped)
                    {
                        await ServiceBackbone.ResponseWithMessage(e, "No active Fishing Frenzy to stop.");
                    }
                    return;
                }

                if (int.TryParse(args[0], out var minutes))
                {
                    var mult = args.Count > 1 && double.TryParse(args[1], out var parsedMult) ? parsedMult : 2.0;
                    var category = args.Count > 2 ? string.Join(" ", args.Skip(2)).Trim() : null;
                    var name = string.IsNullOrWhiteSpace(category) ? "Fishing Frenzy" : $"{category} Hotspot";
                    await frenzyService.StartFrenzyAsync(name, minutes, mult, category, string.IsNullOrWhiteSpace(category) ? 0.0 : 0.5, announceInChat: true);
                    return;
                }
            }

            var active = frenzyService.GetActiveFrenzy();
            if (active == null)
            {
                var modHint = isElevated ? " Mods: use !fishfrenzy <minutes> [multiplier] [category]" : "";
                var noneMsg = FishingMessageSettings.Format(msgs.FrenzyStatusNone, ("modHint", modHint));
                if (!string.IsNullOrWhiteSpace(noneMsg))
                    await ServiceBackbone.ResponseWithMessage(e, noneMsg);
                return;
            }

            var rem = active.GetRemaining(DateTime.UtcNow);
            var activeMsg = FishingMessageSettings.Format(msgs.FrenzyStatusActive,
                ("name", active.Name),
                ("bonus", FishingFrenzyService.BuildBonusSummary(active)),
                ("remaining", $"{(int)rem.TotalMinutes}m {rem.Seconds:D2}s"));
            if (!string.IsNullOrWhiteSpace(activeMsg))
                await ServiceBackbone.ResponseWithMessage(e, activeMsg);
        }

        private async Task HandleFishTourneyAsync(CommandEventArgs e)
        {
            var msgs = await GetMessagesAsync();
            var args = e.Args ?? [];
            var isElevated = e.IsBroadcaster || e.IsMod;

            if (args.Count > 0 && args[0].Equals("quick", StringComparison.OrdinalIgnoreCase))
            {
                if (!isElevated)
                {
                    await ServiceBackbone.ResponseWithMessage(e, "Only moderators or the broadcaster can launch a Quick Tournament.");
                    return;
                }

                var minutes = args.Count > 1 && int.TryParse(args[1], out var parsedMin) ? Math.Clamp(parsedMin, 1, 240) : 10;
                var theme = args.Count > 2 ? string.Join(" ", args.Skip(2)).Trim() : "Random";

                var tournament = await fishingService.StartQuickTournamentAsync(new QuickTournamentRequest(theme, minutes));
                var targetCat = tournament.EligibleCategories.FirstOrDefault()?.Category;
                await frenzyService.StartFrenzyAsync(
                    $"{tournament.Name} Frenzy",
                    minutes,
                    1.75,
                    targetCat,
                    string.IsNullOrWhiteSpace(targetCat) ? 0.0 : 0.45,
                    announceInChat: false,
                    linkedTournamentId: tournament.Id);

                var quickMsg = FishingMessageSettings.Format(msgs.QuickTournamentStart,
                    ("name", tournament.Name),
                    ("minutes", minutes.ToString()),
                    ("category", FishingFormatHelper.FormatScoreCategory(tournament.PrimaryScoreCategory)),
                    ("frenzyNote", " + 🔥 1.75x Frenzy!"));
                if (!string.IsNullOrWhiteSpace(quickMsg))
                    await ServiceBackbone.SendChatMessage(quickMsg);
                return;
            }

            var active = (await fishingService.GetCurrentFishingTournaments())
                .Where(t => t.Status == FishingTournamentStatus.Active)
                .ToList();

            if (active.Count == 0)
            {
                var modHint = isElevated ? " Mods: !fishtourney quick [minutes] [theme]" : "";
                await ServiceBackbone.ResponseWithMessage(e, $"🏆 No active fishing tournaments right now.{modHint}");
                return;
            }

            foreach (var t in active.Take(2))
            {
                var standings = await fishingService.GetFishingTournamentStandings(t.Id, 3);
                var topText = standings.Count > 0
                    ? string.Join(", ", standings.Select(s => $"#{s.Rank} {s.Username} ({FishingFormatHelper.FormatCompactNumber(s.Score)})"))
                    : "No catches yet";
                var timeText = t.EndsAtUtc.HasValue
                    ? FormatRemaining(t.EndsAtUtc.Value - DateTime.UtcNow)
                    : "No end time";
                await ServiceBackbone.ResponseWithMessage(e, $"🏆 {t.Name} ({timeText}): {topText}");
            }
        }

        private static string FormatRemaining(TimeSpan span)
        {
            if (span <= TimeSpan.Zero) return "Ending soon";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m left";
            return $"{span.Minutes}m {span.Seconds:D2}s left";
        }
    }
}
