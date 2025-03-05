using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DotNetTwitchBot.Circuit
{
    public class CircuitUserService(ILogger<CircuitUserService> logger, IpLog ipLog) : ICircuitUserService
    {
        public ConcurrentDictionary<string, CircuitUser> Circuits { get; private set; } = new ConcurrentDictionary<string, CircuitUser>();

        public event EventHandler? CircuitsChanged;
        public event UserRemovedEventHandler? UserRemoved;

        void OnCircuitsChanged() => CircuitsChanged?.Invoke(this, EventArgs.Empty);
        void OnUserRemoved(string UserId)
        {
            var args = new UserRemovedEventArgs
            {
                UserId = UserId
            };
            UserRemoved?.Invoke(this, args);
        }

        public async Task Connect(string CircuitId, string UserId, string? userIp)
        {
            if (string.IsNullOrEmpty(CircuitId)) return;
            if (string.IsNullOrEmpty(UserId)) UserId = "Anonymous";
            if (string.IsNullOrEmpty(userIp)) userIp = "Unknown";
            if (Circuits.ContainsKey(CircuitId))
                Circuits[CircuitId].UserId = UserId;
            else
            {
                var circuitUser = new CircuitUser
                {
                    UserId = UserId,
                    CircuitId = CircuitId,
                    UserIp = userIp
                };
                Circuits[CircuitId] = circuitUser;
            }
            await ipLog.AddLogEntry(UserId, userIp);
            logger.LogInformation("{userId} connected to web interface. Ip: {ip}", UserId, userIp);
            OnCircuitsChanged();
        }

        public void Disconnect(string CircuitId)
        {
            Circuits.TryRemove(CircuitId, out var circuitRemoved);
            if (circuitRemoved != null)
            {
                OnUserRemoved(circuitRemoved.UserId);
                logger.LogInformation("{UserId} disconnected from web interface.", circuitRemoved.UserId);
                OnCircuitsChanged();
            }
        }

        public void UpdateUserLastSeen(string CircuitId, string uri)
        {
            if (string.IsNullOrEmpty(CircuitId)) return;
            if (Circuits.ContainsKey(CircuitId))
            {
                Circuits[CircuitId].LastPage = uri;
                Circuits[CircuitId].LastSeen = DateTime.Now;
                var sanitizedUri = uri.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
                logger.LogInformation("{UserId} navigated to {Uri}.", Circuits[CircuitId].UserId, sanitizedUri);
                OnCircuitsChanged();
            }

        }


    }
}
