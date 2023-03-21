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
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Bot.Commands.Features;

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
        private LoyaltyFeature _loyaltyFeature;
        private readonly IServiceScopeFactory _scopeFactory;

        public BotMaintenanceController(
            ILogger<BotMaintenanceController> logger,
            TwitchService twitchService,
            IServiceScopeFactory scopeFactory,
            CustomCommand customCommand,
            AudioCommands audioCommands,
            LoyaltyFeature loyaltyFeature
            )
        {
            _logger = logger;
            _twitchService = twitchService;
            _scopeFactory = scopeFactory;
            _customCommand = customCommand;
            _audioCommands = audioCommands;
            _loyaltyFeature = loyaltyFeature;
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
                var records = csv.GetRecords<PhantomImport>();
                foreach (var record in records)
                {
                    var audioCommand = new AudioCommand
                    {
                        CommandName = record.variable,
                        AudioFile = record.value,
                        UserCooldown = -1,
                        GlobalCooldown = 60
                    };
                    await _audioCommands.AddAudioCommand(audioCommand);
                }
            }
            return Ok();
        }
        [HttpGet("/lastseen")]
        public async Task<ActionResult> ImportLastSeen()
        {
            using (var reader = new StreamReader("data\\lastseen.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<PhantomImport>();
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    foreach (var record in records)
                    {


                        var viewer = await db.Viewers.FirstOrDefaultAsync(x => x.Username.Equals(record.variable));
                        if (viewer == null)
                        {
                            viewer = new Viewer
                            {
                                DisplayName = record.variable,
                                Username = record.variable
                            };
                        }
                        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                        dateTime = dateTime.AddMilliseconds(double.Parse(record.value));
                        viewer.LastSeen = dateTime;
                        db.Viewers.Update(viewer);
                    }
                    await db.SaveChangesAsync();
                }
            }
            return Ok();
        }

        [HttpGet("/importpoints")]
        public async Task<ActionResult> ImportPoints()
        {
            using (var reader = new StreamReader("data\\points.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<PhantomImport>();
                foreach (var record in records)
                {
                    var points = Int64.Parse(record.value);
                    await _loyaltyFeature.AddPointsToViewer(record.variable, points);
                }
            }
            return Ok();
        }

        [HttpGet("/importtime")]
        public async Task<ActionResult> ImportTime()
        {
            using (var reader = new StreamReader("data\\time.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<PhantomImport>();
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    foreach (var record in records)
                    {
                        var time = Int64.Parse(record.value);
                        var viewer = await db.ViewersTime.FirstOrDefaultAsync(x => x.Username.Equals(record.variable));
                        if (viewer == null)
                        {
                            viewer = new ViewerTime
                            {
                                Username = record.variable,
                            };
                        }
                        viewer.Time = time;
                        db.ViewersTime.Update(viewer);
                    }
                    await db.SaveChangesAsync();
                }
            }
            return Ok();
        }
        [HttpGet("/importmessages")]
        public async Task<ActionResult> ImportMessages()
        {
            using (var reader = new StreamReader("data\\messages.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<PhantomImport>();
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    foreach (var record in records)
                    {
                        var messageCount = Int64.Parse(record.value);
                        var viewer = await db.ViewerMessageCounts.FirstOrDefaultAsync(x => x.Username.Equals(record.variable));
                        if (viewer == null)
                        {
                            viewer = new ViewerMessageCount
                            {
                                Username = record.variable,
                            };
                        }
                        viewer.MessageCount = messageCount;
                        db.ViewerMessageCounts.Update(viewer);
                    }
                    await db.SaveChangesAsync();
                }
            }
            return Ok();
        }
    }
}