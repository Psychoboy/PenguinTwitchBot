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
                GatewayIntents = GatewayIntents.All | GatewayIntents.GuildPresences
            };
            _client = new DiscordSocketClient(config);
            _client.Log += LogAsync;
            _client.Connected += Connected;
            _client.Ready += OnReady;
            _client.SlashCommandExecuted += SlashCommandHandler;
            //_client.GuildMemberUpdated += GuildMemberUpdated;
            _client.PresenceUpdated += PresenceUpdated;
            _client.MessageReceived += MessageReceived;

            ServerId = Convert.ToUInt64(configuration["discordServerId"]);
            Initialize(configuration["discordToken"]);
        }

        private Task PresenceUpdated(SocketUser arg1, SocketPresence arg2, SocketPresence arg3)
        {
            if (arg1 is IGuildUser == false) return Task.CompletedTask;
            try
            {
                var user = (IGuildUser)arg1;

                if (user.IsStreaming && !_streamingIds.Keys.Contains(user.Id))
                {
                    _logger.LogInformation($"User {user.DisplayName} started streaming.");
                    _streamingIds[user.Id] = default(byte);
                }
                else if (!user.IsStreaming && _streamingIds.Keys.Contains(user.Id))
                {
                    _logger.LogInformation($"User {user.DisplayName} stopped streaming.");
                    _streamingIds.Remove(user.Id, out var doNotCare);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error with PrecenceUpdated");
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

        // private Task GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> arg1, SocketGuildUser arg2)
        // {

        // }

        private async Task SlashCommandHandler(SocketSlashCommand arg)
        {
            // arg.RespondWithModalAsync
            // var channel = await arg.User.CreateDMChannelAsync();

            // await channel.SendMessageAsync($"{arg.User.Mention} this worked.");
            var eventArgs = new CommandEventArgs
            {
                Command = arg.CommandName,
                DiscordMention = arg.User.Mention,
                isDiscord = true
            };


            var commandResponse = _customCommands.CustomCommandResponse(arg.CommandName);
            var message = await _customCommands.ProcessTags(eventArgs, commandResponse);
            if (message != null && message.Cancel == false && message.Message.Length > 0)
            {
                await arg.RespondAsync(message.Message);
            }
        }

        private async Task OnReady()
        {
            var guild = _client.GetGuild(ServerId);

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
            _logger.LogInformation("Discord Bot is ready.");
            // return Task.CompletedTask;
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