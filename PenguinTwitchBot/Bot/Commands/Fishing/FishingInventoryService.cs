using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public class FishingInventoryService : IFishingInventoryService
    {
        private const string ShopItemInclude = "ShopItem.TargetFishType";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FishingInventoryService> _logger;

        // Serializes gold/use mutations per user so two concurrent actions for the same user
        // can't both read/decrement the same balance or RemainingUses value.
        private readonly KeyedSemaphore _userLocks = new();

        public FishingInventoryService(IServiceScopeFactory scopeFactory, ILogger<FishingInventoryService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<List<UserFishingBoost>> GetUserBoosts(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var boosts = await db.UserFishingBoosts.GetAsync(b => b.UserId == userId, includeProperties: ShopItemInclude);
            foreach (var b in boosts)
            {
                ReconcileDurability(b);
            }
            return boosts;
        }

        public async Task<List<UserFishingBoost>> GetUserEquippedItems(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var boosts = await db.UserFishingBoosts.GetAsync(b => b.UserId == userId && b.IsEquipped, includeProperties: ShopItemInclude);
            foreach (var b in boosts)
            {
                ReconcileDurability(b);
            }
            return boosts;
        }

        public async Task<Dictionary<EquipmentSlot, UserFishingBoost>> GetUserEquipmentBySlot(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var equipped = await db.UserFishingBoosts.GetAsync(
                b => b.UserId == userId && b.IsEquipped && b.ShopItem!.EquipmentSlot != null,
                includeProperties: ShopItemInclude);

            foreach (var b in equipped)
            {
                ReconcileDurability(b);
            }

            return equipped.ToDictionary(e => e.ShopItem!.EquipmentSlot!.Value, e => e);
        }

        public async Task PurchaseBoost(string userId, int shopItemId, int quantity = 1)
        {
            if (quantity < 1)
            {
                throw new InvalidOperationException("Purchase quantity must be at least 1");
            }

            using var userLock = await _userLocks.AcquireAsync(userId, CancellationToken.None);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var shopItem = await db.FishingShopItems.GetByIdAsync(shopItemId);
            if (shopItem == null || !shopItem.Enabled || shopItem.IsAdminOnly)
            {
                throw new InvalidOperationException("Shop item not found, disabled, or not available for purchase");
            }

            if (!shopItem.MaxUses.HasValue && quantity != 1)
            {
                throw new InvalidOperationException("Only limited-use items can be purchased in multiples");
            }

            if (shopItem.MaxUses.HasValue && shopItem.MaxUses.Value <= 0)
            {
                throw new InvalidOperationException("Limited-use items must have at least 1 max use");
            }

            var totalCost = shopItem.Cost * quantity;
            var gold = await db.FishingGolds.Find(g => g.UserId == userId).FirstOrDefaultAsync();
            if (gold == null || gold.TotalGold < totalCost)
            {
                throw new InvalidOperationException("Not enough gold");
            }

            gold.TotalGold -= totalCost;
            db.UserFishingBoosts.AddRange(Enumerable
                .Range(0, shopItem.MaxUses.HasValue ? quantity : 1)
                .Select(_ => NewBoost(userId, shopItem)));

            // The debit and the new boosts share one SaveChanges, so they commit or roll back together.
            await db.SaveChangesAsync();
        }

        public async Task GiveItemToUser(string userId, int shopItemId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var shopItem = await db.FishingShopItems.GetByIdAsync(shopItemId);
            if (shopItem == null)
            {
                throw new InvalidOperationException("Shop item not found");
            }

            db.UserFishingBoosts.Add(NewBoost(userId, shopItem));
            await db.SaveChangesAsync();
        }

        public async Task SellItem(string userId, int userBoostId)
        {
            using var userLock = await _userLocks.AcquireAsync(userId, CancellationToken.None);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var userBoost = await FindUserBoost(db, userId, userBoostId);

            var sellEligibility = FishingInventorySellRules.GetSellEligibility(userBoost);
            if (sellEligibility != SellEligibilityReason.Eligible)
            {
                throw new InvalidOperationException(FishingInventorySellRules.GetSellFailureMessage(sellEligibility));
            }

            var gold = await db.FishingGolds.Find(g => g.UserId == userId).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("User gold record not found");

            gold.TotalGold += FishingInventorySellRules.GetSellPrice(userBoost!.ShopItem, userBoost);
            db.UserFishingBoosts.Remove(userBoost);
            await db.SaveChangesAsync();
        }

        public async Task EquipItem(string userId, int userBoostId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var userBoost = await FindUserBoost(db, userId, userBoostId)
                ?? throw new InvalidOperationException("Item not found");

            // Limited-use items with no uses left cannot be equipped.
            if (userBoost.ShopItem!.MaxUses.HasValue && userBoost.RemainingUses == 0)
            {
                throw new InvalidOperationException("Item has no remaining uses");
            }

            if (userBoost.ShopItem.EquipmentSlot.HasValue)
            {
                var slotItems = await db.UserFishingBoosts.GetAsync(
                    b => b.UserId == userId && b.IsEquipped && b.ShopItem!.EquipmentSlot == userBoost.ShopItem.EquipmentSlot,
                    includeProperties: "ShopItem");

                foreach (var item in slotItems)
                {
                    item.IsEquipped = false;
                }
            }

            userBoost.IsEquipped = true;
            await db.SaveChangesAsync();
        }

        public async Task UnequipItem(string userId, int userBoostId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var userBoost = await db.UserFishingBoosts.Find(b => b.Id == userBoostId && b.UserId == userId).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Item not found");

            userBoost.IsEquipped = false;
            await db.SaveChangesAsync();
        }

        public Task ConsumeItemUse(string userId, int userBoostId)
        {
            return ConsumeItemUses(userId, new[] { userBoostId });
        }

        // Batches uses across all equipped items in a single query/save instead of one round trip per item.
        public async Task ConsumeItemUses(string userId, IEnumerable<int> userBoostIds)
        {
            var ids = userBoostIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return;
            }

            using var userLock = await _userLocks.AcquireAsync(userId, CancellationToken.None);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var userBoosts = await db.UserFishingBoosts.GetAsync(
                b => b.UserId == userId && ids.Contains(b.Id) && b.IsEquipped,
                includeProperties: "ShopItem");

            if (userBoosts.Count == 0)
            {
                return;
            }

            // RemainingUses == -1 means unlimited, so those items are left untouched.
            foreach (var userBoost in userBoosts.Where(b => b.RemainingUses != -1))
            {
                if (userBoost.RemainingUses > 0)
                {
                    userBoost.RemainingUses--;
                }

                if (userBoost.RemainingUses <= 0)
                {
                    await RemoveAndEquipReplacement(db, userBoost);
                }
            }

            await db.SaveChangesAsync();
        }

        public async Task<List<FishingBrokenItemInfo>> ConsumeItemDurability(string userId, IEnumerable<int> userBoostIds, double repairCostMultiplier)
        {
            var ids = userBoostIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return [];
            }

            using var userLock = await _userLocks.AcquireAsync(userId, CancellationToken.None);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var userBoosts = await db.UserFishingBoosts.GetAsync(
                b => b.UserId == userId && ids.Contains(b.Id) && b.IsEquipped,
                includeProperties: "ShopItem");

            if (userBoosts.Count == 0)
            {
                return [];
            }

            var brokenItems = new List<FishingBrokenItemInfo>();

            foreach (var userBoost in userBoosts)
            {
                ReconcileDurability(userBoost);
                var shopItem = userBoost.ShopItem;
                if (shopItem?.MaxDurability.HasValue == true && userBoost.CurrentDurability.HasValue)
                {
                    var previousDurability = userBoost.CurrentDurability.Value;
                    var loss = Math.Max(0.0, shopItem.DurabilityLossPerUse ?? 1.0);
                    userBoost.CurrentDurability = Math.Max(0.0, userBoost.CurrentDurability.Value - loss);

                    if (previousDurability > 0.0 && userBoost.CurrentDurability.Value <= 0.0)
                    {
                        var brokenInfo = new FishingBrokenItemInfo
                        {
                            UserBoostId = userBoost.Id,
                            ShopItemId = userBoost.ShopItemId,
                            ItemName = shopItem.Name,
                            EquipmentSlot = shopItem.EquipmentSlot ?? EquipmentSlot.Rod,
                            ItemCost = shopItem.Cost,
                            WasReplaced = repairCostMultiplier <= 0
                        };
                        brokenItems.Add(brokenInfo);

                        if (repairCostMultiplier > 0)
                        {
                            // Preserved for repair; durability capped at 0
                            userBoost.CurrentDurability = 0.0;
                        }
                        else
                        {
                            // Permanently broken & removed; auto-equip spare if available
                            await RemoveAndEquipReplacement(db, userBoost);
                        }
                    }
                }
            }

            await db.SaveChangesAsync();
            return brokenItems;
        }

        public Task<FishingSnapEvent> ConsumeItemsOnLineSnap(string userId, string username)
        {
            return ApplySnap(userId, username, "Line", SnapLossType.Line);
        }

        public Task<FishingSnapEvent> ConsumeItemsOnRodSnap(string userId, string username)
        {
            return ApplySnap(userId, username, "Rod", SnapLossType.Rod);
        }

        public Task<FishingSnapEvent> ConsumeItemsOnReelJam(string userId, string username)
        {
            return ApplySnap(userId, username, "Reel", SnapLossType.Reel);
        }

        public Task<FishingSnapEvent> ConsumeItemsOnTackleBoxLost(string userId, string username)
        {
            return ApplySnap(userId, username, "TackleBox", SnapLossType.TackleBox);
        }

        public Task<FishingSnapEvent> ConsumeItemsOnNetBreak(string userId, string username)
        {
            return ApplySnap(userId, username, "Net", SnapLossType.Net);
        }

        public int CalculateRepairCost(FishingShopItem shopItem, double currentDurability, double repairCostMultiplier)
        {
            if (repairCostMultiplier <= 0 || !shopItem.MaxDurability.HasValue || shopItem.MaxDurability.Value <= 0)
            {
                return 0;
            }

            var missingRatio = Math.Max(0.0, (shopItem.MaxDurability.Value - currentDurability) / shopItem.MaxDurability.Value);
            if (missingRatio <= 0.0)
            {
                return 0;
            }

            return Math.Max(1, (int)Math.Ceiling(shopItem.Cost * missingRatio * repairCostMultiplier));
        }

        public async Task<int> RepairItem(string userId, int userBoostId, double? repairCostMultiplier = null)
        {
            using var userLock = await _userLocks.AcquireAsync(userId, CancellationToken.None);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var multiplier = repairCostMultiplier ?? (await db.FishingSettings.Find(s => true).FirstOrDefaultAsync())?.RepairCostMultiplier ?? 0.0;
            if (multiplier <= 0)
            {
                throw new InvalidOperationException("Repair feature is currently disabled");
            }

            var boost = await FindUserBoost(db, userId, userBoostId)
                ?? throw new InvalidOperationException("Item not found");

            ReconcileDurability(boost);

            var shopItem = boost.ShopItem;
            if (shopItem == null || !shopItem.MaxDurability.HasValue || !boost.CurrentDurability.HasValue)
            {
                throw new InvalidOperationException("This item does not have durability");
            }

            if (boost.CurrentDurability.Value >= shopItem.MaxDurability.Value)
            {
                throw new InvalidOperationException("Item is already at full durability");
            }

            var cost = CalculateRepairCost(shopItem, boost.CurrentDurability.Value, multiplier);
            var gold = await db.FishingGolds.Find(g => g.UserId == userId).FirstOrDefaultAsync();
            if (gold == null || gold.TotalGold < cost)
            {
                throw new InvalidOperationException("Not enough gold to repair item");
            }

            gold.TotalGold -= cost;
            boost.CurrentDurability = (double)shopItem.MaxDurability.Value;

            await db.SaveChangesAsync();
            return cost;
        }

        public async Task<int> RepairAllEquippedItems(string userId, double? repairCostMultiplier = null)
        {
            using var userLock = await _userLocks.AcquireAsync(userId, CancellationToken.None);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var multiplier = repairCostMultiplier ?? (await db.FishingSettings.Find(s => true).FirstOrDefaultAsync())?.RepairCostMultiplier ?? 0.0;
            if (multiplier <= 0)
            {
                throw new InvalidOperationException("Repair feature is currently disabled");
            }

            var equipped = await db.UserFishingBoosts.GetAsync(
                b => b.UserId == userId && b.IsEquipped,
                includeProperties: ShopItemInclude);

            var repairable = new List<(UserFishingBoost Boost, int Cost)>();
            var totalCost = 0;

            foreach (var boost in equipped)
            {
                ReconcileDurability(boost);
                var shopItem = boost.ShopItem;
                if (shopItem?.MaxDurability.HasValue == true && boost.CurrentDurability.HasValue && boost.CurrentDurability.Value < shopItem.MaxDurability.Value)
                {
                    var cost = CalculateRepairCost(shopItem, boost.CurrentDurability.Value, multiplier);
                    repairable.Add((boost, cost));
                    totalCost += cost;
                }
            }

            if (repairable.Count == 0)
            {
                return 0;
            }

            var gold = await db.FishingGolds.Find(g => g.UserId == userId).FirstOrDefaultAsync();
            if (gold == null || gold.TotalGold < totalCost)
            {
                throw new InvalidOperationException("Not enough gold to repair all items");
            }

            gold.TotalGold -= totalCost;
            foreach (var (boost, _) in repairable)
            {
                boost.CurrentDurability = (double)boost.ShopItem!.MaxDurability!.Value;
            }

            await db.SaveChangesAsync();
            return totalCost;
        }

        private enum SnapLossType
        {
            Line,
            Rod,
            Reel,
            TackleBox,
            Net
        }

        private async Task<FishingSnapEvent> ApplySnap(string userId, string username, string snapType, SnapLossType lossType)
        {
            using var userLock = await _userLocks.AcquireAsync(userId, CancellationToken.None);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var lossResult = await ApplySnapLosses(db, userId, lossType);
            var snapEvent = new FishingSnapEvent
            {
                UserId = userId,
                Username = username,
                SnapType = snapType,
                TotalGoldLost = decimal.Round(lossResult.TotalGoldLost, 2, MidpointRounding.AwayFromZero),
                LostItemCount = lossResult.LostItems.Count,
                LostItemsJson = JsonSerializer.Serialize(lossResult.LostItems),
                SnappedAt = DateTime.UtcNow
            };

            db.FishingSnapEvents.Add(snapEvent);
            await db.SaveChangesAsync();
            return snapEvent;
        }

        private static async Task<FishingSnapLossResult> ApplySnapLosses(IUnitOfWork db, string userId, SnapLossType lossType)
        {
            var lossResult = new FishingSnapLossResult();

            var equippedItems = await db.UserFishingBoosts.GetAsync(
                b => b.UserId == userId && b.IsEquipped,
                includeProperties: "ShopItem");

            foreach (var item in equippedItems)
            {
                ReconcileDurability(item);

                // Unbreakable items never break or get lost during accidents
                if (item.ShopItem?.DisableBreaking == true)
                {
                    continue;
                }

                var slot = item.ShopItem?.EquipmentSlot;

                var isTargetSlotLost = lossType switch
                {
                    SnapLossType.Rod => slot == EquipmentSlot.Rod || slot == EquipmentSlot.Line || slot == EquipmentSlot.Hook,
                    SnapLossType.Line => slot == EquipmentSlot.Line || slot == EquipmentSlot.Hook,
                    SnapLossType.Reel => slot == EquipmentSlot.Reel,
                    SnapLossType.TackleBox => slot == EquipmentSlot.TackleBox,
                    SnapLossType.Net => slot == EquipmentSlot.Net,
                    _ => false
                };

                if (isTargetSlotLost)
                {
                    RegisterFullItemLoss(lossResult, item);
                    db.UserFishingBoosts.Remove(item);
                    continue;
                }

                if (lossType != SnapLossType.Line && lossType != SnapLossType.Rod)
                {
                    continue;
                }

                if (slot != EquipmentSlot.Bait && slot != EquipmentSlot.Lure)
                {
                    continue;
                }

                if (item.RemainingUses == -1)
                {
                    // Unlimited bait/lure are fully lost on snap.
                    RegisterFullItemLoss(lossResult, item);
                    db.UserFishingBoosts.Remove(item);
                    continue;
                }

                var remainingUsesBefore = item.RemainingUses;
                if (item.RemainingUses > 0)
                {
                    item.RemainingUses--;
                    RegisterUseLoss(lossResult, item, remainingUsesBefore - item.RemainingUses, remainingUsesBefore, item.RemainingUses);
                }

                if (item.RemainingUses <= 0)
                {
                    await RemoveAndEquipReplacement(db, item);
                }
            }

            return lossResult;
        }

        private static async Task RemoveAndEquipReplacement(IUnitOfWork db, UserFishingBoost item)
        {
            item.IsEquipped = false;
            db.UserFishingBoosts.Remove(item);

            var replacement = await db.UserFishingBoosts
                .Find(b => b.UserId == item.UserId &&
                           b.ShopItemId == item.ShopItemId &&
                           b.Id != item.Id &&
                           !b.IsEquipped &&
                           b.RemainingUses != 0)
                .OrderBy(b => b.PurchasedAt)
                .ThenBy(b => b.Id)
                .FirstOrDefaultAsync();

            if (replacement != null)
            {
                replacement.IsEquipped = true;
            }
        }

        private static Task<UserFishingBoost?> FindUserBoost(IUnitOfWork db, string userId, int userBoostId)
        {
            return db.UserFishingBoosts
                .Find(b => b.Id == userBoostId && b.UserId == userId)
                .Include(b => b.ShopItem)
                .FirstOrDefaultAsync();
        }

        private static void ReconcileDurability(UserFishingBoost boost)
        {
            var shopItem = boost.ShopItem;
            if (shopItem == null) return;

            if (shopItem.MaxDurability.HasValue)
            {
                if (!boost.CurrentDurability.HasValue)
                {
                    boost.CurrentDurability = (double)shopItem.MaxDurability.Value;
                }
                else if (boost.CurrentDurability.Value > shopItem.MaxDurability.Value)
                {
                    boost.CurrentDurability = (double)shopItem.MaxDurability.Value;
                }
            }
            else
            {
                boost.CurrentDurability = null;
            }
        }

        private static UserFishingBoost NewBoost(string userId, FishingShopItem shopItem)
        {
            return new UserFishingBoost
            {
                UserId = userId,
                ShopItemId = shopItem.Id,
                RemainingUses = shopItem.MaxUses ?? -1, // -1 means unlimited
                CurrentDurability = shopItem.MaxDurability.HasValue ? (double)shopItem.MaxDurability.Value : null
            };
        }

        private static void RegisterFullItemLoss(FishingSnapLossResult lossResult, UserFishingBoost item)
        {
            var cost = item.ShopItem?.Cost ?? 0;
            var valueLost = decimal.Round(cost, 2, MidpointRounding.AwayFromZero);

            lossResult.TotalGoldLost += valueLost;
            lossResult.LostItems.Add(new FishingSnapLostItem
            {
                UserBoostId = item.Id,
                ShopItemId = item.ShopItemId,
                ItemName = item.ShopItem?.Name ?? "Unknown Item",
                EquipmentSlot = item.ShopItem?.EquipmentSlot?.ToString() ?? "Unknown",
                ItemCostAtSnap = cost,
                UsesLost = item.RemainingUses == -1 ? -1 : Math.Max(1, item.RemainingUses),
                RemainingUsesBefore = item.RemainingUses,
                RemainingUsesAfter = null,
                ItemRemoved = true,
                GoldValueLost = valueLost
            });
        }

        private static void RegisterUseLoss(
            FishingSnapLossResult lossResult,
            UserFishingBoost item,
            int usesLost,
            int remainingUsesBefore,
            int remainingUsesAfter)
        {
            var perUseLoss = CalculatePerUseLoss(item.ShopItem);
            var valueLost = decimal.Round(perUseLoss * usesLost, 2, MidpointRounding.AwayFromZero);

            lossResult.TotalGoldLost += valueLost;
            lossResult.LostItems.Add(new FishingSnapLostItem
            {
                UserBoostId = item.Id,
                ShopItemId = item.ShopItemId,
                ItemName = item.ShopItem?.Name ?? "Unknown Item",
                EquipmentSlot = item.ShopItem?.EquipmentSlot?.ToString() ?? "Unknown",
                ItemCostAtSnap = item.ShopItem?.Cost ?? 0,
                UsesLost = usesLost,
                RemainingUsesBefore = remainingUsesBefore,
                RemainingUsesAfter = remainingUsesAfter,
                ItemRemoved = false,
                GoldValueLost = valueLost
            });
        }

        private static decimal CalculatePerUseLoss(FishingShopItem? item)
        {
            if (item == null)
            {
                return 0m;
            }

            if (item.MaxUses.HasValue && item.MaxUses.Value > 0)
            {
                return (decimal)item.Cost / item.MaxUses.Value;
            }

            return item.Cost;
        }
    }
}
