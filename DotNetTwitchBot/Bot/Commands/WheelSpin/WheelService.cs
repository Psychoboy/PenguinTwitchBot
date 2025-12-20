using DotNetTwitchBot.Bot;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Wheel;
using DotNetTwitchBot.Bot.Notifications;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Repository;
using MediatR;
using Quartz.Core;
using System.Security.Cryptography;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.Commands.WheelSpin
{
    public class WheelService(
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler,
        IServiceScopeFactory scopeFactory,
        IWebSocketMessenger webSocketMessenger,
        IMediator mediator,
        ILogger<WheelService> logger) : 
        BaseCommandService(serviceBackbone, commandHandler, "WheelService", mediator), IHostedService, IWheelService
    {
        private readonly JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private int WinningIndex { get; set; }
        private Wheel? CurrentWheel { get; set; } = null;
        private readonly List<string> nameEntries = [];
        private Wheel? nameWheel = null;
        private bool nameWheelActive = false;
        private bool nameWheelShown = false;

        public void OpenNameWheel()
        {
            nameWheelActive = true;
            nameEntries.Clear();
            nameWheel = null;
            ServiceBackbone.SendChatMessage("The viewer wheel is now open! Type !join to enter the wheel.");
        }

        public void ShowNameWheel()
        {
            nameWheelShown = true;
            nameWheel = new()
            {
                WinningMessage = "Congratulations {label}!"
            };
            foreach (var name in nameEntries)
            {
                nameWheel.Properties.Add(new WheelProperty { Label = name });
            }
            ShowWheel(nameWheel);
        }

        public void CloseNameWheel()
        {
            nameWheelShown = false;
            nameWheelActive = false;
        }

        public void SpinNameWheel()
        {
            if(nameWheel == null) return;
            SpinWheel();
        }

        public void ShowWheel(Wheel wheel)
        {
            var showWheel = new ShowWheel();
            var props = wheel.Properties;
            props.Sort((a, b) => a.Order.CompareTo(b.Order));
            wheel.Properties = props;
            CurrentWheel = wheel;

            showWheel.Items.AddRange(props);
            var json = JsonSerializer.Serialize(showWheel, jsonOptions);
            var task = webSocketMessenger.AddToQueue(json);
            task.Wait(500);
        }

        public void HideWheel()
        {
            var hideWheel = new HideWheel();
            var json = JsonSerializer.Serialize(hideWheel, jsonOptions);
            var task = webSocketMessenger.AddToQueue(json);
            task.Wait(500);
        }

        public void SpinWheel()
        {
            List<WheelSpinIndex> spots = [];
            for(var index = 0; index < CurrentWheel?.Properties.Count; index++)
            {
                var prop = CurrentWheel.Properties[index];
                var totalWeight = prop.Weight * 100;
                for (var i = 0; i < totalWeight; i++)
                {
                    spots.Add(new WheelSpinIndex { Index = index });
                }
            }
            WinningIndex = spots[RandomNumberGenerator.GetInt32(0, spots.Count)].Index;
            var spinWheel = new SpinWheel(WinningIndex);
            var json = JsonSerializer.Serialize(spinWheel, jsonOptions);
            var task = webSocketMessenger.AddToQueue(json);
            task.Wait(500);
        }

        public async Task<List<Wheel>> GetWheels()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var wheels = await db.Wheels.GetAsync(includeProperties: "Properties");
            foreach (var wheel in wheels)
            {
                wheel.Properties = [.. wheel.Properties.OrderBy(p => p.Order)];
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
                var winningMessage = CurrentWheel?.WinningMessage.Replace("{label}", CurrentWheel.Properties[index].Label, StringComparison.OrdinalIgnoreCase);
                if (winningMessage != null)
                {
                    Thread.Sleep(4000);
                    await ServiceBackbone.SendChatMessage(winningMessage);
                }
            }
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "showwheel":
                    { 
                        var wheel = await GetWheelFromCommand(e);
                        if (wheel == null) return;
                        ShowWheel(wheel);
                    }
                    break;
                case "hidewheel":
                    HideWheel();
                    break;
                case "spinwheel":
                    {
                        if(CurrentWheel != null)
                            SpinWheel();
                    }
                    break;
                case "join":
                    {
                        if (nameWheelActive && nameEntries.Contains(e.Name) == false)
                        {
                            nameEntries.Add(e.Name);
                            if (nameWheel != null && nameWheelShown)
                            {
                                nameWheel.Properties.Add(new WheelProperty { Label = e.Name });
                                ShowWheel(nameWheel);
                            }
                        }
                    }
                    break;
                case "opennamewheel":
                    OpenNameWheel();
                    break;
                case "shownamewheel":
                    ShowNameWheel();
                    break;
                case "closenamewheel":
                    CloseNameWheel();
                    break;
                case "spinnamewheel":
                    SpinNameWheel();
                    break;
            }
        }

        private async Task<Wheel?> GetWheelFromCommand(CommandEventArgs e)
        {
            if(!int.TryParse(e.Arg, out var id))
            {
                logger.LogError("Invalid wheel id {id}", e.Arg);
                return null;
            }
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var wheels = await db.Wheels.GetAsync(includeProperties: "Properties");
            return wheels.FirstOrDefault(x => x.Id == id);
        }

        public override async Task Register()
        {
            await RegisterDefaultCommand("showwheel", this, ModuleName, Rank.Streamer);
            await RegisterDefaultCommand("hidewheel", this, ModuleName, Rank.Streamer);
            await RegisterDefaultCommand("spinwheel", this, ModuleName, Rank.Streamer);
            await RegisterDefaultCommand("join", this, ModuleName, Rank.Viewer);
            await RegisterDefaultCommand("opennamewheel", this, ModuleName, Rank.Streamer);
            await RegisterDefaultCommand("shownamewheel", this, ModuleName, Rank.Streamer);
            await RegisterDefaultCommand("closenamewheel", this, ModuleName, Rank.Streamer);
            await RegisterDefaultCommand("spinnamewheel", this, ModuleName, Rank.Streamer);
            logger.LogInformation("Registered commands for {moduleName}", ModuleName);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting {module}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping {module}", ModuleName);
            return Task.CompletedTask;
        }
    }
}
