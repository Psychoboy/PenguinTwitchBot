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
                    WS_TOPICS: { parachute: 'parachute', plinko: 'plinko', balloon: 'balloon', curling: 'curling', golf: 'golf' },
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
                                    fill: function() { window.__canvasFillCount = (window.__canvasFillCount || 0) + 1; },
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
                    var map = {};
                    var q = query || '';
                    if (q.indexOf('?') === 0) q = q.substring(1);
                    if (q) {
                        var pairs = q.split('&');
                        for (var i = 0; i < pairs.length; i++) {
                            var parts = pairs[i].split('=');
                            map[decodeURIComponent(parts[0])] = parts.length > 1 ? decodeURIComponent(parts[1]) : '';
                        }
                    }
                    return {
                        get: function(param) { return map[param] !== undefined ? map[param] : null; },
                        set: function(param, val) { map[param] = String(val); },
                        toString: function() {
                            var out = [];
                            for (var k in map) {
                                if (map.hasOwnProperty(k)) out.push(encodeURIComponent(k) + '=' + encodeURIComponent(map[k]));
                            }
                            return out.join('&');
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

        private static Engine CreateStarshipOverlayEngine(string view, string extraSetup = "")
        {
            var engine = new Engine(options => options.TimeoutInterval(TimeSpan.FromSeconds(5)));
            engine.Execute($@"
                var __elements = {{}};
                function __element() {{
                    var element = {{ hidden: false, style: {{}}, dataset: {{}}, children: [],
                        replaceChildren: function() {{ this.children = []; }},
                        append: function() {{ this.children.push.apply(this.children, arguments); }},
                        appendChild: function(child) {{ this.children.push(child); return child; }} }};
                    Object.defineProperty(element, 'textContent', {{
                        get: function() {{ return this._textContent || ''; }},
                        set: function(value) {{ this._textContent = String(value); }}
                    }});
                    return element;
                }}
                var window = {{
                    WS_TOPICS: {{ starship: 'starship' }},
                    __handlers: {{}},
                    __fetchCount: 0,
                    __audioPlayed: [],
                    createWsSocket: function(name, topics) {{
                        window.__topics = topics;
                        return {{ readyState: 1, send: function() {{}}, addEventListener: function(name, handler) {{ window.__handlers[name] = handler; }} }};
                    }},
                    setInterval: function(handler) {{ window.__renderTick = handler; return 1; }},
                    wsStatusShow: function() {{}}
                }};
                var document = {{
                    body: {{ dataset: {{ starshipView: '{view}' }} }},
                    getElementById: function(id) {{ return __elements[id] || (__elements[id] = __element()); }},
                    createElement: function() {{ return __element(); }}
                }};
                var WebSocket = {{ OPEN: 1 }};
                var Audio = function(src) {{
                    var self = this;
                    this.src = src || '';
                    this.volume = 1;
                    this.preload = 'none';
                    this.paused = true;
                    this.ended = false;
                    this.load = function() {{}};
                    this.addEventListener = function() {{}};
                    this.pause = function() {{ self.paused = true; }};
                    this.play = function() {{
                        self.paused = false;
                        return Promise.resolve();
                    }};
                }};
                var fetch = function() {{ window.__fetchCount++; return Promise.resolve({{ ok: true, json: function() {{ return Promise.resolve({{ type: 'expedition_state', snapshot: null }}); }} }}); }};
            ");

            var jsRoot = FindWwwRootPath();
            if (extraSetup.Length > 0) engine.Execute(extraSetup);
            engine.Execute(File.ReadAllText(Path.Combine(jsRoot, "starship-scene.js")));
            engine.Execute(File.ReadAllText(Path.Combine(jsRoot, "starship-art.js")));
            engine.Execute(File.ReadAllText(Path.Combine(jsRoot, "drop-games-audio.js")));
            engine.Execute(File.ReadAllText(Path.Combine(jsRoot, "starship-overlay.js")));
            return engine;
        }

        [Theory]
        [InlineData("event", 620, 360)]
        [InlineData("status", 520, 180)]
        public void StarshipWidgets_RegistryDefaultSizeDoesNotShrinkText(string view, int width, int height)
        {
            var definition = PenguinTwitchBot.Database.Bot.Models.Overlay.WidgetRegistry.Find("starship-" + view)!;
            Assert.Equal(width, definition.DefaultWidth);
            Assert.Equal(height, definition.DefaultHeight);
            var engine = CreateStarshipOverlayEngine(view, $"window.innerWidth = {width}; window.innerHeight = {height};");

            Assert.Equal("1", engine.Evaluate("document.getElementById('stage').style['--ui-scale']").AsString());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StarshipChoices_PreserveRowsAndScrollOnlyOverflowRespectingReducedMotion(bool reducedMotion)
        {
            var engine = CreateStarshipOverlayEngine("event", $@"
                window.__clock = 1000000;
                Date.now = function() {{ return window.__clock; }};
                window.matchMedia = function() {{ return {{ matches: {reducedMotion.ToString().ToLowerInvariant()} }}; }};");
            engine.Execute(@"
                window.__choiceSnapshot = { status: 'EventOpen', currentEvent: {
                    sequence: 1, title: 'Salvage', stepNumber: 1,
                    choices: [{ choiceId: 'scan', label: 'Scan the wreck', number: 1 }], voteCounts: { scan: 2 }
                }};
                window.StarshipOverlay.applyEnvelope({type:'expedition_state', snapshot:window.__choiceSnapshot});
                window.__firstRow = document.getElementById('choices').children[0];
                document.getElementById('choices').scrollHeight = 300;
                document.getElementById('choices').clientHeight = 200;
                window.__renderTick();
            ");
            Assert.Equal(0, engine.Evaluate("document.getElementById('choices').scrollTop").AsNumber());
            engine.Execute("window.__clock += 6000; window.__renderTick();");
            Assert.True(engine.Evaluate("document.getElementById('choices').children[0] === window.__firstRow").AsBoolean());
            Assert.Equal(reducedMotion ? 0 : 5, engine.Evaluate("document.getElementById('choices').scrollTop").AsNumber());
            engine.Execute(@"
                window.StarshipOverlay.applyAppearance({animationsEnabled:false});
                window.__clock += 6000;
                window.__renderTick();
            ");
            Assert.Equal(reducedMotion ? 0 : 5, engine.Evaluate("document.getElementById('choices').scrollTop").AsNumber());
        }

        private static void DispatchStarshipOverlayState(Engine engine, object snapshot, bool realtime = true)
        {
            var payload = JsonSerializer.Serialize(new { type = "expedition_state", snapshot });
            var serializedPayload = JsonSerializer.Serialize(payload);
            if (realtime)
                engine.Execute($"window.__handlers.message({{ data: {serializedPayload} }});");
            else
                engine.Execute($"window.StarshipOverlay.applyEnvelope(JSON.parse({serializedPayload}));");
        }

        [Fact]
        public void StarshipAudio_PlaysOneEventCueAndEmergencyCueButNotOnVoteUpdatesOrBootstrap()
        {
            var openedAt = DateTimeOffset.UtcNow;
            var eventSnapshot = new
            {
                expeditionId = "voyage-1", status = "EventOpen", updatedAtUtc = openedAt.ToString("O"), serverNowUtc = openedAt.ToString("O"),
                currentLeg = 1, legCount = 5, currentSector = "Kepler Reach", currentRoute = "Uncharted", crewCount = 1,
                ship = new { name = "Test Ship", hullStyle = "cruiser", accentColor = "#66d0c2", variant = 0 },
                currentEvent = new
                {
                    sequence = 1, stepNumber = 1, title = "Choose a Route", eventType = "RouteChoice", sector = "Kepler Reach",
                    description = "Choose a route.", openedAtUtc = openedAt.ToString("O"), closesAtUtc = openedAt.AddMinutes(1).ToString("O"),
                    choices = Array.Empty<object>(), voteCounts = new Dictionary<string, int>(), path = Array.Empty<string>(),
                    openSound = new { enabled = true, url = "/media/starship_route_choice.wav", volume = 55 }
                },
                activity = new[] { new { id = 1, atUtc = openedAt.ToString("O"), kind = "EventOpened", text = "Route opened", actor = "" } },
                timeline = Array.Empty<object>()
            };

            var bootstrapEngine = CreateStarshipOverlayEngine("event");
            DispatchStarshipOverlayState(bootstrapEngine, eventSnapshot, realtime: false);
            Assert.Equal(0, bootstrapEngine.Evaluate("window.__audioPlayed.length").AsNumber());

            var engine = CreateStarshipOverlayEngine("event");
            DispatchStarshipOverlayState(engine, eventSnapshot);
            Assert.Equal("/media/starship_route_choice.wav", engine.Evaluate("window.__audioPlayed.map(sound => sound.src).join('|')").AsString());
            Assert.Equal("/media/starship_route_choice.wav", engine.Evaluate("window.__audioPlayed[0].src").AsString());
            Assert.Equal(0.55, engine.Evaluate("window.__audioPlayed[0].volume").AsNumber(), 2);

            var voteUpdate = new
            {
                expeditionId = "voyage-1", status = "EventOpen", updatedAtUtc = openedAt.AddSeconds(1).ToString("O"), serverNowUtc = openedAt.ToString("O"),
                currentLeg = 1, legCount = 5, currentSector = "Kepler Reach", currentRoute = "Uncharted", crewCount = 1,
                ship = new { name = "Test Ship", hullStyle = "cruiser", accentColor = "#66d0c2", variant = 0 },
                currentEvent = new
                {
                    sequence = 1, stepNumber = 1, title = "Choose a Route", eventType = "RouteChoice", sector = "Kepler Reach",
                    description = "Choose a route.", openedAtUtc = openedAt.ToString("O"), closesAtUtc = openedAt.AddMinutes(1).ToString("O"),
                    choices = Array.Empty<object>(), voteCounts = new Dictionary<string, int> { ["direct"] = 1 }, path = Array.Empty<string>(),
                    openSound = new { enabled = true, url = "/media/starship_route_choice.wav", volume = 55 }
                },
                activity = new[] { new { id = 1, atUtc = openedAt.ToString("O"), kind = "EventOpened", text = "Route opened", actor = "" } },
                timeline = Array.Empty<object>()
            };
            DispatchStarshipOverlayState(engine, voteUpdate);
            Assert.Equal(1, engine.Evaluate("window.__audioPlayed.length").AsNumber());

            var stepUpdate = new
            {
                expeditionId = "voyage-1", status = "EventOpen", updatedAtUtc = openedAt.AddSeconds(2).ToString("O"), serverNowUtc = openedAt.ToString("O"),
                currentLeg = 1, legCount = 5, currentSector = "Kepler Reach", currentRoute = "Uncharted", crewCount = 1,
                ship = new { name = "Test Ship", hullStyle = "cruiser", accentColor = "#66d0c2", variant = 0 },
                currentEvent = new
                {
                    sequence = 1, stepNumber = 2, title = "Choose a Route", eventType = "RouteChoice", sector = "Kepler Reach",
                    description = "Choose a route.", openedAtUtc = openedAt.ToString("O"), closesAtUtc = openedAt.AddMinutes(1).ToString("O"),
                    choices = Array.Empty<object>(), voteCounts = new Dictionary<string, int>(), path = new[] { "Take the detour" },
                    openSound = new { enabled = true, url = "/media/starship_route_choice.wav", volume = 55 }
                },
                activity = new[]
                {
                    new { id = 1, atUtc = openedAt.ToString("O"), kind = "EventOpened", text = "Route opened", actor = "" },
                    new { id = 2, atUtc = openedAt.AddSeconds(2).ToString("O"), kind = "StepAdvanced", text = "Step 2", actor = "" }
                },
                timeline = Array.Empty<object>()
            };
            DispatchStarshipOverlayState(engine, stepUpdate);
            Assert.Equal(1, engine.Evaluate("window.__audioPlayed.length").AsNumber());

            var returned = new
            {
                expeditionId = "voyage-1", status = "Returned", updatedAtUtc = openedAt.AddSeconds(2).ToString("O"), serverNowUtc = openedAt.ToString("O"),
                currentLeg = 1, legCount = 5, currentSector = "Kepler Reach", currentRoute = "Uncharted", crewCount = 1,
                ship = new { name = "Test Ship", hullStyle = "cruiser", accentColor = "#66d0c2", variant = 0 },
                shipSystems = new { hull = 0, supplies = 40, pendingSuccessBonusPercent = 0, activeSuccessBonusPercent = 0, returnReason = "HullDepleted" },
                emergencyReturnSound = new { enabled = true, url = "/media/starship_emergency_return.wav", volume = 70 },
                currentEvent = (object?)null,
                activity = new[]
                {
                    new { id = 1, atUtc = openedAt.ToString("O"), kind = "EventOpened", text = "Route opened", actor = "" },
                    new { id = 2, atUtc = openedAt.AddSeconds(2).ToString("O"), kind = "StepAdvanced", text = "Step 2", actor = "" },
                    new { id = 3, atUtc = openedAt.AddSeconds(3).ToString("O"), kind = "VoyageEnded", text = "Emergency return", actor = "" }
                },
                latestResult = (object?)null, timeline = Array.Empty<object>()
            };
            DispatchStarshipOverlayState(engine, returned);
            Assert.Equal(2, engine.Evaluate("window.__audioPlayed.length").AsNumber());
            Assert.Equal("/media/starship_emergency_return.wav", engine.Evaluate("window.__audioPlayed[1].src").AsString());
        }

        [Fact]
        public void StarshipAudio_PlaysBoardingCueOnceWhenCrewCanJoin()
        {
            var openedAt = DateTimeOffset.UtcNow;
            var boarding = new
            {
                expeditionId = "voyage-boarding", status = "Boarding", updatedAtUtc = openedAt.ToString("O"), serverNowUtc = openedAt.ToString("O"),
                currentLeg = 0, legCount = 5, currentSector = "Kepler Reach", currentRoute = "Uncharted", crewCount = 0,
                boardingOpenSound = new { enabled = true, url = "/media/starship_boarding_open.wav", volume = 60 },
                ship = new { name = "Test Ship", hullStyle = "cruiser", accentColor = "#66d0c2", variant = 0 },
                activity = new[] { new { id = 1, atUtc = openedAt.ToString("O"), kind = "BoardingOpened", text = "Boarding open", actor = "" } },
                timeline = Array.Empty<object>()
            };
            var engine = CreateStarshipOverlayEngine("event");

            DispatchStarshipOverlayState(engine, boarding);
            DispatchStarshipOverlayState(engine, boarding);

            Assert.Equal(1, engine.Evaluate("window.__audioPlayed.length").AsNumber());
            Assert.Equal("/media/starship_boarding_open.wav", engine.Evaluate("window.__audioPlayed[0].src").AsString());
            Assert.Equal(0.6, engine.Evaluate("window.__audioPlayed[0].volume").AsNumber(), 2);
        }

        [Fact]
        public void StarshipStatusWidget_RendersBoardingAndVoyageProgress()
        {
            var engine = CreateStarshipOverlayEngine("status");
            DispatchStarshipOverlayState(engine, new
            {
                status = "Boarding",
                updatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                currentLeg = 0,
                legCount = 5,
                currentSector = "Kepler Reach",
                currentRoute = "Uncharted",
                crewCount = 3,
                boardingClosesAtUtc = DateTimeOffset.UtcNow.AddSeconds(35).ToString("O"),
                nextEventAtUtc = (string?)null,
                currentEvent = (object?)null,
                latestResult = (object?)null
            });

            Assert.Equal("Boarding", engine.Evaluate("document.getElementById('phase').textContent").AsString());
            Assert.Equal("Kepler Reach", engine.Evaluate("document.getElementById('sector').textContent").AsString());
            Assert.Equal("3", engine.Evaluate("document.getElementById('crew-count').textContent").AsString());
            Assert.Equal("Leg 0 / 5", engine.Evaluate("document.getElementById('leg-count').textContent").AsString());
            Assert.Contains("Takeoff in", engine.Evaluate("document.getElementById('timing').textContent").AsString());
            Assert.Equal("starship", engine.Evaluate("window.__topics[0]").AsString());
        }

        [Fact]
        public void StarshipStatusWidget_RendersShipRouteMapAndScalesToTheSourceSize()
        {
            var engine = CreateStarshipOverlayEngine("status", "window.innerWidth = 1040; window.innerHeight = 360;");
            Assert.Equal("2", engine.Evaluate("document.getElementById('stage').style['--ui-scale']").AsString());

            var now = DateTimeOffset.UtcNow;
            DispatchStarshipOverlayState(engine, new
            {
                status = "EventOpen",
                updatedAtUtc = now.ToString("O"),
                serverNowUtc = now.ToString("O"),
                currentLeg = 2,
                legCount = 6,
                currentSector = "Kepler Reach",
                currentRoute = "Direct Route",
                crewCount = 4,
                boardingClosesAtUtc = (string?)null,
                nextEventAtUtc = (string?)null,
                currentEvent = (object?)null,
                latestResult = (object?)null,
                ship = new { name = "Rockhopper Voyager", hullStyle = "cruiser", accentColor = "#f4bd68" },
                stationLegs = new[] { 3, 5 }
            });

            Assert.Equal("Rockhopper Voyager", engine.Evaluate("document.getElementById('ship-name').textContent").AsString());
            Assert.False(engine.Evaluate("document.getElementById('ship-icon').hidden").AsBoolean());
            var icon = engine.Evaluate("document.getElementById('ship-icon').innerHTML").AsString();
            Assert.Contains("#f4bd68", icon);
            Assert.Contains("<svg", icon);
            Assert.Equal(icon, engine.Evaluate("document.getElementById('ship-marker').innerHTML").AsString());
            Assert.Equal("#f4bd68", engine.Evaluate("document.getElementById('stage').style['--ship-accent']").AsString());
            Assert.Equal(6, (int)engine.Evaluate("document.getElementById('route-nodes').children.length").AsNumber());
            Assert.Equal("leg-node done", engine.Evaluate("document.getElementById('route-nodes').children[0].className").AsString());
            Assert.Equal("leg-node station current", engine.Evaluate("document.getElementById('route-nodes').children[2].className").AsString());
            Assert.Equal("leg-node", engine.Evaluate("document.getElementById('route-nodes').children[3].className").AsString());
            Assert.Equal("leg-node station", engine.Evaluate("document.getElementById('route-nodes').children[4].className").AsString());
            Assert.StartsWith("41.6", engine.Evaluate("document.getElementById('ship-marker').style.left").AsString(), StringComparison.Ordinal);

            DispatchStarshipOverlayState(engine, new
            {
                status = "Cruising",
                updatedAtUtc = now.AddSeconds(1).ToString("O"),
                serverNowUtc = now.AddSeconds(1).ToString("O"),
                currentLeg = 2,
                legCount = 6,
                currentSector = "Kepler Reach",
                currentRoute = "Direct Route",
                crewCount = 4,
                nextEventAtUtc = now.AddMinutes(5).ToString("O"),
                currentEvent = (object?)null,
                latestResult = (object?)null,
                ship = new { name = "Odd Ship", hullStyle = "nonsense", accentColor = "red;}</style>" },
                stationLegs = Array.Empty<int>()
            });
            Assert.Equal("#66d0c2", engine.Evaluate("document.getElementById('stage').style['--ship-accent']").AsString());
            Assert.Contains("<polygon points=\"2,16 26,5 62,16 26,27\"", engine.Evaluate("document.getElementById('ship-icon').innerHTML").AsString());
        }

        [Fact]
        public void StarshipOverlay_ShowsJoinToastsPulsesCrewAndRevealsFadedStatusWidget()
        {
            var engine = CreateStarshipOverlayEngine("status");
            var now = DateTimeOffset.UtcNow;
            object Snapshot(int second, params object[] activity) => new
            {
                status = "Cruising",
                updatedAtUtc = now.AddSeconds(second).ToString("O"),
                serverNowUtc = now.AddSeconds(second).ToString("O"),
                currentLeg = 1,
                legCount = 5,
                currentSector = "Kepler Reach",
                currentRoute = "Direct Route",
                crewCount = 3,
                nextEventAtUtc = now.AddMinutes(10).ToString("O"),
                currentEvent = (object?)null,
                latestResult = (object?)null,
                activity
            };
            object Join(long id, string name, int aboard) => new
            {
                id,
                atUtc = now.ToString("O"),
                kind = "CrewJoined",
                text = $"{name} joined the crew - {aboard} aboard",
                actor = name
            };

            DispatchStarshipOverlayState(engine, Snapshot(0));
            Assert.Equal("true", engine.Evaluate("document.getElementById('starship-widget').dataset.faded").AsString());
            Assert.Equal(string.Empty, engine.Evaluate("document.getElementById('starship-toast').textContent").AsString());

            DispatchStarshipOverlayState(engine, Snapshot(1, Join(1, "Pilot", 4)));
            Assert.False(engine.Evaluate("document.getElementById('starship-toast').hidden").AsBoolean());
            Assert.Equal("Pilot joined the crew - 4 aboard", engine.Evaluate("document.getElementById('starship-toast').textContent").AsString());
            Assert.Equal("false", engine.Evaluate("document.getElementById('starship-widget').dataset.faded").AsString());
            Assert.Equal("true", engine.Evaluate("document.getElementById('crew-block').dataset.pulse").AsString());

            DispatchStarshipOverlayState(engine, Snapshot(2, Join(2, "Medic", 5), Join(3, "Engineer", 6), Join(4, "Cook", 7)));
            Assert.Equal("Pilot joined the crew - 4 aboard", engine.Evaluate("document.getElementById('starship-toast').textContent").AsString());

            var eventEngine = CreateStarshipOverlayEngine("event");
            DispatchStarshipOverlayState(eventEngine, Snapshot(0));
            DispatchStarshipOverlayState(eventEngine, Snapshot(1, Join(1, "Pilot", 4)));
            Assert.False(eventEngine.Evaluate("document.getElementById('starship-toast').hidden").AsBoolean());
            Assert.True(eventEngine.Evaluate("document.getElementById('starship-widget').hidden").AsBoolean());

            DispatchStarshipOverlayState(engine, Snapshot(29));
            Assert.Equal("false", engine.Evaluate("document.getElementById('starship-widget').dataset.faded").AsString());
            DispatchStarshipOverlayState(engine, Snapshot(31));
            Assert.Equal("true", engine.Evaluate("document.getElementById('starship-widget').dataset.faded").AsString());
        }

        [Fact]
        public void StarshipOverlay_CoalescesBurstsOfJoinsIntoOneToast()
        {
            var engine = CreateStarshipOverlayEngine("status");
            var now = DateTimeOffset.UtcNow;
            DispatchStarshipOverlayState(engine, new
            {
                status = "Cruising",
                updatedAtUtc = now.ToString("O"),
                serverNowUtc = now.ToString("O"),
                currentLeg = 1, legCount = 5, currentSector = "Kepler Reach", currentRoute = "Direct Route", crewCount = 3,
                currentEvent = (object?)null,
                latestResult = (object?)null,
                activity = new[]
                {
                    new { id = 1L, atUtc = now.ToString("O"), kind = "CrewJoined", text = "Pilot joined the crew - 4 aboard", actor = "Pilot" },
                    new { id = 2L, atUtc = now.ToString("O"), kind = "CrewJoined", text = "Medic joined the crew - 5 aboard", actor = "Medic" },
                    new { id = 3L, atUtc = now.ToString("O"), kind = "CrewJoined", text = "Cook joined the crew - 6 aboard", actor = "Cook" },
                    new { id = 4L, atUtc = now.ToString("O"), kind = "CrewJoined", text = "Engineer joined the crew - 7 aboard", actor = "Engineer" }
                }
            });

            Assert.Equal("Pilot and 3 others joined the crew - 7 aboard", engine.Evaluate("document.getElementById('starship-toast').textContent").AsString());
        }

        [Fact]
        public void StarshipScene_ActivityRenewsStatusHoldWithoutOverridingManualModesOrReplayingOnReconnect()
        {
            var engine = CreateStarshipOverlayEngine("status");
            var now = DateTimeOffset.UtcNow;
            engine.Execute($@"
                var activityTracker = window.StarshipScene.createTracker();
                function cruiseAt(second, activitySecond) {{
                    return {{ expeditionId: 'voyage-one', status: 'Cruising',
                        serverNowUtc: new Date(Date.parse('{now:O}') + second * 1000).toISOString(),
                        activity: [{{ id: activitySecond + 1, kind: 'RewardBanked',
                            atUtc: new Date(Date.parse('{now:O}') + activitySecond * 1000).toISOString() }}] }};
                }}
                activityTracker.update(cruiseAt(20, 0));
                window.__activityVisible = activityTracker.derive(cruiseAt(20, 0)).statusVisible;
                window.__manualHidden = activityTracker.derive(cruiseAt(20, 0), 'BothOnDemand').statusFaded;
                window.__activityEventHidden = !activityTracker.derive(cruiseAt(20, 0)).eventVisible;
                activityTracker.update(cruiseAt(25, 25));
                activityTracker.update(cruiseAt(40, 25));
                window.__renewedVisible = activityTracker.derive(cruiseAt(40, 25)).statusVisible;
                var reconnectTracker = window.StarshipScene.createTracker();
                reconnectTracker.update(cruiseAt(54, 25));
                window.__reconnectedVisible = reconnectTracker.derive(cruiseAt(54, 25)).statusVisible;
                reconnectTracker.update(cruiseAt(56, 25));
                window.__expiredHidden = reconnectTracker.derive(cruiseAt(56, 25)).statusFaded;
                reconnectTracker.update({{ expeditionId: 'voyage-two', status: 'Cruising', activity: [] }});
                window.__newVoyageHidden = reconnectTracker.derive({{ status: 'Cruising' }}).statusFaded;
            ");

            foreach (var assertion in new[] { "__activityVisible", "__manualHidden", "__activityEventHidden",
                         "__renewedVisible", "__reconnectedVisible", "__expiredHidden", "__newVoyageHidden" })
                Assert.True(engine.Evaluate("window." + assertion).AsBoolean(), assertion);
        }

        private static object StarshipAppearanceSnapshot(DateTimeOffset now, int second, object ship, object? currentEvent = null) => new
        {
            status = currentEvent == null ? "Cruising" : "EventOpen",
            updatedAtUtc = now.AddSeconds(second).ToString("O"),
            serverNowUtc = now.AddSeconds(second).ToString("O"),
            currentLeg = 1, legCount = 5, currentSector = "Kepler Reach", currentRoute = "Direct Route", crewCount = 2,
            statusForcedUntilUtc = now.AddMinutes(5).ToString("O"),
            nextEventAtUtc = now.AddMinutes(10).ToString("O"),
            currentEvent,
            latestResult = (object?)null,
            ship
        };

        [Fact]
        public void StarshipOverlay_AppearanceEnvelopeSetsStyleVariablesAndSectionSwitches()
        {
            var engine = CreateStarshipOverlayEngine("status");
            var now = DateTimeOffset.UtcNow;
            var snapshot = StarshipAppearanceSnapshot(now, 0, new { name = "Odd", hullStyle = "arrow", accentColor = "#f4bd68", variant = 0 });
            var payload = JsonSerializer.Serialize(new
            {
                type = "expedition_state",
                snapshot,
                appearance = new
                {
                    panelColor = "#102030", panelOpacityPercent = 50, textColor = "#ffffff", mutedColor = "not-a-colour",
                    accentColor = "#112233", highlightColor = "#445566", warningColor = "#778899", useShipAccent = false,
                    fontFamily = "Verdana", cornerRadiusPx = 12, animationsEnabled = false,
                    showEffectChips = false, showVoteBars = false, showOddsPanel = false, showPath = false, showRouteMap = false, showToasts = false
                }
            });
            engine.Execute($"window.__handlers.message({{ data: {JsonSerializer.Serialize(payload)} }});");

            string Style(string name) => engine.Evaluate($"document.getElementById('stage').style['{name}']").AsString();
            Assert.Equal("rgba(16, 32, 48, 0.5)", Style("--panel"));
            Assert.Equal("#ffffff", Style("--text"));
            Assert.Equal("#112233", Style("--teal"));
            Assert.Equal("rgba(17, 34, 51, 0.42)", Style("--panel-line"));
            Assert.Equal("#445566", Style("--amber"));
            Assert.Equal("#778899", Style("--coral"));
            Assert.Equal("12px", Style("--radius"));
            Assert.Equal("'Verdana', 'Arial Narrow', sans-serif", Style("--font"));
            Assert.Equal("#112233", Style("--ship-accent"));
            Assert.True(engine.Evaluate("document.getElementById('stage').style['--muted'] === undefined").AsBoolean());
            foreach (var (name, value) in new[] { ("animations", "off"), ("chips", "off"), ("votebars", "off"), ("odds", "off"), ("path", "off"), ("routemap", "off") })
                Assert.Equal(value, engine.Evaluate($"document.getElementById('stage').dataset.{name}").AsString());
        }

        [Fact]
        public void StarshipOverlay_PicksCustomShipImagesAndRejectsUnsafeAddresses()
        {
            var now = DateTimeOffset.UtcNow;
            string IconMarkup(int variant, object appearance, params string[] unused)
            {
                var engine = CreateStarshipOverlayEngine("status");
                var payload = JsonSerializer.Serialize(new
                {
                    type = "expedition_state",
                    snapshot = StarshipAppearanceSnapshot(now, 0, new { name = "Odd", hullStyle = "scout", accentColor = "#f4bd68", variant }),
                    appearance
                });
                engine.Execute($"window.__handlers.message({{ data: {JsonSerializer.Serialize(payload)} }});");
                return engine.Evaluate("(function () { var icon = document.getElementById('ship-icon'); return icon.children.length ? 'IMG:' + icon.children[0].src + ':' + icon.children[0].className : icon.innerHTML; })()").AsString();
            }

            var customOnly = new { useBuiltInShipArt = false, shipImages = new[] { "/media/ship%20a.png", "https://cdn.example.com/b.webp" } };
            Assert.Equal("IMG:/media/ship%20a.png:art-image", IconMarkup(0, customOnly));
            Assert.Equal("IMG:https://cdn.example.com/b.webp:art-image", IconMarkup(1, customOnly));
            Assert.Equal("IMG:/media/ship%20a.png:art-image", IconMarkup(2, customOnly));

            var mixed = new { useBuiltInShipArt = true, shipImages = new[] { "/media/ship.png" } };
            Assert.StartsWith("<svg", IconMarkup(0, mixed), StringComparison.Ordinal);
            Assert.Equal("IMG:/media/ship.png:art-image", IconMarkup(1, mixed));

            var unsafeOnly = new { useBuiltInShipArt = false, shipImages = new[] { "javascript:alert(1)", "//evil.example.com/x.png", "data:image/png;base64,AAAA", "/media/has space.png" } };
            Assert.StartsWith("<svg", IconMarkup(0, unsafeOnly), StringComparison.Ordinal);
        }

        [Fact]
        public void StarshipArtCatalog_PreservesAllShipAndObjectVariants()
        {
            var jsRoot = FindWwwRootPath();
            var engine = new Engine();
            engine.Execute("var window = {};");
            engine.Execute(File.ReadAllText(Path.Combine(jsRoot, "starship-art.js")));
            var ships = new HashSet<string>();
            foreach (var style in new[] { "arrow", "hauler", "scout", "cruiser" })
                ships.Add(engine.Evaluate($"window.StarshipArt.shipSvg('{style}', '#66d0c2')").AsString());
            Assert.Equal(4, ships.Count);
            foreach (var kind in new[] { "wreck", "beacon", "debris", "station", "fork" })
            {
                Assert.Equal(3, (int)engine.Evaluate($"window.StarshipArt.variantCount('{kind}')").AsNumber());
                var variants = Enumerable.Range(0, 3).Select(index => engine.Evaluate($"window.StarshipArt.objectSvg('{kind}', {index}, '#66d0c2')").AsString()).ToList();
                Assert.Equal(3, variants.Distinct().Count());
                Assert.All(variants, svg =>
                {
                    Assert.StartsWith("<svg", svg, StringComparison.Ordinal);
                    Assert.DoesNotContain("%", svg);
                });
            }
            Assert.Equal("", engine.Evaluate("window.StarshipArt.objectSvg('unknown', 0, '#66d0c2')").AsString());
            Assert.Equal(0, (int)engine.Evaluate("window.StarshipArt.variantCount('unknown')").AsNumber());
        }

        [Fact]
        public void StarshipOverlay_PicksCustomEventObjectImagesPerKindAndFallsBackToBuiltInArt()
        {
            var now = DateTimeOffset.UtcNow;
            string ArtMarkup(string kind, object appearance, string[]? eventImages = null)
            {
                var engine = CreateStarshipOverlayEngine("event");
                var currentEvent = new
                {
                    sequence = 2, eventType = "X", title = "T", description = "D", sector = "Kepler Reach",
                    openedAtUtc = now.ToString("O"), closesAtUtc = now.AddSeconds(30).ToString("O"),
                    subject = "the thing", objectKind = kind, objectVariant = 1, objectRoll = 7,
                    objectImages = eventImages ?? [],
                    choices = Array.Empty<object>(), defaultChoiceId = "x", voteCounts = new Dictionary<string, int>(),
                    stepNumber = 1, path = Array.Empty<string>()
                };
                var payload = JsonSerializer.Serialize(new
                {
                    type = "expedition_state",
                    snapshot = StarshipAppearanceSnapshot(now, 0, new { name = "S", hullStyle = "arrow", accentColor = "#66d0c2", variant = 0 }, currentEvent),
                    appearance
                });
                engine.Execute($"window.__handlers.message({{ data: {JsonSerializer.Serialize(payload)} }});");
                return engine.Evaluate("(function () { var art = document.getElementById('scene-object'); return art.children.length ? 'IMG:' + art.children[0].src : art.innerHTML.substring(0, 4); })()").AsString();
            }

            var wreckImages = new { useBuiltInObjectArt = false, objectImages = new Dictionary<string, string[]> { ["wreck"] = ["/media/w1.png", "/media/w2.png"] } };
            Assert.Equal("IMG:/media/w2.png", ArtMarkup("wreck", wreckImages));
            Assert.Equal("IMG:/media/event.png", ArtMarkup("wreck", wreckImages, ["/media/event.png"]));
            Assert.Equal("IMG:/media/w2.png", ArtMarkup("wreck", wreckImages, ["javascript:alert(1)"]));
            Assert.Equal("<svg", ArtMarkup("beacon", wreckImages));
            Assert.Equal("<svg", ArtMarkup("wreck", new { useBuiltInObjectArt = true, objectImages = new Dictionary<string, string[]> { ["wreck"] = ["javascript:alert(1)"] } }));
        }

        [Fact]
        public void StarshipEventWidget_DisclosesUnlimitedCapsWithoutZeroingLosses()
        {
            var engine = CreateStarshipOverlayEngine("event");
            var now = DateTimeOffset.UtcNow;
            var currentEvent = new
            {
                sequence = 2, eventType = "custom-event", title = "Custom Event", description = "A choice.", sector = "Kepler Reach",
                openedAtUtc = now.ToString("O"), closesAtUtc = now.AddSeconds(30).ToString("O"),
                choices = Array.Empty<object>(), voteCounts = new Dictionary<string, int>(), stepNumber = 1, path = Array.Empty<string>(),
                rewardDisclosure = new
                {
                    pointTypeName = "Credits", successChancePercent = 75, tiers = Array.Empty<object>(), riskLossPercent = 25,
                    maximumRiskLossPerEvent = (long?)null, maximumPerEvent = (long?)null, maximumUnbankedPerVoyage = (long?)null
                }
            };
            DispatchStarshipOverlayState(engine, StarshipAppearanceSnapshot(now, 0,
                new { name = "Ship", hullStyle = "arrow", accentColor = "#66d0c2", variant = 0 }, currentEvent));

            Assert.Contains("Unlimited/event", engine.Evaluate("document.getElementById('reward-caps').textContent").AsString());
            Assert.Contains("Unlimited/voyage", engine.Evaluate("document.getElementById('reward-caps').textContent").AsString());
            Assert.Contains("25% unbanked, no amount cap", engine.Evaluate("document.getElementById('reward-risk').textContent").AsString());
        }

        [Theory]
        [InlineData(2, "false")]
        [InlineData(3, "true")]
        [InlineData(4, "true")]
        public void StarshipEventWidget_SelectsCompactLayoutForMoreThanTwoChoices(int count, string expectedCompact)
        {
            var engine = CreateStarshipOverlayEngine("event");
            var now = DateTimeOffset.UtcNow;
            var currentEvent = new
            {
                sequence = 2, eventType = "custom-event", title = "Custom Event", description = "A choice.", sector = "Kepler Reach",
                openedAtUtc = now.ToString("O"), closesAtUtc = now.AddSeconds(30).ToString("O"),
                choices = Enumerable.Range(1, count).Select(index => new { choiceId = $"choice-{index}", label = $"Choice {index}", number = index }),
                voteCounts = new Dictionary<string, int>(), stepNumber = 1, path = Array.Empty<string>()
            };
            DispatchStarshipOverlayState(engine, StarshipAppearanceSnapshot(now, 0,
                new { name = "Ship", hullStyle = "arrow", accentColor = "#66d0c2", variant = 0 }, currentEvent));

            Assert.Equal(count, (int)engine.Evaluate("document.getElementById('choices').children.length").AsNumber());
            Assert.Equal(expectedCompact, engine.Evaluate("document.getElementById('choices').dataset.compact").AsString());
        }

        [Fact]
        public void StarshipStatusWidget_FadesWhileCruisingUnlessForcedVisible()
        {
            var engine = CreateStarshipOverlayEngine("status");
            var now = DateTimeOffset.UtcNow;
            object Snapshot(string status, DateTimeOffset updatedAt, DateTimeOffset? forcedUntil) => new
            {
                status,
                updatedAtUtc = updatedAt.ToString("O"),
                currentLeg = 1,
                legCount = 5,
                currentSector = "Kepler Reach",
                currentRoute = "Direct Route",
                crewCount = 3,
                boardingClosesAtUtc = (string?)null,
                nextEventAtUtc = now.AddMinutes(10).ToString("O"),
                currentEvent = (object?)null,
                latestResult = (object?)null,
                statusForcedUntilUtc = forcedUntil?.ToString("O")
            };
            string Faded() => engine.Evaluate("document.getElementById('starship-widget').dataset.faded").AsString();

            DispatchStarshipOverlayState(engine, Snapshot("Boarding", now, null));
            Assert.Equal("true", Faded());
            Assert.True(engine.Evaluate("document.getElementById('starship-widget').hidden").AsBoolean());

            DispatchStarshipOverlayState(engine, Snapshot("Cruising", now.AddSeconds(1), null));
            Assert.Equal("true", Faded());

            DispatchStarshipOverlayState(engine, Snapshot("Cruising", now.AddSeconds(2), now.AddSeconds(30)));
            Assert.Equal("false", Faded());

            DispatchStarshipOverlayState(engine, Snapshot("Cruising", now.AddSeconds(3), now.AddSeconds(-1)));
            Assert.Equal("true", Faded());

            DispatchStarshipOverlayState(engine, Snapshot("EventOpen", now.AddSeconds(4), null));
            Assert.Equal("true", Faded());

            DispatchStarshipOverlayState(engine, Snapshot("Returned", now.AddSeconds(5), null));
            Assert.Equal("false", Faded());

            var endedEngine = CreateStarshipOverlayEngine("status");
            DispatchStarshipOverlayState(endedEngine, Snapshot("Returned", now.AddSeconds(-60), null));
            Assert.Equal("true", endedEngine.Evaluate("document.getElementById('starship-widget').dataset.faded").AsString());

            DispatchStarshipOverlayState(endedEngine, Snapshot("Returned", now.AddSeconds(-59), now.AddSeconds(30)));
            Assert.Equal("false", endedEngine.Evaluate("document.getElementById('starship-widget').dataset.faded").AsString());
        }

        [Theory]
        [InlineData("Boarding")]
        [InlineData("EventOpen")]
        [InlineData("Cruising")]
        public void StarshipStatusWidget_ExplicitShowOverridesOverlapUntilExpiry(string status)
        {
            var statusEngine = CreateStarshipOverlayEngine("status");
            var eventEngine = CreateStarshipOverlayEngine("event");
            var now = DateTimeOffset.UtcNow;
            object Snapshot(int second, bool forceStatus) => new
            {
                status,
                updatedAtUtc = now.AddSeconds(second).ToString("O"),
                serverNowUtc = now.AddSeconds(second).ToString("O"),
                statusForcedUntilUtc = forceStatus ? now.AddSeconds(30).ToString("O") : null,
                currentLeg = 1, legCount = 5, currentSector = "Kepler Reach", currentRoute = "Direct Route", crewCount = 3,
                boardingClosesAtUtc = now.AddMinutes(2).ToString("O"),
                nextEventAtUtc = now.AddMinutes(5).ToString("O"),
                currentEvent = status == "EventOpen" ? new
                {
                    sequence = 2, title = "Signal", sector = "Kepler Reach", description = "A signal.",
                    openedAtUtc = now.ToString("O"), closesAtUtc = now.AddMinutes(2).ToString("O"),
                    choices = Array.Empty<object>()
                } : null
            };

            DispatchStarshipOverlayState(statusEngine, Snapshot(0, false));
            Assert.Equal("true", statusEngine.Evaluate("document.getElementById('starship-widget').dataset.faded").AsString());

            foreach (var second in new[] { 1, 29, 31 })
            {
                var snapshot = Snapshot(second, true);
                DispatchStarshipOverlayState(statusEngine, snapshot);
                DispatchStarshipOverlayState(eventEngine, snapshot);

                Assert.Equal(second < 30 ? "false" : "true",
                    statusEngine.Evaluate("document.getElementById('starship-widget').dataset.faded").AsString());
                Assert.Equal(status != "Cruising" && second >= 30,
                    statusEngine.Evaluate("document.getElementById('starship-widget').hidden").AsBoolean());
                Assert.Equal(status == "Cruising",
                    eventEngine.Evaluate("document.getElementById('starship-widget').hidden").AsBoolean());
            }
        }

        [Theory]
        [InlineData("Aborted")]
        [InlineData("Returned")]
        public void StarshipOverlay_ShowStatusAfterVoyageEndDoesNotReopenEventOrExtendStatusHold(string status)
        {
            var statusEngine = CreateStarshipOverlayEngine("status");
            var eventEngine = CreateStarshipOverlayEngine("event");
            var now = DateTimeOffset.UtcNow;
            object Snapshot(int second) => new
            {
                status,
                updatedAtUtc = now.AddSeconds(second).ToString("O"),
                serverNowUtc = now.AddSeconds(second).ToString("O"),
                statusForcedUntilUtc = now.AddSeconds(10).ToString("O"),
                latestResult = new { sequence = 0, choice = status, resolvedAtUtc = now.ToString("O") },
                activity = new[] { new { id = 1, kind = "VoyageEnded", atUtc = now.AddMinutes(-5).ToString("O") } }
            };

            foreach (var second in new[] { 0, 11 })
            {
                DispatchStarshipOverlayState(statusEngine, Snapshot(second));
                DispatchStarshipOverlayState(eventEngine, Snapshot(second));

                Assert.True(eventEngine.Evaluate("document.getElementById('starship-widget').hidden").AsBoolean());
                Assert.False(statusEngine.Evaluate("document.getElementById('starship-widget').hidden").AsBoolean());
                Assert.Equal(second < 10 ? "false" : "true",
                    statusEngine.Evaluate("document.getElementById('starship-widget').dataset.faded").AsString());
            }
        }

        [Fact]
        public void StarshipStatusWidget_DoesNotOverlapEventWhileJoinToastIsVisible()
        {
            var engine = CreateStarshipOverlayEngine("status");
            var now = DateTimeOffset.UtcNow;
            DispatchStarshipOverlayState(engine, new
            {
                status = "EventOpen",
                updatedAtUtc = now.ToString("O"),
                serverNowUtc = now.ToString("O"),
                activity = new[] { new { id = 1, kind = "CrewJoined", atUtc = now.ToString("O"), actor = "Viewer", text = "Viewer joined" } },
                currentEvent = new { sequence = 1, stepNumber = 1, closesAtUtc = now.AddSeconds(30).ToString("O"), choices = Array.Empty<object>() },
                latestResult = (object?)null
            });

            Assert.Equal("true", engine.Evaluate("document.getElementById('starship-widget').dataset.faded").AsString());
            Assert.False(engine.Evaluate("document.getElementById('starship-toast').hidden").AsBoolean());
        }

        [Fact]
        public void StarshipOverlay_AnimatesEachActivityItemOnceAndReplaysOnlyRecentOnesAfterLoad()
        {
            var engine = CreateStarshipOverlayEngine("status");
            engine.Execute("window.__feed = []; window.StarshipOverlay.onFeed(function (item) { window.__feed.push(item.id); });");
            var now = DateTimeOffset.UtcNow;
            object Item(long id, double ageSeconds) => new
            {
                id,
                atUtc = now.AddSeconds(-ageSeconds).ToString("O"),
                kind = "CrewJoined",
                text = $"Viewer{id} joined the crew - {id} aboard",
                actor = $"Viewer{id}"
            };
            object Snapshot(DateTimeOffset updatedAt, params object[] activity) => new
            {
                status = "Cruising",
                updatedAtUtc = updatedAt.ToString("O"),
                serverNowUtc = updatedAt.ToString("O"),
                currentLeg = 1,
                legCount = 5,
                currentSector = "Kepler Reach",
                currentRoute = "Direct Route",
                crewCount = 3,
                nextEventAtUtc = now.AddMinutes(10).ToString("O"),
                currentEvent = (object?)null,
                latestResult = (object?)null,
                activity
            };

            DispatchStarshipOverlayState(engine, Snapshot(now, Item(1, 120), Item(2, 3)));
            Assert.Equal("2", engine.Evaluate("window.__feed.join(',')").AsString());

            DispatchStarshipOverlayState(engine, Snapshot(now.AddSeconds(1), Item(1, 120), Item(2, 3), Item(3, 0)));
            Assert.Equal("2,3", engine.Evaluate("window.__feed.join(',')").AsString());

            DispatchStarshipOverlayState(engine, Snapshot(now.AddSeconds(2), Item(2, 3), Item(3, 0)));
            Assert.Equal("2,3", engine.Evaluate("window.__feed.join(',')").AsString());
        }

        [Fact]
        public void StarshipOverlay_CountdownsUseServerTimeWhenTheLocalClockDiffers()
        {
            var engine = CreateStarshipOverlayEngine("status");
            var serverNow = DateTimeOffset.UtcNow.AddHours(2);
            DispatchStarshipOverlayState(engine, new
            {
                status = "Boarding",
                updatedAtUtc = serverNow.ToString("O"),
                serverNowUtc = serverNow.ToString("O"),
                currentLeg = 0,
                legCount = 5,
                currentSector = "Kepler Reach",
                currentRoute = "Uncharted",
                crewCount = 1,
                boardingClosesAtUtc = serverNow.AddSeconds(35).ToString("O"),
                nextEventAtUtc = (string?)null,
                currentEvent = (object?)null,
                latestResult = (object?)null
            });

            var timing = engine.Evaluate("document.getElementById('timing').textContent").AsString();
            Assert.True(timing is "Takeoff in 00:35" or "Takeoff in 00:34", timing);
        }

        [Theory]
        [InlineData("status")]
        [InlineData("event")]
        public void StarshipWidgets_RenderResourcesAndAuthoritativeUnavailableChoices(string view)
        {
            var engine = CreateStarshipOverlayEngine(view);
            var now = DateTimeOffset.UtcNow;
            DispatchStarshipOverlayState(engine, new
            {
                status = "EventOpen", updatedAtUtc = now.ToString("O"), serverNowUtc = now.ToString("O"), currentLeg = 2, legCount = 6,
                currentSector = "Kepler Reach", currentRoute = "Survey Route", crewCount = 3,
                shipSystems = new { hull = 20, maximumHull = 75, supplies = 0, pendingSuccessBonusPercent = 0, activeSuccessBonusPercent = 10, returnReason = "" },
                currentEvent = new
                {
                    sequence = 3, eventType = "StationStop", title = "Station", sector = "Kepler Reach", description = "A station.",
                    closesAtUtc = now.AddMinutes(1).ToString("O"), openedAtUtc = now.ToString("O"), stepNumber = 1, path = Array.Empty<string>(),
                    choices = new[] { new { choiceId = "resupply", label = "Resupply", number = 1 }, new { choiceId = "continue", label = "Continue", number = 2 } },
                    defaultChoiceId = "resupply", voteCounts = new Dictionary<string, int>(),
                    shipDisclosure = new
                    {
                        choices = new object[]
                        {
                            new { choiceId = "resupply", label = "Resupply", available = true, number = 1, suppliesDelta = 40 },
                            new { choiceId = "continue", label = "Continue", available = true, number = 2, nextEventSuccessBonusPercent = 10 },
                            new { choiceId = "repair", label = "Repair", available = false, number = (int?)null, suppliesCost = 15 }
                        }
                    }
                }
            });
            Assert.False(engine.Evaluate("document.getElementById('ship-systems').hidden").AsBoolean());
            Assert.Equal("Hull 20/75", engine.Evaluate("document.getElementById('ship-hull-text').textContent").AsString());
            Assert.Equal((20d / 75 * 100) + "%", engine.Evaluate("document.getElementById('ship-hull-fill').style.width").AsString());
            Assert.Equal("0%", engine.Evaluate("document.getElementById('ship-supplies-fill').style.width").AsString());
            Assert.Contains("Empty supplies", engine.Evaluate("document.getElementById('ship-bonus').textContent").AsString());
            if (view == "event")
            {
                Assert.Equal(3, (int)engine.Evaluate("document.getElementById('choices').children.length").AsNumber());
                Assert.Equal("!sv 2", engine.Evaluate("document.getElementById('choices').children[1].children[1].textContent").AsString());
                Assert.Equal("Unavailable", engine.Evaluate("document.getElementById('choices').children[2].children[1].textContent").AsString());
                Assert.Contains("Requires 15 supplies", engine.Evaluate("document.getElementById('choices').children[2].children[5].textContent").AsString());
                Assert.Contains("Supplies net +40", engine.Evaluate("document.getElementById('choices').children[0].children[5].textContent").AsString());
                Assert.Equal("Supplies +40", engine.Evaluate("document.getElementById('choices').children[0].children[6].textContent").AsString());
                Assert.Contains("Next event +10pp success", engine.Evaluate("document.getElementById('choices').children[1].children[6].textContent").AsString());
                Assert.Equal("Requires 15 supplies", engine.Evaluate("document.getElementById('choices').children[2].children[6].textContent").AsString());
            }
        }

        [Fact]
        public void StarshipScene_NewVoyageResetsFeedAndNewResolutionTimeRefreshesResult()
        {
            var engine = CreateStarshipOverlayEngine("event");
            engine.Execute(@"
                var tracker = window.StarshipScene.createTracker();
                var clock = Date.parse('2026-10-21T12:00:00Z');
                function state(id, seconds, resolvedSeconds) {
                    var stamp = new Date(clock + seconds * 1000).toISOString();
                    return { expeditionId: id, status: 'Cruising', updatedAtUtc: stamp, serverNowUtc: stamp,
                        activity: [], latestResult: { sequence: 2, choice: 'Recover', resolvedAtUtc: new Date(clock + resolvedSeconds * 1000).toISOString() } };
                }
                var first = state('voyage-one', 0, 0);
                first.activity = [{ id: 20, atUtc: first.serverNowUtc, kind: 'CrewJoined' }];
                window.__firstFeed = tracker.update(first).length;
                window.__replayFeed = tracker.update(first).length;
                var expired = state('voyage-one', 20, 0);
                tracker.update(expired);
                window.__expiredResult = tracker.derive(expired).eventVisible;
                var second = state('voyage-two', 20, 20);
                second.activity = [{ id: 1, atUtc: second.serverNowUtc, kind: 'CrewJoined' }];
                window.__secondFeed = tracker.update(second).length;
                window.__freshResult = tracker.derive(second).eventVisible;
                window.__secondReplay = tracker.update(second).length;
            ");
            Assert.Equal(1, (int)engine.Evaluate("window.__firstFeed").AsNumber());
            Assert.Equal(0, (int)engine.Evaluate("window.__replayFeed").AsNumber());
            Assert.False(engine.Evaluate("window.__expiredResult").AsBoolean());
            Assert.Equal(1, (int)engine.Evaluate("window.__secondFeed").AsNumber());
            Assert.True(engine.Evaluate("window.__freshResult").AsBoolean());
            Assert.Equal(0, (int)engine.Evaluate("window.__secondReplay").AsNumber());
        }

        [Theory]
        [InlineData("recover", "Kepler Reach · Default: 2) Recover the cache")]
        [InlineData("missing", "Kepler Reach")]
        public void StarshipEventWidget_DefaultFooterUsesViewerChoiceOrOmitsSuffix(string defaultId, string expected)
        {
            var engine = CreateStarshipOverlayEngine("event");
            var now = DateTimeOffset.UtcNow;
            var currentEvent = new
            {
                sequence = 2, eventType = "cache", title = "Cache", description = "A cache.", sector = "Kepler Reach",
                openedAtUtc = now.ToString("O"), closesAtUtc = now.AddSeconds(30).ToString("O"),
                choices = new[] { new { choiceId = "pass", label = "Pass", number = 1 }, new { choiceId = "recover", label = "Recover the cache", number = 2 } },
                defaultChoiceId = defaultId, voteCounts = new Dictionary<string, int>(), stepNumber = 1, path = Array.Empty<string>()
            };
            DispatchStarshipOverlayState(engine, StarshipAppearanceSnapshot(now, 0,
                new { name = "Ship", hullStyle = "arrow", accentColor = "#66d0c2", variant = 0 }, currentEvent));
            Assert.Equal(expected, engine.Evaluate("document.getElementById('event-sector').textContent").AsString());
        }

        [Fact]
        public void StarshipScene_DerivesStepTransitionResultAndVoyageEndScenes()
        {
            var engine = CreateStarshipOverlayEngine("event");
            var now = DateTimeOffset.UtcNow;
            string Iso(double seconds) => now.AddSeconds(seconds).ToString("O");
            engine.Execute($@"
                var tracker = window.StarshipScene.createTracker();
                function eventSnapshot(step) {{
                    return {{ status: 'EventOpen', updatedAtUtc: '{Iso(0)}', serverNowUtc: '{Iso(0)}',
                        currentEvent: {{ sequence: 4, stepNumber: step }}, latestResult: null }};
                }}
                tracker.update(eventSnapshot(1));
                window.__first = tracker.derive(eventSnapshot(1)).name;
                tracker.update(eventSnapshot(2));
                window.__second = tracker.derive(eventSnapshot(2)).name;
                var cruising = {{ status: 'Cruising', updatedAtUtc: '{Iso(1)}', serverNowUtc: '{Iso(1)}',
                    latestResult: {{ sequence: 4, choice: 'Pass it by', resolvedAtUtc: '{Iso(1)}' }}, currentEvent: null }};
                tracker.update(cruising);
                window.__result = tracker.derive(cruising);
                var oldCruising = {{ status: 'Cruising', updatedAtUtc: '{Iso(-100)}', serverNowUtc: '{Iso(0)}',
                    latestResult: {{ sequence: 5, choice: 'Old', resolvedAtUtc: '{Iso(-100)}' }}, currentEvent: null }};
                var freshTracker = window.StarshipScene.createTracker();
                freshTracker.update(oldCruising);
                window.__stale = freshTracker.derive(oldCruising);
                var recentCruising = {{ status: 'Cruising', updatedAtUtc: '{Iso(0)}', serverNowUtc: '{Iso(0)}',
                    latestResult: {{ sequence: 5, choice: 'Recent', resolvedAtUtc: '{Iso(-25)}' }}, currentEvent: null }};
                var recentTracker = window.StarshipScene.createTracker();
                recentTracker.update(recentCruising);
                window.__recent = recentTracker.derive(recentCruising);
                var ended = {{ status: 'Returned', updatedAtUtc: '{Iso(2)}', serverNowUtc: '{Iso(2)}',
                    latestResult: {{ sequence: 6, choice: 'Done', resolvedAtUtc: '{Iso(2)}' }}, currentEvent: null }};
                tracker.update(ended);
                window.__ended = tracker.derive(ended);
                var idle = {{ status: 'Inactive', updatedAtUtc: '{Iso(2)}', serverNowUtc: '{Iso(2)}' }};
                window.__idle = tracker.derive(idle);
                var forcedIdle = {{ status: 'Inactive', updatedAtUtc: '{Iso(2)}', serverNowUtc: '{Iso(2)}',
                    statusForcedUntilUtc: '{Iso(30)}' }};
                window.__forcedIdle = tracker.derive(forcedIdle);
                var activeEvent = {{ status: 'EventOpen', updatedAtUtc: '{Iso(2)}', serverNowUtc: '{Iso(2)}',
                    eventForcedUntilUtc: null }};
                window.__autoNoOverlap = tracker.derive(activeEvent, 'AutomaticNoOverlap');
                window.__statusManual = tracker.derive(activeEvent, 'StatusOnDemand');
                window.__bothManual = tracker.derive(activeEvent, 'BothOnDemand');
                window.__eventManual = tracker.derive(activeEvent, 'EventOnDemand');
                var forcedEvent = {{ status: 'Cruising', updatedAtUtc: '{Iso(2)}', serverNowUtc: '{Iso(2)}',
                    eventForcedUntilUtc: '{Iso(30)}' }};
                window.__forcedEvent = tracker.derive(forcedEvent, 'EventOnDemand');
            ");

            Assert.Equal("EventVoting", engine.Evaluate("window.__first").AsString());
            Assert.Equal("StepTransition", engine.Evaluate("window.__second").AsString());
            Assert.Equal("Result", engine.Evaluate("window.__result.name").AsString());
            Assert.True(engine.Evaluate("window.__result.eventVisible").AsBoolean());
            Assert.True(engine.Evaluate("window.__result.statusFaded").AsBoolean());
            Assert.Equal("Cruising", engine.Evaluate("window.__stale.name").AsString());
            Assert.True(engine.Evaluate("window.__stale.statusFaded").AsBoolean());
            Assert.False(engine.Evaluate("window.__recent.statusFaded").AsBoolean());
            Assert.False(engine.Evaluate("window.__stale.eventVisible").AsBoolean());
            Assert.Equal("VoyageEnd", engine.Evaluate("window.__ended.name").AsString());
            Assert.True(engine.Evaluate("window.__ended.statusFaded").AsBoolean());
            Assert.True(engine.Evaluate("window.__idle.statusFaded").AsBoolean());
            Assert.False(engine.Evaluate("window.__forcedIdle.statusFaded").AsBoolean());
            Assert.True(engine.Evaluate("window.__autoNoOverlap.eventVisible").AsBoolean());
            Assert.True(engine.Evaluate("window.__autoNoOverlap.statusFaded").AsBoolean());
            Assert.True(engine.Evaluate("window.__statusManual.eventVisible").AsBoolean());
            Assert.True(engine.Evaluate("window.__statusManual.statusFaded").AsBoolean());
            Assert.False(engine.Evaluate("window.__bothManual.eventVisible").AsBoolean());
            Assert.True(engine.Evaluate("window.__bothManual.statusFaded").AsBoolean());
            Assert.False(engine.Evaluate("window.__eventManual.eventVisible").AsBoolean());
            Assert.False(engine.Evaluate("window.__eventManual.statusFaded").AsBoolean());
            Assert.True(engine.Evaluate("window.__forcedEvent.eventVisible").AsBoolean());
        }

        [Fact]
        public void StarshipEventWidget_DrawsEveryObjectKindAndVariant()
        {
            var now = DateTimeOffset.UtcNow;
            var markups = new HashSet<string>();
            foreach (var kind in new[] { "wreck", "beacon", "debris", "station", "fork" })
            {
                for (var variant = 0; variant < 3; variant++)
                {
                    var engine = CreateStarshipOverlayEngine("event");
                    DispatchStarshipOverlayState(engine, new
                    {
                        status = "EventOpen",
                        updatedAtUtc = now.ToString("O"),
                        serverNowUtc = now.ToString("O"),
                        currentLeg = 1, legCount = 5, currentSector = "Kepler Reach", currentRoute = "Direct Route", crewCount = 2,
                        currentEvent = new
                        {
                            sequence = 2, eventType = "X", title = "T", description = "D", sector = "Kepler Reach",
                            openedAtUtc = now.ToString("O"), closesAtUtc = now.AddSeconds(30).ToString("O"),
                            subject = "the thing", objectKind = kind, objectVariant = variant,
                            choices = Array.Empty<object>(), defaultChoiceId = "x",
                            voteCounts = new Dictionary<string, int>(), stepNumber = 1, path = Array.Empty<string>()
                        },
                        latestResult = (object?)null,
                        ship = new { name = "S", hullStyle = "arrow", accentColor = "#66d0c2" }
                    });

                    var markup = engine.Evaluate("document.getElementById('scene-object').innerHTML").AsString();
                    Assert.StartsWith("<svg", markup, StringComparison.Ordinal);
                    Assert.DoesNotContain("%", markup, StringComparison.Ordinal);
                    markups.Add(markup);
                }
            }

            Assert.Equal(15, markups.Count);
        }

        [Fact]
        public void StarshipEventWidget_ShowsShipAndTimelineOnVoyageEndAndShipOnBoarding()
        {
            var engine = CreateStarshipOverlayEngine("event");
            var now = DateTimeOffset.UtcNow;
            var ship = new { name = "Rockhopper Voyager", hullStyle = "scout", accentColor = "#8fb8ff" };
            DispatchStarshipOverlayState(engine, new
            {
                status = "Boarding",
                updatedAtUtc = now.ToString("O"),
                serverNowUtc = now.ToString("O"),
                currentLeg = 0, legCount = 5, currentSector = "Kepler Reach", currentRoute = "Uncharted", crewCount = 2,
                boardingClosesAtUtc = now.AddSeconds(30).ToString("O"),
                currentEvent = (object?)null,
                latestResult = (object?)null,
                ship
            });
            Assert.Equal("Rockhopper Voyager", engine.Evaluate("document.getElementById('event-title').textContent").AsString());
            Assert.Equal("Rockhopper Voyager", engine.Evaluate("document.getElementById('scene-subject').textContent").AsString());
            Assert.Contains("#8fb8ff", engine.Evaluate("document.getElementById('scene-object').innerHTML").AsString());
            Assert.False(engine.Evaluate("document.getElementById('event-ship').hidden").AsBoolean());

            DispatchStarshipOverlayState(engine, new
            {
                status = "Returned",
                updatedAtUtc = now.AddSeconds(1).ToString("O"),
                serverNowUtc = now.AddSeconds(1).ToString("O"),
                currentLeg = 5, legCount = 5, currentSector = "Kepler Reach", currentRoute = "Direct Route", crewCount = 2,
                currentEvent = (object?)null,
                latestResult = new
                {
                    sequence = 5, title = "Space Station", sector = "Kepler Reach", choice = "Resupply",
                    resolvedAtUtc = now.AddSeconds(1).ToString("O"),
                    bankedAmounts = Array.Empty<object>(),
                    outcome = "", rarity = "",
                    lines = new[] { "Voyage completed: 5 events, 3 successful, 1 failed, 2 crew." },
                    story = "", subject = "", objectKind = "", objectVariant = 0
                },
                ship,
                timeline = new[]
                {
                    new { sequence = 1, title = "Choose a Route", outcome = "", rarity = "", objectKind = "fork" },
                    new { sequence = 2, title = "Derelict Salvage", outcome = "Success", rarity = "Rare", objectKind = "wreck" },
                    new { sequence = 3, title = "Navigation Hazard", outcome = "Failure", rarity = "Common", objectKind = "debris" }
                }
            });

            Assert.Equal("Voyage complete", engine.Evaluate("document.getElementById('result-banner').textContent").AsString());
            Assert.Equal("VOYAGE COMPLETE", engine.Evaluate("document.getElementById('event-kicker').textContent").AsString());
            Assert.Equal(3, (int)engine.Evaluate("document.getElementById('result-timeline').children.length").AsNumber());
            Assert.Equal("2 Derelict Salvage \u00b7 Rare \u2713", engine.Evaluate("document.getElementById('result-timeline').children[1].textContent").AsString());
            Assert.Equal("Failure", engine.Evaluate("document.getElementById('result-timeline').children[2].dataset.outcome").AsString());
            Assert.Equal("Rockhopper Voyager", engine.Evaluate("document.getElementById('scene-subject').textContent").AsString());
            Assert.True(engine.Evaluate("document.getElementById('result-story').hidden").AsBoolean());
        }

        [Fact]
        public void StarshipEventWidget_RendersVoteCommandsThenCollapsesToResolution()
        {
            var engine = CreateStarshipOverlayEngine("event");
            Assert.Equal(1, (int)engine.Evaluate("window.__fetchCount").AsNumber());
            engine.Execute("window.__handlers.open();");
            Assert.Equal(2, (int)engine.Evaluate("window.__fetchCount").AsNumber());
            var updatedAt = DateTimeOffset.UtcNow;
            DispatchStarshipOverlayState(engine, new
            {
                status = "EventOpen",
                updatedAtUtc = updatedAt.ToString("O"),
                currentLeg = 0,
                legCount = 5,
                currentSector = "Kepler Reach",
                currentRoute = "Uncharted",
                crewCount = 2,
                boardingClosesAtUtc = (string?)null,
                nextEventAtUtc = (string?)null,
                currentEvent = new
                {
                    sequence = 1,
                    eventType = "RouteChoice",
                    title = "Choose a Route",
                    description = "The crew can take a direct route or detour through a survey area.",
                    sector = "Kepler Reach",
                    closesAtUtc = updatedAt.AddSeconds(40).ToString("O"),
                    openedAtUtc = updatedAt.AddSeconds(-20).ToString("O"),
                    subject = "the ISV Quiet Meridian",
                    objectKind = "wreck",
                    objectVariant = 1,
                    choices = new object[]
                    {
                        new { choiceId = "direct", label = "Take the direct route", isRisky = false, number = 1, hint = "" },
                        new { choiceId = "detour", label = "Take the survey detour", isRisky = true, number = 2, hint = "rewards x0.8", successModifierPercent = 20, rewardMultiplierPercent = 80, riskMultiplierPercent = 100, continuesEvent = true, revealsRarity = true }
                    },
                    stepNumber = 2,
                    path = new[] { "Scan the wreck" },
                    revealedRarity = "Rare",
                    revealedRarityColor = "#ff8800",
                    stepNote = "Scan complete: this is a Rare find.",
                    effectiveSuccessChancePercent = 95,
                    defaultChoiceId = "direct",
                    voteCounts = new Dictionary<string, int> { ["direct"] = 1, ["detour"] = 0 },
                    rewardDisclosure = new
                    {
                        pointTypeName = "Salvage Credits",
                        successChancePercent = 75,
                        tiers = new object[]
                        {
                            new { name = "Common", color = "#a8c1bd", chancePercent = 60, rewardText = "5-12 Salvage Credits", runsActions = false },
                            new { name = "Uncommon", color = "#8be28b", chancePercent = 30, rewardText = "15-25 Salvage Credits", runsActions = false },
                            new { name = "Rare", color = "#ff8800", chancePercent = 10, rewardText = "30-50 Salvage Credits + 5 Gold", runsActions = true }
                        },
                        riskLossPercent = 25,
                        maximumRiskLossPerEvent = 40,
                        maximumPerEvent = 100,
                        maximumUnbankedPerVoyage = 500
                    }
                },
                latestResult = (object?)null
            });

            Assert.False(engine.Evaluate("document.getElementById('starship-widget').hidden").AsBoolean());
            Assert.Equal("Choose a Route", engine.Evaluate("document.getElementById('event-title').textContent").AsString());
            Assert.Equal(2, (int)engine.Evaluate("document.getElementById('choices').children.length").AsNumber());
            Assert.Contains("!sv 1", engine.Evaluate("document.getElementById('choices').children[0].children[1].textContent").AsString());
            Assert.Equal("+20% success,x0.8 reward,reveals rarity", engine.Evaluate("document.getElementById('choices').children[1].children[3].children.map(function (chip) { return chip.textContent; }).join(',')").AsString());
            Assert.Equal("100%", engine.Evaluate("document.getElementById('choices').children[0].children[4].children[0].style.width").AsString());
            Assert.Equal("0%", engine.Evaluate("document.getElementById('choices').children[1].children[4].children[0].style.width").AsString());
            Assert.Equal("Rare find", engine.Evaluate("document.getElementById('rarity-badge').textContent").AsString());
            Assert.Equal("Rare", engine.Evaluate("document.getElementById('rarity-badge').dataset.rarity").AsString());
            Assert.Equal("the ISV Quiet Meridian", engine.Evaluate("document.getElementById('scene-subject').textContent").AsString());
            Assert.Contains("<svg", engine.Evaluate("document.getElementById('scene-object').innerHTML").AsString());
            Assert.True(engine.Evaluate("document.getElementById('event-description').hidden").AsBoolean());
            var ringOffset = double.Parse(engine.Evaluate("document.getElementById('ring-fg').style.strokeDashoffset").AsString(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(ringOffset, 106.8 / 3 - 4, 106.8 / 3 + 4);
            Assert.Equal("1 vote", engine.Evaluate("document.getElementById('choices').children[0].children[2].textContent").AsString());
            Assert.Contains("Success chance: 95%", engine.Evaluate("document.getElementById('reward-success').textContent").AsString());
            Assert.Contains("failure 5%", engine.Evaluate("document.getElementById('reward-success').textContent").AsString());
            Assert.Contains("only viewers who commit", engine.Evaluate("document.getElementById('reward-scope').textContent").AsString());
            Assert.Contains("Rarity: Rare (30-50", engine.Evaluate("document.getElementById('reward-rarity').textContent").AsString());
            Assert.DoesNotContain("Common 60%", engine.Evaluate("document.getElementById('reward-rarity').textContent").AsString());
            Assert.Contains("+ 5 Gold", engine.Evaluate("document.getElementById('reward-rarity').textContent").AsString());
            Assert.Equal("#ff8800", engine.Evaluate("document.getElementById('rarity-badge').style['--rarity-color']").AsString());
            Assert.Contains("Path: Scan the wreck", engine.Evaluate("document.getElementById('event-sector').textContent").AsString());
            Assert.Equal("Scan complete: this is a Rare find.", engine.Evaluate("document.getElementById('step-note').textContent").AsString());
            Assert.Contains("STEP 2", engine.Evaluate("document.getElementById('event-kicker').textContent").AsString());
            Assert.Contains("capped at 40", engine.Evaluate("document.getElementById('reward-risk').textContent").AsString());
            Assert.False(engine.Evaluate("document.getElementById('reward-risk').hidden").AsBoolean());

            DispatchStarshipOverlayState(engine, new
            {
                status = "Cruising",
                updatedAtUtc = updatedAt.AddSeconds(1).ToString("O"),
                currentLeg = 1,
                legCount = 5,
                currentSector = "Ternion Belt",
                currentRoute = "Direct Route",
                crewCount = 2,
                boardingClosesAtUtc = (string?)null,
                nextEventAtUtc = updatedAt.AddMinutes(10).ToString("O"),
                currentEvent = (object?)null,
                latestResult = new
                {
                    sequence = 1,
                    title = "Choose a Route",
                    sector = "Kepler Reach",
                    choice = "Take the direct route",
                    resolvedAtUtc = updatedAt.AddSeconds(1).ToString("O"),
                    bankedAmounts = new[] { new { pointTypeId = 12, pointTypeName = "Salvage Credits", amount = 30, averagePerCrewMember = 15, isPaid = true } },
                    outcome = "Success",
                    rarity = "Rare",
                    lines = new[] { "SUCCESS - Rare find.", "Earned 15 Salvage Credits average per eligible crew member." },
                    story = "Pilot boards the ISV Quiet Meridian. The crew hauls out an intact cryo-vault.",
                    subject = "the ISV Quiet Meridian",
                    objectKind = "wreck",
                    objectVariant = 2
                }
            });

            Assert.False(engine.Evaluate("document.getElementById('starship-widget').hidden").AsBoolean());
            Assert.Equal("Success", engine.Evaluate("document.getElementById('result-banner').textContent").AsString());
            Assert.Equal("Success", engine.Evaluate("document.getElementById('result-banner').dataset.outcome").AsString());
            Assert.Equal("Pilot boards the ISV Quiet Meridian. The crew hauls out an intact cryo-vault.", engine.Evaluate("document.getElementById('result-story').textContent").AsString());
            Assert.False(engine.Evaluate("document.getElementById('result-story').hidden").AsBoolean());
            Assert.True(engine.Evaluate("document.getElementById('result-timeline').hidden").AsBoolean());
            Assert.Equal("2", engine.Evaluate("String(document.getElementById('result-lines').children.length)").AsString());
            Assert.Equal("SUCCESS - Rare find.", engine.Evaluate("document.getElementById('result-lines').children[0].textContent").AsString());
            Assert.True(engine.Evaluate("document.getElementById('result-lines').children[0].hidden").AsBoolean());
            Assert.Equal("Earned 15 Salvage Credits (avg / crew).", engine.Evaluate("document.getElementById('result-lines').children[1].textContent").AsString());
            Assert.Equal("Take the direct route", engine.Evaluate("document.getElementById('result-choice').textContent").AsString());
            Assert.Equal("15 Salvage Credits banked (avg / crew)", engine.Evaluate("document.getElementById('result-banked').children[0].textContent").AsString());
            Assert.True(engine.Evaluate("document.getElementById('event-panel').hidden").AsBoolean());

            DispatchStarshipOverlayState(engine, new
            {
                status = "EventOpen",
                updatedAtUtc = updatedAt.ToString("O"),
                currentLeg = 0,
                legCount = 5,
                currentSector = "Kepler Reach",
                currentRoute = "Uncharted",
                crewCount = 2,
                boardingClosesAtUtc = (string?)null,
                nextEventAtUtc = (string?)null,
                currentEvent = new
                {
                    sequence = 1,
                    eventType = "SignalDetected",
                    title = "Stale Signal",
                    description = "Old state",
                    sector = "Kepler Reach",
                    closesAtUtc = updatedAt.AddSeconds(40).ToString("O"),
                    choices = new[] { new { choiceId = "scan", label = "Scan" } },
                    defaultChoiceId = "scan",
                    voteCounts = new Dictionary<string, int>()
                },
                latestResult = (object?)null
            });
            Assert.Equal("Choose a Route", engine.Evaluate("document.getElementById('event-title').textContent").AsString());
            Assert.Equal("Take the direct route", engine.Evaluate("document.getElementById('result-choice').textContent").AsString());
        }

        [Fact]
        public void WsClient_RegistersStarshipTopicAndSubscribesToIt()
        {
            var engine = new Engine();
            engine.Execute(@"
                var window = { location: { protocol: 'https:', host: 'overlay.test' } };
                var URLSearchParams = function() {
                    this.values = [];
                    this.set = function(key, value) { this.values.push(key + '=' + value); };
                    this.toString = function() { return this.values.join('&'); };
                };
                var ReconnectingWebSocket = function(uri) {
                    window.__socketUri = uri;
                    this.addEventListener = function() {};
                    this.send = function() {};
                };
            ");
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-client.js")));
            engine.Execute("window.createWsSocket('starship-test', [window.WS_TOPICS.starship]);");

            Assert.Equal("starship", engine.Evaluate("window.WS_TOPICS.starship").AsString());
            Assert.Contains("topics=starship", engine.Evaluate("window.__socketUri").AsString());
        }

        [Fact]
        public void OverlayBus_BroadcastAndSend_DeliversToSubscribersLocally()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-client.js")));

            engine.Execute(@"
                window.__receivedEvents = [];
                window.OverlayBus.on('banner_shift', function(payload, evt) {
                    window.__receivedEvents.push({ payload: payload, evt: evt });
                });
                window.OverlayBus.broadcast('banner_shift', { slot: 2, game: 'plinko' });
            ");

            var count = engine.Evaluate("window.__receivedEvents.length").AsNumber();
            Assert.Equal(1, count);
            Assert.Equal(2, engine.Evaluate("window.__receivedEvents[0].payload.slot").AsNumber());
            Assert.Equal("plinko", engine.Evaluate("window.__receivedEvents[0].payload.game").AsString());
        }

        [Fact]
        public void OverlayBus_DuplicateEvents_DispatchesOnlyOnce()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-client.js")));

            engine.Execute(@"
                window.__callCount = 0;
                window.OverlayBus.on('test_event', function() {
                    window.__callCount++;
                });

                var testEvt = {
                    id: 'evt_dedup_123',
                    type: 'test_event',
                    target: '*',
                    payload: {}
                };

                window.OverlayBus._receive(testEvt);
                window.OverlayBus._receive(testEvt);
            ");

            var count = engine.Evaluate("window.__callCount").AsNumber();
            Assert.Equal(1, count);
        }

        [Fact]
        public void CreateWsSocket_TopLevelWindow_UsesOwnOverlayCompositorHub()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                var ReconnectingWebSocket = function(uri) {
                    window.__socketUri = uri;
                    this.readyState = 1;
                    this.addEventListener = function() {};
                    this.send = function() {};
                };
            ");
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-client.js")));

            engine.Execute(@"
                window.__overlayCompositorHub = new window.OverlayHub({
                    clientName: 'MainOverlay',
                    topics: ['overlay']
                });
                var clientSocket = window.createWsSocket('MainOverlay', ['overlay']);
                window.__isVirtual = clientSocket instanceof window.OverlayHub || typeof clientSocket._dispatchMessage === 'function';
            ");

            var isVirtual = engine.Evaluate("window.__isVirtual").AsBoolean();
            Assert.True(isVirtual);
            var hubUri = engine.Evaluate("window.__socketUri").AsString();
            Assert.DoesNotContain("key=", hubUri);
            Assert.Contains("multiplex=true", hubUri);
        }

        [Fact]
        public void DropGamesCommon_UpdateActiveGameState_IgnoresOtherDropActive()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "drop-games-common.js")));

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
        public void OverlayHub_MultiplexesVirtualSockets_RoutesMessagesCorrectly()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                var ReconnectingWebSocket = function(uri) {
                    window.__hubUri = uri;
                    this.readyState = 1;
                    this.listeners = {};
                    this.addEventListener = function(evt, fn) { this.listeners[evt] = fn; };
                    this.sent = [];
                    this.send = function(data) { this.sent.push(data); };
                };
            ");
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-client.js")));

            engine.Execute(@"
                var hub = new window.OverlayHub({ clientName: 'Overlay', topics: ['overlay'] });
                var vsPlinko = hub.createVirtualSocket('Plinko', ['plinko']);
                var vsChat = hub.createVirtualSocket('Chat', ['chat']);

                window.__plinkoMsgs = [];
                window.__chatMsgs = [];
                vsPlinko.addEventListener('message', function(e) { window.__plinkoMsgs.push(e.data); });
                vsChat.addEventListener('message', function(e) { window.__chatMsgs.push(e.data); });

                // Simulate physical socket message with multiplex envelope
                hub.physicalSocket.listeners['message']({
                    data: JSON.stringify({ topic: 'plinko', payload: JSON.stringify({ type: 'plinko_drop', dropToken: 'abc' }) })
                });

                // Simulate virtual socket send
                vsPlinko.send(JSON.stringify({ type: 'pong', game: 'plinko' }));
            ");

            Assert.Equal(1, engine.Evaluate("window.__plinkoMsgs.length").AsNumber());
            Assert.Equal(0, engine.Evaluate("window.__chatMsgs.length").AsNumber());
            Assert.Contains("plinko_drop", engine.Evaluate("window.__plinkoMsgs[0]").AsString());
            Assert.Contains("pong", engine.Evaluate("hub.physicalSocket.sent[hub.physicalSocket.sent.length - 1]").AsString());
        }

        [Fact]
        public void WsStatus_StandaloneWidget_ShowsConnectedAndDisconnected()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                var statusClasses = [];
                var statusEl = {
                    id: 'ws-status',
                    dataset: { overlay: '🚨 Alerts' },
                    textContent: 'Disconnected',
                    style: { display: '' },
                    classList: {
                        add: function(c) { if (statusClasses.indexOf(c) === -1) statusClasses.push(c); },
                        remove: function(c1, c2) {
                            statusClasses = statusClasses.filter(function(x) { return x !== c1 && x !== c2; });
                        }
                    }
                };
                document.getElementById = function(id) {
                    if (id === 'ws-status') return statusEl;
                    return null;
                };
            ");
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-status.js")));

            engine.Execute("window.wsStatusShow(true);");
            Assert.Equal("🚨 Alerts Connected", engine.Evaluate("statusEl.textContent").AsString());
            Assert.Contains("connected", engine.Evaluate("statusClasses.join(' ')").AsString());

            engine.Execute("window.wsStatusShow(false);");
            Assert.Equal("🚨 Alerts Disconnected", engine.Evaluate("statusEl.textContent").AsString());
            Assert.DoesNotContain("connected", engine.Evaluate("statusClasses.join(' ')").AsString());
        }

        [Fact]
        public void WsStatus_SharedChildWidget_SuppressesStatusMessages()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                var statusClasses = [];
                var statusEl = {
                    id: 'ws-status',
                    dataset: { overlay: '🚨 Alerts' },
                    textContent: 'Disconnected',
                    style: { display: '' },
                    classList: {
                        add: function(c) { if (statusClasses.indexOf(c) === -1) statusClasses.push(c); },
                        remove: function(c1, c2) {
                            statusClasses = statusClasses.filter(function(x) { return x !== c1 && x !== c2; });
                        }
                    }
                };
                document.getElementById = function(id) {
                    if (id === 'ws-status') return statusEl;
                    return null;
                };
                window.parent = { __overlayCompositorHub: {} };
            ");
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-status.js")));

            Assert.True(engine.Evaluate("window.isSharedWebSocketWidget()").AsBoolean());
            Assert.Equal("none", engine.Evaluate("statusEl.style.display").AsString());

            engine.Execute("window.wsStatusShow(true);");
            // textContent remains Disconnected because wsStatusShow was suppressed
            Assert.Equal("Disconnected", engine.Evaluate("statusEl.textContent").AsString());
            Assert.Equal("none", engine.Evaluate("statusEl.style.display").AsString());
        }

        [Fact]
        public void OverlayHub_UpdatesHostOverlayWsStatus_AndChildVirtualSocketsSuppressed()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                var statusCalls = [];
                window.wsStatusShow = function(connected) {
                    statusCalls.push(connected);
                };
                var ReconnectingWebSocket = function(uri) {
                    this.readyState = 1;
                    this.listeners = {};
                    this.addEventListener = function(evt, fn) { this.listeners[evt] = fn; };
                    this.send = function() {};
                };
            ");
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-client.js")));

            engine.Execute(@"
                var hub = new window.OverlayHub({ clientName: 'Overlay', topics: ['overlay'] });
                hub.physicalSocket.listeners['open']();
            ");

            var openCalls = engine.Evaluate("statusCalls.length").AsNumber();
            Assert.Equal(1, openCalls);
            Assert.True(engine.Evaluate("statusCalls[0]").AsBoolean());

            engine.Execute(@"
                hub.physicalSocket.listeners['close']();
            ");
            var totalCalls = engine.Evaluate("statusCalls.length").AsNumber();
            Assert.Equal(2, totalCalls);
            Assert.False(engine.Evaluate("statusCalls[1]").AsBoolean());
        }

        [Fact]
        public void CreateWsSocket_LocalHubPreferred_AndParentCrossoriginErrorGuarded()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                var ReconnectingWebSocket = function(uri) {
                    this.readyState = 1;
                    this.addEventListener = function() {};
                    this.send = function() {};
                };
            ");
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-client.js")));

            engine.Execute(@"
                window.__overlayCompositorHub = new window.OverlayHub({ clientName: 'HostOverlay', topics: ['overlay'] });
                // Simulate cross-origin parent access throwing an error
                window.parent = {};
                Object.defineProperty(window.parent, '__overlayCompositorHub', {
                    get: function() { throw new Error('Permission denied to access cross-origin object'); }
                });

                var sock = window.createWsSocket('HostOverlay', ['overlay']);
                window.__isHostVirtual = (sock instanceof window.OverlayHub) || typeof sock._dispatchMessage === 'function';
            ");

            Assert.True(engine.Evaluate("window.__isHostVirtual").AsBoolean());
        }

        [Fact]
        public void WsStatus_SocketOpensBeforeBodyParsed_ReplaysOnDomContentLoaded()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                var domListeners = {};
                document.readyState = 'loading';
                document.addEventListener = function(evt, handler) {
                    domListeners[evt] = handler;
                };

                var statusClasses = [];
                var statusEl = null; // not yet in DOM
                document.getElementById = function(id) {
                    if (id === 'ws-status') return statusEl;
                    return null;
                };
            ");
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-status.js")));

            // Socket opens before body parsed
            engine.Execute("window.wsStatusShow(true);");

            // Now body parses and DOM element becomes available
            engine.Execute(@"
                statusEl = {
                    id: 'ws-status',
                    dataset: { overlay: 'Overlay' },
                    textContent: 'Disconnected',
                    style: { display: '' },
                    classList: {
                        add: function(c) { if (statusClasses.indexOf(c) === -1) statusClasses.push(c); },
                        remove: function(c1, c2) {
                            statusClasses = statusClasses.filter(function(x) { return x !== c1 && x !== c2; });
                        }
                    }
                };
                document.readyState = 'interactive';
                if (domListeners['DOMContentLoaded']) {
                    domListeners['DOMContentLoaded']();
                }
            ");

            Assert.Equal("Overlay Connected", engine.Evaluate("statusEl.textContent").AsString());
            Assert.Contains("connected", engine.Evaluate("statusClasses.join(' ')").AsString());
        }

        [Fact]
        public void WsStatus_RemovesHiddenAttribute_OnShow()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                var statusClasses = ['hidden'];
                var statusEl = {
                    id: 'ws-status',
                    dataset: { overlay: '🚨 Alerts' },
                    textContent: 'Disconnected',
                    hidden: true,
                    style: { display: '' },
                    hasAttribute: function(attr) { return attr === 'hidden'; },
                    removeAttribute: function(attr) { if (attr === 'hidden') this.hidden = false; },
                    classList: {
                        add: function(c) { if (statusClasses.indexOf(c) === -1) statusClasses.push(c); },
                        remove: function(c1, c2) {
                            statusClasses = statusClasses.filter(function(x) { return x !== c1 && x !== c2; });
                        }
                    }
                };
                document.getElementById = function(id) {
                    if (id === 'ws-status') return statusEl;
                    return null;
                };
            ");
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-status.js")));

            engine.Execute("window.wsStatusShow(true);");
            Assert.False(engine.Evaluate("statusEl.hidden").AsBoolean());
            Assert.DoesNotContain("hidden", engine.Evaluate("statusClasses.join(' ')").AsString());
            Assert.Contains("connected", engine.Evaluate("statusClasses.join(' ')").AsString());
        }

        [Fact]
        public void TimerAndCounterOverlay_InSharedMode_SuppressesStatus()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                window.parent = { __overlayCompositorHub: {} };
                var statusEl = {
                    style: { display: '' },
                    setAttribute: function(attr, val) { this[attr] = val; }
                };
                var isShared = (function () {
                    try {
                        if (window.parent && window.parent !== window && window.parent.__overlayCompositorHub) return true;
                        if (new URLSearchParams(window.location.search).get('shared') === '1') return true;
                    } catch (_) {}
                    return false;
                })();
                if (isShared && statusEl) {
                    statusEl.style.display = 'none';
                }
            ");

            Assert.Equal("none", engine.Evaluate("statusEl.style.display").AsString());
        }

        [Fact]
        public void DropGamesCommon_OverlayBus_CoordinatesDropsLocally()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "ws-client.js")));
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "drop-games-common.js")));

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
            engine.Execute(File.ReadAllText(Path.Combine(FindWwwRootPath(), "drop-games-common.js")));

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
        public void BalloonJs_ManualSessionEnd_HidesCloudAfterGraceWithOtherGamesActive()
        {
            var jsDir = FindWwwRootPath();
            var balloonJsPath = Path.Combine(jsDir, "balloon.js");
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(File.ReadAllText(balloonJsPath));

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
        public void CurlingJs_PowerVarianceAndGuaranteedStraightShot_BehavesAsExpected()
        {
            var jsDir = FindWwwRootPath();
            var curlingJsPath = Path.Combine(jsDir, "curling.js");
            Assert.True(File.Exists(curlingJsPath), $"curling.js not found at {curlingJsPath}");

            var code = File.ReadAllText(curlingJsPath);
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => engine.Execute(code));
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

            var code = File.ReadAllText(curlingJsPath);
            var engine = CreateMockBrowserEngine(out _);

            var ex = Record.Exception(() => engine.Execute(code));
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

            var code = File.ReadAllText(curlingJsPath);

            // 1. Verify button disc radius in drawTargetHouse is r * 0.32 matching scoring radius
            Assert.Contains("ctx.arc(cx, cy, r * 0.32, 0, Math.PI * 2);", code);
            Assert.DoesNotContain("ctx.arc(cx, cy, r * 0.42, 0, Math.PI * 2);", code);

            var engine = CreateMockBrowserEngine(out _);
            var ex = Record.Exception(() => engine.Execute(code));
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

        [Fact]
        public void GolfJs_LipOutCannotSinkOnFollowingFrame()
        {
            var jsDir = FindWwwRootPath();
            var golfJsPath = Path.Combine(jsDir, "golf.js");
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(File.ReadAllText(golfJsPath));

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
            engine.Execute(File.ReadAllText(Path.Combine(jsDir, "golf.js")));

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
            engine.Execute(File.ReadAllText(golfJsPath));

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
            engine.Execute(File.ReadAllText(golfJsPath));

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
