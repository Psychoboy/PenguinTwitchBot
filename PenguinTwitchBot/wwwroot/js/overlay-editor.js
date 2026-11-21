// Overlay Editor — interact.js integration
// The canvas is rendered at 50% scale (960x540 represents 1920x1080).
// All pixel values passed to Blazor are in actual (1920x1080) space.

const WIDGET_COLORS = {
    alerts:    '#1565C0',
    clips:     '#2E7D32',
    fireworks: '#BF360C',
    fishing:   '#00695C',
    wheel:     '#6A1B9A',
};

let _dotnet = null;
let _interactable = null;
let _scale = 0.5; // updated on each init() call
let _snapSize = 0; // 0 = disabled, >0 = snap increment in canvas px
let _viewportEl = null;
let _isPanning = false;
let _lastPanX = 0;
let _lastPanY = 0;
let _viewportHandlers = null;
let _keyDownHandler = null;

function snapValue(val) {
    if (_snapSize > 0) {
        return Math.round(val / _snapSize) * _snapSize;
    }
    return Math.round(val);
}

function detachKeyboardShortcuts() {
    if (_keyDownHandler) {
        document.removeEventListener('keydown', _keyDownHandler);
        _keyDownHandler = null;
    }
}

function attachKeyboardShortcuts() {
    detachKeyboardShortcuts();
    _keyDownHandler = (e) => {
        if (!_dotnet) return;

        const key = e.key;
        const isSave = (e.ctrlKey || e.metaKey) && key.toLowerCase() === 's';
        if (isSave) {
            e.preventDefault();
            _dotnet.invokeMethodAsync('OnKeyboardSave');
            return;
        }

        const active = document.activeElement;
        if (active && (
            active.tagName === 'INPUT' ||
            active.tagName === 'TEXTAREA' ||
            active.tagName === 'SELECT' ||
            active.isContentEditable
        )) {
            return;
        }

        const step = e.shiftKey ? 10 : 1;
        if (key === 'ArrowLeft') {
            e.preventDefault();
            _dotnet.invokeMethodAsync('OnKeyboardNudge', -step, 0);
        } else if (key === 'ArrowRight') {
            e.preventDefault();
            _dotnet.invokeMethodAsync('OnKeyboardNudge', step, 0);
        } else if (key === 'ArrowUp') {
            e.preventDefault();
            _dotnet.invokeMethodAsync('OnKeyboardNudge', 0, -step);
        } else if (key === 'ArrowDown') {
            e.preventDefault();
            _dotnet.invokeMethodAsync('OnKeyboardNudge', 0, step);
        } else if (key === 'Delete' || key === 'Backspace') {
            e.preventDefault();
            _dotnet.invokeMethodAsync('OnKeyboardDelete');
        } else if (key === 'Escape') {
            _dotnet.invokeMethodAsync('OnKeyboardEscape');
        }
    };
    document.addEventListener('keydown', _keyDownHandler);
}

function detachViewportInteractions() {
    if (!_viewportEl || !_viewportHandlers) return;

    _viewportEl.removeEventListener('mousedown', _viewportHandlers.onMouseDown);
    _viewportEl.removeEventListener('wheel', _viewportHandlers.onWheel);
    document.removeEventListener('mousemove', _viewportHandlers.onMouseMove);
    document.removeEventListener('mouseup', _viewportHandlers.onMouseUp);
    _viewportEl.classList.remove('canvas-scroll-wrapper--panning');

    _viewportHandlers = null;
    _viewportEl = null;
    _isPanning = false;
}

function attachViewportInteractions() {
    detachViewportInteractions();
    _viewportEl = document.getElementById('canvas-scroll-wrapper');
    if (!_viewportEl) return;

    const onMouseDown = (e) => {
        // Left-drag pans only on empty canvas space; middle-drag pans anywhere.
        const clickedWidget = e.target.closest('.widget-tile');
        if (e.button !== 0 && e.button !== 1) return;
        if (e.button === 0 && clickedWidget) return;

        _isPanning = true;
        _lastPanX = e.clientX;
        _lastPanY = e.clientY;
        _viewportEl.classList.add('canvas-scroll-wrapper--panning');
        e.preventDefault();
    };

    const onMouseMove = (e) => {
        if (!_isPanning || !_viewportEl) return;

        const dx = e.clientX - _lastPanX;
        const dy = e.clientY - _lastPanY;
        _lastPanX = e.clientX;
        _lastPanY = e.clientY;

        _viewportEl.scrollLeft -= dx;
        _viewportEl.scrollTop -= dy;
        e.preventDefault();
    };

    const onMouseUp = () => {
        if (!_isPanning || !_viewportEl) return;
        _isPanning = false;
        _viewportEl.classList.remove('canvas-scroll-wrapper--panning');
    };

    const onWheel = (e) => {
        if (!_dotnet || !_viewportEl) return;

        // Wheel controls zoom on the canvas viewport.
        const direction = e.deltaY < 0 ? 1 : -1;
        const rect = _viewportEl.getBoundingClientRect();
        const anchorX = e.clientX - rect.left;
        const anchorY = e.clientY - rect.top;
        _dotnet.invokeMethodAsync('AdjustZoomFromWheel', direction, anchorX, anchorY);
        e.preventDefault();
    };

    _viewportEl.addEventListener('mousedown', onMouseDown, { passive: false });
    _viewportEl.addEventListener('wheel', onWheel, { passive: false });
    document.addEventListener('mousemove', onMouseMove, { passive: false });
    document.addEventListener('mouseup', onMouseUp);

    _viewportHandlers = { onMouseDown, onMouseMove, onMouseUp, onWheel };
}

