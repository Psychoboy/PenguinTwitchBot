(function () {
    const queryParams = new URLSearchParams(window.location.search);
    const isDebug = queryParams.get('debug') === '1';
    const overlayKey = (queryParams.get('key') || '').trim();

    let ws = null;
    let isProcessing = false;
    const queue = [];
    let scaleFrameHandle = 0;

    // Running combo tracker for multi-cast batches
    let comboState = { username: '', totalGold: 0, catches: 0, batchTotal: 1 };

    // Active Frenzy state & visibility config
    let frenzyStateVersion = 0;
    let frenzyStartedAtMs = 0;
    let frenzyEndsAtMs = 0;
    let frenzyTimerHandle = null;
    let frenzyFlashUntilMs = 0;
    let lastFrenzyReminderAtMs = 0;
    let overlayConfig = {
        frenzyBannerMode: 'Auto',
        frenzyReminderIntervalMinutes: 5,
        frenzyReminderDurationSeconds: 12,
        autoAlwaysThresholdMinutes: 15
    };

    // Active audio references
    let currentCatchAudio = null;

    // DOM elements
    const containerEl = document.querySelector('.container');
    const frenzyPillEl = document.getElementById('frenzy-pill');
    const frenzyTitleEl = document.getElementById('frenzy-title');
    const frenzyTimerEl = document.getElementById('frenzy-timer');

    const castStageEl = document.getElementById('cast-stage');
    const castCanvas = document.getElementById('cast-canvas');
    const castCtx = castCanvas ? castCanvas.getContext('2d') : null;
    const castUsernameEl = document.getElementById('cast-username');
    const castComboTagEl = document.getElementById('cast-combo-tag');

    const alertEl = document.getElementById('fishing-alert');
    const cardContentEl = document.getElementById('card-content');
    const fxCanvas = document.getElementById('fx-canvas');
    const fxCtx = fxCanvas ? fxCanvas.getContext('2d') : null;
    const usernameEl = document.getElementById('username');
    const comboPillEl = document.getElementById('combo-pill');
    const badgesRowEl = document.getElementById('badges-row');
    const accidentIconEl = document.getElementById('accident-icon');
    const fishImageEl = document.getElementById('fish-image');
    const fishNameEl = document.getElementById('fish-name');
    const rarityEl = document.getElementById('rarity');
    const starsEl = document.getElementById('stars');
    const statsRowEl = document.getElementById('stats-row');
    const weightEl = document.getElementById('weight');
    const goldEl = document.getElementById('gold');
    const rewardsRowEl = document.getElementById('rewards-row');
    const accidentDetailsEl = document.getElementById('accident-details');
    const comboFooterEl = document.getElementById('combo-footer');

    function logDebug(...args) {
        if (isDebug) console.log('[FishingOverlay]', ...args);
    }

    function sleep(ms) {
        return new Promise((resolve) => setTimeout(resolve, ms));
    }

    function toFiniteNumber(value, fallback) {
        const parsed = typeof value === 'string' ? Number.parseFloat(value.trim()) : Number(value);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    function formatCompactNumber(value) {
        const num = toFiniteNumber(value, 0);
        const abs = Math.abs(num);
        const sign = num < 0 ? '-' : '';
        if (abs < 10000) return Math.round(num).toLocaleString();
        const trim = (scaled) => (scaled >= 100 ? Number(scaled.toFixed(1)) : Number(scaled.toFixed(2))).toString();
        if (abs < 1e6) return `${sign}${trim(abs / 1e3)}K`;
        if (abs < 1e9) return `${sign}${trim(abs / 1e6)}M`;
        if (abs < 1e12) return `${sign}${trim(abs / 1e9)}B`;
        return `${sign}${trim(abs / 1e12)}T`;
    }

    function normalizeRarity(rarity) {
        const key = typeof rarity === 'string' ? rarity.trim().toLowerCase() : '';
        const map = {
            common: 'Common',
            uncommon: 'Uncommon',
            rare: 'Rare',
            epic: 'Epic',
            legendary: 'Legendary',
            mythical: 'Mythical',
            accident: 'Accident'
        };
        return map[key] ? { label: map[key], cssClass: key } : { label: 'Common', cssClass: 'common' };
    }

    function playSound(url, enabled, volume) {
        if (!enabled || !url) return null;
        try {
            const linear = Math.max(0, Math.min(1, toFiniteNumber(volume, 0.8)));
            const gain = linear * linear;
            if (gain <= 0) return null;
            const audio = new Audio(url);
            audio.volume = gain;
            audio.play().catch((err) => logDebug('Audio play skipped:', err));
            return audio;
        } catch (err) {
            logDebug('Audio error:', err);
            return null;
        }
    }

    function stopCurrentCatchAudio() {
        if (currentCatchAudio) {
            try {
                currentCatchAudio.pause();
                currentCatchAudio.currentTime = 0;
            } catch (_) {}
            currentCatchAudio = null;
        }
    }

    // --- Frenzy Banner ---
    function applyOverlayConfig(cfg) {
        if (!cfg || typeof cfg !== 'object') return;
        overlayConfig = {
            frenzyBannerMode: cfg.frenzyBannerMode || overlayConfig.frenzyBannerMode || 'Auto',
            frenzyReminderIntervalMinutes: Math.max(1, toFiniteNumber(cfg.frenzyReminderIntervalMinutes, overlayConfig.frenzyReminderIntervalMinutes || 5)),
            frenzyReminderDurationSeconds: Math.max(5, toFiniteNumber(cfg.frenzyReminderDurationSeconds, overlayConfig.frenzyReminderDurationSeconds || 12)),
            autoAlwaysThresholdMinutes: Math.max(1, toFiniteNumber(cfg.autoAlwaysThresholdMinutes, overlayConfig.autoAlwaysThresholdMinutes || 15))
        };
        evaluateFrenzyBannerVisibility();
    }

    async function loadOverlayConfig() {
        try {
            const res = await fetch('/api/overlay/fishing-overlay-config', { cache: 'no-store' });
            if (res.ok) {
                applyOverlayConfig(await res.json());
            }
        } catch (_) {}
    }

    function evaluateFrenzyBannerVisibility() {
        if (!frenzyPillEl) return;
        const now = Date.now();
        if (frenzyEndsAtMs <= now) {
            frenzyPillEl.classList.remove('show');
            return;
        }

        const mode = (overlayConfig.frenzyBannerMode || 'Auto').toLowerCase();
        if (mode === 'hidden') {
            frenzyPillEl.classList.remove('show');
            return;
        }

        const remSec = Math.max(0, Math.ceil((frenzyEndsAtMs - now) / 1000));
        const totalDurationMin = frenzyStartedAtMs > 0
            ? (frenzyEndsAtMs - frenzyStartedAtMs) / 60000
            : (frenzyEndsAtMs - now) / 60000;
        const isShortEvent = totalDurationMin <= (overlayConfig.autoAlwaysThresholdMinutes || 15) + 0.25;
        const isFinalCountdown = remSec <= 120;
        const isCastingNow = isProcessing || queue.length > 0;

        let shouldShow = false;
        if (mode === 'always' || (mode === 'auto' && isShortEvent)) {
            shouldShow = true;
        } else if (mode === 'oncast') {
            shouldShow = isCastingNow || isFinalCountdown || now < frenzyFlashUntilMs;
        } else if (mode === 'periodic' || mode === 'auto') {
            const intervalMs = (overlayConfig.frenzyReminderIntervalMinutes || 5) * 60000;
            const durationMs = (overlayConfig.frenzyReminderDurationSeconds || 12) * 1000;
            if (now - lastFrenzyReminderAtMs >= intervalMs) {
                lastFrenzyReminderAtMs = now;
                frenzyFlashUntilMs = Math.max(frenzyFlashUntilMs, now + durationMs);
            }
            shouldShow = (mode === 'auto' && isCastingNow) || isFinalCountdown || now < frenzyFlashUntilMs;
        }

        const wasShown = frenzyPillEl.classList.contains('show');
        frenzyPillEl.classList.toggle('show', shouldShow);
        if (wasShown !== shouldShow) {
            scheduleScaleContainer();
        }
    }

    function updateFrenzyBanner(data) {
        if (!data || !data.active || !data.endsAtUtc) {
            frenzyStartedAtMs = 0;
            frenzyEndsAtMs = 0;
            frenzyFlashUntilMs = 0;
            frenzyPillEl.classList.remove('show');
            if (frenzyTimerHandle) {
                clearInterval(frenzyTimerHandle);
                frenzyTimerHandle = null;
            }
            scheduleScaleContainer();
            return;
        }

        const endsMs = Date.parse(data.endsAtUtc);
        if (!Number.isFinite(endsMs) || endsMs <= Date.now()) {
            updateFrenzyBanner(null);
            return;
        }

        const parsedStart = data.startedAtUtc ? Date.parse(data.startedAtUtc) : NaN;
        const isNewFrenzy = Math.abs(endsMs - frenzyEndsAtMs) > 2000;
        frenzyStartedAtMs = Number.isFinite(parsedStart) ? parsedStart : Date.now();
        frenzyEndsAtMs = endsMs;

        if (isNewFrenzy) {
            const flashMs = (overlayConfig.frenzyReminderDurationSeconds || 12) * 1000;
            frenzyFlashUntilMs = Date.now() + flashMs;
            lastFrenzyReminderAtMs = Date.now();
        }

        const summary = data.bonusSummary ? ` • ${data.bonusSummary}` : '';
        frenzyTitleEl.textContent = `🔥 ${data.name || 'Fishing Frenzy'}${summary}`;

        if (data.soundEnabled && data.soundUrl) {
            playSound(data.soundUrl, true, data.soundVolume);
        }

        const tick = () => {
            const remSec = Math.max(0, Math.ceil((frenzyEndsAtMs - Date.now()) / 1000));
            if (remSec <= 0) {
                updateFrenzyBanner(null);
                return;
            }
            const m = Math.floor(remSec / 60);
            const s = remSec % 60;
            frenzyTimerEl.textContent = `${m}:${String(s).padStart(2, '0')}`;
            evaluateFrenzyBannerVisibility();
        };

        tick();
        if (frenzyTimerHandle) clearInterval(frenzyTimerHandle);
        frenzyTimerHandle = setInterval(tick, 1000);
        scheduleScaleContainer();
    }

    async function loadInitialFrenzyState() {
        const versionAtStart = frenzyStateVersion;
        try {
            const res = await fetch('/api/overlay/fishing-frenzy-state', { cache: 'no-store' });
            if (res.ok && frenzyStateVersion === versionAtStart) {
                const data = await res.json();
                if (frenzyStateVersion === versionAtStart && data && data.active) {
                    updateFrenzyBanner(data);
                }
            }
        } catch (_) {}
    }

    // --- LurkBait-style Animated Waterline / Bobber Canvas ---
    function playCastAnimation(username, durationMs, batchIndex, batchTotal, soundEnabled, soundVolume, castSoundUrl, biteSoundUrl) {
        return new Promise((resolve) => {
            if (!castCtx || !castStageEl) {
                resolve();
                return;
            }

            castUsernameEl.textContent = `🎣 ${username} casts...`;
            if (batchTotal > 1) {
                castComboTagEl.textContent = `${batchIndex} / ${batchTotal}`;
                castComboTagEl.style.display = 'inline-block';
            } else {
                castComboTagEl.style.display = 'none';
            }

            castStageEl.style.display = 'block';
            castStageEl.offsetHeight;
            scheduleScaleContainer();
            castStageEl.classList.add('show');

            playSound(castSoundUrl, soundEnabled, soundVolume);

            const W = castCanvas.width;
            const H = castCanvas.height;
            const waterY = H * 0.62;
            const targetX = W * 0.48;
            const startTime = performance.now();
            const totalMs = Math.max(450, durationMs);
            const castEnd = 0.34;
            const biteStart = 0.62;
            const reelStart = 0.80;
            let biteSoundPlayed = false;

            const droplets = [];
            for (let i = 0; i < 14; i++) {
                droplets.push({
                    vx: (Math.random() - 0.5) * 4.2,
                    vy: -2.5 - Math.random() * 4.0,
                    r: 1.8 + Math.random() * 2.2
                });
            }

            function drawFrame(now) {
                const elapsed = now - startTime;
                const p = Math.min(1, elapsed / totalMs);

                castCtx.clearRect(0, 0, W, H);

                // Water gradient & gentle animated sine waves
                const grad = castCtx.createLinearGradient(0, waterY - 8, 0, H);
                grad.addColorStop(0, 'rgba(38, 166, 232, 0.55)');
                grad.addColorStop(0.5, 'rgba(14, 98, 168, 0.72)');
                grad.addColorStop(1, 'rgba(6, 40, 82, 0.92)');
                castCtx.fillStyle = grad;
                castCtx.beginPath();
                castCtx.moveTo(0, H);
                for (let x = 0; x <= W; x += 8) {
                    const y = waterY + Math.sin(x * 0.045 + elapsed * 0.005) * 3.5 + Math.cos(x * 0.025 - elapsed * 0.003) * 2;
                    castCtx.lineTo(x, y);
                }
                castCtx.lineTo(W, H);
                castCtx.closePath();
                castCtx.fill();

                // Highlight shimmer line on water surface
                castCtx.strokeStyle = 'rgba(190, 238, 255, 0.65)';
                castCtx.lineWidth = 2;
                castCtx.beginPath();
                for (let x = 0; x <= W; x += 8) {
                    const y = waterY + Math.sin(x * 0.045 + elapsed * 0.005) * 3.5 + Math.cos(x * 0.025 - elapsed * 0.003) * 2;
                    if (x === 0) castCtx.moveTo(x, y);
                    else castCtx.lineTo(x, y);
                }
                castCtx.stroke();

                // Compute bobber position
                let bx = targetX;
                let by = waterY;
                const rodTipX = W - 18;
                const rodTipY = 26;

                if (p < castEnd) {
                    const t = p / castEnd;
                    bx = rodTipX + (targetX - rodTipX) * t;
                    by = rodTipY + (waterY - rodTipY) * t - Math.sin(t * Math.PI) * 58;
                } else if (p < biteStart) {
                    const t = (p - castEnd) / (biteStart - castEnd);
                    by = waterY + Math.sin(t * Math.PI * 5) * 4;
                    // Expanding water ripples
                    for (let r = 0; r < 2; r++) {
                        const rp = (t * 1.4 + r * 0.45) % 1;
                        castCtx.strokeStyle = `rgba(200, 244, 255, ${(1 - rp) * 0.7})`;
                        castCtx.lineWidth = 1.5;
                        castCtx.beginPath();
                        castCtx.ellipse(targetX, waterY + 4, 10 + rp * 48, 3 + rp * 11, 0, 0, Math.PI * 2);
                        castCtx.stroke();
                    }
                } else if (p < reelStart) {
                    if (!biteSoundPlayed) {
                        biteSoundPlayed = true;
                        castUsernameEl.textContent = `❗ ${username} got a BITE!`;
                        playSound(biteSoundUrl, soundEnabled, soundVolume);
                    }
                    const t = (p - biteStart) / (reelStart - biteStart);
                    by = waterY + 12 + Math.sin(t * Math.PI * 8) * 3;
                    // Splash rings
                    castCtx.strokeStyle = `rgba(255, 240, 150, ${(1 - t) * 0.9})`;
                    castCtx.lineWidth = 2.2;
                    castCtx.beginPath();
                    castCtx.ellipse(targetX, waterY + 4, 14 + t * 54, 4 + t * 14, 0, 0, Math.PI * 2);
                    castCtx.stroke();

                    // Bright "!" bite exclamation
                    const pop = Math.min(1, t * 4);
                    castCtx.save();
                    castCtx.font = `900 ${Math.round(28 * pop)}px Segoe UI, sans-serif`;
                    castCtx.textAlign = 'center';
                    castCtx.fillStyle = '#ffd740';
                    castCtx.strokeStyle = '#1b1100';
                    castCtx.lineWidth = 4;
                    castCtx.strokeText('!', targetX, waterY - 24 - t * 10);
                    castCtx.fillText('!', targetX, waterY - 24 - t * 10);
                    castCtx.restore();
                } else {
                    const t = (p - reelStart) / (1 - reelStart);
                    by = waterY + 8 - t * 88;
                    bx = targetX + Math.sin(t * Math.PI * 4) * 5;
                    // Splash droplets bursting upward
                    castCtx.fillStyle = 'rgba(205, 242, 255, 0.85)';
                    for (const d of droplets) {
                        const dx = targetX + d.vx * t * 18;
                        const dy = waterY + d.vy * t * 16 + 0.5 * 34 * t * t;
                        castCtx.beginPath();
                        castCtx.arc(dx, dy, d.r * (1 - t * 0.5), 0, Math.PI * 2);
                        castCtx.fill();
                    }
                }

                // Fishing line from rod tip to bobber
                castCtx.strokeStyle = 'rgba(240, 248, 255, 0.78)';
                castCtx.lineWidth = 1.5;
                castCtx.beginPath();
                castCtx.moveTo(rodTipX, rodTipY);
                const midX = (rodTipX + bx) * 0.5;
                const sag = p >= biteStart ? -8 : 16;
                castCtx.quadraticCurveTo(midX, (rodTipY + by) * 0.5 + sag, bx, by - 7);
                castCtx.stroke();

                // Red & white bobber
                castCtx.save();
                castCtx.translate(bx, by);
                castCtx.beginPath();
                castCtx.arc(0, 0, 8.5, Math.PI, 0);
                castCtx.fillStyle = '#ff3d3d';
                castCtx.fill();
                castCtx.beginPath();
                castCtx.arc(0, 0, 8.5, 0, Math.PI);
                castCtx.fillStyle = '#ffffff';
                castCtx.fill();
                castCtx.strokeStyle = 'rgba(0,0,0,0.5)';
                castCtx.lineWidth = 1.2;
                castCtx.beginPath();
                castCtx.arc(0, 0, 8.5, 0, Math.PI * 2);
                castCtx.stroke();
                castCtx.restore();

                if (p < 1) {
                    requestAnimationFrame(drawFrame);
                } else {
                    castStageEl.classList.remove('show');
                    castStageEl.style.display = 'none';
                    resolve();
                }
            }

            requestAnimationFrame(drawFrame);
        });
    }

    // --- Particle Burst on Reveal Card (Rare+, New Dex, Server Record) ---
    function launchParticleBurst(rarityClass, extraHype) {
        if (!fxCtx || !fxCanvas) return;
        const count = rarityClass === 'mythical' ? 46 :
                      rarityClass === 'legendary' ? 38 :
                      rarityClass === 'epic' ? 24 :
                      (rarityClass === 'rare' || extraHype) ? 16 : 0;

        if (count === 0) {
            fxCtx.clearRect(0, 0, fxCanvas.width, fxCanvas.height);
            return;
        }

        const palettes = {
            mythical: ['#ff2d55', '#ff80ab', '#ffd740', '#ea80fc', '#ffffff'],
            legendary: ['#ffd700', '#ffab00', '#fff59d', '#ffffff'],
            epic: ['#ce93d8', '#ab47bc', '#e1bee7', '#ffd740'],
            rare: ['#4fc3f7', '#29b6f6', '#e1f5fe', '#ffd740']
        };
        const colors = palettes[rarityClass] || ['#ffd740', '#69f0ae', '#40c4ff'];

        const W = fxCanvas.width;
        const H = fxCanvas.height;
        const cx = W * 0.5;
        const cy = H * 0.36;
        const particles = [];

        for (let i = 0; i < count; i++) {
            const angle = Math.random() * Math.PI * 2;
            const speed = 1.8 + Math.random() * 4.4;
            particles.push({
                x: cx,
                y: cy,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed - 1.8,
                size: 3 + Math.random() * 4,
                color: colors[i % colors.length],
                life: 1.0,
                decay: 0.015 + Math.random() * 0.014,
                rot: Math.random() * Math.PI * 2,
                vrot: (Math.random() - 0.5) * 0.2
            });
        }

        function tick() {
            fxCtx.clearRect(0, 0, W, H);
            let alive = false;
            for (const pt of particles) {
                if (pt.life <= 0) continue;
                alive = true;
                pt.x += pt.vx;
                pt.y += pt.vy;
                pt.vy += 0.09;
                pt.vx *= 0.985;
                pt.life -= pt.decay;
                pt.rot += pt.vrot;

                fxCtx.save();
                fxCtx.globalAlpha = Math.max(0, pt.life);
                fxCtx.translate(pt.x, pt.y);
                fxCtx.rotate(pt.rot);
                fxCtx.fillStyle = pt.color;
                fxCtx.fillRect(-pt.size * 0.5, -pt.size * 0.5, pt.size, pt.size * 0.7);
                fxCtx.restore();
            }
            if (alive) requestAnimationFrame(tick);
        }

        requestAnimationFrame(tick);
    }

    function addBadge(text, cssClass) {
        const span = document.createElement('span');
        span.className = `badge ${cssClass}`;
        span.textContent = text;
        badgesRowEl.appendChild(span);
    }

    // --- Main Catch / Accident Handler ---
    async function handleFishingAlert(data) {
        const {
            username = 'Unknown',
            fishName = 'Unknown Catch',
            rarity = 'Common',
            stars = 0,
            weight = 0,
            gold = 0,
            imageFileName = '',
            lineSnapped = false,
            rodSnapped = false,
            reelJammed = false,
            tackleBoxLost = false,
            netBroken = false,
            isAccident = false,
            accidentReason = '',
            lostItems = '',
            totalGoldLost = 0,
            isNewSpecies = false,
            isPersonalBest = false,
            isServerRecord = false,
            isFrenzyBonus = false,
            triggeredBoosts = [],
            tournamentMatches = [],
            pointRewards = [],
            brokenItems = [],
            batchIndex = 1,
            batchTotal = 1,
            revealId = '',
            enableCastAnimation = true,
            castAnimationMs = 1600,
            soundEnabled = true,
            soundVolume = 0.8,
            castSoundUrl = '/media/fishing_cast.mp3',
            biteSoundUrl = '/media/fishing_bite.mp3',
            catchSoundUrl = '/media/fishing_catch_common.mp3',
            accidentSoundUrl = '/media/fishing_accident.mp3',
            duration = 5000
        } = data ?? {};

        const isFailure = isAccident || lineSnapped || rodSnapped || reelJammed || tackleBoxLost || netBroken || String(rarity).toLowerCase() === 'accident';
        const normalized = normalizeRarity(rarity);
        const safeBatchTotal = Math.max(1, Math.floor(toFiniteNumber(batchTotal, 1)));
        const safeBatchIndex = Math.max(1, Math.floor(toFiniteNumber(batchIndex, 1)));
        const safeGold = Math.max(0, Math.floor(toFiniteNumber(gold, 0)));

        // Track multi-cast combo state
        if (safeBatchTotal > 1) {
            if (safeBatchIndex === 1 || comboState.username !== username) {
                comboState = { username, totalGold: 0, catches: 0, batchTotal: safeBatchTotal };
            }
            if (!isFailure) {
                comboState.totalGold += safeGold;
                comboState.catches += 1;
            }
        } else {
            comboState = { username: '', totalGold: 0, catches: 0, batchTotal: 1 };
        }

        // Stage 1: LurkBait cast/bobber animation (slightly snappier on combo items 2..N, still respecting castAnimationMs)
        if (enableCastAnimation) {
            const baseAnimMs = Math.max(600, toFiniteNumber(castAnimationMs, 1600));
            const animMs = safeBatchTotal > 1 && safeBatchIndex > 1
                ? Math.max(1000, Math.floor(baseAnimMs * 0.65))
                : baseAnimMs;
            await playCastAnimation(username, animMs, safeBatchIndex, safeBatchTotal, soundEnabled, soundVolume, castSoundUrl, biteSoundUrl);
        }

        // Stage 2: Populate Splash Reveal Card
        stopCurrentCatchAudio();
        badgesRowEl.innerHTML = '';
        rewardsRowEl.innerHTML = '';
        rewardsRowEl.classList.remove('show');
        accidentDetailsEl.classList.remove('show');
        accidentDetailsEl.textContent = '';

        if (safeBatchTotal > 1) {
            comboPillEl.textContent = `COMBO ${safeBatchIndex}/${safeBatchTotal}`;
            comboPillEl.classList.add('show');
        } else {
            comboPillEl.classList.remove('show');
        }

        cardContentEl.className = 'content';

        if (isFailure) {
            cardContentEl.classList.add('rarity-accident');
            usernameEl.textContent = `${username} — Equipment Failure!`;

            let failureText = accidentReason || fishName;
            let icon = '💥';
            if (rodSnapped) { failureText = 'ROD SNAPPED'; icon = '🎣💥'; }
            else if (reelJammed) { failureText = 'REEL JAMMED'; icon = '⚙️⚠️'; }
            else if (tackleBoxLost) { failureText = 'TACKLE BOX LOST'; icon = '🧰🌊'; }
            else if (netBroken) { failureText = 'NET BROKEN'; icon = '🕸️💥'; }
            else if (lineSnapped) { failureText = 'LINE SNAPPED'; icon = '🧵💥'; }

            fishNameEl.textContent = failureText;
            fishImageEl.style.display = 'none';
            accidentIconEl.textContent = icon;
            accidentIconEl.style.display = 'block';
            rarityEl.className = 'rarity accident';
            rarityEl.textContent = 'ACCIDENT';
            starsEl.innerHTML = '';
            starsEl.style.display = 'none';
            statsRowEl.style.display = 'none';

            const detailsParts = [];
            if (lostItems && lostItems.trim().length > 0) detailsParts.push(`Lost: ${lostItems}`);
            if (totalGoldLost > 0) detailsParts.push(`Value lost: ${formatCompactNumber(totalGoldLost)}g`);
            if (detailsParts.length > 0) {
                accidentDetailsEl.textContent = detailsParts.join(' • ');
                accidentDetailsEl.classList.add('show');
            }
        } else {
            cardContentEl.classList.add(`rarity-${normalized.cssClass}`);
            usernameEl.textContent = `${username} caught:`;
            fishNameEl.textContent = fishName;
            accidentIconEl.style.display = 'none';

            if (imageFileName && imageFileName.length > 0) {
                fishImageEl.src = '/fishes/' + imageFileName;
                fishImageEl.style.display = 'block';
            } else {
                fishImageEl.src = '';
                fishImageEl.style.display = 'none';
            }

            // Contextual highlight badges
            if (isNewSpecies) addBadge('✨ NEW DEX!', 'badge-dex');
            if (isServerRecord) addBadge('👑 SERVER RECORD!', 'badge-record');
            else if (isPersonalBest) addBadge('🏆 NEW PB!', 'badge-pb');

            if (Array.isArray(tournamentMatches) && tournamentMatches.length > 0) {
                const topMatch = tournamentMatches[0];
                const label = topMatch.tookLead
                    ? `👑 TOOK #1!`
                    : `🎯 TOURNEY #${topMatch.rank}`;
                addBadge(label, 'badge-tourney');
            }

            if (isFrenzyBonus) addBadge('🔥 FRENZY', 'badge-frenzy');

            if (Array.isArray(triggeredBoosts) && triggeredBoosts.length > 0) {
                const b = triggeredBoosts[0];
                const usesText = b.remainingUses !== null && b.remainingUses !== undefined && b.maxUses
                    ? ` (${b.remainingUses}/${b.maxUses})`
                    : '';
                addBadge(`🪱 ${b.itemName}${usesText}`, 'badge-bait');
            }

            if (Array.isArray(brokenItems) && brokenItems.length > 0) {
                addBadge(`⚠️ ${brokenItems[0]} BROKE!`, 'badge-broken');
            }

            rarityEl.className = `rarity ${normalized.cssClass}`;
            rarityEl.textContent = normalized.label;

            const safeStars = Math.max(0, Math.floor(toFiniteNumber(stars, 0)));
            starsEl.innerHTML = '';
            for (let i = 0; i < safeStars; i++) {
                const s = document.createElement('span');
                s.className = 'star';
                s.style.animationDelay = `${i * 0.09}s`;
                s.textContent = '★';
                starsEl.appendChild(s);
            }
            starsEl.style.display = safeStars > 0 ? 'inline-flex' : 'none';

            weightEl.textContent = `⚖️ ${toFiniteNumber(weight, 0)} kg`;
            goldEl.textContent = `🪙 ${formatCompactNumber(safeGold)} g`;
            statsRowEl.style.display = 'flex';

            // Compact-formatted built-in multi-point rewards
            if (Array.isArray(pointRewards) && pointRewards.length > 0) {
                for (const r of pointRewards) {
                    const pill = document.createElement('span');
                    pill.className = 'reward-pill';
                    pill.textContent = r.formatted || `+${formatCompactNumber(r.amount)} ${r.pointTypeName || 'pts'}`;
                    rewardsRowEl.appendChild(pill);
                }
                rewardsRowEl.classList.add('show');
            }
        }

        if (safeBatchTotal > 1) {
            const isFinal = safeBatchIndex >= safeBatchTotal;
            comboFooterEl.textContent = isFinal
                ? `Combo Complete: ${comboState.catches}/${safeBatchTotal} caught • ${formatCompactNumber(comboState.totalGold)}g total`
                : `Combo Running Total: ${formatCompactNumber(comboState.totalGold)}g`;
            comboFooterEl.classList.add('show');
        } else {
            comboFooterEl.classList.remove('show');
        }

        alertEl.style.display = 'block';
        alertEl.offsetHeight;
        scheduleScaleContainer();
        alertEl.classList.add('show');

        // Two-way ack back to the bot right as the fish splashes onto screen (authenticated overlay only)
        if (revealId && overlayKey) {
            fetch('/api/overlay/fishing-revealed', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ revealId, overlayKey })
            }).catch(() => {});
        }

        const isHypeCatch = isFailure || isNewSpecies || isPersonalBest || isServerRecord ||
            normalized.cssClass === 'epic' || normalized.cssClass === 'legendary' || normalized.cssClass === 'mythical';

        if (!isFailure) {
            launchParticleBurst(normalized.cssClass, isNewSpecies || isServerRecord);
        }

        currentCatchAudio = playSound(isFailure ? accidentSoundUrl : catchSoundUrl, soundEnabled, soundVolume);

        // Compute readable reveal duration (respects DisplayDurationMs, snappy combo pace, and busy-chat backpressure)
        let displayMs = Math.max(2500, toFiniteNumber(duration, 5000));
        if (safeBatchTotal > 1) {
            displayMs = isHypeCatch ? 4800 : (safeBatchIndex === safeBatchTotal ? 3600 : 3000);
        } else if (queue.length > 3 && !isHypeCatch && normalized.cssClass === 'common') {
            displayMs = Math.max(3500, Math.floor(displayMs * 0.6));
        }

        await sleep(displayMs);

        alertEl.classList.remove('show');
        await sleep(280);
        alertEl.style.display = 'none';
        if (fxCtx) fxCtx.clearRect(0, 0, fxCanvas.width, fxCanvas.height);
        stopCurrentCatchAudio();
    }

    // --- Event-Driven Queue Processor ---
    function enqueueFishingEvent(eventData) {
        queue.push(eventData);
        if (!isProcessing) {
            processQueue();
        }
    }

    async function processQueue() {
        if (isProcessing) return;
        isProcessing = true;
        evaluateFrenzyBannerVisibility();
        try {
            while (queue.length > 0) {
                const next = queue.shift();
                await handleFishingAlert(next);
            }
        } catch (err) {
            console.error('[FishingOverlay] Queue error:', err);
        } finally {
            isProcessing = false;
            if (queue.length > 0) {
                processQueue();
            } else {
                evaluateFrenzyBannerVisibility();
            }
        }
    }

    // --- Scaling (compatible with OverlayEditor & OBS Browser Sources) ---
    function scaleContainer() {
        if (!containerEl) return;
        containerEl.style.transform = '';
        const naturalW = containerEl.offsetWidth;
        const naturalH = containerEl.offsetHeight;
        if (naturalW <= 0 || naturalH <= 0) return;
        const scale = Math.min(window.innerWidth / naturalW, window.innerHeight / naturalH);
        if (Number.isFinite(scale) && scale > 0) {
            containerEl.style.transform = `scale(${scale})`;
        }
    }

    function scheduleScaleContainer() {
        if (scaleFrameHandle !== 0) cancelAnimationFrame(scaleFrameHandle);
        scaleFrameHandle = requestAnimationFrame(() => {
            scaleFrameHandle = 0;
            scaleContainer();
        });
    }

    window.addEventListener('resize', scheduleScaleContainer);

    function registerAuthenticatedOverlay() {
        if (!overlayKey) return;
        fetch('/api/overlay/fishing-register', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ overlayKey })
        }).catch(() => {});
    }

    // --- WebSocket Connection ---
    function connectWS() {
        try {
            ws = createWsSocket('Fishing', [WS_TOPICS.fishing]);
            ws.addEventListener('open', () => {
                logDebug('WebSocket connected');
                wsStatusShow(true);
                registerAuthenticatedOverlay();
            });
            ws.addEventListener('close', () => {
                wsStatusShow(false);
            });
            ws.addEventListener('message', (message) => {
                try {
                    const raw = message.data;
                    if (raw === 'ping') {
                        ws.send('pong');
                        return;
                    }
                    const json = JSON.parse(raw);
                    if (json.fishing !== undefined) {
                        enqueueFishingEvent(json.fishing);
                    }
                    if (json.fishingFrenzy !== undefined) {
                        frenzyStateVersion++;
                        updateFrenzyBanner(json.fishingFrenzy);
                    }
                    if (json.fishingOverlayConfig !== undefined) {
                        applyOverlayConfig(json.fishingOverlayConfig);
                    }
                } catch (err) {
                    logDebug('Parse error:', err);
                }
            });
        } catch (err) {
            console.error('[FishingOverlay] WebSocket init failed:', err);
        }
    }

    loadOverlayConfig().then(() => loadInitialFrenzyState());
    registerAuthenticatedOverlay();
    if (overlayKey) {
        setInterval(registerAuthenticatedOverlay, 45000);
    }
    connectWS();
})();
