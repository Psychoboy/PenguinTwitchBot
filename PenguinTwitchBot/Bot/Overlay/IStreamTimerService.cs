namespace PenguinTwitchBot.Bot.Overlay
{
    public record StreamTimerState(
        bool IsRunning,
        string Direction,
        double Seconds,
        DateTime UpdatedAtUtc,
        DateTime ServerNowUtc,
        string Name = "Default");

    public interface IStreamTimerService
    {
        IReadOnlyList<string> GetTimerNames();

        IReadOnlyList<StreamTimerState> GetAllStates();

        StreamTimerState GetState(string? timerName = null);

        /// <summary>The live value, advanced for any time elapsed since the last state change.</summary>
        double GetCurrentSeconds(string? timerName = null);

        Task<bool> CreateTimerAsync(string timerName);

        Task<bool> DeleteTimerAsync(string timerName);

        /// <summary>Starts (or resumes) the specified timer. Direction is "up" or "down".</summary>
        Task StartTimerAsync(string timerName, string? direction = null, double? startSeconds = null, bool reset = false);

        /// <summary>Sets the direction and/or value without changing whether the timer is running.</summary>
        Task ConfigureTimerAsync(string timerName, string? direction = null, double? seconds = null);

        Task StopTimerAsync(string timerName, bool reset = false);

        Task AddTimeAsync(string timerName, double seconds);

        Task RemoveTimeAsync(string timerName, double seconds);

        Task SetTimeAsync(string timerName, double seconds);

        /// <summary>Starts (or resumes) the default timer. Direction is "up" or "down".</summary>
        Task StartAsync(string? direction = null, double? startSeconds = null, bool reset = false)
            => StartTimerAsync("Default", direction, startSeconds, reset);

        /// <summary>Sets the direction and/or value of the default timer without changing whether it is running.</summary>
        Task ConfigureAsync(string? direction = null, double? seconds = null)
            => ConfigureTimerAsync("Default", direction, seconds);

        Task StopAsync(bool reset = false)
            => StopTimerAsync("Default", reset);

        Task AddTimeAsync(double seconds)
            => AddTimeAsync("Default", seconds);

        Task RemoveTimeAsync(double seconds)
            => RemoveTimeAsync("Default", seconds);

        Task SetTimeAsync(double seconds)
            => SetTimeAsync("Default", seconds);
    }
}
