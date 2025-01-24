using DotNetTwitchBot.Bot;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Wheel;
using DotNetTwitchBot.Bot.Notifications;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Repository;
using System.Security.Cryptography;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.Commands.WheelSpin
{
    public class WheelService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IServiceScopeFactory scopeFactory,
        IWebSocketMessenger webSocketMessenger,
        ILogger<WheelService> logger) : 
        BaseCommandService(serviceBackbone, commandHandler, "WheelService"), IHostedService, IWheelService
    {
        private readonly JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private int WinningIndex { get; set; }
        private Wheel? CurrentWheel { get; set; } = null;
        public void ShowWheel(Wheel wheel)
        {
            var showWheel = new ShowWheel();
            var props = wheel.Properties;
            showWheel.Items.AddRange(props);
            var json = JsonSerializer.Serialize(showWheel, jsonOptions);
            webSocketMessenger.AddToQueue(json);
        }

        public void HideWheel()
        {
            var hideWheel = new HideWheel();
            var json = JsonSerializer.Serialize(hideWheel, jsonOptions);
            webSocketMessenger.AddToQueue(json);
        }

        public void SpinWheel(Wheel wheel)
        {
            CurrentWheel = wheel;
            WinningIndex = RandomNumberGenerator.GetInt32(0, wheel.Properties.Count);
            var spinWheel = new SpinWheel(WinningIndex);
            var json = JsonSerializer.Serialize(spinWheel, jsonOptions);
            webSocketMessenger.AddToQueue(json);
        }

        public async Task<List<Wheel>> GetWheels()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var wheels = await db.Wheels.GetAsync(includeProperties: "Properties");
            foreach (var wheel in wheels)
            {
                wheel.Properties = wheel.Properties.OrderBy(p => p.Order).ToList();
            }
            return wheels;
        }

        public async Task AddWheel(Wheel wheel)
        {
            for (var i = 0; i < wheel.Properties.Count; i++)
            {
                wheel.Properties[i].Order = i;
            }
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.Wheels.AddAsync(wheel);
            await db.SaveChangesAsync();
        }

        public async Task SaveWheel(Wheel wheel)
        {
            for (var i = 0; i < wheel.Properties.Count; i++)
            {
                wheel.Properties[i].Order = i;
            }
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Wheels.Update(wheel);
            await db.SaveChangesAsync();
        }

        public async Task DeleteProperties(List<WheelProperty> properties)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.WheelProperties.RemoveRange(properties);
            await db.SaveChangesAsync();
        }

        public async Task DeleteWheel(Wheel wheel)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Wheels.Remove(wheel);
            await db.SaveChangesAsync();
        }
        public async Task ValidateAndProcessWinner(int index)
        {
            if (WinningIndex == index)
            {
                var winningMessage = CurrentWheel?.WinningMessage.Replace("{label}", CurrentWheel.Properties[index].Label);
                if (winningMessage != null)
                {
                    await ServiceBackbone.SendChatMessage(winningMessage);
                }
            }
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override Task Register()
        {
            logger.LogInformation("Registered commands for {moduleName}", ModuleName);
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
