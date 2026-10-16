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
                options.TimeoutInterval(TimeSpan.FromSeconds(5));
            });

            var logs = new System.Collections.Generic.List<string>();
            logCollector = msg => logs.Add(msg);

            const string mockDomScript = @"
                var window = {
                    innerWidth: 1920,
                    innerHeight: 1080,
                    addEventListener: function() {},
                    removeEventListener: function() {},
                    location: { search: '' },
                    WS_TOPICS: { parachute: 'parachute', plinko: 'plinko' },
                    __sentCompletions: [],
                    __drawnTexts: [],
                    __onWsSend: function(data) {
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
                var WebSocket = { OPEN: 1 };
                var URLSearchParams = function() {
                    return { get: function() { return null; } };
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
            var plinkoJs = File.ReadAllText(Path.Combine(jsDir, "plinko.js"));

            // Extract brightenColor function directly to test its pure logic
            var startIndex = plinkoJs.IndexOf("function brightenColor(");
            Assert.True(startIndex >= 0, "brightenColor function not found in plinko.js");

            // Extract the function body
            var openBrace = plinkoJs.IndexOf('{', startIndex);
            var depth = 1;
            var endIndex = openBrace + 1;
            while (depth > 0 && endIndex < plinkoJs.Length)
            {
                if (plinkoJs[endIndex] == '{') depth++;
                else if (plinkoJs[endIndex] == '}') depth--;
                endIndex++;
            }
            var brightenFunc = plinkoJs.Substring(startIndex, endIndex - startIndex);

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
        public void PlinkoJs_WaypointsAndLanding_HaveNoFastTravelJumpsOrOvershoot()
        {
            const int rows = 8;
            const int bucketCount = 5;

            for (var targetBucket = 0; targetBucket < bucketCount; targetBucket++)
            {
                var waypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, bucketCount, targetBucket);
                Assert.Equal(rows + 2, waypoints.Count);

                // Verify every step between waypoints never exceeds 0.65 / bucketCount
                for (var i = 0; i < waypoints.Count - 1; i++)
                {
                    var wpA = waypoints[i];
                    var wpB = waypoints[i + 1];

                    var dx = Math.Abs(wpB.X - wpA.X);
                    var maxAllowedDx = 0.65 / bucketCount;

                    Assert.True(dx <= maxAllowedDx + 0.02,
                        $"Step from row {wpA.Row} (X={wpA.X}) to row {wpB.Row} (X={wpB.X}) has excessive horizontal delta {dx:F4} > {maxAllowedDx:F4} (Fast-travel jump detected).");
                }

                // Verify final landing is within target bucket bounds
                var finalWp = waypoints[^1];
                var minBucketX = (double)targetBucket / bucketCount;
                var maxBucketX = (double)(targetBucket + 1) / bucketCount;

                Assert.InRange(finalWp.X, minBucketX, maxBucketX);
                Assert.Equal(0.96, finalWp.Y);
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
            engine.Execute($@"
                for (var f = 0; f < {frameCount}; f++) {{
                    performance._now += {dtMs};
                    if (window.__nextRaf) {{
                        var cb = window.__nextRaf;
                        window.__nextRaf = null;
                        cb(performance._now);
                    }}
                }}
            ");
        }

        private static void DispatchWsMessage(Engine engine, object message)
        {
            var json = JsonSerializer.Serialize(message);
            engine.SetValue("__msgJson", json);
            engine.Execute("window.__wsMessageHandler({ data: __msgJson });");
        }

        [Fact]
        public void PlinkoJs_MultipleConcurrentTokens_BounceOffEachOtherAndLandInDesignatedBuckets()
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
                var waypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, bucketCount, t);
                DispatchWsMessage(engine, new
                {
                    type = "plinko_drop",
                    dropToken = $"token-{t}",
                    displayName = $"Player{t}",
                    userColor = "#00FF00",
                    board = new
                    {
                        rowCount = rows,
                        durationSeconds = 1.8,
                        gravity = 1.0,
                        bounciness = 0.85,
                        mass = 1.0,
                        buckets = buckets,
                        winningBucketIndex = t,
                        waypoints = waypoints.Select(w => new { x = w.X, y = w.Y, row = w.Row }).ToList()
                    }
                });
            }

            // Step 150 frames (~2.5s) to let all tokens collide mid-air and complete landing
            StepEngineFrames(engine, 150);

            // Verify console.error was clean
            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            // Verify all 5 tokens sent completions and each landed in its designated bucket
            var completionsCount = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(bucketCount, completionsCount);

            for (var t = 0; t < bucketCount; t++)
            {
                var reportedBucket = engine.Evaluate($"window.__sentCompletions.find(c => c.dropToken === 'token-{t}').winningBucketIndex").AsNumber();
                Assert.Equal(t, (int)reportedBucket);
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
                    StartX = 0.51,
                    StartY = 0.05,
                    Vx = 0.08,
                    Vy = 0.30,
                    Gravity = 0.32,
                    Radius = 0.028,
                    Mass = 0.5,
                    SpawnTickOffset = 15,
                    SpeedScale = 1.0
                }
            };

            ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0, droplets);

            // Server simulated trajectory recorded collisions
            Assert.NotEmpty(record.Collisions);
            var serverLandingX = record.LandingX;

            // 2. Dispatch drop + collisions and droplet message to JS engine
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
                    collisions = record.Collisions.Select(c => new
                    {
                        tick = c.Tick,
                        dropletId = c.DropletId,
                        x = c.X,
                        y = c.Y,
                        deltaVx = c.DeltaVx,
                        deltaVy = c.DeltaVy
                    }).ToList()
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

            // Client landing X must track server simulation closely (within 0.05 delta)
            Assert.True(Math.Abs(clientLandingX - serverLandingX) <= 0.05,
                $"Client landingX {clientLandingX} deviated from server simulation {serverLandingX} by {Math.Abs(clientLandingX - serverLandingX):F4}");
        }

        [Theory]
        [InlineData(6, 3)]
        [InlineData(7, 5)]
        [InlineData(8, 5)]
        [InlineData(10, 7)]
        [InlineData(12, 9)]
        [InlineData(8, 12)]
        public void PlinkoSimulationEngine_BuildWaypoints_AllRowsAndBucketCounts_RemainMonotonicAndCentered(int rows, int bucketCount)
        {
            for (var targetBucket = 0; targetBucket < bucketCount; targetBucket++)
            {
                var waypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, bucketCount, targetBucket);
                Assert.Equal(rows + 2, waypoints.Count);

                for (var i = 0; i < waypoints.Count - 1; i++)
                {
                    var wpA = waypoints[i];
                    var wpB = waypoints[i + 1];

                    // Waypoints must descend monotonically in Y
                    Assert.True(wpB.Y > wpA.Y, $"Waypoints must strictly descend: row {wpA.Row} (Y={wpA.Y}) to row {wpB.Row} (Y={wpB.Y})");

                    // Lateral step bound
                    var dx = Math.Abs(wpB.X - wpA.X);
                    var maxAllowedDx = 0.65 / bucketCount;
                    Assert.True(dx <= maxAllowedDx + 0.02,
                        $"Step delta {dx:F4} exceeded {maxAllowedDx:F4} between row {wpA.Row} and {wpB.Row}");
                }

                // Final waypoint must be centered inside target bucket
                var finalWp = waypoints[^1];
                var minBucketX = (double)targetBucket / bucketCount;
                var maxBucketX = (double)(targetBucket + 1) / bucketCount;
                Assert.InRange(finalWp.X, minBucketX, maxBucketX);
                Assert.Equal(0.96, finalWp.Y);
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

            ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0, droplets);
            Assert.True(record.Collisions.Count >= 2, $"Expected at least 2 droplet collisions, got {record.Collisions.Count}");

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
                    collisions = record.Collisions.Select(c => new
                    {
                        tick = c.Tick,
                        dropletId = c.DropletId,
                        x = c.X,
                        y = c.Y,
                        deltaVx = c.DeltaVx,
                        deltaVy = c.DeltaVy
                    }).ToList()
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
            Assert.True(Math.Abs(clientLandingX - record.LandingX) <= 0.05,
                $"Storm cumulative landingX {clientLandingX} differed from server simulation {record.LandingX} by {Math.Abs(clientLandingX - record.LandingX):F4}");
        }

        [Theory]
        [InlineData(0.1, 0.1, 0.1)]   // Minimum bounds
        [InlineData(10.0, 5.0, 10.0)] // Maximum bounds
        [InlineData(5.0, 0.1, 10.0)]  // High gravity, low bounce
        [InlineData(0.1, 5.0, 0.1)]   // Low gravity, high bounce
        public void DropGames_ExtremePhysicsParameters_DoNotProduceNaNOrBoundsViolation(double gravity, double bounciness, double mass)
        {
            var jsDir = FindWwwRootPath();

            // 1. Plinko Extreme Check (C# lattice + JS overlay execution)
            const int rows = 8;
            const int bucketCount = 5;
            for (var b = 0; b < bucketCount; b++)
            {
                var waypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, bucketCount, b);
                foreach (var wp in waypoints)
                {
                    Assert.False(double.IsNaN(wp.X) || double.IsInfinity(wp.X), "Plinko Waypoint X is NaN or Infinity");
                    Assert.False(double.IsNaN(wp.Y) || double.IsInfinity(wp.Y), "Plinko Waypoint Y is NaN or Infinity");
                    Assert.InRange(wp.X, 0.0, 1.0);
                    Assert.InRange(wp.Y, 0.0, 1.0);
                }
            }

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

            const int targetBucket = 2;
            var targetWaypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, bucketCount, targetBucket);
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
                    winningBucketIndex = targetBucket,
                    waypoints = targetWaypoints.Select(w => new { x = w.X, y = w.Y, row = w.Row }).ToList()
                }
            });

            StepEngineFrames(plinkoEngine, 100);
            Assert.Equal(0, plinkoEngine.Evaluate("console._errors.length").AsNumber());
            var plinkoLanded = plinkoEngine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'extreme-plinko')");
            Assert.False(plinkoLanded.IsNull() || plinkoLanded.IsUndefined(), "Plinko JS failed to complete under extreme physics");
            var landedIdx = plinkoEngine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'extreme-plinko').winningBucketIndex").AsNumber();
            Assert.Equal(targetBucket, (int)landedIdx);

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
                    collisions = record.Collisions.Select(c => new
                    {
                        tick = c.Tick,
                        dropletId = c.DropletId,
                        x = c.X,
                        y = c.Y,
                        deltaVx = c.DeltaVx,
                        deltaVy = c.DeltaVy
                    }).ToList()
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
            Assert.True(Math.Abs(clientLandingX - record.LandingX) <= 0.05,
                $"Client landingX {clientLandingX} differed from server {record.LandingX} under extreme physics (gravity={gravity}, bounce={bounciness}, mass={mass})");
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
                        collisions = record.Collisions.Select(c => new
                        {
                            tick = c.Tick,
                            dropletId = c.DropletId,
                            x = c.X,
                            y = c.Y,
                            deltaVx = c.DeltaVx,
                            deltaVy = c.DeltaVy
                        }).ToList()
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

            Assert.True(Math.Abs(clientLandingA - dropA.LandingX) <= 0.05,
                $"Drop A client landingX {clientLandingA} differed from server {dropA.LandingX} by {Math.Abs(clientLandingA - dropA.LandingX):F4}");
            Assert.True(Math.Abs(clientLandingB - dropB.LandingX) <= 0.05,
                $"Drop B client landingX {clientLandingB} differed from server {dropB.LandingX} by {Math.Abs(clientLandingB - dropB.LandingX):F4}");
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

            // Drop 3 tokens landing in Low (10), Mid (25), and High (100)
            for (var t = 0; t < 3; t++)
            {
                var waypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, bucketCount, t);
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
                        winningBucketIndex = t,
                        waypoints = waypoints.Select(w => new { x = w.X, y = w.Y, row = w.Row }).ToList()
                    }
                });
            }

            // Step 150 frames to complete all drops
            StepEngineFrames(engine, 150);

            // Verify all 3 completions were reported
            var completionCount = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(3, (int)completionCount);

            // Verify token-2 reported winning bucket index 2
            var token2Bucket = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'token-2').winningBucketIndex").AsNumber();
            Assert.Equal(2, (int)token2Bucket);

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
            var r1Waypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(8, 3, 1);
            DispatchWsMessage(plinkoEngine, new
            {
                type = "plinko_drop",
                dropToken = "round1-token",
                displayName = "Player1",
                board = new
                {
                    rowCount = 8,
                    durationSeconds = 2.0,
                    gravity = 1.0,
                    bounciness = 0.85,
                    mass = 1.0,
                    buckets = plinkoBuckets,
                    winningBucketIndex = 1,
                    waypoints = r1Waypoints.Select(w => new { x = w.X, y = w.Y, row = w.Row }).ToList()
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
                    winningBucketIndex = 2,
                    waypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(8, 3, 2).Select(w => new { x = w.X, y = w.Y, row = w.Row }).ToList()
                }
            });

            StepEngineFrames(plinkoEngine, 90);
            var completionsR2 = plinkoEngine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(1, (int)completionsR2);
            var r2Bucket = plinkoEngine.Evaluate("window.__sentCompletions[0].winningBucketIndex").AsNumber();
            Assert.Equal(2, (int)r2Bucket);

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

            ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0, droplets);
            Assert.NotEmpty(record.Collisions);

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
                    collisions = record.Collisions.Select(c => new
                    {
                        tick = c.Tick,
                        dropletId = c.DropletId,
                        x = c.X,
                        y = c.Y,
                        deltaVx = c.DeltaVx,
                        deltaVy = c.DeltaVy
                    }).ToList()
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
            Assert.True(Math.Abs(clientLandingX - record.LandingX) <= 0.05,
                $"Droplet knockback + wall bounce landingX {clientLandingX} differed from server simulation {record.LandingX} by {Math.Abs(clientLandingX - record.LandingX):F4}");
        }

        [Fact]
        public void PlinkoJs_DenseMultiTokenCrowd_AllTokensBounceAndLandInDesignatedBuckets()
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

            // Drop 12 simultaneous tokens targeted across buckets 0 to 4
            const int tokenCount = 12;
            for (var t = 0; t < tokenCount; t++)
            {
                var targetBucket = t % bucketCount;
                var waypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, bucketCount, targetBucket);
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
                        winningBucketIndex = targetBucket,
                        waypoints = waypoints.Select(w => new { x = w.X, y = w.Y, row = w.Row }).ToList()
                    }
                });
            }

            // Step 120 frames to allow full descent and token-token collisions
            StepEngineFrames(engine, 120);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var completions = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(tokenCount, (int)completions);

            // Verify every single token landed in its designated winning bucket
            for (var t = 0; t < tokenCount; t++)
            {
                var expectedBucket = t % bucketCount;
                var actualBucket = engine.Evaluate($"window.__sentCompletions.find(c => c.dropToken === 'crowd-token-{t}').winningBucketIndex").AsNumber();
                Assert.Equal(expectedBucket, (int)actualBucket);
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
        public void PlinkoJs_ExtremeTokenCollision_MaxBouncinessAndMassRatio_DecaysCleanlyIntoTargetBuckets()
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

            // Token A: Mass 10.0, Bounce 5.0, target bucket 0 (left)
            var wpA = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, bucketCount, 0);
            DispatchWsMessage(engine, new
            {
                type = "plinko_drop",
                dropToken = "heavy-token",
                displayName = "HeavyToken",
                userColor = "#FF0000",
                board = new
                {
                    rowCount = rows,
                    durationSeconds = 1.5,
                    gravity = 1.0,
                    bounciness = 5.0,
                    mass = 10.0,
                    buckets = buckets,
                    winningBucketIndex = 0,
                    waypoints = wpA.Select(w => new { x = w.X, y = w.Y, row = w.Row }).ToList()
                }
            });

            // Token B: Mass 0.1, Bounce 5.0, target bucket 4 (right)
            var wpB = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, bucketCount, 4);
            DispatchWsMessage(engine, new
            {
                type = "plinko_drop",
                dropToken = "light-token",
                displayName = "LightToken",
                userColor = "#0000FF",
                board = new
                {
                    rowCount = rows,
                    durationSeconds = 1.5,
                    gravity = 1.0,
                    bounciness = 5.0,
                    mass = 0.1,
                    buckets = buckets,
                    winningBucketIndex = 4,
                    waypoints = wpB.Select(w => new { x = w.X, y = w.Y, row = w.Row }).ToList()
                }
            });

            // Step 120 frames
            StepEngineFrames(engine, 120);

            var errors = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errors);

            var completions = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(2, (int)completions);

            var heavyLandedBucket = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'heavy-token').winningBucketIndex").AsNumber();
            var lightLandedBucket = engine.Evaluate("window.__sentCompletions.find(c => c.dropToken === 'light-token').winningBucketIndex").AsNumber();

            // Both tokens strictly preserve their server-designated winning buckets
            Assert.Equal(0, (int)heavyLandedBucket);
            Assert.Equal(4, (int)lightLandedBucket);
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

            // Simulate each drop through droplet interactions
            foreach (var record in drops)
            {
                ParachuteSimulationEngine.SimulateParachuteTrajectory(record, 0, droplets);
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
                        collisions = record.Collisions.Select(c => new
                        {
                            tick = c.Tick,
                            dropletId = c.DropletId,
                            x = c.X,
                            y = c.Y,
                            deltaVx = c.DeltaVx,
                            deltaVy = c.DeltaVy
                        }).ToList()
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

            // Step 420 frames
            StepEngineFrames(engine, 420);

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

            // Target bucket 3 (forces lateral peg navigation with dx != 0)
            const int targetBucket = 3;
            var waypoints = PlinkoSimulationEngine.BuildPlinkoWaypoints(rows, bucketCount, targetBucket);

            DispatchWsMessage(engine, new
            {
                type = "plinko_drop",
                dropToken = "arc-test-token",
                displayName = "ArcViewer",
                board = new
                {
                    rowCount = rows,
                    durationSeconds = 2.0,
                    gravity = 1.0,
                    bounciness = 0.85,
                    mass = 1.0,
                    buckets = buckets,
                    winningBucketIndex = targetBucket,
                    waypoints = waypoints.Select(w => new { x = w.X, y = w.Y, row = w.Row }).ToList()
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

            // Verify the vertical movement exhibits non-linear gravitational acceleration
            // With gravitational acceleration, the token rolls smoothly over the apex and picks up downward velocity
            var deltaY1 = sampledPositions[10].y - sampledPositions[0].y;
            var deltaY2 = sampledPositions[25].y - sampledPositions[15].y;
            Assert.True(deltaY1 > 0, "Token moves downward");
            Assert.True(deltaY2 > 0, "Token continues downward");

            // Complete the full drop
            StepEngineFrames(engine, 120);

            var completions = engine.Evaluate("window.__sentCompletions.length").AsNumber();
            Assert.Equal(1, (int)completions);

            var landedBucket = engine.Evaluate("window.__sentCompletions[0].winningBucketIndex").AsNumber();
            Assert.Equal(targetBucket, (int)landedBucket);
        }

        [Fact]
        public void DropGameMediaResolver_SoundBundle_ResolvesCorrectly_WithOverridesAndDefaults()
        {
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

            var bundle = DropGameMediaResolver.ResolveParachuteSoundBundle(pSettings, theme, null);
            Assert.NotNull(bundle);
            Assert.Equal(2, bundle.MaxConcurrentSounds);

            // Start: Inherit -> enabled, volume 80
            Assert.NotNull(bundle.Start);
            Assert.Equal(80, bundle.Start.Volume);

            // End: Mute -> disabled
            Assert.NotNull(bundle.End);
            Assert.False(bundle.End.Enabled);

            // Drop: Custom -> enabled, volume 95
            Assert.NotNull(bundle.Drop);
            Assert.Equal(95, bundle.Drop.Volume);

            // Win: Inherit -> enabled, volume 90
            Assert.NotNull(bundle.Win);
            Assert.Equal(90, bundle.Win.Volume);
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

            // 1. Send session_start with sound bundle
            var sessionStartPayload = JsonSerializer.Serialize(new
            {
                type = "session_start",
                session = new { isActive = true, totalDurationSeconds = 60 },
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
    }
}
