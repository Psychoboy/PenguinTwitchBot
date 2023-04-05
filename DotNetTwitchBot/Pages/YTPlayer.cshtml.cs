using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace DotNetTwitchBot.Pages
{
    public class YTPlayer : PageModel
    {
        private readonly ILogger<YTPlayer> _logger;

        public YTPlayer(ILogger<YTPlayer> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}