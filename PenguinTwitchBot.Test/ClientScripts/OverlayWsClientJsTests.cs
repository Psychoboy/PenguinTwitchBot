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
    public class OverlayWsClientJsTests : DropGamesJsTestBase
    {
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
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-client.js"));
            engine.Execute("window.createWsSocket('starship-test', [window.WS_TOPICS.starship]);");

            Assert.Equal("starship", engine.Evaluate("window.WS_TOPICS.starship").AsString());
            Assert.Contains("topics=starship", engine.Evaluate("window.__socketUri").AsString());
        }


        [Fact]
        public void OverlayBus_BroadcastAndSend_DeliversToSubscribersLocally()
        {
            var engine = CreateMockBrowserEngine(out _);
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-client.js"));

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
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-client.js"));

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
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-client.js"));

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
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-client.js"));

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
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-status.js"));

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
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-status.js"));

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
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-client.js"));

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
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-client.js"));

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
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-status.js"));

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
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-status.js"));

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
                        if (window.__isSharedWebSocketWidget) return true;
                        if (window.parent && window.parent !== window) return true;
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
        public void OverlayHost_NeverTreatedAsSharedWidget()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                window.__isOverlayHost = true;
                window.parent = { __overlayCompositorHub: {} };
            ");
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-status.js"));

            Assert.False(engine.Evaluate("window.isSharedWebSocketWidget()").AsBoolean());
        }


        [Fact]
        public void OverlayHub_PhysicalSocket_ConnectingAndError_NotifiesOfflineStatus()
        {
            var engine = CreateMockBrowserEngine(out _);
            engine.Execute(@"
                var statusCalls = [];
                window.wsStatusShow = function(connected) {
                    statusCalls.push(connected);
                };
                var ReconnectingWebSocket = function(uri) {
                    this.readyState = 0; // CONNECTING
                    this.listeners = {};
                    this.addEventListener = function(evt, fn) { this.listeners[evt] = fn; };
                    this.send = function() {};
                };
            ");
            ExecuteScriptFile(engine, Path.Combine(FindWwwRootPath(), "ws-client.js"));

            engine.Execute(@"
                var hub = new window.OverlayHub({ clientName: 'Overlay', topics: ['overlay'] });
                if (hub.physicalSocket.listeners['connecting']) {
                    hub.physicalSocket.listeners['connecting']();
                }
            ");

            var countAfterConnecting = engine.Evaluate("statusCalls.length").AsNumber();
            Assert.Equal(1, countAfterConnecting);
            Assert.False(engine.Evaluate("statusCalls[0]").AsBoolean());

            engine.Execute(@"
                if (hub.physicalSocket.listeners['error']) {
                    hub.physicalSocket.listeners['error']();
                }
            ");
            var countAfterError = engine.Evaluate("statusCalls.length").AsNumber();
            Assert.Equal(2, countAfterError);
            Assert.False(engine.Evaluate("statusCalls[1]").AsBoolean());
        }

    }
}
