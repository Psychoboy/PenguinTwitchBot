using Jint;
using System.IO;
using System.Text.Json;

namespace PenguinTwitchBot.Test.ClientScripts
{
    public class FireworksEventHandlerTests
    {
        [Fact]
        public void NewEvent_ForwardsBurstConfiguration()
        {
            var engine = CreateEngine();

            engine.Execute("window.FireworksEventHandler.handle({ event: { type: 'Fireworks' }, data: { count: 4, delayMs: 250, shell: 'Ring', size: 7 } }, function (value) { window.result = JSON.stringify(value); });");

            using var result = JsonDocument.Parse(engine.Evaluate("window.result").AsString());
            Assert.Equal(4, result.RootElement.GetProperty("count").GetInt32());
            Assert.Equal(250, result.RootElement.GetProperty("delayMs").GetInt32());
            Assert.Equal("Ring", result.RootElement.GetProperty("shell").GetString());
            Assert.Equal(7, result.RootElement.GetProperty("size").GetInt32());
        }

        [Fact]
        public void LegacyActionName_StillLaunchesOneDefaultBurst()
        {
            var engine = CreateEngine();

            engine.Execute("window.FireworksEventHandler.handle({ event: { type: 'Action' }, data: { name: 'Fireworks' } }, function (value) { window.result = JSON.stringify(value); });");

            using var result = JsonDocument.Parse(engine.Evaluate("window.result").AsString());
            Assert.Equal(1, result.RootElement.GetProperty("count").GetInt32());
            Assert.Equal(0, result.RootElement.GetProperty("delayMs").GetInt32());
            Assert.Equal("", result.RootElement.GetProperty("shell").GetString());
            Assert.Equal("", result.RootElement.GetProperty("size").GetString());
        }

        [Fact]
        public void LaunchQueue_QueuesAllBurstsAtThePerformanceGap()
        {
            var engine = CreateEngine();
            engine.Execute(@"
                var now = 1000;
                var timers = [];
                var launchTimes = [];
                var lastLaunchAt = null;
                var timerApi = {
                    setTimeout: function (callback, delay) {
                        timers.push({ callback: callback, at: now + delay });
                    }
                };
                var queue = window.FireworksEventHandler.createLaunchQueue(
                    function () {
                        launchTimes.push(now);
                        lastLaunchAt = now;
                        return true;
                    },
                    function (entry) {
                        var gap = lastLaunchAt === null ? 0 : 240 - (now - lastLaunchAt);
                        return Math.max(entry.delayBeforeMs, gap, 0);
                    },
                    timerApi);

                for (var i = 0; i < 100; i++) {
                    queue.enqueue({ delayBeforeMs: i === 0 ? 0 : 100 });
                }

                while (timers.length > 0 && launchTimes.length < 100) {
                    var next = timers.shift();
                    now = next.at;
                    next.callback();
                }

                var minimumGap = Infinity;
                for (var j = 1; j < launchTimes.length; j++) {
                    minimumGap = Math.min(minimumGap, launchTimes[j] - launchTimes[j - 1]);
                }
                window.launchCount = launchTimes.length;
                window.pendingCount = queue.getPendingCount();
                window.minimumGap = minimumGap;
            ");

            Assert.Equal(100, engine.Evaluate("window.launchCount").AsNumber());
            Assert.Equal(0, engine.Evaluate("window.pendingCount").AsNumber());
            Assert.Equal(240, engine.Evaluate("window.minimumGap").AsNumber());
        }

        private static Engine CreateEngine()
        {
            var engine = new Engine();
            engine.SetValue("window", new object());
            var path = Path.Combine(FindWwwRootPath(), "js", "fireworks-event-handler.js");
            engine.Execute(File.ReadAllText(path));
            return engine;
        }

        private static string FindWwwRootPath()
        {
            var directory = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(directory))
            {
                var candidate = Path.Combine(directory, "PenguinTwitchBot", "wwwroot");
                if (Directory.Exists(candidate)) return candidate;
                directory = Directory.GetParent(directory)?.FullName ?? "";
            }

            throw new DirectoryNotFoundException("Could not locate PenguinTwitchBot/wwwroot for Fireworks JavaScript tests.");
        }
    }
}