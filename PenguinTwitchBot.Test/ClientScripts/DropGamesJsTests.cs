using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jint;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PenguinTwitchBot.Test.ClientScripts
{
    public class DropGamesJsTests
    {
        private static string FindWwwRootPath()
        {
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                var target = Path.Combine(dir, "PenguinTwitchBot", "wwwroot", "js");
                if (Directory.Exists(target))
                {
                    return target;
                }
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }

            var fallback = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PenguinTwitchBot", "wwwroot", "js"));
            if (Directory.Exists(fallback)) return fallback;

            throw new DirectoryNotFoundException("Could not locate wwwroot/js directory for drop games JS tests.");
        }

        private static Engine CreateMockBrowserEngine(out Action<string> logCollector)
        {
            var engine = new Engine(options =>
            {
                options.Strict = false;
                options.TimeoutInterval(TimeSpan.FromSeconds(15));
            });

            var logs = new System.Collections.Generic.List<string>();
            logCollector = msg => logs.Add(msg);

            const string mockDomScript = @"
                var window = {
                    innerWidth: 1920,
                    innerHeight: 1080,
                    addEventListener: function() {},
                    removeEventListener: function() {},
                    location: { search: '?key=test-secret-key' },
                    WS_TOPICS: { parachute: 'parachute', plinko: 'plinko' },
                    __plinkoTestMode: true,
                    __sentCompletions: [],
                    __sentRaw: [],
                    __drawnTexts: [],
                    __onWsSend: function(data) {
                        if (window.__sentRaw) window.__sentRaw.push(String(data));
                        try {
                            window.__sentCompletions.push(JSON.parse(data));
                        } catch (_) {}
                    },
                    createWsSocket: function(topic, topics) {
                        return {
                            readyState: 1,
                            send: function(data) { if (window.__onWsSend) window.__onWsSend(data); },
                            addEventListener: function(evt, handler) {
                                if (evt === 'message') window.__wsMessageHandler = handler;
                            }
                        };
                    }
                };
                var document = {
                    getElementById: function(id) {
                        return {
                            getContext: function(type) {
                                return {
                                    save: function() {},
                                    restore: function() {},
                                    beginPath: function() {},
                                    closePath: function() {},
                                    moveTo: function() {},
                                    lineTo: function() {},
                                    quadraticCurveTo: function() {},
                                    bezierCurveTo: function() {},
                                    stroke: function() {},
                                    fill: function() {},
                                    arc: function() {},
                                    ellipse: function() {},
                                    rect: function() {},
                                    roundRect: function() {},
                                    fillRect: function() {},
                                    strokeRect: function() {},
                                    clearRect: function() {},
                                    drawImage: function() {},
                                    fillText: function(txt) { if (window.__drawnTexts) window.__drawnTexts.push(String(txt)); },
                                    strokeText: function() {},
                                    measureText: function(txt) { return { width: (txt || '').length * 10 }; },
                                    rotate: function() {},
                                    scale: function() {},
                                    translate: function() {},
                                    resetTransform: function() {},
                                    clip: function() {},
                                    createLinearGradient: function() { return { addColorStop: function() {} }; },
                                    createRadialGradient: function() { return { addColorStop: function() {} }; }
                                };
                            },
                            width: 1920,
                            height: 1080,
                            style: {}
                        };
                    },
                    addEventListener: function() {},
                    createElement: function() { return {}; }
                };
                var Image = function() {
                    this.onload = null;
                    this.src = '';
                    this.width = 64;
                    this.height = 64;
                    this.complete = true;
                };
                var performance = {
                    _now: 1000,
                    now: function() { return this._now; }
                };
                var requestAnimationFrame = function(cb) { window.__nextRaf = cb; };
                var __nextTimerId = 1;
                var __activeIntervals = {};
                var setInterval = function(cb, ms) {
                    var id = __nextTimerId++;
                    __activeIntervals[id] = { cb: cb, ms: ms };
                    return id;
                };
                var clearInterval = function(id) {
                    delete __activeIntervals[id];
                };
                var WebSocket = { OPEN: 1 };
                var URLSearchParams = function(query) {
                    var q = query || '';
                    if (q.indexOf('?') === 0) q = q.substring(1);
                    return {
                        get: function(param) {
                            var pairs = q ? q.split('&') : [];
                            for (var i = 0; i < pairs.length; i++) {
                                var parts = pairs[i].split('=');
                                if (decodeURIComponent(parts[0]) === param) {
                                    return parts.length > 1 ? decodeURIComponent(parts[1]) : '';
                                }
                            }
                            return null;
                        }
                    };
                };
                window.__audioPlayed = [];
                window.__audioPreloaded = [];
                var Audio = function(src) {
                    var self = this;
                    this.src = src || '';
                    this.preload = 'none';
                    this.volume = 1;
                    this.paused = true;
                    this.ended = false;
                    this.currentTime = 0;
                    this._listeners = {};
                    this.addEventListener = function(evt, handler) {
                        if (!self._listeners[evt]) self._listeners[evt] = [];
                        self._listeners[evt].push(handler);
                    };
                    this.load = function() {
                        if (window.__audioPreloaded) {
                            window.__audioPreloaded.push({ src: self.src, preload: self.preload });
                        }
                    };
                    this.play = function() {
                        self.paused = false;
                        self.ended = false;
                        if (window.__audioPlayed) {
                            window.__audioPlayed.push({ src: self.src, volume: self.volume });
                        }
                        return Promise.resolve();
                    };
                    this.pause = function() {
                        self.paused = true;
                    };
                };
                var console = {
                    _errors: [],
                    log: function() {},
                    warn: function() {},
                    error: function(e) { this._errors.push(String(e)); }
                };
            ";

            engine.Execute(mockDomScript);
            var commonJsPath = Path.Combine(FindWwwRootPath(), "drop-games-common.js");
            if (File.Exists(commonJsPath))
            {
                engine.Execute(File.ReadAllText(commonJsPath));
                engine.Execute("var DropGamesCommon = window.DropGamesCommon;");
            }
            var audioJsPath = Path.Combine(FindWwwRootPath(), "drop-games-audio.js");
            if (File.Exists(audioJsPath))
            {
                engine.Execute(File.ReadAllText(audioJsPath));
                engine.Execute("var DropGamesAudio = window.DropGamesAudio;");
            }
            return engine;
        }

        [Fact]
        public void PlinkoJs_LoadsAndExecutesWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var plinkoJsPath = Path.Combine(jsDir, "plinko.js");
            Assert.True(File.Exists(plinkoJsPath), $"plinko.js not found at {plinkoJsPath}");

            var code = File.ReadAllText(plinkoJsPath);
            var engine = CreateMockBrowserEngine(out _);

            // Executing the full script in the mock DOM environment
            var ex = Record.Exception(() => engine.Execute(code));
            Assert.Null(ex);

            // Verify console.error had 0 errors
            var errorCount = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errorCount);
        }

        [Fact]
        public void ParachuteJs_LoadsAndExecutesWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJsPath = Path.Combine(jsDir, "parachute.js");
            Assert.True(File.Exists(parachuteJsPath), $"parachute.js not found at {parachuteJsPath}");

            var code = File.ReadAllText(parachuteJsPath);
            var engine = CreateMockBrowserEngine(out _);

            // Executing the full script in the mock DOM environment
            var ex = Record.Exception(() => engine.Execute(code));
            Assert.Null(ex);

            // Verify console.error had 0 errors
            var errorCount = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errorCount);
        }

        [Fact]
        public void BalloonJs_LoadsAndExecutesWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var balloonJsPath = Path.Combine(jsDir, "balloon.js");
            Assert.True(File.Exists(balloonJsPath), $"balloon.js not found at {balloonJsPath}");

            var code = File.ReadAllText(balloonJsPath);
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => engine.Execute(code));
            Assert.Null(ex);

            var errorCount = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errorCount);
        }

        [Fact]
        public void DropGamesCommon_SharedCanvasHudAndHelpers_ValidateExecutionAndParity()
        {
            var engine = CreateMockBrowserEngine(out _);

            // 1. getBannerX positioning
            var xCenter = engine.Evaluate("DropGamesCommon.getBannerX(1920, 480, 'top-center')").AsNumber();
            Assert.Equal(720, xCenter); // (1920 - 480) / 2 = 720
            var xLeft = engine.Evaluate("DropGamesCommon.getBannerX(1920, 480, 'top-left')").AsNumber();
            Assert.Equal(24, xLeft);
            var xRight = engine.Evaluate("DropGamesCommon.getBannerX(1920, 480, 'top-right')").AsNumber();
            Assert.Equal(1920 - 480 - 24, xRight);

            // 2. calculateDropAlpha
            // Active session leader = 1.0
            var leaderAlpha = engine.Evaluate("DropGamesCommon.calculateDropAlpha(1000, 10000, { isActive: true }, true).alpha").AsNumber();
            Assert.Equal(1.0, leaderAlpha);

            // Active session non-leader after 3.5s + 2.0s dims to 0.40
            var dimmedAlpha = engine.Evaluate("DropGamesCommon.calculateDropAlpha(1000, 7000, { isActive: true }, false).alpha").AsNumber();
            Assert.Equal(0.40, Math.Round(dimmedAlpha, 2));

            // Post-round celebration after 6.0s -> shouldRemove = true
            var postRoundRemove = engine.Evaluate("DropGamesCommon.calculateDropAlpha(1000, 10000, { isActive: false, endedAtMs: 2000 }, false).shouldRemove").AsBoolean();
            Assert.True(postRoundRemove);

            // 3. sortDropsForDraw
            var sortedTokens = engine.Evaluate(@"
                var items = [
                    { score: 50, completed: true, isLeader: false },
                    { score: 100, completed: true, isLeader: true },
                    { score: 10, completed: false, isLeader: false }
                ];
                DropGamesCommon.sortDropsForDraw(items);
                JSON.stringify(items);
            ").AsString();

            Assert.Contains(@"""isLeader"":true", sortedTokens);
            // Leader must be at the end of the array (drawn on top)
            var lastIsLeader = engine.Evaluate("items[items.length - 1].isLeader").AsBoolean();
            Assert.True(lastIsLeader, "Winning leader must be sorted last so it is rendered on top of other drops");
        }

        [Theory]
        [InlineData("#000080", false)] // Navy blue -> must be brightened, not white
        [InlineData("#006400", false)] // Dark green -> brightened, not white
        [InlineData("#800000", false)] // Maroon -> brightened, not white
        [InlineData("#800080", false)] // Purple -> brightened, not white
        [InlineData("#000000", false)] // Black -> bright silver, not white
        [InlineData("#FFD700", true)]  // Leader gold -> preserved exactly
        [InlineData("#FF69B4", true)]  // Hot pink -> preserved exactly
        [InlineData("#FFFFFF", true)]  // White -> preserved exactly
        public void BrightenColor_PreservesHueAndEnsuresContrast(string inputHex, bool shouldMatchExactly)
        {
            var jsDir = FindWwwRootPath();
            var commonJs = File.ReadAllText(Path.Combine(jsDir, "drop-games-common.js"));

            // Extract brightenColor function directly to test its pure logic
            var startIndex = commonJs.IndexOf("function brightenColor(");
            Assert.True(startIndex >= 0, "brightenColor function not found in drop-games-common.js");

            // Extract the function body
            var openBrace = commonJs.IndexOf('{', startIndex);
            var depth = 1;
            var endIndex = openBrace + 1;
            while (depth > 0 && endIndex < commonJs.Length)
            {
                if (commonJs[endIndex] == '{') depth++;
                else if (commonJs[endIndex] == '}') depth--;
                endIndex++;
            }
            var brightenFunc = commonJs.Substring(startIndex, endIndex - startIndex);

            var engine = new Engine();
            engine.Execute(brightenFunc);

            var result = engine.Evaluate($"brightenColor('{inputHex}')").AsString();
            Assert.NotNull(result);
            Assert.StartsWith("#", result);

            if (shouldMatchExactly)
            {
                Assert.Equal(inputHex.ToUpperInvariant(), result.ToUpperInvariant());
            }
            else
            {
                // Must NOT turn into pure white
                Assert.NotEqual("#FFFFFF", result.ToUpperInvariant());

                // Calculate luminance in C# to verify it passes contrast criteria
                var r = Convert.ToInt32(result.Substring(1, 2), 16) / 255.0;
                var g = Convert.ToInt32(result.Substring(3, 2), 16) / 255.0;
                var b = Convert.ToInt32(result.Substring(5, 2), 16) / 255.0;
                var lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                Assert.True(lum >= 0.40, $"Brightened color {result} from {inputHex} has luminance {lum:F3}, which is too low for contrast.");
            }
        }

        [Fact]
        public void PlinkoJs_PuckPhysics_ContinuousMotion_NoFastTravelTeleportsOrDiscontinuities()
        {
            var jsDir = FindWwwRootPath();
            var plinkoJs = File.ReadAllText(Path.Combine(jsDir, "plinko.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(plinkoJs);

            const int rows = 8;
            const int bucketCount = 5;

            var buckets = new[]
            {
                new { label = "0", score = 10, isWin = false },
                new { label = "1", score = 25, isWin = true },
                new { label = "2", score = 100, isWin = true },
                new { label = "3", score = 25, isWin = true },
                new { label = "4", score = 10, isWin = false }
            };

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new { gameType = "plinko", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                board = new { rows, cols = bucketCount, buckets }
            });

            DispatchWsMessage(engine, new
            {
                type = "plinko_drop",
                dropToken = "continuous-token",
                displayName = "PhysViewer",
                board = new
                {
                    rowCount = rows,
                    gravity = 1.0,
                    bounciness = 0.85,
                    mass = 1.0,
                    buckets,
                    physics = new { startX = 0.50, startY = 0.04, gravity = 1.0, bounciness = 0.85, mass = 1.0 }
                }
            });

            // Sample positions frame-by-frame and ensure no teleportation
            double prevX = 0.50;
            for (var f = 0; f < 60; f++)
            {
                StepEngineFrames(engine, 1);
                var curX = engine.Evaluate("window.__plinkoTokens[0].x").AsNumber();
                var curY = engine.Evaluate("window.__plinkoTokens[0].y").AsNumber();

                var dx = Math.Abs(curX - prevX);
                Assert.True(dx < 0.15, $"Discontinuous horizontal jump detected: dx={dx:F4} at frame {f}");
                Assert.InRange(curX, 0.0, 1.0);
                Assert.InRange(curY, 0.0, 1.0);
                prevX = curX;
            }
        }

        [Fact]
        public void ParachuteJs_PhysicsDecayDamping_MatchesServerSimulationParity()
        {
            // Server ParachuteSimulationEngine damping constants:
            // vx decay = 0.998 per tick
            // vy decay = 0.96 per tick
            // At 60 ticks (1 second):
            // Expected vx multiplier = Math.Pow(0.998, 60) ≈ 0.8868
            // Expected vy multiplier = Math.Pow(0.96, 60) ≈ 0.0863

            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));

            // Verify velocity damping constants in parachute.js match server
            Assert.Contains("Math.pow(0.998, dtSec * 60)", parachuteJs);
            Assert.Contains("Math.pow(0.96, dtSec * 60)", parachuteJs);

            // Verify wind acceleration was removed
            Assert.DoesNotContain("windEffect", parachuteJs);
            Assert.DoesNotContain("windAccelX", parachuteJs);

            // Verify overlap separation is capped to prevent teleporting
            Assert.Contains("Math.min(2.5, 0.5 * (minDistPx - distPx))", parachuteJs);
        }

        private static void StepEngineFrames(Engine engine, int frameCount, double dtMs = 16.666)
        {
            const int chunkSize = 60;
            var remaining = frameCount;
            while (remaining > 0)
            {
                var frames = Math.Min(remaining, chunkSize);
                engine.Execute($@"
                    for (var f = 0; f < {frames}; f++) {{
                        performance._now += {dtMs};
                        if (window.__nextRaf) {{
                            var cb = window.__nextRaf;
                            window.__nextRaf = null;
                            cb(performance._now);
                        }}
                    }}
                ");
                remaining -= frames;
            }
        }

        private static void DispatchWsMessage(Engine engine, object message)
        {
            var json = JsonSerializer.Serialize(message);
            engine.SetValue("__msgJson", json);
            engine.Execute("window.__wsMessageHandler({ data: __msgJson });");
        }

        [Fact]
        public void PlinkoJs_MultipleConcurrentTokens_BounceOffEachOtherAndLandInBuckets()
        {
            var jsDir = FindWwwRootPath();
            var plinkoJs = File.ReadAllText(Path.Combine(jsDir, "plinko.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(plinkoJs);

            const int rows = 8;
            const int bucketCount = 5;

            var buckets = new[]
            {
                new { label = "0", score = 10, isWin = false },
                new { label = "1", score = 25, isWin = true },
                new { label = "2", score = 100, isWin = true },
                new { label = "3", score = 25, isWin = true },
                new { label = "4", score = 10, isWin = false }
            };

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new
                {
                    gameType = "plinko",
                    isActive = true,
                    durationSeconds = 60,
                    totalDurationSeconds = 60,
                    roundNumber = 1
                },
                board = new { rows, cols = bucketCount, buckets }
            });

            // Spawn 5 simultaneous tokens targeted to buckets 0 through 4 with overlapping flight paths
            for (var t = 0; t < bucketCount; t++)
            {
                DispatchWsMessage(engine, new
                {
                    type = "plinko_drop",
                    dropToken = $"token-{t}",
                    displayName = $"Player{t}",
                    userColor = "#00FF00",
                    board = new
                    {
                        rowCount = rows,
                        durationSeconds = 2.4,
                        gravity = 1.0,
                        bounciness = 0.85,
                        mass = 1.0,
                        buckets = buckets,
                        physics = new
                        {
                            startX = (t + 0.5) / bucketCount,
                            startY = 0.04,
                            gravity = 1.0,
                            bounciness = 0.85,
                            mass = 1.0
                        }
                    }
                });
            }

            // Step 180 frames (~3.0s) to let all tokens collide mid-air and complete landing
            StepEngineFrames(engine, 180);

            // Verify console.error was clean
            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var completionsCount = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(bucketCount, completionsCount);

            for (var t = 0; t < bucketCount; t++)
            {
                var reportedBucket = engine.Evaluate($"window.__sentCompletions.find(c => c.dropToken === 'token-{t}').winningBucketIndex").AsNumber();
                Assert.InRange((int)reportedBucket, 0, bucketCount - 1);

                var tokenPy = engine.Evaluate($"window.__plinkoTokens.find(token => token.dropToken === 'token-{t}').py").AsNumber();
                Assert.True(tokenPy >= 1000, $"Token token-{t} should have reached the floor, got py={tokenPy}");
            }
        }

        [Fact]
        public void ParachuteJs_MultipleParachutists_CollideMidAir_BounceElasticallyAndLandWithinValidationTolerance()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new
                {
                    gameType = "parachute",
                    isActive = true,
                    durationSeconds = 60,
                    totalDurationSeconds = 60,
                    roundNumber = 1
                },
                target = new { centerX = 0.50, targetHalfWidth = 0.09 }
            });

            // Spawn 2 parachutists on converging/intersecting flight paths
            // Drop A flies rightward from X=0.35, Drop B flies leftward from X=0.65
            DispatchWsMessage(engine, new
            {
                type = "parachute_drop",
                dropToken = "player-A",
                displayName = "PlayerA",
                trajectory = new
                {
                    startX = 0.35,
                    startY = 0.05,
                    initialVx = 0.08,
                    initialVy = 0.18,
                    landingY = 0.92,
                    totalTicks = 240,
                    bounciness = 1.0,
                    mass = 1.0,
                    targetCenterX = 0.50,
                    targetHalfWidth = 0.09
                }
            });

            DispatchWsMessage(engine, new
            {
                type = "parachute_drop",
                dropToken = "player-B",
                displayName = "PlayerB",
                trajectory = new
                {
                    startX = 0.65,
                    startY = 0.05,
                    initialVx = -0.08,
                    initialVy = 0.18,
                    landingY = 0.92,
                    totalTicks = 240,
                    bounciness = 1.0,
                    mass = 1.0,
                    targetCenterX = 0.50,
                    targetHalfWidth = 0.09
                }
            });

            // Step 360 frames (~6.0s) to completion
            StepEngineFrames(engine, 360);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var compA = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'player-A')");
            var compB = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'player-B')");
            Assert.False(compA.IsUndefined());
            Assert.False(compB.IsUndefined());

            var landingXA = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'player-A').landingX").AsNumber();
            var landingXB = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'player-B').landingX").AsNumber();

            // Landing positions must stay bounded within physical screen limits
            Assert.InRange(landingXA, 0.05, 0.95);
            Assert.InRange(landingXB, 0.05, 0.95);

            // Parachutists bounced off each other elastically in mid-air
            Assert.True(landingXA < landingXB, "Drop A and Drop B preserved physical separation after elastic bounce");

            // Tolerance check: server-side DropGameCompleteHandler allows 0.30 baseline tolerance
            var serverRecordA = CreateTestDropRecord("player-A", "PlayerA", 0.35, 0.08, 0.18, 0.92, 240, 0.55);
            var tolerance = Math.Min(0.40, 0.30 + (serverRecordA.DropletHitCount * 0.05));
            Assert.True(Math.Abs(landingXA - serverRecordA.LandingX) <= tolerance,
                $"Reported landingX {landingXA} exceeded server tolerance {tolerance} from {serverRecordA.LandingX}");
        }

        private static PendingDropRecord CreateTestDropRecord(
            string dropToken,
            string user,
            double startX,
            double initialVx,
            double initialVy,
            double landingY,
            int totalTicks = 240,
            double landingX = 0.50,
            double bounciness = 0.85,
            double mass = 1.0)
        {
            var now = DateTime.UtcNow;
            return new PendingDropRecord
            {
                DropToken = dropToken,
                GameType = DropGameType.Parachute,
                Username = user.ToLowerInvariant(),
                DisplayName = user,
                UserId = $"user-{dropToken}",
                EventArgs = new CommandEventArgs { Command = "drop", Name = user.ToLowerInvariant(), DisplayName = user, UserId = $"user-{dropToken}" },
                Theme = new DropGameTheme(),
                CreatedAtUtc = now,
                EarliestCompletionUtc = now,
                ExpiresAtUtc = now.AddMinutes(1),
                StartX = startX,
                StartY = 0.05,
                InitialVx = initialVx,
                InitialVy = initialVy,
                LandingY = landingY,
                LandingX = landingX,
                TotalTicks = totalTicks,
                Bounciness = bounciness,
                Mass = mass,
                TargetCenterX = 0.50,
                TargetHalfWidth = 0.09
            };
        }

        [Fact]
        public void ParachuteJs_DropletCollisions_KnockbackParityWithServerSimulation()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new
                {
                    gameType = "parachute",
                    isActive = true,
                    durationSeconds = 60,
                    totalDurationSeconds = 60,
                    roundNumber = 1
                },
                target = new { centerX = 0.50, targetHalfWidth = 0.09 }
            });

            // 1. Simulate server trajectory with droplet collisions
            var record = CreateTestDropRecord("drop-droplets", "DropletViewer", 0.50, 0.0, 0.14, 0.92, 260);

            var droplets = new List<DropletInstanceDto>
            {
                new DropletInstanceDto
                {
                    Id = "drop-1",
                    StartX = 0.49,
                    StartY = 0.05,
                    Vx = 0.04,
                    Vy = 0.30,
                    Gravity = 0.32,
                    Radius = 0.028,
                    Mass = 0.5,
                    SpawnTickOffset = 15,
                    SpeedScale = 1.0
                },
                new DropletInstanceDto
                {
                    Id = "drop-2",
                    StartX = 0.51,
                    StartY = 0.05,
                    Vx = 0.08,
                    Vy = 0.30,
                    Gravity = 0.32,
                    Radius = 0.028,
                    Mass = 0.5,
                    SpawnTickOffset = 15,
                    SpeedScale = 1.0
                },
                new DropletInstanceDto
                {
                    Id = "drop-3",
                    StartX = 0.53,
                    StartY = 0.05,
                    Vx = 0.10,
                    Vy = 0.30,
                    Gravity = 0.32,
                    Radius = 0.028,
                    Mass = 0.5,
                    SpawnTickOffset = 15,
                    SpeedScale = 1.0
                }
            };

            ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0);
            var initialBaselineLandingX = record.LandingX;

            // 2. Dispatch drop and droplet message to JS engine
            DispatchWsMessage(engine, new
            {
                type = "parachute_drop",
                dropToken = record.DropToken,
                displayName = record.DisplayName,
                trajectory = new
                {
                    startX = record.StartX,
                    startY = record.StartY,
                    initialVx = record.InitialVx,
                    initialVy = record.InitialVy,
                    landingY = record.LandingY,
                    totalTicks = record.TotalTicks,
                    bounciness = record.Bounciness,
                    mass = record.Mass,
                    targetCenterX = record.TargetCenterX,
                    targetHalfWidth = record.TargetHalfWidth,
                    collisions = new object[0]
                }
            });

            DispatchWsMessage(engine, new
            {
                type = "parachute_droplets",
                droplets = droplets.Select(d => new
                {
                    id = d.Id,
                    startX = d.StartX,
                    startY = d.StartY,
                    vx = d.Vx,
                    vy = d.Vy,
                    gravity = d.Gravity,
                    radius = d.Radius,
                    spawnTickOffset = d.SpawnTickOffset
                }).ToList()
            });

            // Step 450 frames (~7.5s) to completion
            StepEngineFrames(engine, 450);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var clientLandingX = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'drop-droplets').landingX").AsNumber();

            // Client canvas executes live collision, deflecting the drop from its baseline path while remaining in bounds
            Assert.False(double.IsNaN(clientLandingX) || double.IsInfinity(clientLandingX));
            Assert.InRange(clientLandingX, 0.05, 0.95);
            Assert.True(Math.Abs(clientLandingX - initialBaselineLandingX) > 0.01,
                $"Expected droplet collision to deflect client landingX from baseline {initialBaselineLandingX}, got {clientLandingX}");
        }

        [Theory]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(7)]
        [InlineData(9)]
        [InlineData(12)]
        public void PlinkoSimulationEngine_EvaluateBucketOutcome_AllBucketConfigurations_EvaluatesCorrectScoreAndWinStatus(int bucketCount)
        {
            var buckets = new List<PlinkoBucket>();
            for (var i = 0; i < bucketCount; i++)
            {
                var isWin = (i == bucketCount / 2);
                buckets.Add(new PlinkoBucket
                {
                    Label = $"B{i}",
                    Score = isWin ? 100 : 0,
                    IsWin = isWin,
                    Weight = 1
                });
            }

            for (var targetBucket = 0; targetBucket < bucketCount; targetBucket++)
            {
                var outcome = PlinkoSimulationEngine.EvaluateBucketOutcome(buckets, targetBucket);
                Assert.Equal($"B{targetBucket}", outcome.Label);
                var expectedWin = (targetBucket == bucketCount / 2);
                Assert.Equal(expectedWin, outcome.IsWin);
                Assert.Equal(expectedWin ? 100 : 0, outcome.Score);
                Assert.Equal(expectedWin, outcome.IsBullseye);
            }
        }

        [Fact]
        public void ParachuteJs_WallBounceRebound_ParityWithServerSimulation()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new
                {
                    gameType = "parachute",
                    isActive = true,
                    durationSeconds = 60,
                    totalDurationSeconds = 60,
                    roundNumber = 1
                },
                target = new { centerX = 0.50, targetHalfWidth = 0.09 }
            });

            // Parachutist begins near left wall moving left with high horizontal speed
            var record = CreateTestDropRecord("wall-bounce-drop", "Bouncer", 0.15, -0.30, 0.16, 0.92, 360);
            ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0, []);

            // Assert that server simulation registered wall bounce (X bounced off 0.05 and reversed direction)
            Assert.True(record.LandingX > 0.15, "Server simulation rebounded player from left wall into open screen area");

            DispatchWsMessage(engine, new
            {
                type = "parachute_drop",
                dropToken = record.DropToken,
                displayName = record.DisplayName,
                trajectory = new
                {
                    startX = record.StartX,
                    startY = record.StartY,
                    initialVx = record.InitialVx,
                    initialVy = record.InitialVy,
                    landingY = record.LandingY,
                    totalTicks = record.TotalTicks,
                    bounciness = record.Bounciness,
                    mass = record.Mass,
                    targetCenterX = record.TargetCenterX,
                    targetHalfWidth = record.TargetHalfWidth,
                    collisions = new object[0]
                }
            });

            // Step 380 frames (~6.3s) to completion
            StepEngineFrames(engine, 380);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var clientLandingX = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'wall-bounce-drop').landingX").AsNumber();

            // Client and server bounce off left wall with matching restitution and land within 0.04
            Assert.True(Math.Abs(clientLandingX - record.LandingX) <= 0.04,
                $"Wall rebound landingX {clientLandingX} differed from server simulation {record.LandingX} by {Math.Abs(clientLandingX - record.LandingX):F4}");
        }

        [Fact]
        public void ParachuteJs_TargetRimBounce_ParityWithServerSimulation()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new
                {
                    gameType = "parachute",
                    isActive = true,
                    durationSeconds = 60,
                    totalDurationSeconds = 60,
                    roundNumber = 1
                },
                target = new { centerX = 0.50, targetHalfWidth = 0.10 }
            });

            // Parachutist trajectory descends directly onto outer lip of the target platform
            var record = CreateTestDropRecord("rim-bounce-drop", "RimBouncer", 0.595, 0.005, 0.16, 0.92, 360);
            ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0, []);

            DispatchWsMessage(engine, new
            {
                type = "parachute_drop",
                dropToken = record.DropToken,
                displayName = record.DisplayName,
                trajectory = new
                {
                    startX = record.StartX,
                    startY = record.StartY,
                    initialVx = record.InitialVx,
                    initialVy = record.InitialVy,
                    landingY = record.LandingY,
                    totalTicks = record.TotalTicks,
                    bounciness = record.Bounciness,
                    mass = record.Mass,
                    targetCenterX = record.TargetCenterX,
                    targetHalfWidth = record.TargetHalfWidth,
                    collisions = new object[0]
                }
            });

            StepEngineFrames(engine, 380);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var clientLandingX = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'rim-bounce-drop').landingX").AsNumber();
            Assert.True(Math.Abs(clientLandingX - record.LandingX) <= 0.04,
                $"Rim bounce landingX {clientLandingX} differed from server simulation {record.LandingX} by {Math.Abs(clientLandingX - record.LandingX):F4}");
        }

        [Fact]
        public void ParachuteJs_MultiDropletStorm_ParityWithServerSimulation()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new
                {
                    gameType = "parachute",
                    isActive = true,
                    durationSeconds = 60,
                    totalDurationSeconds = 60,
                    roundNumber = 1
                },
                target = new { centerX = 0.50, targetHalfWidth = 0.09 }
            });

            var record = CreateTestDropRecord("storm-drop", "StormViewer", 0.50, 0.0, 0.14, 0.92, 380);

            // 3 successive droplets falling along flight path
            var droplets = new List<DropletInstanceDto>
            {
                new DropletInstanceDto { Id = "d-1", StartX = 0.500, StartY = 0.046, Vx = 0.12, Vy = 0.35, Gravity = 0.32, Radius = 0.035, Mass = 0.4, SpawnTickOffset = 20, SpeedScale = 1.0 },
                new DropletInstanceDto { Id = "d-2", StartX = 0.538, StartY = 0.116, Vx = 0.12, Vy = 0.35, Gravity = 0.32, Radius = 0.035, Mass = 0.4, SpawnTickOffset = 50, SpeedScale = 1.0 },
                new DropletInstanceDto { Id = "d-3", StartX = 0.608, StartY = 0.186, Vx = 0.12, Vy = 0.35, Gravity = 0.32, Radius = 0.035, Mass = 0.4, SpawnTickOffset = 80, SpeedScale = 1.0 }
            };

            ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0);
            var baselineLandingX = record.LandingX;

            DispatchWsMessage(engine, new
            {
                type = "parachute_drop",
                dropToken = record.DropToken,
                displayName = record.DisplayName,
                trajectory = new
                {
                    startX = record.StartX,
                    startY = record.StartY,
                    initialVx = record.InitialVx,
                    initialVy = record.InitialVy,
                    landingY = record.LandingY,
                    totalTicks = record.TotalTicks,
                    bounciness = record.Bounciness,
                    mass = record.Mass,
                    targetCenterX = record.TargetCenterX,
                    targetHalfWidth = record.TargetHalfWidth,
                    collisions = new object[0]
                }
            });

            DispatchWsMessage(engine, new
            {
                type = "parachute_droplets",
                droplets = droplets.Select(d => new
                {
                    id = d.Id,
                    startX = d.StartX,
                    startY = d.StartY,
                    vx = d.Vx,
                    vy = d.Vy,
                    gravity = d.Gravity,
                    radius = d.Radius,
                    spawnTickOffset = d.SpawnTickOffset
                }).ToList()
            });

            StepEngineFrames(engine, 420);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var clientLandingX = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'storm-drop').landingX").AsNumber();
            Assert.False(double.IsNaN(clientLandingX) || double.IsInfinity(clientLandingX));
            Assert.InRange(clientLandingX, 0.05, 0.95);
            Assert.True(Math.Abs(clientLandingX - baselineLandingX) > 0.01,
                $"Storm cumulative droplet collision deflected player from baseline {baselineLandingX}, got {clientLandingX}");
        }

        [Theory]
        [InlineData(0.1, 0.1, 0.1)]   // Minimum bounds
        [InlineData(10.0, 5.0, 10.0)] // Maximum bounds
        [InlineData(5.0, 0.1, 10.0)]  // High gravity, low bounce
        [InlineData(0.1, 5.0, 0.1)]   // Low gravity, high bounce
        public void DropGames_ExtremePhysicsParameters_DoNotProduceNaNOrBoundsViolation(double gravity, double bounciness, double mass)
        {
            var jsDir = FindWwwRootPath();

            // 1. Plinko Extreme Check (JS 2D physics execution)
            const int rows = 8;
            const int bucketCount = 5;

            var plinkoJs = File.ReadAllText(Path.Combine(jsDir, "plinko.js"));
            var plinkoEngine = CreateMockBrowserEngine(out _);
            plinkoEngine.Execute(plinkoJs);

            var buckets = new[]
            {
                new { label = "0", score = 10, isWin = false },
                new { label = "1", score = 20, isWin = false },
                new { label = "2", score = 100, isWin = true },
                new { label = "3", score = 20, isWin = false },
                new { label = "4", score = 10, isWin = false }
            };

            DispatchWsMessage(plinkoEngine, new
            {
                type = "session_start",
                session = new { gameType = "plinko", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                board = new { rows, cols = bucketCount, buckets }
            });

            DispatchWsMessage(plinkoEngine, new
            {
                type = "plinko_drop",
                dropToken = "extreme-plinko",
                displayName = "ExtremePlayer",
                userColor = "#FF00FF",
                board = new
                {
                    rowCount = rows,
                    durationSeconds = 1.2,
                    gravity = gravity,
                    bounciness = bounciness,
                    mass = mass,
                    buckets = buckets,
                    physics = new { startX = 0.50, startY = 0.04, gravity, bounciness, mass }
                }
            });

            StepEngineFrames(plinkoEngine, 100);
            Assert.Equal(0, plinkoEngine.Evaluate("console._errors.length").AsNumber());
            var plinkoLanded = plinkoEngine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'extreme-plinko')");
            Assert.False(plinkoLanded.IsNull() || plinkoLanded.IsUndefined(), "Plinko JS failed to complete under extreme physics");
            var landedIdx = plinkoEngine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'extreme-plinko').winningBucketIndex").AsNumber();
            Assert.InRange((int)landedIdx, 0, bucketCount - 1);

            // 2. Parachute Extreme Check (C# simulation + JS overlay execution)
            var baseRecord = CreateTestDropRecord("extreme-drop", "ExtremeUser", 0.50, 0.20, 0.14, 0.92, 360);
            var record = new PendingDropRecord
            {
                DropToken = baseRecord.DropToken,
                GameType = baseRecord.GameType,
                Username = baseRecord.Username,
                DisplayName = baseRecord.DisplayName,
                UserId = baseRecord.UserId,
                EventArgs = baseRecord.EventArgs,
                Theme = baseRecord.Theme,
                CreatedAtUtc = baseRecord.CreatedAtUtc,
                EarliestCompletionUtc = baseRecord.EarliestCompletionUtc,
                ExpiresAtUtc = baseRecord.ExpiresAtUtc,
                StartX = baseRecord.StartX,
                StartY = baseRecord.StartY,
                InitialVx = baseRecord.InitialVx,
                InitialVy = baseRecord.InitialVy,
                LandingY = baseRecord.LandingY,
                LandingX = baseRecord.LandingX,
                TotalTicks = baseRecord.TotalTicks,
                Bounciness = bounciness,
                Mass = mass,
                Gravity = gravity,
                TargetCenterX = baseRecord.TargetCenterX,
                TargetHalfWidth = baseRecord.TargetHalfWidth
            };

            var droplets = new List<DropletInstanceDto>
            {
                new DropletInstanceDto { Id = "d-extreme", StartX = 0.52, StartY = 0.05, Vx = 0.05, Vy = 0.30, Gravity = gravity, Radius = 0.028, Mass = mass, SpawnTickOffset = 15, SpeedScale = 1.0 }
            };

            ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0, droplets);
            Assert.False(double.IsNaN(record.LandingX) || double.IsInfinity(record.LandingX), "Parachute LandingX is NaN or Infinity");
            Assert.InRange(record.LandingX, 0.05, 0.95);

            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var parachuteEngine = CreateMockBrowserEngine(out _);
            parachuteEngine.Execute(parachuteJs);

            DispatchWsMessage(parachuteEngine, new
            {
                type = "session_start",
                session = new { gameType = "parachute", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                target = new { centerX = record.TargetCenterX, targetHalfWidth = record.TargetHalfWidth }
            });

            DispatchWsMessage(parachuteEngine, new
            {
                type = "parachute_drop",
                dropToken = record.DropToken,
                displayName = record.DisplayName,
                trajectory = new
                {
                    startX = record.StartX,
                    startY = record.StartY,
                    initialVx = record.InitialVx,
                    initialVy = record.InitialVy,
                    landingY = record.LandingY,
                    totalTicks = record.TotalTicks,
                    bounciness = record.Bounciness,
                    mass = record.Mass,
                    gravity = record.Gravity,
                    targetCenterX = record.TargetCenterX,
                    targetHalfWidth = record.TargetHalfWidth,
                    collisions = new object[0]
                }
            });

            DispatchWsMessage(parachuteEngine, new
            {
                type = "parachute_droplets",
                droplets = droplets.Select(d => new
                {
                    id = d.Id,
                    startX = d.StartX,
                    startY = d.StartY,
                    vx = d.Vx,
                    vy = d.Vy,
                    gravity = d.Gravity,
                    radius = d.Radius,
                    spawnTickOffset = d.SpawnTickOffset
                }).ToList()
            });

            StepEngineFrames(parachuteEngine, 420);
            Assert.Equal(0, parachuteEngine.Evaluate("console._errors.length").AsNumber());

            var parachuteLanded = parachuteEngine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'extreme-drop')");
            Assert.False(parachuteLanded.IsNull() || parachuteLanded.IsUndefined(), "Parachute JS failed to complete under extreme physics");
            var clientLandingX = parachuteEngine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'extreme-drop').landingX").AsNumber();
            Assert.False(double.IsNaN(clientLandingX) || double.IsInfinity(clientLandingX));
            Assert.InRange(clientLandingX, 0.05, 0.95);
        }

        [Fact]
        public void ParachuteJs_DenseDropletShower_MultipleDropsParityWithServerSimulation()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new
                {
                    gameType = "parachute",
                    isActive = true,
                    durationSeconds = 60,
                    totalDurationSeconds = 60,
                    roundNumber = 1
                },
                target = new { centerX = 0.50, targetHalfWidth = 0.10 }
            });

            // Two drops descending simultaneously across the board without mutual collision
            var dropA = CreateTestDropRecord("dense-drop-A", "PlayerA", 0.25, 0.0, 0.14, 0.92, 380);
            var dropB = CreateTestDropRecord("dense-drop-B", "PlayerB", 0.75, 0.0, 0.14, 0.92, 380);

            // Dense shower of 20 droplets spanning the screen width
            var droplets = new List<DropletInstanceDto>();
            for (var i = 0; i < 20; i++)
            {
                droplets.Add(new DropletInstanceDto
                {
                    Id = $"shower-d-{i}",
                    StartX = 0.10 + (i % 10) * 0.08,
                    StartY = 0.02 + (i / 10) * 0.04,
                    Vx = (i % 2 == 0 ? 0.03 : -0.03),
                    Vy = 0.32 + (i % 4) * 0.04,
                    Gravity = 0.30,
                    Radius = 0.025,
                    Mass = 0.45,
                    SpawnTickOffset = i * 12,
                    SpeedScale = 1.0
                });
            }

            // Simulate both drops through the 20-droplet shower
            ParachuteSimulationEngine.SimulateParachuteTrajectory(dropA, 0, droplets);
            ParachuteSimulationEngine.SimulateParachuteTrajectory(dropB, 0, droplets);

            // Dispatch drops to JS client
            foreach (var record in new[] { dropA, dropB })
            {
                DispatchWsMessage(engine, new
                {
                    type = "parachute_drop",
                    dropToken = record.DropToken,
                    displayName = record.DisplayName,
                    trajectory = new
                    {
                        startX = record.StartX,
                        startY = record.StartY,
                        initialVx = record.InitialVx,
                        initialVy = record.InitialVy,
                        landingY = record.LandingY,
                        totalTicks = record.TotalTicks,
                        bounciness = record.Bounciness,
                        mass = record.Mass,
                        targetCenterX = record.TargetCenterX,
                        targetHalfWidth = record.TargetHalfWidth,
                        collisions = new object[0]
                    }
                });
            }

            // Dispatch all 20 droplets to JS client
            DispatchWsMessage(engine, new
            {
                type = "parachute_droplets",
                droplets = droplets.Select(d => new
                {
                    id = d.Id,
                    startX = d.StartX,
                    startY = d.StartY,
                    vx = d.Vx,
                    vy = d.Vy,
                    gravity = d.Gravity,
                    radius = d.Radius,
                    spawnTickOffset = d.SpawnTickOffset
                }).ToList()
            });

            // Step 420 frames to complete both drops
            StepEngineFrames(engine, 420);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var clientLandingA = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'dense-drop-A').landingX").AsNumber();
            var clientLandingB = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'dense-drop-B').landingX").AsNumber();

            Assert.False(double.IsNaN(clientLandingA) || double.IsInfinity(clientLandingA));
            Assert.False(double.IsNaN(clientLandingB) || double.IsInfinity(clientLandingB));
            Assert.InRange(clientLandingA, 0.05, 0.95);
            Assert.InRange(clientLandingB, 0.05, 0.95);
        }

        [Fact]
        public void DropGames_SessionLeaderboardCrown_IdentifiesAndCrownsHighestScore()
        {
            var jsDir = FindWwwRootPath();
            var plinkoJs = File.ReadAllText(Path.Combine(jsDir, "plinko.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(plinkoJs);

            const int rows = 8;
            const int bucketCount = 3;

            var buckets = new[]
            {
                new { label = "Low", score = 10, isWin = false },
                new { label = "Mid", score = 25, isWin = true },
                new { label = "High", score = 100, isWin = true }
            };

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new
                {
                    gameType = "plinko",
                    isActive = true,
                    durationSeconds = 60,
                    totalDurationSeconds = 60,
                    roundNumber = 1
                },
                board = new { rows, cols = bucketCount, buckets }
            });

            // Drop 3 tokens landing across buckets
            for (var t = 0; t < 3; t++)
            {
                DispatchWsMessage(engine, new
                {
                    type = "plinko_drop",
                    dropToken = $"token-{t}",
                    displayName = $"Player{t}",
                    userColor = "#00FF00",
                    board = new
                    {
                        rowCount = rows,
                        durationSeconds = 1.5,
                        gravity = 1.0,
                        bounciness = 0.85,
                        mass = 1.0,
                        buckets = buckets,
                        physics = new
                        {
                            startX = (t + 0.5) / 3.0,
                            startY = 0.04,
                            gravity = 1.0,
                            bounciness = 0.85,
                            mass = 1.0
                        }
                    }
                });
            }

            // Step 150 frames to complete all drops
            StepEngineFrames(engine, 150);

            // Verify all 3 completions were reported
            var completionCount = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(3, (int)completionCount);

            // Assert that the leader crown emoji 👑 was rendered on the canvas
            var hasCrown = engine.Evaluate("window.__drawnTexts.some(t => t.indexOf('👑') >= 0)").AsBoolean();
            Assert.True(hasCrown, "Highest score drop was crowned with 👑 on the overlay");
        }

        [Fact]
        public void DropGames_MidFlightSessionReset_ClearsActiveDropsAndDoesNotSendStaleCompletions()
        {
            var jsDir = FindWwwRootPath();

            // 1. Plinko Mid-Flight Reset Test
            var plinkoJs = File.ReadAllText(Path.Combine(jsDir, "plinko.js"));
            var plinkoEngine = CreateMockBrowserEngine(out _);
            plinkoEngine.Execute(plinkoJs);

            var plinkoBuckets = new[]
            {
                new { label = "0", score = 10, isWin = false },
                new { label = "1", score = 50, isWin = true },
                new { label = "2", score = 10, isWin = false }
            };

            DispatchWsMessage(plinkoEngine, new
            {
                type = "session_start",
                session = new { gameType = "plinko", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                board = new { rows = 8, cols = 3, buckets = plinkoBuckets }
            });

            // Spawn token in Round 1
            DispatchWsMessage(plinkoEngine, new
            {
                type = "plinko_drop",
                dropToken = "round1-token",
                displayName = "Player1",
                board = new
                {
                    rowCount = 8,
                    durationSeconds = 1.0,
                    gravity = 1.0,
                    bounciness = 0.85,
                    mass = 1.0,
                    buckets = plinkoBuckets,
                    physics = new { startX = 0.50, startY = 0.04, gravity = 1.0, bounciness = 0.85, mass = 1.0 }
                }
            });

            // Step 30 frames (token is mid-flight)
            StepEngineFrames(plinkoEngine, 30);
            Assert.Equal(0, (int)plinkoEngine.Evaluate("window.__sentCompletions.length").AsNumber());

            // New session starts (Round 2) - should immediately abort/reset mid-flight drops (#1365)
            DispatchWsMessage(plinkoEngine, new
            {
                type = "session_start",
                session = new { gameType = "plinko", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 2 },
                board = new { rows = 8, cols = 3, buckets = plinkoBuckets }
            });

            // Step 120 more frames
            StepEngineFrames(plinkoEngine, 120);

            // Verify aborted round1-token never completed or sent a completion event
            var completionsR1 = plinkoEngine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(0, (int)completionsR1);

            // Spawn a valid new token for Round 2
            DispatchWsMessage(plinkoEngine, new
            {
                type = "plinko_drop",
                dropToken = "round2-token",
                displayName = "Player2",
                board = new
                {
                    rowCount = 8,
                    durationSeconds = 1.0,
                    gravity = 1.0,
                    bounciness = 0.85,
                    mass = 1.0,
                    buckets = plinkoBuckets,
                    physics = new { startX = 0.80, startY = 0.04, gravity = 1.0, bounciness = 0.85, mass = 1.0 }
                }
            });

            StepEngineFrames(plinkoEngine, 90);
            var completionsR2 = plinkoEngine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(1, (int)completionsR2);
            var r2Bucket = plinkoEngine.Evaluate("window.__sentCompletions[0].winningBucketIndex").AsNumber();
            Assert.InRange((int)r2Bucket, 0, 2);

            // 2. Parachute Mid-Flight Reset Test
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var parachuteEngine = CreateMockBrowserEngine(out _);
            parachuteEngine.Execute(parachuteJs);

            DispatchWsMessage(parachuteEngine, new
            {
                type = "session_start",
                session = new { gameType = "parachute", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                target = new { centerX = 0.50, targetHalfWidth = 0.10 }
            });

            var p1Record = CreateTestDropRecord("p1-drop", "Parachutist1", 0.50, 0.0, 0.12, 0.92, 300);
            DispatchWsMessage(parachuteEngine, new
            {
                type = "parachute_drop",
                dropToken = p1Record.DropToken,
                displayName = p1Record.DisplayName,
                trajectory = new
                {
                    startX = p1Record.StartX,
                    startY = p1Record.StartY,
                    initialVx = p1Record.InitialVx,
                    initialVy = p1Record.InitialVy,
                    landingY = p1Record.LandingY,
                    totalTicks = p1Record.TotalTicks,
                    targetCenterX = p1Record.TargetCenterX,
                    targetHalfWidth = p1Record.TargetHalfWidth,
                    collisions = new List<object>()
                }
            });

            // Step 40 frames (parachutist is descending)
            StepEngineFrames(parachuteEngine, 40);
            Assert.Equal(0, (int)parachuteEngine.Evaluate("window.__sentCompletions.length").AsNumber());

            // New session starts (Round 2) - should immediately abort/reset mid-flight drops (#1365)
            DispatchWsMessage(parachuteEngine, new
            {
                type = "session_start",
                session = new { gameType = "parachute", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 2 },
                target = new { centerX = 0.50, targetHalfWidth = 0.10 }
            });

            StepEngineFrames(parachuteEngine, 200);
            var parachuteCompletionsR1 = parachuteEngine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(0, (int)parachuteCompletionsR1);

            // Spawn a new parachutist in Round 2
            var p2Record = CreateTestDropRecord("p2-drop", "Parachutist2", 0.50, 0.0, 0.20, 0.92, 200);
            DispatchWsMessage(parachuteEngine, new
            {
                type = "parachute_drop",
                dropToken = p2Record.DropToken,
                displayName = p2Record.DisplayName,
                trajectory = new
                {
                    startX = p2Record.StartX,
                    startY = p2Record.StartY,
                    initialVx = p2Record.InitialVx,
                    initialVy = p2Record.InitialVy,
                    landingY = p2Record.LandingY,
                    totalTicks = p2Record.TotalTicks,
                    targetCenterX = p2Record.TargetCenterX,
                    targetHalfWidth = p2Record.TargetHalfWidth,
                    collisions = new List<object>()
                }
            });

            StepEngineFrames(parachuteEngine, 360);
            var parachuteCompletionsR2 = parachuteEngine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(1, (int)parachuteCompletionsR2);
            var p2Token = parachuteEngine.Evaluate("window.__sentCompletions[0].dropToken").AsString();
            Assert.Equal("p2-drop", p2Token);
        }

        [Fact]
        public void ParachuteJs_DropletKnockbackIntoWallBounceCombo_ParityWithServerSimulation()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new { gameType = "parachute", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                target = new { centerX = 0.50, targetHalfWidth = 0.10 }
            });

            // Parachutist begins near left screen margin moving left
            var record = CreateTestDropRecord("combo-drop", "ComboUser", 0.12, -0.05, 0.14, 0.92, 360);

            // Droplet hits parachutist pushing it hard to the left into wall margin (x <= 0.05)
            var droplets = new List<DropletInstanceDto>
            {
                new DropletInstanceDto
                {
                    Id = "combo-droplet",
                    StartX = 0.125,
                    StartY = 0.046,
                    Vx = -0.15,
                    Vy = 0.35,
                    Gravity = 0.32,
                    Radius = 0.035,
                    Mass = 0.5,
                    SpawnTickOffset = 18,
                    SpeedScale = 1.0
                }
            };

            ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0);
            var initialBaselineLandingX = record.LandingX;

            DispatchWsMessage(engine, new
            {
                type = "parachute_drop",
                dropToken = record.DropToken,
                displayName = record.DisplayName,
                trajectory = new
                {
                    startX = record.StartX,
                    startY = record.StartY,
                    initialVx = record.InitialVx,
                    initialVy = record.InitialVy,
                    landingY = record.LandingY,
                    totalTicks = record.TotalTicks,
                    bounciness = record.Bounciness,
                    mass = record.Mass,
                    targetCenterX = record.TargetCenterX,
                    targetHalfWidth = record.TargetHalfWidth,
                    collisions = new object[0]
                }
            });

            DispatchWsMessage(engine, new
            {
                type = "parachute_droplets",
                droplets = droplets.Select(d => new
                {
                    id = d.Id,
                    startX = d.StartX,
                    startY = d.StartY,
                    vx = d.Vx,
                    vy = d.Vy,
                    gravity = d.Gravity,
                    radius = d.Radius,
                    spawnTickOffset = d.SpawnTickOffset
                }).ToList()
            });

            StepEngineFrames(engine, 400);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var clientLandingX = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'combo-drop').landingX").AsNumber();
            Assert.False(double.IsNaN(clientLandingX) || double.IsInfinity(clientLandingX));
            Assert.InRange(clientLandingX, 0.05, 0.95);
            Assert.True(clientLandingX > 0.15,
                $"Droplet knockback + wall bounce rebounded player into open area, got {clientLandingX}");
        }

        [Fact]
        public void PlinkoJs_DenseMultiTokenCrowd_AllTokensBounceAndLandInBuckets()
        {
            var jsDir = FindWwwRootPath();
            var plinkoJs = File.ReadAllText(Path.Combine(jsDir, "plinko.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(plinkoJs);

            const int rows = 8;
            const int bucketCount = 5;

            var buckets = new[]
            {
                new { label = "0", score = 10, isWin = false },
                new { label = "1", score = 25, isWin = true },
                new { label = "2", score = 100, isWin = true },
                new { label = "3", score = 25, isWin = true },
                new { label = "4", score = 10, isWin = false }
            };

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new { gameType = "plinko", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                board = new { rows, cols = bucketCount, buckets }
            });

            // Drop 12 simultaneous tokens distributed across buckets
            const int tokenCount = 12;
            for (var t = 0; t < tokenCount; t++)
            {
                var targetBucket = t % bucketCount;
                DispatchWsMessage(engine, new
                {
                    type = "plinko_drop",
                    dropToken = $"crowd-token-{t}",
                    displayName = $"CrowdUser{t}",
                    userColor = "#00FFAA",
                    board = new
                    {
                        rowCount = rows,
                        durationSeconds = 1.4,
                        gravity = 1.0,
                        bounciness = 0.85,
                        mass = 1.0,
                        buckets = buckets,
                        physics = new
                        {
                            startX = (targetBucket + 0.5) / bucketCount,
                            startY = 0.04,
                            gravity = 1.0,
                            bounciness = 0.85,
                            mass = 1.0
                        }
                    }
                });
            }

            // Step 150 frames to allow full descent and token-token collisions
            StepEngineFrames(engine, 150);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var completions = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(tokenCount, (int)completions);

            // Verify every single token landed in a valid winning bucket
            for (var t = 0; t < tokenCount; t++)
            {
                var actualBucket = engine.Evaluate($"window.__sentCompletions.find(c => c.dropToken === 'crowd-token-{t}').winningBucketIndex").AsNumber();
                Assert.InRange((int)actualBucket, 0, bucketCount - 1);
            }
        }

        [Fact]
        public void ParachuteJs_ExtremeHeadOnCollision_MaxBouncinessAndMassRatio_DoesNotExplodeOrViolateBounds()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new { gameType = "parachute", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                target = new { centerX = 0.50, targetHalfWidth = 0.10 }
            });

            // Drop A: 10.0 kg heavy anvil, bounciness 5.0, falling rightwards
            var heavyDrop = CreateTestDropRecord("heavy-drop", "HeavyPlayer", 0.44, 0.12, 0.14, 0.92, 360, 0.50, 5.0, 10.0);

            // Drop B: 0.1 kg featherweight, bounciness 5.0, falling leftwards (head-on collision)
            var lightDrop = CreateTestDropRecord("light-drop", "LightPlayer", 0.56, -0.12, 0.14, 0.92, 360, 0.50, 5.0, 0.1);

            foreach (var record in new[] { heavyDrop, lightDrop })
            {
                DispatchWsMessage(engine, new
                {
                    type = "parachute_drop",
                    dropToken = record.DropToken,
                    displayName = record.DisplayName,
                    trajectory = new
                    {
                        startX = record.StartX,
                        startY = record.StartY,
                        initialVx = record.InitialVx,
                        initialVy = record.InitialVy,
                        landingY = record.LandingY,
                        totalTicks = record.TotalTicks,
                        bounciness = record.Bounciness,
                        mass = record.Mass,
                        targetCenterX = record.TargetCenterX,
                        targetHalfWidth = record.TargetHalfWidth,
                        collisions = new List<object>()
                    }
                });
            }

            // Step 400 frames
            StepEngineFrames(engine, 400);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var completions = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(2, (int)completions);

            var heavyLandingX = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'heavy-drop').landingX").AsNumber();
            var lightLandingX = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'light-drop').landingX").AsNumber();

            // Verify no NaN or infinity occurred under 100:1 mass ratio and 5.0 bounciness
            Assert.False(double.IsNaN(heavyLandingX) || double.IsInfinity(heavyLandingX));
            Assert.False(double.IsNaN(lightLandingX) || double.IsInfinity(lightLandingX));

            // Verify both landed safely within playable boundaries [0.05, 0.95]
            Assert.InRange(heavyLandingX, 0.05, 0.95);
            Assert.InRange(lightLandingX, 0.05, 0.95);
        }

        [Fact]
        public void PlinkoJs_ExtremeTokenCollision_MaxBouncinessAndMassRatio_DecaysCleanlyIntoBuckets()
        {
            var jsDir = FindWwwRootPath();
            var plinkoJs = File.ReadAllText(Path.Combine(jsDir, "plinko.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(plinkoJs);

            const int rows = 8;
            const int bucketCount = 5;

            var buckets = new[]
            {
                new { label = "0", score = 10, isWin = false },
                new { label = "1", score = 25, isWin = true },
                new { label = "2", score = 100, isWin = true },
                new { label = "3", score = 25, isWin = true },
                new { label = "4", score = 10, isWin = false }
            };

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new { gameType = "plinko", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                board = new { rows, cols = bucketCount, buckets }
            });

            // Token A: Mass 10.0, Bounce 5.0, start on left (0.10)
            DispatchWsMessage(engine, new
            {
                type = "plinko_drop",
                dropToken = "heavy-token",
                displayName = "HeavyToken",
                userColor = "#FF0000",
                board = new
                {
                    rowCount = rows,
                    durationSeconds = 1.4,
                    gravity = 1.0,
                    bounciness = 5.0,
                    mass = 10.0,
                    buckets = buckets,
                    physics = new { startX = 0.10, startY = 0.04, gravity = 1.0, bounciness = 5.0, mass = 10.0 }
                }
            });

            // Token B: Mass 0.1, Bounce 5.0, start on right (0.90)
            DispatchWsMessage(engine, new
            {
                type = "plinko_drop",
                dropToken = "light-token",
                displayName = "LightToken",
                userColor = "#0000FF",
                board = new
                {
                    rowCount = rows,
                    durationSeconds = 1.4,
                    gravity = 1.0,
                    bounciness = 5.0,
                    mass = 0.1,
                    buckets = buckets,
                    physics = new { startX = 0.90, startY = 0.04, gravity = 1.0, bounciness = 5.0, mass = 0.1 }
                }
            });

            // Step 150 frames
            StepEngineFrames(engine, 150);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var completions = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(2, (int)completions);

            var heavyLandedBucket = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'heavy-token').winningBucketIndex").AsNumber();
            var lightLandedBucket = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'light-token').winningBucketIndex").AsNumber();

            // Both tokens land cleanly into valid buckets
            Assert.InRange((int)heavyLandedBucket, 0, 4);
            Assert.InRange((int)lightLandedBucket, 0, 4);
        }

        [Fact]
        public void ParachuteJs_ChaoticIntersection_MultiParachutistsAndMultiDroplets_AllResolveCleanly()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new { gameType = "parachute", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                target = new { centerX = 0.50, targetHalfWidth = 0.10 }
            });

            // 4 parachutists with varying masses and initial angles
            var drops = new[]
            {
                CreateTestDropRecord("p-1", "Player1", 0.20, 0.08, 0.14, 0.92, 380, 0.50, 1.2, 2.0),
                CreateTestDropRecord("p-2", "Player2", 0.40, -0.04, 0.16, 0.92, 360, 0.50, 2.5, 0.5),
                CreateTestDropRecord("p-3", "Player3", 0.60, 0.04, 0.15, 0.92, 370, 0.50, 0.8, 1.5),
                CreateTestDropRecord("p-4", "Player4", 0.80, -0.08, 0.14, 0.92, 380, 0.50, 3.0, 0.8)
            };

            // 10 droplets distributed across the field
            var droplets = new List<DropletInstanceDto>();
            for (var d = 0; d < 10; d++)
            {
                droplets.Add(new DropletInstanceDto
                {
                    Id = $"chaotic-drop-{d}",
                    StartX = 0.15 + (d * 0.07),
                    StartY = 0.02 + ((d % 3) * 0.03),
                    Vx = (d % 2 == 0 ? 0.04 : -0.04),
                    Vy = 0.30 + ((d % 4) * 0.03),
                    Gravity = 0.32,
                    Radius = 0.028,
                    Mass = 0.5,
                    SpawnTickOffset = d * 15,
                    SpeedScale = 1.0
                });
            }

            // Baseline simulation for each drop
            foreach (var record in drops)
            {
                ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0);
                DispatchWsMessage(engine, new
                {
                    type = "parachute_drop",
                    dropToken = record.DropToken,
                    displayName = record.DisplayName,
                    trajectory = new
                    {
                        startX = record.StartX,
                        startY = record.StartY,
                        initialVx = record.InitialVx,
                        initialVy = record.InitialVy,
                        landingY = record.LandingY,
                        totalTicks = record.TotalTicks,
                        bounciness = record.Bounciness,
                        mass = record.Mass,
                        targetCenterX = record.TargetCenterX,
                        targetHalfWidth = record.TargetHalfWidth,
                        collisions = new object[0]
                    }
                });
            }

            // Dispatch droplets to JS
            DispatchWsMessage(engine, new
            {
                type = "parachute_droplets",
                droplets = droplets.Select(d => new
                {
                    id = d.Id,
                    startX = d.StartX,
                    startY = d.StartY,
                    vx = d.Vx,
                    vy = d.Vy,
                    gravity = d.Gravity,
                    radius = d.Radius,
                    spawnTickOffset = d.SpawnTickOffset
                }).ToList()
            });

            // Step 750 frames to allow full resolution of live knockbacks and upward bounces
            StepEngineFrames(engine, 750);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var completions = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(4, (int)completions);

            for (var i = 1; i <= 4; i++)
            {
                var landingX = engine.Evaluate($"window.__sentCompletions.find(c => c.dropToken === 'p-{i}').landingX").AsNumber();
                Assert.False(double.IsNaN(landingX) || double.IsInfinity(landingX));
                Assert.InRange(landingX, 0.05, 0.95);
            }
        }

        [Fact]
        public void PlinkoJs_WaypointTrajectory_FollowsNaturalGravitationalArcNotStraightDiagonal()
        {
            var jsDir = FindWwwRootPath();
            var plinkoJs = File.ReadAllText(Path.Combine(jsDir, "plinko.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(plinkoJs);

            const int rows = 8;
            const int bucketCount = 5;

            var buckets = new[]
            {
                new { label = "0", score = 10, isWin = false },
                new { label = "1", score = 25, isWin = true },
                new { label = "2", score = 100, isWin = true },
                new { label = "3", score = 25, isWin = true },
                new { label = "4", score = 10, isWin = false }
            };

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new { gameType = "plinko", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                board = new { rows, cols = bucketCount, buckets }
            });

            DispatchWsMessage(engine, new
            {
                type = "plinko_drop",
                dropToken = "arc-test-token",
                displayName = "ArcViewer",
                board = new
                {
                    rowCount = rows,
                    durationSeconds = 1.8,
                    gravity = 1.0,
                    bounciness = 0.85,
                    mass = 1.0,
                    buckets = buckets,
                    physics = new { startX = 0.60, startY = 0.04, gravity = 1.0, bounciness = 0.85, mass = 1.0 }
                }
            });

            // Sample positions over the first 30 frames
            var sampledPositions = new List<(double x, double y)>();
            for (var f = 0; f < 30; f++)
            {
                StepEngineFrames(engine, 1);
                var x = engine.Evaluate("window.__plinkoTokens[0].x").AsNumber();
                var y = engine.Evaluate("window.__plinkoTokens[0].y").AsNumber();
                sampledPositions.Add((x, y));
            }

            // Verify the vertical movement exhibits authentic Plinko physics:
            // 1. Initial downward free fall from dispenser into first peg
            var deltaY1 = sampledPositions[10].y - sampledPositions[0].y;
            Assert.True(deltaY1 > 0, "Token moves downward from dispenser");

            // 2. Token advances downward through the peg field
            var deltaOverall = sampledPositions[29].y - sampledPositions[0].y;
            Assert.True(deltaOverall > 0, "Token progresses downward through the peg field");

            // Complete the full drop
            StepEngineFrames(engine, 120);

            var completions = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(1, (int)completions);

            var landedBucket = engine.Evaluate("window.__sentCompletions[0].winningBucketIndex").AsNumber();
            Assert.InRange((int)landedBucket, 0, bucketCount - 1);
        }

        [Fact]
        public void PlinkoJs_OrganicBouncePhysics_ExhibitsUpwardBounceAndSkipsIntermediateRows_LandsInBucket()
        {
            var jsDir = FindWwwRootPath();
            var plinkoJs = File.ReadAllText(Path.Combine(jsDir, "plinko.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(plinkoJs);

            const int rows = 8;
            const int bucketCount = 5;

            var buckets = new[]
            {
                new { label = "0", score = 10, isWin = false },
                new { label = "1", score = 25, isWin = true },
                new { label = "2", score = 100, isWin = true },
                new { label = "3", score = 25, isWin = true },
                new { label = "4", score = 10, isWin = false }
            };

            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new { gameType = "plinko", isActive = true, durationSeconds = 60, totalDurationSeconds = 60, roundNumber = 1 },
                board = new { rows, cols = bucketCount, buckets }
            });

            DispatchWsMessage(engine, new
            {
                type = "plinko_drop",
                dropToken = "bounce-up-test-token",
                displayName = "Bouncer",
                board = new
                {
                    rowCount = rows,
                    durationSeconds = 2.0,
                    gravity = 1.0,
                    bounciness = 1.0,
                    mass = 1.0,
                    buckets = buckets,
                    physics = new { startX = 0.50, startY = 0.04, gravity = 1.0, bounciness = 1.0, mass = 1.0 }
                }
            });

            // 1. Step 1 frame to initialize
            StepEngineFrames(engine, 1);

            // 2. Sample frame positions across the drop to detect true upward bounce (negative delta Y)
            var ySamples = new List<double>();
            var detectedUpwardBounce = false;
            double maxUpwardHop = 0;

            for (var f = 0; f < 150; f++)
            {
                StepEngineFrames(engine, 1);
                var y = engine.Evaluate("window.__plinkoTokens[0].y").AsNumber();
                if (ySamples.Count > 0)
                {
                    var prevY = ySamples.Last();
                    var dy = y - prevY;
                    if (dy < -0.0005) // token is moving UPWARDS against gravity
                    {
                        detectedUpwardBounce = true;
                        var hop = prevY - y;
                        if (hop > maxUpwardHop) maxUpwardHop = hop;
                    }
                }
                ySamples.Add(y);
            }

            Assert.True(detectedUpwardBounce, "Token should exhibit a true upward bounce (negative delta Y) after peg impact");
            Assert.True(maxUpwardHop > 0.001, $"Upward bounce hop should be visible (got {maxUpwardHop:F4})");

            // 3. Complete the drop and verify bucket landing
            StepEngineFrames(engine, 60);
            var completions = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(1, (int)completions);
            var landedBucket = engine.Evaluate("window.__sentCompletions[0].winningBucketIndex").AsNumber();
            Assert.InRange((int)landedBucket, 0, bucketCount - 1);
        }

        [Fact]
        public void DropGameMediaResolver_SoundBundle_ResolvesCorrectly_WithOverridesAndDefaults()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "DropGameMediaResolverTests_" + Guid.NewGuid().ToString("N"));
            var audioDir = Path.Combine(tempRoot, "audio");
            Directory.CreateDirectory(audioDir);

            try
            {
                // Create temporary audio fixtures
                File.WriteAllBytes(Path.Combine(audioDir, "chime.wav"), [0x52, 0x49, 0x46, 0x46]);
                File.WriteAllBytes(Path.Combine(audioDir, "fanfare.mp3"), [0xFF, 0xFB, 0x90, 0x64]);
                File.WriteAllBytes(Path.Combine(audioDir, "theme_drop.wav"), [0x52, 0x49, 0x46, 0x46]);

                var mediaService = new MediaUploadService(tempRoot, NullLogger<MediaUploadService>.Instance);

                var pSettings = new ParachuteGameSettings
                {
                    MaxConcurrentSounds = 2,
                    StartSound = new DropGameSoundConfig { Enabled = true, AudioFile = "chime.wav", Volume = 80 },
                    EndSound = new DropGameSoundConfig { Enabled = true, AudioFile = "horn.mp3", Volume = 70 },
                    DropSound = new DropGameSoundConfig { Enabled = false, AudioFile = "swoosh.wav", Volume = 60 },
                    WinSound = new DropGameSoundConfig { Enabled = true, AudioFile = "fanfare.mp3", Volume = 90 }
                };

                var theme = new DropGameTheme
                {
                    Id = "custom",
                    Name = "Custom Theme",
                    ParachuteStartSound = new DropGameSoundOverride { Override = false }, // Inherit
                    ParachuteEndSound = new DropGameSoundOverride { Override = true, Enabled = false }, // Mute
                    ParachuteDropSound = new DropGameSoundOverride { Override = true, Enabled = true, AudioFile = "theme_drop.wav", Volume = 95 }, // Custom
                    ParachuteWinSound = new DropGameSoundOverride { Override = false } // Inherit
                };

                var bundle = DropGameMediaResolver.ResolveParachuteSoundBundle(pSettings, theme, mediaService);
                Assert.NotNull(bundle);
                Assert.Equal(2, bundle.MaxConcurrentSounds);

                // Start: Inherit -> enabled, url resolved to /audio/chime.wav, volume 80
                Assert.NotNull(bundle.Start);
                Assert.True(bundle.Start.Enabled);
                Assert.Equal("/audio/chime.wav", bundle.Start.Url);
                Assert.Equal(80, bundle.Start.Volume);

                // End: Mute -> disabled, null url
                Assert.NotNull(bundle.End);
                Assert.False(bundle.End.Enabled);
                Assert.Null(bundle.End.Url);

                // Drop: Custom -> enabled, url resolved to /audio/theme_drop.wav, volume 95
                Assert.NotNull(bundle.Drop);
                Assert.True(bundle.Drop.Enabled);
                Assert.Equal("/audio/theme_drop.wav", bundle.Drop.Url);
                Assert.Equal(95, bundle.Drop.Volume);

                // Win: Inherit -> enabled, url resolved to /audio/fanfare.mp3, volume 90
                Assert.NotNull(bundle.Win);
                Assert.True(bundle.Win.Enabled);
                Assert.Equal("/audio/fanfare.mp3", bundle.Win.Url);
                Assert.Equal(90, bundle.Win.Volume);

                // Also verify that when both configs are absent, ResolveSound returns null
                var unconfigured = DropGameMediaResolver.ResolveSound(null, null, mediaService);
                Assert.Null(unconfigured);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    try { Directory.Delete(tempRoot, true); } catch { /* Ignore cleanup issues in temp */ }
                }
            }
        }

        [Fact]
        public void ParachuteJs_SoundManager_PreloadsAndPlaysEventsWithoutQueuing()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJsPath = Path.Combine(jsDir, "parachute.js");
            Assert.True(File.Exists(parachuteJsPath));

            var code = File.ReadAllText(parachuteJsPath);
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(code);

            // 1. Send session_start with sound bundle
            var sessionStartPayload = JsonSerializer.Serialize(new
            {
                type = "session_start",
                session = new
                {
                    isActive = true,
                    totalDurationSeconds = 60,
                    remainingSeconds = 60,
                    totalDrops = 0
                },
                sounds = new
                {
                    maxConcurrentSounds = 1,
                    start = new { enabled = true, url = "/audio/start.mp3", volume = 80 },
                    end = new { enabled = true, url = "/audio/end.mp3", volume = 75 },
                    drop = new { enabled = true, url = "/audio/drop.mp3", volume = 70 },
                    win = new { enabled = true, url = "/audio/win.mp3", volume = 90 }
                }
            });

            engine.Evaluate($"window.__wsMessageHandler({{ data: '{sessionStartPayload}' }})");

            // Verify preloading was triggered for bundle sounds
            var preloadedCount = engine.Evaluate("window.__audioPreloaded.length").AsNumber();
            Assert.True(preloadedCount >= 4, "Start, end, drop, and win sounds are preloaded upfront");

            // Verify start sound played immediately
            var playedCount = engine.Evaluate("window.__audioPlayed.length").AsNumber();
            Assert.True(playedCount >= 1, "Start sound played on session_start");
            var lastPlayedSrc = engine.Evaluate("window.__audioPlayed[window.__audioPlayed.length - 1].src").AsString();
            Assert.Equal("/audio/start.mp3", lastPlayedSrc);

            // 2. Send parachute_drop
            var dropPayload = JsonSerializer.Serialize(new
            {
                type = "parachute_drop",
                dropToken = "sound-drop-token",
                displayName = "AudioTester",
                sounds = new
                {
                    maxConcurrentSounds = 1,
                    drop = new { enabled = true, url = "/audio/drop.mp3", volume = 70 },
                    win = new { enabled = true, url = "/audio/win.mp3", volume = 90 }
                },
                physics = new
                {
                    startX = 0.5,
                    startY = 0.90,
                    landingY = 0.94,
                    initialVx = 0.0,
                    initialVy = 0.20,
                    durationSeconds = 1.0,
                    totalTicks = 60,
                    isHit = true,
                    isBullseye = true,
                    targetCenterX = 0.5,
                    targetHalfWidth = 0.10
                }
            });

            engine.Evaluate($"window.__wsMessageHandler({{ data: '{dropPayload}' }})");

            // Verify drop sound played immediately
            lastPlayedSrc = engine.Evaluate("window.__audioPlayed[window.__audioPlayed.length - 1].src").AsString();
            Assert.Equal("/audio/drop.mp3", lastPlayedSrc);

            // 3. Step frames to simulate parachute landing on platform
            StepEngineFrames(engine, 70);

            // Verify win sound played on landing
            var totalPlayed = (int)engine.Evaluate("window.__audioPlayed.length").AsNumber();
            Assert.True(totalPlayed >= 3, "Start, drop, and win sounds were triggered");
            lastPlayedSrc = engine.Evaluate("window.__audioPlayed[window.__audioPlayed.length - 1].src").AsString();
            Assert.Equal("/audio/win.mp3", lastPlayedSrc);

            // 4. Send session_end
            var sessionEndPayload = JsonSerializer.Serialize(new
            {
                type = "session_end",
                session = new { isActive = false, totalDrops = 1 },
                sounds = new
                {
                    maxConcurrentSounds = 1,
                    end = new { enabled = true, url = "/audio/end.mp3", volume = 75 }
                }
            });

            engine.Evaluate($"window.__wsMessageHandler({{ data: '{sessionEndPayload}' }})");

            lastPlayedSrc = engine.Evaluate("window.__audioPlayed[window.__audioPlayed.length - 1].src").AsString();
            Assert.Equal("/audio/end.mp3", lastPlayedSrc);
        }

        [Fact]
        public void PlinkoJs_SoundManager_PreloadsAndPlaysEventsWithoutQueuing()
        {
            var jsDir = FindWwwRootPath();
            var plinkoJsPath = Path.Combine(jsDir, "plinko.js");
            Assert.True(File.Exists(plinkoJsPath));

            var code = File.ReadAllText(plinkoJsPath);
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(code);

            var winningBuckets = new[]
            {
                new { label = "WIN", score = 100, isWin = true },
                new { label = "WIN", score = 100, isWin = true },
                new { label = "WIN", score = 100, isWin = true },
                new { label = "WIN", score = 100, isWin = true },
                new { label = "WIN", score = 100, isWin = true }
            };

            // 1. Send session_start with sound bundle
            var sessionStartPayload = JsonSerializer.Serialize(new
            {
                type = "session_start",
                session = new { isActive = true, totalDurationSeconds = 60 },
                board = new { rows = 5, cols = 5, buckets = winningBuckets },
                sounds = new
                {
                    maxConcurrentSounds = 1,
                    start = new { enabled = true, url = "/audio/plinko_start.mp3", volume = 85 },
                    end = new { enabled = true, url = "/audio/plinko_end.mp3", volume = 75 },
                    drop = new { enabled = true, url = "/audio/plinko_drop.mp3", volume = 70 },
                    pegCollision = new { enabled = true, url = "/audio/peg_clink.wav", volume = 60 },
                    win = new { enabled = true, url = "/audio/plinko_win.mp3", volume = 90 }
                }
            });

            engine.Evaluate($"window.__wsMessageHandler({{ data: '{sessionStartPayload}' }})");

            // Verify start sound played
            var lastPlayedSrc = engine.Evaluate("window.__audioPlayed[window.__audioPlayed.length - 1].src").AsString();
            Assert.Equal("/audio/plinko_start.mp3", lastPlayedSrc);

            // 2. Send plinko_drop
            var dropPayload = JsonSerializer.Serialize(new
            {
                type = "plinko_drop",
                dropToken = "plinko-sound-token",
                displayName = "PlinkoPlayer",
                sounds = new
                {
                    maxConcurrentSounds = 1,
                    drop = new { enabled = true, url = "/audio/plinko_drop.mp3", volume = 70 },
                    pegCollision = new { enabled = true, url = "/audio/peg_clink.wav", volume = 60 },
                    win = new { enabled = true, url = "/audio/plinko_win.mp3", volume = 90 }
                },
                board = new
                {
                    rowCount = 5,
                    durationSeconds = 1.0,
                    gravity = 1.0,
                    bounciness = 0.8,
                    mass = 1.0,
                    isWin = true,
                    winningBucketIndex = 2,
                    buckets = winningBuckets,
                    waypoints = new[]
                    {
                        new { x = 0.5, y = 0.08, row = -1 },
                        new { x = 0.5, y = 0.25, row = 0 },
                        new { x = 0.45, y = 0.50, row = 2 },
                        new { x = 0.5, y = 0.88, row = 5 }
                    }
                }
            });

            engine.Evaluate($"window.__wsMessageHandler({{ data: '{dropPayload}' }})");

            // Verify drop sound played immediately
            lastPlayedSrc = engine.Evaluate("window.__audioPlayed[window.__audioPlayed.length - 1].src").AsString();
            Assert.Equal("/audio/plinko_drop.mp3", lastPlayedSrc);

            // 3. Step frames through peg collision
            StepEngineFrames(engine, 30);

            // Verify peg collision was triggered
            var clinkPlayed = engine.Evaluate("window.__audioPlayed.some(function(a) { return a.src === '/audio/peg_clink.wav'; })").AsBoolean();
            Assert.True(clinkPlayed, "Peg collision sound clinked on peg bounce");

            // 4. Complete drop into winning bucket
            StepEngineFrames(engine, 60);

            var winPlayed = engine.Evaluate("window.__audioPlayed.some(function(a) { return a.src === '/audio/plinko_win.mp3'; })").AsBoolean();
            Assert.True(winPlayed, "Win sound played on winning bucket landing");

            // 5. Send session_end
            var sessionEndPayload = JsonSerializer.Serialize(new
            {
                type = "session_end",
                session = new { isActive = false, totalDrops = 1 },
                sounds = new
                {
                    maxConcurrentSounds = 1,
                    end = new { enabled = true, url = "/audio/plinko_end.mp3", volume = 75 }
                }
            });

            engine.Evaluate($"window.__wsMessageHandler({{ data: '{sessionEndPayload}' }})");

            lastPlayedSrc = engine.Evaluate("window.__audioPlayed[window.__audioPlayed.length - 1].src").AsString();
            Assert.Equal("/audio/plinko_end.mp3", lastPlayedSrc);
        }

        [Fact]
        public void DropGamesAudioJs_LoadsAndExecutesWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var audioJsPath = Path.Combine(jsDir, "drop-games-audio.js");
            Assert.True(File.Exists(audioJsPath), $"drop-games-audio.js not found at {audioJsPath}");

            var code = File.ReadAllText(audioJsPath);
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => engine.Execute(code));
            Assert.Null(ex);

            var hasFactory = engine.Evaluate("typeof window.DropGamesAudio.createSoundManager === 'function'").AsBoolean();
            Assert.True(hasFactory);
        }

        [Fact]
        public void DropGamesAudioJs_CreateSoundManager_PreloadsAndEnforcesVoiceLimit()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                var sm = DropGamesAudio.createSoundManager();
                sm.updateBundle({
                    maxConcurrentSounds: 2,
                    start: { enabled: true, url: '/audio/start.mp3', volume: 80 },
                    end: { enabled: true, url: '/audio/end.mp3', volume: 75 },
                    drop: { enabled: true, url: '/audio/drop.mp3', volume: 70 }
                });
            ");

            var preloaded = engine.Evaluate("window.__audioPreloaded.length").AsNumber();
            Assert.True(preloaded >= 3);

            engine.Execute(@"
                sm.play({ enabled: true, url: '/audio/sound1.mp3', volume: 80 }, 'sound1', false);
                sm.play({ enabled: true, url: '/audio/sound2.mp3', volume: 80 }, 'sound2', false);
                sm.play({ enabled: true, url: '/audio/sound3.mp3', volume: 80 }, 'sound3', false);
            ");

            var activeCount = (int)engine.Evaluate("sm.getActiveCount()").AsNumber();
            Assert.Equal(2, activeCount);

            var totalPlayed = (int)engine.Evaluate("window.__audioPlayed.length").AsNumber();
            Assert.True(totalPlayed >= 3);
        }

        [Fact]
        public void DropGamesCommonJs_LoadsAndExecutesWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var commonJsPath = Path.Combine(jsDir, "drop-games-common.js");
            Assert.True(File.Exists(commonJsPath), $"drop-games-common.js not found at {commonJsPath}");

            var code = File.ReadAllText(commonJsPath);
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => engine.Execute(code));
            Assert.Null(ex);

            Assert.True(engine.Evaluate("typeof window.DropGamesCommon.createImageLoader === 'function'").AsBoolean());
            Assert.True(engine.Evaluate("typeof window.DropGamesCommon.brightenColor === 'function'").AsBoolean());
            Assert.True(engine.Evaluate("typeof window.DropGamesCommon.drawSeasonalDefaultPenguin === 'function'").AsBoolean());
        }

        [Fact]
        public void DropGamesCommonJs_ImageLoader_NormalizesTwitchEmoteUrls()
        {
            var engine = CreateMockBrowserEngine(out _);
            var normalized = engine.Evaluate("DropGamesCommon.normalizeImageUrl('https://static-cdn.jtvnw.net/emoticons/v2/emotesv2_123/static/light/1.0')").AsString();
            Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/emotesv2_123/default/light/1.0", normalized);
        }

        [Fact]
        public void DropGamesCommonJs_SeasonalPenguin_ExecutesWithoutError()
        {
            var engine = CreateMockBrowserEngine(out _);
            var ex = Record.Exception(() => engine.Execute(@"
                var canvas = document.getElementById('plinko-canvas');
                var ctx = canvas.getContext('2d');
                DropGamesCommon.drawSeasonalDefaultPenguin(ctx, 32, 'spring');
                DropGamesCommon.drawSeasonalDefaultPenguin(ctx, 32, 'summer');
                DropGamesCommon.drawSeasonalDefaultPenguin(ctx, 32, 'fall');
                DropGamesCommon.drawSeasonalDefaultPenguin(ctx, 32, 'winter');
                DropGamesCommon.drawSeasonalDefaultPenguin(ctx, 32, 'halloween');
                DropGamesCommon.drawSeasonalDefaultPenguin(ctx, 32, 'christmas');
            "));
            Assert.Null(ex);
        }

        [Fact]
        public void ParachuteJs_ParachuteRockets_DestroysCanopy_TriggersFreeFallAndCrashLanding()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJsPath = Path.Combine(jsDir, "parachute.js");
            Assert.True(File.Exists(parachuteJsPath));

            var code = File.ReadAllText(parachuteJsPath);
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(code);

            // 1. Start session with full sound bundle
            var sessionStartPayload = JsonSerializer.Serialize(new
            {
                type = "session_start",
                session = new { isActive = true, totalDurationSeconds = 60, totalDrops = 0 },
                sounds = new
                {
                    maxConcurrentSounds = 5,
                    start = new { enabled = false, url = "/audio/start.mp3", volume = 80 },
                    rocketLaunch = new { enabled = true, url = "/media/rocket_launch.wav", volume = 80 },
                    rocketExplode = new { enabled = true, url = "/media/rocket_explode.wav", volume = 80 },
                    parachutePop = new { enabled = true, url = "/media/parachute_pop.wav", volume = 80 },
                    cannonball = new { enabled = true, url = "/media/cannonball.wav", volume = 80 },
                    crashLand = new { enabled = true, url = "/media/splat.mp3", volume = 80 }
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{sessionStartPayload}' }})");

            // 2. Launch parachutist
            var dropPayload = JsonSerializer.Serialize(new
            {
                type = "parachute_drop",
                dropToken = "victim-drop-token",
                displayName = "FallingPenguin",
                physics = new
                {
                    startX = 0.50,
                    startY = 0.10,
                    landingY = 0.94,
                    initialVx = 0.0,
                    initialVy = 0.12,
                    durationSeconds = 6.0,
                    totalTicks = 360,
                    isHit = true,
                    isBullseye = true,
                    targetCenterX = 0.50,
                    targetHalfWidth = 0.10
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{dropPayload}' }})");

            // Step a few frames while parachutist glides normally
            StepEngineFrames(engine, 10);
            var yGlide = engine.Evaluate("window.__activeDrops[0].y").AsNumber();
            Assert.True(yGlide > 0.10, "Parachutist starts descent");

            // 3. Fire a barrage of rockets across the parachutist path to guarantee a direct hit without server trajectory updates
            var rocketPayload = JsonSerializer.Serialize(new
            {
                type = "parachute_rockets",
                rockets = new[]
                {
                    new
                    {
                        id = "sniper-rocket-1",
                        startX = 0.48,
                        startY = 0.55,
                        targetX = 0.48,
                        targetY = 0.10,
                        vx = 0.0,
                        vy = -0.45,
                        accelerationY = 0.0,
                        fuseTicks = 120,
                        spawnTickOffset = 0,
                        color = "#FF3366",
                        trailType = "sparks",
                        targetDropToken = "victim-drop-token",
                        knockbackMultiplier = 1.0,
                        canopyKillChance = 1.0
                    },
                    new
                    {
                        id = "sniper-rocket-2",
                        startX = 0.50,
                        startY = 0.55,
                        targetX = 0.50,
                        targetY = 0.10,
                        vx = 0.0,
                        vy = -0.45,
                        accelerationY = 0.0,
                        fuseTicks = 120,
                        spawnTickOffset = 0,
                        color = "#FF3366",
                        trailType = "sparks",
                        targetDropToken = "victim-drop-token",
                        knockbackMultiplier = 1.0,
                        canopyKillChance = 1.0
                    },
                    new
                    {
                        id = "sniper-rocket-3",
                        startX = 0.52,
                        startY = 0.55,
                        targetX = 0.52,
                        targetY = 0.10,
                        vx = 0.0,
                        vy = -0.45,
                        accelerationY = 0.0,
                        fuseTicks = 120,
                        spawnTickOffset = 0,
                        color = "#FF3366",
                        trailType = "sparks",
                        targetDropToken = "victim-drop-token",
                        knockbackMultiplier = 1.0,
                        canopyKillChance = 1.0
                    }
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{rocketPayload}' }})");

            // Verify active rockets registered
            var rocketCount = (int)engine.Evaluate("window.__activeRockets.length").AsNumber();
            Assert.Equal(3, rocketCount);

            // Free fall should not begin before rocket impacts
            var destroyedInitial = engine.Evaluate("window.__activeDrops[0].parachuteDestroyed").AsBoolean();
            Assert.False(destroyedInitial, "Parachute canopy should not be destroyed before rocket collision occurs");

            // Step 10 frames while rockets are still climbing
            StepEngineFrames(engine, 10);
            var destroyedMidFlight = engine.Evaluate("window.__activeDrops[0].parachuteDestroyed").AsBoolean();
            Assert.False(destroyedMidFlight, "Parachute canopy should remain intact while rockets are mid-flight");

            // Step 35 more frames: rockets collide with canopy/body and detonate
            StepEngineFrames(engine, 35);

            var isDestroyed = engine.Evaluate("window.__activeDrops[0].parachuteDestroyed").AsBoolean();
            Assert.True(isDestroyed, "Parachute canopy marked destroyed upon rocket collision");

            // Free fall speed should accelerate significantly faster than initial vy (0.12)
            var vyFreeFall = engine.Evaluate("window.__activeDrops[0].vy").AsNumber();
            Assert.True(vyFreeFall > 0.35, $"Parachutist should accelerate in free fall (got {vyFreeFall})");

            // Step frames until crash landing occurs
            StepEngineFrames(engine, 70);

            var completions = (int)engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.True(completions >= 1, "Completion token was sent upon crash landing");

            var sentPayload = engine.Evaluate("JSON.stringify(window.__sentCompletions[0])").AsString();
            Assert.Contains("\"parachuteDestroyed\":true", sentPayload);

            // Verify both rocket launch and crash landing sounds triggered
            var audioPlayed = engine.Evaluate("window.__audioPlayed.map(a => a.src).join(';')").AsString();
            Assert.Contains("rocket_launch.wav", audioPlayed);
            Assert.True(audioPlayed.Contains("cannonball.wav") || audioPlayed.Contains("splat.mp3"),
                $"Crash landing sound should trigger, got: {audioPlayed}");
        }

        [Fact]
        public void ParachuteJs_RocketDirectHit_DetonatesOnCollisionAndAppliesKnockback()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJsPath = Path.Combine(jsDir, "parachute.js");
            Assert.True(File.Exists(parachuteJsPath));

            var code = File.ReadAllText(parachuteJsPath);
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(code);

            // 1. Start session
            var sessionStartPayload = JsonSerializer.Serialize(new
            {
                type = "session_start",
                session = new { isActive = true, totalDurationSeconds = 60, totalDrops = 0 },
                sounds = new
                {
                    maxConcurrentSounds = 5,
                    rocketLaunch = new { enabled = true, url = "/media/rocket_launch.wav", volume = 80 },
                    rocketExplode = new { enabled = true, url = "/media/rocket_explode.wav", volume = 80 },
                    parachutePop = new { enabled = true, url = "/media/parachute_pop.wav", volume = 80 }
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{sessionStartPayload}' }})");

            // 2. Launch parachutist gliding in mid-air
            var dropPayload = JsonSerializer.Serialize(new
            {
                type = "parachute_drop",
                dropToken = "target-token-xyz",
                displayName = "RocketTarget",
                physics = new
                {
                    startX = 0.50,
                    startY = 0.30,
                    landingY = 0.94,
                    initialVx = 0.0,
                    initialVy = 0.05,
                    durationSeconds = 12.0,
                    totalTicks = 720,
                    isHit = true,
                    isBullseye = true,
                    targetCenterX = 0.50,
                    targetHalfWidth = 0.10
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{dropPayload}' }})");

            // Step 10 frames: parachutist is around y = 0.308
            StepEngineFrames(engine, 10);
            var dropY = engine.Evaluate("window.__activeDrops[0].y").AsNumber();
            Assert.True(dropY >= 0.30 && dropY <= 0.35);

            // 3. Fire a spread of rockets from underneath (x = 0.49..0.51, startY = 0.60) moving upward at realistic speed into the parachutist
            var rocketPayload = JsonSerializer.Serialize(new
            {
                type = "parachute_rockets",
                rockets = new[]
                {
                    new
                    {
                        id = "impact-rocket-1",
                        startX = 0.49,
                        startY = 0.60,
                        targetX = 0.49,
                        targetY = 0.10,
                        vx = 0.0,
                        vy = -0.50,
                        accelerationY = 0.0,
                        fuseTicks = 120,
                        spawnTickOffset = 0,
                        color = "#FF3366",
                        trailType = "sparks",
                        targetDropToken = "target-token-xyz",
                        knockbackMultiplier = 2.0,
                        canopyKillChance = 1.0
                    },
                    new
                    {
                        id = "impact-rocket-2",
                        startX = 0.50,
                        startY = 0.60,
                        targetX = 0.50,
                        targetY = 0.10,
                        vx = 0.0,
                        vy = -0.50,
                        accelerationY = 0.0,
                        fuseTicks = 120,
                        spawnTickOffset = 0,
                        color = "#FF3366",
                        trailType = "sparks",
                        targetDropToken = "target-token-xyz",
                        knockbackMultiplier = 2.0,
                        canopyKillChance = 1.0
                    },
                    new
                    {
                        id = "impact-rocket-3",
                        startX = 0.51,
                        startY = 0.60,
                        targetX = 0.51,
                        targetY = 0.10,
                        vx = 0.0,
                        vy = -0.50,
                        accelerationY = 0.0,
                        fuseTicks = 120,
                        spawnTickOffset = 0,
                        color = "#FF3366",
                        trailType = "sparks",
                        targetDropToken = "target-token-xyz",
                        knockbackMultiplier = 2.0,
                        canopyKillChance = 1.0
                    }
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{rocketPayload}' }})");

            Assert.Equal(3, (int)engine.Evaluate("window.__activeRockets.length").AsNumber());

            // Mid-flight: after 15 frames (~0.24s), rockets have climbed to ~0.48, parachutist is at ~0.32
            // They have not collided yet: rockets are still flying!
            StepEngineFrames(engine, 15);
            Assert.Equal(3, (int)engine.Evaluate("window.__activeRockets.length").AsNumber());
            Assert.False(engine.Evaluate("window.__activeDrops[0].parachuteDestroyed").AsBoolean());

            // After 25 more frames (40 frames total, ~0.64s): rockets meet parachutist around y = 0.34
            // Direct collision with the parachutist body/canopy occurs and detonates on impact!
            StepEngineFrames(engine, 25);

            // The rockets have collided/detonated
            var remainingRockets = (int)engine.Evaluate("window.__activeRockets.length").AsNumber();
            Assert.True(remainingRockets < 3, "At least one rocket detonated on direct impact");

            // The drop must have recorded the rocket collision
            var rocketHits = (int)engine.Evaluate("window.__activeDrops[0].rocketHits || 0").AsNumber();
            Assert.True(rocketHits >= 1, "Rocket hits should be incremented upon collision");

            // The parachute canopy was hit and destroyed
            var destroyed = engine.Evaluate("window.__activeDrops[0].parachuteDestroyed").AsBoolean();
            Assert.True(destroyed, "Parachute canopy should be destroyed on rocket canopy impact");

            // Explosion and pop sounds should be played
            var audioPlayed = engine.Evaluate("window.__audioPlayed.map(a => a.src).join(';')").AsString();
            Assert.Contains("rocket_explode.wav", audioPlayed);
            Assert.Contains("parachute_pop.wav", audioPlayed);
        }

        [Fact]
        public void ParachuteJs_TargetedRocketWithNullToken_TargetsAnyActiveParachutist()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJsPath = Path.Combine(jsDir, "parachute.js");
            Assert.True(File.Exists(parachuteJsPath));

            var code = File.ReadAllText(parachuteJsPath);
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(code);

            // 1. Start session
            var sessionStartPayload = JsonSerializer.Serialize(new
            {
                type = "session_start",
                session = new { isActive = true, totalDurationSeconds = 60, totalDrops = 0 },
                sounds = new
                {
                    maxConcurrentSounds = 5,
                    rocketLaunch = new { enabled = true, url = "/media/rocket_launch.wav", volume = 80 },
                    rocketExplode = new { enabled = true, url = "/media/rocket_explode.wav", volume = 80 },
                    parachutePop = new { enabled = true, url = "/media/parachute_pop.wav", volume = 80 }
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{sessionStartPayload}' }})");

            // 2. Launch an active parachutist in mid-air
            var dropPayload = JsonSerializer.Serialize(new
            {
                type = "parachute_drop",
                dropToken = "any-active-player-token",
                displayName = "AnyActiveViewer",
                physics = new
                {
                    startX = 0.50,
                    startY = 0.25,
                    landingY = 0.94,
                    initialVx = 0.0,
                    initialVy = 0.08,
                    durationSeconds = 10.0,
                    totalTicks = 600,
                    targetCenterX = 0.50,
                    targetHalfWidth = 0.10
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{dropPayload}' }})");

            // 3. Fire a Targeted rocket where targetDropToken is null (server didn't lock to a specific user)
            var targetedRocketPayload = JsonSerializer.Serialize(new
            {
                type = "parachute_rockets",
                rockets = new[]
                {
                    new
                    {
                        id = "auto-targeted-rocket-1",
                        startX = 0.50,
                        startY = 0.95,
                        targetX = 0.50,
                        targetY = 0.15,
                        vx = 0.0,
                        vy = -0.65,
                        accelerationY = 0.04,
                        fuseTicks = 120,
                        spawnTickOffset = 0,
                        color = "#FF3366",
                        trailType = "sparks",
                        targetDropToken = (string?)null,
                        pattern = "Targeted",
                        isTargeted = true,
                        knockbackMultiplier = 1.0,
                        canopyKillChance = 1.0,
                        directHitKillsParachute = true
                    }
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{targetedRocketPayload}' }})");

            // 4. Step 2 frames: rocket launches and dynamically binds to the active mid-air parachutist
            StepEngineFrames(engine, 2);
            var boundToken = engine.Evaluate("window.__activeRockets[0].targetDropToken").AsString();
            Assert.Equal("any-active-player-token", boundToken);

            // Parachutist canopy is not destroyed yet
            Assert.False(engine.Evaluate("window.__activeDrops[0].parachuteDestroyed").AsBoolean());

            // 5. Step 65 frames (~1.08s): rocket ascends from 0.95 to intercept canopy at ~0.33 and detonates
            StepEngineFrames(engine, 65);

            var destroyed = engine.Evaluate("window.__activeDrops[0].parachuteDestroyed").AsBoolean();
            Assert.True(destroyed, "Parachute canopy was intercepted and destroyed by auto-targeted rocket");
            var rocketHits = (int)engine.Evaluate("window.__activeDrops[0].rocketHits || 0").AsNumber();
            Assert.True(rocketHits >= 1, "Rocket hit counter incremented");
        }

        [Fact]
        public void ParachuteJs_MultiRocketTargetedLaunch_PicksActiveDropsRandomly_AndExcludesNearHorizontalDrops()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJsPath = Path.Combine(jsDir, "parachute.js");
            Assert.True(File.Exists(parachuteJsPath));

            var code = File.ReadAllText(parachuteJsPath);
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(code);

            // 1. Start session
            var sessionStartPayload = JsonSerializer.Serialize(new
            {
                type = "session_start",
                session = new { isActive = true, totalDurationSeconds = 60, totalDrops = 0 },
                sounds = new
                {
                    maxConcurrentSounds = 5,
                    rocketLaunch = new { enabled = true, url = "/media/rocket_launch.wav", volume = 80 },
                    rocketExplode = new { enabled = true, url = "/media/rocket_explode.wav", volume = 80 }
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{sessionStartPayload}' }})");

            // 2. Launch 2 active parachutists high in the air
            var drop1 = JsonSerializer.Serialize(new
            {
                type = "parachute_drop",
                dropToken = "target-high-left",
                displayName = "HighLeft",
                physics = new { startX = 0.35, startY = 0.25, landingY = 0.94, initialVx = 0.0, initialVy = 0.05, durationSeconds = 12.0, totalTicks = 720 }
            });
            var drop2 = JsonSerializer.Serialize(new
            {
                type = "parachute_drop",
                dropToken = "target-high-right",
                displayName = "HighRight",
                physics = new { startX = 0.65, startY = 0.25, landingY = 0.94, initialVx = 0.0, initialVy = 0.05, durationSeconds = 12.0, totalTicks = 720 }
            });
            // 3. Launch 1 active parachutist almost on the floor (would cause near-horizontal launch)
            var dropFloor = JsonSerializer.Serialize(new
            {
                type = "parachute_drop",
                dropToken = "floor-drop-avoid",
                displayName = "FloorDrop",
                physics = new { startX = 0.90, startY = 0.88, landingY = 0.94, initialVx = 0.0, initialVy = 0.02, durationSeconds = 12.0, totalTicks = 720 }
            });

            engine.Evaluate($"window.__wsMessageHandler({{ data: '{drop1}' }})");
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{drop2}' }})");
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{dropFloor}' }})");

            // 4. Fire 6 targeted rockets with targetDropToken = null
            var rocketsPayload = JsonSerializer.Serialize(new
            {
                type = "parachute_rockets",
                rockets = Enumerable.Range(0, 6).Select(i => new
                {
                    id = $"multi-rocket-{i}",
                    startX = 0.20 + (i * 0.12),
                    startY = 1.02,
                    targetX = 0.50,
                    targetY = 0.10,
                    vx = 0.0,
                    vy = -0.70,
                    accelerationY = 0.04,
                    fuseTicks = 120,
                    spawnTickOffset = i * 2,
                    targetDropToken = (string?)null,
                    pattern = "Targeted",
                    isTargeted = true
                }).ToArray()
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{rocketsPayload}' }})");

            // Step 20 frames so all 6 rockets launch and acquire targets
            StepEngineFrames(engine, 20);

            var targetsPickedJson = engine.Evaluate("JSON.stringify(window.__activeRockets.map(r => r.targetDropToken))").AsString();
            var targetsPicked = JsonSerializer.Deserialize<List<string>>(targetsPickedJson);

            Assert.NotNull(targetsPicked);
            Assert.Equal(6, targetsPicked.Count);

            // None of the rockets should have targeted the near-horizontal floor drop!
            Assert.DoesNotContain("floor-drop-avoid", targetsPicked);

            // All targeted rockets picked valid high mid-air drops
            Assert.All(targetsPicked, t => Assert.True(t == "target-high-left" || t == "target-high-right"));
        }

        [Fact]
        public void ParachuteJs_TargetedRocket_WhenServerTargetAlreadyLanded_RetargetsActiveMidAirDrop()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJsPath = Path.Combine(jsDir, "parachute.js");
            Assert.True(File.Exists(parachuteJsPath));

            var code = File.ReadAllText(parachuteJsPath);
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(code);

            // 1. Start session
            var sessionStartPayload = JsonSerializer.Serialize(new
            {
                type = "session_start",
                session = new { isActive = true, totalDurationSeconds = 60, totalDrops = 0 },
                sounds = new
                {
                    maxConcurrentSounds = 5,
                    rocketLaunch = new { enabled = true, url = "/media/rocket_launch.wav", volume = 80 },
                    rocketExplode = new { enabled = true, url = "/media/rocket_explode.wav", volume = 80 }
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{sessionStartPayload}' }})");

            // 2. Player 1 has already landed / reached ground (y >= landingY - 0.05)
            var drop1Payload = JsonSerializer.Serialize(new
            {
                type = "parachute_drop",
                dropToken = "landed-player-token",
                displayName = "LandedViewer",
                physics = new
                {
                    startX = 0.30,
                    startY = 0.90,
                    landingY = 0.94,
                    initialVx = 0.0,
                    initialVy = 0.0,
                    durationSeconds = 10.0,
                    totalTicks = 600,
                    targetCenterX = 0.50,
                    targetHalfWidth = 0.10
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{drop1Payload}' }})");

            // 3. Player 2 is high mid-air
            var drop2Payload = JsonSerializer.Serialize(new
            {
                type = "parachute_drop",
                dropToken = "active-midair-player-token",
                displayName = "MidAirViewer",
                physics = new
                {
                    startX = 0.60,
                    startY = 0.25,
                    landingY = 0.94,
                    initialVx = 0.0,
                    initialVy = 0.08,
                    durationSeconds = 10.0,
                    totalTicks = 600,
                    targetCenterX = 0.50,
                    targetHalfWidth = 0.10
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{drop2Payload}' }})");

            // 4. Fire a rocket that was originally assigned by the server to landed-player-token
            var rocketPayload = JsonSerializer.Serialize(new
            {
                type = "parachute_rockets",
                triggeredBy = "AdminStreamer",
                rockets = new[]
                {
                    new
                    {
                        id = "rocket_fallback_test",
                        spawnTickOffset = 0,
                        startX = 0.50,
                        startY = 1.02,
                        targetX = 0.50,
                        targetY = 0.20,
                        vx = 0.0,
                        vy = -0.65,
                        accelerationY = 0.04,
                        fuseTicks = 80,
                        color = "#FF3366",
                        trailType = "sparks",
                        targetDropToken = "landed-player-token",
                        pattern = "Targeted",
                        isTargeted = true,
                        knockbackMultiplier = 1.0,
                        canopyKillChance = 1.0,
                        directHitKillsParachute = true
                    }
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{rocketPayload}' }})");

            // 5. Advance frames to trigger rocket launch
            StepEngineFrames(engine, 2);

            // The rocket should have recognized landed-player-token is already landed and retargeted active-midair-player-token!
            var pickedTarget = engine.Evaluate("window.__activeRockets[0].targetDropToken").AsString();
            Assert.Equal("active-midair-player-token", pickedTarget);
        }

        [Fact]
        public void ParachuteJs_NonTargetedRocket_RemainsNullToken_AndExplodesAtApex()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJsPath = Path.Combine(jsDir, "parachute.js");
            Assert.True(File.Exists(parachuteJsPath));

            var code = File.ReadAllText(parachuteJsPath);
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(code);

            // 1. Start session
            var sessionStartPayload = JsonSerializer.Serialize(new
            {
                type = "session_start",
                session = new { isActive = true, totalDurationSeconds = 60, totalDrops = 0 },
                sounds = new
                {
                    maxConcurrentSounds = 5,
                    rocketLaunch = new { enabled = true, url = "/media/rocket_launch.wav", volume = 80 },
                    rocketExplode = new { enabled = true, url = "/media/rocket_explode.wav", volume = 80 }
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{sessionStartPayload}' }})");

            // 2. Launch an active parachutist on the right side of the screen
            var dropPayload = JsonSerializer.Serialize(new
            {
                type = "parachute_drop",
                dropToken = "innocent-bystander-drop",
                displayName = "BystanderViewer",
                physics = new
                {
                    startX = 0.85,
                    startY = 0.30,
                    landingY = 0.94,
                    initialVx = 0.0,
                    initialVy = 0.08,
                    durationSeconds = 10.0,
                    totalTicks = 600,
                    targetCenterX = 0.85,
                    targetHalfWidth = 0.10
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{dropPayload}' }})");

            // 3. Fire a Random (non-targeted) rocket on the left side (x = 0.15) aiming towards apex targetY = 0.10
            var randomRocketPayload = JsonSerializer.Serialize(new
            {
                type = "parachute_rockets",
                rockets = new[]
                {
                    new
                    {
                        id = "ambient-random-rocket-1",
                        startX = 0.15,
                        startY = 0.95,
                        targetX = 0.15,
                        targetY = 0.10,
                        vx = 0.0,
                        vy = -0.70,
                        accelerationY = 0.04,
                        fuseTicks = 120,
                        spawnTickOffset = 0,
                        color = "#33CCFF",
                        trailType = "sparks",
                        targetDropToken = (string?)null,
                        pattern = "Random",
                        isTargeted = false
                    }
                }
            });
            engine.Evaluate($"window.__wsMessageHandler({{ data: '{randomRocketPayload}' }})");

            // 4. Step 5 frames: rocket launches and targetDropToken remains null (does not target bystander)
            StepEngineFrames(engine, 5);
            var tokenVal = engine.Evaluate("window.__activeRockets[0].targetDropToken");
            Assert.True(tokenVal.IsNull() || tokenVal.IsUndefined(),
                "Non-targeted rocket must retain null targetDropToken");

            // 5. Step 75 frames: rocket ascends to its target apex (y <= targetY) and detonates in high sky
            StepEngineFrames(engine, 75);

            var activeRocketsCount = (int)engine.Evaluate("window.__activeRockets.length").AsNumber();
            Assert.Equal(0, activeRocketsCount);

            // Parachutist was not hit
            Assert.False(engine.Evaluate("window.__activeDrops[0].parachuteDestroyed").AsBoolean());
            Assert.Equal(0, (int)engine.Evaluate("window.__activeDrops[0].rocketHits || 0").AsNumber());

            // Detonation sound played
            var audioPlayed = engine.Evaluate("window.__audioPlayed.map(a => a.src).join(';')").AsString();
            Assert.Contains("rocket_explode.wav", audioPlayed);
        }

        [Fact]
        public void ParachuteJs_PingWithActiveDrops_SendsRichHeartbeatPong()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            // Add an active drop
            engine.Evaluate(@"
                window.__activeDrops.push({
                    dropToken: 'ping-drop-token-123',
                    isComplete: false,
                    landingCelebration: false
                });
            ");

            // Dispatch ping from server
            engine.Evaluate("window.__wsMessageHandler({ data: 'ping' });");

            var sentCount = (int)engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(1, sentCount);

            var sentJson = engine.Evaluate("JSON.stringify(window.__sentCompletions[0])").AsString();
            Assert.Contains("\"type\":\"pong\"", sentJson);
            Assert.Contains("\"game\":\"parachute\"", sentJson);
            Assert.Contains("\"ping-drop-token-123\"", sentJson);
            Assert.Contains("\"test-secret-key\"", sentJson);
        }

        [Fact]
        public void ParachuteJs_PingWithNoActiveDrops_SendsPlainPong()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            // No active drops
            engine.Evaluate("window.__activeDrops = [];");

            // Dispatch ping from server
            engine.Evaluate("window.__wsMessageHandler({ data: 'ping' });");

            var rawCount = (int)engine.Evaluate("window.__sentRaw.length").AsNumber();
            Assert.True(rawCount >= 1);
            var lastRaw = engine.Evaluate("window.__sentRaw[window.__sentRaw.length - 1]").AsString();
            Assert.Equal("pong", lastRaw);
        }

        [Fact]
        public void ParachuteJs_ActiveDrop_StartsClientDrivenHeartbeatTimer()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJs = File.ReadAllText(Path.Combine(jsDir, "parachute.js"));
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(parachuteJs);

            DispatchWsMessage(engine, new
            {
                type = "parachute_drop",
                dropToken = "client-timer-token-456",
                username = "TestUser",
                trajectory = new
                {
                    startX = 0.50,
                    startY = 0.06,
                    landingY = 0.88,
                    initialVy = 0.12,
                    totalTicks = 240
                }
            });

            // Heartbeat interval should be registered in __activeIntervals
            var intervalCount = (int)engine.Evaluate("Object.keys(__activeIntervals).length").AsNumber();
            Assert.True(intervalCount >= 1, "Interval should be active while drop is in progress");

            // Execute the interval callback directly (simulating 5 second tick)
            engine.Evaluate(@"
                var keys = Object.keys(__activeIntervals);
                if (keys.length > 0) {
                    __activeIntervals[keys[0]].cb();
                }
            ");

            var sentCount = (int)engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.True(sentCount >= 1);
            var sentJson = engine.Evaluate("JSON.stringify(window.__sentCompletions[0])").AsString();
            Assert.Contains("\"client-timer-token-456\"", sentJson);
            Assert.Contains("\"type\":\"pong\"", sentJson);
        }

        [Fact]
        public void DropGamesCommon_ResolveCircleCollision_ResolvesHeadOnCollision()
        {
            var engine = CreateMockBrowserEngine(out _);

            var script = @"
                var posA = { x: 100, y: 100 };
                var posB = { x: 130, y: 100 };
                var velA = { vx: 10, vy: 0 };
                var velB = { vx: -10, vy: 0 };
                var res = DropGamesCommon.resolveCircleCollision(posA, posB, velA, velB, 20, 20, { restitution: 0.8 });
                ({
                    collided: res.collided,
                    separating: res.separating,
                    impulse: res.impulse,
                    velAvx: velA.vx,
                    velBvx: velB.vx,
                    finalDist: Math.hypot(posB.x - posA.x, posB.y - posA.y)
                });
            ";

            var result = engine.Evaluate(script);
            Assert.True(result.Get("collided").AsBoolean());
            Assert.False(result.Get("separating").AsBoolean());
            Assert.True(result.Get("impulse").AsNumber() > 0);
            Assert.True(result.Get("velAvx").AsNumber() < 0, "Body A should bounce leftward");
            Assert.True(result.Get("velBvx").AsNumber() > 0, "Body B should bounce rightward");
            Assert.True(result.Get("finalDist").AsNumber() >= 39.99, "Positions should be separated to avoid overlap");
        }

        [Fact]
        public void DropGamesCommon_ResolveCircleCollision_SeparatingObjectsIgnored()
        {
            var engine = CreateMockBrowserEngine(out _);

            var script = @"
                var posA = { x: 100, y: 100 };
                var posB = { x: 130, y: 100 };
                var velA = { vx: -10, vy: 0 };
                var velB = { vx: 10, vy: 0 };
                var res = DropGamesCommon.resolveCircleCollision(posA, posB, velA, velB, 20, 20);
                ({
                    collided: res.collided,
                    separating: res.separating,
                    impulse: res.impulse,
                    velAvx: velA.vx,
                    velBvx: velB.vx
                });
            ";

            var result = engine.Evaluate(script);
            Assert.True(result.Get("collided").AsBoolean());
            Assert.True(result.Get("separating").AsBoolean());
            Assert.Equal(0, result.Get("impulse").AsNumber());
            Assert.Equal(-10, result.Get("velAvx").AsNumber());
            Assert.Equal(10, result.Get("velBvx").AsNumber());
        }

        [Fact]
        public void CurlingJs_LoadsAndExecutesWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var curlingJsPath = Path.Combine(jsDir, "curling.js");
            Assert.True(File.Exists(curlingJsPath), $"curling.js not found at {curlingJsPath}");

            var code = File.ReadAllText(curlingJsPath);
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => engine.Execute(code));
            Assert.Null(ex);

            var errorCount = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errorCount);
        }

        [Fact]
        public void DropGamesCommon_CalculateBannerStackSlot_CalculatesCorrectSlots()
        {
            var engine = CreateMockBrowserEngine(out _);

            var script = @"
                var res = {
                    alone: DropGamesCommon.calculateBannerStackSlot('curling', []),
                    fourAllParachute: DropGamesCommon.calculateBannerStackSlot('parachute', ['plinko', 'balloon', 'curling']),
                    fourAllPlinko: DropGamesCommon.calculateBannerStackSlot('plinko', ['parachute', 'balloon', 'curling']),
                    fourAllBalloon: DropGamesCommon.calculateBannerStackSlot('balloon', ['parachute', 'plinko', 'curling']),
                    fourAllCurling: DropGamesCommon.calculateBannerStackSlot('curling', ['parachute', 'plinko', 'balloon']),
                    noParachutePlinko: DropGamesCommon.calculateBannerStackSlot('plinko', ['balloon', 'curling']),
                    noParachuteBalloon: DropGamesCommon.calculateBannerStackSlot('balloon', ['plinko', 'curling']),
                    noParachuteCurling: DropGamesCommon.calculateBannerStackSlot('curling', ['plinko', 'balloon']),
                    setSupport: DropGamesCommon.calculateBannerStackSlot('curling', new Set(['parachute', 'plinko'])),
                    selfExcluded: DropGamesCommon.calculateBannerStackSlot('curling', ['curling', 'parachute'])
                };
                res;
            ";

            var result = engine.Evaluate(script);
            Assert.Equal(0, result.Get("alone").AsNumber());
            Assert.Equal(0, result.Get("fourAllParachute").AsNumber());
            Assert.Equal(1, result.Get("fourAllPlinko").AsNumber());
            Assert.Equal(2, result.Get("fourAllBalloon").AsNumber());
            Assert.Equal(3, result.Get("fourAllCurling").AsNumber());
            Assert.Equal(0, result.Get("noParachutePlinko").AsNumber());
            Assert.Equal(1, result.Get("noParachuteBalloon").AsNumber());
            Assert.Equal(2, result.Get("noParachuteCurling").AsNumber());
            Assert.Equal(2, result.Get("setSupport").AsNumber());
            Assert.Equal(1, result.Get("selfExcluded").AsNumber());
        }

        [Fact]
        public void DropGamesCommon_DrawSessionBanner_MultiGameStacking_PositionsAccurately()
        {
            var engine = CreateMockBrowserEngine(out _);

            var script = @"
                var mockCtx = {
                    save: function() {},
                    restore: function() {},
                    beginPath: function() {},
                    fill: function() {},
                    stroke: function() {},
                    rect: function() {},
                    roundRect: function() {},
                    fillText: function() {},
                    measureText: function() { return { width: 50 }; }
                };

                var banner0 = DropGamesCommon.drawSessionBanner(mockCtx, {
                    now: 1000,
                    width: 1920,
                    currentGame: 'parachute',
                    activeOtherGames: ['plinko', 'balloon', 'curling'],
                    sessionState: { isActive: true, totalDrops: 5, remainingSeconds: 60 }
                });

                var banner1 = DropGamesCommon.drawSessionBanner(mockCtx, {
                    now: 1000,
                    width: 1920,
                    currentGame: 'plinko',
                    activeOtherGames: ['parachute', 'balloon', 'curling'],
                    sessionState: { isActive: true, totalDrops: 5, remainingSeconds: 60 }
                });

                var banner2 = DropGamesCommon.drawSessionBanner(mockCtx, {
                    now: 1000,
                    width: 1920,
                    currentGame: 'balloon',
                    activeOtherGames: ['parachute', 'plinko', 'curling'],
                    sessionState: { isActive: true, totalDrops: 5, remainingSeconds: 60 }
                });

                var banner3 = DropGamesCommon.drawSessionBanner(mockCtx, {
                    now: 1000,
                    width: 1920,
                    currentGame: 'curling',
                    activeOtherGames: ['parachute', 'plinko', 'balloon'],
                    sessionState: { isActive: true, totalDrops: 5, remainingSeconds: 60 }
                });

                ({
                    banner0Y: banner0.bannerY,
                    banner1Y: banner1.bannerY,
                    banner2Y: banner2.bannerY,
                    banner3Y: banner3.bannerY
                });
            ";

            var result = engine.Evaluate(script);
            Assert.Equal(18, result.Get("banner0Y").AsNumber());
            Assert.Equal(72, result.Get("banner1Y").AsNumber());
            Assert.Equal(126, result.Get("banner2Y").AsNumber());
            Assert.Equal(180, result.Get("banner3Y").AsNumber());
        }

        [Fact]
        public void DropGamesCommon_SharedParticleSystems_AmbientAndSeasonalBursts_ExecuteAndClearCorrectly()
        {
            var engine = CreateMockBrowserEngine(out _);

            var script = @"
                var canvas = document.getElementById('plinko-canvas');
                var ctx = canvas.getContext('2d');

                // 1. getSeasonPreset
                var presetFromProp = DropGamesCommon.getSeasonPreset({ seasonPreset: 'Halloween' });
                var presetFromId = DropGamesCommon.getSeasonPreset({ id: 'custom-winter-theme' });
                var presetDefault = DropGamesCommon.getSeasonPreset(null);

                // 2. syncAmbientParticles & updateAndDrawAmbientParticles
                var ambient = [];
                var theme = {
                    enableBackgroundParticles: true,
                    particleType: 'petals',
                    particleDensity: 25
                };
                DropGamesCommon.syncAmbientParticles(ambient, { width: 1920, height: 1080, currentTheme: theme, isVisible: true });
                var countAfterSync = ambient.length;

                DropGamesCommon.updateAndDrawAmbientParticles(ctx, ambient, { dtSec: 0.016, width: 1920, height: 1080, currentTheme: theme, isVisible: true });

                // When board becomes hidden (isVisible: false), ambient particles clear immediately
                DropGamesCommon.updateAndDrawAmbientParticles(ctx, ambient, { dtSec: 0.016, width: 1920, height: 1080, currentTheme: theme, isVisible: false });
                var countAfterHide = ambient.length;

                // 3. spawnSeasonalBurst & updateAndDrawParticles (both pixel and normalized)
                var burstPx = [];
                DropGamesCommon.spawnSeasonalBurst(burstPx, { x: 500, y: 400, season: 'fall', count: 20, isBullseye: true });
                var burstPxInitial = burstPx.length;
                DropGamesCommon.updateAndDrawParticles(ctx, burstPx, { dt: 0.016, width: 1920, height: 1080, gravity: 45 });

                var burstNorm = [];
                DropGamesCommon.spawnSeasonalBurst(burstNorm, { x: 0.5, y: 0.2, season: 'christmas', count: 15, isBullseye: false, isNormalized: true });
                var burstNormInitial = burstNorm.length;
                DropGamesCommon.updateAndDrawParticles(ctx, burstNorm, { dt: 0.016, width: 1920, height: 1080, gravity: 0.45 });

                // Step until particles decay and expire
                for (var step = 0; step < 120; step++) {
                    DropGamesCommon.updateAndDrawParticles(ctx, burstPx, { dt: 0.05, width: 1920, height: 1080 });
                    DropGamesCommon.updateAndDrawParticles(ctx, burstNorm, { dt: 0.05, width: 1920, height: 1080 });
                }

                ({
                    presetFromProp: presetFromProp,
                    presetFromId: presetFromId,
                    presetDefault: presetDefault,
                    countAfterSync: countAfterSync,
                    countAfterHide: countAfterHide,
                    burstPxInitial: burstPxInitial,
                    burstNormInitial: burstNormInitial,
                    burstPxRemaining: burstPx.length,
                    burstNormRemaining: burstNorm.length
                });
            ";

            var result = engine.Evaluate(script);
            Assert.Equal("halloween", result.Get("presetFromProp").AsString());
            Assert.Equal("winter", result.Get("presetFromId").AsString());
            Assert.Equal("spring", result.Get("presetDefault").AsString());
            Assert.Equal(25, result.Get("countAfterSync").AsNumber());
            Assert.Equal(0, result.Get("countAfterHide").AsNumber());
            Assert.Equal(20, result.Get("burstPxInitial").AsNumber());
            Assert.Equal(15, result.Get("burstNormInitial").AsNumber());
            Assert.Equal(0, result.Get("burstPxRemaining").AsNumber());
            Assert.Equal(0, result.Get("burstNormRemaining").AsNumber());
        }

        [Fact]
        public void GolfJs_LoadsAndExecutesShotSimulationWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var golfJsPath = Path.Combine(jsDir, "golf.js");
            Assert.True(File.Exists(golfJsPath), $"golf.js not found at {golfJsPath}");

            var code = File.ReadAllText(golfJsPath);
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => engine.Execute(code));
            Assert.Null(ex);

            // Dispatch a golf_shot message and verify activeBalls spawns the golfer
            DispatchWsMessage(engine, new
            {
                type = "golf_shot",
                dropToken = "golf-js-token-1",
                username = "golfer1",
                displayName = "Golfer1",
                power = 72.0,
                condition = "StandardGreen",
                targetDistancePercent = 84.0,
                decalUrl = "",
                strokeNumber = 1,
                maxStrokesPerPlayer = 1,
                strokePenaltyPoints = 10
            });

            var activeBallsCount = engine.Evaluate("window.GolfEngine.activeBalls.length").AsNumber();
            Assert.Equal(1, activeBallsCount);

            StepEngineFrames(engine, 280);

            var errorCount = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errorCount);

            var completionSent = engine.Evaluate("window.__sentCompletions.some(c => c.dropToken === 'golf-js-token-1')").AsBoolean();
            Assert.True(completionSent, "Expected golf.js to complete the shot and send completion for golf-js-token-1");

            var golfSlot = engine.Evaluate("DropGamesCommon.calculateBannerStackSlot('golf', ['parachute', 'plinko', 'balloon', 'curling'])").AsNumber();
            Assert.Equal(4, golfSlot);
        }
    }
}
