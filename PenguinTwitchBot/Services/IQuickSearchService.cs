namespace PenguinTwitchBot.Services;

public enum QuickSearchResultType
{
    Page,
    Action,
    Timer,
    Command,
    AudioCommand,
    Keyword,
    ExternalCommand,
    Counter
}

public sealed record QuickSearchResult(
    string Title,
    string Href,
    string Icon,
    string Category,
    QuickSearchResultType ResultType,
    string? Subtitle = null
);

public interface IQuickSearchService
{
    Task<List<QuickSearchResult>> SearchAsync(
        string query,
        IEnumerable<QuickSearchResult> pageLinks,
        CancellationToken cancellationToken = default);
}

