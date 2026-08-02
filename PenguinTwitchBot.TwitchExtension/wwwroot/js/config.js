(function () {
    var form = document.getElementById('configForm');
    var feedback = document.getElementById('feedback');

    async function loadConfig() {
        try {
            var config = await window._extGetConfig();
            document.getElementById('tournamentCount').value = config.tournamentCount;
            document.getElementById('catchCount').value = config.catchCount;
            document.getElementById('refreshInterval').value = config.refreshInterval;
        } catch (error) {
            console.error('Failed to load configuration', error);
        }
    }

    async function saveConfig(event) {
        event.preventDefault();
        try {
            var config = {
                tournamentCount: parseInt(document.getElementById('tournamentCount').value, 10) || 5,
                catchCount: parseInt(document.getElementById('catchCount').value, 10) || 10,
                refreshInterval: parseInt(document.getElementById('refreshInterval').value, 10) || 30
            };
            await window._extSetConfig(config);
            feedback.textContent = 'Settings saved!';
            feedback.className = 'feedback success';
            setTimeout(function () {
                feedback.textContent = '';
                feedback.className = 'feedback';
            }, 3000);
        } catch (error) {
            console.error('Failed to save configuration', error);
            feedback.textContent = 'Failed to save settings.';
            feedback.className = 'feedback error';
        }
    }

    if (form) {
        form.addEventListener('submit', saveConfig);
    }

    if (window.twitch && window.twitch.ext && window.twitch.ext.onAuthorized) {
        window.twitch.ext.onAuthorized(function () {
            loadConfig();
        });
        setTimeout(loadConfig, 2000);
    } else {
        loadConfig();
    }
})();