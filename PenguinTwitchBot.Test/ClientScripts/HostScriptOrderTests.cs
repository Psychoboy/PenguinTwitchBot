namespace PenguinTwitchBot.Test.ClientScripts;

public class HostScriptOrderTests
{
    [Fact]
    public void BlazorTimeLoadsBeforeBlazorStarts()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PenguinTwitchBot.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var host = File.ReadAllText(Path.Combine(directory.FullName, "PenguinTwitchBot", "Pages", "_Host.cshtml"));
        var timeScript = host.IndexOf("<script src=\"_content/BlazorTime/blazorTime.js\"", StringComparison.Ordinal);
        var blazorScript = host.IndexOf("<script src=\"_framework/blazor.server.js\"", StringComparison.Ordinal);
        Assert.True(timeScript >= 0 && blazorScript > timeScript);
    }
}