window.overlayEditor = {

    init(dotnetHelper, scale, snapSize) {
        _dotnet = dotnetHelper;
        _scale = scale || 0.5;
        if (typeof snapSize === 'number') {
            _snapSize = snapSize;
        }
        if (_interactable) {
            _interactable.unset();
            _interactable = null;
        }

        attachViewportInteractions();
        attachKeyboardShortcuts();

        // Selector targeting — automatically covers widgets added/removed later.
        _interactable = interact('.widget-tile')
            .draggable({
                modifiers: [],
                listeners: {
                    move(event) {
                        const el = event.target;
                        const rawX = (parseFloat(el.dataset.x) || 0) + event.dx;
                        const rawY = (parseFloat(el.dataset.y) || 0) + event.dy;
                        el.dataset.x = rawX;
                        el.dataset.y = rawY;
                        const canvasX = snapValue(rawX / _scale);
                        const canvasY = snapValue(rawY / _scale);
                        el.style.left = (canvasX * _scale) + 'px';
                        el.style.top  = (canvasY * _scale) + 'px';
                        _dotnet.invokeMethodAsync('OnWidgetMoved',
                            el.dataset.widgetId,
                            canvasX,
                            canvasY);
                    },
                    end(event) {
                        _dotnet.invokeMethodAsync('OnDragEnd');
                    }
                }
            })
            .resizable({
                edges: { right: true, bottom: true, left: true, top: true },
                modifiers: [
                    interact.modifiers.restrictSize({ min: { width: 20, height: 20 } })
                ],
                listeners: {
                    move(event) {
                        const el = event.target;
                        const rawX = (parseFloat(el.dataset.x) || 0) + event.deltaRect.left;
                        const rawY = (parseFloat(el.dataset.y) || 0) + event.deltaRect.top;
                        el.dataset.x = rawX;
                        el.dataset.y = rawY;
                        const canvasX = snapValue(rawX / _scale);
                        const canvasY = snapValue(rawY / _scale);
                        const canvasW = Math.max(10, snapValue(event.rect.width  / _scale));
                        const canvasH = Math.max(10, snapValue(event.rect.height / _scale));
                        el.style.left   = (canvasX * _scale) + 'px';
                        el.style.top    = (canvasY * _scale) + 'px';
                        el.style.width  = (canvasW * _scale) + 'px';
                        el.style.height = (canvasH * _scale) + 'px';
                        _dotnet.invokeMethodAsync('OnWidgetResized',
                            el.dataset.widgetId,
                            canvasX,
                            canvasY,
                            canvasW,
                            canvasH);
                    },
                    end(event) {
                        _dotnet.invokeMethodAsync('OnDragEnd');
                    }
                }
            });
    },

    setSnapSize(snapSize) {
        _snapSize = typeof snapSize === 'number' ? snapSize : 0;
    },

    destroy() {
        if (_interactable) {
            _interactable.unset();
            _interactable = null;
        }
        detachViewportInteractions();
        detachKeyboardShortcuts();
    },

    panViewport(deltaX, deltaY) {
        const wrapper = document.getElementById('canvas-scroll-wrapper');
        if (!wrapper) return;
        wrapper.scrollBy({ left: deltaX || 0, top: deltaY || 0, behavior: 'smooth' });
    },

    applyZoomAnchor(wrapperId, anchorX, anchorY, oldScale, newScale) {
        const wrapper = document.getElementById(wrapperId);
        if (!wrapper || !oldScale || !newScale) return;

        const contentX = (wrapper.scrollLeft + anchorX) / oldScale;
        const contentY = (wrapper.scrollTop + anchorY) / oldScale;

        wrapper.scrollLeft = Math.max(0, (contentX * newScale) - anchorX);
        wrapper.scrollTop = Math.max(0, (contentY * newScale) - anchorY);
    },

    getWidgetColor(type) {
        return WIDGET_COLORS[type] || '#455A64';
    }
};
