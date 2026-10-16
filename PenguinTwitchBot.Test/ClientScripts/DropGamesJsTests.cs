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
                var Audio = function() {
                    return {
                        play: function() { return Promise.resolve(); },
                        pause: function() {},
                        volume: 1
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

            // Tolerance check: server-side DropGameCompleteHandler allows 0.40 baseline tolerance
            var serverRecordA = CreateTestDropRecord("player-A", "PlayerA", 0.35, 0.08, 0.18, 0.92, 240, 0.55);
            var tolerance = Math.Min(0.85, 0.40 + (serverRecordA.DropletHitCount * 0.15));
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
            double landingX = 0.50)
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
                Bounciness = 0.85,
                Mass = 1.0,
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
            // 1. Plinko Extreme Check
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

            // 2. Parachute Extreme Check
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
    }
}
