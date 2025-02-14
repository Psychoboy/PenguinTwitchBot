
namespace DotNetTwitchBot.CustomMiddleware
{
    public class BackgroundServiceStarter<T>(T backgroundService) : IHostedService where T : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return backgroundService.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return backgroundService.StopAsync(cancellationToken);
        }
    }
}
