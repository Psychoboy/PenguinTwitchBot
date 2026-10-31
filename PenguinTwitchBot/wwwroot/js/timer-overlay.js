/* global signalR */
(function () {
    const containerEl = document.getElementById("timer-container");
    const textEl = document.getElementById("timer-text");
    const statusEl = document.getElementById("timer-status");

    const isShared = (function () {
        try {
            if (window.parent && window.parent !== window && window.parent.__overlayCompositorHub) return true;
            if (new URLSearchParams(window.location.search).get('shared') === '1') return true;
        } catch (_) {}
        return false;
    })();
    if (isShared && statusEl) {
        statusEl.style.display = 'none';
        if (typeof statusEl.setAttribute === 'function') statusEl.setAttribute('hidden', '');
    }

    let statusHideTimer = null;
    function setStatus(message, connected) {
        if (isShared || !statusEl) return;
        if (typeof statusEl.removeAttribute === 'function') statusEl.removeAttribute('hidden');
        if (statusEl.hidden !== undefined) statusEl.hidden = false;
        if (statusEl.style && statusEl.style.display === 'none') statusEl.style.display = '';
        if (statusHideTimer) {
            clearTimeout(statusHideTimer);
            statusHideTimer = null;
        }
        statusEl.textContent = message;
        if (connected) {
            statusEl.classList.add("connected");
            statusEl.classList.remove("hidden");
            statusHideTimer = setTimeout(() => {
                statusEl.classList.add("hidden");
            }, 2000);
        } else {
            statusEl.classList.remove("connected", "hidden");
        }
    }

    const params = new URLSearchParams(window.location.search);

    function param(name, fallback) {
        const value = params.get(name);
        return value === null || value.trim() === "" ? fallback : value;
    }

    const settings = {
        timerName: param("timerName", param("timer", param("name", "Default"))).trim(),
        fontFamily: param("fontFamily", '"Segoe UI", sans-serif'),
        fontSize: parseInt(param("fontSize", "72"), 10),
        fontWeight: param("fontWeight", "700"),
        color: param("color", "#000000"),
        bgColor: param("bgColor", "transparent"),
        textShadow: param("textShadow", "none"),
        letterSpacing: parseInt(param("letterSpacing", "0"), 10),
        borderRadius: parseInt(param("borderRadius", "0"), 10),
        padding: parseInt(param("padding", "0"), 10),
        format: param("format", "auto"),
        showTenths: param("showTenths", "false") === "true",
        prefix: param("prefix", ""),
        suffix: param("suffix", ""),
        hideWhenStopped: param("hideWhenStopped", "false") === "true",
        hideWhenZero: param("hideWhenZero", "false") === "true"
    };

    function applyStyles() {
        const root = containerEl.style;
        root.setProperty("--timer-bg", settings.bgColor);
        root.setProperty("--timer-radius", `${settings.borderRadius}px`);
        root.setProperty("--timer-padding", `${settings.padding}px`);
        root.setProperty("--timer-font-family", settings.fontFamily);
        root.setProperty("--timer-font-size", `${settings.fontSize}px`);
        root.setProperty("--timer-font-weight", settings.fontWeight);
        root.setProperty("--timer-color", settings.color);
        root.setProperty("--timer-text-shadow", settings.textShadow);
        root.setProperty("--timer-letter-spacing", `${settings.letterSpacing}px`);
    }

    // Server-authoritative anchor; the browser interpolates between updates.
    let state = {
        isRunning: false,
        isHidden: false,
        direction: "up",
        seconds: 0,
        anchorClientMs: performance.now()
    };

    let lastAppliedUpdatedAtMs = Number.NEGATIVE_INFINITY;

    function applyState(payload) {
        if (!payload) return;

        // If this update is for a different timer, ignore it
        if (payload.name && payload.name.localeCompare(settings.timerName, undefined, { sensitivity: "accent" }) !== 0) {
            return;
        }

        const updatedAtMs = Date.parse(payload.updatedAtUtc);

        // The initial fetch can land after a hub push, so never let an older snapshot win.
        if (Number.isFinite(updatedAtMs) && updatedAtMs < lastAppliedUpdatedAtMs) return;

        const direction = payload.direction === "down" ? "down" : "up";
        let seconds = Number(payload.seconds) || 0;

        // payload.seconds is the value at updatedAtUtc, so advance it to the server's current time.
        const serverNowMs = Date.parse(payload.serverNowUtc);
        if (payload.isRunning && Number.isFinite(updatedAtMs) && Number.isFinite(serverNowMs) && serverNowMs > updatedAtMs) {
            const elapsed = (serverNowMs - updatedAtMs) / 1000;
            seconds = direction === "down" ? Math.max(0, seconds - elapsed) : seconds + elapsed;
        }

        if (Number.isFinite(updatedAtMs)) lastAppliedUpdatedAtMs = updatedAtMs;

        state = {
            isRunning: !!payload.isRunning,
            isHidden: !!payload.isHidden,
            direction: direction,
            seconds: seconds,
            anchorClientMs: performance.now()
        };

        render();
    }

    function currentSeconds() {
        if (!state.isRunning) return state.seconds;

        const elapsed = (performance.now() - state.anchorClientMs) / 1000;
        return state.direction === "down"
            ? Math.max(0, state.seconds - elapsed)
            : state.seconds + elapsed;
    }

    function pad(value, length) {
        return String(Math.floor(value)).padStart(length, "0");
    }

    function format(totalSeconds) {
        const hours = Math.floor(totalSeconds / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const totalMinutes = Math.floor(totalSeconds / 60);
        const seconds = Math.floor(totalSeconds % 60);
        const tenths = Math.floor((totalSeconds % 1) * 10);

        let text;
        if (settings.format === "hh:mm:ss" || (settings.format === "auto" && hours > 0)) {
            text = `${pad(hours, 2)}:${pad(minutes, 2)}:${pad(seconds, 2)}`;
        } else if (settings.format === "ss") {
            text = pad(totalSeconds, 2);
        } else {
            text = `${pad(totalMinutes, 2)}:${pad(seconds, 2)}`;
        }

        if (settings.showTenths) text += `.${tenths}`;

        return `${settings.prefix}${text}${settings.suffix}`;
    }

    function render() {
        const value = currentSeconds();
        const shouldHide = state.isHidden
            || (settings.hideWhenStopped && !state.isRunning)
            || (settings.hideWhenZero && value < 1 && !state.isRunning);

        containerEl.classList.toggle("hidden", shouldHide);
        textEl.textContent = format(value);
    }

    async function loadInitialState() {
        try {
            const url = "/api/overlay/timer-state?name=" + encodeURIComponent(settings.timerName);
            const response = await fetch(url, { cache: "no-store" });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            applyState(await response.json());
        } catch (err) {
            console.error("Failed loading timer state", err);
        }
    }

    async function connect() {
        if (typeof signalR === "undefined") {
            setStatus("Timer: SignalR failed to load", false);
            console.error("SignalR client library is not available");
            return;
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/mainhub")
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: retryContext => {
                    const delays = [0, 2000, 5000, 10000, 15000, 30000];
                    if (retryContext.previousRetryCount < delays.length) {
                        return delays[retryContext.previousRetryCount];
                    }
                    return 30000;
                }
            })
            .build();

        connection.on("StreamTimerUpdate", applyState);
        connection.onreconnecting(() => setStatus("Timer: reconnecting...", false));
        connection.onreconnected(() => {
            setStatus("Timer: connected", true);
            loadInitialState();
        });
        connection.onclose(() => {
            setStatus("Timer: disconnected", false);
            setTimeout(connect, 5000);
        });

        try {
            await connection.start();
            setStatus("Timer: connected", true);
        } catch (err) {
            setStatus("Timer: connection failed", false);
            console.error("Failed connecting to the timer hub, retrying shortly", err);
            setTimeout(connect, 5000);
        }
    }

    applyStyles();
    render();
    loadInitialState();
    connect();

    function tick() {
        render();
        requestAnimationFrame(tick);
    }
    requestAnimationFrame(tick);
})();
