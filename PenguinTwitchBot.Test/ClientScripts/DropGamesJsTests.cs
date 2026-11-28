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
    public class DropGamesJsTests : DropGamesJsTestBase
    {
        [Fact]
        public void DropGamesCommon_UpdateActiveGameState_IgnoresOtherDropActive()
        {
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "drop-games-common.js"));

            engine.Execute(@"
                var activeSet = new Set();
                window.DropGamesCommon.updateActiveGameStateFromMessage(activeSet, 'parachute', {
                    type: 'other_drop_active',
                    otherGame: 'plinko',
                    isOtherGameActive: true
                });
                window.__activeCount = activeSet.size;
            ");

            var activeCount = engine.Evaluate("window.__activeCount").AsNumber();
            Assert.Equal(0, activeCount);
        }


        [Fact]
        public void DropGamesCommon_OverlayBus_CoordinatesDropsLocally()
        {
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-client.js"));
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "drop-games-common.js"));

            engine.Execute(@"
                var socket = {
                    readyState: 1,
                    send: function() {},
                    addEventListener: function() {}
                };
                var completionChannel = window.DropGamesCommon.createCompletionChannel({
                    game: 'curling',
                    socket: socket,
                    overlayKey: 'test-key'
                });
                window.__curlingReceived = [];
                completionChannel.attach(function(msg) {
                    window.__curlingReceived.push(msg);
                });

                // Plinko drops without touching server
                window.DropGamesCommon.broadcastDropActive('plinko', 8000);
            ");

            Assert.Equal(1, engine.Evaluate("window.__curlingReceived.length").AsNumber());
            Assert.Equal("other_drop_active", engine.Evaluate("window.__curlingReceived[0].type").AsString());
            Assert.Equal("plinko", engine.Evaluate("window.__curlingReceived[0].otherGame").AsString());
        }


        [Fact]
        public void DropGamesCommon_PlinkoAndBalloonSimultaneous_DoNotOverlapBanners()
        {
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "drop-games-common.js"));

            engine.Execute(@"
                var now = 100000;
                var ctx = {
                    save: function() {},
                    restore: function() {},
                    beginPath: function() {},
                    roundRect: function() {},
                    fill: function() {},
                    stroke: function() {},
                    fillText: function() {},
                    measureText: function() { return { width: 50 }; }
                };

                var plinkoActiveOthers = new Set(['balloon']);
                var balloonActiveOthers = new Set(['plinko']);

                var plinkoRes = window.DropGamesCommon.drawSessionBanner(ctx, {
                    now: now,
                    width: 1920,
                    headerConfig: { yOffset: 18, autoAvoidOverlap: true },
                    sessionState: { isActive: true, totalDrops: 5, totalDurationSeconds: 90, startedAtMs: now - 10000, endsAtMs: now + 80000 },
                    currentGame: 'plinko',
                    activeOtherGames: plinkoActiveOthers,
                    isOtherGameActive: true,
                    otherDropsActiveUntilMs: now + 8000, // balloon drop is active!
                    currentBannerY: 18
                });

                var balloonRes = window.DropGamesCommon.drawSessionBanner(ctx, {
                    now: now,
                    width: 1920,
                    headerConfig: { yOffset: 18, autoAvoidOverlap: true },
                    sessionState: { isActive: true, totalDrops: 3, totalDurationSeconds: 90, startedAtMs: now - 10000, endsAtMs: now + 80000 },
                    currentGame: 'balloon',
                    activeOtherGames: balloonActiveOthers,
                    isOtherGameActive: true,
                    otherDropsActiveUntilMs: 0,
                    currentBannerY: 72
                });

                window.__plinkoBannerY = plinkoRes.bannerY;
                window.__balloonBannerY = balloonRes.bannerY;
            ");

            var plinkoY = engine.Evaluate("window.__plinkoBannerY").AsNumber();
            var balloonY = engine.Evaluate("window.__balloonBannerY").AsNumber();

            // Plinko should stay at top slot 0 (18px), Balloon at slot 1 (72px)
            Assert.Equal(18, plinkoY);
            Assert.Equal(72, balloonY);
            Assert.NotEqual(plinkoY, balloonY);
        }


        [Fact]
        public void BalloonJs_LoadsAndExecutesWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var balloonJsPath = Path.Combine(jsDir, "balloon.js");
            Assert.True(File.Exists(balloonJsPath), $"balloon.js not found at {balloonJsPath}");
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => ExecuteScriptFile(engine, balloonJsPath));
            Assert.Null(ex);

            var errorCount = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errorCount);
        }


        [Fact]
        public void RacingJs_FallbackRenderLoop_ContinuesSchedulingFrames()
        {
            var jsDir = FindWwwRootPath();
            var racingJsPath = Path.Combine(jsDir, "racing.js");
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute("DropGamesCommon.createRenderLoop = undefined;");

            var ex = Record.Exception(() => ExecuteScriptFile(engine, racingJsPath));
            Assert.Null(ex);
            Assert.True(engine.Evaluate("window.__nextRaf !== null").AsBoolean());

            engine.Execute(@"
                var renderFrame = window.__nextRaf;
                window.__nextRaf = null;
                performance._now += 16.666;
                renderFrame(performance._now);
            ");

            Assert.True(engine.Evaluate("window.__nextRaf !== null").AsBoolean());
            Assert.Equal(0, engine.Evaluate("console._errors.length").AsNumber());
        }


        [Fact]
        public void BalloonJs_ManualSessionEnd_HidesCloudAfterGraceWithOtherGamesActive()
        {
            var jsDir = FindWwwRootPath();
            var balloonJsPath = Path.Combine(jsDir, "balloon.js");
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, balloonJsPath);

            DispatchWsMessage(engine, new
            {
                type = "other_game_status",
                otherGame = "plinko",
                isOtherGameActive = true,
                activeGames = new[] { "parachute", "plinko", "curling", "golf" }
            });
            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new { totalDurationSeconds = 90, remainingSeconds = 90, totalDrops = 0 },
                target = new { centerX = 0.5, halfWidth = 0.09, speed = 0.5 }
            });
            StepEngineFrames(engine, 1);
            Assert.True(engine.Evaluate("window.__canvasFillCount").AsNumber() > 0);

            DispatchWsMessage(engine, new
            {
                type = "session_end",
                session = new { isActive = false, totalDrops = 0 }
            });
            StepEngineFrames(engine, 361);
            var fillsAfterGrace = engine.Evaluate("window.__canvasFillCount").AsNumber();
            StepEngineFrames(engine, 1);

            Assert.Equal(fillsAfterGrace, engine.Evaluate("window.__canvasFillCount").AsNumber());
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


        [Fact]
        public void DropGamesCommon_SharedStripMetrics_PreservesPositionAndDirectionRules()
        {
            var engine = CreateMockBrowserEngine(out _);
            var metrics = engine.Evaluate("DropGamesCommon.getStripMetrics(1920, 1080, { stripPosition: 'Left', trackThicknessPercent: 14, slideDirection: 'RightToLeft', targetDistancePercent: 78 }, 78)");

            Assert.Equal(0, metrics.Get("x").AsNumber());
            Assert.Equal(268.8, metrics.Get("w").AsNumber(), 3);
            Assert.Equal(1080, metrics.Get("length").AsNumber());
            Assert.Equal(268.8, metrics.Get("breadth").AsNumber(), 3);
            Assert.False(metrics.Get("isHorizontal").AsBoolean());
            Assert.True(metrics.Get("isReversed").AsBoolean());
            Assert.Equal(0.22, metrics.Get("effectiveTargetRatio").AsNumber(), 3);
            Assert.Equal(237.6, metrics.Get("targetCenterY").AsNumber(), 3);

            var centered = engine.Evaluate("DropGamesCommon.getStripMetrics(1920, 1080, { stripPosition: 'Bottom', slideDirection: 'CenterTarget', targetDistancePercent: 84 }, 84)");
            Assert.Equal(0.50, centered.Get("effectiveTargetRatio").AsNumber());
        }


        [Fact]
        public void DropGamesCommon_UpdateActiveGameState_HandlesStatusAndSessionPayloads()
        {
            var engine = CreateMockBrowserEngine(out _);
            var result = engine.Evaluate(@"
                var activeGames = new Set();
                var directStatus = DropGamesCommon.updateActiveGameStateFromMessage(activeGames, 'plinko', {
                    type: 'other_game_status', otherGame: 'golf', isOtherGameActive: true
                });
                var directTracked = activeGames.has('golf');
                var sessionStatus = DropGamesCommon.updateActiveGameStateFromMessage(activeGames, 'plinko', {
                    type: 'session_update', activeGames: ['plinko', 'parachute']
                });
                ({ directStatus: directStatus, directTracked: directTracked, sessionStatus: sessionStatus,
                   excludesSelf: !activeGames.has('plinko'), tracksOther: activeGames.has('parachute') });
            ");

            Assert.True(result.Get("directStatus").AsBoolean());
            Assert.True(result.Get("directTracked").AsBoolean());
            Assert.True(result.Get("sessionStatus").AsBoolean());
            Assert.True(result.Get("excludesSelf").AsBoolean());
            Assert.True(result.Get("tracksOther").AsBoolean());
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
            var commonJs = ReadScriptText(Path.Combine(jsDir, "drop-games-common.js"));

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
            var plinkoEngine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(plinkoEngine, Path.Combine(jsDir, "plinko.js"));

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
            var parachuteEngine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(parachuteEngine, Path.Combine(jsDir, "parachute.js"));

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
        public void DropGames_SessionLeaderboardCrown_IdentifiesAndCrownsHighestScore()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "plinko.js"));

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
            var plinkoEngine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(plinkoEngine, Path.Combine(jsDir, "plinko.js"));

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
            var parachuteEngine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(parachuteEngine, Path.Combine(jsDir, "parachute.js"));

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
        public void DropGamesAudioJs_LoadsAndExecutesWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var audioJsPath = Path.Combine(jsDir, "drop-games-audio.js");
            Assert.True(File.Exists(audioJsPath), $"drop-games-audio.js not found at {audioJsPath}");
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => ExecuteScriptFile(engine, audioJsPath));
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
        public void DropGamesAudioJs_AppliesPerceptualQuadraticVolumeCurve()
        {
            var engine = CreateMockBrowserEngine(out _);

            Assert.Equal(0.0, engine.Evaluate("DropGamesAudio.percentToGain(0)").AsNumber(), 4);
            Assert.Equal(0.04, engine.Evaluate("DropGamesAudio.percentToGain(20)").AsNumber(), 4);
            Assert.Equal(0.25, engine.Evaluate("DropGamesAudio.percentToGain(50)").AsNumber(), 4);
            Assert.Equal(0.49, engine.Evaluate("DropGamesAudio.percentToGain(70)").AsNumber(), 4);
            Assert.Equal(1.0, engine.Evaluate("DropGamesAudio.percentToGain(100)").AsNumber(), 4);

            engine.Execute(@"
                window.__audioPlayed = [];
                var sm = DropGamesAudio.createSoundManager();
                sm.updateBundle({ maxConcurrentSounds: 4 });
                sm.play({ enabled: true, url: '/audio/silent.mp3', volume: 0 }, 'silent', false);
                sm.play({ enabled: true, url: '/audio/quiet.mp3', volume: 20 }, 'quiet', false);
                sm.play({ enabled: true, url: '/audio/half.mp3', volume: 50 }, 'half', false);
                sm.play({ enabled: true, url: '/audio/full.mp3', volume: 100 }, 'full', false);
            ");

            // 0% volume should not play at all; remaining 3 sounds should honor quadratic perceptual gain
            var quietEntries = engine.Evaluate("window.__audioPlayed.filter(function(a) { return a.src === '/audio/quiet.mp3'; })");
            Assert.Equal(0, (int)engine.Evaluate("window.__audioPlayed.filter(function(a) { return a.src === '/audio/silent.mp3'; }).length").AsNumber());
            Assert.Equal(0.04, engine.Evaluate("window.__audioPlayed.filter(function(a) { return a.src === '/audio/quiet.mp3'; })[0].volume").AsNumber(), 4);
            Assert.Equal(0.25, engine.Evaluate("window.__audioPlayed.filter(function(a) { return a.src === '/audio/half.mp3'; })[0].volume").AsNumber(), 4);
            Assert.Equal(1.0, engine.Evaluate("window.__audioPlayed.filter(function(a) { return a.src === '/audio/full.mp3'; })[0].volume").AsNumber(), 4);
        }


        [Fact]
        public void DropGamesAudioJs_ZeroVolumeDoesNotStealActiveVoice()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                window.__audioPlayed = [];
                var sm = DropGamesAudio.createSoundManager();
                sm.updateBundle({ maxConcurrentSounds: 1 });
                sm.play({ enabled: true, url: '/audio/go.wav', volume: 80 }, 'countdown_go', true);
                sm.play({ enabled: true, url: '/audio/muted.wav', volume: 0 }, 'engine', false);
            ");

            Assert.Equal(1, (int)engine.Evaluate("sm.getActiveCount()").AsNumber());
            Assert.Equal(0, (int)engine.Evaluate("window.__audioPlayed.filter(function(a) { return a.src === '/audio/muted.wav'; }).length").AsNumber());
        }


        [Fact]
        public void DropGamesCommonJs_LoadsAndExecutesWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var commonJsPath = Path.Combine(jsDir, "drop-games-common.js");
            Assert.True(File.Exists(commonJsPath), $"drop-games-common.js not found at {commonJsPath}");
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => ExecuteScriptFile(engine, commonJsPath));
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
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => ExecuteScriptFile(engine, curlingJsPath));
            Assert.Null(ex);

            var errorCount = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errorCount);
        }


        [Fact]
        public void CurlingJs_PowerVarianceAndGuaranteedStraightShot_BehavesAsExpected()
        {
            var jsDir = FindWwwRootPath();
            var curlingJsPath = Path.Combine(jsDir, "curling.js");
            Assert.True(File.Exists(curlingJsPath), $"curling.js not found at {curlingJsPath}");
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => ExecuteScriptFile(engine, curlingJsPath));
            Assert.Null(ex);

            // 1. Session start with powerVariancePercent: 4.0
            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new { totalDurationSeconds = 60 },
                powerVariancePercent = 4.0,
                condition = "Normal",
                stripPosition = "Bottom",
                slideDirection = "LeftToRight",
                targetDistancePercent = 80.0
            });

            var boardVariance = engine.Evaluate("window.CurlingEngine.boardConfig.powerVariancePercent").AsNumber();
            Assert.Equal(4.0, boardVariance);

            // 2. Dispatch multiple curling slides with raw power 75.0 and variance active
            for (var i = 0; i < 5; i++)
            {
                DispatchWsMessage(engine, new
                {
                    type = "curling_slide",
                    dropToken = $"var-stone-{i}",
                    username = $"player{i}",
                    power = 75.0,
                    effectivePower = 76.25,
                    powerVariancePercent = 4.0,
                    condition = "Normal"
                });
            }

            var stonesCount = engine.Evaluate("window.CurlingEngine.activeStones.length").AsNumber();
            Assert.Equal(5, stonesCount);

            // Client must use the server-resolved effective power (no client-side resampling)
            var minPower = engine.Evaluate("Math.min(...window.CurlingEngine.activeStones.map(s => s.variedPower))").AsNumber();
            var maxPower = engine.Evaluate("Math.max(...window.CurlingEngine.activeStones.map(s => s.variedPower))").AsNumber();
            Assert.Equal(76.25, minPower);
            Assert.Equal(76.25, maxPower);

            // Same dropToken must yield identical lateral dispersion across overlay instances (seeded PRNG)
            DispatchWsMessage(engine, new { type = "curling_slide", dropToken = "same-token", username = "a", power = 75.0, effectivePower = 75.0, powerVariancePercent = 4.0, condition = "Normal" });
            DispatchWsMessage(engine, new { type = "curling_slide", dropToken = "same-token", username = "b", power = 75.0, effectivePower = 75.0, powerVariancePercent = 4.0, condition = "Normal" });
            var sameV = engine.Evaluate("(function(){var a=window.CurlingEngine.activeStones.filter(s=>s.dropToken==='same-token');return a[0].v===a[1].v && a[0].speedV===a[1].speedV && a[0].drag===a[1].drag;})()").AsBoolean();
            Assert.True(sameV);

            // Different shots (different dropTokens) with the exact same power must still disperse differently
            var distinct = engine.Evaluate("(function(){var a=window.CurlingEngine.activeStones.filter(s=>s.dropToken.indexOf('var-stone-')===0);var seen={};a.forEach(s=>{seen[s.v.toFixed(6)+'|'+s.speedV.toFixed(6)+'|'+s.drag.toFixed(6)]=1;});return Object.keys(seen).length;})()").AsNumber();
            Assert.Equal(5, distinct);

            // Check that curlDirection was assigned (+1 or -1) for varied stones
            var curlDirs = engine.Evaluate("window.CurlingEngine.activeStones.map(s => Math.abs(s.curlDirection))").AsArray();
            Assert.All(curlDirs, d => Assert.Equal(1, d.AsNumber()));

            // 3. Dispatch guaranteed button test shot (powerVariancePercent = 0.0)
            DispatchWsMessage(engine, new
            {
                type = "curling_slide",
                dropToken = "guaranteed-stone-1",
                username = "admin_test",
                power = 78.5,
                powerVariancePercent = 0.0,
                condition = "Normal"
            });

            var guaranteedStone = engine.Evaluate("window.CurlingEngine.activeStones.find(s => s.dropToken === 'guaranteed-stone-1')");
            Assert.NotNull(guaranteedStone);

            // Guaranteed stone must have exact power, zero lateral spawn offset, zero curl direction, and zero lateral speed
            var guaranteedVariedPower = engine.Evaluate("window.CurlingEngine.activeStones.find(s => s.dropToken === 'guaranteed-stone-1').variedPower").AsNumber();
            var guaranteedOffsetV = engine.Evaluate("window.CurlingEngine.activeStones.find(s => s.dropToken === 'guaranteed-stone-1').v").AsNumber();
            var guaranteedCurlDir = engine.Evaluate("window.CurlingEngine.activeStones.find(s => s.dropToken === 'guaranteed-stone-1').curlDirection").AsNumber();
            var guaranteedSpeedV = engine.Evaluate("window.CurlingEngine.activeStones.find(s => s.dropToken === 'guaranteed-stone-1').speedV").AsNumber();

            Assert.Equal(78.5, guaranteedVariedPower);
            Assert.Equal(0.0, guaranteedOffsetV);
            Assert.Equal(0, guaranteedCurlDir);
            Assert.Equal(0.0, guaranteedSpeedV);
        }


        [Fact]
        public void CurlingJs_ShuffleboardKitchenPenaltyAndSoundResolution_BehavesAsExpected()
        {
            var jsDir = FindWwwRootPath();
            var curlingJsPath = Path.Combine(jsDir, "curling.js");
            Assert.True(File.Exists(curlingJsPath), $"curling.js not found at {curlingJsPath}");
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => ExecuteScriptFile(engine, curlingJsPath));
            Assert.Null(ex);

            // Default configuration assertions
            var defaultKitchen = engine.Evaluate("window.CurlingEngine.boardConfig.enableShuffleboardKitchenPenalty").AsBoolean();
            var defaultVariance = engine.Evaluate("window.CurlingEngine.boardConfig.powerVariancePercent").AsNumber();
            Assert.False(defaultKitchen);
            Assert.Equal(7.0, defaultVariance);

            // Configure Shuffleboard mode with default kitchen penalty disabled
            engine.Execute(@"
                window.CurlingEngine.boardConfig.scoringType = 'Shuffleboard';
                window.CurlingEngine.boardConfig.enableShuffleboardKitchenPenalty = false;
                var mockMetrics = {
                    isReversed: false,
                    targetDistRatio: 0.84,
                    stoneRadius: 20
                };
                var stoneOvershot = {
                    u: 0.88,
                    v: 0.5,
                    isReversed: false
                };
                window.CurlingEngine.evaluateStoneOutcome(stoneOvershot, mockMetrics);
            ");

            var scoreWithoutPenalty = engine.Evaluate("stoneOvershot.score").AsNumber();
            var isWinWithoutPenalty = engine.Evaluate("stoneOvershot.isWin").AsBoolean();
            Assert.Equal(0, scoreWithoutPenalty);
            Assert.False(isWinWithoutPenalty);

            // Configure Shuffleboard mode with kitchen penalty enabled
            engine.Execute(@"
                window.CurlingEngine.boardConfig.enableShuffleboardKitchenPenalty = true;
                window.CurlingEngine.evaluateStoneOutcome(stoneOvershot, mockMetrics);
            ");

            var scoreWithPenalty = engine.Evaluate("stoneOvershot.score").AsNumber();
            Assert.Equal(-10, scoreWithPenalty);

            // Verify sound bundle parsing on session_start
            DispatchWsMessage(engine, new
            {
                type = "session_start",
                session = new { totalDurationSeconds = 45 },
                sounds = new
                {
                    curlingWin = "/audio/theme_win.mp3",
                    curlingScore = "/audio/theme_score.mp3",
                    curlingFoul = "/audio/theme_foul.mp3"
                }
            });

            var foulSound = engine.Evaluate("window.CurlingEngine.sessionState.sounds.curlingFoul").AsString();
            var winSound = engine.Evaluate("window.CurlingEngine.sessionState.sounds.curlingWin").AsString();
            Assert.Equal("/audio/theme_foul.mp3", foulSound);
            Assert.Equal("/audio/theme_win.mp3", winSound);
        }


        [Fact]
        public void CurlingJs_StoneSpecificTargetDistanceAndButtonRadius_BehavesAsExpected()
        {
            var jsDir = FindWwwRootPath();
            var curlingJsPath = Path.Combine(jsDir, "curling.js");
            Assert.True(File.Exists(curlingJsPath), $"curling.js not found at {curlingJsPath}");

            var code = ReadScriptText(curlingJsPath);

            // 1. Verify button disc radius in drawTargetHouse is r * 0.32 matching scoring radius
            Assert.Contains("ctx.arc(cx, cy, r * 0.32, 0, Math.PI * 2);", code);
            Assert.DoesNotContain("ctx.arc(cx, cy, r * 0.42, 0, Math.PI * 2);", code);

            var engine = CreateMockBrowserEngine(out _);
            var ex = Record.Exception(() => ExecuteScriptFile(engine, curlingJsPath));
            Assert.Null(ex);

            // 2. Dispatch stone 1 with targetDistancePercent: 75.0
            DispatchWsMessage(engine, new
            {
                type = "curling_slide",
                dropToken = "stone-shot-1",
                username = "player1",
                power = 75.0,
                targetDistancePercent = 75.0
            });

            // Dispatch stone 2 with targetDistancePercent: 88.0 (mutates boardConfig)
            DispatchWsMessage(engine, new
            {
                type = "curling_slide",
                dropToken = "stone-shot-2",
                username = "player2",
                power = 88.0,
                targetDistancePercent = 88.0
            });

            // Verify each stone retained its own target distance
            var stone1Target = engine.Evaluate("window.CurlingEngine.activeStones.find(s => s.dropToken === 'stone-shot-1').targetDistancePercent").AsNumber();
            var stone2Target = engine.Evaluate("window.CurlingEngine.activeStones.find(s => s.dropToken === 'stone-shot-2').targetDistancePercent").AsNumber();
            Assert.Equal(75.0, stone1Target);
            Assert.Equal(88.0, stone2Target);

            // 3. Evaluate stone 1 when resting at u: 0.75 (exact center of its stone-specific target 75.0%)
            // Even though mock shared metrics has targetDistRatio: 0.88, stone 1 must score 100 bullseye!
            engine.Execute(@"
                window.CurlingEngine.boardConfig.scoringType = 'CurlingHouse';
                var stone1 = window.CurlingEngine.activeStones.find(s => s.dropToken === 'stone-shot-1');
                stone1.u = 0.75;
                stone1.v = 0.0;
                var sharedMetrics = {
                    x: 0,
                    y: 900,
                    w: 1920,
                    h: 180,
                    length: 1920,
                    breadth: 180,
                    isHorizontal: true,
                    targetDistRatio: 0.88,
                    effectiveTargetRatio: 0.88,
                    targetCenterX: 1920 * 0.88,
                    targetCenterY: 990,
                    stoneRadius: 25,
                    isReversed: false
                };
                window.CurlingEngine.evaluateStoneOutcome(stone1, sharedMetrics);
            ");

            var stone1Score = engine.Evaluate("stone1.score").AsNumber();
            var stone1Bullseye = engine.Evaluate("stone1.isBullseye").AsBoolean();
            Assert.Equal(100, stone1Score);
            Assert.True(stone1Bullseye);

            // 4. Verify fallback to shared metrics when a stone lacks targetDistRatio / targetDistancePercent
            engine.Execute(@"
                var legacyStone = {
                    u: 0.88,
                    v: 0.0,
                    isReversed: false
                };
                window.CurlingEngine.evaluateStoneOutcome(legacyStone, sharedMetrics);
            ");

            var legacyScore = engine.Evaluate("legacyStone.score").AsNumber();
            var legacyBullseye = engine.Evaluate("legacyStone.isBullseye").AsBoolean();
            Assert.Equal(100, legacyScore);
            Assert.True(legacyBullseye);
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

                var configuredBurst = [];
                DropGamesCommon.spawnSeasonalBurst(configuredBurst, { x: 500, y: 400, season: 'fall', type: 'leaves', count: 1, isLanding: true });
                var configuredType = configuredBurst[0].type;
                var fillsBeforeConfiguredBurst = window.__canvasFillCount || 0;
                DropGamesCommon.updateAndDrawParticles(ctx, configuredBurst, { dt: 0.016, width: 1920, height: 1080 });
                var configuredBurstDrawCount = (window.__canvasFillCount || 0) - fillsBeforeConfiguredBurst;

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
                    configuredType: configuredType,
                    configuredBurstDrawCount: configuredBurstDrawCount,
                    configuredBurstVy: configuredBurst[0].vy,
                    configuredBurstMaxLife: configuredBurst[0].maxLife,
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
            Assert.Equal("leaf", result.Get("configuredType").AsString());
            Assert.Equal(1, result.Get("configuredBurstDrawCount").AsNumber());
            Assert.True(result.Get("configuredBurstVy").AsNumber() < 0);
            Assert.True(result.Get("configuredBurstMaxLife").AsNumber() >= 1.0);
            Assert.Equal(0, result.Get("burstPxRemaining").AsNumber());
            Assert.Equal(0, result.Get("burstNormRemaining").AsNumber());
        }


        [Fact]
        public void DropGamesCommon_RenderLoop_SleepsWhenIdleAndWakesOnDemand()
        {
            var engine = CreateMockBrowserEngine(out _);
            var result = engine.Evaluate(@"
                var frameCount = 0;
                var keepRendering = false;
                var loop = DropGamesCommon.createRenderLoop(function() { frameCount++; }, function() { return keepRendering; });

                loop.wake();
                var firstFrame = window.__nextRaf;
                window.__nextRaf = null;
                firstFrame(1000);
                var cleanupFrame = window.__nextRaf;
                window.__nextRaf = null;
                cleanupFrame(1016);
                var stoppedWhenIdle = window.__nextRaf === null;

                keepRendering = true;
                loop.wake();
                var activeFrame = window.__nextRaf;
                window.__nextRaf = null;
                activeFrame(1032);
                var continuedWhileActive = window.__nextRaf !== null;

                keepRendering = false;
                var finalFrame = window.__nextRaf;
                window.__nextRaf = null;
                finalFrame(1048);
                var finalCleanupFrame = window.__nextRaf;
                window.__nextRaf = null;
                finalCleanupFrame(1064);

                ({ frameCount: frameCount, stoppedWhenIdle: stoppedWhenIdle, continuedWhileActive: continuedWhileActive, stoppedAfterActivity: window.__nextRaf === null });
            ");

            Assert.Equal(5, result.Get("frameCount").AsNumber());
            Assert.True(result.Get("stoppedWhenIdle").AsBoolean());
            Assert.True(result.Get("continuedWhileActive").AsBoolean());
            Assert.True(result.Get("stoppedAfterActivity").AsBoolean());
        }


        [Fact]
        public void GolfJs_LoadsAndExecutesShotSimulationWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var golfJsPath = Path.Combine(jsDir, "golf.js");
            Assert.True(File.Exists(golfJsPath), $"golf.js not found at {golfJsPath}");
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => ExecuteScriptFile(engine, golfJsPath));
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


        [Fact]
        public void GolfJs_LipOutCannotSinkOnFollowingFrame()
        {
            var jsDir = FindWwwRootPath();
            var golfJsPath = Path.Combine(jsDir, "golf.js");
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, golfJsPath);

            var result = engine.Evaluate(@"
                var metrics = window.GolfEngine.getTrackMetrics();
                var ball = {
                    dropToken: 'golf-lipout-regression',
                    u: metrics.effectiveTargetRatio,
                    v: 0,
                    speedU: 0.01,
                    speedV: 0,
                    angle: 0,
                    drag: 0.22,
                    heightZ: 0,
                    vz: 0,
                    gravityZ: 2.6,
                    bounceCount: 0,
                    maxBounces: 0,
                    radius: metrics.ballRadius,
                    sinkScale: 1,
                    isSunk: false,
                    isLipOut: true,
                    completed: false,
                    strokeNumber: 1,
                    strokePenaltyPoints: 10,
                    isReversed: false,
                    startAxisPosition: metrics.effectiveTargetRatio
                };
                window.GolfEngine.activeBalls.push(ball);
                window.GolfEngine.stepPhysics(0.016, metrics);
                ({ isSunk: ball.isSunk, completed: ball.completed });
            ");

            Assert.False(result.Get("isSunk").AsBoolean());
            Assert.False(result.Get("completed").AsBoolean());
        }


        [Fact]
        public void GolfJs_RightToLeftShot_StartsOnRightSide()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "golf.js"));

            DispatchWsMessage(engine, new
            {
                type = "golf_shot",
                dropToken = "golf-rtl-token",
                username = "golfer1",
                power = 70.0,
                stripPosition = "Bottom",
                slideDirection = "RightToLeft",
                isReversed = true,
                startAxisPosition = 0.05,
                powerVariancePercent = 0.0,
                targetDistancePercent = 84.0
            });

            var startU = engine.Evaluate("window.GolfEngine.activeBalls[0].u").AsNumber();
            Assert.Equal(0.95, startU, 3);
        }


        [Fact]
        public void GolfJs_MaxScoreScalesGreenAndStrokePenaltyFloorsAtZero()
        {
            var jsDir = FindWwwRootPath();
            var golfJsPath = Path.Combine(jsDir, "golf.js");
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, golfJsPath);

            var result = engine.Evaluate(@"
                var cup = window.GolfEngine.calculateGolfScore(0, 100, 10, true, 1, 10, 50);
                var midGreen = window.GolfEngine.calculateGolfScore(55, 100, 10, false, 1, 10, 50);
                var fringe = window.GolfEngine.calculateGolfScore(100, 100, 10, false, 1, 10, 50);
                var overPenalty = window.GolfEngine.calculateGolfScore(0, 100, 10, true, 6, 15, 50);
                ({ cup: cup, midGreen: midGreen, fringe: fringe, overPenalty: overPenalty });
            ");

            Assert.Equal(50, result.Get("cup").Get("baseScore").AsNumber());
            Assert.Equal(50, result.Get("cup").Get("score").AsNumber());
            Assert.Equal(26, result.Get("midGreen").Get("baseScore").AsNumber());
            Assert.Equal(1, result.Get("fringe").Get("baseScore").AsNumber());
            Assert.Equal(50, result.Get("overPenalty").Get("baseScore").AsNumber());
            Assert.Equal(0, result.Get("overPenalty").Get("score").AsNumber());
        }


        [Fact]
        public void GolfJs_LipOutRetainsEnoughMomentumToRollAwayFromCup()
        {
            var jsDir = FindWwwRootPath();
            var golfJsPath = Path.Combine(jsDir, "golf.js");
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, golfJsPath);

            var result = engine.Evaluate(@"
                var metrics = window.GolfEngine.getTrackMetrics();
                var ball = {
                    dropToken: 'golf-lipout-momentum',
                    u: metrics.effectiveTargetRatio - 17 / metrics.length,
                    v: 0,
                    speedU: 0.105,
                    speedV: 0,
                    angle: 0,
                    drag: 0.22,
                    heightZ: 0,
                    vz: 0,
                    gravityZ: 2.6,
                    bounceCount: 0,
                    maxBounces: 0,
                    radius: metrics.ballRadius,
                    sinkScale: 1,
                    isSunk: false,
                    isLipOut: false,
                    completed: false,
                    strokeNumber: 1,
                    strokePenaltyPoints: 10,
                    isReversed: false,
                    startAxisPosition: metrics.effectiveTargetRatio - 17 / metrics.length
                };
                window.GolfEngine.activeBalls.push(ball);
                for (var frame = 0; frame < 10 && !ball.isLipOut; frame++) {
                    window.GolfEngine.stepPhysics(0.016, metrics);
                }
                var lipOutU = ball.u;
                var lipOutV = ball.v;
                for (var settleFrame = 0; settleFrame < 120 && !ball.completed; settleFrame++) {
                    window.GolfEngine.stepPhysics(0.016, metrics);
                }
                ({ isLipOut: ball.isLipOut, isSunk: ball.isSunk, completed: ball.completed,
                   travelAfterLipOutPx: Math.hypot((ball.u - lipOutU) * metrics.length, (ball.v - lipOutV) * metrics.breadth),
                   minimumTravelPx: metrics.cupRadius * 1.5 });
            ");

            Assert.True(result.Get("isLipOut").AsBoolean());
            Assert.False(result.Get("isSunk").AsBoolean());
            Assert.True(result.Get("completed").AsBoolean());
            var travelAfterLipOut = result.Get("travelAfterLipOutPx").AsNumber();
            var minimumTravel = result.Get("minimumTravelPx").AsNumber();
            Assert.True(travelAfterLipOut >= minimumTravel,
                $"Lip-out traveled {travelAfterLipOut:F1}px; expected at least {minimumTravel:F1}px after rim contact.");
        }

    }
}
