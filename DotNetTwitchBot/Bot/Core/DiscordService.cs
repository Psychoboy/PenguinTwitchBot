using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using DotNetTwitchBot.Bot.Commands.Custom;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.TwitchServices;
using Serilog.Events;

namespace DotNetTwitchBot.Bot.Core
{
    public class DiscordService
    {
        private DiscordSocketClient _client;
        private ILogger<DiscordService> _logger;
        private ServiceBackbone _serviceBackbone;
        private CustomCommand _customCommands;
        private TwitchService _twitchService;
        private ConcurrentDictionary<ulong, byte> _streamingIds = new ConcurrentDictionary<ulong, byte>();
        private DiscordSettings _settings;

        public DiscordService(
            CustomCommand customCommands,
            ILogger<DiscordService> logger,
            ServiceBackbone serviceBackbone,
            TwitchService twitchService,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceBackbone = serviceBackbone;
            _serviceBackbone.StreamStarted += OnStreamStarted;
            _customCommands = customCommands;
            _twitchService = twitchService;
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildPresences | GatewayIntents.GuildMembers,
                AlwaysDownloadUsers = true
            };
            _client = new DiscordSocketClient(config);
            _client.Log += LogAsync;
            _client.Connected += Connected;
            _client.Ready += OnReady;
            _client.SlashCommandExecuted += SlashCommandHandler;
            _client.PresenceUpdated += PresenceUpdated;
            _client.MessageReceived += MessageReceived;

            var settings = configuration.GetRequiredSection("Discord").Get<DiscordSettings>();
            if (settings == null)
            {
                throw new Exception("Invalid Configuration. Discord settings missing.");
            }
            _settings = settings;
            Initialize(settings.DiscordToken);
        }

        private Task OnStreamStarted(object? sender)
        {
            Task.Run(AnnounceLive);
            return Task.CompletedTask;
        }

        private async void AnnounceLive()
        {
            try
            {
                await Task.Delay(60000); //Delay to generate thumbnail
                IGuild guild = _client.GetGuild(_settings.DiscordServerId);

                // COMMENTED FOR TESTING
                //var channel = (IMessageChannel)await guild.GetChannelAsync(_settings.BroadcastChannel);
                var channel = (IMessageChannel)await guild.GetChannelAsync(911807868775313468);
                var embed = new EmbedBuilder()
                    .WithColor(100, 65, 164)
                    .WithThumbnailUrl("https://static-cdn.jtvnw.net/jtv_user_pictures/7397d16d-a2ff-4835-8f63-249b4738581b-profile_image-300x300.png")
                    .WithTitle("SuperPenguinTV has just went live on Twitch!")
                    .AddField("Now Playing", await _twitchService.GetCurrentGame())
                    .AddField("Stream Title", await _twitchService.GetStreamTitle())
                    .WithUrl("https://twitch.tv/SuperPenguinTV")
                    .WithImageUrl(await _twitchService.GetStreamThumbnail())
                    .WithCurrentTimestamp()
                    .WithFooter("Twitch").Build();
                var message = "";
                //Commented for testing
                // if (_settings.PingRoleWhenLive != 0)
                // {
                //     message += $"<@{_settings.PingRoleWhenLive}> ";
                // }
                message += "FOR TESTING - NOTIFICATION REMOVED - SuperPenguinTV just went LIVE on Twitch!";
                await channel.SendMessageAsync(message, embed: embed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting live.");
            }
        }


        private async Task PresenceUpdated(SocketUser arg1, SocketPresence before, SocketPresence after)
        {
            if (arg1 is IGuildUser == false) return;
            try
            {
                if (before == null || after == null || before.Activities == null || after.Activities == null) return;
                var user = (IGuildUser)arg1;
                if (before.Activities.Where(x => x.Type == ActivityType.Streaming && x.Name.Equals("Twitch")).Any() && after.Activities.Where(x => x.Type == ActivityType.Streaming).Count() == 0)
                {
                    await UserStreaming(user, false);
                }
                else if (before.Activities.Where(x => x.Type == ActivityType.Streaming).Count() == 0 && after.Activities.Where(x => x.Type == ActivityType.Streaming && x.Name.Equals("Twitch")).Any())
                {
                    await UserStreaming(user, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error with PresenceUpdated");
            }
        }

        private async Task MessageReceived(SocketMessage arg)
        {
            if (arg.Type != MessageType.Default) return;
            if (arg.Author is IGuildUser == false) return;
            var user = arg.Author as IGuildUser;
            var message = await arg.Channel.GetMessageAsync(arg.Id);
            if (string.IsNullOrWhiteSpace(message.Content.Trim())) return;
            _logger.LogInformation($"[DISCORD] [#{message.Channel.Name}] {user?.DisplayName}: {message.Content}");
        }

        private async Task SlashCommandHandler(SocketSlashCommand arg)
        {
            var eventArgs = new CommandEventArgs
            {
                Command = arg.CommandName,
                DiscordMention = arg.User.Mention,
                isDiscord = true
            };

            if (_customCommands.CustomCommandExists(arg.CommandName) == false) return;
            var commandResponse = _customCommands.CustomCommandResponse(arg.CommandName);
            var message = await _customCommands.ProcessTags(eventArgs, commandResponse);
            if (message != null && message.Cancel == false && message.Message.Length > 0)
            {
                await arg.RespondAsync(message.Message);
            }
        }

        private async Task OnReady()
        {
            IGuild guild = _client.GetGuild(_settings.DiscordServerId);
            await guild.DownloadUsersAsync(); //Load all users
            var users = await guild.GetUsersAsync();
            foreach (var user in users)
            {
                var activities = user.Activities;
                if (activities.Where(x => x.Type == ActivityType.Streaming && x.Name.Equals("Twitch")).Any())
                {
                    await UserStreaming(user, true);
                }
            }
            {
                var guildCommand = new SlashCommandBuilder();
                guildCommand.WithName("gib");
                guildCommand.WithDescription("Gib Stuff");
                try
                {
                    await guild.CreateApplicationCommandAsync(guildCommand.Build());
                }
                catch (HttpException exception)
                {
                    _logger.LogError(exception, "Error creating command");
                }
            }
            _logger.LogInformation("Discord Bot is ready.");
        }

        private async Task UserStreaming(IGuildUser user, bool isStreaming)
        {
            if (isStreaming)
            {
                _logger.LogInformation($"User {user.DisplayName} started streaming.");
                if (_settings.RoleIdToAssignMemberWhenLive != 0)
                {
                    await user.AddRoleAsync(_settings.RoleIdToAssignMemberWhenLive);
                }
            }
            else
            {
                _logger.LogInformation($"User {user.DisplayName} stopped streaming.");
                if (_settings.RoleIdToAssignMemberWhenLive != 0)
                {
                    await user.RemoveRoleAsync(_settings.RoleIdToAssignMemberWhenLive);
                }
            }
        }

        private Task Connected()
        {
            _logger.LogInformation("Discord Bot Connected.");
            return Task.CompletedTask;
        }

        private async void Initialize(string? discordToken)
        {
            await _client.LoginAsync(TokenType.Bot, discordToken);
            await _client.StartAsync();
        }

        public async Task LogAsync(LogMessage message)
        {
            var severity = message.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Trace,
                LogSeverity.Debug => LogLevel.Debug,
                _ => LogLevel.Information
            };
            _logger.Log(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
            await Task.CompletedTask;
        }
    }
}