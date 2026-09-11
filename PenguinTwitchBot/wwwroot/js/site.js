// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Download file helper function
window.downloadFile = async function(filename, content) {
    let blob;
    if (content && typeof content.arrayBuffer === 'function') {
        const buffer = await content.arrayBuffer();
        blob = new Blob([buffer], { type: 'application/octet-stream' });
    } else if (typeof content === 'string') {
        blob = new Blob([content], { type: 'application/json' });
    }
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.style.display = 'none';
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
};

window.panScrollElementById = function (elementId, deltaX, deltaY) {
    const el = document.getElementById(elementId);
    if (!el) return;
    el.scrollBy({
        left: deltaX || 0,
        top: deltaY || 0,
        behavior: 'smooth'
    });
};

// Auto-refresh the page when the Blazor Server circuit reconnect UI reports a failed/rejected
// reconnect, so users don't have to manually click "refresh". Guarded against reload loops via
// a capped, time-windowed counter in sessionStorage (survives the reload, cleared on success).
(function () {
    const STORAGE_KEY = 'blazorAutoReloadState';
    const MAX_AUTO_RELOADS = 3;
    const ATTEMPT_WINDOW_MS = 60000; // stale attempts outside this window don't count against the cap

    function readState() {
        try {
            return JSON.parse(sessionStorage.getItem(STORAGE_KEY) || 'null');
        } catch {
            return null;
        }
    }

    function tryConsumeReloadAttempt() {
        const now = Date.now();
        let state = readState();
        if (!state || (now - state.lastAttempt) > ATTEMPT_WINDOW_MS) {
            state = { count: 0, lastAttempt: now };
        }

        if (state.count >= MAX_AUTO_RELOADS) {
            return false;
        }

        state.count += 1;
        state.lastAttempt = now;
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state));
        return true;
    }

    document.addEventListener('DOMContentLoaded', function () {
        const modal = document.getElementById('components-reconnect-modal');
        if (!modal) return;

        const observer = new MutationObserver(function () {
            const classList = modal.classList;
            if (classList.contains('components-reconnect-failed') || classList.contains('components-reconnect-rejected')) {
                if (tryConsumeReloadAttempt()) {
                    location.reload();
                }
                // Cap reached: leave the existing "click here to refresh" UI as the manual fallback.
            } else if (!classList.contains('components-reconnect-show')) {
                // Circuit is connected/idle again, so reset the guard for the next incident.
                sessionStorage.removeItem(STORAGE_KEY);
            }
        });

        observer.observe(modal, { attributes: true, attributeFilter: ['class'] });
    });
})();
