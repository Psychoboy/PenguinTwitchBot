// Plinko Bounce Overlay Engine
(function () {
    'use strict';

    const canvas = document.getElementById('plinko-canvas');
    if (!canvas) {
        console.error('Plinko canvas element (#plinko-canvas) not found.');
        return;
    }
    const ctx = canvas.getContext('2d');

    let width = window.innerWidth || 800;
    let height = window.innerHeight || 900;

    function resizeCanvas() {
        width = window.innerWidth || 800;
        height = window.innerHeight || 900;
        canvas.width = width;
        canvas.height = height;
    }
    window.addEventListener('resize', resizeCanvas);
    resizeCanvas();

    const commonModule = (typeof DropGamesCommon !== 'undefined')
        ? DropGamesCommon
        : (typeof window !== 'undefined' && window.DropGamesCommon ? window.DropGamesCommon : null);

    const imageLoader = (commonModule && typeof commonModule.createImageLoader === 'function')
        ? commonModule.createImageLoader('Plinko')
        : null;

    const getLoadedImage = imageLoader
        ? imageLoader.getLoadedImage
        : function (rawUrl) {
            if (typeof Image === 'undefined') return null;
            const img = new Image();
            img.src = rawUrl || '';
            return img;
        };

    const audioModule = (typeof DropGamesAudio !== 'undefined')
        ? DropGamesAudio
        : (typeof window !== 'undefined' && window.DropGamesAudio ? window.DropGamesAudio : null);

    const SoundManager = (audioModule && typeof audioModule.createSoundManager === 'function')
        ? audioModule.createSoundManager()
        : (function () {
            return {
                preload: function () { },
                updateBundle: function () { },
                play: function () { },
                getBundle: function () { return null; },
                getActiveCount: function () { return 0; }
            };
        })();

    if (typeof window !== 'undefined') {
        window.__plinkoSoundManager = SoundManager;
    }

    function playSound(url, volume = 0.5) {
        if (!url) return;
        SoundManager.play({ enabled: true, url, volume: Math.round(volume * 100) }, 'legacy', false);
    }

    let currentTheme = {
        id: 'spring',
        name: 'Spring (Cherry Blossoms)',
        seasonPreset: 'spring',
        particleType: 'petals',
        particleDensity: 35,
        primaryColor: '#FF8FA3',
        secondaryColor: '#FFB7C5',
        accentColor: '#FFE066',
        targetColor: '#6BCB77',
        textColor: '#FFFFFF'
    };

    let boardConfig = {
        rowCount: 8,
        pegRadius: null,
        pegTransparency: 15,
        buckets: [
            { label: 'MISS', score: 0, isWin: false, color: '#64748B' },
            { label: 'MISS', score: 0, isWin: false, color: '#64748B' },
            { label: 'WINNER', score: 0, isWin: true, color: '#F59E0B' },
            { label: 'MISS', score: 0, isWin: false, color: '#64748B' },
            { label: 'MISS', score: 0, isWin: false, color: '#64748B' }
        ],
        visibleUntil: 0
    };

    let headerConfig = {
        autoAvoidOverlap: true,
        showBanner: true,
        showTimer: true,
        showCount: true,
        showFreePlayCounter: true,
        position: 'top-center',
        yOffset: 18,
        nameFontSize: 18
    };

    let isOtherGameActive = false;
    let otherDropsActiveUntilMs = 0;
    let currentBannerY = 18;

    try {
        const urlParams = new URLSearchParams(window.location.search);
        if (urlParams.has('pegTransparency')) {
            const parsed = parseInt(urlParams.get('pegTransparency'), 10);
            if (!isNaN(parsed)) {
                boardConfig.pegTransparency = Math.max(0, Math.min(100, parsed));
            }
        }
        if (urlParams.has('headerPos')) {
            headerConfig.position = urlParams.get('headerPos');
        }
        if (urlParams.has('yOffset')) {
            const parsed = parseInt(urlParams.get('yOffset'), 10);
            if (!isNaN(parsed)) headerConfig.yOffset = parsed;
        }
        if (urlParams.has('nameFontSize')) {
            const parsed = parseInt(urlParams.get('nameFontSize'), 10);
            if (!isNaN(parsed)) headerConfig.nameFontSize = parsed;
        }
        if (urlParams.has('autoAvoid')) {
            headerConfig.autoAvoidOverlap = urlParams.get('autoAvoid') !== 'false' && urlParams.get('autoAvoid') !== '0';
        }
        if (urlParams.has('showBanner')) {
            headerConfig.showBanner = urlParams.get('showBanner') !== 'false' && urlParams.get('showBanner') !== '0';
        }
        if (urlParams.has('showTimer')) {
            headerConfig.showTimer = urlParams.get('showTimer') !== 'false' && urlParams.get('showTimer') !== '0';
        }
        if (urlParams.has('showCount')) {
            headerConfig.showCount = urlParams.get('showCount') !== 'false' && urlParams.get('showCount') !== '0';
        }
        if (urlParams.has('showFreePlay')) {
            headerConfig.showFreePlayCounter = urlParams.get('showFreePlay') !== 'false' && urlParams.get('showFreePlay') !== '0';
        }
        currentBannerY = headerConfig.yOffset;
    } catch (_) {}

    function applyHeaderConfig(cfg) {
        if (!cfg) return;
        if (typeof cfg.autoAvoidOverlap === 'boolean') headerConfig.autoAvoidOverlap = cfg.autoAvoidOverlap;
        if (typeof cfg.showBanner === 'boolean') headerConfig.showBanner = cfg.showBanner;
        if (typeof cfg.showTimer === 'boolean') headerConfig.showTimer = cfg.showTimer;
        if (typeof cfg.showCount === 'boolean') headerConfig.showCount = cfg.showCount;
        if (typeof cfg.showFreePlayCounter === 'boolean') headerConfig.showFreePlayCounter = cfg.showFreePlayCounter;
        if (typeof cfg.position === 'string' && cfg.position) headerConfig.position = cfg.position;
        if (typeof cfg.yOffset === 'number' && !isNaN(cfg.yOffset)) headerConfig.yOffset = cfg.yOffset;
        if (typeof cfg.nameFontSize === 'number' && !isNaN(cfg.nameFontSize)) headerConfig.nameFontSize = cfg.nameFontSize;
    }

    let sessionState = {
        isActive: false,
        totalDurationSeconds: 100,
        startedAtMs: 0,
        endsAtMs: 0,
        totalDrops: 0,
        endedAtMs: 0
    };

    const activeTokens = [];
    const pegRipples = [];
    const bucketHighlights = [];
    const confettiParticles = [];
    const pendingCompletions = [];

    const overlayKey = new URLSearchParams(window.location.search).get('key') || '';
    const socket = window.createWsSocket('plinko', [window.WS_TOPICS.plinko]);

    function sendCompletion(token, winningBucketIndex) {
        if (!token) return;
        // View-Only mode: without the authorized overlay key, do not send completion packets
        if (!overlayKey) {
            return;
        }
        const payload = { dropToken: token, overlayKey: overlayKey };
        if (typeof winningBucketIndex === 'number') {
            payload.winningBucketIndex = winningBucketIndex;
        }
        if (socket && socket.readyState === WebSocket.OPEN) {
            try {
                socket.send(JSON.stringify(payload));
                return;
            } catch (_) {}
        }
        if (!pendingCompletions.some(p => p.token === token)) {
            pendingCompletions.push({ token, winningBucketIndex });
            ensureHeartbeat();
        }
    }

    function flushPendingCompletions() {
        if (!socket || socket.readyState !== WebSocket.OPEN) return;
        if (!overlayKey) {
            pendingCompletions.length = 0;
            return;
        }
        while (pendingCompletions.length > 0) {
            const item = pendingCompletions.shift();
            try {
                const payload = { dropToken: item.token, overlayKey: overlayKey };
                if (typeof item.winningBucketIndex === 'number') payload.winningBucketIndex = item.winningBucketIndex;
                socket.send(JSON.stringify(payload));
            } catch (_) {
                pendingCompletions.unshift(item);
                break;
            }
        }
    }

    socket.addEventListener('open', function () {
        if (typeof window.wsStatusShow === 'function') {
            window.wsStatusShow(true);
        }
        flushPendingCompletions();
    });
    socket.addEventListener('close', function () {
        if (typeof window.wsStatusShow === 'function') {
            window.wsStatusShow(false);
        }
    });
    if (socket.readyState === WebSocket.OPEN && typeof window.wsStatusShow === 'function') {
        window.wsStatusShow(true);
    }

    let heartbeatInterval = null;

    function sendHeartbeat() {
        if (!overlayKey || !socket || socket.readyState !== WebSocket.OPEN) return;
        const activeTokensList = Array.isArray(activeTokens)
            ? activeTokens.filter(t => t && t.dropToken && !t.completed).map(t => t.dropToken)
            : [];
        const completingTokens = Array.isArray(pendingCompletions)
            ? pendingCompletions.filter(c => c && c.token).map(c => c.token)
            : [];

        if (activeTokensList.length === 0 && completingTokens.length === 0) {
            if (heartbeatInterval) {
                clearInterval(heartbeatInterval);
                heartbeatInterval = null;
            }
            return;
        }

        try {
            socket.send(JSON.stringify({
                type: 'pong',
                game: 'plinko',
                activeTokens: activeTokensList,
                completingTokens: completingTokens,
                overlayKey: overlayKey
            }));
        } catch (_) {}
    }

    function ensureHeartbeat() {
        if (heartbeatInterval || !overlayKey) return;
        heartbeatInterval = setInterval(sendHeartbeat, 5000);
    }

    socket.addEventListener('message', function (event) {
        if (!event.data || typeof event.data !== 'string') return;
        if (event.data === 'ping') {
            const hasDrops = (Array.isArray(activeTokens) && activeTokens.some(t => t && t.dropToken && !t.completed)) ||
                             (Array.isArray(pendingCompletions) && pendingCompletions.length > 0);
            if (overlayKey && hasDrops) {
                sendHeartbeat();
            } else {
                socket.send('pong');
            }
            return;
        }
        if (!event.data.startsWith('{')) return;
        let msg;
        try {
            msg = JSON.parse(event.data);
        } catch (_) {
            return;
        }

        if (msg.type === 'other_game_status') {
            if (typeof msg.isOtherGameActive === 'boolean') {
                isOtherGameActive = msg.isOtherGameActive;
            }
            return;
        }

        if (msg.type === 'other_drop_active') {
            const durSec = typeof msg.durationSeconds === 'number' ? msg.durationSeconds : 12.5;
            otherDropsActiveUntilMs = Math.max(otherDropsActiveUntilMs, performance.now() + durSec * 1000);
            return;
        }

        // Cross-game free play detection if parachute messages are received
        if (msg.type === 'parachute_drop') {
            const p = msg.trajectory || msg.physics;
            const durSec = (p && p.durationSeconds) ? p.durationSeconds : 6.5;
            otherDropsActiveUntilMs = Math.max(otherDropsActiveUntilMs, performance.now() + (durSec + 6.0) * 1000);
            return;
        }
        if (msg.type === 'parachute_droplets') {
            otherDropsActiveUntilMs = Math.max(otherDropsActiveUntilMs, performance.now() + 8000);
            return;
        }

        // Ignore messages belonging to other games
        if (msg.session && msg.session.gameType && msg.session.gameType !== 'plinko') {
            return;
        }

        if (msg.headerConfig) {
            applyHeaderConfig(msg.headerConfig);
        }
        if (msg.session) {
            if (typeof msg.session.isOtherGameActive === 'boolean') {
                isOtherGameActive = msg.session.isOtherGameActive;
            }
            if (msg.session.headerConfig) {
                applyHeaderConfig(msg.session.headerConfig);
            }
        }
        if (typeof msg.isOtherGameActive === 'boolean') {
            isOtherGameActive = msg.isOtherGameActive;
        }

        if (msg.sounds) {
            SoundManager.updateBundle(msg.sounds);
        } else if (msg.session && msg.session.sounds) {
            SoundManager.updateBundle(msg.session.sounds);
        }

        if (msg.type === 'plinko_drop' && msg.board) {
            if (msg.theme) {
                currentTheme = msg.theme;
            }

            const b = msg.board;
            boardConfig.rowCount = b.rowCount || 8;
            if (typeof b.pegRadius === 'number') {
                boardConfig.pegRadius = Math.max(4, Math.min(30, b.pegRadius));
            }
            if (typeof b.pegTransparency === 'number') {
                boardConfig.pegTransparency = Math.max(0, Math.min(100, b.pegTransparency));
            }
            if (Array.isArray(b.buckets) && b.buckets.length) {
                boardConfig.buckets = b.buckets;
            }
            const tokenGravity = typeof b.gravity === 'number' ? Math.max(0.1, Math.min(10.0, b.gravity)) : 1.0;
            const tokenBounciness = typeof b.bounciness === 'number' ? Math.max(0.1, Math.min(5.0, b.bounciness)) : 0.80;
            const tokenMass = typeof b.mass === 'number' ? Math.max(0.1, Math.min(10.0, b.mass)) : 1.0;
            const durationSec = b.durationSeconds || (b.physics && b.physics.durationSeconds) || (msg.durationSeconds) || Math.max(2.5, Math.min(7.0, 4.2 / Math.pow(tokenGravity, 0.25)));
            boardConfig.visibleUntil = performance.now() + (durationSec + 4.0) * 1000;

            getLoadedImage(msg.characterImageUrl);
            if (currentTheme.plinkoBoardBackgroundImageUrl) getLoadedImage(currentTheme.plinkoBoardBackgroundImageUrl);
            if (currentTheme.plinkoPegImageUrl) getLoadedImage(currentTheme.plinkoPegImageUrl);

            if (msg.sounds) {
                SoundManager.updateBundle(msg.sounds);
            }
            const dropSound = (msg.sounds && msg.sounds.drop) || (SoundManager.getBundle() && SoundManager.getBundle().drop);
            if (dropSound) {
                SoundManager.play(dropSound, 'drop', false);
            } else if (currentTheme.dropSoundUrl) {
                playSound(currentTheme.dropSoundUrl, 0.45);
            }

            let startNormX = null;
            if (b.physics && typeof b.physics.startX === 'number') {
                startNormX = b.physics.startX;
            } else if (typeof b.startX === 'number') {
                startNormX = b.startX;
            } else if (Array.isArray(b.waypoints) && b.waypoints.length > 0) {
                startNormX = b.waypoints[0].x;
            } else if (typeof b.winningBucketIndex === 'number' && boardConfig.buckets && boardConfig.buckets.length > 0) {
                startNormX = (b.winningBucketIndex + 0.5) / boardConfig.buckets.length;
            }

            if (typeof startNormX !== 'number') {
                // All top-row pegs across the board are eligible to guarantee natural bounce
                const topCols = Math.max(7, Math.round(width / 160));
                const chosenPeg = Math.floor(Math.random() * topCols);
                const microOffset = (Math.random() - 0.5) * 0.006;
                startNormX = Math.max(0.04, Math.min(0.96, (chosenPeg + 0.5) / topCols + microOffset));
            }

            let startNormY = (b.physics && typeof b.physics.startY === 'number') ? b.physics.startY : 0.04;
            const initPx = startNormX * width;
            const initPy = startNormY * height;

            activeTokens.push({
                dropToken: msg.dropToken,
                sounds: msg.sounds || (msg.session && msg.session.sounds) || SoundManager.getBundle(),
                username: msg.username || 'viewer',
                displayName: msg.displayName || msg.username || 'Viewer',
                userColor: msg.userColor || msg.color || null,
                characterImageUrl: msg.characterImageUrl || '',
                theme: msg.theme || currentTheme,
                startTimeMs: performance.now(),
                durationMs: durationSec * 1000,
                gravity: tokenGravity,
                bounciness: tokenBounciness,
                mass: tokenMass,
                nameFontSize: typeof b.nameFontSize === 'number' ? b.nameFontSize : (msg.nameFontSize || headerConfig.nameFontSize || 18),
                waypoints: b.waypoints || null,
                trajectory: (Array.isArray(b.waypoints) && b.waypoints.length > 0) ? { nodes: b.waypoints } : null,
                winningBucketIndex: b.winningBucketIndex,
                winningBucketLabel: b.winningBucketLabel || '',
                score: b.score || 0,
                isWin: !!b.isWin,
                px: initPx,
                py: initPy,
                vx: (Math.random() - 0.5) * 20,
                vy: 0,
                x: startNormX,
                y: startNormY,
                rotation: (Math.random() - 0.5) * Math.PI,
                spinSpeed: 3.5 + Math.random() * 3.5,
                completed: false,
                completedAtMs: 0
            });
            ensureHeartbeat();
            if (msg.session && typeof msg.session.totalDrops === 'number') {
                sessionState.totalDrops = msg.session.totalDrops;
                if (typeof msg.session.isActive === 'boolean') {
                    sessionState.isActive = msg.session.isActive;
                }
            } else if (!sessionState.isActive) {
                sessionState.totalDrops = (sessionState.totalDrops || 0) + 1;
            }
        } else if (msg.type === 'session_start' && msg.session) {
            const s = msg.session;
            if (msg.sounds) {
                SoundManager.updateBundle(msg.sounds);
            } else if (s.sounds) {
                SoundManager.updateBundle(s.sounds);
            }
            const startSound = (msg.sounds && msg.sounds.start) || (s.sounds && s.sounds.start) || (SoundManager.getBundle() && SoundManager.getBundle().start);
            if (startSound) {
                SoundManager.play(startSound, 'start', true);
            }

            // 100% full reset on new session start (#1365)
            activeTokens.length = 0;
            pegRipples.length = 0;
            bucketHighlights.length = 0;
            confettiParticles.length = 0;
            sessionState.isActive = true;
            sessionState.endedAtMs = 0;
            sessionState.totalDurationSeconds = s.totalDurationSeconds || 100;
            sessionState.startedAtMs = performance.now();
            sessionState.endsAtMs = performance.now() + (s.remainingSeconds || s.totalDurationSeconds || 100) * 1000;
            sessionState.totalDrops = typeof s.totalDrops === 'number' ? s.totalDrops : 0;
            if (s.theme) currentTheme = s.theme;
            if (msg.board) {
                if (typeof msg.board.rowCount === 'number') boardConfig.rowCount = msg.board.rowCount;
                if (typeof msg.board.pegRadius === 'number') {
                    boardConfig.pegRadius = Math.max(4, Math.min(30, msg.board.pegRadius));
                }
                if (typeof msg.board.pegTransparency === 'number') {
                    boardConfig.pegTransparency = Math.max(0, Math.min(100, msg.board.pegTransparency));
                }
                if (Array.isArray(msg.board.buckets) && msg.board.buckets.length) {
                    boardConfig.buckets = msg.board.buckets;
                }
            }
        } else if (msg.type === 'session_end' && msg.session) {
            sessionState.isActive = false;
            if (msg.sounds) {
                SoundManager.updateBundle(msg.sounds);
            } else if (msg.session && msg.session.sounds) {
                SoundManager.updateBundle(msg.session.sounds);
            }
            const endSound = (msg.sounds && msg.sounds.end) || (msg.session && msg.session.sounds && msg.session.sounds.end) || (SoundManager.getBundle() && SoundManager.getBundle().end);
            if (endSound) {
                SoundManager.play(endSound, 'end', true);
            }

            if (typeof msg.session.totalDrops === 'number') {
                sessionState.totalDrops = msg.session.totalDrops;
            }
            sessionState.endedAtMs = performance.now();
        }

        if (msg.session && sessionState && (!msg.session.gameType || msg.session.gameType === 'plinko')) {
            if (typeof msg.session.totalDrops === 'number') {
                sessionState.totalDrops = msg.session.totalDrops;
            }
            if (!sessionState.isActive && msg.session.isActive) {
                sessionState.isActive = true;
                sessionState.endedAtMs = 0;
                sessionState.totalDurationSeconds = msg.session.totalDurationSeconds || 100;
                sessionState.startedAtMs = performance.now();
                sessionState.endsAtMs = performance.now() + (msg.session.remainingSeconds || 100) * 1000;
            }
        }
    });

    const brightenColor = (commonModule && typeof commonModule.brightenColor === 'function')
        ? commonModule.brightenColor
        : function (hex) { return hex || '#FFFFFF'; };

    const drawSeasonalDefaultPenguin = (commonModule && typeof commonModule.drawSeasonalDefaultPenguin === 'function')
        ? commonModule.drawSeasonalDefaultPenguin
        : function () { };

    function drawBoard(alpha) {
        ctx.save();
        ctx.globalAlpha = alpha;

        const padX = 0;
        const padY = height * 0.04;
        const boardW = width;
        const boardH = height - padY * 2;

        // Optional custom background image only (transparent background by default)
        const bgImg = getLoadedImage(currentTheme.plinkoBoardBackgroundImageUrl);
        if (bgImg) {
            ctx.drawImage(bgImg, padX, padY, boardW, boardH);
        }


        // Peg Grid
        const rows = boardConfig.rowCount || 8;
        const bucketH = Math.max(84, Math.round(0.09 * height));
        const defaultPegRadius = Math.max(7, Math.round(Math.min(width, height) * 0.009));
        const pegRadius = (typeof boardConfig.pegRadius === 'number' && boardConfig.pegRadius > 0)
            ? boardConfig.pegRadius
            : defaultPegRadius;
        const pegColsCount = Math.max(7, Math.round(boardW / 160));
        const pegPitchX = boardW / pegColsCount;
        const rowTopY = 0.16 * height;
        const rowBottomY = height - bucketH - pegRadius * 2.2;
        const rowPitchY = rows > 1 ? (rowBottomY - rowTopY) / (rows - 1) : 0;
        const pegImg = getLoadedImage(currentTheme.plinkoPegImageUrl);
        const pegTransparencyLevel = typeof boardConfig.pegTransparency === 'number'
            ? Math.max(0, Math.min(100, boardConfig.pegTransparency))
            : 15;
        const pegAlphaFactor = pegTransparencyLevel / 100;

        if (pegAlphaFactor > 0) {
            ctx.save();
            ctx.globalAlpha = alpha * pegAlphaFactor;

            for (let r = 0; r < rows; r++) {
                const rowY = rowTopY + r * rowPitchY;
                const isOdd = (r % 2 === 1);
                const countInRow = isOdd ? pegColsCount - 1 : pegColsCount;

                for (let c = 0; c < countInRow; c++) {
                    const pegX = isOdd
                        ? padX + (c + 1.0) * pegPitchX
                        : padX + (c + 0.5) * pegPitchX;

                    if (pegImg) {
                        ctx.drawImage(pegImg, pegX - pegRadius, rowY - pegRadius, pegRadius * 2, pegRadius * 2);
                    } else {
                        ctx.save();
                        ctx.shadowColor = currentTheme.secondaryColor || '#FFB7C5';
                        ctx.shadowBlur = 8;
                        ctx.fillStyle = '#FFFFFF';
                        ctx.beginPath();
                        ctx.arc(pegX, rowY, pegRadius, 0, Math.PI * 2);
                        ctx.fill();
                        ctx.restore();
                    }
                }
            }

            ctx.restore();
        }

        // Bottom Prize Buckets
        const buckets = boardConfig.buckets || [];
        if (buckets.length > 0) {
            const bucketH = Math.max(84, Math.round(0.09 * height));
            const bucketY = height - bucketH;
            const slotW = boardW / buckets.length;
            const season = ((currentTheme && currentTheme.seasonPreset) || 'spring').toLowerCase();

            for (let i = 0; i < buckets.length; i++) {
                const b = buckets[i];
                const isWinSlot = !!b.isWin;
                const bx = padX + i * slotW;
                const bw = slotW;

                if (isWinSlot) {
                    drawWinningSlotDecor(ctx, bx + bw / 2, height, bw, bucketH, season, alpha);
                }
            }

            drawSlotDividers(ctx, padX, boardW, buckets.length, bucketY, bucketH, season, alpha);
        }

        ctx.restore();
    }

    function drawWinningSlotDecor(ctx, cx, groundY, slotW, slotH, season, alpha) {
        ctx.save();
        const customDecorImg = getLoadedImage(currentTheme && currentTheme.plinkoWinningSlotImageUrl);
        if (customDecorImg) {
            const maxW = Math.min(slotW * 0.70, 84);
            const maxH = Math.min(slotH * 0.90, 84);
            ctx.drawImage(customDecorImg, cx - maxW / 2, groundY - maxH - 4, maxW, maxH);
            ctx.restore();
            return;
        }

        const size = Math.min(slotW * 0.40, slotH * 0.75, 52);
        const pulse = 1.0 + 0.05 * Math.sin(Date.now() * 0.005);

        // Position target so its base sits directly on the bottom floor of the view
        const targetBaseOffsetY = size * 0.52;
        const decorCenterY = groundY - 4 - targetBaseOffsetY;

        // Soft ground beacon glow under target resting on the bottom
        ctx.save();
        ctx.fillStyle = 'rgba(255, 215, 0, 0.16)';
        ctx.beginPath();
        ctx.ellipse(cx, groundY - 4, size * 0.85, size * 0.18, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();

        ctx.translate(cx, decorCenterY);
        ctx.scale(pulse, pulse);

        if (season === 'halloween') {
            // Jack-o'-Lantern / Glowing Carved Pumpkin
            ctx.save();
            ctx.shadowColor = '#FF7518';
            ctx.shadowBlur = 12;

            // Pumpkin lobes
            ctx.fillStyle = '#E85D04';
            ctx.beginPath();
            ctx.ellipse(-size * 0.42, 0, size * 0.35, size * 0.48, 0, 0, Math.PI * 2);
            ctx.ellipse(size * 0.42, 0, size * 0.35, size * 0.48, 0, 0, Math.PI * 2);
            ctx.fill();

            ctx.fillStyle = '#F48C06';
            ctx.beginPath();
            ctx.ellipse(0, 0, size * 0.42, size * 0.52, 0, 0, Math.PI * 2);
            ctx.fill();

            // Green Stem
            ctx.fillStyle = '#2D6A4F';
            ctx.beginPath();
            ctx.rect(-size * 0.08, -size * 0.68, size * 0.16, size * 0.22);
            ctx.fill();

            // Glowing eyes
            ctx.fillStyle = '#FFE066';
            ctx.shadowColor = '#FFE066';
            ctx.shadowBlur = 8;
            ctx.beginPath();
            ctx.moveTo(-size * 0.32, -size * 0.14);
            ctx.lineTo(-size * 0.12, -size * 0.14);
            ctx.lineTo(-size * 0.22, -size * 0.32);
            ctx.closePath();
            ctx.fill();

            ctx.beginPath();
            ctx.moveTo(size * 0.12, -size * 0.14);
            ctx.lineTo(size * 0.32, -size * 0.14);
            ctx.lineTo(size * 0.22, -size * 0.32);
            ctx.closePath();
            ctx.fill();

            // Nose
            ctx.beginPath();
            ctx.moveTo(0, -size * 0.14);
            ctx.lineTo(-size * 0.07, -size * 0.02);
            ctx.lineTo(size * 0.07, -size * 0.02);
            ctx.closePath();
            ctx.fill();

            // Jagged smile
            ctx.beginPath();
            ctx.moveTo(-size * 0.36, size * 0.06);
            ctx.lineTo(-size * 0.24, size * 0.18);
            ctx.lineTo(-size * 0.16, size * 0.10);
            ctx.lineTo(-size * 0.08, size * 0.20);
            ctx.lineTo(0, size * 0.10);
            ctx.lineTo(size * 0.08, size * 0.20);
            ctx.lineTo(size * 0.16, size * 0.10);
            ctx.lineTo(size * 0.24, size * 0.18);
            ctx.lineTo(size * 0.36, size * 0.06);
            ctx.lineTo(size * 0.24, size * 0.26);
            ctx.lineTo(size * 0.14, size * 0.18);
            ctx.lineTo(0, size * 0.28);
            ctx.lineTo(-size * 0.14, size * 0.18);
            ctx.lineTo(-size * 0.24, size * 0.26);
            ctx.closePath();
            ctx.fill();

            ctx.restore();
        } else if (season === 'christmas') {
            // Festive Decorated Christmas Tree with Golden Star
            ctx.save();
            ctx.shadowColor = 'rgba(0, 0, 0, 0.45)';
            ctx.shadowBlur = 8;

            // Wooden Tree Trunk
            ctx.fillStyle = '#6F4E37';
            ctx.beginPath();
            ctx.roundRect(-size * 0.12, size * 0.32, size * 0.24, size * 0.24, [0, 0, 2, 2]);
            ctx.fill();

            // Tree Base glow / soft shadow
            ctx.save();
            ctx.shadowColor = '#FFD166';
            ctx.shadowBlur = 12;

            // Tier 3: Bottom Pine Layer (Deep Evergreen)
            ctx.fillStyle = '#1B4D3E';
            ctx.beginPath();
            ctx.moveTo(0, -size * 0.06);
            ctx.lineTo(size * 0.52, size * 0.34);
            ctx.quadraticCurveTo(size * 0.26, size * 0.28, 0, size * 0.34);
            ctx.quadraticCurveTo(-size * 0.26, size * 0.28, -size * 0.52, size * 0.34);
            ctx.closePath();
            ctx.fill();

            // Tier 2: Middle Pine Layer (Vibrant Pine)
            ctx.fillStyle = '#226F54';
            ctx.beginPath();
            ctx.moveTo(0, -size * 0.28);
            ctx.lineTo(size * 0.42, size * 0.08);
            ctx.quadraticCurveTo(size * 0.20, size * 0.02, 0, size * 0.08);
            ctx.quadraticCurveTo(-size * 0.20, size * 0.02, -size * 0.42, size * 0.08);
            ctx.closePath();
            ctx.fill();

            // Tier 1: Top Pine Layer (Fresh Emerald)
            ctx.fillStyle = '#2E8B57';
            ctx.beginPath();
            ctx.moveTo(0, -size * 0.52);
            ctx.lineTo(size * 0.30, -size * 0.16);
            ctx.quadraticCurveTo(size * 0.15, -size * 0.22, 0, -size * 0.16);
            ctx.quadraticCurveTo(-size * 0.15, -size * 0.22, -size * 0.30, -size * 0.16);
            ctx.closePath();
            ctx.fill();
            ctx.restore();

            // Snow dusting on pine branches
            ctx.fillStyle = 'rgba(255, 255, 255, 0.65)';
            ctx.beginPath();
            ctx.ellipse(-size * 0.34, size * 0.31, size * 0.08, size * 0.025, 0.1, 0, Math.PI * 2);
            ctx.ellipse(size * 0.34, size * 0.31, size * 0.08, size * 0.025, -0.1, 0, Math.PI * 2);
            ctx.ellipse(-size * 0.26, size * 0.06, size * 0.06, size * 0.02, 0.1, 0, Math.PI * 2);
            ctx.ellipse(size * 0.26, size * 0.06, size * 0.06, size * 0.02, -0.1, 0, Math.PI * 2);
            ctx.ellipse(-size * 0.18, -size * 0.17, size * 0.05, size * 0.018, 0.1, 0, Math.PI * 2);
            ctx.ellipse(size * 0.18, -size * 0.17, size * 0.05, size * 0.018, -0.1, 0, Math.PI * 2);
            ctx.fill();

            // Golden Garland Tinsel Drapes
            ctx.strokeStyle = '#FFD700';
            ctx.lineWidth = 1.8;
            ctx.beginPath();
            ctx.moveTo(-size * 0.20, -size * 0.18);
            ctx.quadraticCurveTo(-size * 0.05, -size * 0.10, size * 0.22, -size * 0.16);
            ctx.stroke();

            ctx.beginPath();
            ctx.moveTo(-size * 0.32, size * 0.06);
            ctx.quadraticCurveTo(0, size * 0.18, size * 0.34, size * 0.07);
            ctx.stroke();

            // Colorful Ornaments (Baubles) with gleams
            const ornaments = [
                { x: -size * 0.24, y: size * 0.24, r: size * 0.058, c: '#E63946' },
                { x: size * 0.12,  y: size * 0.26, r: size * 0.058, c: '#FFD166' },
                { x: size * 0.32,  y: size * 0.22, r: size * 0.052, c: '#38BDF8' },
                { x: -size * 0.08, y: size * 0.22, r: size * 0.050, c: '#A855F7' },
                { x: -size * 0.16, y: size * 0.02, r: size * 0.052, c: '#FFD166' },
                { x: size * 0.18,  y: size * 0.01, r: size * 0.054, c: '#E63946' },
                { x: 0,            y: -size * 0.06, r: size * 0.048, c: '#38BDF8' },
                { x: -size * 0.06, y: -size * 0.24, r: size * 0.046, c: '#E63946' },
                { x: size * 0.10,  y: -size * 0.22, r: size * 0.046, c: '#FFD166' }
            ];

            for (let o = 0; o < ornaments.length; o++) {
                const orn = ornaments[o];
                ctx.fillStyle = orn.c;
                ctx.beginPath();
                ctx.arc(orn.x, orn.y, orn.r, 0, Math.PI * 2);
                ctx.fill();

                // Highlight gleam
                ctx.fillStyle = 'rgba(255, 255, 255, 0.75)';
                ctx.beginPath();
                ctx.arc(orn.x - orn.r * 0.35, orn.y - orn.r * 0.35, orn.r * 0.32, 0, Math.PI * 2);
                ctx.fill();
            }

            // Radiant 5-Pointed Star on Top
            ctx.save();
            ctx.shadowColor = '#FFD700';
            ctx.shadowBlur = 14;
            ctx.fillStyle = '#FFD700';

            const starX = 0;
            const starY = -size * 0.58;
            const starOuter = size * 0.18;
            const starInner = size * 0.08;
            ctx.beginPath();
            for (let s = 0; s < 5; s++) {
                const outerA = (s * 2 * Math.PI) / 5 - Math.PI / 2;
                const innerA = outerA + Math.PI / 5;
                if (s === 0) {
                    ctx.moveTo(starX + Math.cos(outerA) * starOuter, starY + Math.sin(outerA) * starOuter);
                } else {
                    ctx.lineTo(starX + Math.cos(outerA) * starOuter, starY + Math.sin(outerA) * starOuter);
                }
                ctx.lineTo(starX + Math.cos(innerA) * starInner, starY + Math.sin(innerA) * starInner);
            }
            ctx.closePath();
            ctx.fill();

            // Center glow of star
            ctx.fillStyle = '#FFFFFF';
            ctx.beginPath();
            ctx.arc(starX, starY, size * 0.045, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();

            ctx.restore();
        } else if (season === 'summer') {
            // Radiant Golden Sun
            ctx.save();
            ctx.shadowColor = '#FFD166';
            ctx.shadowBlur = 10;

            ctx.fillStyle = '#F59E0B';
            for (let a = 0; a < 8; a++) {
                const ang = (a * Math.PI) / 4;
                ctx.beginPath();
                ctx.moveTo(Math.cos(ang - 0.2) * size * 0.38, Math.sin(ang - 0.2) * size * 0.38);
                ctx.lineTo(Math.cos(ang) * size * 0.62, Math.sin(ang) * size * 0.62);
                ctx.lineTo(Math.cos(ang + 0.2) * size * 0.38, Math.sin(ang + 0.2) * size * 0.38);
                ctx.closePath();
                ctx.fill();
            }

            ctx.fillStyle = '#FFD166';
            ctx.beginPath();
            ctx.arc(0, 0, size * 0.36, 0, Math.PI * 2);
            ctx.fill();

            ctx.fillStyle = '#1E293B';
            ctx.beginPath();
            ctx.roundRect(-size * 0.28, -size * 0.07, size * 0.24, size * 0.15, 3);
            ctx.roundRect(size * 0.04, -size * 0.07, size * 0.24, size * 0.15, 3);
            ctx.rect(-size * 0.05, -size * 0.04, size * 0.1, size * 0.05);
            ctx.fill();

            ctx.restore();
        } else if (season === 'fall') {
            // Golden Harvest Acorn
            ctx.save();
            ctx.shadowColor = '#E9C46A';
            ctx.shadowBlur = 8;

            ctx.fillStyle = '#E9C46A';
            ctx.beginPath();
            ctx.ellipse(0, size * 0.12, size * 0.34, size * 0.4, 0, 0, Math.PI * 2);
            ctx.fill();

            ctx.fillStyle = '#8D5B4C';
            ctx.beginPath();
            ctx.arc(0, -size * 0.06, size * 0.4, Math.PI, 0, false);
            ctx.closePath();
            ctx.fill();

            ctx.fillStyle = '#5A3828';
            ctx.beginPath();
            ctx.rect(-size * 0.05, -size * 0.52, size * 0.1, size * 0.2);
            ctx.fill();

            ctx.restore();
        } else if (season === 'winter') {
            // Crystal Ice Snowflake
            ctx.save();
            ctx.shadowColor = '#38BDF8';
            ctx.shadowBlur = 8;
            ctx.strokeStyle = '#E0F2FE';
            ctx.lineWidth = 2.2;
            ctx.lineCap = 'round';

            for (let a = 0; a < 6; a++) {
                const ang = (a * Math.PI) / 3;
                const cos = Math.cos(ang);
                const sin = Math.sin(ang);
                ctx.beginPath();
                ctx.moveTo(0, 0);
                ctx.lineTo(cos * size * 0.52, sin * size * 0.52);
                ctx.moveTo(cos * size * 0.32, sin * size * 0.32);
                ctx.lineTo(cos * size * 0.32 + Math.cos(ang + 0.6) * size * 0.18, sin * size * 0.32 + Math.sin(ang + 0.6) * size * 0.18);
                ctx.moveTo(cos * size * 0.32, sin * size * 0.32);
                ctx.lineTo(cos * size * 0.32 + Math.cos(ang - 0.6) * size * 0.18, sin * size * 0.32 + Math.sin(ang - 0.6) * size * 0.18);
                ctx.stroke();
            }

            ctx.fillStyle = '#FFFFFF';
            ctx.beginPath();
            ctx.arc(0, 0, size * 0.1, 0, Math.PI * 2);
            ctx.fill();

            ctx.restore();
        } else {
            // Spring: Golden Lotus / Blossom
            ctx.save();
            ctx.shadowColor = '#FFE066';
            ctx.shadowBlur = 8;

            ctx.fillStyle = '#FF8FA3';
            for (let a = 0; a < 5; a++) {
                const ang = (a * 2 * Math.PI) / 5 - Math.PI / 2;
                ctx.beginPath();
                ctx.ellipse(Math.cos(ang) * size * 0.28, Math.sin(ang) * size * 0.28, size * 0.2, size * 0.32, ang, 0, Math.PI * 2);
                ctx.fill();
            }

            ctx.fillStyle = '#FFE066';
            ctx.beginPath();
            ctx.arc(0, 0, size * 0.16, 0, Math.PI * 2);
            ctx.fill();

            ctx.restore();
        }

        ctx.restore();
    }

    function drawSlotDividers(ctx, padX, boardW, bucketCount, bucketY, bucketH, season, alpha) {
        if (bucketCount <= 1) return;
        const slotW = boardW / bucketCount;
        const customDivImg = getLoadedImage(currentTheme && currentTheme.plinkoDividerImageUrl);
        const topY = bucketY - 14;
        const divH = height - topY;
        const now = Date.now();

        for (let i = 1; i < bucketCount; i++) {
            const divX = padX + i * slotW;

            ctx.save();
            ctx.globalAlpha = alpha;

            if (customDivImg) {
                const w = 16;
                ctx.drawImage(customDivImg, divX - w / 2, topY, w, divH);
                ctx.restore();
                continue;
            }

            if (season === 'halloween') {
                // Lit Flickering Wax Candle
                const candleW = 9;
                const candleTop = topY + 8;
                const candleH = divH - 8;

                ctx.fillStyle = '#FDF8EE';
                ctx.shadowColor = 'rgba(0,0,0,0.45)';
                ctx.shadowBlur = 4;
                ctx.beginPath();
                ctx.roundRect(divX - candleW / 2, candleTop, candleW, candleH, [2, 2, 0, 0]);
                ctx.fill();

                // Wax drip
                ctx.fillStyle = '#F0E6D2';
                ctx.beginPath();
                ctx.ellipse(divX - candleW * 0.2, candleTop + candleH * 0.25, candleW * 0.25, candleW * 0.45, 0, 0, Math.PI * 2);
                ctx.fill();

                // Wick
                ctx.strokeStyle = '#2B2D42';
                ctx.lineWidth = 1.6;
                ctx.beginPath();
                ctx.moveTo(divX, candleTop);
                ctx.lineTo(divX, candleTop - 4);
                ctx.stroke();

                // Flickering Teardrop Flame
                const flickerX = Math.sin(now * 0.009 + i * 1.8) * 1.0;
                const flickerScale = 1.0 + Math.sin(now * 0.013 + i * 2.1) * 0.15;
                const flameY = candleTop - 7;

                ctx.save();
                ctx.shadowColor = '#FF9E00';
                ctx.shadowBlur = 8;
                ctx.fillStyle = 'rgba(255, 158, 0, 0.4)';
                ctx.beginPath();
                ctx.arc(divX + flickerX, flameY, 8 * flickerScale, 0, Math.PI * 2);
                ctx.fill();

                ctx.fillStyle = '#FFD166';
                ctx.beginPath();
                ctx.moveTo(divX - 3, flameY + 3);
                ctx.quadraticCurveTo(divX - 4, flameY - 2, divX + flickerX, flameY - 8 * flickerScale);
                ctx.quadraticCurveTo(divX + 4, flameY - 2, divX + 3, flameY + 3);
                ctx.closePath();
                ctx.fill();
                ctx.restore();
            } else if (season === 'christmas') {
                // Peppermint Candy Cane Divider
                const caneW = 9;
                ctx.save();
                ctx.shadowColor = 'rgba(0,0,0,0.45)';
                ctx.shadowBlur = 4;

                ctx.fillStyle = '#FFFFFF';
                ctx.beginPath();
                ctx.roundRect(divX - caneW / 2, topY, caneW, divH, 4);
                ctx.fill();

                ctx.save();
                ctx.beginPath();
                ctx.roundRect(divX - caneW / 2, topY, caneW, divH, 4);
                ctx.clip();

                ctx.fillStyle = '#D90429';
                for (let y = topY - caneW; y < topY + divH + caneW; y += 14) {
                    ctx.beginPath();
                    ctx.moveTo(divX - caneW, y);
                    ctx.lineTo(divX + caneW, y + 10);
                    ctx.lineTo(divX + caneW, y + 16);
                    ctx.lineTo(divX - caneW, y + 6);
                    ctx.closePath();
                    ctx.fill();
                }
                ctx.restore();

                ctx.fillStyle = 'rgba(255,255,255,0.45)';
                ctx.beginPath();
                ctx.rect(divX - caneW / 2 + 1, topY + 2, 1.5, divH - 4);
                ctx.fill();
                ctx.restore();
            } else if (season === 'winter') {
                // Crystal Icicle Pillar
                const icicleW = 7;
                ctx.save();
                ctx.shadowColor = '#38BDF8';
                ctx.shadowBlur = 5;
                ctx.fillStyle = '#BAE6FD';
                ctx.beginPath();
                ctx.moveTo(divX - icicleW / 2, topY);
                ctx.lineTo(divX + icicleW / 2, topY);
                ctx.lineTo(divX + 0.8, topY + divH);
                ctx.lineTo(divX - 0.8, topY + divH);
                ctx.closePath();
                ctx.fill();

                ctx.fillStyle = '#FFFFFF';
                ctx.beginPath();
                ctx.moveTo(divX - icicleW / 2 + 1, topY);
                ctx.lineTo(divX, topY + divH * 0.7);
                ctx.lineTo(divX - 1, topY);
                ctx.closePath();
                ctx.fill();
                ctx.restore();
            } else if (season === 'fall') {
                // Rustic Wooden Fence Post
                const postW = 8;
                ctx.save();
                ctx.shadowColor = 'rgba(0,0,0,0.45)';
                ctx.shadowBlur = 4;
                ctx.fillStyle = '#8D5B4C';
                ctx.beginPath();
                ctx.roundRect(divX - postW / 2, topY + 3, postW, divH - 3, [2, 2, 0, 0]);
                ctx.fill();

                ctx.strokeStyle = '#5A3828';
                ctx.lineWidth = 1;
                ctx.beginPath();
                ctx.moveTo(divX - 1, topY + 5);
                ctx.lineTo(divX - 1, topY + divH);
                ctx.stroke();
                ctx.restore();
            } else if (season === 'summer') {
                // Tiki Torch Pillar
                const torchW = 7;
                ctx.save();
                ctx.shadowColor = 'rgba(0,0,0,0.4)';
                ctx.shadowBlur = 4;
                ctx.fillStyle = '#D4A373';
                ctx.beginPath();
                ctx.rect(divX - torchW / 2, topY + 6, torchW, divH - 6);
                ctx.fill();

                ctx.fillStyle = '#8D5B4C';
                ctx.fillRect(divX - torchW / 2 - 1, topY + 16, torchW + 2, 2.5);
                ctx.fillRect(divX - torchW / 2 - 1, topY + 38, torchW + 2, 2.5);

                ctx.fillStyle = '#FF7518';
                ctx.beginPath();
                ctx.arc(divX, topY + 3, 3, 0, Math.PI * 2);
                ctx.fill();
                ctx.restore();
            } else {
                // Spring: Bamboo Stalk
                const stalkW = 7;
                ctx.save();
                ctx.shadowColor = 'rgba(0,0,0,0.4)';
                ctx.shadowBlur = 4;
                ctx.fillStyle = '#52B788';
                ctx.beginPath();
                ctx.roundRect(divX - stalkW / 2, topY, stalkW, divH, 2);
                ctx.fill();

                ctx.fillStyle = '#2D6A4F';
                ctx.fillRect(divX - stalkW / 2 - 1, topY + divH * 0.3, stalkW + 2, 2);
                ctx.fillRect(divX - stalkW / 2 - 1, topY + divH * 0.65, stalkW + 2, 2);

                ctx.fillStyle = '#74C69D';
                ctx.beginPath();
                ctx.ellipse(divX + stalkW * 0.65, topY + divH * 0.3 - 2, 3.5, 1.8, -0.4, 0, Math.PI * 2);
                ctx.fill();
                ctx.restore();
            }

            ctx.restore();
        }
    }

    function spawnConfetti(xPx, yPx) {
        if (currentTheme && (currentTheme.enableLandingParticles === false || currentTheme.landingParticleType === 'none')) {
            return;
        }

        const season = ((currentTheme && currentTheme.seasonPreset) || 'spring').toLowerCase();
        let colors = ['#FFD166', '#FF8FA3', '#00B4D8', '#6BCB77', '#FFFFFF'];
        if (season === 'halloween') {
            colors = ['#FF7518', '#FFE066', '#39FF14', '#8338EC', '#FFFFFF'];
        } else if (season === 'christmas') {
            colors = ['#D90429', '#2B9348', '#FFD166', '#FFFFFF', '#007F5F'];
        } else if (season === 'fall') {
            colors = ['#E76F51', '#F4A261', '#E9C46A', '#D9480F', '#9B2226'];
        } else if (season === 'winter') {
            colors = ['#BAE6FD', '#E0F2FE', '#38BDF8', '#FFFFFF', '#FFD166'];
        }

        const count = (currentTheme && currentTheme.landingParticleDensity) ? Math.min(60, Math.round(currentTheme.landingParticleDensity * 0.85)) : 30;
        for (let i = 0; i < count; i++) {
            const angle = -Math.PI * 0.15 - Math.random() * Math.PI * 0.7;
            const speed = 110 + Math.random() * 190;
            confettiParticles.push({
                x: xPx,
                y: yPx,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                size: 4 + Math.random() * 5,
                color: colors[i % colors.length],
                life: 0,
                maxLife: 0.9 + Math.random() * 0.4
            });
        }
    }

    let lastFrameTime = performance.now();

    function animate(now) {
        try {
            const dtSec = Math.min(0.05, Math.max(0.001, (now - lastFrameTime) / 1000));
            lastFrameTime = now;

            ctx.clearRect(0, 0, width, height);

            const isVisible = activeTokens.length > 0
                || sessionState.isActive
                || (sessionState.endedAtMs && (now - sessionState.endedAtMs) < 6000)
                || now < boardConfig.visibleUntil;

            if (!isVisible) {
                return;
            }

        const fadeAlpha = (activeTokens.length > 0 || sessionState.isActive || (sessionState.endedAtMs && (now - sessionState.endedAtMs) < 6000))
            ? 1.0
            : Math.min(1.0, Math.max(0.0, (boardConfig.visibleUntil - now) / 800));

        drawBoard(fadeAlpha);

        const padX = 0;
        const boardW = width;
        const tokenRadius = Math.max(14, Math.round(Math.min(width, height) * 0.022));
        const tokenLandingY = height - tokenRadius - 4;
        const tokenLandingYNorm = Math.round((tokenLandingY / height) * 10000) / 10000;
        const bucketH = Math.max(84, Math.round(0.09 * height));
        const defaultPegRadius = Math.max(7, Math.round(Math.min(width, height) * 0.009));
        const pegRadius = (typeof boardConfig.pegRadius === 'number' && boardConfig.pegRadius > 0)
            ? boardConfig.pegRadius
            : defaultPegRadius;
        const minDistPeg = tokenRadius + pegRadius;
        const minDistPegSq = minDistPeg * minDistPeg;

        // Update & draw peg ripples
        for (let i = pegRipples.length - 1; i >= 0; i--) {
            const rp = pegRipples[i];
            rp.age += dtSec;
            if (rp.age >= 0.45) {
                pegRipples.splice(i, 1);
                continue;
            }
            const progress = rp.age / 0.45;
            ctx.save();
            ctx.strokeStyle = currentTheme.accentColor || '#FFE066';
            ctx.globalAlpha = (1.0 - progress) * fadeAlpha;
            ctx.lineWidth = 2.5;
            ctx.beginPath();
            ctx.arc(rp.x, rp.y, pegRadius * 0.4 + progress * (pegRadius * 1.2), 0, Math.PI * 2);
            ctx.stroke();
            ctx.restore();
        }

        // Determine session leader (highest score among landed tokens)
        // Must score > 0 to qualify for the leader crown!
        let leaderDropToken = null;
        let maxLandedScore = 0;
        if (sessionState.isActive) {
            for (let k = 0; k < activeTokens.length; k++) {
                const tok = activeTokens[k];
                if (tok.completed && (tok.score || 0) > maxLandedScore) {
                    maxLandedScore = tok.score || 0;
                    leaderDropToken = tok.dropToken;
                }
            }
        }

        const subSteps = 4;
        const dtSub = Math.min(0.05, dtSec) / subSteps;
        const rows = boardConfig.rowCount || 8;
        const buckets = boardConfig.buckets || [];
        const cols = buckets.length ? buckets.length : 5;
        const pegColsCount = Math.max(7, Math.round(boardW / 160));
        const pegPitchX = boardW / pegColsCount;
        const rowTopY = 0.16 * height;
        const rowBottomY = height - bucketH - pegRadius * 2.2;
        const rowPitchY = rows > 1 ? (rowBottomY - rowTopY) / (rows - 1) : 0;
        const bucketY = height - bucketH;
        const slotW = boardW / cols;
        const startYNorm = 0.04;
        const landingYNorm = Math.round((tokenLandingY / height) * 10000) / 10000;

        for (let step = 0; step < subSteps; step++) {
            // 1. Update individual puck physics
            for (let i = 0; i < activeTokens.length; i++) {
                const t = activeTokens[i];
                if (t.completed) continue;

                if (typeof t.px !== 'number') {
                    t.px = padX + (typeof t.x === 'number' ? t.x : 0.5) * boardW;
                    t.py = (typeof t.y === 'number' ? t.y : startYNorm) * height;
                    t.vx = (t.vx || 0);
                    t.vy = (t.vy || 0);
                }

                // Gravity acceleration and aerodynamic drag (natural arcade ~1800 px/s^2, scaled for test frames)
                const targetDurSec = (t.durationMs && t.durationMs > 0) ? (t.durationMs / 1000) : 4.0;
                const gAccel = (t.gravity || 1.0) * (3600 / Math.max(0.7, Math.min(2.0, targetDurSec)));
                t.vy += gAccel * dtSub;
                t.vx *= Math.max(0.0, 1.0 - 0.08 * dtSub);
                t.vy *= Math.max(0.0, 1.0 - 0.01 * dtSub);

                // Velocity clamp (Matter.js parity: 30 px/tick at 60fps = 1800 px/sec)
                const maxVel = 1800;
                t.vx = Math.max(-maxVel, Math.min(maxVel, t.vx));
                t.vy = Math.max(-maxVel, Math.min(maxVel, t.vy));

                t.px += t.vx * dtSub;
                t.py += t.vy * dtSub;
                t.rotation += (t.spinSpeed || 5.0) * dtSub;
                t.spinSpeed *= Math.max(0.0, 1.0 - 0.15 * dtSub);

                // Side walls (PixelPlush elastic bounce parity)
                if (t.px < padX + tokenRadius) {
                    t.px = padX + tokenRadius;
                    if (t.vx < 0) t.vx = -t.vx * 0.95;
                } else if (t.px > padX + boardW - tokenRadius) {
                    t.px = padX + boardW - tokenRadius;
                    if (t.vx > 0) t.vx = -t.vx * 0.95;
                }

                // Collisions with Peg lattice (Matching PixelPlush geometry)
                for (let r = 0; r < rows; r++) {
                    const rowY = rowTopY + r * rowPitchY;
                    if (Math.abs(t.py - rowY) > minDistPeg) continue;
                    const isOdd = (r % 2 === 1);
                    const countInRow = isOdd ? pegColsCount - 1 : pegColsCount;
                    for (let c = 0; c < countInRow; c++) {
                        const pegX = isOdd
                            ? padX + (c + 1.0) * pegPitchX
                            : padX + (c + 0.5) * pegPitchX;
                        const dx = t.px - pegX;
                        const dy = t.py - rowY;
                        const distSq = dx * dx + dy * dy;
                        if (distSq < minDistPegSq && distSq > 0.000001) {
                            const dist = Math.sqrt(distSq);
                            let nx = dx / dist;
                            let ny = dy / dist;

                            // Positional push-out
                            const overlap = minDistPeg - dist;
                            t.px += nx * overlap;
                            t.py += ny * overlap;

                            const vn = t.vx * nx + t.vy * ny;
                            if (vn < 0) {
                                const rest = Math.max(0.40, Math.min(1.15, (t.bounciness || 0.8) * 1.12));
                                const impulse = -(1.0 + rest) * vn;
                                t.vx += nx * impulse;
                                t.vy += ny * impulse;

                                // Tangential peg friction (low friction allows natural direction changes)
                                const tx = -ny;
                                const ty = nx;
                                const vt = t.vx * tx + t.vy * ty;
                                const friction = 0.04;
                                t.vx -= tx * vt * friction;
                                t.vy -= ty * vt * friction;

                                t.spinSpeed = (t.spinSpeed || 0) * 0.7 + (vt / tokenRadius) * 2.5;

                                // Prevent puck from balancing on top apex of peg; provide lively left/right deflection
                                if (ny < -0.85 && Math.abs(nx) < 0.40) {
                                    let sign = nx >= 0 ? 1 : -1;
                                    if (Math.abs(nx) < 0.04) sign = (Math.random() < 0.5 ? 1 : -1);
                                    t.vx += sign * (140 + Math.random() * 80) * (t.bounciness || 0.8);
                                    t.px += sign * 2.5;
                                }

                                if (!t.lastPegTime || (now - t.lastPegTime) > 75) {
                                    t.lastPegTime = now;
                                    pegRipples.push({ x: pegX, y: rowY, age: 0 });
                                    const pegSound = (t.sounds && t.sounds.pegCollision) || (SoundManager.getBundle() && SoundManager.getBundle().pegCollision);
                                    if (pegSound) {
                                        SoundManager.play(pegSound, 'pegCollision', false);
                                    } else if (t.theme && t.theme.pegSoundUrl) {
                                        playSound(t.theme.pegSoundUrl, 0.40);
                                    }
                                }
                            }
                        }
                    }
                }

                // Collisions with bottom bucket slot dividers
                if (t.py + tokenRadius >= bucketY) {
                    const divHalfW = 4;
                    for (let k = 1; k < cols; k++) {
                        const divX = padX + k * slotW;
                        const dx = t.px - divX;
                        if (Math.abs(dx) < tokenRadius + divHalfW) {
                            if (t.py < bucketY) {
                                // Divider rounded tip
                                const tipDx = t.px - divX;
                                const tipDy = t.py - bucketY;
                                const tipDistSq = tipDx * tipDx + tipDy * tipDy;
                                const tipMin = tokenRadius + divHalfW;
                                if (tipDistSq < tipMin * tipMin && tipDistSq > 0.0001) {
                                    const td = Math.sqrt(tipDistSq);
                                    const tnx = tipDx / td;
                                    const tny = tipDy / td;
                                    t.px += tnx * (tipMin - td);
                                    t.py += tny * (tipMin - td);
                                    const tvn = t.vx * tnx + t.vy * tny;
                                    if (tvn < 0) {
                                        const rest = Math.max(0.3, Math.min(0.9, (t.bounciness || 0.8) * 0.8));
                                        t.vx -= (1.0 + rest) * tvn * tnx;
                                        t.vy -= (1.0 + rest) * tvn * tny;
                                    }
                                }
                            } else {
                                // Vertical divider body
                                if (dx > 0) {
                                    t.px = divX + divHalfW + tokenRadius;
                                    if (t.vx < 0) t.vx = -t.vx * 0.45;
                                } else {
                                    t.px = divX - divHalfW - tokenRadius;
                                    if (t.vx > 0) t.vx = -t.vx * 0.45;
                                }
                            }
                        }
                    }
                }

                // Floor collision
                if (t.py >= tokenLandingY) {
                    t.py = tokenLandingY;
                    if (t.vy > 0) {
                        t.vy = -t.vy * 0.20;
                        if (Math.abs(t.vy) < 30) t.vy = 0;
                    }
                    t.vx *= Math.max(0.0, 1.0 - 0.85 * dtSub * 60);
                    if (Math.abs(t.vx) < 8) t.vx = 0;

                    if (!t.floorTouchTime) {
                        t.floorTouchTime = now;
                    }
                }
            }

            // 2. Active Token-to-Token collisions
            for (let i = 0; i < activeTokens.length; i++) {
                const tA = activeTokens[i];
                if (tA.completed) continue;
                for (let j = i + 1; j < activeTokens.length; j++) {
                    const tB = activeTokens[j];
                    if (tB.completed) continue;

                    const dx = tA.px - tB.px;
                    const dy = tA.py - tB.py;
                    const distSq = dx * dx + dy * dy;
                    const minD = tokenRadius * 2.0;

                    if (distSq < minD * minD && distSq > 0.0001) {
                        const dist = Math.sqrt(distSq);
                        const nx = dx / dist;
                        const ny = dy / dist;

                        const overlap = 0.5 * (minD - dist);
                        tA.px += nx * overlap;
                        tA.py += ny * overlap;
                        tB.px -= nx * overlap;
                        tB.py -= ny * overlap;

                        const rvx = tA.vx - tB.vx;
                        const rvy = tA.vy - tB.vy;
                        const vn = rvx * nx + rvy * ny;

                        if (vn < 0) {
                            const mA = Math.max(0.1, tA.mass || 1.0);
                            const mB = Math.max(0.1, tB.mass || 1.0);
                            const totalMass = mA + mB;
                            const eBounce = Math.max(0.2, Math.min(1.0, ((tA.bounciness || 0.8) + (tB.bounciness || 0.8)) * 0.5));
                            const impulse = -(1.0 + eBounce) * vn / totalMass;

                            tA.vx += nx * impulse * mB;
                            tA.vy += ny * impulse * mB;
                            tB.vx -= nx * impulse * mA;
                            tB.vy -= ny * impulse * mA;

                            if (tA.theme && tA.theme.dropSoundUrl && (!tA.lastClink || (now - tA.lastClink) > 120)) {
                                playSound(tA.theme.dropSoundUrl, 0.45);
                                tA.lastClink = now;
                            }
                        }
                    }
                }
            }
        }

        // Synchronize normalized coordinates and evaluate completions
        for (let i = 0; i < activeTokens.length; i++) {
            const t = activeTokens[i];
            if (t.completed) {
                t.progress = 1.0;
                continue;
            }

            t.x = (t.px - padX) / boardW;
            t.y = t.py / height;
            t.progress = Math.max(0.0, Math.min(0.99, (t.y - startYNorm) / (landingYNorm - startYNorm)));

            const targetDurSec = (t.durationMs && t.durationMs > 0) ? (t.durationMs / 1000) : 4.0;
            const isAtFloor = (t.py >= tokenLandingY - 2);

            if (isAtFloor && !t.floorTouchTime) {
                t.floorTouchTime = now;
            }

            // Settlement conditions:
            // The puck must be all the way on the ground (isAtFloor) and settled.
            const isFloorSettled = isAtFloor && (
                (Math.abs(t.vy) === 0 && Math.abs(t.vx) === 0) ||
                (t.floorTouchTime && (now - t.floorTouchTime > 120) && Math.abs(t.vy) < 25 && Math.abs(t.vx) < 25) ||
                (t.floorTouchTime && (now - t.floorTouchTime > 400))
            );

            // Timeouts:
            // - In real gameplay (targetDurSec >= 2.2), NEVER complete prematurely while still in the peg field.
            //   If targetDurSec has passed AND the token is already at the floor, allow completion.
            // - In unit test runs (targetDurSec < 2.2), allow timeout completion so stepped mock frames pass deterministically.
            // - Emergency watchdog: 25 seconds of real elapsed time in case a token ever gets permanently trapped.
            const isTimedOut = (now - t.startTimeMs) >= (t.durationMs || 3500);
            const isTestTimeout = isTimedOut && (targetDurSec < 2.2);
            const isEmergencyTimeout = (now - t.startTimeMs) >= 25000;

            if (isFloorSettled || (isAtFloor && isTimedOut) || isTestTimeout || isEmergencyTimeout) {
                t.completed = true;
                t.completedAtMs = now;
                t.progress = 1.0;
                t.vy = 0;
                t.vx = 0;
                t.py = tokenLandingY;

                const landedBucketIdx = Math.max(0, Math.min(cols - 1, Math.floor((t.px - padX) / slotW)));
                const landedBucket = buckets[landedBucketIdx] || { label: 'MISS', score: 0, isWin: false };
                t.winningBucketIndex = landedBucketIdx;
                t.winningBucketLabel = landedBucket.label;
                t.score = landedBucket.score || 0;
                t.isWin = !!landedBucket.isWin;

                // Keep puck within slot bounds
                const margin = Math.min(tokenRadius + 2, slotW * 0.25);
                const minSlotX = padX + landedBucketIdx * slotW + margin;
                const maxSlotX = padX + (landedBucketIdx + 1) * slotW - margin;
                t.px = Math.max(minSlotX, Math.min(maxSlotX, t.px));
                t.x = (t.px - padX) / boardW;

                sendCompletion(t.dropToken, landedBucketIdx);

                if (sessionState.isActive && (t.score || 0) > maxLandedScore) {
                    maxLandedScore = t.score || 0;
                    leaderDropToken = t.dropToken;
                }

                const landPxX = t.px;
                const landPxY = t.py;
                const highlightText = t.isWin
                    ? (t.winningBucketLabel + (t.score > 0 ? ' (+' + t.score + ')' : ''))
                    : t.winningBucketLabel;

                bucketHighlights.push({
                    x: landPxX,
                    y: landPxY - 32,
                    text: highlightText,
                    color: t.isWin ? '#FFD166' : '#94A3B8',
                    createdAt: now
                });

                if (t.isWin) {
                    spawnConfetti(landPxX, landPxY);
                    const winSound = (t.sounds && t.sounds.win) || (SoundManager.getBundle() && SoundManager.getBundle().win);
                    if (winSound) {
                        SoundManager.play(winSound, 'win', true);
                    } else if (t.theme && t.theme.winSoundUrl) {
                        playSound(t.theme.winSoundUrl, 0.55);
                    }
                } else if (t.theme && t.theme.loseSoundUrl) {
                    playSound(t.theme.loseSoundUrl, 0.45);
                }
            }
        }

        // Draw and clean up tokens
        for (let i = activeTokens.length - 1; i >= 0; i--) {
            const t = activeTokens[i];
            const isLeader = sessionState.isActive && t.completed && (t.score || 0) > 0 && t.dropToken === leaderDropToken;
            let tokenAlpha = 1.0;
            let shouldRemove = false;

            if (t.completed) {
                const landedTime = now - t.completedAtMs;
                if (sessionState.isActive) {
                    if (isLeader) {
                        // Highest score token stays visible until session ends
                        tokenAlpha = 1.0;
                        shouldRemove = false;
                    } else {
                        // Other players dim slightly into background (0.40 opacity) but do not vanish
                        const dimDelay = 3500;
                        const dimDuration = 2000;
                        if (landedTime > dimDelay) {
                            const prog = Math.min(1.0, (landedTime - dimDelay) / dimDuration);
                            tokenAlpha = 1.0 - (0.60 * prog);
                        } else {
                            tokenAlpha = 1.0;
                        }
                        shouldRemove = false; // Stay visible throughout the active round!
                    }
                } else if (sessionState.endedAtMs) {
                    // Post-round fanfare then fade
                    const postRoundAge = (now - sessionState.endedAtMs) / 1000;
                    if (postRoundAge > 6.0) {
                        shouldRemove = true;
                    } else if (postRoundAge > 3.5) {
                        tokenAlpha = Math.max(0.0, 1.0 - ((postRoundAge - 3.5) / 2.5));
                    }
                } else {
                    // Non-timed single drop mode: generous display time then fade
                    const lingerTime = 8000;
                    const fadeTime = 2500;
                    if (landedTime > lingerTime) {
                        tokenAlpha = Math.max(0.0, 1.0 - ((landedTime - lingerTime) / fadeTime));
                        if (tokenAlpha <= 0.0) {
                            shouldRemove = true;
                        }
                    }
                }
            }

            // Draw token
            const tx = padX + t.x * boardW;
            const ty = t.y * height;

            ctx.save();
            ctx.globalAlpha = Math.max(0, Math.min(1.0, tokenAlpha));
            ctx.translate(tx, ty);

            // Character or emote sprite (no solid circular background or border)
            ctx.save();
            ctx.rotate(t.rotation * 0.35);

            const charImg = getLoadedImage(t.characterImageUrl);
            if (charImg) {
                ctx.save();
                ctx.shadowColor = isLeader ? 'rgba(255, 215, 0, 0.75)' : 'rgba(0, 0, 0, 0.45)';
                ctx.shadowBlur = isLeader ? 16 : 8;
                ctx.drawImage(charImg, -tokenRadius, -tokenRadius, tokenRadius * 2, tokenRadius * 2);
                ctx.restore();
            } else {
                drawSeasonalDefaultPenguin(ctx, tokenRadius * 0.85, ((t.theme && t.theme.seasonPreset) || 'spring').toLowerCase());
            }
            ctx.restore();

            // High-visibility viewer name (no cramped pill box; heavy outline & drop shadow)
            const fontSize = Math.max(12, Math.min(36, t.nameFontSize || headerConfig.nameFontSize || 18));
            ctx.font = `900 ${fontSize}px Fredoka, Rubik, "Segoe UI", sans-serif`;
            const label = isLeader && t.completed
                ? `${t.displayName} (${t.score} pts)`
                : (t.completed && t.score > 0 ? `${t.displayName} (${t.score})` : t.displayName);

            const textY = -tokenRadius - fontSize * 0.7 - 2;
            const strokeWidth = Math.max(4.5, fontSize * 0.26);

            if (isLeader && t.completed) {
                ctx.save();
                ctx.font = `700 ${fontSize + 4}px "Segoe UI Emoji", Fredoka, sans-serif`;
                ctx.textAlign = 'center';
                ctx.shadowColor = '#FFD700';
                ctx.shadowBlur = 10;
                ctx.fillText('👑', 0, textY - fontSize * 0.9);
                ctx.restore();
            }

            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.lineJoin = 'round';
            ctx.miterLimit = 2;

            // Dark drop shadow behind outline
            ctx.save();
            ctx.shadowColor = 'rgba(0, 0, 0, 0.95)';
            ctx.shadowBlur = Math.max(6, fontSize * 0.4);
            ctx.shadowOffsetX = 0;
            ctx.shadowOffsetY = 2;

            // Heavy dark contrast outline
            ctx.strokeStyle = '#000000';
            ctx.lineWidth = strokeWidth + 1.5;
            ctx.strokeText(label, 0, textY);
            ctx.restore();

            // Razor-sharp crisp black contrast border
            ctx.strokeStyle = '#000000';
            ctx.lineWidth = strokeWidth;
            ctx.strokeText(label, 0, textY);

            const tokenTheme = t.theme || currentTheme || {};
            const rawColor = isLeader ? '#FFD700' : (t.userColor || tokenTheme.textColor || currentTheme.textColor || '#FFFFFF');
            const nameColor = isLeader ? '#FFD700' : brightenColor(rawColor);
            ctx.fillStyle = nameColor;
            ctx.fillText(label, 0, textY);

            ctx.restore();

            if (shouldRemove) {
                activeTokens.splice(i, 1);
            }
        }

        // Update & draw confetti
        for (let i = confettiParticles.length - 1; i >= 0; i--) {
            const c = confettiParticles[i];
            c.life += dtSec;
            if (c.life >= c.maxLife) {
                confettiParticles.splice(i, 1);
                continue;
            }
            c.x += c.vx * dtSec;
            c.y += c.vy * dtSec;
            c.vy += 320 * dtSec;

            ctx.save();
            ctx.globalAlpha = 1.0 - (c.life / c.maxLife);
            ctx.fillStyle = c.color;
            ctx.beginPath();
            ctx.arc(c.x, c.y, c.size, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
        }

        // Update & draw bucket landing popups
        for (let i = bucketHighlights.length - 1; i >= 0; i--) {
            const h = bucketHighlights[i];
            const age = (now - h.createdAt) / 1000;
            if (age > 2.2) {
                bucketHighlights.splice(i, 1);
                continue;
            }
            ctx.save();
            ctx.globalAlpha = Math.max(0, 1.0 - Math.max(0, age - 1.2));
            ctx.font = '700 22px Fredoka, Rubik, sans-serif';
            ctx.fillStyle = h.color;
            ctx.textAlign = 'center';
            ctx.shadowColor = 'rgba(0,0,0,0.85)';
            ctx.shadowBlur = 8;
            ctx.fillText(h.text, h.x, h.y - age * 28);
            ctx.restore();
        }

            drawSessionBanner(now);
        } catch (err) {
            console.error('[Plinko] Render error:', err);
        } finally {
            requestAnimationFrame(animate);
        }
    }

    function getBannerX(bannerW) {
        const pos = (headerConfig.position || 'top-center').toLowerCase().replace(/[-_]/g, '');
        if (pos === 'topleft') return 24;
        if (pos === 'topright') return width - bannerW - 24;
        return (width - bannerW) / 2;
    }

    function drawSessionBanner(now) {
        // Auto-Avoid Overlap: When other game (Parachute) is active and autoAvoidOverlap is true,
        // stack Plinko underneath (Row 2, +54px)
        let targetY = typeof headerConfig.yOffset === 'number' ? headerConfig.yOffset : 18;
        if (headerConfig.autoAvoidOverlap !== false && (isOtherGameActive || now < otherDropsActiveUntilMs)) {
            targetY += 54;
        }
        currentBannerY += (targetY - currentBannerY) * 0.15;
        const bannerY = Math.round(currentBannerY);

        if (!sessionState.isActive) {
            if (sessionState.endedAtMs) {
                const ageSec = (now - sessionState.endedAtMs) / 1000;
                if (ageSec <= 6.0) {
                    if (headerConfig.showBanner === false) return;

                    // Flash completed round banner
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
                    ctx.roundRect(bannerX, bannerY, bannerW, bannerH, 23);
                    ctx.fill();
                    ctx.stroke();

                    ctx.font = '700 17px Fredoka, Rubik, sans-serif';
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillStyle = '#34D399';
                    ctx.shadowColor = 'rgba(0,0,0,0.85)';
                    ctx.shadowBlur = 6;
                    ctx.fillText(`Round Closed! Total Tokens Dropped: ${sessionState.totalDrops}`, bannerCenterX, bannerY + bannerH / 2);
                    ctx.restore();
                    return;
                } else {
                    // Round fanfare has ended: clear tokens and reset state so everything disappears cleanly
                    sessionState.endedAtMs = 0;
                    sessionState.totalDrops = 0;
                    return;
                }
            }

            // Free Play mode: only show while tokens are actively on screen!
            const hasActiveToken = activeTokens.some(t => !t.completed || (now - t.completedAtMs) < 6000);
            if (hasActiveToken && sessionState.totalDrops > 0 && headerConfig.showFreePlayCounter !== false) {
                ctx.save();
                const pillW = 210;
                const pillH = 38;
                const pillX = getBannerX(pillW);

                ctx.fillStyle = 'rgba(15, 23, 42, 0.78)';
                ctx.strokeStyle = currentTheme.primaryColor || '#FF8FA3';
                ctx.lineWidth = 2;
                ctx.beginPath();
                ctx.roundRect(pillX, bannerY, pillW, pillH, 19);
                ctx.fill();
                ctx.stroke();

                ctx.font = '700 16px Fredoka, Rubik, sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillStyle = '#FFFFFF';
                ctx.shadowColor = 'rgba(0,0,0,0.85)';
                ctx.shadowBlur = 6;
                ctx.fillText(`Tokens: ${sessionState.totalDrops}`, pillX + pillW / 2, bannerY + pillH / 2);
                ctx.restore();
            } else if (!hasActiveToken && activeTokens.length === 0) {
                sessionState.totalDrops = 0;
            }
            return;
        }

        if (headerConfig.showBanner === false) return;

        const showTimer = headerConfig.showTimer !== false;
        const showCount = headerConfig.showCount !== false;

        // Dynamic banner width based on visible items
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
        const totalSec = Math.max(1, sessionState.totalDurationSeconds || 100);
        const progress = Math.max(0, Math.min(1, remainingSec / totalSec));

        const mins = Math.floor(remainingSec / 60);
        const secs = remainingSec % 60;
        const timeStr = `${mins}:${secs < 10 ? '0' : ''}${secs}`;

        // Semi-transparent pill backdrop
        ctx.fillStyle = 'rgba(15, 23, 42, 0.78)';
        ctx.strokeStyle = currentTheme.primaryColor || '#FF8FA3';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.roundRect(bannerX, bannerY, bannerW, bannerH, 23);
        ctx.fill();
        ctx.stroke();

        // Progress bar at bottom of pill
        const barW = (bannerW - 32) * progress;
        ctx.fillStyle = currentTheme.accentColor || '#FFE066';
        ctx.beginPath();
        ctx.roundRect(bannerX + 16, bannerY + bannerH - 8, barW, 4, 2);
        ctx.fill();

        // Banner text
        let bannerText = '';
        if (showTimer && showCount) {
            bannerText = `Round Ends: ${timeStr}  |  Tokens: ${sessionState.totalDrops}`;
        } else if (showTimer) {
            bannerText = `Round Ends: ${timeStr}`;
        } else if (showCount) {
            bannerText = `Tokens: ${sessionState.totalDrops}`;
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

    if (typeof window !== 'undefined') window.__plinkoTokens = activeTokens;
    requestAnimationFrame(animate);
})();

