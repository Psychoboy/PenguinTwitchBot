using PenguinTwitchBot.Controllers;
using PenguinTwitchBot.Models;

namespace PenguinTwitchBot.Services;

public interface ITwitchExtensionTrackingService
{
    event EventHandler? SessionsChanged;

    IReadOnlyCollection<TwitchExtensionSession> GetActiveSessions();

    void RecordRequest(
        TwitchExtensionClaims claims,
        string feature,
        string endpoint,
        string httpMethod,
        int statusCode,
        string? clientIp,
        string? actionName = null,
        Func<Task<string?>>? resolveUsernameAsync = null);

    void RecordAuthFailure(TwitchExtensionTokenFailureReason reason);
}
