using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Models;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;
using PenguinTwitchBot.TwitchApi.EventSub.EventArgs.Channel;

namespace PenguinTwitchBot.Bot.Core
{
    public class ChatHistory(
        IServiceScopeFactory scopeFactory, 
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        ITwitchService twitchService,
        IChatHistoryRetentionSettingsService chatHistoryRetentionSettings,
        Application.Notifications.IPenguinDispatcher dispatcher,
        ILogger<ChatHistory> logger
        ) : BaseCommandService(serviceBackbone, commandHandler, "ChatHistory", dispatcher), IChatHistory, IHostedService
    {
        private const int CleanupBatchSize = 500;
        private static readonly TimeSpan CleanupBatchPause = TimeSpan.FromMilliseconds(25);

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly IServiceBackbone _serviceBackbone = serviceBackbone;
        private readonly ILogger<ChatHistory> _logger = logger;

        public async Task<PagedDataResponse<ViewerChatHistory>> GetViewerChatMessages(PaginationFilter filter, bool includeCommands)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var validFilter = new PaginationFilter(filter.Page, filter.Count);
            var pagedData = await unitOfWork.ViewerChatHistories.GetAsync(
                filter: includeCommands ? x => x.Username.Equals(filter.Filter) : x => x.Username.Equals(filter.Filter) && x.Message.StartsWith("!") == false,
                orderBy: a => a.OrderByDescending(b => b.CreatedAt),
                offset: (validFilter.Page) * filter.Count,
                limit: filter.Count
            );
            var totalRecords = await unitOfWork.ViewerChatHistories.Find(x => x.Username.Equals(filter.Filter)).CountAsync();

            return new PagedDataResponse<ViewerChatHistory>
            {
                Data = pagedData,
                TotalItems = totalRecords
            };
        }

        private async Task AddMessage(string name, string displayName, string message, string messageId)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var chatHistory = new ViewerChatHistory
            {
                Username = name,
                DisplayName = displayName,
                Message = message,
                MessageId = messageId,
            };
            try
            {
                await db.ViewerChatHistories.AddAsync(chatHistory);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure to save chat history. {username}: {message}", name, message);
            }
        }

        public Task AddChatMessage(ChatMessageEventArgs e)
        {
            if(e.FromOwnChannel == false) return Task.CompletedTask;
            return AddMessage(e.Name, e.DisplayName, e.Message, e.MessageId);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Chat History");
            await Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Chat History");
            return Task.CompletedTask;
        }

        public async Task CleanOldLogs()
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var monthsToKeep = await chatHistoryRetentionSettings.GetChatHistoryMonthsToKeepAsync();
                var cutoffUtc = DateTime.UtcNow.AddMonths(-monthsToKeep);
                var totalDeleted = 0;

                while (true)
                {
                    var logs = db.ViewerChatHistories
                        .Find(x => x.CreatedAt < cutoffUtc)
                        .OrderBy(x => x.Id)
                        .Take(CleanupBatchSize)
                        .ToList();

                    if (logs.Count == 0)
                    {
                        break;
                    }

                    db.ViewerChatHistories.RemoveRange(logs);
                    totalDeleted += await db.SaveChangesAsync();

                    // Yield between batches so other SQLite writers can acquire the lock.
                    await Task.Delay(CleanupBatchPause);
                }

                _logger.LogInformation("Removed {amount} chat histories using retention of {months} months in batches of {batchSize}", totalDeleted, monthsToKeep, CleanupBatchSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean old chat logs");
            }
        }

        public async Task DeleteChatMessage(ChannelChatMessageDeleteEventArgs e)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var chatHistory = db.ViewerChatHistories.Find(x => x.MessageId == e.Event.MessageId).FirstOrDefault();
                if (chatHistory != null)
                {
                    db.ViewerChatHistories.Remove(chatHistory);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete chat message");
            }
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            if(command.Equals("vanish", StringComparison.OrdinalIgnoreCase))
            {
                await DeleteLastChatMessage(e);
            }
        }

        private async Task DeleteLastChatMessage(CommandEventArgs e)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var chatHistory = db.ViewerChatHistories.Find(x => x.Username == e.Name && x.Message.StartsWith("!" + e.Command) == false).OrderByDescending(x => x.CreatedAt).FirstOrDefault();
                if (chatHistory != null)
                {
                    db.ViewerChatHistories.Remove(chatHistory);
                    await db.SaveChangesAsync();
                    if(!string.IsNullOrWhiteSpace(chatHistory.MessageId))
                    {
                        await twitchService.DeleteMessage(chatHistory.MessageId);
                    }
                }
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete last chat message");
            }
        }

        public async override Task Register()
        {
            _logger.LogInformation("Registered Chat History");
            await RegisterDefaultCommand("vanish", this, ModuleName, description: "Deletes your last chat message.");
        }
    }
}
