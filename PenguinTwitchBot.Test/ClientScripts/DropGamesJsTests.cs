using System;
using System.IO;
using Jint;
using PenguinTwitchBot.Bot.Commands.Games.Parachute;
using PenguinTwitchBot.Bot.Commands.Games.Plinko;
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
                                    fillText: function() {},
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
    }
}
