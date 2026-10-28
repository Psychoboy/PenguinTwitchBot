// Packs variable-height tiles into a grid while keeping left-to-right order: each tile spans as many
// small rows as its content needs, so the next tile slides into the first free cell.
const ROW_UNIT = 8;
const GAP = 16;
const states = new WeakMap();

function place(item) {
    const content = item.firstElementChild;
    if (!content) return;
    const height = content.getBoundingClientRect().height;
    item.style.gridRowEnd = 'span ' + Math.max(1, Math.ceil((height + GAP) / ROW_UNIT));
}

export function init(container) {
    if (!container || states.has(container)) return;

    const observed = new Set();
    const resizeObserver = new ResizeObserver(entries => {
        for (const entry of entries) place(entry.target.parentElement);
    });

    const sync = () => {
        container.querySelectorAll(':scope > .homepage-masonry-item').forEach(item => {
            const content = item.firstElementChild;
            if (content && !observed.has(content)) {
                observed.add(content);
                resizeObserver.observe(content);
            }
            place(item);
        });
    };

    const mutationObserver = new MutationObserver(sync);
    mutationObserver.observe(container, { childList: true });

    container.classList.add('is-masonry');
    sync();
    states.set(container, { resizeObserver, mutationObserver });
}

export function dispose(container) {
    const state = states.get(container);
    if (!state) return;
    state.resizeObserver.disconnect();
    state.mutationObserver.disconnect();
    container.classList.remove('is-masonry');
    container.querySelectorAll(':scope > .homepage-masonry-item').forEach(item => { item.style.gridRowEnd = ''; });
    states.delete(container);
}
