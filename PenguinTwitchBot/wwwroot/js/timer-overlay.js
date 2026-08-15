/* global signalR */
(function () {
    const containerEl = document.getElementById("timer-container");
    const textEl = document.getElementById("timer-text");

    const params = new URLSearchParams(window.location.search);

    function param(name, fallback) {
        const value = params.get(name);
        return value === null || value.trim() === "" ? fallback : value;
    }

    const settings = {
        fontFamily: param("fontFamily", '"Segoe UI", sans-serif'),
        fontSize: parseInt(param("fontSize", "72"), 10),
        fontWeight: param("fontWeight", "700"),
        color: param("color", "#ffffff"),
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
        direction: "up",
        seconds: 0,
        anchorClientMs: performance.now()
    };

    function applyState(payload) {
        if (!payload) return;

        const direction = payload.direction === "down" ? "down" : "up";
        let seconds = Number(payload.seconds) || 0;

        // payload.seconds is the value at updatedAtUtc, so advance it to the server's current time.
        const updatedAtMs = Date.parse(payload.updatedAtUtc);
        const serverNowMs = Date.parse(payload.serverNowUtc);
        if (payload.isRunning && Number.isFinite(updatedAtMs) && Number.isFinite(serverNowMs) && serverNowMs > updatedAtMs) {
            const elapsed = (serverNowMs - updatedAtMs) / 1000;
            seconds = direction === "down" ? Math.max(0, seconds - elapsed) : seconds + elapsed;
        }

        state = {
            isRunning: !!payload.isRunning,
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
        const shouldHide = (settings.hideWhenStopped && !state.isRunning)
            || (settings.hideWhenZero && value < 1 && !state.isRunning);

        containerEl.classList.toggle("hidden", shouldHide);
        textEl.textContent = format(value);
    }

    async function loadInitialState() {
        try {
            const response = await fetch("/api/overlay/timer-state", { cache: "no-store" });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            applyState(await response.json());
        } catch (err) {
            console.error("Failed loading timer state", err);
        }
    }

    async function connect() {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/mainhub")
            .withAutomaticReconnect()
            .build();

        connection.on("StreamTimerUpdate", applyState);
        connection.onreconnected(loadInitialState);

        try {
            await connection.start();
        } catch (err) {
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
