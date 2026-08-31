(function () {
    var currentTab = 'tournaments';
    var refreshInterval = 30000;
    var refreshTimer = null;

    function formatScore(value) {
        if (!Number.isFinite(value)) {
            return '0';
        }
        if (Math.abs(value % 1) < 0.0001) {
            return value.toLocaleString(undefined, { maximumFractionDigits: 0 });
        }
        return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function renderError(message) {
        var banner = document.getElementById('error-banner');
        if (banner) {
            banner.textContent = message;
            banner.style.display = 'block';
            setTimeout(function () { banner.style.display = 'none'; }, 5000);
        }
    }

    function renderTournaments(tournaments) {
        var contentEl = document.getElementById('tournament-content');
        if (!contentEl) return;

        if (!Array.isArray(tournaments) || tournaments.length === 0) {
            contentEl.innerHTML = '<div class="empty">No active tournaments</div>';
            return;
        }

        contentEl.innerHTML = tournaments.map(function (tournament) {
            var rows = tournament.standings.map(function (standing) {
                return '<tr>' +
                    '<td class="rank">#' + standing.rank + '</td>' +
                    '<td>' + standing.username + '</td>' +
                    '<td class="score">' + formatScore(standing.score) + '</td>' +
                    '<td class="catches">' + standing.catchCount + '</td>' +
                    '</tr>';
            }).join('');

            if (!rows) {
                rows = '<tr><td colspan="4" class="empty">No catches yet</td></tr>';
            }

            return '<section class="tournament">' +
                '<div class="tournament-title">' +
                '<div class="tournament-name" title="' + tournament.name + '">' + tournament.name + '</div>' +
                '<div class="tournament-category">' + tournament.scoreCategory + '</div>' +
                '</div>' +
                '<table><thead><tr><th>#</th><th>Player</th><th class="score">Score</th><th class="catches">Catches</th></tr></thead><tbody>' + rows + '</tbody></table>' +
                '</section>';
        }).join('');
    }

    function renderCatches(catches) {
        var contentEl = document.getElementById('catch-content');
        if (!contentEl) return;

        if (!Array.isArray(catches) || catches.length === 0) {
            contentEl.innerHTML = '<div class="empty">No recent catches</div>';
            return;
        }

        contentEl.innerHTML = catches.map(function (catchEntry) {
            return '<div class="catch-row">' +
                '<span class="catch-user">' + catchEntry.username + '</span>' +
                '<span class="catch-details">' + catchEntry.fishName + '</span>' +
                '<span class="catch-weight">' + formatScore(catchEntry.weight) + ' kg</span>' +
                '</div>';
        }).join('');
    }

    async function loadTournaments() {
        try {
            var response = await window._extFetch('/api/twitch-extension/fishing-tournaments');
            var tournaments = await response.json();
            renderTournaments(tournaments);
        } catch (error) {
            console.error('Failed to load tournaments', error);
            renderError('Unable to load tournament standings');
        }
    }

    async function loadCatches() {
        try {
            var response = await window._extFetch('/api/twitch-extension/recent-catches');
            var catches = await response.json();
            renderCatches(catches);
        } catch (error) {
            console.error('Failed to load recent catches', error);
            renderError('Unable to load recent catches');
        }
    }

    function switchTab(tab) {
        currentTab = tab;
        var tabs = document.querySelectorAll('.tab');
        tabs.forEach(function (t) { t.classList.remove('active'); });
        var activeTab = document.querySelector('.tab[data-tab="' + tab + '"]');
        if (activeTab) activeTab.classList.add('active');

        var tournamentPanel = document.getElementById('tournaments-panel');
        var catchesPanel = document.getElementById('catches-panel');

        if (tournamentPanel) tournamentPanel.style.display = tab === 'tournaments' ? '' : 'none';
        if (catchesPanel) catchesPanel.style.display = tab === 'catches' ? '' : 'none';

        if (tab === 'tournaments') loadTournaments();
        if (tab === 'catches') loadCatches();
    }

    function startRefreshTimer() {
        if (refreshTimer) clearInterval(refreshTimer);
        refreshTimer = setInterval(function () {
            if (currentTab === 'tournaments') loadTournaments();
            if (currentTab === 'catches') loadCatches();
        }, refreshInterval);
    }

    async function init() {
        try {
            var config = await window._extGetConfig();
            if (config && config.refreshInterval) {
                refreshInterval = config.refreshInterval * 1000;
            }
        } catch (e) {
            refreshInterval = 30000;
        }

        var tabButtons = document.querySelectorAll('.tab');
        tabButtons.forEach(function (btn) {
            btn.addEventListener('click', function () {
                switchTab(btn.getAttribute('data-tab'));
            });
        });

        switchTab('tournaments');
        startRefreshTimer();
    }

    if (window._onExtensionReady) {
        init();
    } else {
        window._onExtensionReady = init;
    }
})();