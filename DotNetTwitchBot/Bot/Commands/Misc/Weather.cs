using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class Weather : BaseCommandService, IHostedService
    {
        private readonly WeatherSettings _settings;
        private readonly ILogger<Weather> _logger;
        private readonly HttpClient _client = new();

        public Weather(
            ILogger<Weather> logger,
            IConfiguration configuration,
            IServiceBackbone serviceBackbone,
            IMediator mediator,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler, "Weather", mediator)
        {
            var settings = configuration.GetRequiredSection("Weather").Get<WeatherSettings>() ?? throw new Exception("Invalid Configuration. Weather settings missing.");
            _settings = settings;
            _logger = logger;
        }

        public override async Task Register()
        {
            var moduleName = "Weather";
            await RegisterDefaultCommand("weather", this, moduleName);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "weather":

                    var response = await GetWeather(e.Arg);
                    await ServiceBackbone.ResponseWithMessage(e, response);
                    break;
            }
        }

        public async Task<string> GetWeather(string? arg)
        {
            var location = _settings.DefaultLocation;
            if (!string.IsNullOrWhiteSpace(arg))
            {
                location = arg;
            }
            var response = await _client.GetAsync($"https://api.weatherapi.com/v1/forecast.json?key={_settings.ApiKey}&q={location}&days=1&aqi=no&alerts=no");
            if (!response.IsSuccessStatusCode)
            {
                return "Failed to get weather for " + location;
            }
            var weather = await response.Content.ReadFromJsonAsync<Models.Weather.ForecastResponse>();
            if (weather == null)
            {
                return "Failed to get weather for " + location;
            }
            var forecastDay = weather.Forecast.ForecastDay.FirstOrDefault();
            if (forecastDay == null)
            {
                return "Failed to get weather for " + location;
            }



            var weatherString = $"The weather in {weather.Location.Name}, {weather.Location.Country} is {weather.Current.Condition.Text} {weather.Current.TempF}F/{weather.Current.TempC}C. Humidity: {weather.Current.Humidity}%.";
            weatherString += $" Today will be {forecastDay.Day.Condition.Text} High: {forecastDay.Day.MaxTempF}F/{forecastDay.Day.MaxTempC}C Low: {forecastDay.Day.MinTempF}F/{forecastDay.Day.MinTempC}C";
            return weatherString;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}