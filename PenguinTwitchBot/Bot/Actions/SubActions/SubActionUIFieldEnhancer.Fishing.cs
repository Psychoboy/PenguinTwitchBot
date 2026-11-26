using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions;

public static partial class SubActionUIFieldEnhancer
{
    private static async Task<List<SubActionUIField>> EnhanceFishingGiveItemToPlayerAsync(List<SubActionUIField> fields, FishingGiveItemToPlayerType giveItem, IServiceProvider serviceProvider)
    {
        var fishingService = serviceProvider.GetRequiredService<IFishingService>();
        var shopService = serviceProvider.GetRequiredService<IFishingShopService>();

        var players = await fishingService.GetAllPlayersWithGold();
        var shopItems = await shopService.GetAllShopItems();

        var playerOptions = players
            .Where(player => !string.IsNullOrWhiteSpace(player.Username))
            .Select(player => new SelectOption
            {
                Name = $"{player.Username} ({player.UserId})",
                Value = player.Username
            })
            .GroupBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.Name)
            .ToList();

        if (!string.IsNullOrWhiteSpace(giveItem.TargetName)
            && !playerOptions.Any(option => string.Equals(option.Value, giveItem.TargetName, StringComparison.OrdinalIgnoreCase)))
        {
            playerOptions.Add(new SelectOption { Name = giveItem.TargetName, Value = giveItem.TargetName });
        }

        var shopItemOptions = shopItems
            .Select(item => new SelectOption
            {
                Id = item.Id,
                Name = item.Enabled ? item.Name : $"{item.Name} (disabled)"
            })
            .OrderBy(option => option.Name)
            .ToList();

        if (giveItem.ShopItemId.HasValue
            && !shopItemOptions.Any(option => option.Id == giveItem.ShopItemId.Value))
        {
            var fallbackName = string.IsNullOrWhiteSpace(giveItem.ShopItemName)
                ? $"Item #{giveItem.ShopItemId.Value}"
                : giveItem.ShopItemName;
            shopItemOptions.Add(new SelectOption { Id = giveItem.ShopItemId.Value, Name = fallbackName });
        }

