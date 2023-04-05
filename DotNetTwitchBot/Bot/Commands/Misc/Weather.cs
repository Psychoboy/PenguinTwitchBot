using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class Weather : BaseCommand
    {
        private WeatherSettings _settings;
        private HttpClient _client = new HttpClient();

        public Weather(
            IConfiguration configuration,
            ServiceBackbone serviceBackbone
            ) : base(serviceBackbone)
        {
            var settings = configuration.GetRequiredSection("Weather").Get<WeatherSettings>();
            if (settings == null)
            {
                throw new Exception("Invalid Configuration. Weather settings missing.");
            }
            _settings = settings;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "weather":

                    var response = await GetWeather(e.Arg);
                    if (e.isDiscord)
                    {

                    }
                    else
                    {
                        await _serviceBackbone.SendChatMessage(e.DisplayName, response);
                    }
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