using System.Collections.Concurrent;

namespace DotNetTwitchBot.Circuit
{
    public class CircuitUserService : ICircuitUserService
    {
        public ConcurrentDictionary<string, CircuitUser> Circuits { get; private set; }

        private readonly ILogger<CircuitUserService> _logger;

        public event EventHandler? CircuitsChanged;
        public event UserRemovedEventHandler? UserRemoved;

        void OnCircuitsChanged() => CircuitsChanged?.Invoke(this, EventArgs.Empty);
        void OnUserRemoved(string UserId)
        {
            var args = new UserRemovedEventArgs();
            args.UserId = UserId;
            UserRemoved?.Invoke(this, args);
        }

        public CircuitUserService(ILogger<CircuitUserService> logger)
        {
            Circuits = new ConcurrentDictionary<string, CircuitUser>();
            _logger = logger;
        }

        public void Connect(string CircuitId, string UserId)
        {
            Connect(CircuitId, UserId, "Unknown");
        }

        public void Connect(string CircuitId, string UserId, string? userIp)
        {
            if (string.IsNullOrEmpty(CircuitId)) return;
            if (string.IsNullOrEmpty(UserId)) UserId = "Anonymous";
            if (string.IsNullOrEmpty(userIp)) userIp = "Unknown";
            if (Circuits.ContainsKey(CircuitId))
                Circuits[CircuitId].UserId = UserId;
            else
            {
                var circuitUser = new CircuitUser();
                circuitUser.UserId = UserId;
                circuitUser.CircuitId = CircuitId;
                circuitUser.UserIp = userIp;
                Circuits[CircuitId] = circuitUser;
            }
            _logger.LogInformation("{userId} connected to web interface. Ip: {ip}", UserId, userIp);
            OnCircuitsChanged();
        }

        public void Disconnect(string CircuitId)
        {
            Circuits.TryRemove(CircuitId, out var circuitRemoved);
            if (circuitRemoved != null)
            {
                OnUserRemoved(circuitRemoved.UserId);
                _logger.LogInformation("{0} disconnected from web interface.", circuitRemoved.UserId);
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
                _logger.LogDebug("{0} navigated to {1}.", Circuits[CircuitId].UserId, uri);
                OnCircuitsChanged();
            }

        }


    }
}