        fields.RemoveAll(field => field.PropertyName == nameof(FishingGiveItemToPlayerType.TargetName));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(FishingGiveItemToPlayerType.TargetName),
            Label = "Target Username",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = playerOptions,
            AllowCustomValue = true,
            HelperText = "Select or type the username of the player who should receive the fishing item."
        });

        fields.RemoveAll(field => field.PropertyName == nameof(FishingGiveItemToPlayerType.ShopItemId));
        fields.Insert(1, new SubActionUIField
        {
            PropertyName = nameof(FishingGiveItemToPlayerType.ShopItemId),
            Label = "Fishing Item",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = shopItemOptions,
            HelperText = "Select which fishing item to grant."
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceFishingTournamentStartAsync(List<SubActionUIField> fields, IServiceProvider serviceProvider)
    {
        var fishingService = serviceProvider.GetRequiredService<IFishingService>();
        var tournaments = await fishingService.GetAllFishingTournaments();
        var options = tournaments
            .Select(t => new SelectOption { Id = t.Id, Name = $"{t.Name} ({t.Status})" })
            .OrderBy(o => o.Name)
            .ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(FishingTournamentStartType.TournamentId));
        fields.Add(new SubActionUIField
        {
            PropertyName = nameof(FishingTournamentStartType.TournamentId),
            Label = "Tournament / Template",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = options,
            HelperText = "Select an existing tournament to start, or a template to clone and start."
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceFishingFrenzyAsync(List<SubActionUIField> fields, FishingFrenzyType frenzy, IServiceProvider serviceProvider)
    {
        var fishingService = serviceProvider.GetRequiredService<IFishingService>();
        var fishTypes = await fishingService.GetAllFishTypes();

        var categoryOptions = new List<SelectOption>
        {
            new() { Name = "None (All Fish)", Value = "" }
        };

        var distinctCategories = fishTypes
            .SelectMany(f => f.Categories)
            .Select(c => c.Category?.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);

        foreach (var cat in distinctCategories)
        {
            categoryOptions.Add(new SelectOption { Name = cat!, Value = cat! });
        }

        if (!string.IsNullOrWhiteSpace(frenzy.TargetCategory) &&
            !categoryOptions.Any(o => string.Equals(o.Value, frenzy.TargetCategory, StringComparison.OrdinalIgnoreCase)))
        {
            categoryOptions.Add(new SelectOption { Name = frenzy.TargetCategory, Value = frenzy.TargetCategory });
        }

        var idx = fields.FindIndex(f => f.PropertyName == nameof(FishingFrenzyType.TargetCategory));
        if (idx >= 0)
        {
            fields[idx] = new SubActionUIField
            {
                PropertyName = nameof(FishingFrenzyType.TargetCategory),
                Label = "Target Category",
                FieldType = UIFieldType.Select,
                Required = false,
                DefaultValue = "",
                SelectOptions = categoryOptions,
                HelperText = "Select None (All Fish) or a specific fish category for a Category Hotspot."
            };
        }

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceFishingQuickTournamentAsync(List<SubActionUIField> fields, FishingQuickTournamentType quickTourney, IServiceProvider serviceProvider)
    {
        var fishingService = serviceProvider.GetRequiredService<IFishingService>();
        var fishTypes = await fishingService.GetAllFishTypes();

        var categoryOptions = new List<SelectOption>
        {
            new() { Name = "Random (All Fish or Random Category)", Value = "Random" },
            new() { Name = "All Fish", Value = "AllFish" }
        };

        var dbCategories = fishTypes
            .Where(f => f.Enabled)
            .SelectMany(f => f.Categories)
            .Select(c => c.Category?.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);

        foreach (var cat in dbCategories)
        {
            categoryOptions.Add(new SelectOption { Name = cat!, Value = cat! });
        }

        if (!string.IsNullOrWhiteSpace(quickTourney.Theme) &&
            !categoryOptions.Any(o => string.Equals(o.Value, quickTourney.Theme, StringComparison.OrdinalIgnoreCase)))
        {
            categoryOptions.Add(new SelectOption { Name = quickTourney.Theme, Value = quickTourney.Theme });
        }

        var idx = fields.FindIndex(f => f.PropertyName == nameof(FishingQuickTournamentType.Theme));
        if (idx >= 0)
        {
            fields[idx] = new SubActionUIField
            {
                PropertyName = nameof(FishingQuickTournamentType.Theme),
                Label = "Fish Category / Pool",
                FieldType = UIFieldType.Select,
                Required = true,
                DefaultValue = "Random",
                SelectOptions = categoryOptions,
                HelperText = "Choose Random, All Fish, or one of your existing fish categories."
            };
        }

        var scoreIdx = fields.FindIndex(f => f.PropertyName == nameof(FishingQuickTournamentType.ScoreCategory));
        if (scoreIdx >= 0)
        {
            fields[scoreIdx] = new SubActionUIField
            {
                PropertyName = nameof(FishingQuickTournamentType.ScoreCategory),
                Label = "Scoring Mode",
                FieldType = UIFieldType.Select,
                Required = true,
                DefaultValue = "Random",
                SelectOptions =
                [
                    new() { Name = "Random Scoring Mode", Value = "Random" },
                    new() { Name = "Largest Weight", Value = "Largest" },
                    new() { Name = "Smallest Weight", Value = "Smallest" },
                    new() { Name = "Most Valuable (Gold)", Value = "MostValuable" },
                    new() { Name = "Most Catches", Value = "MostCatches" },
                    new() { Name = "Total Weight", Value = "TotalWeight" },
                    new() { Name = "Best Average", Value = "Average" }
                ],
                HelperText = "How tournament winners are scored."
            };
        }

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceFishingTournamentEndAsync(List<SubActionUIField> fields, IServiceProvider serviceProvider)
    {
        var fishingService = serviceProvider.GetRequiredService<IFishingService>();
        var tournaments = await fishingService.GetAllFishingTournaments();
        var options = tournaments
            .Select(t => new SelectOption { Id = t.Id, Name = $"{t.Name} ({t.Status})" })
            .OrderBy(o => o.Name)
            .ToList();

        fields.RemoveAll(f => f.PropertyName == nameof(FishingTournamentEndType.TournamentId));
        fields.Insert(0, new SubActionUIField
        {
            PropertyName = nameof(FishingTournamentEndType.TournamentId),
            Label = "Tournament",
            FieldType = UIFieldType.Select,
            Required = true,
            SelectOptions = options,
            HelperText = "Fishing tournament to end."
        });

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceFishingModifyAsync(List<SubActionUIField> fields, FishingModifyType modify, IServiceProvider serviceProvider)
    {
        var fishingService = serviceProvider.GetService<IFishingService>();
        var shopService = serviceProvider.GetService<IFishingShopService>();

        if (modify.TargetType == FishingModifyTargetType.Fish)
        {
            if (fishingService != null)
            {
                var fishTypes = await fishingService.GetAllFishTypes();
                var fishOptions = fishTypes
                    .Select(f => new SelectOption
                    {
                        Id = f.Id,
                        Name = $"{f.Name} (ID: {f.Id} | {f.Rarity} | {f.BaseGold}g{(f.Enabled ? "" : " - disabled")})",
                        Value = f.Id.ToString()
                    })
                    .OrderBy(o => o.Name)
                    .ToList();

                if (!string.IsNullOrWhiteSpace(modify.TargetFish)
                    && !fishOptions.Any(o => o.Value == modify.TargetFish || o.Name == modify.TargetFish))
                {
                    fishOptions.Insert(0, new SelectOption { Name = modify.TargetFish, Value = modify.TargetFish });
                }

                var targetFishField = fields.FirstOrDefault(f => f.PropertyName == nameof(FishingModifyType.TargetFish));
                if (targetFishField != null)
                {
                    targetFishField.SelectOptions = fishOptions;
                }

                var settings = await fishingService.GetSettings();
                if (settings != null)
                {
                    var rarityField = fields.FirstOrDefault(f => f.PropertyName == nameof(FishingModifyType.RarityMode));
                    if (rarityField != null)
                    {
                        rarityField.HelperText = $"Auto threshold rules: Uncommon \u2265 {settings.RarityUncommonThreshold}g, Rare \u2265 {settings.RarityRareThreshold}g, Epic \u2265 {settings.RarityEpicThreshold}g, Legendary \u2265 {settings.RarityLegendaryThreshold}g, Mythical \u2265 {settings.RarityMythicalThreshold}g.";
                    }
                }
            }
        }
        else
        {
            if (shopService != null)
            {
                var shopItems = await shopService.GetAllShopItems();
                var shopItemOptions = shopItems
                    .Select(item => new SelectOption
                    {
                        Id = item.Id,
                        Name = $"{item.Name} (ID: {item.Id} | {item.Cost}g{(item.Enabled ? "" : " - disabled")})",
                        Value = item.Id.ToString()
                    })
                    .OrderBy(o => o.Name)
                    .ToList();

                if (!string.IsNullOrWhiteSpace(modify.TargetShopItem)
                    && !shopItemOptions.Any(o => o.Value == modify.TargetShopItem || o.Name == modify.TargetShopItem))
                {
                    shopItemOptions.Insert(0, new SelectOption { Name = modify.TargetShopItem, Value = modify.TargetShopItem });
                }

                var targetShopField = fields.FirstOrDefault(f => f.PropertyName == nameof(FishingModifyType.TargetShopItem));
                if (targetShopField != null)
                {
                    targetShopField.SelectOptions = shopItemOptions;
                }

                if (fishingService != null)
                {
                    var targetFishField = fields.FirstOrDefault(f => f.PropertyName == nameof(FishingModifyType.NewTargetFish));
                    var targetCatField = fields.FirstOrDefault(f => f.PropertyName == nameof(FishingModifyType.NewTargetCategory));
                    if (targetFishField != null || targetCatField != null)
                    {
                        var fishTypes = await fishingService.GetAllFishTypes();
                        if (targetFishField != null)
                        {
                            targetFishField.SelectOptions = fishTypes
                                .Select(f => new SelectOption
                                {
                                    Id = f.Id,
                                    Name = $"{f.Name} (ID: {f.Id} | {f.Rarity})",
                                    Value = f.Id.ToString()
                                })
                                .OrderBy(o => o.Name)
                                .ToList();
                        }

                        if (targetCatField != null)
                        {
                            targetCatField.SelectOptions = fishTypes
                                .SelectMany(f => f.Categories.Select(c => c.Category))
                                .Where(c => !string.IsNullOrWhiteSpace(c))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(c => c)
                                .Select(c => new SelectOption { Name = c, Value = c })
                                .ToList();
                        }
                    }
                }
            }
        }

        return fields;
    }
}
