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

window.overlayEditor = {

    init(dotnetHelper, scale) {
        _dotnet = dotnetHelper;
        _scale = scale || 0.5;
        if (_interactable) {
            _interactable.unset();
            _interactable = null;
        }

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
    },

    getWidgetColor(type) {
        return WIDGET_COLORS[type] || '#455A64';
    }
};
