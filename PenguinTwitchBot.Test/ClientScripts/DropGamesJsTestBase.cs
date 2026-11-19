using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Acornima.Ast;
using Jint;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Services;

namespace PenguinTwitchBot.Test.ClientScripts
{
    public abstract class DropGamesJsTestBase
    {
        private const string MockDomScript = @"
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
                                setTransform: function() {},
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
                                setLineDash: function() {},
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

        private static readonly ConcurrentDictionary<string, string> ScriptTextCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Prepared<Script>> PreparedScripts = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Lazy<Prepared<Script>> PreparedMockDomScript = new(
            static () => Engine.PrepareScript(MockDomScript, "mock-dom.js"),
            LazyThreadSafetyMode.ExecutionAndPublication);

        protected static string ReadScriptText(string fullPath)
        {
            var normalizedPath = Path.GetFullPath(fullPath);
            return ScriptTextCache.GetOrAdd(normalizedPath, static path => File.ReadAllText(path));
        }

        protected static void ExecuteScriptFile(Engine engine, string fullPath)
        {
            var normalizedPath = Path.GetFullPath(fullPath);
            var prepared = PreparedScripts.GetOrAdd(
                normalizedPath,
                static path => Engine.PrepareScript(ReadScriptText(path), path));
            engine.Execute(in prepared);
        }

        protected static string FindWwwRootPath()
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

        protected static Engine CreateMockBrowserEngine(out Action<string> logCollector)
        {
            var engine = new Engine(options =>
            {
                options.Strict = false;
                options.TimeoutInterval(TimeSpan.FromSeconds(15));
            });

            var logs = new System.Collections.Generic.List<string>();
            logCollector = msg => logs.Add(msg);

            var mockDom = PreparedMockDomScript.Value;
            engine.Execute(in mockDom);

            var jsRoot = FindWwwRootPath();
            var commonJsPath = Path.Combine(jsRoot, "drop-games-common.js");
            if (File.Exists(commonJsPath))
            {
                ExecuteScriptFile(engine, commonJsPath);
                engine.Execute("var DropGamesCommon = window.DropGamesCommon;");
            }
            var audioJsPath = Path.Combine(jsRoot, "drop-games-audio.js");
            if (File.Exists(audioJsPath))
            {
                ExecuteScriptFile(engine, audioJsPath);
                engine.Execute("var DropGamesAudio = window.DropGamesAudio;");
            }
            return engine;
        }

        protected static Engine CreateStarshipOverlayEngine(string view, string extraSetup = "")
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
            ExecuteScriptFile(engine, Path.Combine(jsRoot, "starship-scene.js"));
            ExecuteScriptFile(engine, Path.Combine(jsRoot, "starship-art.js"));
            ExecuteScriptFile(engine, Path.Combine(jsRoot, "drop-games-audio.js"));
            ExecuteScriptFile(engine, Path.Combine(jsRoot, "starship-overlay.js"));
            return engine;
        }

        protected static void DispatchStarshipOverlayState(Engine engine, object snapshot, bool realtime = true)
        {
            var payload = JsonSerializer.Serialize(new { type = "expedition_state", snapshot });
            var serializedPayload = JsonSerializer.Serialize(payload);
            if (realtime)
                engine.Execute($"window.__handlers.message({{ data: {serializedPayload} }});");
            else
                engine.Execute($"window.StarshipOverlay.applyEnvelope(JSON.parse({serializedPayload}));");
        }

        protected static object StarshipAppearanceSnapshot(DateTimeOffset now, int second, object ship, object? currentEvent = null) => new
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

        protected static void StepEngineFrames(Engine engine, int frameCount, double dtMs = 16.666)
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

        protected static void DispatchWsMessage(Engine engine, object message)
        {
            var json = JsonSerializer.Serialize(message);
            engine.SetValue("__msgJson", json);
            engine.Execute("window.__wsMessageHandler({ data: __msgJson });");
        }

        protected static PendingDropRecord CreateTestDropRecord(
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
    }
}
