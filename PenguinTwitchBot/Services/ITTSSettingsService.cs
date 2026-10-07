namespace PenguinTwitchBot.Services;

public interface ITTSSettingsService
{
    Task<int> GetKokoroThreadsAsync(int defaultValue = 2);
    Task SetKokoroThreadsAsync(int value);
}

