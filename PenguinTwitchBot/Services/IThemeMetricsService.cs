using System.Threading.Tasks;

namespace PenguinTwitchBot.Services;

public interface IThemeMetricsService
{
    void TrackActiveSession(string sessionId, string themeId, bool isDarkMode);
    void UntrackActiveSession(string sessionId);
    Task UpdateMetricsAsync();
}

