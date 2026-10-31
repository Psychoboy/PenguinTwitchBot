// Shared helper for the single /ws endpoint and cross-widget OverlayBus communication.
// Widgets declare the topics they handle and the server forwards matching events.
// Inside an overlay layout (overlay.html), child widgets share a single multiplexed
// WebSocket connection managed by OverlayHub to drastically lower network traffic and socket overhead.
// Widgets can also communicate directly in the client without server roundtrips using OverlayBus.
// Requires reconnecting-websocket/reconnectingWS.min.js to be loaded first for direct connections.
(function (global) {
    'use strict';

    const WS_TOPICS = {
        chat: 'chat',
        alerts: 'alerts',
        clips: 'clips',
        fishing: 'fishing',
        wheel: 'wheel',
        overlay: 'overlay',
        events: 'events',
        parachute: 'parachute',
        plinko: 'plinko',
        balloon: 'balloon',
        curling: 'curling',
        golf: 'golf',
        racing: 'racing',
        starship: 'starship',
        ufoclaw: 'ufoclaw'
    };

    function buildQueryString(paramsObj) {
        if (typeof URLSearchParams !== 'undefined') {
            try {
                const p = new URLSearchParams();
                if (typeof p.set === 'function') {
                    Object.keys(paramsObj).forEach(function (k) {
                        if (paramsObj[k] !== undefined && paramsObj[k] !== null && paramsObj[k] !== '') {
                            p.set(k, paramsObj[k]);
                        }
                    });
                    return p.toString();
                }
            } catch (_) {}
        }
        const parts = [];
        Object.keys(paramsObj).forEach(function (k) {
            if (paramsObj[k] !== undefined && paramsObj[k] !== null && paramsObj[k] !== '') {
                parts.push(encodeURIComponent(k) + '=' + encodeURIComponent(paramsObj[k]));
            }
        });
        return parts.join('&');
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // OverlayBus: Direct client-side inter-widget communication
    // ─────────────────────────────────────────────────────────────────────────────
    const CHANNEL_NAME = 'penguin_overlay_bus';
    const localListeners = new Map();
    const MAX_DISPATCHED_IDS = 1000;
    const dispatchedEventIds = new Set();
    let broadcastChannel = null;

    function recordDispatchedEventId(id) {
        if (!id) return;
        dispatchedEventIds.add(id);
        if (dispatchedEventIds.size > MAX_DISPATCHED_IDS) {
            const oldest = dispatchedEventIds.keys().next().value;
            dispatchedEventIds.delete(oldest);
        }
    }

    try {
        if (typeof global.BroadcastChannel !== 'undefined') {
            broadcastChannel = new global.BroadcastChannel(CHANNEL_NAME);
            broadcastChannel.onmessage = function (event) {
                if (event && event.data) {
                    dispatchToLocal(event.data, false);
                }
            };
        }
    } catch (_) {
        broadcastChannel = null;
    }

    function dispatchToLocal(eventData, forwardToFrames) {
        if (!eventData || typeof eventData !== 'object') return;
        if (eventData.id) {
            if (dispatchedEventIds.has(eventData.id)) return;
            recordDispatchedEventId(eventData.id);
        }
        const type = eventData.type;
        const target = eventData.target;
        const clientName = (global.__overlayClientName || '').toLowerCase();

        // If target is specified, ignore if it doesn't match this client/window
        if (target && clientName && target !== '*' && target.toLowerCase() !== clientName) {
            // Check if target matches any subscribed topics
            const myTopics = (global.__overlayClientTopics || []).map(function (t) { return String(t).toLowerCase(); });
            if (!myTopics.includes(target.toLowerCase())) {
                if (forwardToFrames && global.__overlayCompositorHub) {
                    global.__overlayCompositorHub.routeBusEventToChildren(eventData);
                }
                return;
            }
        }

        // Notify handlers for specific event type
        if (type && localListeners.has(type)) {
            const handlers = Array.from(localListeners.get(type));
            for (let i = 0; i < handlers.length; i++) {
                try { handlers[i](eventData.payload, eventData); } catch (e) { console.error('OverlayBus handler error:', e); }
            }
        }

        // Notify wildcard '*' handlers
        if (localListeners.has('*')) {
            const handlers = Array.from(localListeners.get('*'));
            for (let i = 0; i < handlers.length; i++) {
                try { handlers[i](eventData.payload, eventData); } catch (e) { console.error('OverlayBus wildcard handler error:', e); }
            }
        }

        if (forwardToFrames && global.__overlayCompositorHub) {
            global.__overlayCompositorHub.routeBusEventToChildren(eventData);
        }
    }

    const OverlayBus = {
        /**
         * Broadcasts an event to all widgets locally and across browser sources.
         * @param {string} type Event name/type
         * @param {*} payload Event payload
         */
        broadcast: function (type, payload) {
            this.send('*', type, payload);
        },

        /**
         * Sends an event to a specific target widget, or '*' for all.
         * @param {string} target Target widget name or topic
         * @param {string} type Event name/type
         * @param {*} payload Event payload
         */
        send: function (target, type, payload) {
            const eventData = {
                id: 'evt_' + Date.now() + '_' + Math.random().toString(36).slice(2, 8),
                source: global.__overlayClientName || 'anonymous',
                target: target || '*',
                type: String(type || ''),
                payload: payload,
                timestamp: Date.now()
            };

            // 1. Dispatch locally in this window/frame
            dispatchToLocal(eventData, false);

            // 2. Dispatch across windows via BroadcastChannel
            if (broadcastChannel) {
                try { broadcastChannel.postMessage(eventData); } catch (_) {}
            }

            // 3. If in a child iframe with accessible parent hub, notify parent hub
            try {
                if (global.parent && global.parent !== global && global.parent.__overlayCompositorHub) {
                    global.parent.__overlayCompositorHub.receiveBusEventFromChild(eventData, global);
                }
            } catch (_) {}

            // 4. If this window is the compositor hub, dispatch down to child iframes
            if (global.__overlayCompositorHub) {
                global.__overlayCompositorHub.routeBusEventToChildren(eventData);
            }
        },

        /**
         * Subscribes to events of the given type (or '*' for all).
         * Returns an unsubscribe function.
         */
        on: function (type, handler) {
            if (typeof handler !== 'function') return function () {};
            const key = String(type || '*');
            if (!localListeners.has(key)) {
                localListeners.set(key, new Set());
            }
            localListeners.get(key).add(handler);
            return function () { OverlayBus.off(key, handler); };
        },

        /**
         * Subscribes once to an event of the given type.
         */
        once: function (type, handler) {
            if (typeof handler !== 'function') return function () {};
            const unsub = OverlayBus.on(type, function (payload, event) {
                unsub();
                handler(payload, event);
            });
            return unsub;
        },

        /**
         * Unsubscribes a previously registered handler.
         */
        off: function (type, handler) {
            const key = String(type || '*');
            if (localListeners.has(key)) {
                localListeners.get(key).delete(handler);
                if (localListeners.get(key).size === 0) {
                    localListeners.delete(key);
                }
            }
        },

        /** Internal receiver for cross-frame messages */
        _receive: function (eventData) {
            dispatchToLocal(eventData, false);
        }
    };

    // ─────────────────────────────────────────────────────────────────────────────
    // VirtualSocket: Emulates WebSocket for iframe widgets sharing the Hub
    // ─────────────────────────────────────────────────────────────────────────────
    function VirtualSocket(hub, clientName, topics) {
        this._hub = hub;
        this.clientName = clientName || 'virtual-widget';
        this.topics = Array.isArray(topics) ? topics.filter(Boolean) : [];
        this._listeners = new Map();
        this.onopen = null;
        this.onmessage = null;
        this.onclose = null;
        this.onerror = null;

        // Register with hub
        hub._registerVirtualSocket(this);

        // If hub's physical socket is already OPEN, schedule open notification
        if (hub.physicalSocket && hub.physicalSocket.readyState === 1) {
            const self = this;
            if (typeof queueMicrotask === 'function') {
                queueMicrotask(function () { self._dispatchOpen(); });
            } else if (typeof setTimeout === 'function') {
                setTimeout(function () { self._dispatchOpen(); }, 0);
            } else {
                self._dispatchOpen();
            }
        }
    }

    VirtualSocket.CONNECTING = 0;
    VirtualSocket.OPEN = 1;
    VirtualSocket.CLOSING = 2;
    VirtualSocket.CLOSED = 3;

    VirtualSocket.prototype = {
        get readyState() {
            return (this._hub && this._hub.physicalSocket)
                ? this._hub.physicalSocket.readyState
                : VirtualSocket.OPEN;
        },
        get url() {
            return (this._hub && this._hub.physicalSocket) ? this._hub.physicalSocket.url : '';
        },
        send: function (data) {
            if (this._hub) {
                this._hub.send(data);
            }
        },
        close: function () {
            if (this._hub) {
                this._hub._unregisterVirtualSocket(this);
            }
            this._dispatchClose({ type: 'close' });
        },
        addEventListener: function (type, handler) {
            if (typeof handler !== 'function') return;
            if (!this._listeners.has(type)) {
                this._listeners.set(type, new Set());
            }
            this._listeners.get(type).add(handler);
        },
        removeEventListener: function (type, handler) {
            if (this._listeners.has(type)) {
                this._listeners.get(type).delete(handler);
            }
        },
        _dispatchOpen: function () {
            const evt = { type: 'open' };
            if (typeof this.onopen === 'function') {
                try { this.onopen(evt); } catch (e) { console.error('VirtualSocket onopen error:', e); }
            }
            this._dispatchEvent('open', evt);
        },
        _dispatchMessage: function (data) {
            const evt = { type: 'message', data: data };
            if (typeof this.onmessage === 'function') {
                try { this.onmessage(evt); } catch (e) { console.error('VirtualSocket onmessage error:', e); }
            }
            this._dispatchEvent('message', evt);
        },
        _dispatchClose: function (evt) {
            const e = evt || { type: 'close' };
            if (typeof this.onclose === 'function') {
                try { this.onclose(e); } catch (err) { console.error('VirtualSocket onclose error:', err); }
            }
            this._dispatchEvent('close', e);
        },
        _dispatchEvent: function (type, evt) {
            if (this._listeners.has(type)) {
                const handlers = Array.from(this._listeners.get(type));
                for (let i = 0; i < handlers.length; i++) {
                    try { handlers[i](evt); } catch (err) { console.error('VirtualSocket ' + type + ' listener error:', err); }
                }
            }
        }
    };

    // ─────────────────────────────────────────────────────────────────────────────
    // OverlayHub: Compositor Hub running in overlay.html
    // ─────────────────────────────────────────────────────────────────────────────
    function OverlayHub(options) {
        const opts = options || {};
        this.clientName = opts.clientName || 'OverlayCompositor';
        this.overlayKey = opts.overlayKey || '';
        this.layoutName = opts.layoutName || '';
        this.activeTopics = new Set(['overlay', ...(opts.topics || [])].filter(Boolean));
        this.virtualSockets = new Set();
        this.childWindows = new Set();

        const scheme = (typeof window !== 'undefined' && window.location.protocol === 'https:') ? 'wss' : 'ws';
        const host = (typeof window !== 'undefined') ? window.location.host : 'localhost';
        const queryString = buildQueryString({
            clientName: this.clientName,
            topics: Array.from(this.activeTopics).join(','),
            multiplex: 'true'
        });

        const socketUri = scheme + '://' + host + '/ws' + (queryString ? '?' + queryString : '');
        const self = this;

        if (typeof ReconnectingWebSocket !== 'undefined') {
            this.physicalSocket = new ReconnectingWebSocket(socketUri, null, {
                reconnectInterval: opts.reconnectInterval || 5000
            });

            this.physicalSocket.addEventListener('open', function () {
                self._syncSubscriptions();
                if (typeof global.wsStatusShow === 'function') {
                    global.wsStatusShow(true);
                }
                self.virtualSockets.forEach(function (vs) { vs._dispatchOpen(); });
            });

            this.physicalSocket.addEventListener('close', function (event) {
                if (typeof global.wsStatusShow === 'function') {
                    global.wsStatusShow(false);
                }
                self.virtualSockets.forEach(function (vs) { vs._dispatchClose(event); });
            });

            this.physicalSocket.addEventListener('message', function (event) {
                self._handlePhysicalMessage(event);
            });
        }
    }

    OverlayHub.prototype = {
        _syncSubscriptions: function () {
            if (!this.physicalSocket || this.physicalSocket.readyState !== 1) return;
            const topics = Array.from(this.activeTopics);
            if (topics.length > 0) {
                try {
                    this.physicalSocket.send(JSON.stringify({
                        request: 'subscribe',
                        topics: topics,
                        multiplex: true
                    }));
                } catch (_) {}
            }
        },

        send: function (data) {
            if (this.physicalSocket && this.physicalSocket.readyState === 1) {
                this.physicalSocket.send(data);
            }
        },

        _registerVirtualSocket: function (virtualSocket) {
            this.virtualSockets.add(virtualSocket);
            let hasNewTopics = false;
            for (let i = 0; i < virtualSocket.topics.length; i++) {
                const t = virtualSocket.topics[i];
                if (!this.activeTopics.has(t)) {
                    this.activeTopics.add(t);
                    hasNewTopics = true;
                }
            }
            if (hasNewTopics) {
                this._syncSubscriptions();
            }
        },

        _unregisterVirtualSocket: function (virtualSocket) {
            this.virtualSockets.delete(virtualSocket);
        },

        createVirtualSocket: function (clientName, topics) {
            return new VirtualSocket(this, clientName, topics);
        },

        _handlePhysicalMessage: function (event) {
            const raw = event.data;
            if (!raw) return;

            // Handle ping-pong keepalive
            if (raw === 'ping') {
                this.send('pong');
                // Forward ping to all virtual sockets so heartbeat queues can respond if needed
                this.virtualSockets.forEach(function (vs) { vs._dispatchMessage('ping'); });
                return;
            }

            // Check if server sent a multiplex envelope: { "topic": "...", "payload": "..." }
            let topic = null;
            let payload = raw;
            if (raw.startsWith('{') && raw.includes('"topic"')) {
                try {
                    const parsed = JSON.parse(raw);
                    if (parsed && typeof parsed.topic === 'string' && typeof parsed.payload === 'string') {
                        topic = parsed.topic;
                        payload = parsed.payload;
                    }
                } catch (_) {}
            }

            // If not an envelope, try to detect topic from content
            if (!topic && raw.startsWith('{')) {
                topic = this._detectTopicFromContent(raw);
            }

            // Dispatch payload to matching virtual sockets
            this._routePayload(topic, payload);
        },

        _detectTopicFromContent: function (raw) {
            try {
                const msg = JSON.parse(raw);
                if (!msg || typeof msg !== 'object') return null;
                if (msg.type === 'overlay_reload' || msg.type === 'refresh_all') return 'overlay';
                if (msg.type === 'chat' || msg.type === 'delete' || msg.type === 'ban' || msg.chatMessage) return 'chat';
                if (msg.type === 'plinko_drop' || (msg.session && msg.session.game === 'plinko')) return 'plinko';
                if (msg.type === 'parachute_drop' || msg.type === 'parachute_droplets' || (msg.session && msg.session.game === 'parachute')) return 'parachute';
                if (msg.type === 'curling_slide' || msg.type === 'curling_drop' || (msg.session && msg.session.game === 'curling')) return 'curling';
                if (msg.type === 'golf_shot' || msg.type === 'golf_putt' || msg.type === 'golf_drop' || (msg.session && msg.session.game === 'golf')) return 'golf';
                if (msg.type === 'balloon_drop' || msg.type === 'balloon_hazards' || (msg.session && msg.session.game === 'balloon')) return 'balloon';
                if (msg.spinTo !== undefined || msg.type === 'wheel') return 'wheel';
                if (msg.clipId !== undefined || msg.type === 'clip') return 'clips';
                if (msg.expeditionId !== undefined || (msg.type && String(msg.type).startsWith('starship_'))) return 'starship';
                if (msg.claw !== undefined || msg.roundId !== undefined) return 'ufoclaw';
                if (msg.type === 'other_game_status' || msg.type === 'other_drop_active') return 'drop_games_all';
                if (['follower', 'sub', 'cheer', 'raid', 'reward', 'tip'].includes(msg.type)) return 'alerts';
            } catch (_) {}
            return null;
        },

        _routePayload: function (topic, payload) {
            const dropGameTopics = ['plinko', 'parachute', 'curling', 'golf', 'balloon'];

            this.virtualSockets.forEach(function (vs) {
                if (!topic) {
                    // Untopiced broadcast: deliver to all
                    vs._dispatchMessage(payload);
                    return;
                }

                if (topic === 'drop_games_all') {
                    // Shared drop game notification: deliver to any drop game widget
                    const isDropGame = vs.topics.some(function (t) { return dropGameTopics.includes(t); });
                    if (isDropGame) vs._dispatchMessage(payload);
                    return;
                }

                // Check if this virtual socket is subscribed to topic
                if (vs.topics.includes(topic) || vs.topics.length === 0) {
                    vs._dispatchMessage(payload);
                }
            });
        },

        receiveBusEventFromChild: function (eventData, fromWindow) {
            if (fromWindow) this.childWindows.add(fromWindow);
            dispatchToLocal(eventData, false);
            this.routeBusEventToChildren(eventData, fromWindow);
        },

        routeBusEventToChildren: function (eventData, exceptWindow) {
            const iframes = (typeof document !== 'undefined') ? document.querySelectorAll('iframe.overlay-widget') : [];
            for (let i = 0; i < iframes.length; i++) {
                try {
                    const cw = iframes[i].contentWindow;
                    if (cw && cw !== exceptWindow && cw.OverlayBus && typeof cw.OverlayBus._receive === 'function') {
                        cw.OverlayBus._receive(eventData);
                    }
                } catch (_) {}
            }
        }
    };

    function createWsSocket(clientName, topics, options) {
        const opts = options || {};
        const topicList = (topics || []).filter(Boolean);

        // Record on global for OverlayBus addressing
        global.__overlayClientName = clientName || '';
        global.__overlayClientTopics = topicList;

        // Check if running inside overlay compositor hub
        let hub = null;
        let isChild = false;
        try {
            if (global.parent && global.parent !== global && global.parent.__overlayCompositorHub) {
                hub = global.parent.__overlayCompositorHub;
                isChild = true;
            } else if (global.__overlayCompositorHub) {
                hub = global.__overlayCompositorHub;
            }
        } catch (_) {
            hub = null;
        }

        if (hub && typeof hub.createVirtualSocket === 'function') {
            if (isChild) {
                global.__isSharedWebSocketWidget = true;
                if (typeof global.wsStatusHide === 'function') {
                    global.wsStatusHide();
                }
            }
            return hub.createVirtualSocket(clientName, topicList, opts);
        }

        // Standalone WebSocket connection
        const scheme = (typeof window !== 'undefined' && window.location.protocol === 'https:') ? 'wss' : 'ws';
        const host = (typeof window !== 'undefined') ? window.location.host : 'localhost';
        const queryString = buildQueryString({
            clientName: clientName || null,
            topics: topicList.length ? topicList.join(',') : null
        });

        const socketUri = scheme + '://' + host + '/ws' + (queryString ? '?' + queryString : '');
        const socket = new ReconnectingWebSocket(socketUri, null, {
            reconnectInterval: opts.reconnectInterval || 5000
        });

        // Re-send the subscription on every (re)connect so the server state matches after a restart.
        socket.addEventListener('open', function () {
            if (topicList.length) {
                socket.send(JSON.stringify({ request: 'subscribe', topics: topicList }));
            }
        });

        return socket;
    }

    global.WS_TOPICS = WS_TOPICS;
    global.OverlayBus = OverlayBus;
    global.OverlayHub = OverlayHub;
    global.createWsSocket = createWsSocket;
}(typeof window !== 'undefined' ? window : (typeof globalThis !== 'undefined' ? globalThis : this)));
