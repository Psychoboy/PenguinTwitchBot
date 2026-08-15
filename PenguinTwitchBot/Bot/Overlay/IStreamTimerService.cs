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

        /// <summary>The live value, advanced for any time elapsed since the last state change.</summary>
        double GetCurrentSeconds();

        /// <summary>Starts (or resumes) the timer. Direction is "up" or "down".</summary>
        Task StartAsync(string? direction = null, double? startSeconds = null, bool reset = false);

        /// <summary>Sets the direction and/or value without changing whether the timer is running.</summary>
        Task ConfigureAsync(string? direction = null, double? seconds = null);

        Task StopAsync(bool reset = false);

        Task AddTimeAsync(double seconds);

        Task RemoveTimeAsync(double seconds);

        Task SetTimeAsync(double seconds);
    }
}
