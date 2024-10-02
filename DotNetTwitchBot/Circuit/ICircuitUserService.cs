using System.Collections.Concurrent;

namespace DotNetTwitchBot.Circuit
{
    public interface ICircuitUserService
    {
        ConcurrentDictionary<string, CircuitUser> Circuits { get; }

        event EventHandler CircuitsChanged;
        event UserRemovedEventHandler UserRemoved;

        Task Connect(string CircuitId, string UserId, string? userIp);
        void Disconnect(string CircuitId);

        void UpdateUserLastSeen(string CircuitId, string uri);
    }
}
