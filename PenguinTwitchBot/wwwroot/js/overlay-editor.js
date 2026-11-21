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
        const active = document.activeElement;
        const isSave = (e.ctrlKey || e.metaKey) && key.toLowerCase() === 's';
        if (isSave) {
            e.preventDefault();
            if (active && (
                active.tagName === 'INPUT' ||
                active.tagName === 'TEXTAREA' ||
                active.tagName === 'SELECT'
            )) {
                active.blur();
                setTimeout(() => {
                    if (_dotnet) {
                        _dotnet.invokeMethodAsync('OnKeyboardSave');
                    }
                }, 0);
            } else {
                _dotnet.invokeMethodAsync('OnKeyboardSave');
            }
            return;
        }

        if (e.defaultPrevented) return;

        if (active) {
            const role = active.getAttribute ? active.getAttribute('role') : null;
            if (
                active.tagName === 'INPUT' ||
                active.tagName === 'TEXTAREA' ||
                active.tagName === 'SELECT' ||
                active.isContentEditable ||
                role === 'combobox' ||
                role === 'listbox' ||
                role === 'menu' ||
                role === 'menuitem' ||
                role === 'slider' ||
                role === 'spinbutton' ||
                (typeof active.closest === 'function' && active.closest('[role="combobox"], [role="listbox"], [role="menu"]'))
            ) {
                return;
            }
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
                        const el = event.target;
                        el.dataset.x = parseFloat(el.style.left) || 0;
                        el.dataset.y = parseFloat(el.style.top) || 0;
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
                        const prevLeftCanvas = Math.round((parseFloat(el.style.left) || 0) / _scale);
                        const prevTopCanvas = Math.round((parseFloat(el.style.top) || 0) / _scale);
                        const prevWidthCanvas = Math.max(10, Math.round((parseFloat(el.style.width) || 0) / _scale));
                        const prevHeightCanvas = Math.max(10, Math.round((parseFloat(el.style.height) || 0) / _scale));

                        const rawX = (parseFloat(el.dataset.x) || 0) + event.deltaRect.left;
                        const rawY = (parseFloat(el.dataset.y) || 0) + event.deltaRect.top;
                        el.dataset.x = rawX;
                        el.dataset.y = rawY;

                        let canvasX;
                        let canvasW;
                        if (event.edges && event.edges.left) {
                            const fixedRightCanvas = prevLeftCanvas + prevWidthCanvas;
                            canvasX = Math.min(snapValue(rawX / _scale), fixedRightCanvas - 10);
                            canvasW = Math.max(10, fixedRightCanvas - canvasX);
                        } else if (event.edges && event.edges.right) {
                            canvasX = prevLeftCanvas;
                            const snappedRightCanvas = snapValue((rawX + event.rect.width) / _scale);
                            canvasW = Math.max(10, snappedRightCanvas - canvasX);
                        } else {
                            canvasX = prevLeftCanvas;
                            canvasW = prevWidthCanvas;
                        }

                        let canvasY;
                        let canvasH;
                        if (event.edges && event.edges.top) {
                            const fixedBottomCanvas = prevTopCanvas + prevHeightCanvas;
                            canvasY = Math.min(snapValue(rawY / _scale), fixedBottomCanvas - 10);
                            canvasH = Math.max(10, fixedBottomCanvas - canvasY);
                        } else if (event.edges && event.edges.bottom) {
                            canvasY = prevTopCanvas;
                            const snappedBottomCanvas = snapValue((rawY + event.rect.height) / _scale);
                            canvasH = Math.max(10, snappedBottomCanvas - canvasY);
                        } else {
                            canvasY = prevTopCanvas;
                            canvasH = prevHeightCanvas;
                        }

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
                        const el = event.target;
                        el.dataset.x = parseFloat(el.style.left) || 0;
                        el.dataset.y = parseFloat(el.style.top) || 0;
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
