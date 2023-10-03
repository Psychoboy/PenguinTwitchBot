using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Repository;

namespace DotNetTwitchBot.Bot.Alerts
{
    public class AlertMessages
    {
        private readonly ILogger<AlertMessages> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public AlertMessages(
            ILogger<AlertMessages> logger,
            IServiceScopeFactory scopeFactory
            )
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task<string> GetCheerMessage(CheerEventArgs e)
        {
            var name = "";
            if (e.IsAnonymous || string.IsNullOrEmpty(e.DisplayName))
            {
                name = "Anonymous";
            }
            else
            {
                name = e.DisplayName;
            }
            AlertMessage? alertMessage = await GetAlertMessage("Cheer");
            string? message;
            if (alertMessage != null)
            {
                message = alertMessage.Value;
            }
            else
            {
                message = "{NAME} just cheered {AMOUNT} bits!";
            }
            message = message.Replace("{NAME}", name).Replace("{AMOUNT}", e.Amount.ToString());
            return message;
        }

        private async Task<AlertMessage?> GetAlertMessage(string name)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.AlertMessages.Find(x => x.Name.Equals(name)).FirstOrDefaultAsync();
        }
    }
}
