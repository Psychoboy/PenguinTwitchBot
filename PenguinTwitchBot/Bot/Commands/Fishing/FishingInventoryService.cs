using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Commands.Fishing
{
    public class FishingInventoryService : IFishingInventoryService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FishingInventoryService> _logger;

        public FishingInventoryService(IServiceScopeFactory scopeFactory, ILogger<FishingInventoryService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<List<UserFishingBoost>> GetUserBoosts(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .ThenInclude(s => s!.TargetFishType)
                .Where(b => b.UserId == userId)
                .ToListAsync();
        }

        public async Task<List<UserFishingBoost>> GetUserEquippedItems(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .ThenInclude(s => s!.TargetFishType)
                .Where(b => b.UserId == userId && b.IsEquipped)
                .ToListAsync();
        }

        public async Task<Dictionary<EquipmentSlot, UserFishingBoost>> GetUserEquipmentBySlot(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var equipped = await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .ThenInclude(s => s!.TargetFishType)
                .Where(b => b.UserId == userId && b.IsEquipped && b.ShopItem!.EquipmentSlot != null)
                .ToListAsync();

            return equipped.Where(e => e.ShopItem?.EquipmentSlot != null)
                          .ToDictionary(e => e.ShopItem!.EquipmentSlot!.Value, e => e);
        }

        public async Task PurchaseBoost(string userId, int shopItemId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var shopItem = await context.FishingShopItems.FindAsync(shopItemId);
            if (shopItem == null || !shopItem.Enabled || shopItem.IsAdminOnly)
            {
                throw new InvalidOperationException("Shop item not found, disabled, or not available for purchase");
            }

            var gold = await context.FishingGolds.FirstOrDefaultAsync(g => g.UserId == userId);
            if (gold == null || gold.TotalGold < shopItem.Cost)
            {
                throw new InvalidOperationException("Not enough gold");
            }

            gold.TotalGold -= shopItem.Cost;

            var userBoost = new UserFishingBoost
            {
                UserId = userId,
                ShopItemId = shopItemId,
                RemainingUses = shopItem.MaxUses ?? -1 // -1 means unlimited
            };

            context.UserFishingBoosts.Add(userBoost);

            await context.SaveChangesAsync();
        }

        public async Task GiveItemToUser(string userId, int shopItemId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var shopItem = await context.FishingShopItems.FindAsync(shopItemId);
            if (shopItem == null)
            {
                throw new InvalidOperationException("Shop item not found");
            }

            var userBoost = new UserFishingBoost
            {
                UserId = userId,
                ShopItemId = shopItemId,
                RemainingUses = shopItem.MaxUses ?? -1 // -1 means unlimited
            };

            context.UserFishingBoosts.Add(userBoost);

            await context.SaveChangesAsync();
        }

        public async Task SellItem(string userId, int userBoostId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userBoost = await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .FirstOrDefaultAsync(b => b.Id == userBoostId && b.UserId == userId);

            var sellEligibility = FishingInventorySellRules.GetSellEligibility(userBoost);
            if (sellEligibility != SellEligibilityReason.Eligible)
            {
                throw new InvalidOperationException(FishingInventorySellRules.GetSellFailureMessage(sellEligibility));
            }

            var sellPrice = FishingInventorySellRules.GetSellPrice(userBoost!.ShopItem);
            var gold = await context.FishingGolds.FirstOrDefaultAsync(g => g.UserId == userId);
            if (gold == null)
            {
               throw new InvalidOperationException("User gold record not found");
            }
            else
            {
                gold.TotalGold += sellPrice;
            }
            context.UserFishingBoosts.Remove(userBoost);
            await context.SaveChangesAsync();
        }

        public async Task EquipItem(string userId, int userBoostId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var userBoost = await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .FirstOrDefaultAsync(b => b.Id == userBoostId && b.UserId == userId);

            if (userBoost == null)
            {
                throw new InvalidOperationException("Item not found");
            }

            // Check if item has expired (consumable items with 0 uses)
            if (userBoost.ShopItem!.IsConsumable && userBoost.RemainingUses == 0)
            {
                throw new InvalidOperationException("Item has no remaining uses");
            }

            // If item has a slot, unequip any item in that slot
            if (userBoost.ShopItem.EquipmentSlot.HasValue)
            {
                var slotItems = await context.UserFishingBoosts
                    .Include(b => b.ShopItem)
                    .Where(b => b.UserId == userId && 
                               b.IsEquipped && 
                               b.ShopItem!.EquipmentSlot == userBoost.ShopItem.EquipmentSlot)
                    .ToListAsync();

                foreach (var item in slotItems)
                {
                    item.IsEquipped = false;
                }
            }

            userBoost.IsEquipped = true;
            await context.SaveChangesAsync();
        }

        public async Task UnequipItem(string userId, int userBoostId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var userBoost = await context.UserFishingBoosts
                .FirstOrDefaultAsync(b => b.Id == userBoostId && b.UserId == userId);

            if (userBoost == null)
            {
                throw new InvalidOperationException("Item not found");
            }

            userBoost.IsEquipped = false;
            await context.SaveChangesAsync();
        }

        public async Task ConsumeItemUse(string userId, int userBoostId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var userBoost = await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .FirstOrDefaultAsync(b => b.Id == userBoostId && b.UserId == userId);

            if (userBoost == null || !userBoost.IsEquipped)
            {
                return;
            }

            // Skip if unlimited uses
            if (userBoost.RemainingUses == -1)
            {
                userBoost.LastUsedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
                return;
            }

            // Only decrement if we have uses remaining (prevent going below 0)
            if (userBoost.RemainingUses > 0)
            {
                userBoost.RemainingUses--;
            }
            userBoost.LastUsedAt = DateTime.UtcNow;

            // If consumable and no uses left, remove the item
            if (userBoost.ShopItem!.IsConsumable && userBoost.RemainingUses <= 0)
            {
                userBoost.IsEquipped = false;
                // Optionally delete the item entirely
                context.UserFishingBoosts.Remove(userBoost);
            }
            else if (userBoost.RemainingUses <= 0)
            {
                // Non-consumable items just get unequipped when out of uses
                userBoost.IsEquipped = false;
            }

            await context.SaveChangesAsync();
        }

        public async Task<FishingSnapEvent> ConsumeItemsOnLineSnap(string userId, string username)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var lossResult = await ApplySnapLosses(context, userId, includeRodLoss: false);
            var snapEvent = BuildSnapEvent(userId, username, "Line", lossResult);
            context.FishingSnapEvents.Add(snapEvent);
            await context.SaveChangesAsync();
            return snapEvent;
        }

        public async Task<FishingSnapEvent> ConsumeItemsOnRodSnap(string userId, string username)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var lossResult = await ApplySnapLosses(context, userId, includeRodLoss: true);
            var snapEvent = BuildSnapEvent(userId, username, "Rod", lossResult);
            context.FishingSnapEvents.Add(snapEvent);
            await context.SaveChangesAsync();
            return snapEvent;
        }

        private static FishingSnapEvent BuildSnapEvent(string userId, string username, string snapType, FishingSnapLossResult lossResult)
        {
            return new FishingSnapEvent
            {
                UserId = userId,
                Username = username,
                SnapType = snapType,
                TotalGoldLost = decimal.Round(lossResult.TotalGoldLost, 2, MidpointRounding.AwayFromZero),
                LostItemCount = lossResult.LostItems.Count,
                LostItemsJson = JsonSerializer.Serialize(lossResult.LostItems),
                SnappedAt = DateTime.UtcNow
            };
        }

        private static async Task<FishingSnapLossResult> ApplySnapLosses(ApplicationDbContext context, string userId, bool includeRodLoss)
        {
            var lossResult = new FishingSnapLossResult();

            var equippedItems = await context.UserFishingBoosts
                .Include(b => b.ShopItem)
                .Where(b => b.UserId == userId && b.IsEquipped)
                .ToListAsync();

            if (includeRodLoss)
            {
                foreach (var rodItem in equippedItems.Where(i => i.ShopItem?.EquipmentSlot == EquipmentSlot.Rod))
                {
                    RegisterFullItemLoss(lossResult, rodItem);
                    context.UserFishingBoosts.Remove(rodItem);
                }
            }

            foreach (var item in equippedItems)
            {
                var slot = item.ShopItem?.EquipmentSlot;

                if (includeRodLoss && slot == EquipmentSlot.Rod)
                {
                    continue;
                }

                // Line/hook are always lost on a snapped line.
                if (slot == EquipmentSlot.Line || slot == EquipmentSlot.Hook)
                {
                    RegisterFullItemLoss(lossResult, item);
                    context.UserFishingBoosts.Remove(item);
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
                    context.UserFishingBoosts.Remove(item);
                    continue;
                }

                var remainingUsesBefore = item.RemainingUses;
                if (item.RemainingUses > 0)
                {
                    item.RemainingUses--;
                }
                var remainingUsesAfter = item.RemainingUses;

                item.LastUsedAt = DateTime.UtcNow;

                var usesLost = Math.Max(0, remainingUsesBefore - remainingUsesAfter);
                if (usesLost > 0)
                {
                    RegisterUseLoss(lossResult, item, usesLost, remainingUsesBefore, remainingUsesAfter);
                }

                if (item.ShopItem?.IsConsumable == true && item.RemainingUses <= 0)
                {
                    item.IsEquipped = false;
                    context.UserFishingBoosts.Remove(item);
                }
                else if (item.RemainingUses <= 0)
                {
                    item.IsEquipped = false;
                }
            }

            return lossResult;
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
