(function () {
    const contentEl = document.getElementById("content");
    const overlayCardEl = document.querySelector(".overlay-card");
    const leadBannerEl = document.getElementById("lead-banner");
    const FALLBACK_POLL_MS = 60000;

    let cachedTournaments = [];
    let previousRanks = new Map();
    let knownTournamentIds = new Set();
    let standingsRequestSeq = 0;
    let leadBannerTimeout = null;
    let podiumActiveUntil = 0;
    let podiumTimeout = null;
    let tournamentRevealUntilMs = 0;
    let lastTournamentReminderAtMs = 0;
    let overlayConfig = {
        tournamentOverlayMode: "Auto",
        tournamentReminderIntervalMinutes: 5,
        tournamentReminderDurationSeconds: 15,
        autoAlwaysThresholdMinutes: 15
    };

    function applyOverlayConfig(cfg) {
        if (!cfg || typeof cfg !== "object") return;
        overlayConfig = {
            tournamentOverlayMode: cfg.tournamentOverlayMode || overlayConfig.tournamentOverlayMode || "Auto",
            tournamentReminderIntervalMinutes: Math.max(1, Number(cfg.tournamentReminderIntervalMinutes) || overlayConfig.tournamentReminderIntervalMinutes || 5),
            tournamentReminderDurationSeconds: Math.max(5, Number(cfg.tournamentReminderDurationSeconds) || overlayConfig.tournamentReminderDurationSeconds || 15),
            autoAlwaysThresholdMinutes: Math.max(1, Number(cfg.autoAlwaysThresholdMinutes) || overlayConfig.autoAlwaysThresholdMinutes || 15)
        };
        evaluateTournamentVisibility();
    }

    async function loadOverlayConfig() {
        try {
            const res = await fetch("/api/overlay/fishing-overlay-config", { cache: "no-store" });
            if (res.ok) {
                applyOverlayConfig(await res.json());
            }
        } catch (_) {}
    }

    function flashTournamentOverlay() {
        const durationMs = (overlayConfig.tournamentReminderDurationSeconds || 15) * 1000;
        tournamentRevealUntilMs = Math.max(tournamentRevealUntilMs, Date.now() + durationMs);
        evaluateTournamentVisibility();
    }

    function evaluateTournamentVisibility() {
        const now = Date.now();
        if (now < podiumActiveUntil) {
            setOverlayVisible(true);
            return;
        }
        if (!cachedTournaments || cachedTournaments.length === 0) {
            setOverlayVisible(false);
            return;
        }

        const mode = (overlayConfig.tournamentOverlayMode || "Auto").toLowerCase();
        const thresholdMin = overlayConfig.autoAlwaysThresholdMinutes || 15;
        const hasShortTournament = cachedTournaments.some((t) => {
            const runMin = Number(t.runDurationMinutes || 0);
            if (runMin > 0 && runMin <= thresholdMin) return true;
            if (t.endsAtUtc) {
                const remMin = (Date.parse(t.endsAtUtc) - now) / 60000;
                return Number.isFinite(remMin) && remMin > 0 && remMin <= 2;
            }
            return false;
        });
        const inFinalCountdown = cachedTournaments.some((t) => {
            if (!t.endsAtUtc) return false;
            const remSec = (Date.parse(t.endsAtUtc) - now) / 1000;
            return Number.isFinite(remSec) && remSec > 0 && remSec <= 120;
        });

        let shouldShow = false;
        if (mode === "always" || (mode === "auto" && hasShortTournament)) {
            shouldShow = true;
        } else if (mode === "oncast") {
            shouldShow = inFinalCountdown || now < tournamentRevealUntilMs;
        } else if (mode === "periodic" || mode === "auto") {
            const intervalMs = (overlayConfig.tournamentReminderIntervalMinutes || 5) * 60000;
            const durationMs = (overlayConfig.tournamentReminderDurationSeconds || 15) * 1000;
            if (now - lastTournamentReminderAtMs >= intervalMs) {
                lastTournamentReminderAtMs = now;
                tournamentRevealUntilMs = Math.max(tournamentRevealUntilMs, now + durationMs);
            }
            shouldShow = inFinalCountdown || now < tournamentRevealUntilMs;
        }

        setOverlayVisible(shouldShow);
    }

    function escapeHtml(str) {
        if (str === null || str === undefined) return "";
        return String(str)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function setOverlayVisible(isVisible) {
        if (overlayCardEl) {
            overlayCardEl.style.display = isVisible ? "flex" : "none";
        }
    }

    function formatCompactScore(value) {
        if (!Number.isFinite(value)) return "0";
        const abs = Math.abs(value);
        const sign = value < 0 ? "-" : "";
        if (abs >= 10000) {
            const trim = (s) => (s >= 100 ? Number(s.toFixed(1)) : Number(s.toFixed(2))).toString();
            if (abs < 1e6) return `${sign}${trim(abs / 1e3)}K`;
            if (abs < 1e9) return `${sign}${trim(abs / 1e6)}M`;
            if (abs < 1e12) return `${sign}${trim(abs / 1e9)}B`;
            return `${sign}${trim(abs / 1e12)}T`;
        }
        if (Math.abs(value % 1) < 0.0001) {
            return value.toLocaleString(undefined, { maximumFractionDigits: 0 });
        }
        return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function formatCountdown(endsAtUtc) {
        if (!endsAtUtc) return { text: "LIVE", cls: "" };
        const diffSec = Math.max(0, Math.floor((Date.parse(endsAtUtc) - Date.now()) / 1000));
        if (!Number.isFinite(diffSec)) return { text: "LIVE", cls: "" };
        if (diffSec <= 0) return { text: "ENDING", cls: "urgent" };

        const h = Math.floor(diffSec / 3600);
        const m = Math.floor((diffSec % 3600) / 60);
        const s = diffSec % 60;
        const text = h > 0 ? `${h}h ${m}m` : `${m}:${String(s).padStart(2, "0")}`;
        const cls = diffSec <= 30 ? "urgent" : diffSec <= 120 ? "warning" : "";
        return { text, cls };
    }

    function showLeadBanner(data) {
        if (!leadBannerEl || !data || !data.newLeader) return;
        leadBannerEl.textContent = `👑 NEW LEADER: ${data.newLeader} (${formatCompactScore(Number(data.score))})!`;
        leadBannerEl.classList.add("show");
        if (leadBannerTimeout) clearTimeout(leadBannerTimeout);
        leadBannerTimeout = setTimeout(() => {
            leadBannerEl.classList.remove("show");
        }, 5000);
    }

    function showPodiumCelebration(data) {
        if (!data) return;
        podiumActiveUntil = Date.now() + 15000;
        setOverlayVisible(true);

        if (data.soundEnabled && data.soundUrl) {
            try {
                const audio = new Audio(data.soundUrl);
                audio.volume = Math.max(0, Math.min(1, Number(data.soundVolume ?? 0.8)));
                audio.play().catch(() => {});
            } catch (_) {}
        }

        const medals = ["🥇", "🥈", "🥉"];
        const podiumRows = Array.isArray(data.podium) && data.podium.length > 0
            ? data.podium.map((p) => {
                const medal = medals[p.rank - 1] || `#${p.rank}`;
                const rewardsHtml = Array.isArray(p.rewards) && p.rewards.length > 0
                    ? `<div class="podium-rewards">${p.rewards.map((r) => `<span class="podium-pill">${escapeHtml(r)}</span>`).join("")}</div>`
                    : "";
                return `
                    <div class="podium-row place-${p.rank}">
                        <div><strong>${medal} ${escapeHtml(p.username)}</strong> <span style="opacity:0.8">(${formatCompactScore(Number(p.score))})</span></div>
                        ${rewardsHtml}
                    </div>`;
            }).join("")
            : `<div class="empty">No catches recorded</div>`;

        contentEl.innerHTML = `
            <div class="podium-card">
                <div class="podium-title">🏆 ${escapeHtml(data.name)} — COMPLETE!</div>
                <div class="podium-sub">Final Podium Results</div>
                ${podiumRows}
            </div>`;

        if (podiumTimeout) clearTimeout(podiumTimeout);
        podiumTimeout = setTimeout(() => {
            podiumActiveUntil = 0;
            loadStandings();
        }, 15000);
    }

    function renderTournament(tournament) {
        const cd = formatCountdown(tournament.endsAtUtc);
        const rows = (tournament.standings || []).map((standing) => {
            const key = `${tournament.id}:${standing.username}`;
            const prevRank = previousRanks.get(key);
            const movedUp = prevRank !== undefined && standing.rank < prevRank;
            previousRanks.set(key, standing.rank);
            const rankClass = standing.rank <= 3 ? `rank rank-${standing.rank}` : "rank";
            const medal = standing.rank === 1 ? "🥇" : standing.rank === 2 ? "🥈" : standing.rank === 3 ? "🥉" : `#${escapeHtml(standing.rank)}`;
            return `
                <tr class="${movedUp ? "rank-up" : ""}">
                    <td class="${rankClass}">${medal}</td>
                    <td>${escapeHtml(standing.username)}</td>
                    <td class="score">${formatCompactScore(standing.score)}</td>
                    <td class="catches">${escapeHtml(standing.catchCount)}</td>
                </tr>`;
        }).join("");

        const bodyRows = rows || `<tr><td colspan="4" class="empty">Cast a line to take #1!</td></tr>`;
        const subParts = [tournament.scoreCategory];
        if (tournament.eligibleSummary) subParts.push(tournament.eligibleSummary);

        return `
            <section class="tournament" data-id="${escapeHtml(tournament.id)}">
                <div class="tournament-title">
                    <div class="tournament-meta">
                        <div class="tournament-name" title="${escapeHtml(tournament.name)}">${escapeHtml(tournament.name)}</div>
                        <div class="tournament-sub">${escapeHtml(subParts.join(" • "))}</div>
                    </div>
                    <div class="countdown-pill ${cd.cls}" data-ends="${escapeHtml(tournament.endsAtUtc || "")}">${escapeHtml(cd.text)}</div>
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

    function updateCountdowns() {
        if (Date.now() < podiumActiveUntil) return;
        const pills = contentEl.querySelectorAll(".countdown-pill[data-ends]");
        pills.forEach((pill) => {
            const ends = pill.getAttribute("data-ends");
            if (!ends) return;
            const cd = formatCountdown(ends);
            pill.textContent = cd.text;
            pill.className = `countdown-pill ${cd.cls}`;
        });
        evaluateTournamentVisibility();
    }

    async function loadStandings(triggerFlash = false) {
        if (Date.now() < podiumActiveUntil) return;
        const requestSeq = ++standingsRequestSeq;
        try {
            const response = await fetch("/api/overlay/fishing-tournaments-standings?top=5", { cache: "no-store" });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            const tournaments = await response.json();
            if (requestSeq !== standingsRequestSeq || Date.now() < podiumActiveUntil) return;

            cachedTournaments = Array.isArray(tournaments) ? tournaments : [];

            if (cachedTournaments.length === 0) {
                knownTournamentIds.clear();
                setOverlayVisible(false);
                contentEl.innerHTML = `<div class="empty">No active tournaments</div>`;
                return;
            }

            let hasNewTournament = false;
            const nextIds = new Set();
            for (const t of cachedTournaments) {
                nextIds.add(t.id);
                if (!knownTournamentIds.has(t.id)) {
                    hasNewTournament = true;
                }
            }
            knownTournamentIds = nextIds;

            contentEl.innerHTML = cachedTournaments.map(renderTournament).join("");
            if (hasNewTournament) {
                lastTournamentReminderAtMs = Date.now();
                flashTournamentOverlay();
            } else if (triggerFlash) {
                flashTournamentOverlay();
            } else {
                evaluateTournamentVisibility();
            }
        } catch (error) {
            console.error("Failed to load tournament standings overlay", error);
        }
    }

    function connectWS() {
        if (typeof createWsSocket !== "function") return;
        try {
            const ws = createWsSocket("FishingTournaments", [WS_TOPICS.fishing]);
            ws.addEventListener("message", (msg) => {
                if (msg.data === "ping") {
                    ws.send("pong");
                    return;
                }
                try {
                    const data = JSON.parse(msg.data);
                    if (data.fishingOverlayConfig) {
                        applyOverlayConfig(data.fishingOverlayConfig);
                    }
                    if (data.tournamentEnded) {
                        showPodiumCelebration(data.tournamentEnded);
                        return;
                    }
                    if (data.tournamentLeadChange) {
                        showLeadBanner(data.tournamentLeadChange);
                    }
                    if (data.tournamentUpdate || data.tournamentLeadChange || data.fishing) {
                        const mode = (overlayConfig.tournamentOverlayMode || "Auto").toLowerCase();
                        const shouldFlash = Boolean(data.tournamentUpdate || data.tournamentLeadChange) || mode !== "periodic";
                        loadStandings(shouldFlash);
                    }
                } catch (_) {}
            });
        } catch (err) {
            console.error("Tournament WS error:", err);
        }
    }

    loadOverlayConfig().then(() => loadStandings(false));
    connectWS();
    setInterval(updateCountdowns, 1000);
    setInterval(() => loadStandings(false), FALLBACK_POLL_MS);
})();
