function wsStatusShow(connected) {
    var el = document.getElementById('ws-status');
    if (!el) return;
    clearTimeout(el._hideTimer);
    var label = el.dataset.overlay ? el.dataset.overlay + ' ' : '';
    if (connected) {
        el.textContent = label + 'Connected';
        el.classList.add('connected');
        el.classList.remove('hidden');
        el._hideTimer = setTimeout(function () { el.classList.add('hidden'); }, 2000);
    } else {
        el.textContent = label + 'Disconnected';
        el.classList.remove('connected', 'hidden');
    }
}
