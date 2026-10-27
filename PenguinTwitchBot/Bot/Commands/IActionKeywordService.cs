using PenguinTwitchBot.Database.Bot.Models.Commands;

namespace PenguinTwitchBot.Bot.Commands
{
    public interface IActionKeywordService
    {
        Task<List<ActionKeyword>> GetAllAsync();
        Task<ActionKeyword?> GetByIdAsync(Guid id);
        Task<ActionKeyword> AddAsync(ActionKeyword keyword);
        Task<ActionKeyword> UpdateAsync(ActionKeyword keyword);
        Task DeleteAsync(Guid id);
        Task<bool> KeywordExistsAsync(string keywordName);
        Task<ActionKeyword?> GetByKeywordNameAsync(string keywordName);
        Task<List<ActionKeyword>> GetAllEnabledAsync();
    }
}
