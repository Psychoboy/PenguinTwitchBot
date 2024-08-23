using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Models;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Core
{
    public class ChatHistory(IServiceScopeFactory scopeFactory, IServiceBackbone serviceBackbone, ILogger<ChatHistory> logger) : IChatHistory, IHostedService
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
            return AddMessage(e.Name, e.DisplayName, e.Command + " " + e.Arg);
        }

        private async Task AddMessage(string name, string displayName, string message)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var chatHistory = new ViewerChatHistory
            {
                Username = name,
                DisplayName = displayName,
                Message = message
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
            return AddMessage(e.Name, e.DisplayName, e.Message);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _serviceBackbone.CommandEvent += OnCommandMessage;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _serviceBackbone.CommandEvent -= OnCommandMessage;
            return Task.CompletedTask;
        }
    }
}
