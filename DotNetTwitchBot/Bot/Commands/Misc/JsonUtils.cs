using System.Text.Json;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public static class JsonUtils
    {
        public static bool DeserializeJson<T>(string json, out T? result)
        {
            try
            {
                result = JsonSerializer.Deserialize<T>(json);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }
    }
}
