using Jint;

namespace PenguinTwitchBot.Test.ClientScripts;

public class AlertAudioPathTests
{
    [Fact]
    public void GetAudioFile_ReturnsMediaUrlsDirectly()
    {
        var engine = CreateEngine();

        engine.Execute("var result = getAudioFile('/media/uploads/thank-you.mp3');");

        Assert.Equal("/media/uploads/thank-you.mp3", engine.Evaluate("result").AsString());
        Assert.Equal(0, engine.Evaluate("lookups.length").AsNumber());
    }

    [Fact]
    public void GetAudioFile_KeepsSearchingAudioDirectoryForLegacyNames()
    {
        var engine = CreateEngine();

        engine.Execute("var result = getAudioFile('thank-you');");

        Assert.Equal("/audio/thank-you.mp3", engine.Evaluate("result").AsString());
        Assert.Equal("/audio/", engine.Evaluate("lookups[0].path").AsString());
    }

    private static Engine CreateEngine()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PenguinTwitchBot.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var alertScript = File.ReadAllText(Path.Combine(directory.FullName, "PenguinTwitchBot", "wwwroot", "js", "alert.js"));
        const string functionStart = "function getAudioFile(name, path) {";
        const string nextFunction = "function handleAudioHook(json) {";
        var startIndex = alertScript.IndexOf(functionStart, StringComparison.Ordinal);
        var endIndex = alertScript.IndexOf(nextFunction, startIndex, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);

        var engine = new Engine();
        engine.Execute("""
            var audioFormats = { probably: ['mp3'], maybe: ['wav'] };
            var lookups = [];
            function findFirstFile(path, name, extensions) {
                lookups.push({ path: path, name: name, extensions: extensions });
                return extensions.length ? path + name + '.' + extensions[0] : '';
            }
            function printDebug() {}
            function getOptionSetting() { return 'false'; }
            """);
        return engine.Execute(alertScript[startIndex..endIndex]);
    }
}