using DotNetTwitchBot.Bot.Models.Commands;

namespace DotNetTwitchBot.Bot.Commands
{
    public interface IActionKeywordService
    {
        Task<List<ActionKeyword>> GetAllAsync();
        Task<ActionKeyword?> GetByIdAsync(int id);
        Task<ActionKeyword> AddAsync(ActionKeyword keyword);
        Task<ActionKeyword> UpdateAsync(ActionKeyword keyword);
        Task DeleteAsync(int id);
        Task<bool> KeywordExistsAsync(string keywordName);
        Task<ActionKeyword?> GetByKeywordNameAsync(string keywordName);
        Task<List<ActionKeyword>> GetAllEnabledAsync();
    }
}
