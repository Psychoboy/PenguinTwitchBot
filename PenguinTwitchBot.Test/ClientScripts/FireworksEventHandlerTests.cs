using Jint;
using System.IO;
using System.Text.Json;

namespace PenguinTwitchBot.Test.ClientScripts
{
    public class FireworksEventHandlerTests
    {
        [Theory]
        [InlineData(false, 0, 0, 0)]
        [InlineData(false, 100, 20, 100)]
        [InlineData(true, 0, 20, 220)]
        [InlineData(true, 0, 300, 0)]
        public void GetLaunchDelay_UsesPayloadForSubActionsAndWidgetGapForLegacy(
            bool isLegacy,
            int payloadDelayMs,
            int elapsedMs,
            int expectedDelayMs)
        {
            var engine = CreateEngine();
            engine.Execute($"window.result = window.FireworksEventHandler.getLaunchDelay({{ isLegacy: {isLegacy.ToString().ToLowerInvariant()}, delayBeforeMs: {payloadDelayMs} }}, {elapsedMs}, 240);");

            Assert.Equal(expectedDelayMs, engine.Evaluate("window.result").AsNumber());
        }

        [Theory]
        [InlineData(false, 240, 0)]
        [InlineData(true, 240, 240)]
        public void GetLaunchMinimumGap_UsesPayloadForSubActionsAndWidgetGapForLegacy(
            bool isLegacy,
            int configuredMinGapMs,
            int expectedMinimumGapMs)
        {
            var engine = CreateEngine();
            engine.Execute($"window.result = window.FireworksEventHandler.getLaunchMinimumGap({{ isLegacy: {isLegacy.ToString().ToLowerInvariant()} }}, {configuredMinGapMs});");

            Assert.Equal(expectedMinimumGapMs, engine.Evaluate("window.result").AsNumber());
        }

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
        public void TargetedTestFire_IsIgnoredByOtherWidgets()
        {
            var engine = CreateEngine();
            engine.Execute("window.launchCount = 0; window.FireworksEventHandler.handle({ event: { type: 'Fireworks' }, data: { widgetId: 'widget-a', count: 1 } }, function () { window.launchCount++; }, function () {}, 'widget-b');");

            Assert.Equal(0, engine.Evaluate("window.launchCount").AsNumber());
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

        [Fact]
        public void LaunchQueue_RemovesFailedHeadLogsAndContinues()
        {
            var engine = CreateEngine();
            engine.Execute(@"
                var timers = [];
                var launchCount = 0;
                var loggedErrors = 0;
                window.console = { error: function () { loggedErrors++; } };
                var queue = window.FireworksEventHandler.createLaunchQueue(
                    function () {
                        launchCount++;
                        if (launchCount === 1) throw new Error('test launch failure');
                        return true;
                    },
                    function () { return 0; },
                    { setTimeout: function (callback) { timers.push(callback); } });
                queue.enqueue({ id: 1 });
                queue.enqueue({ id: 2 });
                while (timers.length > 0) timers.shift()();
                window.launchCount = launchCount;
                window.loggedErrors = loggedErrors;
                window.pendingCount = queue.getPendingCount();
            ");

            Assert.Equal(2, engine.Evaluate("window.launchCount").AsNumber());
            Assert.Equal(1, engine.Evaluate("window.loggedErrors").AsNumber());
            Assert.Equal(0, engine.Evaluate("window.pendingCount").AsNumber());
        }

        [Fact]
        public void LaunchQueue_UsesWindowTimerWhenStatusCallbackIsProvided()
        {
            var engine = CreateEngine();
            engine.Execute(@"
                var timers = [];
                var statusChanges = [];
                window.setTimeout = function (callback) { timers.push(callback); };
                var queue = window.FireworksEventHandler.createLaunchQueue(
                    function () { return true; },
                    function () { return 0; },
                    undefined,
                    function (pending) { statusChanges.push(pending); });
                queue.enqueueJob({ jobId: 'job-1', count: 1, delayMs: 0 }, 10);
                while (timers.length > 0) timers.shift()();
                window.pendingCount = queue.getPendingCount();
                window.statusChanges = JSON.stringify(statusChanges);
            ");

            Assert.Equal(0, engine.Evaluate("window.pendingCount").AsNumber());
            Assert.Equal("[1,0]", engine.Evaluate("window.statusChanges").AsString());
        }

        [Fact]
        public void ClearQueueEvent_DropsPendingBurstsWithoutLaunchingThem()
        {
            var engine = CreateEngine();
            engine.Execute(@"
                var timers = [];
                var launches = 0;
                var queue = window.FireworksEventHandler.createLaunchQueue(
                    function () { launches++; return true; },
                    function () { return 0; },
                    { setTimeout: function (callback) { timers.push(callback); } });
                queue.enqueueJob({ jobId: 'managed-job', count: 3, delayMs: 0 }, 5);
                queue.enqueueJob({ isLegacy: true, count: 1, delayMs: 0 }, 5);
                var cleared = 0;
                var handled = window.FireworksEventHandler.handle(
                    { event: { type: 'FireworksClearQueue' }, data: { widgetId: 'widget-a' } },
                    function () { launches++; },
                    function (jobId) { cleared = queue.clearManaged(jobId); },
                    'widget-a');
                while (timers.length > 0) timers.shift()();
                window.cleared = cleared;
                window.launches = launches;
                window.pendingCount = queue.getPendingCount();
                window.handled = handled;
            ");

            Assert.True(engine.Evaluate("window.handled").AsBoolean());
            Assert.Equal(3, engine.Evaluate("window.cleared").AsNumber());
            Assert.Equal(1, engine.Evaluate("window.launches").AsNumber());
            Assert.Equal(0, engine.Evaluate("window.pendingCount").AsNumber());
        }

        [Fact]
        public void LaunchQueue_EnforcesCapacityAndCancelsOnlySelectedJob()
        {
            var engine = CreateEngine();
            engine.Execute(@"
                var timers = [];
                var queue = window.FireworksEventHandler.createLaunchQueue(
                    function () { return true; },
                    function () { return 0; },
                    { setTimeout: function (callback) { timers.push(callback); } });
                var first = queue.enqueueJob({ jobId: 'job-1', count: 3, delayMs: 10 }, 2);
                var overflow = queue.enqueueJob({ jobId: 'job-2', count: 2, delayMs: 10 }, 2);
                queue.enqueueJob({ isLegacy: true, count: 1, delayMs: 0 }, 2);
                var managedBeforeCancel = queue.getManagedPendingCount();
                var jobs = queue.getJobs();
                var cancelled = queue.clearManaged('job-1');
                window.accepted = first.accepted;
                window.rejected = first.rejected + overflow.rejected;
                window.managedBeforeCancel = managedBeforeCancel;
                window.cancelled = cancelled;
                window.pendingAfterCancel = queue.getPendingCount();
                window.jobPending = jobs[0].pending;
            ");

            Assert.Equal(2, engine.Evaluate("window.accepted").AsNumber());
            Assert.Equal(3, engine.Evaluate("window.rejected").AsNumber());
            Assert.Equal(2, engine.Evaluate("window.managedBeforeCancel").AsNumber());
            Assert.Equal(2, engine.Evaluate("window.jobPending").AsNumber());
            Assert.Equal(2, engine.Evaluate("window.cancelled").AsNumber());
            Assert.Equal(1, engine.Evaluate("window.pendingAfterCancel").AsNumber());
        }

        [Fact]
        public void LaunchQueue_AppliesStartDelayAndRandomizedBetweenBurstVariance()
        {
            var engine = CreateEngine();
            engine.Execute(@"
                var timers = [];
                var scheduledDelays = [];
                Math.random = function () { return 0.99; };
                var queue = window.FireworksEventHandler.createLaunchQueue(
                    function (entry) { scheduledDelays.push(entry.delayBeforeMs); return true; },
                    function (entry) { return window.FireworksEventHandler.getLaunchDelay(entry, 0, 240); },
                    { setTimeout: function (callback) { timers.push(callback); } });
                queue.enqueueJob({
                    jobId: 'show-1',
                    count: 3,
                    startDelayMs: 500,
                    delayMs: 100,
                    delayVarianceMs: 20
                }, 10);
                while (timers.length > 0) timers.shift()();
                window.scheduledDelays = JSON.stringify(scheduledDelays);
            ");

            Assert.Equal("[500,120,120]", engine.Evaluate("window.scheduledDelays").AsString());
        }

        [Fact]
        public void CancelEvent_DoesNotAffectOtherWidgetQueues()
        {
            var engine = CreateEngine();
            engine.Execute(@"
                var cancelCount = 0;
                var handled = window.FireworksEventHandler.handle(
                    { event: { type: 'FireworksClearQueue' }, data: { widgetId: 'widget-b' } },
                    function () {},
                    function () { cancelCount++; },
                    'widget-a');
                window.cancelCount = cancelCount;
                window.handled = handled;
            ");

            Assert.True(engine.Evaluate("window.handled").AsBoolean());
            Assert.Equal(0, engine.Evaluate("window.cancelCount").AsNumber());
        }

        [Fact]
        public void CancelJobEvent_PassesTheSelectedJobId()
        {
            var engine = CreateEngine();
            engine.Execute(@"
                var cancelledJobId = '';
                var handled = window.FireworksEventHandler.handle(
                    { event: { type: 'FireworksCancelJob' }, data: { widgetId: 'widget-a', jobId: 'job-42' } },
                    function () {},
                    function (jobId) { cancelledJobId = jobId; },
                    'widget-a');
                window.cancelledJobId = cancelledJobId;
                window.handled = handled;
            ");

            Assert.True(engine.Evaluate("window.handled").AsBoolean());
            Assert.Equal("job-42", engine.Evaluate("window.cancelledJobId").AsString());
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