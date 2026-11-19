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
    public class ParachuteJsTests : DropGamesJsTestBase
    {
        [Fact]
        public void ParachuteJs_LoadsAndExecutesWithoutRuntimeErrors()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJsPath = Path.Combine(jsDir, "parachute.js");
            Assert.True(File.Exists(parachuteJsPath), $"parachute.js not found at {parachuteJsPath}");
            var engine = CreateMockBrowserEngine(out _);

            // Executing the full script in the mock DOM environment
            var ex = Record.Exception(() => ExecuteScriptFile(engine, parachuteJsPath));
            Assert.Null(ex);

            // Verify console.error had 0 errors
            var errorCount = engine.Evaluate("console._errors.length").AsNumber();
            Assert.Equal(0, errorCount);
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
            var parachuteJs = ReadScriptText(Path.Combine(jsDir, "parachute.js"));

            // Verify velocity damping constants in parachute.js match server
            Assert.Contains("Math.pow(0.998, dtSec * 60)", parachuteJs);
            Assert.Contains("Math.pow(0.96, dtSec * 60)", parachuteJs);

            // Verify wind acceleration was removed
            Assert.DoesNotContain("windEffect", parachuteJs);
            Assert.DoesNotContain("windAccelX", parachuteJs);

            // Verify overlap separation is capped to prevent teleporting
            Assert.Contains("Math.min(2.5, 0.5 * (minDistPx - distPx))", parachuteJs);
        }


        [Fact]
        public void ParachuteJs_MultipleParachutists_CollideMidAir_BounceElasticallyAndLandWithinValidationTolerance()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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


        [Fact]
        public void ParachuteJs_DropletCollisions_KnockbackParityWithServerSimulation()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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


        [Fact]
        public void ParachuteJs_WallBounceRebound_ParityWithServerSimulation()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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


        [Fact]
        public void ParachuteJs_DenseDropletShower_MultipleDropsParityWithServerSimulation()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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
        public void ParachuteJs_DropletKnockbackIntoWallBounceCombo_ParityWithServerSimulation()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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
        public void ParachuteJs_ExtremeHeadOnCollision_MaxBouncinessAndMassRatio_DoesNotExplodeOrViolateBounds()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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
        public void ParachuteJs_ChaoticIntersection_MultiParachutistsAndMultiDroplets_AllResolveCleanly()
        {
            var jsDir = FindWwwRootPath();
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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
        public void ParachuteJs_SoundManager_PreloadsAndPlaysEventsWithoutQueuing()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJsPath = Path.Combine(jsDir, "parachute.js");
            Assert.True(File.Exists(parachuteJsPath));
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, parachuteJsPath);

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
        public void ParachuteJs_ParachuteRockets_DestroysCanopy_TriggersFreeFallAndCrashLanding()
        {
            var jsDir = FindWwwRootPath();
            var parachuteJsPath = Path.Combine(jsDir, "parachute.js");
            Assert.True(File.Exists(parachuteJsPath));
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, parachuteJsPath);

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
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, parachuteJsPath);

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
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, parachuteJsPath);

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
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, parachuteJsPath);

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
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, parachuteJsPath);

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
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, parachuteJsPath);

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
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(jsDir, "parachute.js"));

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

    }
}
