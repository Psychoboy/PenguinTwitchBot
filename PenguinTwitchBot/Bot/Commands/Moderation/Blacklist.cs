using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Repository;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace PenguinTwitchBot.Bot.Commands.Moderation
{
    public class Blacklist(
        IServiceScopeFactory scopeFactory,
        ITwitchService twitchService,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        Application.Notifications.IPenguinDispatcher dispatcher,
        ILogger<Blacklist> logger
            ) : BaseCommandService(serviceBackbone, commandHandler, "Blacklist", dispatcher), IHostedService
    {
        private readonly ConcurrentBag<WordFilter> _blackList = new();

        public async Task AddBlacklist(WordFilter wordFilter)
        {
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await db.WordFilters.AddAsync(wordFilter);
                await db.SaveChangesAsync();
            }
            await LoadBlacklist();
        }

        public async Task UpdateBlacklist(WordFilter wordFilter)
        {
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.WordFilters.Update(wordFilter);
                await db.SaveChangesAsync();
            }
            await LoadBlacklist();
        }

        public async Task DeleteBlacklist(WordFilter wordFilter)
        {
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.WordFilters.Remove(wordFilter);
                await db.SaveChangesAsync();
            }
            await LoadBlacklist();
        }

        public async Task<WordFilter?> GetWordFilter(int id)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.WordFilters.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public List<WordFilter> GetBlackList()
        {
            return _blackList.ToList();
        }

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);

        public bool IsBlacklisted(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            foreach (var wordFilter in _blackList)
            {
                if (wordFilter.IsRegex)
                {
                    try
                    {
                        var regex = new Regex(wordFilter.Phrase, RegexOptions.None, RegexTimeout);
                        if (regex.IsMatch(message)) return true;
                    }
                    catch (RegexMatchTimeoutException ex)
                    {
                        logger.LogWarning(ex, "Regex evaluation timed out for blacklist phrase {Phrase}; rejecting message as unsafe.", wordFilter.Phrase);
                        return true;
                    }
                    catch (ArgumentException ex)
                    {
                        logger.LogWarning(ex, "Invalid stored regex pattern {Phrase} in blacklist; skipping pattern.", wordFilter.Phrase);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Regex error evaluating blacklist phrase {Phrase}", wordFilter.Phrase);
                    }
                }
                else if (message.Contains(wordFilter.Phrase, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public async Task ChatMessage(ChatMessageEventArgs e)
        {
            bool match = false;
            if (e.IsMod || e.IsBroadcaster) return;
            foreach (var wordFilter in _blackList)
            {
                if (wordFilter.IsRegex)
                {
                    try
                    {
                        var regex = new Regex(wordFilter.Phrase, RegexOptions.None, RegexTimeout);
                        if (regex.IsMatch(e.Message)) match = true;
                    }
                    catch (RegexMatchTimeoutException ex)
                    {
                        logger.LogWarning(ex, "Regex evaluation timed out in ChatMessage for blacklist phrase {Phrase}; treating as match.", wordFilter.Phrase);
                        match = true;
                    }
                    catch (ArgumentException ex)
                    {
                        logger.LogWarning(ex, "Invalid stored regex pattern {Phrase} in blacklist; skipping pattern.", wordFilter.Phrase);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Regex error in ChatMessage evaluating blacklist phrase {Phrase}", wordFilter.Phrase);
                    }
                }
                else if (e.Message.Contains(wordFilter.Phrase, StringComparison.OrdinalIgnoreCase))
                {
                    match = true;
                }

                if (match)
                {
                    await twitchService.TimeoutUser(e.Name, wordFilter.BanReason, wordFilter.PermaBan ? null : wordFilter.TimeOutLength);
                    await ServiceBackbone.SendChatMessage(wordFilter.Message);
                    break;
                }
            }
        }

        public async Task LoadBlacklist()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            _blackList.Clear();
            _blackList.AddRange(await db.WordFilters.GetAsync(orderBy: x => x.OrderBy(y => y.Id)));
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override async Task Register()
        {
            await LoadBlacklist();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}
