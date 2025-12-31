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

        public async Task Connect(string CircuitId, string UserName, string userId, string? userIp)
        {
            if (string.IsNullOrEmpty(CircuitId)) return;
            if (string.IsNullOrEmpty(UserName)) UserName = "Anonymous";
            if (string.IsNullOrEmpty(userIp)) userIp = "Unknown";
            if (Circuits.ContainsKey(CircuitId))
                Circuits[CircuitId].UserName = UserName;
            else
            {
                var circuitUser = new CircuitUser
                {
                    UserName = UserName,
                    CircuitId = CircuitId,
                    UserIp = userIp
                };
                Circuits[CircuitId] = circuitUser;
            }
            await ipLog.AddLogEntry(UserName, userId, userIp);
            logger.LogInformation("{userId} connected to web interface. Ip: {ip}", UserName, userIp);
            OnCircuitsChanged();
        }

        public void Disconnect(string CircuitId)
        {
            Circuits.TryRemove(CircuitId, out var circuitRemoved);
            if (circuitRemoved != null)
            {
                OnUserRemoved(circuitRemoved.UserName);
                logger.LogInformation("{UserId} disconnected from web interface.", circuitRemoved.UserName);
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
                logger.LogInformation("{UserId} navigated to {Uri}.", Circuits[CircuitId].UserName, sanitizedUri);
                OnCircuitsChanged();
            }

        }


    }
}
