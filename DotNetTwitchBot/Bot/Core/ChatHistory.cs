using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Models;
using DotNetTwitchBot.Repository;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

namespace DotNetTwitchBot.Bot.Core
{
    public class ChatHistory(
        IServiceScopeFactory scopeFactory, 
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        ITwitchService twitchService,
        ILogger<ChatHistory> logger
        ) : BaseCommandService(serviceBackbone, commandHandler, "ChatHistory"), IChatHistory, IHostedService
    {
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

        private Task OnCommandMessage(object? sender, CommandEventArgs e)
        {
            if(e.FromOwnChannel == false) return Task.CompletedTask;
            return AddMessage(e.Name, e.DisplayName, e.Command + " " + e.Arg, e.MessageId);
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

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Chat History");
            _serviceBackbone.CommandEvent += OnCommandMessage;
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Chat History");
            _serviceBackbone.CommandEvent -= OnCommandMessage;
            return Task.CompletedTask;
        }

        public async Task CleanOldLogs()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var logs = db.ViewerChatHistories.Find(x => x.CreatedAt < DateTime.Now.AddMonths(-12));
            db.ViewerChatHistories.RemoveRange(logs);
            var result = await db.SaveChangesAsync();
            _logger.LogInformation("Removed {amount} chat histories", result);

        }

        public async Task DeleteChatMessage(ChannelChatMessageDeleteArgs e)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var chatHistory = db.ViewerChatHistories.Find(x => x.MessageId == e.Notification.Payload.Event.MessageId).FirstOrDefault();
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
                var chatHistory = db.ViewerChatHistories.Find(x => x.Username == e.Name).OrderByDescending(x => x.CreatedAt).FirstOrDefault();
                if (chatHistory != null)
                {
                    db.ViewerChatHistories.Remove(chatHistory);
                    await db.SaveChangesAsync();
                    if(chatHistory.MessageId != null && !string.IsNullOrWhiteSpace(chatHistory.MessageId))
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

        public override Task Register()
        {
            _logger.LogInformation("Registered Chat History");
            return RegisterDefaultCommand("vanish", this, ModuleName, description: "Deletes your last chat message.");
        }
    }
}
