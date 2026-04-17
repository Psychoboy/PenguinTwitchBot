using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging;

namespace DotNetTwitchBot.Circuit
{
    public class CircuitHandlerService : CircuitHandler
    {
        public string CircuitId { get; private set; } = "";

        readonly ICircuitUserService _userService;
        readonly ILogger<CircuitHandlerService> _logger;

        public CircuitHandlerService(ICircuitUserService userService, ILogger<CircuitHandlerService> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public override Task OnCircuitOpenedAsync(Microsoft.AspNetCore.Components.Server.Circuits.Circuit circuit, CancellationToken cancellationToken)
        {
            CircuitId = circuit.Id;
            return base.OnCircuitOpenedAsync(circuit, cancellationToken);
        }

        public override Task OnCircuitClosedAsync(Microsoft.AspNetCore.Components.Server.Circuits.Circuit circuit, CancellationToken cancellationToken)
        {
            _userService.Disconnect(circuit.Id);
            return base.OnCircuitClosedAsync(circuit, cancellationToken);
        }

        public override Task OnConnectionDownAsync(Microsoft.AspNetCore.Components.Server.Circuits.Circuit circuit, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Circuit {CircuitId} connection down", circuit.Id);
            return base.OnConnectionDownAsync(circuit, cancellationToken);
        }

        public override Task OnConnectionUpAsync(Microsoft.AspNetCore.Components.Server.Circuits.Circuit circuit, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Circuit {CircuitId} connection restored", circuit.Id);
            return base.OnConnectionUpAsync(circuit, cancellationToken);
        }
    }
}
