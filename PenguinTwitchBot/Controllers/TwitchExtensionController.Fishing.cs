using Microsoft.AspNetCore.Mvc;
using PenguinTwitchBot.Database.Bot.Models.Fishing;

namespace PenguinTwitchBot.Controllers;

public partial class TwitchExtensionController
{
    #region Fishing

    [HttpGet("fishing-tournaments")]
    [HttpGet("fishing/tournaments")]
    public async Task<IActionResult> GetFishingTournaments([FromQuery] int top = 5)
    {
        var safeTop = Math.Clamp(top, 1, 20);

        var tournaments = await _fishingService.GetCurrentFishingTournaments();
        var activeTournaments = tournaments
            .Where(t => t.Status == FishingTournamentStatus.Active)
            .ToList();

        var response = new List<ExtensionTournamentResponse>(activeTournaments.Count);
        foreach (var tournament in activeTournaments)
        {
            var standings = await _fishingService.GetFishingTournamentStandings(tournament.Id, safeTop);
            response.Add(new ExtensionTournamentResponse(
                tournament.Id,
                tournament.Name,
                tournament.PrimaryScoreCategory.ToString(),
                standings.Select(s => new ExtensionTournamentStandingResponse(
                    s.Rank,
                    s.Username,
                    s.Score,
                    s.CatchCount)).ToList()));
        }

        return Ok(response);
    }

    [HttpGet("recent-catches")]
    [HttpGet("fishing/recent-catches")]
    public async Task<IActionResult> GetRecentCatches([FromQuery] int count = 20)
    {
        var safeCount = Math.Clamp(count, 1, 50);

        var catches = await _fishingService.GetRecentCatches(safeCount);

        var response = catches.Select(c => new ExtensionRecentCatchResponse(
            c.Username,
            c.FishType?.Name ?? "Unknown",
            c.Weight,
            c.CaughtAt)).ToList();

        return Ok(response);
    }

    [HttpGet("fishing/store")]
    public async Task<IActionResult> GetFishingStore()
    {
        var shopItems = await _fishingShopService.GetAllShopItems();
        var response = shopItems.Select(item => new ExtensionFishingShopItemResponse(
            Id: item.Id,
            Name: item.Name,
            Description: item.Description,
            Cost: item.Cost,
            EquipmentSlot: item.EquipmentSlot?.ToString(),
            BoostType: item.BoostType.ToString(),
            BoostAmount: item.BoostAmount,
            MaxUses: item.MaxUses,
            MaxDurability: item.MaxDurability,
            TargetFishName: item.TargetFishType?.Name)).ToList();

        return Ok(response);
    }

    [HttpGet("fishing/viewer")]
    public async Task<IActionResult> GetFishingViewer()
    {
        var claims = GetClaims();
        if (claims == null)
        {
            return Unauthorized(new { error = "auth_required", message = "Missing or invalid authorization token." });
        }

        if (!claims.HasUserId)
        {
            return Ok(new ExtensionFishingViewerResponse(
                Username: string.Empty,
                TotalGold: 0,
                Items: new List<ExtensionUserFishingBoostResponse>()));
        }

        var viewer = await TwitchExtensionSecurity.ResolveViewerAsync(claims.UserId!, _viewerFeature, _twitchService, _logger);
        var gold = await _fishingService.GetUserGold(claims.UserId!);
        var boosts = await _fishingInventoryService.GetUserBoosts(claims.UserId!);
        var settings = await _fishingService.GetSettings();
        var repairMultiplier = settings?.RepairCostMultiplier ?? 0.0;

        var items = boosts.Select(b =>
        {
            var canRepair = repairMultiplier > 0
                && b.ShopItem?.MaxDurability.HasValue == true
                && (b.CurrentDurability ?? b.ShopItem.MaxDurability.Value) < b.ShopItem.MaxDurability.Value;
            int? repairCost = canRepair
                ? _fishingInventoryService.CalculateRepairCost(b.ShopItem!, b.CurrentDurability ?? b.ShopItem!.MaxDurability!.Value, repairMultiplier)
                : null;

            return new ExtensionUserFishingBoostResponse(
                Id: b.Id,
                ShopItemId: b.ShopItemId,
                Name: b.ShopItem?.Name ?? "Unknown Item",
                Description: b.ShopItem?.Description ?? string.Empty,
                EquipmentSlot: b.ShopItem?.EquipmentSlot?.ToString(),
                IsEquipped: b.IsEquipped,
                RemainingUses: b.RemainingUses,
                CurrentDurability: b.CurrentDurability,
                MaxDurability: b.ShopItem?.MaxDurability,
                IsBroken: b.IsBroken,
                BoostSummary: FormatBoostSummary(b.ShopItem),
                CanRepair: canRepair,
                RepairCost: repairCost);
        }).ToList();

        return Ok(new ExtensionFishingViewerResponse(
            Username: viewer?.DisplayName ?? claims.UserId!,
            TotalGold: gold?.TotalGold ?? 0,
            Items: items));
    }

