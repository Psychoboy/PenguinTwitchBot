(function () {
    const contentEl = document.getElementById("content");
    const overlayCardEl = document.querySelector(".overlay-card");
    const REFRESH_MS = 10000;

    function setOverlayVisible(isVisible) {
        if (!overlayCardEl) {
            return;
        }

        overlayCardEl.style.display = isVisible ? "flex" : "none";
    }

    function formatScore(value) {
        if (!Number.isFinite(value)) {
            return "0";
        }

        if (Math.abs(value % 1) < 0.0001) {
            return value.toLocaleString(undefined, { maximumFractionDigits: 0 });
        }

        return value.toLocaleString(undefined, {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2,
        });
    }

    function renderEmpty(message) {
        setOverlayVisible(false);
        contentEl.innerHTML = `<div class="empty">${message}</div>`;
    }

    function renderError(message) {
        setOverlayVisible(true);
        contentEl.innerHTML = `<div class="error">${message}</div>`;
    }

    function renderTournament(tournament) {
        const rows = tournament.standings.map((standing) => {
            return `
                <tr>
                    <td class="rank">#${standing.rank}</td>
                    <td>${standing.username}</td>
                    <td class="score">${formatScore(standing.score)}</td>
                    <td class="catches">${standing.catchCount}</td>
                </tr>`;
        }).join("");

        const bodyRows = rows || `<tr><td colspan="4" class="empty">No catches yet</td></tr>`;

        return `
            <section class="tournament">
                <div class="tournament-title">
                    <div class="tournament-name" title="${tournament.name}">${tournament.name}</div>
                    <div class="tournament-category">${tournament.scoreCategory}</div>
                </div>
                <table>
                    <thead>
                        <tr>
                            <th>#</th>
                            <th>Player</th>
                            <th class="score">Score</th>
                            <th class="catches">Catches</th>
                        </tr>
                    </thead>
                    <tbody>${bodyRows}</tbody>
                </table>
            </section>`;
    }

    async function loadStandings() {
        try {
            const response = await fetch("/api/overlay/fishing-tournaments-standings?top=5", { cache: "no-store" });
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const tournaments = await response.json();
            if (!Array.isArray(tournaments) || tournaments.length === 0) {
                renderEmpty("No active tournaments");
                return;
            }

            setOverlayVisible(true);
            contentEl.innerHTML = tournaments.map(renderTournament).join("");
        } catch (error) {
            console.error("Failed to load tournament standings overlay", error);
            renderError("Unable to load tournament standings");
        }
    }

    loadStandings();
    setInterval(loadStandings, REFRESH_MS);
})();
