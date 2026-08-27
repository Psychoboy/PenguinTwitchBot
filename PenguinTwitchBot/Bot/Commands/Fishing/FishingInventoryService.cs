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
            return await db.UserFishingBoosts.GetAsync(b => b.UserId == userId, includeProperties: ShopItemInclude);
        }

        public async Task<List<UserFishingBoost>> GetUserEquippedItems(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.UserFishingBoosts.GetAsync(b => b.UserId == userId && b.IsEquipped, includeProperties: ShopItemInclude);
        }

        public async Task<Dictionary<EquipmentSlot, UserFishingBoost>> GetUserEquipmentBySlot(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var equipped = await db.UserFishingBoosts.GetAsync(
                b => b.UserId == userId && b.IsEquipped && b.ShopItem!.EquipmentSlot != null,
                includeProperties: ShopItemInclude);

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

            gold.TotalGold += FishingInventorySellRules.GetSellPrice(userBoost!.ShopItem);
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

        public Task<FishingSnapEvent> ConsumeItemsOnLineSnap(string userId, string username)
        {
            return ApplySnap(userId, username, "Line", includeRodLoss: false);
        }

        public Task<FishingSnapEvent> ConsumeItemsOnRodSnap(string userId, string username)
        {
            return ApplySnap(userId, username, "Rod", includeRodLoss: true);
        }

        private async Task<FishingSnapEvent> ApplySnap(string userId, string username, string snapType, bool includeRodLoss)
        {
            using var userLock = await _userLocks.AcquireAsync(userId, CancellationToken.None);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var lossResult = await ApplySnapLosses(db, userId, includeRodLoss);
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

        private static async Task<FishingSnapLossResult> ApplySnapLosses(IUnitOfWork db, string userId, bool includeRodLoss)
        {
            var lossResult = new FishingSnapLossResult();

            var equippedItems = await db.UserFishingBoosts.GetAsync(
                b => b.UserId == userId && b.IsEquipped,
                includeProperties: "ShopItem");

            foreach (var item in equippedItems)
            {
                var slot = item.ShopItem?.EquipmentSlot;

                // Rods only break on a rod snap; line and hook are always lost.
                var alwaysLost = (includeRodLoss && slot == EquipmentSlot.Rod)
                    || slot == EquipmentSlot.Line
                    || slot == EquipmentSlot.Hook;

                if (alwaysLost)
                {
                    RegisterFullItemLoss(lossResult, item);
                    db.UserFishingBoosts.Remove(item);
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

        private static UserFishingBoost NewBoost(string userId, FishingShopItem shopItem)
        {
            return new UserFishingBoost
            {
                UserId = userId,
                ShopItemId = shopItem.Id,
                RemainingUses = shopItem.MaxUses ?? -1 // -1 means unlimited
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
