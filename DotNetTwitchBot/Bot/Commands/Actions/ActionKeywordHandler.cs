using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Actions.Utilities;
using DotNetTwitchBot.Application.ChatMessage.Notifications;
using System.Text.RegularExpressions;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Commands;

namespace DotNetTwitchBot.Bot.Commands.Actions
{
    internal class KeywordWithCompiledRegex
    {
        public ActionKeyword Keyword { get; set; } = null!;
        public Regex? CompiledRegex { get; set; }

        public KeywordWithCompiledRegex(ActionKeyword keyword)
        {
            Keyword = keyword;
            if (keyword.IsRegex)
            {
                try
                {
                    CompiledRegex = new Regex(
                        keyword.CommandName,
                        keyword.IsCaseSensitive ? RegexOptions.Compiled : RegexOptions.Compiled | RegexOptions.IgnoreCase,
                        TimeSpan.FromMilliseconds(500));
                }
                catch (Exception)
                {
                    // Invalid regex, will be null
                    CompiledRegex = null;
                }
            }
        }
    }

    public class ActionKeywordHandler(
        IServiceScopeFactory serviceScopeFactory,
        ICommandHandler commandHandler,
        ILogger<ActionKeywordHandler> logger) : Application.Notifications.INotificationHandler<ReceivedChatMessage>
    {
        private readonly SemaphoreSlim _keywordLock = new(1, 1);
        private List<KeywordWithCompiledRegex> _cachedKeywords = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(1); // Cache expires after 1 minute

        private async Task<List<KeywordWithCompiledRegex>> GetKeywordsAsync()
        {
            await _keywordLock.WaitAsync();
            try
            {
                // Reload cache if expired
                if (DateTime.UtcNow - _lastCacheUpdate > _cacheExpiration)
                {
                    await ReloadKeywordsCacheAsync();
                }

                return _cachedKeywords;
            }
            finally
            {
                _keywordLock.Release();
            }
        }

        private async Task ReloadKeywordsCacheAsync()
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var actionKeywordService = scope.ServiceProvider.GetRequiredService<IActionKeywordService>();

                var keywords = await actionKeywordService.GetAllEnabledAsync();
                _cachedKeywords = keywords.Select(k => new KeywordWithCompiledRegex(k)).ToList();
                _lastCacheUpdate = DateTime.UtcNow;

                logger.LogInformation("Reloaded {Count} keywords into cache", _cachedKeywords.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reloading keywords cache");
            }
        }

        /// <summary>
        /// Invalidates the keyword cache, forcing a reload on the next message
        /// </summary>
        public void InvalidateCache()
        {
            _lastCacheUpdate = DateTime.MinValue;
            logger.LogInformation("Keyword cache invalidated");
        }

        public async Task Handle(ReceivedChatMessage notification, CancellationToken cancellationToken)
        {
            try
            {
                if (notification.EventArgs == null || string.IsNullOrWhiteSpace(notification.EventArgs.Message))
                    return;

                // Ignore commands (messages starting with !)
                if (notification.EventArgs.Message.StartsWith("!"))
                    return;

                // Get cached keywords
                var keywordsWithRegex = await GetKeywordsAsync();

                foreach (var keywordEntry in keywordsWithRegex)
                {
                    var keyword = keywordEntry.Keyword;
                    bool isMatch = false;

                    // Check if the message matches the keyword
                    if (keyword.IsRegex)
                    {
                        if (keywordEntry.CompiledRegex != null)
                        {
                            try
                            {
                                isMatch = keywordEntry.CompiledRegex.IsMatch(notification.EventArgs.Message);
                            }
                            catch (RegexMatchTimeoutException)
                            {
                                logger.LogWarning("Regex timeout for keyword {KeywordName}", keyword.CommandName);
                                continue;
                            }
                        }
                        else
                        {
                            logger.LogWarning("Invalid regex for keyword {KeywordName}, skipping", keyword.CommandName);
                            continue;
                        }
                    }
                    else
                    {
                        isMatch = notification.EventArgs.Message.Contains(keyword.CommandName, 
                            keyword.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                    }

                    if (!isMatch) continue;

                    // Convert chat message to command event args for permission checks
                    var commandEventArgs = new CommandEventArgs
                    {
                        Arg = notification.EventArgs.Message,
                        Args = notification.EventArgs.Message.Split(" ").ToList(),
                        IsWhisper = false,
                        Name = notification.EventArgs.Name,
                        DisplayName = notification.EventArgs.DisplayName,
                        IsBroadcaster = notification.EventArgs.IsBroadcaster,
                        IsMod = notification.EventArgs.IsMod,
                        IsSub = notification.EventArgs.IsSub,
                        IsVip = notification.EventArgs.IsVip,
                        UserId = notification.EventArgs.UserId,
                        FromOwnChannel = notification.EventArgs.FromOwnChannel
                    };

                    if (!CommandHandler.CheckIfAllowedInSharedChat(commandEventArgs, keyword))
                    {
                        logger.LogWarning("User {User} attempted to trigger broadcaster-only keyword {Keyword}", 
                            notification.EventArgs.DisplayName, keyword.CommandName);
                        continue;
                    }

                    // Check permissions
                    if (!await commandHandler.CheckPermission(keyword, commandEventArgs))
                    {
                        logger.LogWarning("User {User} does not have permission to trigger keyword {Keyword}", 
                            notification.EventArgs.DisplayName, keyword.CommandName);
                        continue;
                    }

                    // Check cooldowns
                    if (keyword.SayCooldown)
                    {
                        if (!await commandHandler.IsGlobalCoolDownExpiredWithMessageForAction(
                            notification.EventArgs.Name,
                            notification.EventArgs.DisplayName,
                            $"keyword {keyword.CommandName}"))
                            continue;
                    }
                    else
                    {
                        if (!await commandHandler.IsCoolDownExpired(
                            notification.EventArgs.Name,
                            $"keyword {keyword.CommandName}"))
                            continue;
                    }

                    // Get and execute actions triggered by this keyword
                    await using var scope = serviceScopeFactory.CreateAsyncScope();
                    var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                    var actionService = scope.ServiceProvider.GetRequiredService<IAction>();

                    var actions = await actionManagement.GetActionsByTriggerTypeAndNameAsync(
                        Models.Actions.Triggers.TriggerTypes.Keyword,
                        keyword.CommandName);

                    var dictionary = CommandEventArgsConverter.ToDictionary(commandEventArgs);

                    foreach (var action in actions)
                    {
                        await actionService.EnqueueAction(dictionary, action);
                    }

                    // Set cooldowns after successful execution
                    if (keyword.GlobalCooldown > 0)
                    {
                        var globalCooldown = CooldownHelper.CalculateCooldown(keyword.GlobalCooldown, keyword.GlobalCooldownMax);
                        await commandHandler.AddGlobalCooldown($"keyword {keyword.CommandName}", globalCooldown);
                    }

                    if (keyword.UserCooldown > 0)
                    {
                        var userCooldown = CooldownHelper.CalculateCooldown(keyword.UserCooldown, keyword.UserCooldownMax);
                        await commandHandler.AddCoolDown(
                            notification.EventArgs.Name,
                            $"keyword {keyword.CommandName}",
                            userCooldown);
                    }

                    // Only trigger the first matching keyword
                    break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling keyword triggers");
            }
        }
    }
}
