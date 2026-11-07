const handlers = new WeakMap();

export function initialize(container) {
    if (handlers.has(container)) return;

    const update = event => {
        const audio = event.target;
        const control = audio.closest('.audio-preview-control');
        if (!control) return;
        const button = control.querySelector('.audio-preview-button');
        const playing = !audio.paused && !audio.ended;
        const action = playing ? 'Pause' : 'Play';
        button.setAttribute('aria-pressed', String(playing));
        button.setAttribute('aria-label', `${action} preview of ${control.dataset.command}`);
        button.title = `${action} preview`;
    };

    const click = event => {
        const button = event.target.closest('.audio-preview-button');
        if (!button || !container.contains(button)) return;
        const audio = button.closest('.audio-preview-control').querySelector('audio');
        if (!audio.paused) {
            audio.pause();
            return;
        }
        container.querySelectorAll('audio').forEach(other => {
            if (other !== audio) other.pause();
        });
        audio.play().catch(() => {
            update({ target: audio });
            button.title = 'Unable to play preview';
        });
    };

    container.addEventListener('click', click);
    ['play', 'pause', 'ended', 'error'].forEach(name => container.addEventListener(name, update, true));
    const observer = new MutationObserver(records => {
        records.forEach(record => record.removedNodes.forEach(node => {
            if (node.nodeType !== 1 || container.contains(node)) return;
            if (node.matches('audio')) node.pause();
            node.querySelectorAll('audio').forEach(audio => audio.pause());
        }));
    });
    observer.observe(container, { childList: true, subtree: true });
    handlers.set(container, { click, update, observer });
}

export function dispose(container) {
    const handler = handlers.get(container);
    if (!handler) return;
    handler.observer.disconnect();
    container.querySelectorAll('audio').forEach(audio => audio.pause());
    container.removeEventListener('click', handler.click);
    ['play', 'pause', 'ended', 'error'].forEach(name => container.removeEventListener(name, handler.update, true));
    handlers.delete(container);
}