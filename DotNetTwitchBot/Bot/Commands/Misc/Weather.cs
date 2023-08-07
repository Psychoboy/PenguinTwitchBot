using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class Weather : BaseCommandService
    {
        private readonly WeatherSettings _settings;
        private readonly ILogger<Weather> _logger;
        private readonly HttpClient _client = new();

        public Weather(
            ILogger<Weather> logger,
            IConfiguration configuration,
            ServiceBackbone serviceBackbone,
            CommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler)
        {
            var settings = configuration.GetRequiredSection("Weather").Get<WeatherSettings>() ?? throw new Exception("Invalid Configuration. Weather settings missing.");
            _settings = settings;
            _logger = logger;
        }

        public override async Task Register()
        {
            var moduleName = "Weather";
            await RegisterDefaultCommand("weather", this, moduleName);
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "weather":

                    var response = await GetWeather(e.Arg);
                    await ServiceBackbone.SendChatMessage(e.DisplayName, response);
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
    }
}