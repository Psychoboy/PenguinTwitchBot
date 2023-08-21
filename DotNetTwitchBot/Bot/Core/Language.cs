using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Core
{
    public class Language : ILanguage
    {
        private readonly ConcurrentDictionary<string, string> languageStrings = new ConcurrentDictionary<string, string>();
        public async Task LoadLanguage()
        {
            var files = Directory.GetFiles(@"Language/english", "*.txt", SearchOption.AllDirectories);
            await ProcessFiles(files);


            if (Directory.Exists(@"Language/custom"))
            {
                files = Directory.GetFiles(@"Language/custom", "*.txt", SearchOption.AllDirectories);
                await ProcessFiles(files);
            }

        }

        private async Task ProcessFiles(string[] files)
        {
            foreach (var file in files)
            {
                await ParseFile(file);
            }
        }

        private async Task ParseFile(string file)
        {
            var lines = await File.ReadAllLinesAsync(file);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (line.Trim().StartsWith("#")) continue;

                var lang = line.Split(new[] { '=' }, 2);
                if (lang.Length != 2) continue;
                languageStrings[lang[0]] = lang[1];
            }
        }

        public string Get(string id)
        {
            if (languageStrings.TryGetValue(id, out var message))
                return message;
            return "";
        }
    }
}
