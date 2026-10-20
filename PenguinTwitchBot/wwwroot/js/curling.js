/**
 * Curling / Shuffleboard Overlay Engine
 * Features:
 * - 60fps high-DPI transparent Canvas physics engine
 * - Fully configurable edge strip overlay: Bottom, Top, Left, Right with adjustable thickness (10-25%)
 * - Inward-facing nametags & scores (rendered inward towards screen center to prevent edge clipping)
 * - Anti-griefing LockOnRest collision mode: sliding stones collide elastically, but ghost through stopped stones
 * - Dynamic ice sheet conditions (Slick -20% drag, Heavy Frost +25% drag, Cross-Drift) with HUD pill indicator
 * - Dual scoring modes: CurlingHouse (Button, 4ft, 8ft, 12ft) and Shuffleboard (Zone 3, Zone 2, Zone 1, Kitchen)
 * - Granite curling stone renderer with chatter avatar/emote decal, gooseneck handle, and resting frost glint
 * - Direct client trust drop completion with authorized overlayKey
 * - Auto-hiding track canvas when no stones or sessions are active
 */
(function (global) {
    'use strict';

    const canvas = document.getElementById('curling-canvas');
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

    // Audio synthesizer via DropGamesAudio
    const audio = (typeof DropGamesAudio !== 'undefined' && DropGamesAudio.createAudioEngine)
        ? DropGamesAudio.createAudioEngine()
        : null;

    let soundBundleManager = null;
    if (typeof DropGamesAudio !== 'undefined' && typeof DropGamesAudio.createSoundBundleManager === 'function') {
        soundBundleManager = DropGamesAudio.createSoundBundleManager();
    }

    function playSound(soundDto, synthType = 'slide', defaultFreq = 440) {
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

        if (audio) {
            try {
                if (synthType === 'slide' && typeof audio.playCurlingSlide === 'function') {
                    audio.playCurlingSlide(volFrac);
                } else if (synthType === 'sweep' && typeof audio.playCurlingSweep === 'function') {
                    audio.playCurlingSweep(volFrac);
                } else if (synthType === 'hit' && typeof audio.playCurlingHit === 'function') {
                    audio.playCurlingHit(volFrac);
                } else if (synthType === 'score' && typeof audio.playCurlingScore === 'function') {
                    audio.playCurlingScore(volFrac);
                } else if (synthType === 'win' && typeof audio.playBullseyeFanfare === 'function') {
                    audio.playBullseyeFanfare(volFrac);
                } else if (typeof audio.playProceduralChime === 'function') {
                    audio.playProceduralChime(defaultFreq, volFrac);
                }
            } catch (_) {}
        }
    }

    // Board & Game Settings
    const boardConfig = {
        stripPosition: 'Bottom',        // Bottom, Top, Left, Right
        slideDirection: 'LeftToRight',  // LeftToRight, RightToLeft, TopToBottom, BottomToTop, Alternating, CenterTarget
        isReversed: false,
        trackThicknessPercent: 14.0,    // 10% - 25%
        boardAlpha: 0.0,                // 0.0 transparent by default
        showBoardBorders: true,         // guide rails
        targetAlpha: 0.85,
        scoringType: 'CurlingHouse',     // CurlingHouse, Shuffleboard
        collisionMode: 'LockOnRest',    // LockOnRest, NoCollision, FullCollision
        condition: 'Normal',            // Normal, Slick, HeavyFrost, CrossDrift
        conditionLabel: 'Normal Ice',
        targetDistancePercent: 78.0,    // 70% - 92%
        enableShuffleboardKitchenPenalty: true,
        powerVariancePercent: 3.0,
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

    const sessionState = {
        isActive: false,
        totalDurationSeconds: 90,
        remainingSeconds: 0,
        startedAtMs: 0,
        endsAtMs: 0,
        endedAtMs: 0,
        totalDrops: 0
    };

    let activeStones = [];
    let ambientParticles = [];
    let particles = [];
    let scorePopups = [];
    let currentTheme = null;

    function getSeasonPreset() {
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.getSeasonPreset) {
            return DropGamesCommon.getSeasonPreset(currentTheme);
        }
        return 'spring';
    }

    function syncAmbientParticles(isBoardVisible) {
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.syncAmbientParticles) {
            DropGamesCommon.syncAmbientParticles(ambientParticles, {
                width,
                height,
                currentTheme,
                isVisible: isBoardVisible,
                maxDensity: 60
            });
        }
    }

    function updateAndDrawAmbientParticles(dtSec, isBoardVisible) {
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.updateAndDrawAmbientParticles) {
            DropGamesCommon.updateAndDrawAmbientParticles(ctx, ambientParticles, {
                dtSec,
                width,
                height,
                currentTheme,
                isVisible: isBoardVisible
            });
        }
    }

    function spawnSeasonalBurst(x, y, season, count, isBullseye) {
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.spawnSeasonalBurst) {
            DropGamesCommon.spawnSeasonalBurst(particles, { x, y, season, count, isBullseye });
        }
    }

    function updateAndDrawParticles(dt) {
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.updateAndDrawParticles) {
            DropGamesCommon.updateAndDrawParticles(ctx, particles, { dt, width, height, gravity: 35 });
        }
    }

    let isOtherGameActive = false;
    const activeOtherGames = new Set();
    let otherDropsActiveUntilMs = 0;
    let currentBannerY = 18;
    let lastCollisionSoundMs = 0;

    // WebSocket connection & completions queue
    const socket = window.createWsSocket('curling', [window.WS_TOPICS.curling]);

    // Shared completion queue + heartbeat (see DropGamesCommon.createCompletionChannel)
    const completionChannel = DropGamesCommon.createCompletionChannel({
        game: 'curling',
        socket: socket,
        overlayKey: overlayKey,
        fields: ['dropToken', 'score', 'isBullseye', 'isWin', 'isHit', 'landingX'],
        getActiveTokens: () => Array.isArray(activeStones)
            ? activeStones.filter(s => s && s.dropToken && !s.completed).map(s => s.dropToken)
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
            if (typeof msg.otherGame === 'string' && msg.otherGame.length > 0) {
                const g = msg.otherGame.toLowerCase();
                if (msg.isOtherGameActive) {
                    activeOtherGames.add(g);
                } else {
                    activeOtherGames.delete(g);
                }
            }
            if (Array.isArray(msg.activeGames)) {
                activeOtherGames.clear();
                msg.activeGames.forEach(g => {
                    if (typeof g === 'string' && g.toLowerCase() !== 'curling') {
                        activeOtherGames.add(g.toLowerCase());
                    }
                });
            }
            isOtherGameActive = activeOtherGames.size > 0 || !!msg.isOtherGameActive;
            return;
        }

        if (msg.type === 'other_drop_active') {
            const durSec = typeof msg.durationSeconds === 'number' ? msg.durationSeconds : 10.0;
            otherDropsActiveUntilMs = Math.max(otherDropsActiveUntilMs, performance.now() + durSec * 1000);
            return;
        }

        if (msg.type === 'parachute_drop' || msg.type === 'plinko_drop' || msg.type === 'balloon_drop') {
            const durSec = typeof msg.durationSeconds === 'number' ? msg.durationSeconds : 10.0;
            otherDropsActiveUntilMs = Math.max(otherDropsActiveUntilMs, performance.now() + (durSec + 6.0) * 1000);
            return;
        }

        const activeList = msg.activeGames || (msg.session && msg.session.activeGames);
        if (Array.isArray(activeList)) {
            activeOtherGames.clear();
            activeList.forEach(g => {
                if (typeof g === 'string' && g.toLowerCase() !== 'curling') {
                    activeOtherGames.add(g.toLowerCase());
                }
            });
            isOtherGameActive = activeOtherGames.size > 0;
        } else if (typeof msg.isOtherGameActive === 'boolean') {
            isOtherGameActive = msg.isOtherGameActive;
        } else if (msg.session && typeof msg.session.isOtherGameActive === 'boolean') {
            isOtherGameActive = msg.session.isOtherGameActive;
        }

        if (msg.type === 'session_start') {
            const s = msg.session || {};
            sessionState.isActive = true;
            sessionState.endedAtMs = 0;
            sessionState.totalDurationSeconds = s.totalDurationSeconds || 90;
            sessionState.remainingSeconds = s.remainingSeconds || sessionState.totalDurationSeconds;
            sessionState.startedAtMs = performance.now();
            sessionState.endsAtMs = performance.now() + (s.remainingSeconds || s.totalDurationSeconds || 90) * 1000;
            sessionState.totalDrops = typeof s.totalDrops === 'number' ? s.totalDrops : 0;

            if (msg.headerConfig) Object.assign(headerConfig, msg.headerConfig);
            else if (s.headerConfig) Object.assign(headerConfig, s.headerConfig);
            if (msg.theme) currentTheme = msg.theme;

            if (typeof msg.stripPosition === 'string') boardConfig.stripPosition = msg.stripPosition;
            if (typeof msg.slideDirection === 'string') boardConfig.slideDirection = msg.slideDirection;
            if (typeof msg.isReversed === 'boolean') boardConfig.isReversed = msg.isReversed;
            else boardConfig.isReversed = (boardConfig.slideDirection === 'RightToLeft' || boardConfig.slideDirection === 'BottomToTop');
            if (typeof msg.trackThicknessPercent === 'number') boardConfig.trackThicknessPercent = msg.trackThicknessPercent;
            if (typeof msg.boardAlpha === 'number') boardConfig.boardAlpha = msg.boardAlpha;
            if (typeof msg.showBoardBorders === 'boolean') boardConfig.showBoardBorders = msg.showBoardBorders;
            if (typeof msg.targetAlpha === 'number') boardConfig.targetAlpha = msg.targetAlpha;
            if (typeof msg.scoringType === 'string') boardConfig.scoringType = msg.scoringType;
            if (typeof msg.collisionMode === 'string') boardConfig.collisionMode = msg.collisionMode;
            if (typeof msg.condition === 'string') boardConfig.condition = msg.condition;
            if (typeof msg.conditionLabel === 'string') boardConfig.conditionLabel = msg.conditionLabel;
            if (typeof msg.targetDistancePercent === 'number') boardConfig.targetDistancePercent = msg.targetDistancePercent;
            if (typeof msg.enableShuffleboardKitchenPenalty === 'boolean') boardConfig.enableShuffleboardKitchenPenalty = msg.enableShuffleboardKitchenPenalty;
            if (typeof msg.powerVariancePercent === 'number') boardConfig.powerVariancePercent = msg.powerVariancePercent;

            boardConfig.visibleUntil = performance.now() + sessionState.totalDurationSeconds * 1000 + 6000;

            if (soundBundleManager && msg.sounds) {
                sessionState.sounds = msg.sounds;
                soundBundleManager.updateBundle(msg.sounds);
            } else if (msg.sounds) {
                sessionState.sounds = msg.sounds;
            }
            playSound(msg.sounds?.start || { enabled: true }, 'chime', 520);
        } else if (msg.type === 'session_end') {
            sessionState.isActive = false;
            sessionState.endedAtMs = performance.now();
            boardConfig.visibleUntil = performance.now() + 6000;
            if (msg.headerConfig) Object.assign(headerConfig, msg.headerConfig);
            else if (msg.session && msg.session.headerConfig) Object.assign(headerConfig, msg.session.headerConfig);

            playSound(msg.sounds?.end || { enabled: true }, 'chime', 380);
        } else if (msg.type === 'curling_slide') {
            handleCurlingSlide(msg);
        }
    }

    function getTrackMetrics() {
        const thicknessRatio = Math.max(0.10, Math.min(0.25, (boardConfig.trackThicknessPercent || 14.0) / 100));
        const pos = (boardConfig.stripPosition || 'Bottom').toLowerCase();
        let trackX, trackY, trackW, trackH;
        let isHorizontal = true;

        if (pos === 'top') {
            trackX = 0;
            trackY = 0;
            trackW = width;
            trackH = height * thicknessRatio;
            isHorizontal = true;
        } else if (pos === 'left') {
            trackX = 0;
            trackY = 0;
            trackW = width * thicknessRatio;
            trackH = height;
            isHorizontal = false;
        } else if (pos === 'right') {
            trackX = width * (1.0 - thicknessRatio);
            trackY = 0;
            trackW = width * thicknessRatio;
            trackH = height;
            isHorizontal = false;
        } else {
            // Default: Bottom
            trackX = 0;
            trackY = height * (1.0 - thicknessRatio);
            trackW = width;
            trackH = height * thicknessRatio;
            isHorizontal = true;
        }

        const trackLength = isHorizontal ? trackW : trackH;
        const trackBreadth = isHorizontal ? trackH : trackW;
        // Comfortable radius within lane breadth allowing vertical/horizontal staggering
        const stoneRadius = Math.max(14, Math.min(36, trackBreadth * 0.32));

        const isReversed = (typeof boardConfig.isReversed === 'boolean')
            ? boardConfig.isReversed
            : (boardConfig.slideDirection === 'RightToLeft' || boardConfig.slideDirection === 'BottomToTop');

        let targetDistRatio = Math.max(0.50, Math.min(0.95, (boardConfig.targetDistancePercent || 78.0) / 100));
        if (boardConfig.slideDirection === 'CenterTarget') {
            targetDistRatio = 0.50;
        }

        const effectiveTargetRatio = isReversed ? (1.0 - targetDistRatio) : targetDistRatio;
        let targetCenterX, targetCenterY;

        if (isHorizontal) {
            targetCenterX = trackX + trackW * effectiveTargetRatio;
            targetCenterY = trackY + trackH * 0.50;
        } else {
            targetCenterX = trackX + trackW * 0.50;
            targetCenterY = trackY + trackH * effectiveTargetRatio;
        }

        return {
            x: trackX,
            y: trackY,
            w: trackW,
            h: trackH,
            length: trackLength,
            breadth: trackBreadth,
            isHorizontal: isHorizontal,
            stoneRadius: stoneRadius,
            targetCenterX: targetCenterX,
            targetCenterY: targetCenterY,
            targetDistRatio: targetDistRatio,
            effectiveTargetRatio: effectiveTargetRatio,
            isReversed: isReversed,
            pos: pos
        };
    }

    function handleCurlingSlide(msg) {
        if (!msg || !msg.dropToken) return;
        sessionState.totalDrops++;

        // Apply slide settings if present in throw payload
        if (typeof msg.stripPosition === 'string') boardConfig.stripPosition = msg.stripPosition;
        if (typeof msg.slideDirection === 'string') boardConfig.slideDirection = msg.slideDirection;
        if (typeof msg.isReversed === 'boolean') boardConfig.isReversed = msg.isReversed;
        else if (typeof msg.slideDirection === 'string') {
            boardConfig.isReversed = (boardConfig.slideDirection === 'RightToLeft' || boardConfig.slideDirection === 'BottomToTop');
        }
        if (typeof msg.scoringType === 'string') boardConfig.scoringType = msg.scoringType;
        if (typeof msg.collisionMode === 'string') boardConfig.collisionMode = msg.collisionMode;
        if (typeof msg.condition === 'string') boardConfig.condition = msg.condition;
        if (typeof msg.targetDistancePercent === 'number') boardConfig.targetDistancePercent = msg.targetDistancePercent;
        if (typeof msg.trackThicknessPercent === 'number') boardConfig.trackThicknessPercent = msg.trackThicknessPercent;
        if (typeof msg.boardAlpha === 'number') boardConfig.boardAlpha = msg.boardAlpha;
        if (typeof msg.showBoardBorders === 'boolean') boardConfig.showBoardBorders = msg.showBoardBorders;
        if (typeof msg.targetAlpha === 'number') boardConfig.targetAlpha = msg.targetAlpha;
        if (typeof msg.enableShuffleboardKitchenPenalty === 'boolean') boardConfig.enableShuffleboardKitchenPenalty = msg.enableShuffleboardKitchenPenalty;
        if (typeof msg.powerVariancePercent === 'number') boardConfig.powerVariancePercent = msg.powerVariancePercent;
        if (msg.theme) currentTheme = msg.theme;
        if (msg.sounds) {
            sessionState.sounds = msg.sounds;
            if (soundBundleManager) soundBundleManager.updateBundle(msg.sounds);
        }

        boardConfig.visibleUntil = Math.max(boardConfig.visibleUntil, performance.now() + 15000);

        const m = getTrackMetrics();

        // Calculate initial slide velocity along slide axis matching server physics calibration
        const rawPower = typeof msg.power === 'number' ? msg.power : 75;
        const variancePct = (typeof msg.powerVariancePercent === 'number')
            ? msg.powerVariancePercent
            : (typeof boardConfig.powerVariancePercent === 'number' ? boardConfig.powerVariancePercent : 3.0);

        // Server-authoritative launch power (variance already applied server-side); no client resampling.
        const variedPower = (typeof msg.effectivePower === 'number' && isFinite(msg.effectivePower))
            ? msg.effectivePower
            : rawPower;

        // Deterministic per-dropToken PRNG so every overlay instance produces identical dispersion/outcome
        const rand = DropGamesCommon.createSeededRandom(msg.dropToken);

        let dragMult = 1.0;
        if (boardConfig.condition === 'Slick') dragMult = 0.78;
        else if (boardConfig.condition === 'HeavyFrost') dragMult = 1.28;
        else if (boardConfig.condition === 'CrossDrift') dragMult = 1.05;

        // Pebbled ice friction jitter: micro-textures on ice sheet slightly alter deceleration per stone
        const pebbleFrictionJitter = variancePct > 0 ? (1.0 + (rand() * 0.04 - 0.02)) : 1.0;

        const isReversed = (typeof msg.isReversed === 'boolean')
            ? msg.isReversed
            : (typeof boardConfig.isReversed === 'boolean'
                ? boardConfig.isReversed
                : (boardConfig.slideDirection === 'RightToLeft' || boardConfig.slideDirection === 'BottomToTop'));

        // Max cross-axis offset so stone never clips over the edges of the track
        const maxV = Math.max(0.06, 0.46 - (m.stoneRadius / m.breadth));
        // Stagger spawn: if variancePct <= 0, center directly along 0.0. Otherwise triangular distribution clustering near center delivery line.
        const spawnOffsetV = variancePct > 0
            ? ((rand() + rand()) - 1.0) * (maxV * 0.35)
            : 0.0;

        // Authentic in-turn / out-turn curl across the sheet (+ dynamic condition drift)
        // If variance is 0 (guaranteed shot), no curl or drift occurs.
        const curlDirection = variancePct > 0 ? (rand() > 0.5 ? 1 : -1) : 0;
        const naturalCurlSpeed = variancePct > 0 ? curlDirection * (0.0020 + rand() * 0.0035) : 0.0;
        const driftSpeed = (variancePct > 0 && boardConfig.condition === 'CrossDrift') ? (rand() * 0.012 - 0.006) : 0.0;

        const startU = isReversed ? 0.95 : 0.05;
        const baseDecel = 0.22;
        const decel = Math.max(0.01, baseDecel * dragMult * pebbleFrictionJitter);
        const powerFraction = Math.max(0.01, Math.min(1.0, variedPower / 100.0));
        const initialSpeed = powerFraction * 0.76;
        const directionSign = isReversed ? -1.0 : 1.0;

        const newStone = {
            dropToken: msg.dropToken,
            username: msg.username || 'Chatter',
            displayName: msg.displayName || msg.username || 'Chatter',
            userColor: msg.userColor || '#3498db',
            decalUrl: msg.decalUrl || '',
            power: rawPower,
            variedPower: variedPower,
            isReversed: isReversed,
            curlDirection: curlDirection,

            // Physics state along slide axis (u: 0.0 to 1.0) and cross axis (v: -0.5 to +0.5 from center)
            u: startU,
            v: spawnOffsetV,
            speedU: directionSign * initialSpeed,
            speedV: naturalCurlSpeed + driftSpeed,
            drag: decel,

            // Rendering state
            angle: 0.0,
            spinSpeed: variancePct > 0 ? (curlDirection || 1) * (0.02 + rand() * 0.02) : 0.0,
            radius: m.stoneRadius,
            completed: false,
            score: null,
            landedAtMs: 0,
            glintPhase: 0,
            isLeader: false
        };

        if (newStone.decalUrl) getLoadedImage(newStone.decalUrl);
        if (currentTheme && currentTheme.curlingStoneImageUrl) getLoadedImage(currentTheme.curlingStoneImageUrl);
        if (currentTheme && currentTheme.curlingHouseImageUrl) getLoadedImage(currentTheme.curlingHouseImageUrl);

        activeStones.push(newStone);
        ensureHeartbeat();
        playSound(msg.sounds?.curlingSlide || { enabled: true }, 'slide');
    }

    // Leader evaluation (with distance tie-break)
    function updateLeader(m) {
        if (!activeStones || activeStones.length === 0) return;
        const metrics = m || getTrackMetrics();
        const effectiveTarget = metrics.effectiveTargetRatio;
        let maxScore = -999;
        let minTargetDist = 999;
        let leaderStone = null;

        for (let i = 0; i < activeStones.length; i++) {
            const s = activeStones[i];
            s.isLeader = false;
            if (s.completed && typeof s.score === 'number' && s.score > 0) {
                const dist = Math.abs(s.u - effectiveTarget);
                if (s.score > maxScore || (s.score === maxScore && dist < minTargetDist)) {
                    maxScore = s.score;
                    minTargetDist = dist;
                    leaderStone = s;
                }
            }
        }

        if (leaderStone && maxScore > 0) {
            leaderStone.isLeader = true;
        }
    }

    // Scoring calculation when stone comes to rest
    function evaluateStoneOutcome(stone, m) {
        const isReversed = (typeof stone.isReversed === 'boolean' ? stone.isReversed : m.isReversed);
        const effectiveTarget = isReversed
            ? (1.0 - m.targetDistRatio)
            : m.targetDistRatio;
        const dirSign = isReversed ? -1.0 : 1.0;
        const offset = dirSign * (stone.u - effectiveTarget);
        let score = 0;
        let isBullseye = false;
        let isWin = false;
        let isHit = false;

        if (boardConfig.scoringType === 'Shuffleboard') {
            // Shuffleboard: targetDistRatio is the 30pt center apex
            // bandW = 0.050 (5% of runway length) matching server physics
            const bandW = 0.050;

            if (offset >= -0.5 * bandW && offset <= 0.5 * bandW) {
                score = 30;
                isBullseye = true;
                isWin = true;
                isHit = true;
            } else if (offset >= -1.5 * bandW && offset < -0.5 * bandW) {
                score = 20;
                isWin = true;
                isHit = true;
            } else if (offset >= -2.5 * bandW && offset < -1.5 * bandW) {
                score = 10;
                isWin = true;
                isHit = true;
            } else if (offset > 0.5 * bandW && offset <= 1.7 * bandW) {
                if (boardConfig.enableShuffleboardKitchenPenalty !== false) {
                    score = -10; // Kitchen penalty!
                    isHit = true;
                } else {
                    score = 0;
                    isHit = false;
                }
            } else {
                score = 0;
            }
        } else {
            // Curling House Rings
            // Circular pixel distance from house center matching drawn concentric radii
            const pos = getStoneCanvasCoords(stone, m);
            const distFromHouseCenter = Math.hypot(pos.x - m.targetCenterX, pos.y - m.targetCenterY);
            const buttonR = m.stoneRadius * 0.42;
            const ring4ftR = m.stoneRadius * 1.15;
            const ring8ftR = m.stoneRadius * 2.1;
            const ring12ftR = m.stoneRadius * 3.2;

            if (distFromHouseCenter <= buttonR) {
                score = 100;
                isBullseye = true;
                isWin = true;
                isHit = true;
            } else if (distFromHouseCenter <= ring4ftR) {
                score = 75;
                isWin = true;
                isHit = true;
            } else if (distFromHouseCenter <= ring8ftR) {
                score = 50;
                isWin = true;
                isHit = true;
            } else if (distFromHouseCenter <= ring12ftR) {
                score = 25;
                isWin = true;
                isHit = true;
            } else {
                score = 0;
            }
        }

        stone.score = score;
        stone.isBullseye = isBullseye;
        stone.isWin = isWin;
        stone.isHit = isHit;

        // Sound & Score Popup
        const bundle = (soundBundleManager && typeof soundBundleManager.getBundle === 'function')
            ? soundBundleManager.getBundle()
            : (sessionState.sounds || null);
        if (isBullseye) {
            playSound(bundle?.win || sessionState.sounds?.win || { enabled: true }, 'win');
        } else if (score > 0) {
            playSound(bundle?.score || sessionState.sounds?.score || { enabled: true }, 'score');
        }

        // Spawn visual score popup
        const pos = getStoneCanvasCoords(stone, m);
        const popupText = score > 0 ? `+${score}` : `${score}`;
        scorePopups.push({
            x: pos.x,
            y: pos.y,
            text: isBullseye ? `+${score} BULLSEYE!` : popupText,
            color: isBullseye ? '#FFD700' : (score > 0 ? '#2ECC71' : '#E74C3C'),
            alpha: 1.0,
            vy: -1.2,
            createdAt: performance.now(),
            duration: 2500
        });

        // Spawn seasonal landing particles
        const season = getSeasonPreset();
        if (isBullseye) {
            spawnSeasonalBurst(pos.x, pos.y, season, 42, true);
        } else if (score > 0) {
            spawnSeasonalBurst(pos.x, pos.y, season, 20, false);
        } else if (score < 0) {
            // Kitchen penalty smoke/sparks
            spawnSeasonalBurst(pos.x, pos.y, season, 12, false);
        }

        // Send completion to server
        sendCompletion({
            dropToken: stone.dropToken,
            score: score,
            isBullseye: isBullseye,
            isWin: isWin,
            isHit: isHit,
            landingX: stone.u
        });
    }

    function getStoneCanvasCoords(stone, m) {
        let cx, cy;
        if (m.isHorizontal) {
            cx = m.x + stone.u * m.length;
            cy = m.y + m.breadth * (0.50 + stone.v);
        } else {
            cx = m.x + m.breadth * (0.50 + stone.v);
            cy = m.y + stone.u * m.length;
        }
        return { x: cx, y: cy };
    }

    // Elastic stone-on-stone collision with 2D isotropic deflection
    function resolveCollisions(m) {
        const mode = boardConfig.collisionMode || 'LockOnRest';
        if (mode === 'NoCollision') return;

        const count = activeStones.length;

        for (let i = 0; i < count; i++) {
            const a = activeStones[i];
            for (let j = i + 1; j < count; j++) {
                const b = activeStones[j];

                // Anti-Griefing: In LockOnRest mode, once a stone has come to rest (completed),
                // moving stones cleanly ghost past it so late throwers cannot displace the button leader!
                if (mode === 'LockOnRest') {
                    if (a.completed || b.completed) {
                        continue;
                    }
                }

                // Positions in pixel canvas space
                const posA = getStoneCanvasCoords(a, m);
                const posB = getStoneCanvasCoords(b, m);
                const rA = a.radius || m.stoneRadius;
                const rB = b.radius || m.stoneRadius;

                // Convert (speedU, speedV) to pixel/sec (vx, vy)
                let vAx, vAy, vBx, vBy;
                if (m.isHorizontal) {
                    vAx = a.speedU * m.length;
                    vAy = a.speedV * m.breadth;
                    vBx = b.speedU * m.length;
                    vBy = b.speedV * m.breadth;
                } else {
                    vAx = a.speedV * m.breadth;
                    vAy = a.speedU * m.length;
                    vBx = b.speedV * m.breadth;
                    vBy = b.speedU * m.length;
                }

                const velA = { vx: vAx, vy: vAy };
                const velB = { vx: vBx, vy: vBy };

                const result = (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.resolveCircleCollision)
                    ? DropGamesCommon.resolveCircleCollision(posA, posB, velA, velB, rA, rB, { restitution: 0.82 })
                    : null;

                if (result && result.collided) {
                    // Update normalized stone coordinates if separated
                    if (result.overlap > 0 && result.normal) {
                        const nx = result.normal.nx;
                        const ny = result.normal.ny;
                        const sep = result.overlap * 0.5;
                        if (m.isHorizontal) {
                            a.u -= (nx * sep) / m.length;
                            a.v -= (ny * sep) / m.breadth;
                            b.u += (nx * sep) / m.length;
                            b.v += (ny * sep) / m.breadth;
                        } else {
                            a.v -= (nx * sep) / m.breadth;
                            a.u -= (ny * sep) / m.length;
                            b.v += (nx * sep) / m.breadth;
                            b.u += (ny * sep) / m.length;
                        }
                    }

                    if (!result.separating) {
                        // Convert back to (speedU, speedV)
                        if (m.isHorizontal) {
                            a.speedU = velA.vx / m.length;
                            a.speedV = velA.vy / m.breadth;
                            b.speedU = velB.vx / m.length;
                            b.speedV = velB.vy / m.breadth;
                        } else {
                            a.speedV = velA.vx / m.breadth;
                            a.speedU = velA.vy / m.length;
                            b.speedV = velB.vx / m.breadth;
                            b.speedU = velB.vy / m.length;
                        }

                        // Sound effect (rate limited)
                        const now = performance.now();
                        if (now - lastCollisionSoundMs > 120) {
                            lastCollisionSoundMs = now;
                            const bundle = (soundBundleManager && typeof soundBundleManager.getBundle === 'function')
                                ? soundBundleManager.getBundle()
                                : (sessionState.sounds || null);
                            playSound(bundle?.hit || { enabled: true }, 'hit');
                        }
                    }
                }
            }
        }
    }

    function drawSessionBanner(now) {
        const hasActiveStones = activeStones.some(s => !s.completed || (now - (s.landedAtMs || now)) < 6000);
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.drawSessionBanner) {
            const res = DropGamesCommon.drawSessionBanner(ctx, {
                now,
                width,
                headerConfig,
                sessionState,
                currentBannerY,
                currentGame: 'curling',
                activeOtherGames,
                isOtherGameActive,
                otherDropsActiveUntilMs,
                hasActiveDrops: hasActiveStones,
                itemPluralName: 'Stones',
                primaryColor: currentTheme?.primaryColor || '#60A5FA',
                accentColor: currentTheme?.accentColor || '#FFE066'
            });
            if (res && typeof res.currentBannerY === 'number') {
                currentBannerY = res.currentBannerY;
            }
            if (!sessionState.isActive && !hasActiveStones && activeStones.length === 0) {
                sessionState.totalDrops = 0;
            }
            return;
        }

        // Fallback banner drawing
        let targetY = typeof headerConfig.yOffset === 'number' ? headerConfig.yOffset : 18;
        if (headerConfig.autoAvoidOverlap !== false) {
            let slot = (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.calculateBannerStackSlot)
                ? DropGamesCommon.calculateBannerStackSlot('curling', activeOtherGames)
                : (isOtherGameActive ? 1 : 0);
            if (now < otherDropsActiveUntilMs && slot === 0) slot = 1;
            targetY += slot * 54;
        }
        currentBannerY += (targetY - currentBannerY) * 0.15;
    }

    // Main 60 FPS Physics & Render Loop
    let lastTime = performance.now();

    function render(now) {
        const dt = Math.min(0.05, (now - lastTime) / 1000);
        lastTime = now;

        ctx.clearRect(0, 0, width, height);

        const m = getTrackMetrics();

        // Update physics
        for (let i = 0; i < activeStones.length; i++) {
            const stone = activeStones[i];
            if (!stone.completed) {
                // Apply friction / drag
                stone.u += stone.speedU * dt;
                stone.v += stone.speedV * dt;
                stone.angle += stone.spinSpeed * (Math.abs(stone.speedU) > 0 ? 1 : 0);

                // Damping along slide axis
                if (stone.speedU > 0) {
                    stone.speedU = Math.max(0, stone.speedU - stone.drag * dt);
                } else if (stone.speedU < 0) {
                    stone.speedU = Math.min(0, stone.speedU + stone.drag * dt);
                }

                // Lateral damping & authentic late-curl hook as stone slows down on pebbled ice
                stone.speedV *= Math.pow(0.96, dt * 60);
                if (stone.curlDirection && Math.abs(stone.speedU) < 0.22 && Math.abs(stone.speedU) > 0.005) {
                    stone.speedV += stone.curlDirection * 0.0006 * dt;
                }

                // Rail bounce damping (staggered pucks bounce off rails)
                const railLimit = Math.max(0.06, 0.48 - (stone.radius / m.breadth));
                if (stone.v < -railLimit) {
                    stone.v = -railLimit;
                    if (stone.speedV < 0) {
                        stone.speedV = -stone.speedV * 0.65;
                        if (now - lastCollisionSoundMs > 150) {
                            lastCollisionSoundMs = now;
                            playSound({ enabled: true }, 'hit');
                        }
                    }
                } else if (stone.v > railLimit) {
                    stone.v = railLimit;
                    if (stone.speedV > 0) {
                        stone.speedV = -stone.speedV * 0.65;
                        if (now - lastCollisionSoundMs > 150) {
                            lastCollisionSoundMs = now;
                            playSound({ enabled: true }, 'hit');
                        }
                    }
                }

                // Check stop threshold
                const isStopped = Math.abs(stone.speedU) < 0.003;
                const isOutOfBounds = stone.isReversed ? (stone.u <= 0.02) : (stone.u >= 0.98);

                if (isStopped || isOutOfBounds) {
                    stone.speedU = 0;
                    stone.speedV = 0;
                    stone.completed = true;
                    stone.landedAtMs = now;
                    evaluateStoneOutcome(stone, m);
                    updateLeader(m);
                }
            } else {
                stone.glintPhase += dt * 3.0;
            }
        }

        // Collisions
        resolveCollisions(m);

        // Clean up expired stones
        for (let i = activeStones.length - 1; i >= 0; i--) {
            const stone = activeStones[i];
            const alphaData = (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.calculateDropAlpha)
                ? DropGamesCommon.calculateDropAlpha(stone.landedAtMs, now, sessionState, stone.isLeader, {
                    dimDelay: 3500,
                    dimDuration: 2000,
                    minAlpha: 0.40,
                    lingerTime: 9000,
                    fadeTime: 2500
                })
                : { alpha: 1.0, shouldRemove: false };

            if (alphaData.shouldRemove) {
                activeStones.splice(i, 1);
            } else {
                stone.renderAlpha = alphaData.alpha;
            }
        }

        // Maintain leader status dynamically every frame
        updateLeader(m);

        // Determine board visibility: visible during active session, 6s post-round, while stones are active/fading, or during free-play visible window
        const isEndedWithinWindow = sessionState.endedAtMs > 0 && (now - sessionState.endedAtMs) < 6000;
        if (sessionState.endedAtMs > 0 && (now - sessionState.endedAtMs) >= 6000) {
            sessionState.endedAtMs = 0;
            sessionState.totalDrops = 0;
        }

        const hasMovingStones = activeStones.some(s => !s.completed);
        const hasRecentStones = activeStones.some(s => (now - (s.landedAtMs || now)) < 6000);
        const isBoardVisible = sessionState.isActive ||
                               isEndedWithinWindow ||
                               hasMovingStones ||
                               hasRecentStones ||
                               (now < boardConfig.visibleUntil);

        if (isBoardVisible) {
            // Ambient seasonal background particles
            syncAmbientParticles(isBoardVisible);
            updateAndDrawAmbientParticles(dt, isBoardVisible);

            drawBoard(m);
            drawTargetHouse(m);
            drawConditionPill(m);

            // Sort stones for rendering (completed behind moving, winning leader at highest index)
            if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.sortDropsForDraw) {
                DropGamesCommon.sortDropsForDraw(activeStones);
            }

            // Render Stones in forward order (non-leaders first, winning leader LAST so it's always drawn on top!)
            for (let i = 0; i < activeStones.length; i++) {
                const stone = activeStones[i];
                drawStone(stone, m, stone.renderAlpha ?? 1.0);
            }

            // Landing & scoring burst particles
            updateAndDrawParticles(dt);
        } else {
            // Clear ambient and burst particles when board is hidden so nothing remains on screen
            if (ambientParticles.length > 0) ambientParticles.length = 0;
            if (particles.length > 0) particles.length = 0;
        }

        // Popups
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.updateAndDrawPopups) {
            DropGamesCommon.updateAndDrawPopups(ctx, scorePopups, { now, dt, width, height });
        }

        // Top Session Banner & Timer
        drawSessionBanner(now);

        requestAnimationFrame(render);
    }

    // Draw Seasonal Corner Flourish
    function drawSeasonalFlourish(season, x, y, size, angle) {
        ctx.save();
        ctx.translate(x, y);
        ctx.rotate(angle);

        if (season === 'spring') {
            // Cherry blossom flower (5 petals + pollen dot)
            ctx.fillStyle = '#FF8FA3';
            for (let p = 0; p < 5; p++) {
                const ang = p * (Math.PI * 2 / 5);
                ctx.beginPath();
                ctx.arc(Math.cos(ang) * size * 0.52, Math.sin(ang) * size * 0.52, size * 0.40, 0, Math.PI * 2);
                ctx.fill();
            }
            ctx.fillStyle = '#FFE066';
            ctx.beginPath();
            ctx.arc(0, 0, size * 0.22, 0, Math.PI * 2);
            ctx.fill();
        } else if (season === 'summer') {
            // Golden Sunburst corner
            ctx.fillStyle = '#FFD166';
            ctx.beginPath();
            ctx.arc(0, 0, size * 0.45, 0, Math.PI * 2);
            ctx.fill();
            ctx.strokeStyle = 'rgba(255, 209, 102, 0.7)';
            ctx.lineWidth = 1.8;
            for (let a = 0; a < Math.PI * 2; a += Math.PI / 4) {
                ctx.beginPath();
                ctx.moveTo(Math.cos(a) * size * 0.55, Math.sin(a) * size * 0.55);
                ctx.lineTo(Math.cos(a) * size * 0.95, Math.sin(a) * size * 0.95);
                ctx.stroke();
            }
        } else if (season === 'fall') {
            // Clustered Autumn Maple Leaves
            const drawLeaf = (lx, ly, col, rot, sc) => {
                ctx.save();
                ctx.translate(lx, ly);
                ctx.rotate(rot);
                ctx.fillStyle = col;
                ctx.beginPath();
                ctx.ellipse(0, 0, size * 0.60 * sc, size * 0.32 * sc, 0, 0, Math.PI * 2);
                ctx.fill();
                ctx.restore();
            };
            drawLeaf(-size * 0.18, -size * 0.18, '#D90429', -0.4, 0.9);
            drawLeaf(size * 0.18, -size * 0.1, '#E76F51', 0.5, 0.85);
            drawLeaf(0, size * 0.18, '#E9C46A', 0.1, 0.75);
        } else if (season === 'winter') {
            // Frosted Hanging Icicles & Snowflake
            ctx.fillStyle = 'rgba(202, 240, 248, 0.85)';
            ctx.beginPath();
            ctx.moveTo(-size * 0.6, 0);
            ctx.lineTo(-size * 0.3, size * 0.9);
            ctx.lineTo(0, 0);
            ctx.lineTo(size * 0.3, size * 0.7);
            ctx.lineTo(size * 0.6, 0);
            ctx.closePath();
            ctx.fill();
            // Center mini snowflake
            ctx.strokeStyle = '#FFFFFF';
            ctx.lineWidth = 1.5;
            for (let a = 0; a < Math.PI; a += Math.PI / 3) {
                ctx.beginPath();
                ctx.moveTo(Math.cos(a) * size * 0.45, Math.sin(a) * size * 0.45);
                ctx.lineTo(-Math.cos(a) * size * 0.45, -Math.sin(a) * size * 0.45);
                ctx.stroke();
            }
        } else if (season === 'halloween') {
            // Cobweb & Glowing Spooky Eyes
            ctx.strokeStyle = 'rgba(180, 160, 220, 0.55)';
            ctx.lineWidth = 1.2;
            for (let a = 0; a <= Math.PI / 2; a += Math.PI / 8) {
                ctx.beginPath();
                ctx.moveTo(0, 0);
                ctx.lineTo(Math.cos(a) * size * 1.1, Math.sin(a) * size * 1.1);
                ctx.stroke();
            }
            for (let r = size * 0.35; r <= size * 1.0; r += size * 0.35) {
                ctx.beginPath();
                ctx.arc(0, 0, r, 0, Math.PI / 2);
                ctx.stroke();
            }
            // Glowing pair of spooky eyes
            ctx.fillStyle = '#39FF14';
            ctx.beginPath();
            ctx.ellipse(size * 0.35, size * 0.32, size * 0.15, size * 0.08, 0.3, 0, Math.PI * 2);
            ctx.ellipse(size * 0.60, size * 0.32, size * 0.15, size * 0.08, -0.3, 0, Math.PI * 2);
            ctx.fill();
        } else if (season === 'christmas') {
            // Holly Leaves & Berry Cluster
            ctx.fillStyle = '#2B9348';
            ctx.beginPath();
            ctx.ellipse(-size * 0.25, -size * 0.1, size * 0.55, size * 0.25, -0.4, 0, Math.PI * 2);
            ctx.ellipse(size * 0.25, -size * 0.1, size * 0.55, size * 0.25, 0.4, 0, Math.PI * 2);
            ctx.fill();
            // Bright red holly berries
            ctx.fillStyle = '#D90429';
            ctx.beginPath();
            ctx.arc(-size * 0.12, size * 0.12, size * 0.2, 0, Math.PI * 2);
            ctx.arc(size * 0.12, size * 0.12, size * 0.2, 0, Math.PI * 2);
            ctx.arc(0, -size * 0.05, size * 0.2, 0, Math.PI * 2);
            ctx.fill();
        }
        ctx.restore();
    }

    // Draw Track / Board Sheet
    function drawBoard(m) {
        ctx.save();
        const season = getSeasonPreset();

        // 1. Board Sheet Background (if boardAlpha > 0)
        if (boardConfig.boardAlpha > 0.0) {
            let baseFill = `rgba(22, 33, 44, ${boardConfig.boardAlpha})`;
            let gradStart = 'rgba(255, 255, 255, 0.03)';
            let gradMid = 'rgba(173, 216, 230, 0.06)';
            let gradEnd = 'rgba(255, 255, 255, 0.03)';

            if (season === 'spring') {
                baseFill = `rgba(32, 24, 34, ${boardConfig.boardAlpha})`;
                gradStart = 'rgba(255, 240, 245, 0.06)';
                gradMid = 'rgba(255, 182, 193, 0.12)';
                gradEnd = 'rgba(255, 240, 245, 0.06)';
            } else if (season === 'summer') {
                baseFill = `rgba(16, 36, 48, ${boardConfig.boardAlpha})`;
                gradStart = 'rgba(224, 247, 250, 0.06)';
                gradMid = 'rgba(0, 180, 216, 0.12)';
                gradEnd = 'rgba(254, 243, 199, 0.06)';
            } else if (season === 'fall') {
                baseFill = `rgba(38, 26, 20, ${boardConfig.boardAlpha})`;
                gradStart = 'rgba(255, 243, 224, 0.06)';
                gradMid = 'rgba(231, 111, 81, 0.12)';
                gradEnd = 'rgba(233, 196, 106, 0.06)';
            } else if (season === 'winter') {
                baseFill = `rgba(14, 28, 48, ${boardConfig.boardAlpha})`;
                gradStart = 'rgba(224, 242, 254, 0.08)';
                gradMid = 'rgba(56, 189, 248, 0.15)';
                gradEnd = 'rgba(224, 242, 254, 0.08)';
            } else if (season === 'halloween') {
                baseFill = `rgba(28, 12, 42, ${boardConfig.boardAlpha})`;
                gradStart = 'rgba(60, 9, 108, 0.25)';
                gradMid = 'rgba(36, 0, 70, 0.35)';
                gradEnd = 'rgba(60, 9, 108, 0.25)';
            } else if (season === 'christmas') {
                baseFill = `rgba(18, 38, 28, ${boardConfig.boardAlpha})`;
                gradStart = 'rgba(27, 67, 50, 0.16)';
                gradMid = 'rgba(217, 4, 41, 0.12)';
                gradEnd = 'rgba(27, 67, 50, 0.16)';
            }

            ctx.fillStyle = baseFill;
            ctx.fillRect(m.x, m.y, m.w, m.h);

            // Sheet ice texture subtle linear gradient
            const grad = m.isHorizontal
                ? ctx.createLinearGradient(m.x, m.y, m.x + m.w, m.y)
                : ctx.createLinearGradient(m.x, m.y, m.x, m.y + m.h);
            grad.addColorStop(0.0, gradStart);
            grad.addColorStop(0.5, gradMid);
            grad.addColorStop(1.0, gradEnd);
            ctx.fillStyle = grad;
            ctx.fillRect(m.x, m.y, m.w, m.h);
        }

        // 2. Guide Rails / Boundary Lines
        if (boardConfig.showBoardBorders) {
            let railStroke = 'rgba(255, 255, 255, 0.22)';
            let centerStroke = 'rgba(255, 255, 255, 0.10)';

            if (season === 'spring') {
                railStroke = 'rgba(255, 182, 193, 0.45)';
                centerStroke = 'rgba(255, 182, 193, 0.20)';
            } else if (season === 'summer') {
                railStroke = 'rgba(255, 209, 102, 0.45)';
                centerStroke = 'rgba(0, 180, 216, 0.22)';
            } else if (season === 'fall') {
                railStroke = 'rgba(230, 126, 34, 0.50)';
                centerStroke = 'rgba(231, 111, 81, 0.22)';
            } else if (season === 'winter') {
                railStroke = 'rgba(186, 230, 253, 0.55)';
                centerStroke = 'rgba(202, 240, 248, 0.25)';
            } else if (season === 'halloween') {
                railStroke = 'rgba(57, 255, 20, 0.45)';
                centerStroke = 'rgba(123, 44, 191, 0.35)';
            } else if (season === 'christmas') {
                railStroke = 'rgba(217, 4, 41, 0.50)';
                centerStroke = 'rgba(43, 147, 72, 0.30)';
            }

            ctx.strokeStyle = railStroke;
            ctx.lineWidth = 1.8;
            ctx.strokeRect(m.x, m.y, m.w, m.h);

            // Center guide line
            ctx.setLineDash([4, 6]);
            ctx.strokeStyle = centerStroke;
            ctx.beginPath();
            if (m.isHorizontal) {
                ctx.moveTo(m.x, m.y + m.h * 0.5);
                ctx.lineTo(m.x + m.w, m.y + m.h * 0.5);
            } else {
                ctx.moveTo(m.x + m.w * 0.5, m.y);
                ctx.lineTo(m.x + m.w * 0.5, m.y + m.h);
            }
            ctx.stroke();
            ctx.setLineDash([]);
        }

        // 3. Seasonal Corner Flourishes
        const flourishSize = Math.max(10, Math.min(22, m.breadth * 0.16));
        const corners = [
            { x: m.x + flourishSize, y: m.y + flourishSize, angle: 0 },
            { x: m.x + m.w - flourishSize, y: m.y + flourishSize, angle: Math.PI / 2 },
            { x: m.x + m.w - flourishSize, y: m.y + m.h - flourishSize, angle: Math.PI },
            { x: m.x + flourishSize, y: m.y + m.h - flourishSize, angle: -Math.PI / 2 }
        ];
        corners.forEach(c => {
            drawSeasonalFlourish(season, c.x, c.y, flourishSize, c.angle);
        });

        ctx.restore();
    }

    // Draw Target House / Shuffleboard Zone
    function drawTargetHouse(m) {
        ctx.save();
        const alpha = Math.max(0.1, Math.min(1.0, boardConfig.targetAlpha ?? 0.85));
        const cx = m.targetCenterX;
        const cy = m.targetCenterY;
        const r = m.stoneRadius;
        const season = getSeasonPreset();

        // Check for custom curling house image override
        if (currentTheme && currentTheme.curlingHouseImageUrl) {
            const houseImg = getLoadedImage(currentTheme.curlingHouseImageUrl);
            if (houseImg) {
                const imgSize = r * 6.4;
                ctx.drawImage(houseImg, cx - imgSize / 2, cy - imgSize / 2, imgSize, imgSize);
                ctx.restore();
                return;
            }
        }

        if (boardConfig.scoringType === 'Shuffleboard') {
            // Shuffleboard linear scoring bands with seasonal palettes
            const isHoriz = m.isHorizontal;
            const bandW = m.length * 0.050;
            const dirSign = m.isReversed ? -1 : 1;

            let z10Col, z20Col, z30Col, zKitchenCol;
            if (season === 'spring') {
                z10Col = `rgba(147, 197, 253, ${alpha * 0.55})`;
                z20Col = `rgba(167, 243, 208, ${alpha * 0.65})`;
                z30Col = `rgba(255, 110, 145, ${alpha * 0.75})`;
                zKitchenCol = `rgba(225, 29, 72, ${alpha * 0.55})`;
            } else if (season === 'summer') {
                z10Col = `rgba(56, 189, 248, ${alpha * 0.55})`;
                z20Col = `rgba(253, 224, 71, ${alpha * 0.65})`;
                z30Col = `rgba(249, 115, 22, ${alpha * 0.75})`;
                zKitchenCol = `rgba(190, 18, 60, ${alpha * 0.55})`;
            } else if (season === 'fall') {
                z10Col = `rgba(217, 119, 6, ${alpha * 0.55})`;
                z20Col = `rgba(234, 88, 12, ${alpha * 0.65})`;
                z30Col = `rgba(185, 28, 28, ${alpha * 0.75})`;
                zKitchenCol = `rgba(120, 53, 15, ${alpha * 0.55})`;
            } else if (season === 'winter') {
                z10Col = `rgba(125, 211, 252, ${alpha * 0.55})`;
                z20Col = `rgba(14, 165, 233, ${alpha * 0.65})`;
                z30Col = `rgba(2, 132, 199, ${alpha * 0.75})`;
                zKitchenCol = `rgba(15, 23, 42, ${alpha * 0.65})`;
            } else if (season === 'halloween') {
                z10Col = `rgba(123, 44, 191, ${alpha * 0.60})`;
                z20Col = `rgba(249, 115, 22, ${alpha * 0.70})`;
                z30Col = `rgba(57, 255, 20, ${alpha * 0.75})`;
                zKitchenCol = `rgba(220, 38, 38, ${alpha * 0.60})`;
            } else if (season === 'christmas') {
                z10Col = `rgba(22, 101, 52, ${alpha * 0.60})`;
                z20Col = `rgba(250, 204, 21, ${alpha * 0.70})`;
                z30Col = `rgba(220, 38, 38, ${alpha * 0.75})`;
                zKitchenCol = `rgba(51, 65, 85, ${alpha * 0.65})`;
            } else {
                z10Col = `rgba(52, 152, 219, ${alpha * 0.5})`;
                z20Col = `rgba(46, 204, 113, ${alpha * 0.6})`;
                z30Col = `rgba(241, 196, 15, ${alpha * 0.7})`;
                zKitchenCol = `rgba(231, 76, 60, ${alpha * 0.5})`;
            }

            const zones = [
                { distFromCenter: -2.0 * bandW, w: bandW, label: '10', score: 10, col: z10Col },
                { distFromCenter: -1.0 * bandW, w: bandW, label: '20', score: 20, col: z20Col },
                { distFromCenter: 0.0, w: bandW, label: '30', score: 30, col: z30Col }
            ];
            if (boardConfig.enableShuffleboardKitchenPenalty !== false) {
                zones.push({ distFromCenter: 1.1 * bandW, w: bandW * 1.2, label: '10 OFF', score: -10, col: zKitchenCol });
            }

            zones.forEach(z => {
                ctx.fillStyle = z.col;
                ctx.strokeStyle = `rgba(255, 255, 255, ${alpha * 0.65})`;
                ctx.lineWidth = 1.5;

                if (isHoriz) {
                    const zCenterX = cx + dirSign * z.distFromCenter;
                    const zx = zCenterX - z.w * 0.5;
                    ctx.fillRect(zx, m.y + 4, z.w, m.h - 8);
                    ctx.strokeRect(zx, m.y + 4, z.w, m.h - 8);

                    ctx.fillStyle = '#ffffff';
                    ctx.font = `bold ${Math.round(r * 0.65)}px 'Rubik', sans-serif`;
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(z.label, zCenterX, cy);
                } else {
                    const zCenterY = cy + dirSign * z.distFromCenter;
                    const zy = zCenterY - z.w * 0.5;
                    ctx.fillRect(m.x + 4, zy, m.w - 8, z.w);
                    ctx.strokeRect(m.x + 4, zy, m.w - 8, z.w);

                    ctx.fillStyle = '#ffffff';
                    ctx.font = `bold ${Math.round(r * 0.65)}px 'Rubik', sans-serif`;
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(z.label, cx, zCenterY);
                }
            });
        } else {
            // Curling House Concentric Rings with seasonal palettes
            let ring12Col, ring8Col, ring4Col, btnCol;
            if (season === 'spring') {
                ring12Col = `rgba(147, 197, 253, ${alpha * 0.65})`;
                ring8Col = `rgba(255, 240, 245, ${alpha * 0.85})`;
                ring4Col = `rgba(255, 110, 145, ${alpha * 0.85})`;
                btnCol = '#FFFFFF';
            } else if (season === 'summer') {
                ring12Col = `rgba(0, 150, 214, ${alpha * 0.65})`;
                ring8Col = `rgba(254, 243, 199, ${alpha * 0.85})`;
                ring4Col = `rgba(249, 115, 22, ${alpha * 0.85})`;
                btnCol = '#FFD166';
            } else if (season === 'fall') {
                ring12Col = `rgba(88, 129, 87, ${alpha * 0.65})`;
                ring8Col = `rgba(254, 240, 215, ${alpha * 0.85})`;
                ring4Col = `rgba(185, 28, 28, ${alpha * 0.85})`;
                btnCol = '#E9C46A';
            } else if (season === 'winter') {
                ring12Col = `rgba(15, 23, 42, ${alpha * 0.70})`;
                ring8Col = `rgba(224, 242, 254, ${alpha * 0.85})`;
                ring4Col = `rgba(2, 132, 199, ${alpha * 0.85})`;
                btnCol = '#FFFFFF';
            } else if (season === 'halloween') {
                ring12Col = `rgba(60, 9, 108, ${alpha * 0.75})`;
                ring8Col = `rgba(24, 24, 36, ${alpha * 0.85})`;
                ring4Col = `rgba(249, 115, 22, ${alpha * 0.90})`;
                btnCol = '#39FF14';
            } else if (season === 'christmas') {
                ring12Col = `rgba(20, 83, 45, ${alpha * 0.75})`;
                ring8Col = `rgba(255, 253, 245, ${alpha * 0.85})`;
                ring4Col = `rgba(220, 38, 38, ${alpha * 0.85})`;
                btnCol = '#FFD700';
            } else {
                ring12Col = `rgba(41, 128, 185, ${alpha * 0.55})`;
                ring8Col = `rgba(240, 243, 244, ${alpha * 0.75})`;
                ring4Col = `rgba(192, 57, 43, ${alpha * 0.75})`;
                btnCol = '#FFFFFF';
            }

            // 12-ft Outer Ring
            ctx.beginPath();
            ctx.arc(cx, cy, r * 3.2, 0, Math.PI * 2);
            ctx.fillStyle = ring12Col;
            ctx.fill();
            ctx.strokeStyle = `rgba(255, 255, 255, ${alpha * 0.8})`;
            ctx.lineWidth = 1.5;
            ctx.stroke();

            // 8-ft Middle Ring
            ctx.beginPath();
            ctx.arc(cx, cy, r * 2.1, 0, Math.PI * 2);
            ctx.fillStyle = ring8Col;
            ctx.fill();
            ctx.strokeStyle = `rgba(255, 255, 255, ${alpha * 0.6})`;
            ctx.lineWidth = 1.2;
            ctx.stroke();

            // 4-ft Ring
            ctx.beginPath();
            ctx.arc(cx, cy, r * 1.15, 0, Math.PI * 2);
            ctx.fillStyle = ring4Col;
            ctx.fill();
            ctx.strokeStyle = `rgba(255, 255, 255, ${alpha * 0.7})`;
            ctx.lineWidth = 1.2;
            ctx.stroke();

            // Center Button Disc
            ctx.beginPath();
            ctx.arc(cx, cy, r * 0.42, 0, Math.PI * 2);
            ctx.fillStyle = btnCol;
            ctx.fill();
            ctx.strokeStyle = `rgba(255, 255, 255, ${alpha * 0.95})`;
            ctx.lineWidth = 1.2;
            ctx.stroke();

            // Procedural Center Button Seasonal Pin
            ctx.save();
            ctx.translate(cx, cy);
            if (season === 'spring') {
                // 5-petal Sakura blossom
                ctx.fillStyle = '#FF8FA3';
                for (let p = 0; p < 5; p++) {
                    const ang = p * (Math.PI * 2 / 5);
                    ctx.beginPath();
                    ctx.arc(Math.cos(ang) * r * 0.16, Math.sin(ang) * r * 0.16, r * 0.10, 0, Math.PI * 2);
                    ctx.fill();
                }
                ctx.fillStyle = '#FFE066';
                ctx.beginPath();
                ctx.arc(0, 0, r * 0.06, 0, Math.PI * 2);
                ctx.fill();
            } else if (season === 'summer') {
                // Radiant golden sun with rays
                ctx.fillStyle = '#E76F51';
                ctx.beginPath();
                ctx.arc(0, 0, r * 0.16, 0, Math.PI * 2);
                ctx.fill();
                ctx.strokeStyle = '#D97706';
                ctx.lineWidth = 1.5;
                for (let a = 0; a < Math.PI * 2; a += Math.PI / 4) {
                    ctx.beginPath();
                    ctx.moveTo(Math.cos(a) * r * 0.18, Math.sin(a) * r * 0.18);
                    ctx.lineTo(Math.cos(a) * r * 0.32, Math.sin(a) * r * 0.32);
                    ctx.stroke();
                }
            } else if (season === 'fall') {
                // Crimson Autumn Maple Leaf
                ctx.fillStyle = '#D90429';
                ctx.beginPath();
                ctx.moveTo(0, -r * 0.28);
                ctx.quadraticCurveTo(r * 0.22, 0, 0, r * 0.28);
                ctx.quadraticCurveTo(-r * 0.22, 0, 0, -r * 0.28);
                ctx.fill();
                ctx.fillStyle = '#9B2226';
                ctx.beginPath();
                ctx.arc(0, 0, r * 0.08, 0, Math.PI * 2);
                ctx.fill();
            } else if (season === 'winter') {
                // 6-pointed crystalline snowflake
                ctx.strokeStyle = '#0284C7';
                ctx.lineWidth = 1.6;
                for (let a = 0; a < Math.PI; a += Math.PI / 3) {
                    ctx.beginPath();
                    ctx.moveTo(Math.cos(a) * r * 0.26, Math.sin(a) * r * 0.26);
                    ctx.lineTo(-Math.cos(a) * r * 0.26, -Math.sin(a) * r * 0.26);
                    ctx.stroke();
                }
                ctx.fillStyle = '#0369A1';
                ctx.beginPath();
                ctx.arc(0, 0, r * 0.07, 0, Math.PI * 2);
                ctx.fill();
            } else if (season === 'halloween') {
                // Spooky black cat eyes inside radioactive green button
                ctx.fillStyle = '#18002E';
                ctx.beginPath();
                ctx.ellipse(-r * 0.12, 0, r * 0.09, r * 0.05, 0.3, 0, Math.PI * 2);
                ctx.ellipse(r * 0.12, 0, r * 0.09, r * 0.05, -0.3, 0, Math.PI * 2);
                ctx.fill();
            } else if (season === 'christmas') {
                // 5-pointed golden star
                ctx.fillStyle = '#B91C1C';
                ctx.beginPath();
                for (let i = 0; i < 5; i++) {
                    const ang = (i * Math.PI * 2 / 5) - Math.PI / 2;
                    const innerAng = ang + Math.PI / 5;
                    const ox = Math.cos(ang) * r * 0.24;
                    const oy = Math.sin(ang) * r * 0.24;
                    const ix = Math.cos(innerAng) * r * 0.10;
                    const iy = Math.sin(innerAng) * r * 0.10;
                    if (i === 0) ctx.moveTo(ox, oy);
                    else ctx.lineTo(ox, oy);
                    ctx.lineTo(ix, iy);
                }
                ctx.closePath();
                ctx.fill();
            } else {
                ctx.beginPath();
                ctx.arc(0, 0, r * 0.15, 0, Math.PI * 2);
                ctx.fillStyle = '#c0392b';
                ctx.fill();
            }
            ctx.restore();

            // Crosshairs / Tee Line & Center Line
            ctx.strokeStyle = `rgba(255, 255, 255, ${alpha * 0.45})`;
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(cx - r * 3.4, cy);
            ctx.lineTo(cx + r * 3.4, cy);
            ctx.moveTo(cx, cy - r * 3.4);
            ctx.lineTo(cx, cy + r * 3.4);
            ctx.stroke();
        }

        ctx.restore();
    }

    // Dynamic Conditions HUD Pill
    function drawConditionPill(m) {
        if (!boardConfig.condition || boardConfig.condition === 'Normal') return;

        ctx.save();
        const label = boardConfig.conditionLabel || boardConfig.condition;
        let icon = '❄️';
        if (boardConfig.condition === 'HeavyFrost') icon = '🧊';
        if (boardConfig.condition === 'CrossDrift') icon = '💨';

        const text = `${icon} ${label}`;
        ctx.font = 'bold 13px "Rubik", sans-serif';
        const textMetrics = ctx.measureText(text);
        const pillW = textMetrics.width + 24;
        const pillH = 24;

        let pillX, pillY;
        if (m.pos === 'top') {
            pillX = m.x + (m.w - pillW) * 0.5;
            pillY = m.y + m.h + 8;
        } else if (m.pos === 'left') {
            pillX = m.x + m.w + 8;
            pillY = m.y + (m.h - pillH) * 0.5;
        } else if (m.pos === 'right') {
            pillX = m.x - pillW - 8;
            pillY = m.y + (m.h - pillH) * 0.5;
        } else {
            // Bottom
            pillX = m.x + (m.w - pillW) * 0.5;
            pillY = m.y - pillH - 8;
        }

        // Draw pill background
        ctx.fillStyle = 'rgba(20, 30, 45, 0.85)';
        ctx.strokeStyle = 'rgba(100, 200, 255, 0.5)';
        ctx.lineWidth = 1;
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.drawRoundRect) {
            DropGamesCommon.drawRoundRect(ctx, pillX, pillY, pillW, pillH, 12, true, true);
        } else {
            ctx.fillRect(pillX, pillY, pillW, pillH);
            ctx.strokeRect(pillX, pillY, pillW, pillH);
        }

        // Text
        ctx.fillStyle = '#E0F7FA';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(text, pillX + pillW * 0.5, pillY + pillH * 0.5 + 1);
        ctx.restore();
    }

    // Draw realistic curling stone with avatar/emote decal & inward nametag
    function drawStone(stone, m, alpha) {
        ctx.save();
        ctx.globalAlpha = Math.max(0.0, Math.min(1.0, alpha));

        const coords = getStoneCanvasCoords(stone, m);
        const cx = coords.x;
        const cy = coords.y;
        const r = stone.radius || m.stoneRadius;

        ctx.translate(cx, cy);

        // 0. Golden Leader Halo & Glow
        if (stone.isLeader) {
            ctx.save();
            ctx.beginPath();
            ctx.arc(0, 0, r + 5, 0, Math.PI * 2);
            ctx.strokeStyle = '#FFD700';
            ctx.lineWidth = 3.5;
            ctx.shadowColor = '#FFD700';
            ctx.shadowBlur = 14;
            ctx.stroke();
            ctx.restore();
        }

        // 1. Subtle drop shadow on the sheet
        ctx.beginPath();
        ctx.arc(3, 4, r * 1.02, 0, Math.PI * 2);
        ctx.fillStyle = 'rgba(0, 0, 0, 0.45)';
        ctx.filter = 'blur(3px)';
        ctx.fill();
        ctx.filter = 'none';

        // Check for custom curling stone image override
        let drawnCustomStone = false;
        if (currentTheme && currentTheme.curlingStoneImageUrl) {
            const stoneImg = getLoadedImage(currentTheme.curlingStoneImageUrl);
            if (stoneImg) {
                ctx.drawImage(stoneImg, -r, -r, r * 2, r * 2);
                drawnCustomStone = true;
            }
        }

        if (!drawnCustomStone) {
            // 2. Granite Stone Body (Radial shaded granite)
            const stoneGrad = ctx.createRadialGradient(-r * 0.3, -r * 0.3, r * 0.1, 0, 0, r);
            stoneGrad.addColorStop(0.0, '#7f8c8d');
            stoneGrad.addColorStop(0.6, '#34495e');
            stoneGrad.addColorStop(1.0, '#1c2833');

            ctx.beginPath();
            ctx.arc(0, 0, r, 0, Math.PI * 2);
            ctx.fillStyle = stoneGrad;
            ctx.fill();
            ctx.strokeStyle = '#111822';
            ctx.lineWidth = 1.5;
            ctx.stroke();

            // Outer running bevel ring
            ctx.beginPath();
            ctx.arc(0, 0, r * 0.88, 0, Math.PI * 2);
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.35)';
            ctx.lineWidth = 1;
            ctx.stroke();

            // 3. Colored Striking Band (Player's Chat Color)
            ctx.beginPath();
            ctx.arc(0, 0, r * 0.82, 0, Math.PI * 2);
            ctx.strokeStyle = stone.userColor || '#3498db';
            ctx.lineWidth = Math.max(3, r * 0.14);
            ctx.stroke();

            // 4. Center Emote Decal or Chatter Avatar
            const innerRadius = r * 0.55;
            const decalImg = stone.decalUrl ? getLoadedImage(stone.decalUrl) : null;
            if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.drawCircularDecal) {
                DropGamesCommon.drawCircularDecal(ctx, decalImg, innerRadius, {
                    fallbackPenguin: true,
                    themeId: getSeasonPreset(),
                    userColor: stone.userColor,
                    displayName: stone.displayName,
                    strokeBorder: true,
                    borderColor: 'rgba(255, 255, 255, 0.4)',
                    borderWidth: 1.2
                });
            }
        } else {
            // Even with custom stone, render player decal on center
            const innerRadius = r * 0.45;
            const decalImg = stone.decalUrl ? getLoadedImage(stone.decalUrl) : null;
            if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.drawCircularDecal) {
                DropGamesCommon.drawCircularDecal(ctx, decalImg, innerRadius, {
                    fallbackPenguin: true,
                    themeId: getSeasonPreset(),
                    userColor: stone.userColor,
                    displayName: stone.displayName,
                    strokeBorder: true,
                    borderColor: 'rgba(255, 255, 255, 0.4)',
                    borderWidth: 1.2
                });
            }
        }

        // 5. Sculpted Gooseneck Handle (Rotates with stone spin)
        ctx.save();
        ctx.rotate(stone.angle || 0);
        ctx.beginPath();
        ctx.arc(0, -r * 0.15, r * 0.40, Math.PI * 0.85, Math.PI * 2.15);
        ctx.strokeStyle = stone.userColor || '#e74c3c';
        ctx.lineWidth = Math.max(2.5, r * 0.12);
        ctx.lineCap = 'round';
        ctx.stroke();

        // Chrome handle mounts
        ctx.fillStyle = '#ecf0f1';
        ctx.beginPath();
        ctx.arc(-r * 0.35, -r * 0.05, 2.5, 0, Math.PI * 2);
        ctx.arc(r * 0.35, -r * 0.05, 2.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();

        // 6. Resting Frost Glint (Pulses once stopped)
        if (stone.completed) {
            const glintPulse = (Math.sin(stone.glintPhase || 0) + 1.0) * 0.5;
            ctx.beginPath();
            ctx.arc(0, 0, r + 2 + glintPulse * 3, 0, Math.PI * 2);
            ctx.strokeStyle = `rgba(173, 216, 230, ${0.25 + glintPulse * 0.35})`;
            ctx.lineWidth = 1.5;
            ctx.stroke();
        }

        ctx.restore(); // Restore origin back to canvas space

        // 7. INWARD-FACING NAMETAG & SCORE PLACEMENT
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.drawInwardNametag) {
            DropGamesCommon.drawInwardNametag(ctx, stone.displayName || stone.username || 'Chatter', cx, cy, r, m.pos, {
                score: stone.score,
                completed: stone.completed,
                isLeader: stone.isLeader,
                userColor: stone.userColor,
                fontSize: headerConfig.nameFontSize || 15
            });
        }
    }

    // Start rendering loop
    requestAnimationFrame(render);

    // Export CurlingEngine for unit testing
    const CurlingEngine = {
        boardConfig: boardConfig,
        sessionState: sessionState,
        activeStones: activeStones,
        handleCurlingSlide: handleCurlingSlide,
        handleWsMessage: handleWsMessage,
        evaluateStoneOutcome: evaluateStoneOutcome,
        getTrackMetrics: getTrackMetrics
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = CurlingEngine;
    }
    if (typeof global !== 'undefined') {
        global.CurlingEngine = CurlingEngine;
    }

})(typeof window !== 'undefined' ? window : this);
