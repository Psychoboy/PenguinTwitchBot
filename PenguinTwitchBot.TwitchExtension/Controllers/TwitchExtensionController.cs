using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PenguinTwitchBot.TwitchExtension.Controllers;

[Route("api/twitch-extension")]
[ApiController]
[AllowAnonymous]
public class TwitchExtensionController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TwitchExtensionController> _logger;

    public TwitchExtensionController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TwitchExtensionController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    private string GetMainAppBaseUrl()
    {
        return _configuration["MainApp:BaseUrl"] ?? "http://localhost:5000";
    }

    /// <summary>
    /// Returns current active fishing tournaments with standings by proxying to the main app.
    /// </summary>
    [HttpGet("fishing-tournaments")]
    public async Task<IActionResult> GetFishingTournaments([FromQuery] int top = 5)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{GetMainAppBaseUrl().TrimEnd('/')}/api/twitch-extension/fishing-tournaments?top={top}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy fishing tournaments request");
            return StatusCode(502, "Unable to reach the main application");
        }
    }

    /// <summary>
    /// Returns recent fish catches by proxying to the main app.
    /// </summary>
    [HttpGet("recent-catches")]
    public async Task<IActionResult> GetRecentCatches([FromQuery] int count = 20)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{GetMainAppBaseUrl().TrimEnd('/')}/api/twitch-extension/recent-catches?count={count}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy recent catches request");
            return StatusCode(502, "Unable to reach the main application");
        }
    }

    /// <summary>
    /// Returns the current extension configuration.
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return Ok(new
        {
            tournamentCount = 5,
            catchCount = 10,
            refreshInterval = 30
        });
    }
}