namespace PenguinTwitchBot.Bot.Overlay
{
    public record StreamTimerState(
        bool IsRunning,
        string Direction,
        double Seconds,
        DateTime UpdatedAtUtc,
        DateTime ServerNowUtc);

    public interface IStreamTimerService
    {
        StreamTimerState GetState();

        /// <summary>Starts (or resumes) the timer. Direction is "up" or "down".</summary>
        Task StartAsync(string? direction = null, double? startSeconds = null, bool reset = false);

        Task StopAsync(bool reset = false);

        Task AddTimeAsync(double seconds);

        Task RemoveTimeAsync(double seconds);

        Task SetTimeAsync(double seconds);
    }
}
