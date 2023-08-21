namespace DotNetTwitchBot.Bot.Core
{
    public interface ILanguage
    {
        string Get(string id);
        Task LoadLanguage();
    }
}