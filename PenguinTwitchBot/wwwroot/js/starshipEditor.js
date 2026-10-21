// Bridge between the Starship editor page and its preview iframes.
let latest = null;
let listener = null;

function frames() {
    return Array.from(document.querySelectorAll('iframe.starship-preview-frame'));
}

function post(target) {
    if (latest && target) target.postMessage({ type: 'starship_preview_appearance', appearance: latest }, window.location.origin);
}

export function start() {
    if (listener) return;
    listener = function (event) {
        if (event.origin !== window.location.origin) return;
        if (event.data && event.data.type === 'starship_preview_ready') post(event.source);
    };
    window.addEventListener('message', listener);
}

export function setAppearance(appearance) {
    latest = appearance;
    frames().forEach(function (frame) { post(frame.contentWindow); });
}

export function stop() {
    if (listener) window.removeEventListener('message', listener);
    listener = null;
}
