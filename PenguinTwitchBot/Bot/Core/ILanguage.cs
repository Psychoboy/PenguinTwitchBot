namespace PenguinTwitchBot.Bot.Core
{
    public interface ILanguage
    {
        string Get(string id);
        void LoadLanguage();
    }
}