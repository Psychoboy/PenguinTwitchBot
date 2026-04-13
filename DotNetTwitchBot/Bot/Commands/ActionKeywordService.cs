using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Commands.Actions;
using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands
{
    public class ActionKeywordService(
        IUnitOfWork unitOfWork,
        IActionManagementService actionManagementService,
        IActionKeywordCache keywordCache,
        ILogger<ActionKeywordService> logger) : IActionKeywordService
    {
        public async Task<List<ActionKeyword>> GetAllAsync()
        {
            return await unitOfWork.ActionKeywords.GetAsync(includeProperties: "PointType");
        }

        public async Task<List<ActionKeyword>> GetAllEnabledAsync()
        {
            return await unitOfWork.ActionKeywords.GetAsync(
                filter: k => !k.Disabled,
                includeProperties: "PointType");
        }

        public async Task<ActionKeyword?> GetByIdAsync(int id)
        {
            return await unitOfWork.ActionKeywords.GetByIdAsync(id);
        }

        public async Task<ActionKeyword?> GetByKeywordNameAsync(string keywordName)
        {
            var normalizedKeywordName = keywordName.ToLower();

            var result = await unitOfWork.ActionKeywords.GetAsync(
                filter: k => k.CommandName.ToLower() == normalizedKeywordName,
                includeProperties: "PointType");
            return result.FirstOrDefault();
        }

        public async Task<ActionKeyword> AddAsync(ActionKeyword keyword)
        {
            // Ensure category is never null
            keyword.Category = keyword.Category ?? string.Empty;

            // Clear navigation property to avoid EF tracking issues
            keyword.PointType = null;

            await unitOfWork.ActionKeywords.AddAsync(keyword);
            await unitOfWork.SaveChangesAsync();

            keywordCache.InvalidateCache();

            return keyword;
        }

        public async Task<ActionKeyword> UpdateAsync(ActionKeyword keyword)
        {
            // Check if the keyword name has changed
            if (keyword.Id.HasValue)
            {
                var existingKeyword = await unitOfWork.ActionKeywords.Find(x => x.Id == keyword.Id.Value).AsNoTracking().FirstOrDefaultAsync();
                if (existingKeyword != null && existingKeyword.CommandName != keyword.CommandName)
                {
                    // Keyword name has changed - update trigger configurations
                    await unitOfWork.Actions.UpdateKeywordTriggerConfigurationsForRenamedKeyword(keyword.Id.Value, existingKeyword.CommandName, keyword.CommandName);
                }
            }

            // Ensure category is never null
            keyword.Category = keyword.Category ?? string.Empty;

            // Clear navigation property to avoid EF tracking issues
            keyword.PointType = null;

            unitOfWork.ActionKeywords.Update(keyword);
            await unitOfWork.SaveChangesAsync();

            keywordCache.InvalidateCache();

            return keyword;
        }

        public async Task DeleteAsync(int id)
        {
            var keyword = await unitOfWork.ActionKeywords.GetByIdAsync(id);
            if (keyword != null)
            {
                try
                {
                    // Delete all triggers that reference this keyword
                    await actionManagementService.DeleteTriggersForKeywordAsync(id);
                    logger.LogInformation("Deleted triggers for keyword {KeywordName} (ID: {KeywordId})", keyword.CommandName, id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error deleting triggers for keyword {KeywordName} (ID: {KeywordId})", keyword.CommandName, id);
                    // Continue with keyword deletion even if trigger deletion fails
                }

                unitOfWork.ActionKeywords.Remove(keyword);
                await unitOfWork.SaveChangesAsync();

                keywordCache.InvalidateCache();
            }
        }

        public async Task<bool> KeywordExistsAsync(string keywordName)
        {
            var actionKeyword = await GetByKeywordNameAsync(keywordName);
            return actionKeyword != null;
        }
    }
}
