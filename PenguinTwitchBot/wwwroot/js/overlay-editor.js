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
let _viewportEl = null;
let _isPanning = false;
let _lastPanX = 0;
let _lastPanY = 0;
let _viewportHandlers = null;

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

    init(dotnetHelper, scale) {
        _dotnet = dotnetHelper;
        _scale = scale || 0.5;
        if (_interactable) {
            _interactable.unset();
            _interactable = null;
        }

        attachViewportInteractions();

        // Selector targeting — automatically covers widgets added/removed later.
        _interactable = interact('.widget-tile')
            .draggable({
                modifiers: [],
                listeners: {
                    move(event) {
                        const el = event.target;
                        const x = (parseFloat(el.dataset.x) || 0) + event.dx;
                        const y = (parseFloat(el.dataset.y) || 0) + event.dy;
                        el.style.left = x + 'px';
                        el.style.top  = y + 'px';
                        el.dataset.x  = x;
                        el.dataset.y  = y;
                        _dotnet.invokeMethodAsync('OnWidgetMoved',
                            el.dataset.widgetId,
                            Math.round(x / _scale),
                            Math.round(y / _scale));
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
                        let x = (parseFloat(el.dataset.x) || 0) + event.deltaRect.left;
                        let y = (parseFloat(el.dataset.y) || 0) + event.deltaRect.top;
                        el.style.left   = x + 'px';
                        el.style.top    = y + 'px';
                        el.style.width  = event.rect.width  + 'px';
                        el.style.height = event.rect.height + 'px';
                        el.dataset.x = x;
                        el.dataset.y = y;
                        _dotnet.invokeMethodAsync('OnWidgetResized',
                            el.dataset.widgetId,
                            Math.round(x / _scale),
                            Math.round(y / _scale),
                            Math.round(event.rect.width  / _scale),
                            Math.round(event.rect.height / _scale));
                    },
                    end(event) {
                        _dotnet.invokeMethodAsync('OnDragEnd');
                    }
                }
            });
    },

    destroy() {
        if (_interactable) {
            _interactable.unset();
            _interactable = null;
        }
        detachViewportInteractions();
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
