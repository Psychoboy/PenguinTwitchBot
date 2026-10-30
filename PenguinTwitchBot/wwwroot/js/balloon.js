/**
 * Balloon Ascent Overlay Engine
 * Features:
 * - 60fps high-DPI transparent Canvas physics engine
 * - Fully non-intrusive: auto-hiding top cloud platform via visibleUntil
 * - Direct client trust: sends computed Score, IsBullseye, IsWin, IsHit, and BalloonPopped with secret overlayKey
 * - Multi-tier hazard system: Nudge (bounce), HeavyPush (anvil downward slam), WindGust (crosswind drift), DartPop (puncture)
 * - Animated emote & sprite caching via DropGamesCommon & procedural audio via DropGamesAudio
 */
(function (global) {
    'use strict';

    const canvas = document.getElementById('balloon-canvas');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');

    const urlParams = new URLSearchParams(window.location.search);
    const overlayKey = urlParams.get('key') || '';

    let width = 0;
    let height = 0;
    let dpr = 1;

    function resize() {
        dpr = window.devicePixelRatio || 1;
        width = window.innerWidth;
        height = window.innerHeight;
        canvas.width = Math.round(width * dpr);
        canvas.height = Math.round(height * dpr);
        ctx.resetTransform();
        ctx.scale(dpr, dpr);
    }
    window.addEventListener('resize', resize);
    resize();

    // Image & emote loader via DropGamesCommon
    const imageLoader = (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.createImageLoader)
        ? DropGamesCommon.createImageLoader()
        : {
            getImage: function (url) {
                if (!url) return null;
                const img = new Image();
                img.src = url;
                return img.complete ? img : null;
            },
            getLoadedImage: function (url) {
                if (!url) return null;
                const img = new Image();
                img.src = url;
                return img.complete ? img : null;
            }
        };

    function getLoadedImage(url) {
        if (!url || !imageLoader) return null;
        try {
            if (typeof imageLoader.getLoadedImage === 'function') return imageLoader.getLoadedImage(url);
            if (typeof imageLoader.getImage === 'function') return imageLoader.getImage(url);
        } catch (_) {}
        return null;
    }

    function playSound(soundDto, synthType = 'chime', defaultFreq = 440) {
        if (!soundDto || soundDto.enabled === false) return;
        const vol = (typeof soundDto.volume === 'number') ? soundDto.volume : 65;
        const volFrac = Math.max(0, Math.min(1, vol / 100));

        if (soundDto.url && soundDto.url.length > 0) {
            try {
                const a = new Audio(soundDto.url);
                a.volume = volFrac;
                a.play().catch(() => {});
                return;
            } catch (_) {}
        }
    }

    // Game state
    const targetCloud = {
        centerX: 0.50,
        baseCenterX: 0.50,
        phase: 0,
        halfWidth: 0.09,
        bullseyeHalfWidth: 0.025,
        speed: 0.5,
        direction: 1,
        y: 0.10,
        alpha: 1.0,
        visibleUntil: 0
    };

    const headerConfig = {
        showBanner: true,
        showTimer: true,
        showCount: true,
        showFreePlayCounter: true,
        position: 'topcenter',
        yOffset: 18,
        nameFontSize: 18
    };
    let currentBannerY = 18;
    let isOtherGameActive = false;
    const activeOtherGames = new Set();
    let otherDropsActiveUntilMs = 0;

    function getBannerX(bannerW) {
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.getBannerX) {
            return DropGamesCommon.getBannerX(width, bannerW, headerConfig.position);
        }
        const pos = (headerConfig.position || 'topcenter').toLowerCase();
        if (pos === 'topleft') return 24;
        if (pos === 'topright') return width - bannerW - 24;
        return (width - bannerW) / 2;
    }

    const sessionState = {
        isActive: false,
        totalDurationSeconds: 90,
        remainingSeconds: 0,
        startedAtMs: 0,
        endsAtMs: 0,
        endedAtMs: 0,
        totalDrops: 0
    };

    let activeBalloons = [];
    let activeHazards = [];
    let particles = [];
    let ambientParticles = [];
    let scorePopups = [];
    let currentTheme = null;

    function preloadThemeImages(theme) {
        if (!theme) return;
        if (theme.balloonCanopyImageUrl) getLoadedImage(theme.balloonCanopyImageUrl);
        if (theme.balloonCloudPlatformImageUrl) getLoadedImage(theme.balloonCloudPlatformImageUrl);
        if (theme.balloonDebrisImageUrl) getLoadedImage(theme.balloonDebrisImageUrl);
        if (theme.balloonDartImageUrl) getLoadedImage(theme.balloonDartImageUrl);
        if (theme.balloonAnvilImageUrl) getLoadedImage(theme.balloonAnvilImageUrl);
        if (theme.balloonNudgeImageUrl) getLoadedImage(theme.balloonNudgeImageUrl);
        if (theme.balloonWindGustImageUrl) getLoadedImage(theme.balloonWindGustImageUrl);
        if (theme.defaultCharacterImageUrl) getLoadedImage(theme.defaultCharacterImageUrl);
        if (theme.defaultDropletImageUrl) getLoadedImage(theme.defaultDropletImageUrl);
    }
    let lastColSoundTime = 0;
    // WebSocket connection & completions queue
    const socket = window.createWsSocket('balloon', [window.WS_TOPICS.balloon]);

    // Shared completion queue + heartbeat + socket wiring (see DropGamesCommon.createCompletionChannel)
    const completionChannel = DropGamesCommon.createCompletionChannel({
        game: 'balloon',
        socket: socket,
        overlayKey: overlayKey,
        getActiveTokens: () => Array.isArray(activeBalloons)
            ? activeBalloons.filter(b => b && b.dropToken && !b.completed).map(b => b.dropToken)
            : []
    });
    const pendingCompletions = completionChannel.pending;
    const sendCompletion = completionChannel.send;
    const flushPendingCompletions = completionChannel.flush;
    const sendHeartbeat = completionChannel.sendHeartbeat;
    const ensureHeartbeat = completionChannel.ensureHeartbeat;
    completionChannel.attach(msg => handleWsMessage(msg));

    function handleWsMessage(msg) {
        if (!msg || typeof msg !== 'object') return;

        if (msg.type === 'other_game_status') {
            isOtherGameActive = DropGamesCommon.updateActiveGameStateFromMessage(activeOtherGames, 'balloon', msg);
            return;
        }

        if (msg.type === 'other_drop_active') {
            const durSec = typeof msg.durationSeconds === 'number' ? msg.durationSeconds : 10.0;
            otherDropsActiveUntilMs = Math.max(otherDropsActiveUntilMs, performance.now() + durSec * 1000);
            return;
        }

        if (msg.type === 'parachute_drop' || msg.type === 'plinko_drop' || msg.type === 'curling_throw') {
            const durSec = typeof msg.durationSeconds === 'number' ? msg.durationSeconds : 10.0;
            otherDropsActiveUntilMs = Math.max(otherDropsActiveUntilMs, performance.now() + (durSec + 6.0) * 1000);
            return;
        }

        const activeGameStatus = DropGamesCommon.updateActiveGameStateFromMessage(activeOtherGames, 'balloon', msg);
        if (activeGameStatus !== null) isOtherGameActive = activeGameStatus;

        if (msg.type === 'session_start' && msg.session) {
            DropGamesCommon.broadcastGameStatus('balloon', true);
            const s = msg.session;
            sessionState.isActive = true;
            sessionState.endedAtMs = 0;
            sessionState.totalDurationSeconds = s.totalDurationSeconds || 90;
            sessionState.remainingSeconds = s.remainingSeconds || sessionState.totalDurationSeconds;
            sessionState.startedAtMs = performance.now();
            sessionState.endsAtMs = performance.now() + (s.remainingSeconds || s.totalDurationSeconds || 90) * 1000;
            sessionState.totalDrops = typeof s.totalDrops === 'number' ? s.totalDrops : 0;
            if (msg.headerConfig) Object.assign(headerConfig, msg.headerConfig);
            else if (s.headerConfig) Object.assign(headerConfig, s.headerConfig);
            if (msg.theme) {
                currentTheme = msg.theme;
                preloadThemeImages(currentTheme);
            }

            if (msg.target) {
                const cx = typeof msg.target.centerX === 'number' ? msg.target.centerX : 0.50;
                targetCloud.baseCenterX = cx;
                if (targetCloud.centerX === undefined || targetCloud.visibleUntil <= performance.now()) {
                    targetCloud.centerX = cx;
                    targetCloud.phase = 0;
                }
                targetCloud.halfWidth = typeof msg.target.halfWidth === 'number' ? msg.target.halfWidth : 0.09;
                targetCloud.bullseyeHalfWidth = targetCloud.halfWidth * 0.25;
                if (typeof msg.target.speed === 'number') {
                    targetCloud.speed = msg.target.speed;
                }
                if (typeof msg.target.alpha === 'number') {
                    targetCloud.alpha = Math.max(0.05, Math.min(1.0, msg.target.alpha));
                }
            }

            targetCloud.visibleUntil = performance.now() + sessionState.totalDurationSeconds * 1000 + 4000;
            playSound(msg.sounds?.start || { enabled: true }, 'chime', 520);
        } else if (msg.type === 'session_end') {
            sessionState.isActive = false;
            DropGamesCommon.broadcastGameStatus('balloon', false);
            sessionState.endedAtMs = performance.now();
            targetCloud.visibleUntil = performance.now() + 6000;
            if (msg.headerConfig) Object.assign(headerConfig, msg.headerConfig);
            else if (msg.session && msg.session.headerConfig) Object.assign(headerConfig, msg.session.headerConfig);
            playSound(msg.sounds?.end || { enabled: true }, 'chime', 380);
        } else if (msg.type === 'balloon_drop') {
            DropGamesCommon.broadcastDropActive('balloon', 12000);
            try {
                // Vibrant balloon color palette
                const balloonPalette = [
                    { primary: '#EF4444', shadow: '#991B1B', highlight: '#FCA5A5' }, // Candy Red
                    { primary: '#3B82F6', shadow: '#1E40AF', highlight: '#93C5FD' }, // Royal Blue
                    { primary: '#10B981', shadow: '#065F46', highlight: '#6EE7B7' }, // Emerald Green
                    { primary: '#F59E0B', shadow: '#B45309', highlight: '#FDE68A' }, // Golden Amber
                    { primary: '#8B5CF6', shadow: '#5B21B6', highlight: '#DDD6FE' }, // Electric Purple
                    { primary: '#EC4899', shadow: '#9D174D', highlight: '#FBCFE8' }, // Bubblegum Pink
                    { primary: '#06B6D4', shadow: '#0E7490', highlight: '#A5F3FC' }, // Cyan Sky
                    { primary: '#F97316', shadow: '#C2410C', highlight: '#FED7AA' }, // Hot Tangerine
                    { primary: '#84CC16', shadow: '#4D7C0F', highlight: '#D9F99D' }, // Lime Spark
                    { primary: '#E11D48', shadow: '#881337', highlight: '#FDA4AF' }  // Ruby Rose
                ];
                const colorChoice = balloonPalette[Math.floor(Math.random() * balloonPalette.length)];

                // Balloon styles: 60% solid, 20% striped, 10% polka, 10% double
                const styleRoll = Math.random();
                let balloonStyle = 'solid';
                if (styleRoll < 0.60) balloonStyle = 'solid';
                else if (styleRoll < 0.80) balloonStyle = 'striped';
                else if (styleRoll < 0.90) balloonStyle = 'polka';
                else balloonStyle = 'double';

                // Physical randomization: sizes, speed, sway, drift
                const sizeVariation = 0.90 + Math.random() * 0.20;
                const speedVariation = 0.88 + Math.random() * 0.24;
                const baseVy = (typeof msg.initialVy === 'number' ? msg.initialVy : -0.16) * speedVariation;

                const b = {
                    dropToken: msg.dropToken,
                    user: msg.user || 'Viewer',
                    userColor: msg.userColor || '#FFFFFF',
                    nameFontSize: typeof msg.nameFontSize === 'number' ? msg.nameFontSize : (msg.headerConfig?.nameFontSize || headerConfig.nameFontSize || 18),
                    characterImageUrl: msg.characterImageUrl || '',
                    balloonCanopyImageUrl: msg.balloonCanopyImageUrl || '',
                    balloonColor: colorChoice.primary,
                    balloonShadow: colorChoice.shadow,
                    balloonHighlight: colorChoice.highlight,
                    balloonStyle: balloonStyle,
                    sizeScale: sizeVariation,
                    x: typeof msg.startX === 'number' ? msg.startX : 0.5,
                    y: typeof msg.startY === 'number' ? msg.startY : 0.94,
                    landingY: typeof msg.landingY === 'number' ? msg.landingY : 0.10,
                    vx: (typeof msg.initialVx === 'number' ? msg.initialVx : 0) + (Math.random() - 0.5) * 0.035,
                    vy: baseVy,
                    baseVy: baseVy,
                    buoyancy: (msg.physics?.buoyancy || 1.0) * speedVariation,
                    bounciness: msg.physics?.bounciness || 0.85,
                    mass: (msg.physics?.mass || 1.0) * sizeVariation,
                    swayAmount: (msg.physics?.swayAmount || 1.0) * (0.85 + Math.random() * 0.3),
                    swayPhase: Math.random() * Math.PI * 2,
                    swayFreq: 2.0 + Math.random() * 1.2,
                    tilt: 0,
                    radius: 0.038 * sizeVariation,
                    canopyRadius: 0.044 * sizeVariation,
                    stringLength: 0.058 * sizeVariation,
                    balloonOffset: 0.165 * sizeVariation,
                    completed: false,
                    popped: false,
                    isWin: false,
                    isBullseye: false,
                    isHit: false,
                    score: 0,
                    recoilVy: 0,
                    recoilTimer: 0,
                    theme: msg.theme || currentTheme,
                    sounds: msg.sounds || {},
                    createdAtMs: performance.now(),
                    durationSeconds: msg.durationSeconds || 6.0
                };

                if (msg.headerConfig) Object.assign(headerConfig, msg.headerConfig);

                if (msg.target) {
                    const cx = typeof msg.target.centerX === 'number' ? msg.target.centerX : 0.50;
                    targetCloud.baseCenterX = cx;
                    if (targetCloud.centerX === undefined || targetCloud.visibleUntil <= performance.now()) {
                        targetCloud.centerX = cx;
                        targetCloud.phase = 0;
                    }
                    targetCloud.halfWidth = typeof msg.target.halfWidth === 'number' ? msg.target.halfWidth : 0.09;
                    targetCloud.bullseyeHalfWidth = targetCloud.halfWidth * 0.25;
                    if (typeof msg.target.speed === 'number') {
                        targetCloud.speed = msg.target.speed;
                    }
                    if (typeof msg.target.alpha === 'number') {
                        targetCloud.alpha = Math.max(0.05, Math.min(1.0, msg.target.alpha));
                    }
                }

                sessionState.totalDrops++;
                targetCloud.visibleUntil = Math.max(targetCloud.visibleUntil, performance.now() + (b.durationSeconds + 4.5) * 1000);
                if (msg.theme) {
                currentTheme = msg.theme;
                preloadThemeImages(currentTheme);
            }

                if (b.characterImageUrl) getLoadedImage(b.characterImageUrl);
                if (b.balloonCanopyImageUrl) getLoadedImage(b.balloonCanopyImageUrl);

                playSound(b.sounds?.balloonLaunch || { enabled: true }, 'launch', 480);

                activeBalloons.push(b);
            } catch (err) {
                console.error('Error handling balloon_drop:', err);
            }
        } else if (msg.type === 'balloon_hazards' && Array.isArray(msg.hazards)) {
            try {
                msg.hazards.forEach(h => {
                    activeHazards.push({
                        id: h.id,
                        x: h.startX,
                        y: h.startY,
                        vx: h.vx || 0,
                        vy: h.vy || 0.20,
                        gravity: h.gravity || 0.12,
                        radius: h.radius || 0.028,
                        mass: h.mass || 3.0,
                        hazardType: h.hazardType || 'Nudge',
                        imageUrl: h.imageUrl || '',
                        targetDropToken: h.targetDropToken,
                        rotation: 0,
                        rotSpeed: (Math.random() - 0.5) * 6.0,
                        alpha: 1.0
                    });
                    if (h.imageUrl) getLoadedImage(h.imageUrl);
                });
                targetCloud.visibleUntil = Math.max(targetCloud.visibleUntil, performance.now() + 6000);
            } catch (err) {
                console.error('Error handling balloon_hazards:', err);
            }
        }
    }

    // Particle FX
    function spawnPopParticles(x, y, color) {
        for (let i = 0; i < 22; i++) {
            const angle = Math.random() * Math.PI * 2;
            const spd = (0.08 + Math.random() * 0.22);
            particles.push({
                x: x,
                y: y,
                vx: Math.cos(angle) * spd,
                vy: Math.sin(angle) * spd,
                size: (4 + Math.random() * 6),
                color: color || '#FF5555',
                alpha: 1.0,
                decay: 0.02 + Math.random() * 0.03,
                type: 'rubber',
                isNormalized: true
            });
        }
    }

    function spawnWinConfetti(x, y) {
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.spawnSeasonalBurst) {
            const season = DropGamesCommon.getSeasonPreset ? DropGamesCommon.getSeasonPreset(currentTheme) : 'summer';
            DropGamesCommon.spawnSeasonalBurst(particles, {
                x,
                y,
                season,
                count: 35,
                isBullseye: true,
                isNormalized: true
            });
        }
    }

    function spawnBounceSparks(x, y) {
        for (let i = 0; i < 7; i++) {
            const angle = Math.random() * Math.PI * 2;
            const spd = 0.04 + Math.random() * 0.12;
            particles.push({
                x: x,
                y: y,
                vx: Math.cos(angle) * spd,
                vy: Math.sin(angle) * spd,
                size: 2.5 + Math.random() * 3.5,
                color: '#FEF08A',
                alpha: 0.9,
                decay: 0.04 + Math.random() * 0.04,
                type: 'spark',
                isNormalized: true
            });
        }
    }

    // Main 60fps render & physics loop
    let lastTime = performance.now();

    function render(now) {
        const dt = Math.min((now - lastTime) / 1000, 0.05);
        lastTime = now;

        ctx.clearRect(0, 0, width, height);

        const isVisible = activeBalloons.length > 0 ||
                          activeHazards.length > 0 ||
                          sessionState.isActive ||
                          (sessionState.endedAtMs > 0 && now - sessionState.endedAtMs < 6000) ||
                          now < targetCloud.visibleUntil;

        if (isVisible) {
            if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.syncAmbientParticles && DropGamesCommon.updateAndDrawAmbientParticles) {
                DropGamesCommon.syncAmbientParticles(ambientParticles, { width, height, currentTheme, isVisible: true });
                DropGamesCommon.updateAndDrawAmbientParticles(ctx, ambientParticles, { dtSec: dt, width, height, currentTheme, isVisible: true });
            }
            updateTargetCloud(dt);
            drawTargetCloud();
            updateAndDrawHazards(dt, now);
            updateAndDrawBalloons(dt, now);
            updateAndDrawParticles(dt);
            drawScorePopups(dt, now);
            drawSessionBanner(now);
        } else {
            ambientParticles.length = 0;
            particles.length = 0;
        }

        requestAnimationFrame(render);
    }

    function updateTargetCloud(dt) {
        if (!targetCloud.speed || targetCloud.speed <= 0) return;

        const minX = targetCloud.halfWidth + 0.04;
        const maxX = 1.0 - targetCloud.halfWidth - 0.04;
        const baseCx = typeof targetCloud.baseCenterX === 'number' ? targetCloud.baseCenterX : 0.50;
        const span = Math.max(0.04, Math.min(baseCx - minX, maxX - baseCx));

        // Gradual, smooth drift with sinusoidal oscillation around baseCenterX
        // Calibrated so default speed 0.5 drifts gracefully across the screen (~45s per one-way sweep)
        // while speed 1.0 is a moderate drift (~22s) and 0.0 is stationary
        const omega = targetCloud.speed * 0.14;
        targetCloud.phase = (targetCloud.phase || 0) + omega * dt;
        targetCloud.centerX = baseCx + Math.sin(targetCloud.phase) * span;
    }

    function drawTargetCloud() {
        const cx = targetCloud.centerX * width;
        const cy = targetCloud.y * height;
        const hw = targetCloud.halfWidth * width;
        const bHw = targetCloud.bullseyeHalfWidth * width;
        const cloudAlpha = typeof targetCloud.alpha === 'number' ? targetCloud.alpha : 1.0;

        ctx.save();
        ctx.globalAlpha = Math.max(0.05, Math.min(1.0, cloudAlpha));

        // Custom cloud platform image if provided
        if (currentTheme && currentTheme.balloonCloudPlatformImageUrl) {
            const cloudImg = getLoadedImage(currentTheme.balloonCloudPlatformImageUrl);
            if (cloudImg) {
                const imgW = hw * 2.2;
                const imgH = imgW * (cloudImg.height / cloudImg.width);
                ctx.drawImage(cloudImg, cx - imgW / 2, cy - imgH / 2, imgW, imgH);
                ctx.restore();
                drawBullseyeRing(cx, cy, bHw, cloudAlpha);
                return;
            }
        }

        const season = ((currentTheme && currentTheme.seasonPreset) || 'spring').toLowerCase();

        // Soft cloud drop shadow and season-specific tints
        let shadowColor = 'rgba(0, 0, 0, 0.25)';
        let cloudBaseColor = '#FFFFFF';
        let cloudPuffColor = '#FFFFFF';

        if (season === 'spring') {
            shadowColor = 'rgba(255, 143, 163, 0.35)';
            cloudBaseColor = '#FFF5F7';
            cloudPuffColor = '#FFFFFF';
        } else if (season === 'summer') {
            shadowColor = 'rgba(0, 180, 216, 0.30)';
            cloudBaseColor = '#FFFFFF';
            cloudPuffColor = '#F0F9FF';
        } else if (season === 'fall') {
            shadowColor = 'rgba(231, 111, 81, 0.35)';
            cloudBaseColor = '#FFF8F0';
            cloudPuffColor = '#FFF3E0';
        } else if (season === 'winter') {
            shadowColor = 'rgba(72, 202, 228, 0.40)';
            cloudBaseColor = '#F0F8FF';
            cloudPuffColor = '#E6F4F8';
        } else if (season === 'halloween') {
            shadowColor = 'rgba(123, 44, 191, 0.55)';
            cloudBaseColor = '#240046';
            cloudPuffColor = '#3C096C';
        } else if (season === 'christmas') {
            shadowColor = 'rgba(217, 4, 41, 0.35)';
            cloudBaseColor = '#FFFFFF';
            cloudPuffColor = '#F8F9FA';
        }

        // Summer Sunbeams peeking behind cloud
        if (season === 'summer') {
            ctx.save();
            ctx.fillStyle = '#FFD166';
            ctx.beginPath();
            ctx.arc(cx, cy - hw * 0.15, hw * 0.38, Math.PI, 0);
            ctx.fill();
            // Sun rays
            ctx.strokeStyle = 'rgba(255, 209, 102, 0.5)';
            ctx.lineWidth = 3;
            for (let a = -Math.PI; a <= 0; a += Math.PI / 6) {
                ctx.beginPath();
                ctx.moveTo(cx + Math.cos(a) * hw * 0.42, cy - hw * 0.15 + Math.sin(a) * hw * 0.42);
                ctx.lineTo(cx + Math.cos(a) * hw * 0.58, cy - hw * 0.15 + Math.sin(a) * hw * 0.58);
                ctx.stroke();
            }
            ctx.restore();
        }

        ctx.shadowColor = shadowColor;
        ctx.shadowBlur = 18;
        ctx.shadowOffsetY = 6;

        // Base cloud platform body
        ctx.fillStyle = cloudBaseColor;
        ctx.beginPath();
        ctx.ellipse(cx, cy, hw, hw * 0.32, 0, 0, Math.PI * 2);
        ctx.fill();

        // Layered cloud puffs
        ctx.fillStyle = cloudPuffColor;
        ctx.beginPath();
        ctx.arc(cx - hw * 0.65, cy - hw * 0.08, hw * 0.28, 0, Math.PI * 2);
        ctx.arc(cx - hw * 0.25, cy - hw * 0.16, hw * 0.36, 0, Math.PI * 2);
        ctx.arc(cx + hw * 0.25, cy - hw * 0.16, hw * 0.36, 0, Math.PI * 2);
        ctx.arc(cx + hw * 0.65, cy - hw * 0.08, hw * 0.28, 0, Math.PI * 2);
        ctx.fill();

        // Reset shadow for seasonal garnish
        ctx.shadowColor = 'transparent';

        // Seasonal ornaments on cloud:
        if (season === 'spring') {
            // Cherry blossom petals on cloud
            const drawBlossom = (bx, by, sz) => {
                ctx.fillStyle = '#FF8FA3';
                for (let p = 0; p < 5; p++) {
                    const ang = p * (Math.PI * 2 / 5);
                    ctx.beginPath();
                    ctx.arc(bx + Math.cos(ang) * sz * 0.6, by + Math.sin(ang) * sz * 0.6, sz * 0.45, 0, Math.PI * 2);
                    ctx.fill();
                }
                ctx.fillStyle = '#FFE066';
                ctx.beginPath();
                ctx.arc(bx, by, sz * 0.25, 0, Math.PI * 2);
                ctx.fill();
            };
            drawBlossom(cx - hw * 0.55, cy - hw * 0.12, hw * 0.08);
            drawBlossom(cx + hw * 0.55, cy - hw * 0.12, hw * 0.08);
        } else if (season === 'winter') {
            // Dangling icicles
            ctx.fillStyle = '#CAF0F8';
            const icicleXs = [-0.6, -0.3, 0.0, 0.3, 0.6];
            const icicleLens = [0.22, 0.35, 0.40, 0.32, 0.20];
            icicleXs.forEach((frac, idx) => {
                const ix = cx + hw * frac;
                const iy = cy + hw * 0.25;
                const len = hw * icicleLens[idx];
                ctx.beginPath();
                ctx.moveTo(ix - hw * 0.04, iy);
                ctx.lineTo(ix + hw * 0.04, iy);
                ctx.lineTo(ix, iy + len);
                ctx.closePath();
                ctx.fill();
            });
        } else if (season === 'fall') {
            // Autumn maple leaves
            const drawLeaf = (lx, ly, col, rot) => {
                ctx.save();
                ctx.translate(lx, ly);
                ctx.rotate(rot);
                ctx.fillStyle = col;
                ctx.beginPath();
                ctx.ellipse(0, 0, hw * 0.08, hw * 0.045, 0, 0, Math.PI * 2);
                ctx.fill();
                ctx.restore();
            };
            drawLeaf(cx - hw * 0.52, cy - hw * 0.10, '#E76F51', -0.3);
            drawLeaf(cx + hw * 0.50, cy - hw * 0.12, '#E9C46A', 0.4);
            drawLeaf(cx + hw * 0.60, cy - hw * 0.06, '#D90429', 0.1);
        } else if (season === 'halloween') {
            // Glowing spooky eyes in the dark cauldron cloud
            ctx.fillStyle = '#39FF14';
            ctx.beginPath();
            ctx.ellipse(cx - hw * 0.50, cy - hw * 0.05, hw * 0.04, hw * 0.02, 0.3, 0, Math.PI * 2);
            ctx.ellipse(cx - hw * 0.42, cy - hw * 0.05, hw * 0.04, hw * 0.02, -0.3, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#FFE066';
            ctx.beginPath();
            ctx.ellipse(cx + hw * 0.42, cy - hw * 0.05, hw * 0.04, hw * 0.02, 0.3, 0, Math.PI * 2);
            ctx.ellipse(cx + hw * 0.50, cy - hw * 0.05, hw * 0.04, hw * 0.02, -0.3, 0, Math.PI * 2);
            ctx.fill();
        } else if (season === 'christmas') {
            // Holly leaves and red berries
            ctx.fillStyle = '#2B9348';
            ctx.beginPath();
            ctx.ellipse(cx - hw * 0.55, cy - hw * 0.12, hw * 0.07, hw * 0.035, -0.4, 0, Math.PI * 2);
            ctx.ellipse(cx - hw * 0.48, cy - hw * 0.12, hw * 0.07, hw * 0.035, 0.4, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#D90429';
            ctx.beginPath();
            ctx.arc(cx - hw * 0.52, cy - hw * 0.15, hw * 0.028, 0, Math.PI * 2);
            ctx.arc(cx - hw * 0.50, cy - hw * 0.11, hw * 0.028, 0, Math.PI * 2);
            ctx.arc(cx - hw * 0.54, cy - hw * 0.11, hw * 0.028, 0, Math.PI * 2);
            ctx.fill();
        }

        ctx.restore();

        drawBullseyeRing(cx, cy, bHw, cloudAlpha);
    }

    function drawBullseyeRing(cx, cy, bHw, cloudAlpha = 1.0) {
        ctx.save();
        ctx.globalAlpha = Math.max(0.05, Math.min(1.0, cloudAlpha));
        const primaryCol = currentTheme?.targetColor || '#38EF7D';
        const accentCol = currentTheme?.accentColor || '#FFD166';
        const centerCol = currentTheme?.primaryColor || '#E76F51';

        // Outer landing ring
        ctx.strokeStyle = primaryCol;
        ctx.lineWidth = 3.5;
        ctx.beginPath();
        ctx.ellipse(cx, cy, bHw * 2.2, bHw * 0.8, 0, 0, Math.PI * 2);
        ctx.stroke();

        // Accent center bullseye ring
        ctx.fillStyle = accentCol;
        ctx.beginPath();
        ctx.ellipse(cx, cy, bHw, bHw * 0.45, 0, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = centerCol;
        ctx.beginPath();
        ctx.ellipse(cx, cy, bHw * 0.4, bHw * 0.2, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
    }

    function updateAndDrawHazards(dt, now) {
        for (let i = activeHazards.length - 1; i >= 0; i--) {
            const h = activeHazards[i];

            h.vy += h.gravity * dt;
            h.x += h.vx * dt;
            h.y += h.vy * dt;
            h.rotation += h.rotSpeed * dt;

            // Draw hazard
            const hx = h.x * width;
            const hy = h.y * height;
            const hr = h.radius * Math.min(width, height);

            ctx.save();
            ctx.translate(hx, hy);
            ctx.rotate(h.rotation);

            let drawn = false;
            if (h.imageUrl) {
                const img = getLoadedImage(h.imageUrl);
                if (img) {
                    const sz = hr * 2.2;
                    ctx.drawImage(img, -sz / 2, -sz / 2, sz, sz);
                    drawn = true;
                }
            }

            if (!drawn) {
                if (h.hazardType === 'HeavyPush') {
                    // Stylized heavy 3D anvil
                    ctx.fillStyle = '#334155';
                    ctx.beginPath();
                    ctx.moveTo(-hr * 1.3, -hr * 0.4);
                    ctx.lineTo(hr * 1.1, -hr * 0.4);
                    ctx.lineTo(hr * 0.8, -hr * 0.1);
                    ctx.lineTo(hr * 0.4, hr * 0.3);
                    ctx.lineTo(hr * 0.7, hr * 0.8);
                    ctx.lineTo(-hr * 0.7, hr * 0.8);
                    ctx.lineTo(-hr * 0.4, hr * 0.3);
                    ctx.lineTo(-hr * 0.9, -hr * 0.1);
                    ctx.closePath();
                    ctx.fill();

                    // Metallic top highlight
                    ctx.fillStyle = '#94A3B8';
                    ctx.fillRect(-hr * 1.2, -hr * 0.45, hr * 2.2, hr * 0.15);
                    ctx.fillStyle = '#CBD5E1';
                    ctx.font = `bold ${Math.max(10, Math.round(hr * 0.45))}px sans-serif`;
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText('10T', 0, hr * 0.2);
                } else if (h.hazardType === 'DartPop') {
                    // Stylized feathered dart
                    ctx.strokeStyle = '#94A3B8';
                    ctx.lineWidth = 2.5;
                    ctx.beginPath();
                    ctx.moveTo(0, -hr * 1.35);
                    ctx.lineTo(0, -hr * 0.6);
                    ctx.stroke();

                    // Dart Body
                    ctx.fillStyle = '#E63946';
                    ctx.beginPath();
                    ctx.moveTo(0, -hr * 0.7);
                    ctx.lineTo(hr * 0.35, hr * 0.3);
                    ctx.lineTo(-hr * 0.35, hr * 0.3);
                    ctx.closePath();
                    ctx.fill();

                    // Flight shaft
                    ctx.fillStyle = '#FCD34D';
                    ctx.fillRect(-hr * 0.12, hr * 0.25, hr * 0.24, hr * 0.5);

                    // Red fletching fins
                    ctx.fillStyle = '#D90429';
                    ctx.beginPath();
                    ctx.moveTo(0, hr * 0.5);
                    ctx.lineTo(hr * 0.5, hr * 1.1);
                    ctx.lineTo(0, hr * 0.9);
                    ctx.lineTo(-hr * 0.5, hr * 1.1);
                    ctx.closePath();
                    ctx.fill();
                } else if (h.hazardType === 'WindGust') {
                    // Swirling crosswind vortex
                    ctx.strokeStyle = 'rgba(255, 255, 255, 0.85)';
                    ctx.lineWidth = 3;
                    ctx.beginPath();
                    ctx.arc(0, 0, hr * 0.9, 0, Math.PI * 1.4);
                    ctx.stroke();

                    ctx.strokeStyle = 'rgba(200, 230, 255, 0.7)';
                    ctx.lineWidth = 2;
                    ctx.beginPath();
                    ctx.arc(0, 0, hr * 0.5, Math.PI * 0.5, Math.PI * 1.9);
                    ctx.stroke();
                } else {
                    // Bouncy Rubber Nudge Bumper
                    const grad = ctx.createRadialGradient(-hr * 0.3, -hr * 0.3, hr * 0.1, 0, 0, hr);
                    grad.addColorStop(0, '#7DD3FC');
                    grad.addColorStop(0.6, '#0284C7');
                    grad.addColorStop(1, '#0369A1');
                    ctx.fillStyle = grad;
                    ctx.beginPath();
                    ctx.arc(0, 0, hr, 0, Math.PI * 2);
                    ctx.fill();

                    ctx.strokeStyle = 'rgba(255, 255, 255, 0.6)';
                    ctx.lineWidth = 2;
                    ctx.stroke();
                }
            }

            ctx.restore();

            // Collision check against active balloons
            for (let j = 0; j < activeBalloons.length; j++) {
                const b = activeBalloons[j];
                if (b.completed || b.popped) continue;

                const canopyX = b.x * width;
                const canopyY = (b.y - (b.balloonOffset || 0.165)) * height;
                const canopyR = b.canopyRadius * Math.min(width, height);

                const dx = hx - canopyX;
                const dy = hy - canopyY;
                const dist = Math.sqrt(dx * dx + dy * dy);

                if (dist < (canopyR + hr)) {
                    // Collision occurred!
                    handleHazardBalloonCollision(h, b, hx, hy);
                    activeHazards.splice(i, 1);
                    break;
                }
            }

            // Despawn off-screen hazards
            if (h.y > 1.1 || h.y < -0.15 || h.x < -0.1 || h.x > 1.1) {
                activeHazards.splice(i, 1);
            }
        }
    }

    function handleHazardBalloonCollision(h, b, hitX, hitY) {
        const bOff = b.balloonOffset || 0.165;
        if (h.hazardType === 'DartPop') {
            // Punctures and pops the balloon!
            b.popped = true;
            b.vy = 0.22; // Initial downward drop speed
            b.spinSpeed = (Math.random() < 0.5 ? -1 : 1) * (7.0 + Math.random() * 4.0);
            b.vx += (Math.random() - 0.5) * 0.12;
            spawnPopParticles(b.x, b.y - bOff, b.balloonColor || b.theme?.primaryColor || '#EF4444');
            playSound(b.sounds?.balloonPop || { enabled: true }, 'pop', 600);
            return;
        }

        if (h.hazardType === 'HeavyPush') {
            // Heavy anvil slam! Pushes down significantly
            b.recoilVy = 0.28;
            b.recoilTimer = 0.9;
            b.y = Math.min(0.92, b.y + 0.08); // Immediate downward depression
            playSound(b.sounds?.balloonBonk || { enabled: true }, 'bonk', 280);
            spawnPopParticles(b.x, b.y - bOff, '#94A3B8');
            return;
        }

        // Nudge / Droplet / Wind: Elastic momentum transfer
        const knockX = (h.vx !== 0 ? Math.sign(h.vx) : (Math.random() - 0.5)) * 0.12;
        b.vx += knockX;
        b.recoilVy = 0.08;
        b.recoilTimer = 0.45;
        playSound(b.sounds?.balloonNudge || { enabled: true }, h.hazardType === 'WindGust' ? 'wind' : 'nudge', 420);
    }

    function resolveBalloonBalloonCollisions(now) {
        const count = activeBalloons.length;
        if (count < 2) return;

        for (let i = 0; i < count; i++) {
            const b1 = activeBalloons[i];
            if (!b1 || b1.completed || b1.popped) continue;

            for (let j = i + 1; j < count; j++) {
                const b2 = activeBalloons[j];
                if (!b2 || b2.completed || b2.popped) continue;

                // 1. Canopy vs Canopy collision check
                const b1Off = b1.balloonOffset || 0.165;
                const b2Off = b2.balloonOffset || 0.165;
                const c1x = b1.x * width;
                const c1y = (b1.y - b1Off) * height;
                const c2x = b2.x * width;
                const c2y = (b2.y - b2Off) * height;
                const c1r = b1.canopyRadius * Math.min(width, height);
                const c2r = b2.canopyRadius * Math.min(width, height);
                const cMinDist = (c1r + c2r) * 0.94;

                const cdx = c1x - c2x;
                const cdy = c1y - c2y;
                const cDistSq = cdx * cdx + cdy * cdy;

                // 2. Avatar vs Avatar collision check
                const a1x = b1.x * width;
                const a1y = b1.y * height;
                const a2x = b2.x * width;
                const a2y = b2.y * height;
                const a1r = b1.radius * Math.min(width, height);
                const a2r = b2.radius * Math.min(width, height);
                const aMinDist = (a1r + a2r) * 0.94;

                const adx = a1x - a2x;
                const ady = a1y - a2y;
                const aDistSq = adx * adx + ady * ady;

                const isCanopyCol = cDistSq < (cMinDist * cMinDist) && cDistSq > 0.01;
                const isAvatarCol = !isCanopyCol && aDistSq < (aMinDist * aMinDist) && aDistSq > 0.01;

                if (isCanopyCol || isAvatarCol) {
                    const dx = isCanopyCol ? cdx : adx;
                    const dy = isCanopyCol ? cdy : ady;
                    const dist = Math.sqrt(dx * dx + dy * dy);
                    if (dist < 0.001) continue;

                    const nx = dx / dist;
                    const ny = dy / dist;
                    const minDist = isCanopyCol ? cMinDist : aMinDist;
                    const overlap = Math.max(0, minDist - dist);

                    // Separate positions in normalized coordinates
                    const sepNormX = (overlap * 0.52 * nx) / width;
                    const sepNormY = (overlap * 0.52 * ny) / height;

                    b1.x += sepNormX;
                    b1.y += sepNormY * 0.35;
                    b2.x -= sepNormX;
                    b2.y -= sepNormY * 0.35;

                    // Elastic momentum exchange
                    const relVx = b1.vx - b2.vx;
                    const relVy = b1.vy - b2.vy;
                    const velAlongNormal = relVx * nx + relVy * ny;

                    if (velAlongNormal < 0) {
                        const restitution = Math.min(b1.bounciness || 0.85, b2.bounciness || 0.85);
                        const m1 = b1.mass || 1.0;
                        const m2 = b2.mass || 1.0;
                        const impulse = -(1 + restitution) * velAlongNormal / (1 / m1 + 1 / m2);

                        b1.vx += (impulse / m1) * nx * 0.85;
                        b2.vx -= (impulse / m2) * nx * 0.85;
                        b1.vy += (impulse / m1) * ny * 0.25;
                        b2.vy -= (impulse / m2) * ny * 0.25;

                        // Visual tilt jolt from impact
                        b1.tilt += (Math.random() - 0.5) * 0.25;
                        b2.tilt += (Math.random() - 0.5) * 0.25;

                        // Sound cue (throttled)
                        if (!lastColSoundTime || (now - lastColSoundTime) > 130) {
                            lastColSoundTime = now;
                            playSound(b1.sounds?.balloonBonk || { enabled: true }, 'nudge', 380);
                        }

                        // Spark effect at contact midpoint
                        const sparkX = (b1.x + b2.x) / 2;
                        const sparkY = isCanopyCol ? (((b1.y - b1Off) + (b2.y - b2Off)) / 2) : (b1.y + b2.y) / 2;
                        spawnBounceSparks(sparkX, sparkY);
                    }
                }
            }
        }
    }

    function updateAndDrawBalloons(dt, now) {
        // Resolve pairwise balloon-to-balloon elastic collisions
        resolveBalloonBalloonCollisions(now);

        // Find current round leader among landed completed players with positive score (matching Parachute Drop)
        let leaderDropToken = null;
        let highestScore = -1;
        for (let i = 0; i < activeBalloons.length; i++) {
            const ab = activeBalloons[i];
            if (ab.completed && (ab.isHit || ab.isBullseye) && typeof ab.score === 'number' && ab.score > highestScore) {
                highestScore = ab.score;
                leaderDropToken = ab.dropToken;
            }
        }

        const toDraw = [];
        for (let i = activeBalloons.length - 1; i >= 0; i--) {
            const b = activeBalloons[i];

            if (!b.completed) {
                // Buoyancy ascent vs free-fall gravity when popped
                if (b.popped) {
                    b.vy += 2.2 * dt; // Gravity accelerates falling avatar downward!
                    b.y += b.vy * dt; // CRITICAL: Updates Y position downward!
                    b.tumbleAngle = (b.tumbleAngle || 0) + (b.spinSpeed || 7.5) * dt;
                    b.tilt = b.tumbleAngle;
                } else if (b.recoilTimer > 0) {
                    b.recoilTimer -= dt;
                    b.y += b.recoilVy * dt;
                    b.swayPhase += b.swayFreq * dt;
                    b.tilt = Math.sin(b.swayPhase) * 0.18 * b.swayAmount + (b.vx * 1.4);
                } else {
                    b.vy = b.baseVy;
                    b.y += b.vy * dt;
                    b.swayPhase += b.swayFreq * dt;
                    b.tilt = Math.sin(b.swayPhase) * 0.18 * b.swayAmount + (b.vx * 1.4);
                }

                b.x += b.vx * dt;

                // Screen boundary rebounds
                if (b.x < 0.05) {
                    b.x = 0.05;
                    b.vx = Math.abs(b.vx) * 0.75;
                } else if (b.x > 0.95) {
                    b.x = 0.95;
                    b.vx = -Math.abs(b.vx) * 0.75;
                }

                // Landing / Target Cloud reached (hit detection touches the top of the balloon canopy)
                const targetArrivalY = targetCloud.y + (b.balloonOffset || 0.165);
                if (!b.popped && b.y <= targetArrivalY) {
                    resolveBalloonArrival(b, now, targetArrivalY);
                } else if (b.popped && b.y >= 1.05) {
                    // Popped avatar fell off the bottom of the screen!
                    b.completed = true;
                    b.score = 0;
                    b.isWin = false;
                    b.isBullseye = false;
                    b.isHit = false;
                    sendCompletion({
                        dropToken: b.dropToken,
                        overlayKey: overlayKey,
                        score: 0,
                        isBullseye: false,
                        isWin: false,
                        isHit: false,
                        landingX: b.x,
                        balloonPopped: true
                    });
                }
            } else if (b.isHit && !b.popped) {
                // If landed on moving cloud platform, drift synchronously with the cloud
                if (typeof b.cloudRelativeX === 'number') {
                    b.x = targetCloud.centerX + b.cloudRelativeX;
                }
                // Snuggle avatar smoothly up against bottom rim of cloud platform to conserve vertical screen space
                const landedTargetY = targetCloud.y + 0.052;
                b.y += (landedTargetY - b.y) * 0.18;
                b.tilt = Math.sin(now * 0.003 + (b.swayPhase || 0)) * 0.05;
            }

            const isLeader = (sessionState.isActive || sessionState.endedAtMs) && b.completed && (b.isHit || b.isBullseye) && b.score > 0 && b.dropToken === leaderDropToken;
            let dropAlpha = 1.0;
            let shouldRemove = false;

            if (b.completed) {
                const elapsed = now - (b.landedAtMs || now);
                if (b.popped) {
                    // Popped balloons that dropped off the bottom of the screen
                    shouldRemove = true;
                } else if (!b.isHit) {
                    // Losers who missed the cloud platform:
                    // Drift upward slowly into the sky while smoothly fading out
                    // Linger for 1200ms so chatters clearly see the "MISS! 0 PTS" popup, then fade out over 1200ms
                    b.y -= 0.035 * dt;
                    b.swayPhase += b.swayFreq * 0.8 * dt;
                    b.tilt = Math.sin(b.swayPhase) * 0.12;
                    if (elapsed < 1200) {
                        dropAlpha = 1.0;
                    } else {
                        const fadeProgress = (elapsed - 1200) / 1200;
                        dropAlpha = Math.max(0, 1.0 - fadeProgress);
                        if (dropAlpha <= 0) {
                            shouldRemove = true;
                        }
                    }
                } else {
                    // Winners who successfully landed on the cloud platform:
                    if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.calculateDropAlpha) {
                        const alphaRes = DropGamesCommon.calculateDropAlpha(b.landedAtMs || now, now, sessionState, isLeader);
                        dropAlpha = alphaRes.alpha;
                        shouldRemove = alphaRes.shouldRemove;
                    } else if (sessionState.isActive) {
                        if (isLeader) {
                            dropAlpha = 1.0;
                            shouldRemove = false;
                        } else {
                            const dimDelay = 3500;
                            const dimDuration = 2000;
                            if (elapsed > dimDelay) {
                                const t = Math.min(1.0, (elapsed - dimDelay) / dimDuration);
                                dropAlpha = 1.0 - (0.60 * t);
                            } else {
                                dropAlpha = 1.0;
                            }
                            shouldRemove = false;
                        }
                    } else if (sessionState.endedAtMs) {
                        const postRoundAge = (now - sessionState.endedAtMs) / 1000;
                        if (postRoundAge > 6.0) {
                            shouldRemove = true;
                        } else if (postRoundAge > 3.5) {
                            dropAlpha = Math.max(0.0, 1.0 - ((postRoundAge - 3.5) / 2.5));
                        }
                    } else {
                        const lingerTime = 8000;
                        const fadeTime = 2500;
                        if (elapsed > lingerTime) {
                            dropAlpha = Math.max(0.0, 1.0 - ((elapsed - lingerTime) / fadeTime));
                            if (dropAlpha <= 0.0) {
                                shouldRemove = true;
                            }
                        }
                    }
                }
            }

            if (shouldRemove) {
                activeBalloons.splice(i, 1);
            } else {
                toDraw.push({ balloon: b, dropAlpha: dropAlpha, isLeader: isLeader, score: b.score || 0, completed: b.completed });
            }
        }

        // Draw in z-order: non-leaders first, and current winning leader LAST so it's always on top of others!
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.sortDropsForDraw) {
            DropGamesCommon.sortDropsForDraw(toDraw);
        } else {
            toDraw.sort((a, b) => {
                if (a.isLeader !== b.isLeader) return a.isLeader ? 1 : -1;
                if (a.completed !== b.completed) return a.completed ? -1 : 1;
                return (a.score || 0) - (b.score || 0);
            });
        }

        for (let i = 0; i < toDraw.length; i++) {
            const item = toDraw[i];
            drawBalloon(item.balloon, now, item.dropAlpha, item.isLeader);
        }
    }

    function resolveBalloonArrival(b, now, arrivalY) {
        b.completed = true;
        b.landedAtMs = now;
        const bOff = b.balloonOffset || 0.165;
        b.y = arrivalY !== undefined ? arrivalY : (targetCloud.y + bOff);

        const dist = Math.abs(b.x - targetCloud.centerX);
        b.isHit = dist <= targetCloud.halfWidth;

        if (b.isHit) {
            b.cloudRelativeX = b.x - targetCloud.centerX;
            const ratio = Math.min(1.0, dist / Math.max(0.0001, targetCloud.halfWidth));
            b.score = Math.max(1, Math.min(100, Math.round(100.0 - ratio * 99.0)));
            b.isBullseye = b.score >= 98;
            b.isWin = true;

            spawnWinConfetti(b.x, targetCloud.y);
            playSound(b.sounds?.balloonWin || { enabled: true }, b.isBullseye ? 'bullseye' : 'land', b.isBullseye ? 880 : 660);

            scorePopups.push({
                x: b.x,
                y: targetCloud.y - 0.04,
                text: b.isBullseye ? `GOLDEN BULLSEYE! ${b.score} PTS` : `+${b.score} PTS!`,
                color: b.isBullseye ? '#FFD166' : '#38EF7D',
                alpha: 1.0,
                createdAtMs: now
            });
        } else {
            b.score = 0;
            b.isWin = false;
            b.isBullseye = false;

            // Miss popup & miss sound for losers
            playSound(b.sounds?.balloonLose || b.sounds?.balloonBonk || { enabled: true }, 'miss', 320);

            scorePopups.push({
                x: b.x,
                y: b.y - bOff - 0.04,
                text: 'MISS! 0 PTS',
                color: '#94A3B8',
                alpha: 1.0,
                createdAtMs: now
            });
        }

        // Send trusted completion to server with authorized key
        sendCompletion({
            dropToken: b.dropToken,
            overlayKey: overlayKey,
            score: b.score,
            isBullseye: b.isBullseye,
            isWin: b.isWin,
            isHit: b.isHit,
            landingX: b.x,
            balloonPopped: b.popped
        });
    }

    function drawInlineDefaultPenguin(ctx, r, season, isPopped, swayPhase, now) {
        ctx.save();

        // 1. Cute dangling / kicking webbed feet
        const kick = isPopped
            ? Math.sin(now * 0.02) * (r * 0.2)
            : Math.sin(now * 0.005 + (swayPhase || 0)) * (r * 0.1);
        ctx.fillStyle = '#F59E0B';
        ctx.beginPath();
        ctx.ellipse(-r * 0.32, r * 0.86 + kick, r * 0.22, r * 0.13, -0.25, 0, Math.PI * 2);
        ctx.ellipse(r * 0.32, r * 0.86 - kick, r * 0.22, r * 0.13, 0.25, 0, Math.PI * 2);
        ctx.fill();

        // 2. Wings / Flippers
        ctx.fillStyle = '#2B2D42';
        if (isPopped) {
            // Flailing wings in free-fall panic \o/
            ctx.beginPath();
            ctx.moveTo(-r * 0.65, 0);
            ctx.quadraticCurveTo(-r * 1.25, -r * 0.7, -r * 1.15, -r * 0.4);
            ctx.quadraticCurveTo(-r * 0.8, -r * 0.1, -r * 0.65, r * 0.25);
            ctx.closePath();
            ctx.fill();

            ctx.beginPath();
            ctx.moveTo(r * 0.65, 0);
            ctx.quadraticCurveTo(r * 1.25, -r * 0.7, r * 1.15, -r * 0.4);
            ctx.quadraticCurveTo(r * 0.8, -r * 0.1, r * 0.65, r * 0.25);
            ctx.closePath();
            ctx.fill();
        } else {
            // Reaching UP clutching the trapeze bar overhead!
            ctx.lineWidth = r * 0.24;
            ctx.strokeStyle = '#2B2D42';
            ctx.lineCap = 'round';
            ctx.beginPath();
            ctx.moveTo(-r * 0.65, r * 0.05);
            ctx.quadraticCurveTo(-r * 0.70, -r * 0.50, -r * 0.45, -r * 0.88);
            ctx.stroke();

            ctx.beginPath();
            ctx.moveTo(r * 0.65, r * 0.05);
            ctx.quadraticCurveTo(r * 0.70, -r * 0.50, r * 0.45, -r * 0.88);
            ctx.stroke();

            // Mitten hands clutching the bar
            ctx.beginPath();
            ctx.arc(-r * 0.45, -r * 0.88, r * 0.14, 0, Math.PI * 2);
            ctx.arc(r * 0.45, -r * 0.88, r * 0.14, 0, Math.PI * 2);
            ctx.fill();
        }

        // 3. Body
        ctx.fillStyle = '#2B2D42';
        ctx.beginPath();
        ctx.arc(0, 0, r, 0, Math.PI * 2);
        ctx.fill();

        // 4. White belly
        ctx.fillStyle = '#F8F9FA';
        ctx.beginPath();
        ctx.ellipse(0, r * 0.14, r * 0.65, r * 0.72, 0, 0, Math.PI * 2);
        ctx.fill();

        // 5. Eyes & Beak
        if (isPopped) {
            // Startled cartoon panic eyes (X X)
            ctx.strokeStyle = '#111827';
            ctx.lineWidth = 2.4;
            // Left X
            ctx.beginPath();
            ctx.moveTo(-r * 0.38, -r * 0.30); ctx.lineTo(-r * 0.18, -r * 0.10);
            ctx.moveTo(-r * 0.18, -r * 0.30); ctx.lineTo(-r * 0.38, -r * 0.10);
            // Right X
            ctx.moveTo(r * 0.18, -r * 0.30); ctx.lineTo(r * 0.38, -r * 0.10);
            ctx.moveTo(r * 0.38, -r * 0.30); ctx.lineTo(r * 0.18, -r * 0.10);
            ctx.stroke();

            // Open surprised beak (gasping)
            ctx.fillStyle = '#FFB703';
            ctx.beginPath();
            ctx.ellipse(0, r * 0.08, r * 0.16, r * 0.20, 0, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#B45309';
            ctx.beginPath();
            ctx.ellipse(0, r * 0.08, r * 0.09, r * 0.12, 0, 0, Math.PI * 2);
            ctx.fill();
        } else {
            // Cheerful eyes looking UP toward the balloon & cloud
            ctx.fillStyle = '#111827';
            ctx.beginPath();
            ctx.arc(-r * 0.28, -r * 0.22, r * 0.13, 0, Math.PI * 2);
            ctx.arc(r * 0.28, -r * 0.22, r * 0.13, 0, Math.PI * 2);
            ctx.fill();

            // Eye shine (upward looking highlights)
            ctx.fillStyle = '#FFFFFF';
            ctx.beginPath();
            ctx.arc(-r * 0.30, -r * 0.26, r * 0.045, 0, Math.PI * 2);
            ctx.arc(r * 0.26, -r * 0.26, r * 0.045, 0, Math.PI * 2);
            ctx.fill();

            // Beak
            ctx.fillStyle = '#FFB703';
            ctx.beginPath();
            ctx.moveTo(-r * 0.18, -r * 0.05);
            ctx.lineTo(r * 0.18, -r * 0.05);
            ctx.lineTo(0, r * 0.18);
            ctx.closePath();
            ctx.fill();
        }

        // 6. Seasonal Headwear / Accessory
        if (season === 'spring') {
            ctx.fillStyle = '#FF8FA3';
            ctx.beginPath();
            ctx.arc(0, -r * 0.92, r * 0.28, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#FFE066';
            ctx.beginPath();
            ctx.arc(0, -r * 0.92, r * 0.11, 0, Math.PI * 2);
            ctx.fill();
        } else if (season === 'summer') {
            ctx.fillStyle = '#00B4D8';
            ctx.fillRect(-r * 0.58, -r * 0.36, r * 0.48, r * 0.26);
            ctx.fillRect(r * 0.10, -r * 0.36, r * 0.48, r * 0.26);
            ctx.strokeStyle = '#FFD166';
            ctx.lineWidth = 2;
            ctx.strokeRect(-r * 0.58, -r * 0.36, r * 0.48, r * 0.26);
            ctx.strokeRect(r * 0.10, -r * 0.36, r * 0.48, r * 0.26);
        } else if (season === 'fall') {
            ctx.fillStyle = '#E76F51';
            ctx.beginPath();
            ctx.rect(-r * 0.78, r * 0.22, r * 1.56, r * 0.28);
            ctx.fill();
        } else if (season === 'winter') {
            ctx.fillStyle = '#48CAE4';
            ctx.beginPath();
            ctx.arc(0, -r * 0.55, r * 0.75, Math.PI, 0);
            ctx.fill();
            ctx.fillStyle = '#FFFFFF';
            ctx.beginPath();
            ctx.arc(0, -r * 1.25, r * 0.22, 0, Math.PI * 2);
            ctx.fill();
        } else if (season === 'halloween') {
            ctx.fillStyle = '#212529';
            ctx.beginPath();
            ctx.moveTo(-r * 0.72, -r * 0.65);
            ctx.lineTo(r * 0.72, -r * 0.65);
            ctx.lineTo(r * 0.22, -r * 1.55);
            ctx.closePath();
            ctx.fill();
            ctx.beginPath();
            ctx.ellipse(0, -r * 0.65, r * 0.88, r * 0.25, 0, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#7B2CBF';
            ctx.fillRect(-r * 0.45, -r * 0.85, r * 0.9, r * 0.22);
            ctx.fillStyle = '#FFD166';
            ctx.fillRect(-r * 0.16, -r * 0.88, r * 0.32, r * 0.28);
        } else if (season === 'christmas') {
            ctx.fillStyle = '#D90429';
            ctx.beginPath();
            ctx.moveTo(-r * 0.68, -r * 0.65);
            ctx.lineTo(r * 0.68, -r * 0.65);
            ctx.quadraticCurveTo(r * 0.85, -r * 1.35, r * 0.45, -r * 1.45);
            ctx.quadraticCurveTo(r * 0.1, -r * 1.15, -r * 0.68, -r * 0.65);
            ctx.fill();
            ctx.fillStyle = '#FFFFFF';
            ctx.fillRect(-r * 0.74, -r * 0.75, r * 1.48, r * 0.26);
            ctx.beginPath();
            ctx.arc(r * 0.52, -r * 1.46, r * 0.24, 0, Math.PI * 2);
            ctx.fill();
        }
        ctx.restore();
    }

    function drawSingleBalloonCanopy(ctx, x, y, r, angle, color, shadow, highlight, style) {
        ctx.save();
        ctx.translate(x, y);
        if (angle) ctx.rotate(angle);

        // Radial lighting for 3D sphere volume
        const grad = ctx.createRadialGradient(-r * 0.32, -r * 0.35, r * 0.08, 0, 0, r * 1.3);
        grad.addColorStop(0, '#FFFFFF');
        grad.addColorStop(0.28, highlight);
        grad.addColorStop(0.55, color);
        grad.addColorStop(1.0, shadow);

        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.ellipse(0, 0, r * 0.95, r * 1.25, 0, 0, Math.PI * 2);
        ctx.fill();

        // Pattern overlays
        if (style === 'striped') {
            ctx.save();
            ctx.clip(); // clip to balloon ellipse
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.42)';
            ctx.lineWidth = r * 0.22;
            ctx.beginPath();
            ctx.ellipse(0, 0, r * 0.48, r * 1.25, 0, 0, Math.PI * 2);
            ctx.stroke();
            ctx.restore();
        } else if (style === 'polka') {
            ctx.save();
            ctx.clip();
            ctx.fillStyle = 'rgba(255, 255, 255, 0.55)';
            const dots = [
                [-r * 0.4, -r * 0.5], [r * 0.4, -r * 0.4],
                [0, -r * 0.15],
                [-r * 0.45, r * 0.3], [r * 0.35, r * 0.35],
                [0, r * 0.65]
            ];
            for (let d = 0; d < dots.length; d++) {
                ctx.beginPath();
                ctx.arc(dots[d][0], dots[d][1], r * 0.10, 0, Math.PI * 2);
                ctx.fill();
            }
            ctx.restore();
        }

        // Curved specular white highlight sheen on top-left shoulder
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.75)';
        ctx.lineWidth = Math.max(2, r * 0.10);
        ctx.lineCap = 'round';
        ctx.beginPath();
        ctx.arc(-r * 0.30, -r * 0.35, r * 0.45, Math.PI * 0.85, Math.PI * 1.35);
        ctx.stroke();

        ctx.restore();
    }

    function drawBalloon(b, now, dropAlpha = 1.0, isLeader = false) {
        const bx = b.x * width;
        const by = b.y * height;
        const br = Math.max(22, b.canopyRadius * Math.min(width, height));
        const charR = Math.max(18, b.radius * Math.min(width, height) * 0.90);
        const balloonOffsetPx = (b.balloonOffset || 0.165) * height;

        ctx.save();
        ctx.globalAlpha = Math.max(0, Math.min(1.0, typeof dropAlpha === 'number' ? dropAlpha : 1.0));
        ctx.translate(bx, by);
        ctx.rotate(b.tilt);

        const showCanopy = !b.popped && (!b.completed || !b.isHit);
        if (showCanopy) {
            const barY = -charR * 0.88;
            const leftGripX = -charR * 0.45;
            const rightGripX = charR * 0.45;

            // Canopy center is balloonOffsetPx above avatar
            const cy = -balloonOffsetPx;
            // Knot sits at the bottom of the balloon teardrop canopy
            const knotY = cy + br * 1.22;

            // Dual Suspension Cords from Knot to Trapeze Bar
            ctx.save();
            ctx.shadowColor = 'rgba(0,0,0,0.30)';
            ctx.shadowBlur = 4;
            ctx.strokeStyle = '#F1F5F9';
            ctx.lineWidth = 1.8;
            ctx.beginPath();
            ctx.moveTo(0, knotY);
            ctx.lineTo(leftGripX, barY);
            ctx.moveTo(0, knotY);
            ctx.lineTo(rightGripX, barY);
            ctx.moveTo(0, knotY);
            ctx.lineTo(0, barY); // Center stabilizing cord
            ctx.stroke();

            // Wooden/brass trapeze crossbar held by avatar
            ctx.strokeStyle = '#B45309';
            ctx.lineWidth = 3.6;
            ctx.lineCap = 'round';
            ctx.beginPath();
            ctx.moveTo(leftGripX - 7, barY);
            ctx.lineTo(rightGripX + 7, barY);
            ctx.stroke();

            // Metallic end rings
            ctx.fillStyle = '#FDE047';
            ctx.beginPath();
            ctx.arc(leftGripX, barY, 2.8, 0, Math.PI * 2);
            ctx.arc(rightGripX, barY, 2.8, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();

            // Balloon Canopy
            let drawnCanopy = false;
            if (b.balloonCanopyImageUrl) {
                const img = getLoadedImage(b.balloonCanopyImageUrl);
                if (img) {
                    const bw = br * 2.2;
                    const bh = bw * (img.height / img.width);
                    ctx.drawImage(img, -bw / 2, cy - bh / 2, bw, bh);
                    drawnCanopy = true;
                }
            }

            if (!drawnCanopy) {
                const bColor = b.balloonColor || b.theme?.primaryColor || '#EF4444';
                const bShadow = b.balloonShadow || '#991B1B';
                const bHigh = b.balloonHighlight || '#FCA5A5';
                const style = b.balloonStyle || 'solid';

                ctx.save();
                ctx.shadowColor = 'rgba(0, 0, 0, 0.28)';
                ctx.shadowBlur = 10;

                if (style === 'double') {
                    // Double balloon duo tied together!
                    drawSingleBalloonCanopy(ctx, -br * 0.45, cy - br * 0.1, br * 0.85, -0.15, bColor, bShadow, bHigh);
                    drawSingleBalloonCanopy(ctx, br * 0.45, cy - br * 0.1, br * 0.85, 0.15, bColor, bShadow, bHigh);
                } else {
                    drawSingleBalloonCanopy(ctx, 0, cy, br, 0, bColor, bShadow, bHigh, style);
                }

                // Tie knot at bottom
                ctx.fillStyle = bColor;
                ctx.beginPath();
                ctx.moveTo(-br * 0.14, knotY);
                ctx.lineTo(br * 0.14, knotY);
                ctx.lineTo(0, knotY + br * 0.18);
                ctx.closePath();
                ctx.fill();

                // Fluttering ribbon tails
                ctx.strokeStyle = bHigh;
                ctx.lineWidth = 1.6;
                ctx.beginPath();
                ctx.moveTo(0, knotY + br * 0.18);
                const ribWave = Math.sin(now * 0.008 + (b.swayPhase || 0)) * 6;
                ctx.quadraticCurveTo(ribWave, knotY + br * 0.40, ribWave * 1.5, knotY + br * 0.65);
                ctx.stroke();

                ctx.restore();
            }
        }

        // Avatar / Character dangling below
        let drawnChar = false;
        if (b.characterImageUrl) {
            const charImg = getLoadedImage(b.characterImageUrl);
            if (charImg) {
                const sz = charR * 2.3;
                ctx.drawImage(charImg, -sz / 2, -sz / 2, sz, sz);
                drawnChar = true;

                // If holding balloon trapeze, draw cute little paws/hands holding the bar in front of the emote!
                if (showCanopy) {
                    const barY = -charR * 0.88;
                    ctx.fillStyle = '#1E293B';
                    ctx.beginPath();
                    ctx.arc(-charR * 0.45, barY, 4.2, 0, Math.PI * 2);
                    ctx.arc(charR * 0.45, barY, 4.2, 0, Math.PI * 2);
                    ctx.fill();

                    // Cute little orange dangling feet kicking beneath the emote!
                    const kick = Math.sin(now * 0.006 + (b.swayPhase || 0)) * (charR * 0.1);
                    ctx.fillStyle = '#F59E0B';
                    ctx.beginPath();
                    ctx.ellipse(-charR * 0.28, charR * 0.78 + kick, 6, 3.5, -0.2, 0, Math.PI * 2);
                    ctx.ellipse(charR * 0.28, charR * 0.78 - kick, 6, 3.5, 0.2, 0, Math.PI * 2);
                    ctx.fill();
                }
            }
        }

        const season = ((b.theme && b.theme.seasonPreset) || 'spring').toLowerCase();

        // Round Leader subtle golden shadow glow (matching Parachute Drop & Plinko)
        if (isLeader && b.completed) {
            ctx.shadowColor = 'rgba(255, 215, 0, 0.75)';
            ctx.shadowBlur = 16;
        }

        if (!drawnChar) {
            drawInlineDefaultPenguin(ctx, charR, season, b.popped, b.swayPhase, now);
            drawnChar = true;
        }

        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.drawOutlinedName) {
            DropGamesCommon.drawOutlinedName(ctx, {
                name: b.user,
                userColor: b.userColor || b.theme?.textColor,
                fontSize: b.nameFontSize || headerConfig.nameFontSize,
                score: b.score,
                completed: b.completed,
                isLeader: isLeader,
                rotation: b.tilt,
                radius: charR,
                position: 'below'
            });
        }

        ctx.restore();
    }

    function updateAndDrawParticles(dt) {
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.updateAndDrawParticles) {
            DropGamesCommon.updateAndDrawParticles(ctx, particles, { dt, width, height, gravity: 45 });
        }
    }

    function drawScorePopups(dt, now) {
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.updateAndDrawPopups) {
            DropGamesCommon.updateAndDrawPopups(ctx, scorePopups, {
                now,
                dt,
                width,
                height,
                maxAge: 2.5,
                driftSpeed: 0.02 * (height || 1080),
                isNormalized: true
            });
        }
    }

    function drawSessionBanner(now) {
        const hasActiveBalloon = activeBalloons.some(b => !b.completed || (now - (b.landedAtMs || now)) < 6000);
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.drawSessionBanner) {
            const res = DropGamesCommon.drawSessionBanner(ctx, {
                now,
                width,
                headerConfig,
                sessionState,
                currentBannerY,
                currentGame: 'balloon',
                activeOtherGames,
                isOtherGameActive,
                otherDropsActiveUntilMs,
                hasActiveDrops: hasActiveBalloon,
                itemPluralName: 'Balloons',
                primaryColor: currentTheme?.primaryColor || '#38BDF8',
                accentColor: currentTheme?.accentColor || '#FFE066'
            });
            currentBannerY = res.currentBannerY;
            if (!sessionState.isActive && !hasActiveBalloon && activeBalloons.length === 0) {
                sessionState.totalDrops = 0;
            }
            return;
        }

        // Fallback banner drawing
        let targetY = typeof headerConfig.yOffset === 'number' ? headerConfig.yOffset : 18;
        if (headerConfig.autoAvoidOverlap !== false) {
            let slot = (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.calculateBannerStackSlot)
                ? DropGamesCommon.calculateBannerStackSlot('balloon', activeOtherGames)
                : (isOtherGameActive ? 1 : 0);
            if (activeOtherGames.size === 0 && !isOtherGameActive && now < otherDropsActiveUntilMs && slot === 0) slot = 1;
            targetY += slot * 54;
        }
        currentBannerY += (targetY - currentBannerY) * 0.15;
        const bannerY = Math.round(currentBannerY);

        if (!sessionState.isActive) {
            if (sessionState.endedAtMs) {
                const ageSec = (now - sessionState.endedAtMs) / 1000;
                if (ageSec <= 6.0) {
                    if (headerConfig.showBanner === false) return;

                    const bannerW = Math.min(width * 0.72, 480);
                    const bannerH = 46;
                    const bannerX = getBannerX(bannerW);
                    const bannerCenterX = bannerX + bannerW / 2;

                    ctx.save();
                    const alpha = Math.max(0, 1.0 - (ageSec / 6.0));
                    ctx.globalAlpha = alpha;

                    ctx.fillStyle = 'rgba(15, 23, 42, 0.88)';
                    ctx.strokeStyle = '#10B981';
                    ctx.lineWidth = 2;
                    ctx.beginPath();
                    if (typeof ctx.roundRect === 'function') {
                        ctx.roundRect(bannerX, bannerY, bannerW, bannerH, 23);
                    } else {
                        ctx.rect(bannerX, bannerY, bannerW, bannerH);
                    }
                    ctx.fill();
                    ctx.stroke();

                    ctx.font = '700 17px Fredoka, Rubik, sans-serif';
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillStyle = '#34D399';
                    ctx.shadowColor = 'rgba(0,0,0,0.85)';
                    ctx.shadowBlur = 6;
                    ctx.fillText(`Round Closed! Total Balloons: ${sessionState.totalDrops}`, bannerCenterX, bannerY + bannerH / 2);
                    ctx.restore();
                    return;
                } else {
                    sessionState.endedAtMs = 0;
                    sessionState.totalDrops = 0;
                    return;
                }
            }

            if (hasActiveBalloon && sessionState.totalDrops > 0 && headerConfig.showFreePlayCounter !== false) {
                ctx.save();
                const pillW = 210;
                const pillH = 38;
                const pillX = getBannerX(pillW);

                ctx.fillStyle = 'rgba(15, 23, 42, 0.78)';
                ctx.strokeStyle = currentTheme?.primaryColor || '#38BDF8';
                ctx.lineWidth = 2;
                ctx.beginPath();
                if (typeof ctx.roundRect === 'function') {
                    ctx.roundRect(pillX, bannerY, pillW, pillH, 19);
                } else {
                    ctx.rect(pillX, bannerY, pillW, pillH);
                }
                ctx.fill();
                ctx.stroke();

                ctx.font = '700 16px Fredoka, Rubik, sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillStyle = '#FFFFFF';
                ctx.shadowColor = 'rgba(0,0,0,0.85)';
                ctx.shadowBlur = 6;
                ctx.fillText(`Balloons: ${sessionState.totalDrops}`, pillX + pillW / 2, bannerY + pillH / 2);
                ctx.restore();
            } else if (!hasActiveBalloon && activeBalloons.length === 0) {
                sessionState.totalDrops = 0;
            }
            return;
        }

        if (headerConfig.showBanner === false) return;

        const showTimer = headerConfig.showTimer !== false;
        const showCount = headerConfig.showCount !== false;

        let bannerW;
        if (showTimer && showCount) {
            bannerW = Math.min(width * 0.72, 480);
        } else if (showTimer) {
            bannerW = Math.min(width * 0.45, 270);
        } else if (showCount) {
            bannerW = Math.min(width * 0.40, 230);
        } else {
            bannerW = Math.min(width * 0.35, 180);
        }

        const bannerH = 46;
        const bannerX = getBannerX(bannerW);
        const bannerCenterX = bannerX + bannerW / 2;

        ctx.save();
        const remainingSec = Math.max(0, Math.ceil((sessionState.endsAtMs - now) / 1000));
        const totalSec = Math.max(1, sessionState.totalDurationSeconds || 60);
        const progress = Math.max(0, Math.min(1, remainingSec / totalSec));

        const mins = Math.floor(remainingSec / 60);
        const secs = remainingSec % 60;
        const timeStr = `${mins}:${secs < 10 ? '0' : ''}${secs}`;

        ctx.fillStyle = 'rgba(15, 23, 42, 0.78)';
        ctx.strokeStyle = currentTheme?.primaryColor || '#38BDF8';
        ctx.lineWidth = 2;
        ctx.beginPath();
        if (typeof ctx.roundRect === 'function') {
            ctx.roundRect(bannerX, bannerY, bannerW, bannerH, 23);
        } else {
            ctx.rect(bannerX, bannerY, bannerW, bannerH);
        }
        ctx.fill();
        ctx.stroke();

        const barW = (bannerW - 32) * progress;
        ctx.fillStyle = currentTheme?.accentColor || '#FFE066';
        ctx.beginPath();
        if (typeof ctx.roundRect === 'function') {
            ctx.roundRect(bannerX + 16, bannerY + bannerH - 8, barW, 4, 2);
        } else {
            ctx.rect(bannerX + 16, bannerY + bannerH - 8, barW, 4);
        }
        ctx.fill();

        let bannerText = '';
        if (showTimer && showCount) {
            bannerText = `Round Ends: ${timeStr}  |  Balloons: ${sessionState.totalDrops}`;
        } else if (showTimer) {
            bannerText = `Round Ends: ${timeStr}`;
        } else if (showCount) {
            bannerText = `Balloons: ${sessionState.totalDrops}`;
        }

        if (bannerText) {
            ctx.font = '700 17px Fredoka, Rubik, sans-serif';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillStyle = '#FFFFFF';
            ctx.shadowColor = 'rgba(0,0,0,0.85)';
            ctx.shadowBlur = 6;
            ctx.fillText(bannerText, bannerCenterX, bannerY + bannerH / 2 - 2);
        }
        ctx.restore();
    }

    requestAnimationFrame(render);
})(typeof window !== 'undefined' ? window : this);

