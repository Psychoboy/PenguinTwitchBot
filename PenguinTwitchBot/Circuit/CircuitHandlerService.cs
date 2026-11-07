using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging;

namespace PenguinTwitchBot.Circuit
{
    public class CircuitHandlerService : CircuitHandler
    {
        public string CircuitId { get; private set; } = "";
        public bool IsConnected { get; private set; } = true;
        public CancellationToken ConnectionToken { get; private set; }
        public event Action<bool>? ConnectionChanged;

        readonly ICircuitUserService _userService;
        readonly ILogger<CircuitHandlerService> _logger;
        private CancellationTokenSource _connectionCancellation = new();

        public CircuitHandlerService(ICircuitUserService userService, ILogger<CircuitHandlerService> logger)
        {
            _userService = userService;
            _logger = logger;
            ConnectionToken = _connectionCancellation.Token;
        }

        public override Task OnCircuitOpenedAsync(Microsoft.AspNetCore.Components.Server.Circuits.Circuit circuit, CancellationToken cancellationToken)
        {
            CircuitId = circuit.Id;
            return base.OnCircuitOpenedAsync(circuit, cancellationToken);
        }

        public override Task OnCircuitClosedAsync(Microsoft.AspNetCore.Components.Server.Circuits.Circuit circuit, CancellationToken cancellationToken)
        {
            if (IsConnected) SetConnectionDown();
            _connectionCancellation.Dispose();
            _userService.Disconnect(circuit.Id);
            return base.OnCircuitClosedAsync(circuit, cancellationToken);
        }

        public override Task OnConnectionDownAsync(Microsoft.AspNetCore.Components.Server.Circuits.Circuit circuit, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Circuit {CircuitId} connection down", circuit.Id);
            if (IsConnected) SetConnectionDown();
            return base.OnConnectionDownAsync(circuit, cancellationToken);
        }

        public override Task OnConnectionUpAsync(Microsoft.AspNetCore.Components.Server.Circuits.Circuit circuit, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Circuit {CircuitId} connection restored", circuit.Id);
            if (!IsConnected) SetConnectionUp();
            return base.OnConnectionUpAsync(circuit, cancellationToken);
        }

        private void SetConnectionDown()
        {
            IsConnected = false;
            _connectionCancellation.Cancel();
            ConnectionChanged?.Invoke(false);
        }

        private void SetConnectionUp()
        {
            _connectionCancellation.Dispose();
            _connectionCancellation = new CancellationTokenSource();
            ConnectionToken = _connectionCancellation.Token;
            IsConnected = true;
            ConnectionChanged?.Invoke(true);
        }
    }
}
