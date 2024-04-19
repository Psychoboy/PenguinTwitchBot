using MediatR;

namespace DotNetTwitchBot.Bot.Commands.AudioCommand
{
    public class RunAudioCommandHandler(AudioCommands audioCommands, ILogger<RunAudioCommandHandler> logger) : INotificationHandler<RunCommandNotification>
    {
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        public async Task Handle(RunCommandNotification notification, CancellationToken cancellationToken)
        {
            var eventArgs = notification.EventArgs;
            try
            {
                if (eventArgs == null) throw new ArgumentNullException(nameof(eventArgs));
                if (eventArgs.SkipLock == false)
                {
                    if (await _semaphoreSlim.WaitAsync(500, cancellationToken) == false)
                    {
                        logger.LogWarning("AudioCommand Lock expired while waiting...");
                    }
                }
                await audioCommands.RunCommand(eventArgs);
            }
            catch (SkipCooldownException)
            {
                //ignore
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running command: {command}", notification.EventArgs?.Command);
            }
            finally
            {
                if (eventArgs?.SkipLock == false)
                    _semaphoreSlim.Release();
            }
        }
    }
}
