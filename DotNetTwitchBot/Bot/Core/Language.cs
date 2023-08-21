using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Core
{
    public class Language : ILanguage
    {
        public Language()
        {
            LoadLanguage();
        }

        private readonly ConcurrentDictionary<string, string> languageStrings = new ConcurrentDictionary<string, string>();
        public void LoadLanguage()
        {
            var files = Directory.GetFiles(@"Language/english", "*.txt", SearchOption.AllDirectories);
            ProcessFiles(files);


            if (Directory.Exists(@"Language/custom"))
            {
                files = Directory.GetFiles(@"Language/custom", "*.txt", SearchOption.AllDirectories);
                ProcessFiles(files);
            }

        }

        private void ProcessFiles(string[] files)
        {
            foreach (var file in files)
            {
                ParseFile(file);
            }
        }

        private void ParseFile(string file)
        {
            var lines = File.ReadAllLines(file);
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
