namespace PenguinTwitchBot.Services;

using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Database.Repository;

public class QuickSearchService : IQuickSearchService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFeatureRuntimeCoordinator _featureRuntimeCoordinator;

    public QuickSearchService(
        IServiceScopeFactory scopeFactory,
        IFeatureRuntimeCoordinator featureRuntimeCoordinator)
    {
        _scopeFactory = scopeFactory;
        _featureRuntimeCoordinator = featureRuntimeCoordinator;
    }

    public async Task<List<QuickSearchResult>> SearchAsync(
        string query,
        IEnumerable<QuickSearchResult> pageLinks,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        string cleanQuery = query.Trim();
        string commandQuery = cleanQuery.TrimStart('!');

        cancellationToken.ThrowIfCancellationRequested();

        // 1. Pages (in-memory)
        var matchedPages = pageLinks
            .Where(p => p.Title.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(p.Subtitle) && p.Subtitle.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.Title.StartsWith(cleanQuery, StringComparison.OrdinalIgnoreCase) ? 0 :
                          p.Title.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenBy(p => p.Title)
            .Take(5)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();

        // 2. Database queries
        await using var scope = _scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Actions
        var allActions = await unitOfWork.Actions.GetAllAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var matchedActions = allActions
            .Where(a => a.Name.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(a.Group) && a.Group.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(a => a.Name.StartsWith(cleanQuery, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(a => a.Name)
            .Take(5)
            .Select(a => new QuickSearchResult(
                Title: a.Name,
                Href: $"/actions/manage?actionId={a.Id}",
                Icon: Icons.Material.Filled.Tune,
                Category: "Actions",
                ResultType: QuickSearchResultType.Action,
                Subtitle: string.IsNullOrWhiteSpace(a.Group)
                    ? (a.Enabled ? "Action" : "Action • Disabled")
                    : (a.Enabled ? $"Action • {a.Group}" : $"Action • {a.Group} (Disabled)")
            ))
            .ToList();

        // Timers
        List<QuickSearchResult> matchedTimers = [];
        if (_featureRuntimeCoordinator.IsEnabled(FeatureKeys.AutoTimer))
        {
            var allTimers = await unitOfWork.TimerGroups.GetAsync();
            cancellationToken.ThrowIfCancellationRequested();

            matchedTimers = allTimers
                .Where(t => t.Name.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Name.StartsWith(cleanQuery, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(t => t.Name)
                .Take(5)
                .Select(t =>
                {
                    int minMinutes = Math.Max(1, t.IntervalMinimumSeconds / 60);
                    int maxMinutes = Math.Max(1, t.IntervalMaximumSeconds / 60);
                    string intervalText = minMinutes == maxMinutes ? $"{minMinutes}m" : $"{minMinutes}-{maxMinutes}m";
                    return new QuickSearchResult(
                        Title: t.Name,
                        Href: $"/actions/timers?timerGroupId={t.Id}",
                        Icon: Icons.Material.Filled.Timer,
                        Category: "Timers",
                        ResultType: QuickSearchResultType.Timer,
                        Subtitle: $"Timer • Every {intervalText}" + (!t.Active ? " (Disabled)" : "")
                    );
                })
                .ToList();
        }

        // Action Commands
        var allActionCommands = await unitOfWork.ActionCommands.GetAllAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var matchedActionCommands = allActionCommands
            .Where(c => c.CommandName.Contains(commandQuery, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(c.Category) && c.Category.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new
            {
                Item = new QuickSearchResult(
                    Title: $"!{c.CommandName}",
                    Href: $"/actions/commands?commandId={c.Id}",
                    Icon: Icons.Material.Filled.Bolt,
                    Category: "Commands",
                    ResultType: QuickSearchResultType.Command,
                    Subtitle: "Action Command" + (!string.IsNullOrWhiteSpace(c.Category) ? $" • {c.Category}" : "") + (c.Disabled ? " (Disabled)" : "")
                ),
                Score = c.CommandName.Equals(commandQuery, StringComparison.OrdinalIgnoreCase) ? 0 :
                        c.CommandName.StartsWith(commandQuery, StringComparison.OrdinalIgnoreCase) ? 1 : 2
            });

        // Default Commands
        var allDefaultCommands = await unitOfWork.DefaultCommands.GetAllAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var matchedDefaultCommands = allDefaultCommands
            .Where(c => c.CustomCommandName.Contains(commandQuery, StringComparison.OrdinalIgnoreCase) ||
                        c.CommandName.Contains(commandQuery, StringComparison.OrdinalIgnoreCase) ||
                        c.ModuleName.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(c.Category) && c.Category.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new
            {
                Item = new QuickSearchResult(
                    Title: $"!{c.CustomCommandName}",
                    Href: $"/commands/default/{c.Id}",
                    Icon: Icons.Material.Filled.Terminal,
                    Category: "Commands",
                    ResultType: QuickSearchResultType.Command,
                    Subtitle: $"Default Command • {c.ModuleName}" + (c.Disabled ? " (Disabled)" : "")
                ),
                Score = c.CustomCommandName.Equals(commandQuery, StringComparison.OrdinalIgnoreCase) ? 0 :
                        c.CustomCommandName.StartsWith(commandQuery, StringComparison.OrdinalIgnoreCase) ? 1 : 2
            });

        // Audio Commands
        var allAudioCommands = await unitOfWork.AudioCommands.GetAllAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var matchedAudioCommands = allAudioCommands
            .Where(c => c.CommandName.Contains(commandQuery, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(c.Category) && c.Category.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(c.AudioFile) && c.AudioFile.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new
            {
                Item = new QuickSearchResult(
                    Title: $"!{c.CommandName}",
                    Href: $"/actions/audiocommands?commandId={c.Id}",
                    Icon: Icons.Material.Filled.AudioFile,
                    Category: "Commands",
                    ResultType: QuickSearchResultType.AudioCommand,
                    Subtitle: "Audio Command" + (!string.IsNullOrWhiteSpace(c.AudioFile) ? $" • {c.AudioFile}" : "") + (c.Disabled ? " (Disabled)" : "")
                ),
                Score = c.CommandName.Equals(commandQuery, StringComparison.OrdinalIgnoreCase) ? 0 :
                        c.CommandName.StartsWith(commandQuery, StringComparison.OrdinalIgnoreCase) ? 1 : 2
            });

        // External Commands
        var allExternalCommands = await unitOfWork.ExternalCommands.GetAllAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var matchedExternalCommands = allExternalCommands
            .Where(c => c.CommandName.Contains(commandQuery, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(c.Category) && c.Category.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(c.Description) && c.Description.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new
            {
                Item = new QuickSearchResult(
                    Title: $"!{c.CommandName}",
                    Href: $"/commands/external?commandId={c.Id}",
                    Icon: Icons.Material.Filled.Extension,
                    Category: "Commands",
                    ResultType: QuickSearchResultType.ExternalCommand,
                    Subtitle: "External Command" + (!string.IsNullOrWhiteSpace(c.Category) ? $" • {c.Category}" : "") + (c.Disabled ? " (Disabled)" : "")
                ),
                Score = c.CommandName.Equals(commandQuery, StringComparison.OrdinalIgnoreCase) ? 0 :
                        c.CommandName.StartsWith(commandQuery, StringComparison.OrdinalIgnoreCase) ? 1 : 2
            });

        // Combine commands, sort by score, take top 6
        var matchedCommands = matchedActionCommands
            .Concat(matchedDefaultCommands)
            .Concat(matchedAudioCommands)
            .Concat(matchedExternalCommands)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Item.Title)
            .Take(6)
            .Select(x => x.Item)
            .ToList();

        // Keywords
        var allKeywords = await unitOfWork.ActionKeywords.GetAllAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var matchedKeywords = allKeywords
            .Where(k => k.CommandName.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(k.Category) && k.Category.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(k => k.CommandName.StartsWith(cleanQuery, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(k => k.CommandName)
            .Take(4)
            .Select(k => new QuickSearchResult(
                Title: k.CommandName,
                Href: $"/actions/keywords?keywordId={k.Id}",
                Icon: Icons.Material.Filled.Key,
                Category: "Keywords",
                ResultType: QuickSearchResultType.Keyword,
                Subtitle: "Keyword" + (!string.IsNullOrWhiteSpace(k.Category) ? $" • {k.Category}" : "") + (k.Disabled ? " (Disabled)" : "")
            ))
            .ToList();

        // Counters
        var allCounters = await unitOfWork.Counters.GetAllAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var matchedCounters = allCounters
            .Where(c => c.CounterName.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(c.DisplayName) && c.DisplayName.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.CounterName.StartsWith(cleanQuery, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(c => c.CounterName)
            .Take(4)
            .Select(c => new QuickSearchResult(
                Title: !string.IsNullOrWhiteSpace(c.DisplayName) ? c.DisplayName : c.CounterName,
                Href: $"/actions/counters?counterId={c.Id}",
                Icon: Icons.Material.Filled.Numbers,
                Category: "Counters",
                ResultType: QuickSearchResultType.Counter,
                Subtitle: $"Counter ({c.CounterName}) • Value: {c.Amount}"
            ))
            .ToList();

        // Assemble ordered results
        var results = new List<QuickSearchResult>();
        results.AddRange(matchedPages);
        results.AddRange(matchedCommands);
        results.AddRange(matchedActions);
        results.AddRange(matchedTimers);
        results.AddRange(matchedKeywords);
        results.AddRange(matchedCounters);

        return results;
    }
}