    [HttpPost("fishing/store/buy")]
    public async Task<IActionResult> BuyFishingItem([FromBody] ExtensionBuyItemRequest request)
    {
        var claims = GetClaims();
        if (claims == null || !claims.HasUserId)
        {
            return Unauthorized(new { error = "identity_required", message = "Please grant identity permission to purchase items." });
        }

        try
        {
            var quantity = Math.Clamp(request.Quantity, 1, 100);
            await _fishingInventoryService.PurchaseBoost(claims.UserId!, request.ShopItemId, quantity);
            return Ok(new { success = true, message = "Item purchased successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purchasing shop item {ItemId} for user {UserId}", request.ShopItemId, claims.UserId);
            return StatusCode(500, new { success = false, message = "Unexpected error purchasing item." });
        }
    }

    [HttpPost("fishing/inventory/equip")]
    public async Task<IActionResult> EquipFishingItem([FromBody] ExtensionEquipItemRequest request)
    {
        var claims = GetClaims();
        if (claims == null || !claims.HasUserId)
        {
            return Unauthorized(new { error = "identity_required", message = "Please grant identity permission to equip items." });
        }

        try
        {
            await _fishingInventoryService.EquipItem(claims.UserId!, request.UserBoostId);
            return Ok(new { success = true, message = "Item equipped successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error equipping item {BoostId} for user {UserId}", request.UserBoostId, claims.UserId);
            return StatusCode(500, new { success = false, message = "Unexpected error equipping item." });
        }
    }

    [HttpPost("fishing/inventory/unequip")]
    public async Task<IActionResult> UnequipFishingItem([FromBody] ExtensionEquipItemRequest request)
    {
        var claims = GetClaims();
        if (claims == null || !claims.HasUserId)
        {
            return Unauthorized(new { error = "identity_required", message = "Please grant identity permission to unequip items." });
        }

        try
        {
            await _fishingInventoryService.UnequipItem(claims.UserId!, request.UserBoostId);
            return Ok(new { success = true, message = "Item unequipped successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unequipping item {BoostId} for user {UserId}", request.UserBoostId, claims.UserId);
            return StatusCode(500, new { success = false, message = "Unexpected error unequipping item." });
        }
    }

    [HttpPost("fishing/inventory/repair")]
    public async Task<IActionResult> RepairFishingItem([FromBody] ExtensionRepairItemRequest request)
    {
        var claims = GetClaims();
        if (claims == null || !claims.HasUserId)
        {
            return Unauthorized(new { error = "identity_required", message = "Please grant identity permission to repair items." });
        }

        try
        {
            var viewer = await TwitchExtensionSecurity.ResolveViewerAsync(claims.UserId!, _viewerFeature, _twitchService, _logger);
            var goldCharged = await _fishingInventoryService.RepairItem(claims.UserId!, request.UserBoostId, username: viewer?.Username);
            return Ok(new { success = true, goldPaid = goldCharged, message = $"Item repaired successfully for {goldCharged} gold." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error repairing item {BoostId} for user {UserId}", request.UserBoostId, claims.UserId);
            return StatusCode(500, new { success = false, message = "Unexpected error repairing item." });
        }
    }

    #endregion

    private static string FormatBoostSummary(FishingShopItem? item)
    {
        if (item == null) return string.Empty;
        var parts = new List<string>(3);
        if (item.BoostAmount != 0)
        {
            parts.Add($"{item.BoostType}: {item.BoostAmount:+0.##;-0.##}%");
        }
        if (item.BoostType2.HasValue && item.BoostAmount2.HasValue && item.BoostAmount2.Value != 0)
        {
            parts.Add($"{item.BoostType2.Value}: {item.BoostAmount2.Value:+0.##;-0.##}%");
        }
        if (item.BoostType3.HasValue && item.BoostAmount3.HasValue && item.BoostAmount3.Value != 0)
        {
            parts.Add($"{item.BoostType3.Value}: {item.BoostAmount3.Value:+0.##;-0.##}%");
        }
        return string.Join(", ", parts);
    }

}
