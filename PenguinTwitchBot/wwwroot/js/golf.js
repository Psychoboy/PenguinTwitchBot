/**
 * Golf (Chip & Putt) Overlay Engine
 * Features:
 * - Non-intrusive 60fps high-DPI transparent Canvas overlay
 * - Invisible fairway by default (boardAlpha = 0.0, showBoardBorders = false) so only flying/rolling balls and the compact putting green appear
 * - Compact semi-transparent putting green (targetAlpha = 0.45) with sunken cup, fluttering flagstick, and closest-to-the-pin guide rings
 * - 2.5D parabolic flight arc (heightZ) with ground shadow, turf bounce + divot particles, and smooth ground roll
 * - Cup capture vs. Lip-Out rim deflection when a ball hits the cup too fast
 * - Option B Closest-to-the-Pin continuous proximity scoring (100 pts sunk, 10-95 pts on green, 0 pts off green)
 * - 1-Stroke (default) or Multi-Stroke mode (1-5 strokes) with configurable per-stroke point penalty
 */
(function (global) {
    'use strict';

    const canvas = typeof document !== 'undefined' ? document.getElementById('golf-canvas') : null;
    const ctx = canvas ? canvas.getContext('2d') : null;

    const urlParams = (typeof window !== 'undefined' && window.location)
        ? new URLSearchParams(window.location.search)
        : new URLSearchParams();
    const overlayKey = urlParams.get('key') || '';

    let width = 1920;
    let height = 1080;
    let dpr = 1;

    function resize() {
        if (!canvas || !ctx || typeof window === 'undefined') return;
        dpr = window.devicePixelRatio || 1;
        width = window.innerWidth || 1920;
        height = window.innerHeight || 1080;
        canvas.width = Math.round(width * dpr);
        canvas.height = Math.round(height * dpr);
        ctx.resetTransform();
        ctx.scale(dpr, dpr);
    }
    if (typeof window !== 'undefined') {
        window.addEventListener('resize', resize);
        resize();
    }

    const imageLoader = (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.createImageLoader)
        ? DropGamesCommon.createImageLoader()
        : {
            getImage: function (url) {
                if (!url || typeof Image === 'undefined') return null;
                const img = new Image();
                img.src = url;
                return img.complete ? img : null;
            },
            getLoadedImage: function (url) {
                if (!url || typeof Image === 'undefined') return null;
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

    const audio = (typeof DropGamesAudio !== 'undefined' && DropGamesAudio.createAudioEngine)
        ? DropGamesAudio.createAudioEngine()
        : null;

    let soundBundleManager = null;
    if (typeof DropGamesAudio !== 'undefined' && typeof DropGamesAudio.createSoundBundleManager === 'function') {
        soundBundleManager = DropGamesAudio.createSoundBundleManager();
    }

    function playSound(soundDto, synthType = 'strike', defaultFreq = 440) {
        if (!soundDto || soundDto.enabled === false) return;
        const vol = (typeof soundDto.volume === 'number') ? soundDto.volume : 65;
        const volFrac = Math.max(0, Math.min(1, vol / 100));

        if (soundDto.url && soundDto.url.length > 0 && typeof Audio !== 'undefined') {
            try {
                const a = new Audio(soundDto.url);
                a.volume = volFrac;
                a.play().catch(() => {});
                return;
            } catch (_) {}
        }

        if (audio) {
            try {
                if (synthType === 'strike' && typeof audio.playGolfStrike === 'function') {
                    audio.playGolfStrike(volFrac);
                } else if (synthType === 'bounce' && typeof audio.playGolfBounce === 'function') {
                    audio.playGolfBounce(volFrac);
                } else if (synthType === 'cup' && typeof audio.playGolfCup === 'function') {
                    audio.playGolfCup(volFrac);
                } else if (synthType === 'lipout' && typeof audio.playGolfLipOut === 'function') {
                    audio.playGolfLipOut(volFrac);
                } else if (synthType === 'win' && typeof audio.playGolfWin === 'function') {
                    audio.playGolfWin(volFrac);
                } else if (synthType === 'fanfare' && typeof audio.playBullseyeFanfare === 'function') {
                    audio.playBullseyeFanfare(volFrac);
                } else if (synthType === 'hit' && typeof audio.playCurlingHit === 'function') {
                    audio.playCurlingHit(volFrac);
                } else if (typeof audio.playProceduralChime === 'function') {
                    audio.playProceduralChime(defaultFreq, volFrac);
                }
            } catch (_) {}
        }
    }

    // Non-intrusive defaults: invisible fairway (boardAlpha = 0.0, showBoardBorders = false)
    // and compact semi-transparent putting green (targetAlpha = 0.45)
    const boardConfig = {
        stripPosition: 'Bottom',
        slideDirection: 'LeftToRight',
        isReversed: false,
        trackThicknessPercent: 14.0,
        boardAlpha: 0.0,
        showBoardBorders: false,
        targetAlpha: 0.45,
        collisionMode: 'LockOnRest',
        condition: 'StandardGreen',
        conditionLabel: 'Standard Green',
        targetDistancePercent: 84.0,
        powerVariancePercent: 3.0,
        cupCaptureSpeedMultiplier: 1.0,
        maxStrokesPerPlayer: 1,
        strokePenaltyPoints: 10,
        maxScore: 100,
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
        totalDrops: 0,
        sounds: null
    };

    let activeBalls = [];
    let ambientParticles = [];
    let particles = [];
    let scorePopups = [];
    let currentTheme = null;

    let isOtherGameActive = false;
    const activeOtherGames = new Set();
    let otherDropsActiveUntilMs = 0;
    let currentBannerY = 18;
    let lastCollisionSoundMs = 0;

    function nowMs() {
        return (typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now();
    }

    function getSeasonPreset() {
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.getSeasonPreset) {
            return DropGamesCommon.getSeasonPreset(currentTheme);
        }
        return 'summer';
    }

    function spawnDivotBurst(x, y) {
        const colors = ['#22C55E', '#15803D', '#854D0E', '#4ADE80'];
        for (let i = 0; i < 8; i++) {
            const angle = -Math.PI * (0.15 + Math.random() * 0.70);
            const spd = 25 + Math.random() * 45;
            particles.push({
                x: x,
                y: y,
                vx: Math.cos(angle) * spd,
                vy: Math.sin(angle) * spd - 12,
                size: 2.5 + Math.random() * 2.5,
                color: colors[i % colors.length],
                alpha: 0.9,
                decay: 0.035 + Math.random() * 0.02,
                type: 'leaf',
                rotation: Math.random() * Math.PI * 2,
                rotSpeed: (Math.random() - 0.5) * 6.0,
                isNormalized: false
            });
        }
    }

    function spawnSeasonalBurst(x, y, season, count, isBullseye) {
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.spawnSeasonalBurst) {
            DropGamesCommon.spawnSeasonalBurst(particles, { x, y, season, count, isBullseye });
        }
    }

    // Calculate Option B Closest-to-the-Pin score + multi-stroke penalty
    function calculateGolfScore(distFromPinPx, greenRadiusPx, cupRadiusPx, isSunk, strokeNumber, strokePenaltyPoints, maxScore) {
        const stroke = Math.max(1, strokeNumber || 1);
        const penaltyPerStroke = Math.max(0, typeof strokePenaltyPoints === 'number' ? strokePenaltyPoints : 10);
        const scoreCap = Math.max(1, Math.min(1000, typeof maxScore === 'number' ? maxScore : 100));
        const strokePenalty = (stroke - 1) * penaltyPerStroke;

        let baseScore = 0;
        let isBullseye = false;
        let isWin = false;
        let isHit = false;

        if (isSunk) {
            baseScore = scoreCap;
            isBullseye = true;
            isWin = true;
            isHit = true;
        } else if (distFromPinPx <= greenRadiusPx) {
            const usableSpan = Math.max(1, greenRadiusPx - cupRadiusPx);
            const t = Math.max(0, Math.min(1, (distFromPinPx - cupRadiusPx) / usableSpan));
            baseScore = Math.max(1, Math.min(scoreCap, Math.round(scoreCap - t * (scoreCap - 1))));
            isBullseye = false;
            isWin = true;
            isHit = true;
        } else {
            baseScore = 0;
            isBullseye = false;
            isWin = false;
            isHit = false;
        }

        const finalScore = Math.max(0, baseScore - strokePenalty);
        return {
            score: finalScore,
            baseScore: baseScore,
            strokePenalty: strokePenalty,
            strokeNumber: stroke,
            isBullseye: isBullseye,
            isHoleInOne: isBullseye && stroke === 1,
            isWin: isWin,
            isHit: isHit
        };
    }

    // WebSocket connection & completion queue
    const socket = (typeof window !== 'undefined' && typeof window.createWsSocket === 'function' && window.WS_TOPICS)
        ? window.createWsSocket('golf', [window.WS_TOPICS.golf || 'golf'])
        : null;

    // Shared completion queue + heartbeat + socket wiring (see DropGamesCommon.createCompletionChannel)
    const completionChannel = DropGamesCommon.createCompletionChannel({
        game: 'golf',
        socket: socket,
        overlayKey: overlayKey,
        fields: ['dropToken', 'score', 'baseScore', 'strokeNumber', 'strokePenalty', 'startAxisPosition', 'isLipOut', 'isBullseye', 'isWin', 'isHit', 'landingX'],
        getActiveTokens: () => Array.isArray(activeBalls)
            ? activeBalls.filter(b => b && b.dropToken && !b.completed).map(b => b.dropToken)
            : []
    });
    const pendingCompletions = completionChannel.pending;
    const sendCompletion = completionChannel.send;
    const flushPendingCompletions = completionChannel.flush;
    const sendHeartbeat = completionChannel.sendHeartbeat;
    const ensureHeartbeat = completionChannel.ensureHeartbeat;
    completionChannel.attach(msg => handleWsMessage(msg));

    function getTrackMetrics() {
        const metrics = DropGamesCommon.getStripMetrics(width, height, boardConfig, 84.0);
        const ballRadius = Math.max(11, Math.min(24, metrics.breadth * 0.22));
        // Compact putting green matching Curling house footprint (~42% of strip breadth radius)
        const greenRadius = Math.max(36, Math.min(metrics.breadth * 0.44, ballRadius * 3.2));
        const cupRadius = Math.max(6, Math.min(13, ballRadius * 0.65));
        return {
            ...metrics,
            ballRadius: ballRadius,
            greenRadius: greenRadius,
            cupRadius: cupRadius
        };
    }

    function getBallCanvasCoords(ball, m) {
        let cx, cy;
        if (m.isHorizontal) {
            cx = m.x + ball.u * m.length;
            cy = m.y + m.breadth * (0.50 + (ball.v || 0));
        } else {
            cx = m.x + m.breadth * (0.50 + (ball.v || 0));
            cy = m.y + ball.u * m.length;
        }
        return { x: cx, y: cy };
    }

    function handleWsMessage(msg) {
        if (!msg || typeof msg !== 'object') return;

        if (msg.type === 'other_game_status') {
            isOtherGameActive = DropGamesCommon.updateActiveGameStateFromMessage(activeOtherGames, 'golf', msg);
            return;
        }

        if (msg.type === 'other_drop_active') {
            const durSec = typeof msg.durationSeconds === 'number' ? msg.durationSeconds : 10.0;
            otherDropsActiveUntilMs = Math.max(otherDropsActiveUntilMs, nowMs() + durSec * 1000);
            return;
        }

        const activeGameStatus = DropGamesCommon.updateActiveGameStateFromMessage(activeOtherGames, 'golf', msg);
        if (activeGameStatus !== null) isOtherGameActive = activeGameStatus;

        if (msg.type === 'session_start' && msg.session) {
            const s = msg.session;
            sessionState.isActive = true;
            sessionState.endedAtMs = 0;
            sessionState.totalDurationSeconds = s.totalDurationSeconds || 90;
            sessionState.remainingSeconds = s.remainingSeconds || sessionState.totalDurationSeconds;
            sessionState.startedAtMs = nowMs();
            sessionState.endsAtMs = nowMs() + (s.remainingSeconds || s.totalDurationSeconds || 90) * 1000;
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
            if (typeof msg.collisionMode === 'string') boardConfig.collisionMode = msg.collisionMode;
            if (typeof msg.condition === 'string') boardConfig.condition = msg.condition;
            if (typeof msg.conditionLabel === 'string') boardConfig.conditionLabel = msg.conditionLabel;
            if (typeof msg.targetDistancePercent === 'number') boardConfig.targetDistancePercent = msg.targetDistancePercent;
            if (typeof msg.powerVariancePercent === 'number') boardConfig.powerVariancePercent = msg.powerVariancePercent;
            if (typeof msg.cupCaptureSpeedMultiplier === 'number') boardConfig.cupCaptureSpeedMultiplier = msg.cupCaptureSpeedMultiplier;
            if (typeof msg.maxStrokesPerPlayer === 'number') boardConfig.maxStrokesPerPlayer = msg.maxStrokesPerPlayer;
            if (typeof msg.strokePenaltyPoints === 'number') boardConfig.strokePenaltyPoints = msg.strokePenaltyPoints;
            if (typeof msg.maxScore === 'number') boardConfig.maxScore = msg.maxScore;

            boardConfig.visibleUntil = nowMs() + sessionState.totalDurationSeconds * 1000 + 6000;

            if (msg.sounds) {
                sessionState.sounds = msg.sounds;
                if (soundBundleManager) soundBundleManager.updateBundle(msg.sounds);
            }
            playSound(msg.sounds?.start || { enabled: true }, 'chime', 540);
        } else if (msg.type === 'session_end') {
            sessionState.isActive = false;
            sessionState.endedAtMs = nowMs();
            boardConfig.visibleUntil = nowMs() + 6000;
            if (msg.headerConfig) Object.assign(headerConfig, msg.headerConfig);
            else if (msg.session && msg.session.headerConfig) Object.assign(headerConfig, msg.session.headerConfig);

            playSound(msg.sounds?.end || { enabled: true }, 'chime', 390);
        } else if (msg.type === 'golf_shot' || msg.type === 'golf_putt') {
            handleGolfShot(msg);
        }
    }

    function handleGolfShot(msg) {
        if (!msg || !msg.dropToken) return;
        sessionState.totalDrops++;

        if (typeof msg.stripPosition === 'string') boardConfig.stripPosition = msg.stripPosition;
        if (typeof msg.slideDirection === 'string') boardConfig.slideDirection = msg.slideDirection;
        if (typeof msg.isReversed === 'boolean') boardConfig.isReversed = msg.isReversed;
        else if (typeof msg.slideDirection === 'string') {
            boardConfig.isReversed = (boardConfig.slideDirection === 'RightToLeft' || boardConfig.slideDirection === 'BottomToTop');
        }
        if (typeof msg.collisionMode === 'string') boardConfig.collisionMode = msg.collisionMode;
        if (typeof msg.condition === 'string') boardConfig.condition = msg.condition;
        if (typeof msg.conditionLabel === 'string') boardConfig.conditionLabel = msg.conditionLabel;
        if (typeof msg.targetDistancePercent === 'number') boardConfig.targetDistancePercent = msg.targetDistancePercent;
        if (typeof msg.powerVariancePercent === 'number') boardConfig.powerVariancePercent = msg.powerVariancePercent;
        if (typeof msg.cupCaptureSpeedMultiplier === 'number') boardConfig.cupCaptureSpeedMultiplier = msg.cupCaptureSpeedMultiplier;
        if (typeof msg.trackThicknessPercent === 'number') boardConfig.trackThicknessPercent = msg.trackThicknessPercent;
        if (typeof msg.boardAlpha === 'number') boardConfig.boardAlpha = msg.boardAlpha;
        if (typeof msg.showBoardBorders === 'boolean') boardConfig.showBoardBorders = msg.showBoardBorders;
        if (typeof msg.targetAlpha === 'number') boardConfig.targetAlpha = msg.targetAlpha;
        if (typeof msg.maxStrokesPerPlayer === 'number') boardConfig.maxStrokesPerPlayer = msg.maxStrokesPerPlayer;
        if (typeof msg.strokePenaltyPoints === 'number') boardConfig.strokePenaltyPoints = msg.strokePenaltyPoints;
        if (typeof msg.maxScore === 'number') boardConfig.maxScore = msg.maxScore;
        if (msg.theme) currentTheme = msg.theme;
        if (msg.sounds) {
            sessionState.sounds = msg.sounds;
            if (soundBundleManager) soundBundleManager.updateBundle(msg.sounds);
        }

        boardConfig.visibleUntil = Math.max(boardConfig.visibleUntil, nowMs() + 15000);

        const m = getTrackMetrics();
        const rawPower = typeof msg.power === 'number' ? msg.power : 75;
        const variancePct = typeof msg.powerVariancePercent === 'number'
            ? msg.powerVariancePercent
            : (typeof boardConfig.powerVariancePercent === 'number' ? boardConfig.powerVariancePercent : 3.0);
        const cupCaptureMult = typeof msg.cupCaptureSpeedMultiplier === 'number'
            ? msg.cupCaptureSpeedMultiplier
            : (typeof boardConfig.cupCaptureSpeedMultiplier === 'number' ? boardConfig.cupCaptureSpeedMultiplier : 1.0);
        const variedPower = variancePct > 0
            ? Math.max(1.0, Math.min(100.0, rawPower + (Math.random() * 2.0 - 1.0) * variancePct))
            : rawPower;
        const strokeNumber = typeof msg.strokeNumber === 'number' && msg.strokeNumber >= 1 ? msg.strokeNumber : 1;
        const maxStrokes = typeof msg.maxStrokesPerPlayer === 'number' ? msg.maxStrokesPerPlayer : (boardConfig.maxStrokesPerPlayer || 1);
        const strokePenaltyPoints = typeof msg.strokePenaltyPoints === 'number' ? msg.strokePenaltyPoints : (boardConfig.strokePenaltyPoints ?? 10);

        const isReversed = (typeof msg.isReversed === 'boolean')
            ? msg.isReversed
            : m.isReversed;

        // Check if this player already has a resting ball on the course for a multi-stroke follow-up
        const userLower = (msg.username || '').toLowerCase();
        let existingIdx = -1;
        if (userLower) {
            for (let i = activeBalls.length - 1; i >= 0; i--) {
                if (activeBalls[i].completed && !activeBalls[i].isSunk && (activeBalls[i].username || '').toLowerCase() === userLower) {
                    existingIdx = i;
                    break;
                }
            }
        }

        let startU = isReversed ? 0.95 : 0.05;
        let startV = 0;
        const maxV = Math.max(0.04, 0.36 - (m.ballRadius / m.breadth));

        if (existingIdx >= 0 && strokeNumber > 1) {
            startU = activeBalls[existingIdx].u;
            startV = activeBalls[existingIdx].v;
            activeBalls.splice(existingIdx, 1);
        } else if (typeof msg.startAxisPosition === 'number' && msg.startAxisPosition > 0.01 && msg.startAxisPosition < 0.99) {
            startU = isReversed ? (1.0 - msg.startAxisPosition) : msg.startAxisPosition;
            startV = variancePct <= 0 ? 0 : (Math.random() * 2 - 1) * (maxV * 0.22);
        } else {
            startV = variancePct <= 0 ? 0 : (Math.random() * 2 - 1) * (maxV * 0.35);
        }

        // Direction toward the pin (allows putting back toward the cup if overshot!)
        const distToPinSigned = m.effectiveTargetRatio - startU;
        const directionSign = Math.abs(distToPinSigned) > 0.005
            ? Math.sign(distToPinSigned)
            : (isReversed ? -1.0 : 1.0);

        const distToPinAbs = Math.abs(distToPinSigned);
        const isChipShot = strokeNumber === 1 || distToPinAbs > 0.22;

        // Calibrate physics with GolfSimulationEngine.cs:
        // StandardGreen friction = 0.22, initialVelocity = (power / 100) * 0.76, travel = v0^2 / (2 * decel)
        let dragMult = 1.0;
        if (boardConfig.condition === 'FastLinks' || boardConfig.condition === 'FastGreen') dragMult = 0.78;
        else if (boardConfig.condition === 'HeavyDew' || boardConfig.condition === 'HeavyRough' || boardConfig.condition === 'MorningDew') dragMult = 1.28;
        else if (boardConfig.condition === 'CrossBreeze' || boardConfig.condition === 'CrossWind') dragMult = 1.04;

        const powerFrac = Math.max(0.01, Math.min(1.0, variedPower / 100.0));
        const baseDecel = 0.22 * dragMult;
        const v0Server = powerFrac * 0.76;
        const rawTravel = (v0Server * v0Server) / (2.0 * baseDecel);

        // On tee shot, use exact server travel distance; on short follow-up strokes, scale proportionally
        const shortShotScale = (strokeNumber > 1 && distToPinAbs < 0.55)
            ? Math.max(0.12, Math.min(1.0, distToPinAbs / 0.78))
            : 1.0;
        const desiredTravel = rawTravel * shortShotScale;
        const targetRestU = Math.max(0.01, Math.min(0.99, startU + directionSign * desiredTravel));

        // Flight arc parameters for chip/tee shots vs. pure ground roll for short putts
        let initialSpeedU;
        let initialVz = 0;
        let gravityZ = 2.6;

        if (isChipShot && desiredTravel > 0.14) {
            // Parabolic carry covers ~66% of distance in the air, then bounces & rolls the remaining ~34%
            const airTime = Math.max(0.55, Math.min(1.35, 0.65 + powerFrac * 0.55));
            initialVz = 0.5 * gravityZ * airTime;
            initialSpeedU = directionSign * (desiredTravel * 0.66) / airTime;
        } else {
            // Pure putting roll: v0 = sqrt(2 * a * d)
            initialSpeedU = directionSign * Math.sqrt(2.0 * baseDecel * desiredTravel);
            initialVz = 0;
        }

        // Lateral dispersion across the putting green (no artificial magnet to v=0 unless variancePct <= 0 test shot)
        const isCrossBreeze = boardConfig.condition === 'CrossBreeze' || boardConfig.condition === 'CrossWind';
        let breakSpeedV = 0;
        if (variancePct > 0) {
            const greenMaxV = Math.min(maxV * 0.85, (m.greenRadius / m.breadth) * 0.90);
            const spreadHalfWidth = strokeNumber > 1
                ? Math.min(greenMaxV * 0.45, (m.cupRadius / m.breadth) * 2.2)
                : greenMaxV * 0.78;
            // Triangular distribution centered on the pin line (v = 0), so shots cluster on the green
            // with only well-lined shots crossing directly over the cup
            const triRand = (Math.random() + Math.random()) - 1.0;
            const windDrift = isCrossBreeze ? (Math.random() * 0.08 - 0.04) : 0;
            const targetV = Math.max(-maxV * 0.92, Math.min(maxV * 0.92, triRand * spreadHalfWidth + windDrift));
            const estTotalTime = Math.max(0.8, isChipShot ? 2.1 : 1.3);
            breakSpeedV = (targetV - startV) / estTotalTime;
        }

        const newBall = {
            dropToken: msg.dropToken,
            username: msg.username || 'Chatter',
            displayName: msg.displayName || msg.username || 'Chatter',
            userColor: msg.userColor || '#22C55E',
            decalUrl: msg.decalUrl || msg.avatarUrl || msg.emoteUrl || '',
            power: rawPower,
            strokeNumber: strokeNumber,
            maxStrokesPerPlayer: maxStrokes,
            strokePenaltyPoints: strokePenaltyPoints,
            maxScore: typeof msg.maxScore === 'number' ? msg.maxScore : (boardConfig.maxScore || 100),
            startAxisPosition: isReversed ? (1.0 - startU) : startU,
            isReversed: isReversed,
            directionSign: directionSign,
            targetRestU: targetRestU,
            cupCaptureSpeedMultiplier: cupCaptureMult,

            u: startU,
            v: startV,
            speedU: initialSpeedU,
            speedV: breakSpeedV,
            drag: baseDecel,

            heightZ: 0.0,
            vz: initialVz,
            gravityZ: gravityZ,
            bounceCount: 0,
            maxBounces: isChipShot ? 2 : 0,

            angle: 0.0,
            radius: m.ballRadius,
            sinkScale: 1.0,
            isSunk: false,
            isLipOut: false,
            completed: false,
            score: null,
            baseScore: null,
            strokePenalty: 0,
            landedAtMs: 0,
            isLeader: false
        };

        if (newBall.decalUrl) getLoadedImage(newBall.decalUrl);
        if (currentTheme && currentTheme.golfBallImageUrl) getLoadedImage(currentTheme.golfBallImageUrl);
        if (currentTheme && currentTheme.golfGreenImageUrl) getLoadedImage(currentTheme.golfGreenImageUrl);
        if (currentTheme && currentTheme.golfFlagPinImageUrl) getLoadedImage(currentTheme.golfFlagPinImageUrl);

        activeBalls.push(newBall);
        ensureHeartbeat();
        const sounds = msg.sounds || sessionState.sounds || {};
        playSound(sounds.golfStrike || sounds.strike || sounds.slide || { enabled: true }, 'strike');
    }

    function updateLeader(m) {
        if (!activeBalls || activeBalls.length === 0) return;
        const metrics = m || getTrackMetrics();
        let maxScore = -1;
        let minPinDist = 999999;
        let leaderBall = null;

        for (let i = 0; i < activeBalls.length; i++) {
            const b = activeBalls[i];
            b.isLeader = false;
            if (b.completed && typeof b.score === 'number' && b.score > 0) {
                const pos = getBallCanvasCoords(b, metrics);
                const dist = Math.hypot(pos.x - metrics.targetCenterX, pos.y - metrics.targetCenterY);
                if (b.score > maxScore || (b.score === maxScore && dist < minPinDist)) {
                    maxScore = b.score;
                    minPinDist = dist;
                    leaderBall = b;
                }
            }
        }

        if (leaderBall && maxScore > 0) {
            leaderBall.isLeader = true;
        }
    }

    function completeBall(ball, m) {
        if (ball.completed) return;
        ball.completed = true;
        ball.speedU = 0;
        ball.speedV = 0;
        ball.heightZ = 0;
        ball.vz = 0;
        ball.landedAtMs = nowMs();

        const pos = getBallCanvasCoords(ball, m);
        const distFromPinPx = ball.isSunk
            ? 0
            : Math.hypot(pos.x - m.targetCenterX, pos.y - m.targetCenterY);

        const outcome = calculateGolfScore(
            distFromPinPx,
            m.greenRadius,
            m.cupRadius,
            ball.isSunk,
            ball.strokeNumber,
            ball.strokePenaltyPoints,
            ball.maxScore
        );

        ball.score = outcome.score;
        ball.baseScore = outcome.baseScore;
        ball.strokePenalty = outcome.strokePenalty;
        ball.isBullseye = outcome.isBullseye;
        ball.isHoleInOne = outcome.isHoleInOne;
        ball.isWin = outcome.isWin;
        ball.isHit = outcome.isHit;

        updateLeader(m);

        const sounds = sessionState.sounds || {};
        if (ball.isSunk) {
            playSound(sounds.golfWin || sounds.win || { enabled: true }, ball.isHoleInOne ? 'fanfare' : 'win');
        } else if (outcome.score > 0) {
            playSound(sounds.golfWin || sounds.score || sounds.win || { enabled: true }, 'win');
        }

        // Visual score popup
        let popupText;
        let popupColor;
        if (ball.isHoleInOne) {
            popupText = `⛳ HOLE-IN-ONE! +${outcome.score}`;
            popupColor = '#FFD700';
        } else if (ball.isSunk) {
            popupText = `⛳ SUNK! +${outcome.score}`;
            popupColor = '#34D399';
        } else if (outcome.score > 0) {
            popupText = ball.strokePenalty > 0
                ? `+${outcome.score} (${outcome.baseScore}-${ball.strokePenalty})`
                : `+${outcome.score} ON GREEN`;
            popupColor = '#4ADE80';
        } else {
            popupText = ball.isLipOut ? 'LIP-OUT! 0' : 'OFF GREEN';
            popupColor = '#F87171';
        }

        scorePopups.push({
            x: pos.x,
            y: pos.y - 10,
            text: popupText,
            color: popupColor,
            createdAtMs: nowMs()
        });

        const season = getSeasonPreset();
        if (ball.isSunk) {
            spawnSeasonalBurst(pos.x, pos.y, season, 40, true);
        } else if (outcome.score > 0) {
            spawnSeasonalBurst(pos.x, pos.y, season, 18, false);
        }

        const normalizedLandingAxis = ball.isReversed ? (1.0 - ball.u) : ball.u;
        sendCompletion({
            dropToken: ball.dropToken,
            score: outcome.score,
            baseScore: outcome.baseScore,
            strokeNumber: ball.strokeNumber,
            strokePenalty: outcome.strokePenalty,
            startAxisPosition: ball.startAxisPosition,
            isLipOut: ball.isLipOut,
            isBullseye: outcome.isBullseye,
            isWin: outcome.isWin,
            isHit: outcome.isHit,
            landingX: Math.max(0.01, Math.min(0.99, normalizedLandingAxis))
        });
    }

    function resolveGroundCollisions(m) {
        const mode = boardConfig.collisionMode || 'LockOnRest';
        if (mode === 'NoCollision') return;

        const count = activeBalls.length;
        for (let i = 0; i < count; i++) {
            const a = activeBalls[i];
            if (a.isSunk || a.heightZ > 0.02) continue;

            for (let j = i + 1; j < count; j++) {
                const b = activeBalls[j];
                if (b.isSunk || b.heightZ > 0.02) continue;

                if (mode === 'LockOnRest' && (a.completed || b.completed)) {
                    continue;
                }

                const posA = getBallCanvasCoords(a, m);
                const posB = getBallCanvasCoords(b, m);
                const rA = a.radius || m.ballRadius;
                const rB = b.radius || m.ballRadius;

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

                if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.resolveCircleCollision) {
                    const res = DropGamesCommon.resolveCircleCollision(posA, posB, velA, velB, rA, rB, {
                        restitution: 0.78
                    });
                    if (res && res.collided) {
                        if (m.isHorizontal) {
                            a.u = (posA.x - m.x) / m.length;
                            a.v = ((posA.y - m.y) / m.breadth) - 0.50;
                            b.u = (posB.x - m.x) / m.length;
                            b.v = ((posB.y - m.y) / m.breadth) - 0.50;
                            a.speedU = velA.vx / m.length;
                            a.speedV = velA.vy / m.breadth;
                            b.speedU = velB.vx / m.length;
                            b.speedV = velB.vy / m.breadth;
                        } else {
                            a.v = ((posA.x - m.x) / m.breadth) - 0.50;
                            a.u = (posA.y - m.y) / m.length;
                            b.v = ((posB.x - m.x) / m.breadth) - 0.50;
                            b.u = (posB.y - m.y) / m.length;
                            a.speedV = velA.vx / m.breadth;
                            a.speedU = velA.vy / m.length;
                            b.speedV = velB.vx / m.breadth;
                            b.speedU = velB.vy / m.length;
                        }

                        if (mode === 'FullCollision') {
                            if (a.completed && Math.hypot(a.speedU, a.speedV) > 0.015) a.completed = false;
                            if (b.completed && Math.hypot(b.speedU, b.speedV) > 0.015) b.completed = false;
                        }

                        const now = nowMs();
                        if (!res.separating && now - lastCollisionSoundMs > 90) {
                            lastCollisionSoundMs = now;
                            playSound(sessionState.sounds?.collision || { enabled: true }, 'hit');
                        }
                    }
                }
            }
        }
    }

    function stepPhysics(dt, m) {
        const maxV = Math.max(0.05, 0.44 - (m.ballRadius / m.breadth));

        for (let i = activeBalls.length - 1; i >= 0; i--) {
            const ball = activeBalls[i];
            if (ball.completed) continue;

            // Advance horizontal/vertical strip coordinates
            ball.u += ball.speedU * dt;
            ball.v += ball.speedV * dt;
            ball.angle += (ball.speedU * 18.0) * dt;

            // Soft turf edge containment
            if (ball.v > maxV) {
                ball.v = maxV;
                ball.speedV = -Math.abs(ball.speedV) * 0.5;
            } else if (ball.v < -maxV) {
                ball.v = -maxV;
                ball.speedV = Math.abs(ball.speedV) * 0.5;
            }

            // Parabolic flight arc (heightZ) & turf bounce
            if (ball.heightZ > 0 || ball.vz > 0) {
                ball.heightZ += ball.vz * dt;
                ball.vz -= ball.gravityZ * dt;

                // Slight air drag while airborne
                ball.speedU *= (1.0 - 0.12 * dt);

                if (ball.heightZ <= 0) {
                    ball.heightZ = 0;
                    const pos = getBallCanvasCoords(ball, m);
                    spawnDivotBurst(pos.x, pos.y);
                    playSound(sessionState.sounds?.golfBounce || sessionState.sounds?.bounce || { enabled: true }, 'bounce');

                    if (ball.bounceCount < ball.maxBounces && Math.abs(ball.vz) > 0.28) {
                        ball.bounceCount++;
                        ball.vz = Math.abs(ball.vz) * 0.36;
                        ball.speedU *= 0.72;
                    } else {
                        ball.vz = 0;
                        if (typeof ball.targetRestU === 'number') {
                            const dir = ball.directionSign || 1;
                            const remDist = Math.max(0, (ball.targetRestU - ball.u) * dir);
                            ball.speedU = dir * Math.sqrt(2.0 * (ball.drag || 0.22) * remDist);
                        } else {
                            ball.speedU *= 0.68;
                        }
                    }
                }
            } else {
                // Ground rolling friction calibrated to bring ball to targetRestU
                const decel = ball.drag || 0.22;
                const absSpeedU = Math.abs(ball.speedU);
                if (absSpeedU > 0.0001) {
                    const nextAbsU = Math.max(0, absSpeedU - decel * dt);
                    const ratio = nextAbsU / absSpeedU;
                    ball.speedU *= ratio;
                    ball.speedV *= ratio;
                } else {
                    ball.speedU = 0;
                    ball.speedV = 0;
                }

                // Check Cup Capture vs. Lip-Out when rolling across the cup
                const pos = getBallCanvasCoords(ball, m);
                const dxPin = pos.x - m.targetCenterX;
                const dyPin = pos.y - m.targetCenterY;
                const distToCupPx = Math.hypot(dxPin, dyPin);
                const captureRadiusPx = m.cupRadius * 0.78;
                const rimRadiusPx = m.cupRadius * 1.08;
                const groundSpd = Math.hypot(ball.speedU, ball.speedV);
                const maxCaptureSpeed = 0.065 * (ball.cupCaptureSpeedMultiplier || boardConfig.cupCaptureSpeedMultiplier || 1.0);

                if (!ball.isLipOut && distToCupPx <= captureRadiusPx && groundSpd <= maxCaptureSpeed) {
                    ball.isSunk = true;
                    ball.sinkScale = 0.72;
                    ball.u = m.effectiveTargetRatio;
                    ball.v = 0;
                    playSound(sessionState.sounds?.golfCup || sessionState.sounds?.cup || { enabled: true }, 'cup');
                    completeBall(ball, m);
                    continue;
                } else if (!ball.isLipOut && distToCupPx <= rimRadiusPx && groundSpd > maxCaptureSpeed && groundSpd <= maxCaptureSpeed * 2.6) {
                    // Lip-out rim deflection when rolling across the cup too fast!
                    ball.isLipOut = true;
                    ball.speedU *= 0.72;
                    ball.speedV += (ball.v >= 0 ? 1 : -1) * 0.042;
                    playSound(sessionState.sounds?.golfLipOut || sessionState.sounds?.lipOut || { enabled: true }, 'lipout');
                    scorePopups.push({
                        x: m.targetCenterX,
                        y: m.targetCenterY - m.cupRadius - 12,
                        text: 'LIP-OUT!',
                        color: '#FBBF24',
                        createdAtMs: nowMs()
                    });
                }

                // Stop condition or out-of-bounds end of strip
                if (ball.u <= 0.01 || ball.u >= 0.99 || Math.hypot(ball.speedU, ball.speedV) < 0.004) {
                    ball.u = Math.max(0.01, Math.min(0.99, ball.u));
                    completeBall(ball, m);
                }
            }
        }

        resolveGroundCollisions(m);
    }

    function drawConditionPill(m) {
        if (!ctx || !boardConfig.condition || boardConfig.condition === 'StandardGreen' || boardConfig.condition === 'Normal') return;

        ctx.save();
        const label = boardConfig.conditionLabel || boardConfig.condition;
        let icon = '⛳';
        if (boardConfig.condition === 'FastLinks' || boardConfig.condition === 'FastGreen') icon = '⚡';
        else if (boardConfig.condition === 'HeavyDew' || boardConfig.condition === 'HeavyRough' || boardConfig.condition === 'MorningDew') icon = '🌿';
        else if (boardConfig.condition === 'CrossBreeze' || boardConfig.condition === 'CrossWind') icon = '💨';

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
            pillX = m.x + (m.w - pillW) * 0.5;
            pillY = m.y - pillH - 8;
        }

        ctx.fillStyle = 'rgba(15, 23, 42, 0.85)';
        ctx.strokeStyle = 'rgba(74, 222, 128, 0.55)';
        ctx.lineWidth = 1.2;
        ctx.beginPath();
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.drawRoundRect) {
            DropGamesCommon.drawRoundRect(ctx, pillX, pillY, pillW, pillH, 12);
        } else {
            ctx.rect(pillX, pillY, pillW, pillH);
        }
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = '#DCFCE7';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(text, pillX + pillW * 0.5, pillY + pillH * 0.5 + 1);
        ctx.restore();
    }

    let lastFrameTime = nowMs();

    function render(timestamp) {
        if (!ctx) return;
        const now = typeof timestamp === 'number' ? timestamp : nowMs();
        const dt = Math.min(0.05, Math.max(0.001, (now - lastFrameTime) / 1000));
        lastFrameTime = now;

        ctx.clearRect(0, 0, width, height);

        const m = getTrackMetrics();
        stepPhysics(dt, m);

        // Clean up expired balls first
        for (let i = activeBalls.length - 1; i >= 0; i--) {
            const ball = activeBalls[i];
            if (ball.completed && typeof DropGamesCommon !== 'undefined' && DropGamesCommon.calculateDropAlpha) {
                const fade = DropGamesCommon.calculateDropAlpha(ball.landedAtMs, now, sessionState, ball.isLeader, {
                    dimDelay: 3500,
                    dimDuration: 2000,
                    minAlpha: 0.45,
                    lingerTime: 9000,
                    fadeTime: 2500
                });
                ball.renderAlpha = fade.alpha;
                if (fade.shouldRemove) {
                    activeBalls.splice(i, 1);
                }
            } else {
                ball.renderAlpha = 1.0;
            }
        }

        // Maintain leader status dynamically every frame
        updateLeader(m);

        // Determine if green/course should be drawn
        const hasActiveBalls = activeBalls.length > 0;
        const isCourseVisible = sessionState.isActive || hasActiveBalls || now < boardConfig.visibleUntil;

        if (isCourseVisible) {
            if (typeof DropGamesCommon !== 'undefined') {
                if (DropGamesCommon.syncAmbientParticles) {
                    DropGamesCommon.syncAmbientParticles(ambientParticles, {
                        width,
                        height,
                        currentTheme,
                        isVisible: isCourseVisible,
                        maxDensity: 45
                    });
                }
                if (DropGamesCommon.updateAndDrawAmbientParticles) {
                    DropGamesCommon.updateAndDrawAmbientParticles(ctx, ambientParticles, {
                        dtSec: dt,
                        width,
                        height,
                        currentTheme,
                        isVisible: isCourseVisible
                    });
                }
            }

            // Optional fairway backdrop (default 0.0 = completely invisible!)
            if (boardConfig.boardAlpha > 0.005) {
                ctx.save();
                ctx.globalAlpha = Math.max(0, Math.min(1, boardConfig.boardAlpha));
                ctx.fillStyle = '#14532D';
                ctx.fillRect(m.x, m.y, m.w, m.h);
                ctx.restore();
            }

            if (boardConfig.showBoardBorders) {
                ctx.save();
                ctx.strokeStyle = 'rgba(74, 222, 128, 0.35)';
                ctx.lineWidth = 1.5;
                ctx.strokeRect(m.x + 1, m.y + 1, m.w - 2, m.h - 2);
                ctx.restore();
            }

            // Compact Putting Green + Cup + Flagstick
            const greenImg = (currentTheme && currentTheme.golfGreenImageUrl) ? getLoadedImage(currentTheme.golfGreenImageUrl) : null;
            const flagImg = (currentTheme && currentTheme.golfFlagPinImageUrl) ? getLoadedImage(currentTheme.golfFlagPinImageUrl) : null;

            if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.drawGolfGreen) {
                DropGamesCommon.drawGolfGreen(ctx, {
                    cx: m.targetCenterX,
                    cy: m.targetCenterY,
                    greenRadius: m.greenRadius,
                    cupRadius: m.cupRadius,
                    alpha: typeof boardConfig.targetAlpha === 'number' ? boardConfig.targetAlpha : 0.45,
                    theme: currentTheme || {},
                    greenImage: greenImg,
                    flagImage: flagImg,
                    now: now
                });
            }

            drawConditionPill(m);
        } else {
            if (ambientParticles.length > 0) ambientParticles.length = 0;
            if (particles.length > 0) particles.length = 0;
        }

        // Sort balls so leaders & airborne balls render cleanly on top
        if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.sortDropsForDraw) {
            DropGamesCommon.sortDropsForDraw(activeBalls);
        }

        // Draw golf balls & inward nametags in forward order so leader is drawn last (on top)
        const ballCustomImg = (currentTheme && currentTheme.golfBallImageUrl) ? getLoadedImage(currentTheme.golfBallImageUrl) : null;

        for (let i = 0; i < activeBalls.length; i++) {
            const ball = activeBalls[i];
            const alpha = typeof ball.renderAlpha === 'number' ? ball.renderAlpha : 1.0;

            const coords = getBallCanvasCoords(ball, m);
            const avatarImg = ball.decalUrl ? getLoadedImage(ball.decalUrl) : null;

            ctx.save();
            ctx.globalAlpha = Math.max(0, Math.min(1, alpha));
            if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.drawGolfBall) {
                DropGamesCommon.drawGolfBall(ctx, {
                    x: coords.x,
                    y: coords.y,
                    radius: ball.radius || m.ballRadius,
                    heightZ: ball.heightZ || 0,
                    sinkScale: ball.sinkScale || 1.0,
                    rotation: ball.angle || 0,
                    userColor: ball.userColor,
                    avatarImg: avatarImg,
                    ballImg: ballCustomImg,
                    themeId: getSeasonPreset(),
                    displayName: ball.displayName,
                    isLeader: ball.isLeader,
                    isHoleInOne: ball.isHoleInOne
                });
            }

            if (typeof DropGamesCommon !== 'undefined' && DropGamesCommon.drawInwardNametag) {
                const maxStrokes = ball.maxStrokesPerPlayer || boardConfig.maxStrokesPerPlayer || 1;
                const strokeTag = maxStrokes > 1 ? ` [S${ball.strokeNumber || 1}/${maxStrokes}]` : '';
                const label = `${ball.displayName || ball.username || 'Chatter'}${strokeTag}`;
                const liftPx = (ball.heightZ || 0) * Math.max(36, (ball.radius || m.ballRadius) * 3.2);
                DropGamesCommon.drawInwardNametag(ctx, label, coords.x, coords.y - liftPx, ball.radius || m.ballRadius, m.pos, {
                    score: ball.score,
                    completed: ball.completed,
                    isLeader: ball.isLeader,
                    userColor: ball.userColor,
                    fontSize: headerConfig.nameFontSize || 15
                });
            }
            ctx.restore();
        }

        // Particles & Popups
        if (typeof DropGamesCommon !== 'undefined') {
            if (DropGamesCommon.updateAndDrawParticles) {
                DropGamesCommon.updateAndDrawParticles(ctx, particles, { dt, width, height, gravity: 35 });
            }
            if (DropGamesCommon.updateAndDrawPopups) {
                DropGamesCommon.updateAndDrawPopups(ctx, scorePopups, { now, dt, width, height });
            }
            if (DropGamesCommon.drawSessionBanner) {
                const bannerRes = DropGamesCommon.drawSessionBanner(ctx, {
                    now,
                    width,
                    headerConfig,
                    sessionState,
                    itemPluralName: 'Golf Shots',
                    primaryColor: '#22C55E',
                    accentColor: '#FACC15',
                    currentGame: 'golf',
                    activeOtherGames,
                    isOtherGameActive,
                    otherDropsActiveUntilMs,
                    currentBannerY,
                    hasActiveDrops: activeBalls.length > 0
                });
                if (bannerRes && typeof bannerRes.currentBannerY === 'number') {
                    currentBannerY = bannerRes.currentBannerY;
                }
            }
        }

        if (typeof requestAnimationFrame === 'function') {
            requestAnimationFrame(render);
        }
    }

    if (typeof requestAnimationFrame === 'function' && canvas) {
        requestAnimationFrame(render);
    }

    // Export GolfEngine for unit testing
    const GolfEngine = {
        boardConfig: boardConfig,
        sessionState: sessionState,
        activeBalls: activeBalls,
        calculateGolfScore: calculateGolfScore,
        getTrackMetrics: getTrackMetrics,
        handleWsMessage: handleWsMessage,
        handleGolfShot: handleGolfShot,
        stepPhysics: stepPhysics
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = GolfEngine;
    }
    if (typeof global !== 'undefined') {
        global.GolfEngine = GolfEngine;
    }
})(typeof window !== 'undefined' ? window : this);

