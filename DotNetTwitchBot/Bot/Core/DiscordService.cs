using System;
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
        private List<ulong> _streamingIds = new List<ulong>();

        public ulong ServerId { get; }

        public DiscordService(
            CustomCommand customCommands,
            ILogger<DiscordService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _customCommands = customCommands;
            _client = new DiscordSocketClient();
            _client.Log += LogAsync;
            _client.Connected += Connected;
            _client.Ready += OnReady;
            _client.SlashCommandExecuted += SlashCommandHandler;
            _client.GuildMemberUpdated += GuildMemberUpdated;
            _client.MessageReceived += MessageReceived;

            ServerId = Convert.ToUInt64(configuration["discordServerId"]);
            Initialize(configuration["discordToken"]);
        }

        private async Task MessageReceived(SocketMessage arg)
        {
            var message = await arg.Channel.GetMessageAsync(arg.Id);
            _logger.LogInformation($"[DISCORD] [#{Tools.CleanInput(message.Channel.Name)}] {Tools.CleanInput(message.Author.ToString())}: {Tools.CleanInput(message.Content)}");
        }

        private Task GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> arg1, SocketGuildUser arg2)
        {
            if (arg2.IsStreaming && !_streamingIds.Contains(arg2.Id))
            {
                _logger.LogInformation($"User {arg2.Nickname} started streaming.");
                _streamingIds.Add(arg2.Id);
            }
            else if (!arg2.IsStreaming && _streamingIds.Contains(arg2.Id))
            {
                _logger.LogInformation($"User {arg2.Nickname} stopped streaming.");
                _streamingIds.Remove(arg2.Id);
            }
            else
            {
                _logger.LogInformation($"User {arg2.Nickname} updated.");
            }
            return Task.CompletedTask;
        }

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