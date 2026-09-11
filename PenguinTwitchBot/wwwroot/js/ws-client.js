// Shared helper for the single /ws endpoint. Widgets declare the topics they
// handle and the server only forwards matching events to them.
// Requires reconnecting-websocket/reconnectingWS.min.js to be loaded first.
(function (global) {
    const WS_TOPICS = {
        chat: 'chat',
        alerts: 'alerts',
        clips: 'clips',
        fishing: 'fishing',
        wheel: 'wheel',
        overlay: 'overlay',
        events: 'events'
    };

    function createWsSocket(clientName, topics, options) {
        const opts = options || {};
        const topicList = (topics || []).filter(Boolean);
        const scheme = window.location.protocol === 'https:' ? 'wss' : 'ws';
        const params = new URLSearchParams();
        if (clientName) params.set('clientName', clientName);
        if (topicList.length) params.set('topics', topicList.join(','));

        const socketUri = scheme + '://' + window.location.host + '/ws?' + params.toString();
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
    global.createWsSocket = createWsSocket;
}(window));
