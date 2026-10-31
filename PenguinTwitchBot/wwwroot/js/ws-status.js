(function (global) {
    'use strict';

    var lastStatus = null;

    function isSharedWebSocketWidget() {
        try {
            if (typeof global !== 'undefined') {
                if (global.__isOverlayHost || global.__overlayCompositorHub) return false;
                if (global.__isSharedWebSocketWidget) return true;
                if (global.parent && global.parent !== global && global.parent.__overlayCompositorHub) return true;
                if (global.location && global.location.search) {
                    var params = new URLSearchParams(global.location.search);
                    if (params.get('shared') === '1' || params.get('embedded') === '1') return true;
                }
            }
        } catch (_) {}
        return false;
    }

    function wsStatusHide() {
        try {
            if (typeof document !== 'undefined') {
                var el = document.getElementById('ws-status');
                if (el) {
                    el.style.display = 'none';
                    if (typeof el.setAttribute === 'function') el.setAttribute('hidden', '');
                }
                if (!document.getElementById('ws-status-shared-hide-style')) {
                    var style = document.createElement('style');
                    style.id = 'ws-status-shared-hide-style';
                    style.textContent = '#ws-status, #counter-status, #timer-status { display: none !important; }';
                    (document.head || document.documentElement).appendChild(style);
                }
            }
        } catch (_) {}
    }

    function wsStatusShow(connected) {
        lastStatus = connected;
        if (isSharedWebSocketWidget()) {
            wsStatusHide();
            return;
        }
        if (typeof document === 'undefined') return;
        var el = document.getElementById('ws-status');
        if (!el) {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', function () {
                    if (lastStatus !== null) {
                        wsStatusShow(lastStatus);
                    }
                }, { once: true });
            }
            return;
        }
        if (typeof el.removeAttribute === 'function') el.removeAttribute('hidden');
        if (el.hidden !== undefined) el.hidden = false;
        if (el.style && el.style.display === 'none') el.style.display = '';
        if (typeof clearTimeout === 'function') clearTimeout(el._hideTimer);
        var label = el.dataset && el.dataset.overlay ? el.dataset.overlay + ' ' : '';
        if (connected) {
            el.textContent = label + 'Connected';
            el.classList.add('connected');
            el.classList.remove('hidden');
            if (typeof setTimeout === 'function') {
                el._hideTimer = setTimeout(function () { el.classList.add('hidden'); }, 2000);
            }
        } else {
            el.textContent = label + 'Disconnected';
            el.classList.remove('connected', 'hidden');
        }
    }

    if (isSharedWebSocketWidget()) {
        wsStatusHide();
        if (typeof document !== 'undefined' && document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', wsStatusHide);
        }
    }

    global.isSharedWebSocketWidget = isSharedWebSocketWidget;
    global.wsStatusHide = wsStatusHide;
    global.wsStatusShow = wsStatusShow;
})(typeof window !== 'undefined' ? window : (typeof globalThis !== 'undefined' ? globalThis : this));
