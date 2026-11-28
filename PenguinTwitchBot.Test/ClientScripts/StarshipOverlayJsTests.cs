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
    public class StarshipOverlayJsTests : DropGamesJsTestBase
    {
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
            Assert.Equal(0.3025, engine.Evaluate("window.__audioPlayed[0].volume").AsNumber(), 4);

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
            Assert.Equal(0.36, engine.Evaluate("window.__audioPlayed[0].volume").AsNumber(), 4);
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
            ExecuteScriptFile(engine, Path.Combine(jsRoot, "starship-art.js"));
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

    }
}
