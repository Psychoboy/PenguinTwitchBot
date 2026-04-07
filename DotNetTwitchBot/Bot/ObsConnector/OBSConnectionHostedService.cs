namespace DotNetTwitchBot.Bot.ObsConnector
{
    /// <summary>
    /// Background service that initializes OBS connections on startup
    /// </summary>
    public class OBSConnectionHostedService : IHostedService
    {
        private readonly IOBSConnectionManager _connectionManager;
        private readonly ILogger<OBSConnectionHostedService> _logger;

        public OBSConnectionHostedService(
            IOBSConnectionManager connectionManager,
            ILogger<OBSConnectionHostedService> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting OBS Connection Service");

            // Run initialization in background to avoid blocking application startup
            _ = Task.Run(async () =>
            {
                try
                {
                    await _connectionManager.ReloadConnectionsAsync();
                    _logger.LogInformation("OBS Connection Service started successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error starting OBS Connection Service");
                }
            }, cancellationToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping OBS Connection Service");
            return Task.CompletedTask;
        }
    }
}
