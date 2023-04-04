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
using Serilog.Events;

namespace DotNetTwitchBot.Bot.Core
{
    public class DiscordService
    {
        private DiscordSocketClient _client;
        private ILogger<DiscordService> _logger;
        private CustomCommand _customCommands;
        private ConcurrentDictionary<ulong, byte> _streamingIds = new ConcurrentDictionary<ulong, byte>();

        public ulong ServerId { get; }

        public DiscordService(
            CustomCommand customCommands,
            ILogger<DiscordService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _customCommands = customCommands;
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

            ServerId = Convert.ToUInt64(configuration["discordServerId"]);
            Initialize(configuration["discordToken"]);
        }

        private Task PresenceUpdated(SocketUser arg1, SocketPresence before, SocketPresence after)
        {
            if (arg1 is IGuildUser == false) return Task.CompletedTask;
            try
            {
                if (before == null || after == null || before.Activities == null || after.Activities == null) return Task.CompletedTask;
                var user = (IGuildUser)arg1;
                if (before.Activities.Where(x => x.Type == ActivityType.Streaming && x.Name.Equals("Twitch")).Any() && after.Activities.Where(x => x.Type == ActivityType.Streaming).Count() == 0)
                {
                    UserStreaming(user, false);
                }
                else if (before.Activities.Where(x => x.Type == ActivityType.Streaming).Count() == 0 && after.Activities.Where(x => x.Type == ActivityType.Streaming && x.Name.Equals("Twitch")).Any())
                {
                    UserStreaming(user, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error with PresenceUpdated");
            }
            return Task.CompletedTask;
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
            // if (arg.CommandName.Equals("testembed"))
            // {
            //     IGuild guild = _client.GetGuild(ServerId);
            //     var channel = (IMessageChannel)await guild.GetChannelAsync(679541861861425153);
            //     var embed = new EmbedBuilder()
            //         .WithColor(100, 65, 164)
            //         .WithThumbnailUrl("https://static-cdn.jtvnw.net/jtv_user_pictures/7397d16d-a2ff-4835-8f63-249b4738581b-profile_image-300x300.png")
            //         .WithTitle("SuperPenguinTV has just went live on Twitch!")
            //         .AddField("Now Playing", "Game goes here")
            //         .AddField("Stream Title", "Stream title goes here")
            //         .WithUrl("https://twitch.tv/SuperPenguinTV")
            //         .WithCurrentTimestamp()
            //         .WithFooter("Twitch").Build();
            //     await channel.SendMessageAsync("testmessage", embed: embed);

            //     return;
            // }
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
            IGuild guild = _client.GetGuild(ServerId);
            await guild.DownloadUsersAsync(); //Load all users
            var users = await guild.GetUsersAsync();
            foreach (var user in users)
            {
                var activities = user.Activities;
                if (activities.Where(x => x.Type == ActivityType.Streaming && x.Name.Equals("Twitch")).Any())
                {
                    UserStreaming(user, true);
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
            // {
            //     var guildCommand = new SlashCommandBuilder();
            //     guildCommand.WithName("testembed");
            //     guildCommand.WithDescription("test Stuff");
            //     try
            //     {
            //         await guild.CreateApplicationCommandAsync(guildCommand.Build());
            //     }
            //     catch (HttpException exception)
            //     {
            //         _logger.LogError(exception, "Error creating command");
            //     }
            // }
            _logger.LogInformation("Discord Bot is ready.");
        }

        private void UserStreaming(IGuildUser user, bool isStreaming)
        {
            if (isStreaming)
            {
                _logger.LogInformation($"User {user.DisplayName} started streaming.");
            }
            else
            {
                _logger.LogInformation($"User {user.DisplayName} stopped streaming.");
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

        // private async Task RegisterCommands()
        // {

        // }

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