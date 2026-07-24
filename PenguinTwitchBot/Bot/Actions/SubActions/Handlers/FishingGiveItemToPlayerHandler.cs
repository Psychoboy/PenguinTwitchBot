using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class FishingGiveItemToPlayerHandler(
        IFishingInventoryService fishingInventoryService,
        IFishingShopService fishingShopService,
        IViewerFeature viewerFeature,
        ILogger<FishingGiveItemToPlayerHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.FishingGiveItemToPlayer;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not FishingGiveItemToPlayerType giveItem)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type provided to FishingGiveItemToPlayerHandler");
            }

            var targetName = VariableReplacer.ReplaceVariables(giveItem.TargetName, variables).Trim();
            if (string.IsNullOrWhiteSpace(targetName))
            {
                throw new SubActionUserFacingException(subAction, "Target Username is required.");
            }

            if (!giveItem.ShopItemId.HasValue || giveItem.ShopItemId.Value <= 0)
            {
                throw new SubActionHandlerException(subAction, "A fishing item is required");
            }

            var targetViewer = await viewerFeature.GetViewerByUserName(targetName)
                ?? throw new SubActionUserFacingException(subAction, "Target user not found: {0}", targetName);

            var targetUserId = targetViewer.UserId;
            await fishingInventoryService.GiveItemToUser(targetUserId, giveItem.ShopItemId.Value);

            var shopItemName = giveItem.ShopItemName;
            if (string.IsNullOrWhiteSpace(shopItemName))
            {
                var shopItems = await fishingShopService.GetAllShopItems();
                shopItemName = shopItems.FirstOrDefault(item => item.Id == giveItem.ShopItemId.Value)?.Name ?? $"Item #{giveItem.ShopItemId.Value}";
            }

            logger.LogInformation(
                "Fishing sub action granted item {ItemName} (ID: {ItemId}) to user {Username} ({UserId})",
                shopItemName,
                giveItem.ShopItemId.Value,
                targetViewer.Username,
                targetUserId);

            context?.LogMessage(
                subActionIndex,
                $"Gave fishing item '{shopItemName}' (ID: {giveItem.ShopItemId.Value}) to user '{targetViewer.Username}' ({targetUserId}).");
        }
    }
}
