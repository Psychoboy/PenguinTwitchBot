using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using DotNetTwitchBot.Bot.Commands.Misc;
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
        [HttpGet("/importquotes")]
        public async Task<ActionResult> ImportQuotes()
        {
            using (var reader = new StreamReader("data\\quotes.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            using (var scope = _scopeFactory.CreateScope())
            {
                var quoteSystem = scope.ServiceProvider.GetRequiredService<QuoteSystem>();
                var records = csv.GetRecords<TempQuote>();
                foreach (var record in records)
                {
                    var quote = new QuoteType
                    {
                        CreatedOn = DateTime.Parse(record.createdDate),
                        CreatedBy = record.createdBy,
                        Game = record.game,
                        Quote = record.quote
                    };
                    await quoteSystem.AddQuote(quote);
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