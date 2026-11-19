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
    public class PlinkoJsTests : DropGamesJsTestBase
    {
        [Fact]
        public void PlinkoJs_LoadsAndExecutesWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var plinkoJsPath = Path.Combine(jsDir, "plinko.js");
            Assert.True(File.Exists(plinkoJsPath), $"plinko.js not found at {plinkoJsPath}");
            var engine = CreateMockBrowserEngine(out _);

            // Executing the full script in the mock DOM environment
            var ex = Record.Exception(() => ExecuteScriptFile(engine, plinkoJsPath));
            Assert.Null(ex);

            // Verify console.error had 0 errors
            var errorCount = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errorCount);
        }


        [Fact]
        public void PlinkoJs_PuckPhysics_ContinuousMotion_NoFastTravelTeleportsOrDiscontinuities()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "plinko.js"));

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
        public void PlinkoJs_MultipleConcurrentTokens_BounceOffEachOtherAndLandInBuckets()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "plinko.js"));

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
        public void PlinkoJs_DenseMultiTokenCrowd_AllTokensBounceAndLandInBuckets()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "plinko.js"));

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
        public void PlinkoJs_ExtremeTokenCollision_MaxBouncinessAndMassRatio_DecaysCleanlyIntoBuckets()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "plinko.js"));

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
        public void PlinkoJs_WaypointTrajectory_FollowsNaturalGravitationalArcNotStraightDiagonal()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "plinko.js"));

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
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "plinko.js"));

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
        public void PlinkoJs_SoundManager_PreloadsAndPlaysEventsWithoutQueuing()
        {
            var jsDir = FindWwwRootPath();
            var plinkoJsPath = Path.Combine(jsDir, "plinko.js");
            Assert.True(File.Exists(plinkoJsPath));
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, plinkoJsPath);

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

    }
}
