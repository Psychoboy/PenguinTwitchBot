using Microsoft.AspNetCore.Components.Server.Circuits;

namespace DotNetTwitchBot.Circuit
{
    public class CircuitHandlerService : CircuitHandler
    {
        public string CircuitId { get; private set; } = "";

        ICircuitUserService _userService;

        public CircuitHandlerService(ICircuitUserService userService)
        {
            _userService = userService;
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
    }
}
