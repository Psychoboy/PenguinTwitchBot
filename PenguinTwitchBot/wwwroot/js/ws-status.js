function wsStatusShow(connected) {
    var el = document.getElementById('ws-status');
    if (!el) return;
    clearTimeout(el._hideTimer);
    if (connected) {
        el.textContent = 'Connected';
        el.classList.add('connected');
        el.classList.remove('hidden');
        el._hideTimer = setTimeout(function () { el.classList.add('hidden'); }, 2000);
    } else {
        el.textContent = 'Disconnected';
        el.classList.remove('connected', 'hidden');
    }
}
