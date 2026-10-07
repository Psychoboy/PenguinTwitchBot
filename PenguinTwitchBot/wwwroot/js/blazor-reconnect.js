// Blazor Server's default reconnection UI (autostart) dispatches "components-reconnect-state-changed"
// on #components-reconnect-modal for each state. "rejected" means the server was reached but the old
// circuit is gone (e.g. app restart) - the same case the manual "Click here to refresh" link in
// MainLayout.razor exists for - so auto-trigger that same location.reload(), capped to avoid a loop.
// "failed" usually means a transient network issue, so just ask Blazor to keep retrying instead of
// reloading, letting the circuit resume normally once connectivity returns.
(function () {
    const RELOAD_GUARD_KEY = 'blazorAutoReloadState';
    const MAX_AUTO_RELOADS = 3;
    const RELOAD_WINDOW_MS = 60000; // stale attempts outside this window don't count against the cap

    function tryConsumeReloadAttempt() {
        let state;
        try {
            state = JSON.parse(sessionStorage.getItem(RELOAD_GUARD_KEY) || 'null');
        } catch {
            state = null;
        }

        const now = Date.now();
        if (!state || (now - state.lastAttempt) > RELOAD_WINDOW_MS) {
            state = { count: 0, lastAttempt: now };
        }

        if (state.count >= MAX_AUTO_RELOADS) {
            return false;
        }

        state.count += 1;
        state.lastAttempt = now;
        sessionStorage.setItem(RELOAD_GUARD_KEY, JSON.stringify(state));
        return true;
    }

    // Blazor dispatches this event with `bubbles: false` and only ever binds it to whatever DOM node
    // is live the first time a disconnect happens, which can differ from the node present at page
    // load. A capture-phase listener on `document` still receives it regardless of which node instance
    // is targeted (capture-phase dispatch happens regardless of the event's bubbles setting).
    document.addEventListener('components-reconnect-state-changed', function (event) {
        if (!event.target || event.target.id !== 'components-reconnect-modal') return;

        const state = event.detail && event.detail.state;

        if (state === 'rejected') {
            // Definitive: reached the server, but the circuit couldn't be resumed.
            if (tryConsumeReloadAttempt()) {
                location.reload();
            }
            // Cap reached: leave the modal up so the manual "click here to refresh" link still works.
        } else if (state === 'failed') {
            // Not definitive (e.g. network blip) - keep retrying rather than reloading.
            window.Blazor?.reconnect();
        } else if (state === 'hide') {
            // Connection restored - reset the guard for the next incident.
            sessionStorage.removeItem(RELOAD_GUARD_KEY);
        }
    }, true);
})();
