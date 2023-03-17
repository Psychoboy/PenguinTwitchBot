using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot;
using DotNetTwitchBot.Bot.Commands.Custom;
using DotNetTwitchBot.Bot.Core.Database;
using Microsoft.AspNetCore.Mvc;
using CsvHelper;
using DotNetTwitchBot.MaintenanceObjects;

namespace DotNetTwitchBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BotMaintenanceController : ControllerBase
    {
        private ILogger<BotMaintenanceController> _logger;
        private TwitchService _twitchService;
        private CustomCommand _customCommand;
        private AudioCommands _audioCommands;
        private readonly IServiceScopeFactory _scopeFactory;

        public BotMaintenanceController(
            ILogger<BotMaintenanceController> logger,
            TwitchService twitchService,
            IServiceScopeFactory scopeFactory,
            CustomCommand customCommand,
            AudioCommands audioCommands
            )
        {
            _logger = logger;
            _twitchService = twitchService;
            _scopeFactory = scopeFactory;
            _customCommand = customCommand;
            _audioCommands = audioCommands;
        }

        [HttpGet("/updatefollows")]
        public async Task<ActionResult> UpdateFollows()
        {
            var followers = await _twitchService.GetAllFollows();
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Followers.AddRangeAsync(followers);
                await db.SaveChangesAsync();
            }
            return Ok();
        }

        [IgnoreAntiforgeryToken]
        [HttpPost("/addcommand")]
        public async Task<ActionResult> AddCommand([FromBody] CustomCommands command)
        {
            await _customCommand.AddCommand(command);
            return Ok();
        }

        [HttpGet("/importaudio")]
        public async Task<ActionResult> ImportAudio()
        {
            using (var reader = new StreamReader("data\\audiocommands.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<AudioImport>();
                foreach (var record in records)
                {
                    var audioCommand = new AudioCommand
                    {
                        CommandName = record.Variable,
                        AudioFile = record.Value,
                        UserCooldown = -1,
                        GlobalCooldown = 60
                    };
                    await _audioCommands.AddAudioCommand(audioCommand);
                }
            }
            return Ok();
        }
    }
}