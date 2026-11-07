using System.Collections.Concurrent;
using System.Reflection;
using Bunit;
using Jint;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSubstitute;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Alias;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Services;
using ViewerPage = PenguinTwitchBot.Pages.ViewerViews.AudioCommands;
using AudioService = PenguinTwitchBot.Bot.Commands.AudioCommand.AudioCommands;

namespace PenguinTwitchBot.Test.Pages;

public class ViewerAudioCommandsTests
{
    /// <summary>Exercises playback, switching, row removal, and cleanup in the preview script.</summary>
    [Fact]
    public void PreviewScriptHandlesPlaybackAndCleanup()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PenguinTwitchBot.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var script = File.ReadAllText(Path.Combine(directory.FullName, "PenguinTwitchBot", "wwwroot", "js", "viewer-audio-preview.js"));
        var engine = new Engine().Execute(script.Replace("export function ", "function ", StringComparison.Ordinal));
        engine.Execute("""
            let observer;
            class MutationObserver {
                constructor(callback) { this.callback = callback; observer = this; }
                observe() {}
                disconnect() { this.disconnected = true; }
            }
            const container = {
                listeners: {}, controls: [],
                addEventListener(name, callback) { this.listeners[name] = callback; },
                removeEventListener(name) { delete this.listeners[name]; },
                contains(node) { return !node.removed; },
                querySelectorAll() { return this.controls.map(control => control.audio); }
            };
            function makeControl(command) {
                const control = { dataset: { command } };
                const button = {
                    attributes: {}, title: '',
                    setAttribute(name, value) { this.attributes[name] = value; },
                    closest(selector) { return selector === '.audio-preview-button' ? this : control; }
                };
                const audio = {
                    paused: true, ended: false, currentTime: 0,
                    closest() { return control; },
                    play() { this.paused = false; container.listeners.play({ target: this }); return { catch() {} }; },
                    pause() { if (!this.paused) { this.paused = true; container.listeners.pause({ target: this }); } }
                };
                control.button = button; control.audio = audio;
                control.querySelector = selector => selector === 'audio' ? audio : button;
                container.controls.push(control);
                return control;
            }
            const first = makeControl('!first'), second = makeControl('!second');
            const click = control => container.listeners.click({ target: control.button });
            initialize(container);
            click(first);
            """);
        Assert.Equal("true", engine.Evaluate("first.button.attributes['aria-pressed']").AsString());
        engine.Execute("first.audio.currentTime = 3; click(first);");
        Assert.True(engine.Evaluate("first.audio.paused && first.audio.currentTime === 3").AsBoolean());
        engine.Execute("click(first); click(second);");
        Assert.True(engine.Evaluate("first.audio.paused && !second.audio.paused && first.audio.currentTime === 3").AsBoolean());
        Assert.Equal("false", engine.Evaluate("first.button.attributes['aria-pressed']").AsString());
        Assert.Equal("true", engine.Evaluate("second.button.attributes['aria-pressed']").AsString());
        engine.Execute("second.audio.paused = true; second.audio.ended = true; container.listeners.ended({target: second.audio});");
        Assert.Equal("false", engine.Evaluate("second.button.attributes['aria-pressed']").AsString());
        engine.Execute("""
            second.audio.ended = false; click(second);
            const removedRow = { nodeType: 1, removed: true, matches: () => false, querySelectorAll: () => [second.audio] };
            observer.callback([{ removedNodes: [removedRow] }]);
            """);
        Assert.True(engine.Evaluate("second.audio.paused").AsBoolean());
        engine.Execute("click(second); dispose(container);");
        Assert.True(engine.Evaluate("second.audio.paused && observer.disconnected && Object.keys(container.listeners).length === 0").AsBoolean());
    }

    /// <summary>Checks visible previews and page rendering across JavaScript initialization outcomes.</summary>
    [Theory]
    [InlineData("none")]
    [InlineData("import-error")]
    [InlineData("import-disconnected")]
    [InlineData("initialize-error")]
    [InlineData("initialize-disconnected")]
    public async Task ViewerCanPreviewAvailableCommandsAndAliases(string failure)
    {
        var root = Path.Combine(Path.GetTempPath(), "viewer-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "audio"));
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, "audio", "sound file.mp3"), [0]);
            var service = new AudioService(Substitute.For<IPenguinDispatcher>(), Substitute.For<IServiceScopeFactory>(),
                NullLogger<AudioService>.Instance, Substitute.For<IServiceBackbone>(), Substitute.For<ILanguage>(), Substitute.For<ICommandHandler>());
            var field = typeof(AudioService).GetField("Commands", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var commands = (ConcurrentDictionary<string, AudioCommand>)field.GetValue(service)!;
            commands["sound"] = new AudioCommand { CommandName = "sound", AudioFile = "sound file", MinimumRank = Rank.Viewer };
            commands["missing"] = new AudioCommand { CommandName = "missing", AudioFile = "absent", MinimumRank = Rank.Viewer };
            commands["hidden"] = new AudioCommand { CommandName = "hidden", AudioFile = "sound file", ExcludeFromUi = true, MinimumRank = Rank.Viewer };
            commands["disabled"] = new AudioCommand { CommandName = "disabled", AudioFile = "sound file", Disabled = true, MinimumRank = Rank.Viewer };

            var aliases = Substitute.For<IAlias>();
            aliases.GetAliasesAsync().Returns(Task.FromResult(new List<AliasModel> { new() { CommandName = "sound", AliasName = "alias" } }));
            await using var context = new BunitContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            if (failure.StartsWith("import", StringComparison.Ordinal))
            {
                var runtime = Substitute.For<IJSRuntime>();
                Exception exception = failure == "import-disconnected"
                    ? new JSDisconnectedException("Disconnected") : new JSException("Import failed");
                runtime.InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]>())
                    .Returns(_ => ValueTask.FromException<IJSObjectReference>(exception));
                context.Services.AddSingleton(runtime);
            }
            else
            {
                var module = context.JSInterop.SetupModule("./js/viewer-audio-preview.js");
                if (failure.StartsWith("initialize", StringComparison.Ordinal))
                {
                    var initialize = module.SetupVoid("initialize", _ => true);
                    if (failure == "initialize-disconnected") initialize.SetException(new JSDisconnectedException("Disconnected"));
                    else initialize.SetException(new JSException("Initialization failed"));
                }
            }
            context.Services.AddMudServices();
            context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            context.Services.AddSingleton(service);
            context.Services.AddSingleton(aliases);
            context.Services.AddSingleton(new MediaUploadService(root, NullLogger<MediaUploadService>.Instance));
            context.AddAuthorization().SetNotAuthorized();

            var page = context.Render<ViewerPage>();
            await page.WaitForAssertionAsync(() => Assert.Equal(2, page.FindAll("audio").Count));
            Assert.All(page.FindAll("audio"), audio => Assert.Equal("/audio/sound%20file.mp3", audio.GetAttribute("src")));
            Assert.All(page.FindAll("audio"), audio => Assert.False(audio.HasAttribute("controls")));
            Assert.Equal(2, page.FindAll("button.audio-preview-button[aria-pressed='false']").Count);
            Assert.Contains("!alias", page.Markup);
            Assert.Contains("!missing", page.Markup);
            Assert.DoesNotContain("!hidden", page.Markup);
            Assert.DoesNotContain("!disabled", page.Markup);
        }
        finally { Directory.Delete(root, true); }
    }
}