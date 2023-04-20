using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using DotNetTwitchBot.Bot.Commands.Misc;
using DotNetTwitchBot.MaintenanceObjects;
using Microsoft.AspNetCore.Mvc;

namespace DotNetTwitchBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImportDataController : ControllerBase
    {
        private IServiceScopeFactory _scopeFactory;

        public ImportDataController(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }
        [HttpGet("/importpastsubs")]
        public async Task<ActionResult> ImportQuotes()
        {
            using (var reader = new StreamReader("data\\pastsubs.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            using (var scope = _scopeFactory.CreateScope())
            {
                var tracker = scope.ServiceProvider.GetRequiredService<Bot.Core.SubscriptionTracker>();
                var records = csv.GetRecords<PhantomImport>().ToList();
                var names = records.Select(x => x.variable).ToList(); ;
                var missingNames = await tracker.MissingSubs(names);
                foreach (var missingName in missingNames)
                {
                    await tracker.AddOrUpdateSubHistory(missingName);
                }
                return Ok();
            }
        }

        class TempQuote
        {
            public string id { get; set; } = String.Empty;
            public string createdBy { get; set; } = String.Empty;
            public string quote { get; set; } = String.Empty;
            public string createdDate { get; set; } = String.Empty;
            public string game { get; set; } = String.Empty;
        }
    }
}