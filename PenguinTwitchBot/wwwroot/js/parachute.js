// Parachute Drop & Interactive Droplets Overlay Engine
(function () {
    'use strict';

    const canvas = document.getElementById('parachute-canvas');
    if (!canvas) {
        console.error('Parachute canvas element (#parachute-canvas) not found.');
        return;
    }
    const ctx = canvas.getContext('2d');

    const TICK_RATE = 60;
    const DT = 1 / TICK_RATE;

    let width = window.innerWidth || 1920;
    let height = window.innerHeight || 1080;

    function resizeCanvas() {
        width = window.innerWidth || 1920;
        height = window.innerHeight || 1080;
        canvas.width = width;
        canvas.height = height;
    }
    window.addEventListener('resize', resizeCanvas);
    resizeCanvas();

    const commonModule = (typeof DropGamesCommon !== 'undefined')
        ? DropGamesCommon
        : (typeof window !== 'undefined' && window.DropGamesCommon ? window.DropGamesCommon : null);

    const imageLoader = (commonModule && typeof commonModule.createImageLoader === 'function')
        ? commonModule.createImageLoader('Parachute')
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
        window.__parachuteSoundManager = SoundManager;
    }

    function playSound(url, volume = 0.5) {
        if (!url) return;
        SoundManager.play({ enabled: true, url, volume: Math.round(volume * 100) }, 'legacy', false);
    }

    // Active game entities
    const activeDrops = [];
    const activeDroplets = [];
    const activeRockets = [];
    const rocketExplosions = [];
    const canopyFragments = [];
    const splashBursts = [];
    const landingPopups = [];
    const ambientParticles = [];
    let currentTheme = {
        id: 'spring',
        name: 'Spring (Cherry Blossoms)',
        seasonPreset: 'spring',
        particleType: 'petals',
        particleDensity: 40,
        primaryColor: '#FF8FA3',
        secondaryColor: '#FFB7C5',
        accentColor: '#FFE066',
        targetColor: '#6BCB77',
        textColor: '#FFFFFF'
    };

    let targetState = {
        centerX: 0.5,
        targetHalfWidth: 0.09,
        bullseyeHalfWidth: 0.025,
        visibleUntil: 0
    };

    let sessionState = {
        isActive: false,
        totalDurationSeconds: 100,
        startedAtMs: 0,
        endsAtMs: 0,
        totalDrops: 0,
        endedAtMs: 0
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
    let currentBannerY = 18;

    try {
        const urlParams = new URLSearchParams(window.location.search);
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


    function syncAmbientParticles() {
        const isEnabled = currentTheme && currentTheme.enableBackgroundParticles !== false && currentTheme.particleType !== 'none';
        const desired = isEnabled
            ? Math.min(80, Math.max(0, currentTheme.particleDensity || 35))
            : 0;

        const pType = (currentTheme && currentTheme.particleType) || 'petals';

        while (ambientParticles.length < desired) {
            const isBat = pType === 'bats';
            ambientParticles.push({
                x: Math.random() * width,
                y: Math.random() * height,
                vx: isBat ? (Math.random() < 0.5 ? -1 : 1) * (45 + Math.random() * 55) : (Math.random() - 0.5) * 35,
                vy: isBat ? (Math.random() - 0.5) * 20 : (pType === 'bubbles' ? -(25 + Math.random() * 45) : (25 + Math.random() * 45)),
                size: isBat ? (10 + Math.random() * 8) : (5 + Math.random() * 8),
                angle: Math.random() * Math.PI * 2,
                spin: isBat ? 0 : (Math.random() - 0.5) * 2.2,
                alpha: 0.45 + Math.random() * 0.45
            });
        }
        if (ambientParticles.length > desired) {
            ambientParticles.length = desired;
        }
    }

    function spawnSplashBurst(xPx, yPx, color, count, scale = 1.0, shape = 'circle') {
        for (let i = 0; i < count; i++) {
            const angle = (Math.PI * 2 * i) / count + (Math.random() - 0.5) * 0.4;
            const speed = (24 + Math.random() * 45) * scale;
            splashBursts.push({
                x: xPx,
                y: yPx,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed - 12,
                size: (1.5 + Math.random() * 1.8) * scale,
                color: color,
                shape: shape,
                rotation: Math.random() * Math.PI * 2,
                spin: (Math.random() - 0.5) * 5,
                alpha: 1.0,
                life: 0,
                maxLife: 0.22 + Math.random() * 0.12
            });
        }
    }

    function spawnRocketExplosion(xPx, yPx, color, count = 40) {
        const palette = [color || '#FF3366', '#FFD166', '#FFFFFF', '#00F0FF', '#FF9900', '#39FF14'];
        for (let i = 0; i < count; i++) {
            const angle = (Math.PI * 2 * i) / count + (Math.random() - 0.5) * 0.35;
            const speed = 70 + Math.random() * 200;
            rocketExplosions.push({
                x: xPx,
                y: yPx,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                gravity: 140 + Math.random() * 80,
                drag: 0.96,
                size: 2.0 + Math.random() * 2.5,
                color: palette[i % palette.length],
                alpha: 1.0,
                life: 0,
                maxLife: 0.55 + Math.random() * 0.40
            });
        }
    }

    function spawnCanopyFragments(xPx, yPx, theme) {
        const colors = [
            (theme && theme.primaryColor) || '#FF8FA3',
            (theme && theme.secondaryColor) || '#FFB7C5',
            '#FFFFFF'
        ];
        for (let i = 0; i < 18; i++) {
            const angle = Math.random() * Math.PI * 2;
            const speed = 40 + Math.random() * 95;
            canopyFragments.push({
                x: xPx + (Math.random() - 0.5) * 32,
                y: yPx + (Math.random() - 0.5) * 16,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed - 30,
                rotation: Math.random() * Math.PI * 2,
                spin: (Math.random() - 0.5) * 8,
                size: 4 + Math.random() * 6,
                color: colors[i % colors.length],
                alpha: 1.0,
                life: 0,
                maxLife: 1.2 + Math.random() * 0.6
            });
        }
    }

    function spawnCannonballSplash(xPx, yPx) {
        const splashColors = ['#00B4D8', '#48CAE4', '#90E0EF', '#FFFFFF', '#CAF0F8'];
        for (let i = 0; i < 50; i++) {
            const angle = -Math.PI * 0.15 - (Math.random() * Math.PI * 0.70);
            const speed = 120 + Math.random() * 260;
            splashBursts.push({
                x: xPx + (Math.random() - 0.5) * 50,
                y: yPx + (Math.random() - 0.5) * 10,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed - 80,
                size: 3.0 + Math.random() * 4.0,
                color: splashColors[i % splashColors.length],
                shape: 'circle',
                alpha: 1.0,
                life: 0,
                maxLife: 0.70 + Math.random() * 0.35
            });
        }
    }

    function spawnCrashSplat(xPx, yPx) {
        const dustColors = ['#D1D5DB', '#9CA3AF', '#6B7280', '#E5E7EB'];
        for (let i = 0; i < 30; i++) {
            const angle = Math.random() * Math.PI * 2;
            const speed = 40 + Math.random() * 110;
            splashBursts.push({
                x: xPx,
                y: yPx,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed * 0.45,
                size: 3.5 + Math.random() * 4.0,
                color: dustColors[i % dustColors.length],
                shape: 'circle',
                alpha: 1.0,
                life: 0,
                maxLife: 0.60 + Math.random() * 0.30
            });
        }
    }

    function spawnLandingPuff(xPx, yPx, seasonPreset, isBullseye = false) {
        if (currentTheme && (currentTheme.enableLandingParticles === false || currentTheme.landingParticleType === 'none')) {
            return;
        }

        const season = (seasonPreset || (currentTheme && currentTheme.seasonPreset) || 'spring').toLowerCase();
        const pType = ((currentTheme && currentTheme.landingParticleType) || season).toLowerCase();
        const density = (currentTheme && currentTheme.landingParticleDensity) || 36;
        const count = isBullseye ? Math.round(density * 1.4) : density;
        const scale = isBullseye ? 1.45 : 1.1;

        if (pType === 'candies' || season === 'halloween') {
            // Halloween candy corn & green potion drops
            const candyColors = ['#FF7518', '#FFE066', '#FFFFFF', '#8338EC', '#39FF14'];
            for (let i = 0; i < count; i++) {
                const angle = -Math.PI * 0.12 - (Math.random() * Math.PI * 0.76);
                const speed = (90 + Math.random() * 170) * scale;
                splashBursts.push({
                    x: xPx + (Math.random() - 0.5) * 40,
                    y: yPx + (Math.random() - 0.5) * 12,
                    vx: Math.cos(angle) * speed,
                    vy: Math.sin(angle) * speed - 65,
                    size: (5 + Math.random() * 6) * scale,
                    color: candyColors[Math.floor(Math.random() * candyColors.length)],
                    shape: i % 2 === 0 ? 'candies' : 'circle',
                    rotation: Math.random() * Math.PI * 2,
                    spin: (Math.random() - 0.5) * 8,
                    alpha: 1.0,
                    life: 0,
                    maxLife: 0.8 + Math.random() * 0.45
                });
            }
        } else if (pType === 'confetti' || season === 'christmas') {
            // Holiday star sparkles & red/green/gold confetti
            const holidayColors = ['#D90429', '#2B9348', '#FFD166', '#FFFFFF', '#007F5F'];
            for (let i = 0; i < count; i++) {
                const angle = -Math.PI * 0.1 - (Math.random() * Math.PI * 0.8);
                const speed = (100 + Math.random() * 180) * scale;
                splashBursts.push({
                    x: xPx + (Math.random() - 0.5) * 38,
                    y: yPx + (Math.random() - 0.5) * 10,
                    vx: Math.cos(angle) * speed,
                    vy: Math.sin(angle) * speed - 70,
                    size: (5 + Math.random() * 5) * scale,
                    color: holidayColors[Math.floor(Math.random() * holidayColors.length)],
                    shape: 'confetti',
                    rotation: Math.random() * Math.PI * 2,
                    spin: (Math.random() - 0.5) * 9,
                    alpha: 1.0,
                    life: 0,
                    maxLife: 0.85 + Math.random() * 0.4
                });
            }
        } else if (pType === 'fall' || pType === 'leaves' || season === 'fall') {
            // Autumn leaf puff
            const leafColors = ['#E76F51', '#F4A261', '#E9C46A', '#D9480F', '#8C3714', '#9B2226'];
            for (let i = 0; i < count; i++) {
                const angle = -Math.PI * 0.15 - (Math.random() * Math.PI * 0.7); // Fountains up and outward
                const speed = (95 + Math.random() * 175) * scale;
                splashBursts.push({
                    x: xPx + (Math.random() - 0.5) * 44,
                    y: yPx + (Math.random() - 0.5) * 14,
                    vx: Math.cos(angle) * speed,
                    vy: Math.sin(angle) * speed - 65,
                    size: (6 + Math.random() * 7) * scale,
                    color: leafColors[Math.floor(Math.random() * leafColors.length)],
                    shape: 'leaf',
                    rotation: Math.random() * Math.PI * 2,
                    spin: (Math.random() - 0.5) * 7,
                    alpha: 1.0,
                    life: 0,
                    maxLife: 0.8 + Math.random() * 0.45
                });
            }
        } else if (pType === 'spring' || pType === 'petals' || season === 'spring') {
            // Cherry blossom petal burst
            const petalColors = ['#FF8FA3', '#FFB7C5', '#FFF0F3', '#FF4D6D', '#FFCCD5'];
            for (let i = 0; i < count; i++) {
                const angle = -Math.PI * 0.12 - (Math.random() * Math.PI * 0.76);
                const speed = (85 + Math.random() * 160) * scale;
                splashBursts.push({
                    x: xPx + (Math.random() - 0.5) * 36,
                    y: yPx + (Math.random() - 0.5) * 10,
                    vx: Math.cos(angle) * speed,
                    vy: Math.sin(angle) * speed - 55,
                    size: (5 + Math.random() * 6) * scale,
                    color: petalColors[Math.floor(Math.random() * petalColors.length)],
                    shape: 'petal',
                    rotation: Math.random() * Math.PI * 2,
                    spin: (Math.random() - 0.5) * 6,
                    alpha: 1.0,
                    life: 0,
                    maxLife: 0.75 + Math.random() * 0.4
                });
            }
        } else if (pType === 'winter' || pType === 'snow' || season === 'winter') {
            // Snow powder poof
            const snowColors = ['#FFFFFF', '#E0F2FE', '#BAE6FD', '#F0F9FF'];
            for (let i = 0; i < count; i++) {
                const angle = -Math.PI * 0.1 - (Math.random() * Math.PI * 0.8);
                const speed = (70 + Math.random() * 150) * scale;
                splashBursts.push({
                    x: xPx + (Math.random() - 0.5) * 40,
                    y: yPx + (Math.random() - 0.5) * 12,
                    vx: Math.cos(angle) * speed,
                    vy: Math.sin(angle) * speed - 55,
                    size: (4 + Math.random() * 7) * scale,
                    color: snowColors[Math.floor(Math.random() * snowColors.length)],
                    shape: 'snow',
                    rotation: 0,
                    spin: 0,
                    alpha: 1.0,
                    life: 0,
                    maxLife: 0.7 + Math.random() * 0.4
                });
            }
        } else {
            // Summer pool water splash
            const waterColors = ['#00B4D8', '#48CAE4', '#90E0EF', '#CAF0F8', '#FFFFFF'];
            for (let i = 0; i < count; i++) {
                const angle = -Math.PI * 0.15 - (Math.random() * Math.PI * 0.7);
                const speed = (95 + Math.random() * 190) * scale;
                splashBursts.push({
                    x: xPx + (Math.random() - 0.5) * 38,
                    y: yPx + (Math.random() - 0.5) * 10,
                    vx: Math.cos(angle) * speed,
                    vy: Math.sin(angle) * speed - 75,
                    size: (4 + Math.random() * 6) * scale,
                    color: waterColors[Math.floor(Math.random() * waterColors.length)],
                    shape: 'water',
                    rotation: 0,
                    spin: 0,
                    alpha: 1.0,
                    life: 0,
                    maxLife: 0.65 + Math.random() * 0.35
                });
            }
        }
    }

    // Interpolate server keyframes for 100% authoritative trajectory alignment
    function sampleKeyframes(keyframes, tick) {
        if (!keyframes || !keyframes.length) return null;
        if (tick <= keyframes[0].tick) return keyframes[0];
        const last = keyframes[keyframes.length - 1];
        if (tick >= last.tick) return last;

        for (let i = 0; i < keyframes.length - 1; i++) {
            const a = keyframes[i];
            const b = keyframes[i + 1];
            if (tick >= a.tick && tick <= b.tick) {
                const span = Math.max(1, b.tick - a.tick);
                const t = (tick - a.tick) / span;
                return {
                    x: a.x + (b.x - a.x) * t,
                    y: a.y + (b.y - a.y) * t,
                    vx: a.vx + (b.vx - a.vx) * t,
                    vy: a.vy + (b.vy - a.vy) * t
                };
            }
        }
        return last;
    }

    // WebSocket connection & completions queue
    const overlayKey = new URLSearchParams(window.location.search).get('key') || '';
    const pendingCompletions = [];
    const socket = window.createWsSocket('parachute', [window.WS_TOPICS.parachute]);

    function sendCompletion(token, landingX, parachuteDestroyed = false) {
        if (!token) return;
        // View-Only mode: without the authorized overlay key, do not send completion packets
        if (!overlayKey) {
            return;
        }
        const payload = { dropToken: token, overlayKey: overlayKey };
        if (typeof landingX === 'number' && !isNaN(landingX)) {
            payload.landingX = Math.round(landingX * 10000) / 10000;
        }
        if (parachuteDestroyed) {
            payload.parachuteDestroyed = true;
            payload.crashLanded = true;
        }
        if (socket && socket.readyState === WebSocket.OPEN) {
            try {
                socket.send(JSON.stringify(payload));
                return;
            } catch (_) {}
        }
        if (!pendingCompletions.some(p => p.token === token)) {
            pendingCompletions.push({ token, landingX, parachuteDestroyed });
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
                if (typeof item.landingX === 'number') payload.landingX = item.landingX;
                if (item.parachuteDestroyed) {
                    payload.parachuteDestroyed = true;
                    payload.crashLanded = true;
                }
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
        const activeTokens = Array.isArray(activeDrops)
            ? activeDrops.filter(d => d && d.dropToken && !d.isComplete).map(d => d.dropToken)
            : [];
        const completingTokens = Array.isArray(pendingCompletions)
            ? pendingCompletions.filter(c => c && c.token).map(c => c.token)
            : [];

        if (activeTokens.length === 0 && completingTokens.length === 0) {
            if (heartbeatInterval) {
                clearInterval(heartbeatInterval);
                heartbeatInterval = null;
            }
            return;
        }

        try {
            socket.send(JSON.stringify({
                type: 'pong',
                game: 'parachute',
                activeTokens: activeTokens,
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
            const hasDrops = (Array.isArray(activeDrops) && activeDrops.some(d => d && d.dropToken && !d.isComplete)) ||
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

        // Ignore messages belonging to other games
        if (msg.session && msg.session.gameType && msg.session.gameType !== 'parachute') {
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

        const p = msg.trajectory || msg.physics;
        if (msg.type === 'parachute_drop' && p) {
            if (msg.theme) {
                currentTheme = msg.theme;
                syncAmbientParticles();
            }

            targetState.centerX = p.targetCenterX || 0.5;
            targetState.targetHalfWidth = p.targetHalfWidth || 0.09;
            targetState.bullseyeHalfWidth = p.bullseyeHalfWidth || (targetState.targetHalfWidth * 0.25);
            targetState.visibleUntil = performance.now() + ((p.durationSeconds || 6.5) + 4.5) * 1000;

            if (msg.session && typeof msg.session.totalDrops === 'number') {
                sessionState.totalDrops = msg.session.totalDrops;
                if (typeof msg.session.isActive === 'boolean') {
                    sessionState.isActive = msg.session.isActive;
                }
            } else if (!sessionState.isActive) {
                sessionState.totalDrops = (sessionState.totalDrops || 0) + 1;
            }

            if (msg.sounds) {
                SoundManager.updateBundle(msg.sounds);
            }
            const dropSound = (msg.sounds && msg.sounds.drop) || (SoundManager.getBundle() && SoundManager.getBundle().drop);
            if (dropSound) {
                SoundManager.play(dropSound, 'drop', false);
            } else if (currentTheme.dropSoundUrl) {
                playSound(currentTheme.dropSoundUrl, 0.45);
            }

            // Preload custom images if any
            getLoadedImage(msg.characterImageUrl);
            if (currentTheme.parachuteCanopyImageUrl) getLoadedImage(currentTheme.parachuteCanopyImageUrl);
            if (currentTheme.targetPlatformImageUrl) getLoadedImage(currentTheme.targetPlatformImageUrl);

            activeDrops.push({
                dropToken: msg.dropToken,
                sounds: msg.sounds || (msg.session && msg.session.sounds) || SoundManager.getBundle(),
                username: msg.username || 'viewer',
                displayName: msg.displayName || msg.username || 'Viewer',
                userColor: msg.userColor || msg.color || null,
                characterImageUrl: msg.characterImageUrl || '',
                theme: msg.theme || currentTheme,
                startTimeMs: performance.now(),
                currentTick: 0,
                totalTicks: p.totalTicks || 390,
                startX: p.startX,
                startY: p.startY,
                landingY: p.landingY || 0.94,
                x: p.startX,
                y: p.startY,
                vx: p.initialVx || 0,
                vy: p.initialVy || 0.12,
                initialVy: p.initialVy || 0.12,
                nameFontSize: typeof p.nameFontSize === 'number' ? p.nameFontSize : (msg.nameFontSize || headerConfig.nameFontSize || 18),
                gravity: typeof p.gravity === 'number' ? p.gravity : 1.0,
                bounciness: typeof p.bounciness === 'number' ? p.bounciness : 0.85,
                mass: typeof p.mass === 'number' ? p.mass : 1.0,
                swayPhase: p.swayPhase || 0,
                swayFrequency: p.swayFrequency || 2.0,
                swayAmplitude: p.swayAmplitude || 0.02,
                landingX: p.landingX,
                score: p.score || 0,
                isHit: !!p.isHit,
                isBullseye: !!p.isBullseye,
                isWin: !!p.isWin,
                dropletHits: 0,
                keyframes: p.keyframes || [],
                collisions: Array.isArray(p.collisions) ? p.collisions : [],
                triggeredCollisionTicks: new Set(),
                tiltAngle: p.initialTilt !== undefined ? p.initialTilt : (Math.random() - 0.5) * 0.45,
                completed: false,
                landedAtMs: 0
            });
            ensureHeartbeat();
        } else if (msg.type === 'parachute_droplets') {
            if (msg.theme) {
                currentTheme = msg.theme;
                syncAmbientParticles();
            }

            const nowMs = performance.now();
            targetState.visibleUntil = Math.max(targetState.visibleUntil, nowMs + 8000);

            if (Array.isArray(msg.droplets)) {
                msg.droplets.forEach(function (d) {
                    if (d.imageUrl) getLoadedImage(d.imageUrl);
                    const speedMultiplier = d.speedScale || 1.0;
                    activeDroplets.push({
                        id: d.id,
                        spawnTimeMs: nowMs + (d.spawnTickOffset || 0) * DT * 1000,
                        startX: d.startX,
                        startY: d.startY,
                        x: d.startX,
                        y: d.startY,
                        vx: (d.vx || 0),
                        vy: (d.vy || 0.12),
                        gravity: (d.gravity || 0.18) * 1.5,
                        radius: d.radius || 0.028,
                        sizeScale: d.sizeScale || 1.0,
                        speedScale: speedMultiplier,
                        sizeLabel: d.sizeLabel || 'Medium',
                        speedLabel: d.speedLabel || 'Normal',
                        imageUrl: d.imageUrl || '',
                        rotation: Math.random() * Math.PI * 2,
                        angularVel: ((Math.random() - 0.5) * 5.0 + (d.vx || 0) * 8.0) * speedMultiplier,
                        restitution: 0.82,
                        isSettled: false,
                        settledTimer: 0,
                        hitCount: 0,
                        alpha: 1.0
                    });
                });
            }

            // Apply server-synchronized trajectory updates to active parachutists
            if (Array.isArray(msg.trajectoryUpdates)) {
                msg.trajectoryUpdates.forEach(function (upd) {
                    const drop = activeDrops.find(function (ad) { return ad.dropToken === upd.dropToken; });
                    if (drop) {
                        drop.landingX = upd.landingX;
                        drop.score = upd.score;
                        drop.isHit = upd.isHit;
                        drop.isBullseye = upd.isBullseye;
                        drop.isWin = upd.isWin;
                        drop.dropletHits = upd.dropletHits || 0;
                        if (Array.isArray(upd.keyframes) && upd.keyframes.length) {
                            drop.keyframes = upd.keyframes;
                        }
                        if (Array.isArray(upd.collisions)) {
                            drop.collisions = upd.collisions;
                        }
                    }
                });
            }
        } else if (msg.type === 'parachute_rockets') {
            const nowMs = performance.now();
            targetState.visibleUntil = Math.max(targetState.visibleUntil, nowMs + 8000);

            if (Array.isArray(msg.rockets)) {
                msg.rockets.forEach(function (r) {
                    activeRockets.push({
                        id: r.id,
                        spawnTimeMs: nowMs + (r.spawnTickOffset || 0) * DT * 1000,
                        startX: r.startX,
                        startY: r.startY,
                        x: r.startX,
                        y: r.startY,
                        targetX: r.targetX,
                        targetY: r.targetY,
                        vx: r.vx || 0,
                        vy: r.vy || -0.85,
                        accelerationY: r.accelerationY || 0.04,
                        fuseTicks: r.fuseTicks || 48,
                        fuseTimeMs: nowMs + ((r.spawnTickOffset || 0) + (r.fuseTicks || 48)) * DT * 1000,
                        color: r.color || '#FF3366',
                        trailType: r.trailType || 'sparks',
                        targetDropToken: r.targetDropToken,
                        sparks: [],
                        hasLaunched: false
                    });
                });
            }

            // Apply server-synchronized trajectory updates to active parachutists
            if (Array.isArray(msg.trajectoryUpdates)) {
                msg.trajectoryUpdates.forEach(function (upd) {
                    const drop = activeDrops.find(function (ad) { return ad.dropToken === upd.dropToken; });
                    if (drop) {
                        drop.landingX = upd.landingX;
                        drop.score = upd.score;
                        drop.isHit = upd.isHit;
                        drop.isBullseye = upd.isBullseye;
                        drop.isWin = upd.isWin;
                        drop.dropletHits = upd.dropletHits || 0;
                        drop.rocketHits = upd.rocketHits || 0;
                        if (upd.parachuteDestroyed) {
                            drop.freeFallStartTick = upd.freeFallStartTick;
                            if (drop.freeFallStartTick === undefined || drop.freeFallStartTick === null || drop.currentTick >= drop.freeFallStartTick) {
                                drop.parachuteDestroyed = true;
                                drop.pendingParachuteDestroyed = false;
                            } else {
                                drop.pendingParachuteDestroyed = true;
                                drop.parachuteDestroyed = false;
                            }
                        }
                        if (Array.isArray(upd.keyframes) && upd.keyframes.length) {
                            drop.keyframes = upd.keyframes;
                        }
                        if (Array.isArray(upd.collisions)) {
                            drop.collisions = upd.collisions;
                        }
                    }
                });
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
            activeDrops.length = 0;
            activeDroplets.length = 0;
            activeRockets.length = 0;
            rocketExplosions.length = 0;
            canopyFragments.length = 0;
            splashBursts.length = 0;
            landingPopups.length = 0;
            sessionState.isActive = true;
            sessionState.endedAtMs = 0;
            sessionState.totalDurationSeconds = s.totalDurationSeconds || 100;
            sessionState.startedAtMs = performance.now();
            sessionState.endsAtMs = performance.now() + (s.remainingSeconds || s.totalDurationSeconds || 100) * 1000;
            sessionState.totalDrops = typeof s.totalDrops === 'number' ? s.totalDrops : 0;
            if (s.theme) {
                currentTheme = s.theme;
                syncAmbientParticles();
            }
            if (s.targetCenterX !== undefined && s.targetCenterX !== null) {
                targetState.centerX = s.targetCenterX;
                if (s.targetHalfWidth) targetState.targetHalfWidth = s.targetHalfWidth;
                if (s.bullseyeHalfWidth) targetState.bullseyeHalfWidth = s.bullseyeHalfWidth;
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
            if (msg.session.targetCenterX !== undefined && msg.session.targetCenterX !== null) {
                targetState.centerX = msg.session.targetCenterX;
            }
        }

        if (msg.session && sessionState && (!msg.session.gameType || msg.session.gameType === 'parachute')) {
            if (typeof msg.session.totalDrops === 'number') {
                sessionState.totalDrops = msg.session.totalDrops;
            }
            if (msg.session.targetCenterX !== undefined && msg.session.targetCenterX !== null) {
                targetState.centerX = msg.session.targetCenterX;
                if (msg.session.targetHalfWidth) targetState.targetHalfWidth = msg.session.targetHalfWidth;
                if (msg.session.bullseyeHalfWidth) targetState.bullseyeHalfWidth = msg.session.bullseyeHalfWidth;
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

    function drawAmbientParticles(dtSec) {
        if (!ambientParticles.length || !currentTheme || currentTheme.enableBackgroundParticles === false || currentTheme.particleType === 'none') return;
        const pType = currentTheme.particleType || 'petals';

        ctx.save();
        for (let i = 0; i < ambientParticles.length; i++) {
            const p = ambientParticles[i];
            p.x += p.vx * dtSec;
            p.y += p.vy * dtSec;
            p.angle += p.spin * dtSec;

            if (pType === 'bats') {
                // Wave flight path for bats
                p.y += Math.sin(p.x * 0.05) * 0.8;
                if (p.x < -30) p.x = width + 25;
                if (p.x > width + 30) p.x = -25;
                if (p.y < 20) p.y = height * 0.6;
                if (p.y > height * 0.75) p.y = 40;
            } else {
                if (p.y > height + 20) {
                    p.y = -15;
                    p.x = Math.random() * width;
                }
                if (p.x < -20) p.x = width + 15;
                if (p.x > width + 20) p.x = -15;
            }

            ctx.save();
            ctx.translate(p.x, p.y);
            ctx.rotate(p.angle);
            ctx.globalAlpha = p.alpha;

            if (pType === 'petals') {
                ctx.fillStyle = currentTheme.secondaryColor || '#FFB7C5';
                ctx.beginPath();
                ctx.ellipse(0, 0, p.size, p.size * 0.58, 0, 0, Math.PI * 2);
                ctx.fill();
            } else if (pType === 'bubbles') {
                ctx.strokeStyle = currentTheme.primaryColor || '#00B4D8';
                ctx.lineWidth = 1.6;
                ctx.beginPath();
                ctx.arc(0, 0, p.size * 0.75, 0, Math.PI * 2);
                ctx.stroke();
            } else if (pType === 'leaves') {
                ctx.fillStyle = (i % 2 === 0) ? (currentTheme.primaryColor || '#E76F51') : (currentTheme.accentColor || '#E9C46A');
                ctx.beginPath();
                ctx.moveTo(0, -p.size);
                ctx.quadraticCurveTo(p.size, 0, 0, p.size);
                ctx.quadraticCurveTo(-p.size, 0, 0, -p.size);
                ctx.fill();
            } else if (pType === 'bats') {
                // Spooky bat silhouette with flapping wings
                ctx.fillStyle = currentTheme.secondaryColor || '#7B2CBF';
                const flap = Math.sin((p.x + performance.now() * 0.02) * 0.4) * (p.size * 0.35);
                ctx.beginPath();
                ctx.ellipse(0, 0, p.size * 0.35, p.size * 0.22, 0, 0, Math.PI * 2);
                ctx.fill();
                // Left wing
                ctx.beginPath();
                ctx.moveTo(-p.size * 0.2, 0);
                ctx.quadraticCurveTo(-p.size * 0.7, -p.size * 0.7 - flap, -p.size * 1.2, -p.size * 0.2 - flap);
                ctx.quadraticCurveTo(-p.size * 0.7, p.size * 0.2, -p.size * 0.2, 0);
                ctx.fill();
                // Right wing
                ctx.beginPath();
                ctx.moveTo(p.size * 0.2, 0);
                ctx.quadraticCurveTo(p.size * 0.7, -p.size * 0.7 - flap, p.size * 1.2, -p.size * 0.2 - flap);
                ctx.quadraticCurveTo(p.size * 0.7, p.size * 0.2, p.size * 0.2, 0);
                ctx.fill();
            } else if (pType === 'stars') {
                // Golden star sparkle
                ctx.fillStyle = currentTheme.accentColor || '#FFD166';
                const s = p.size * 0.65;
                ctx.beginPath();
                ctx.moveTo(0, -s);
                ctx.lineTo(s * 0.25, -s * 0.25);
                ctx.lineTo(s, 0);
                ctx.lineTo(s * 0.25, s * 0.25);
                ctx.lineTo(0, s);
                ctx.lineTo(-s * 0.25, s * 0.25);
                ctx.lineTo(-s, 0);
                ctx.lineTo(-s * 0.25, -s * 0.25);
                ctx.closePath();
                ctx.fill();
            } else {
                // snow
                ctx.fillStyle = '#FFFFFF';
                ctx.beginPath();
                ctx.arc(0, 0, p.size * 0.45, 0, Math.PI * 2);
                ctx.fill();
            }
            ctx.restore();
        }
        ctx.restore();
    }

    function getPlatformMetrics() {
        const targetW = targetState.targetHalfWidth * 2 * width;
        const bullseyeW = targetState.bullseyeHalfWidth * 2 * width;
        const cx = (typeof targetState.centerX === 'number' ? targetState.centerX : 0.5) * width;
        const y = height - 34;
        const season = ((currentTheme && currentTheme.seasonPreset) || 'spring').toLowerCase();
        return { targetW, bullseyeW, cx, y, season };
    }

    function drawTargetPlatformBack() {
        const { targetW, bullseyeW, cx, y, season } = getPlatformMetrics();
        ctx.save();

        const customTargetImg = getLoadedImage(currentTheme.targetPlatformImageUrl);
        if (customTargetImg) {
            ctx.drawImage(customTargetImg, cx - targetW / 2, y - 28, targetW, 56);
            ctx.restore();
            return;
        }

        ctx.shadowColor = 'rgba(0, 0, 0, 0.45)';
        ctx.shadowBlur = 16;
        ctx.shadowOffsetY = 6;

        if (season === 'fall') {
            // === AUTUMN: Deep earthy basin & back leaf mound ===
            ctx.fillStyle = '#4A2810';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 8, y - 24, targetW + 16, 48, 20);
            ctx.fill();

            // Layered deep back autumn leaves
            const backLeafColors = ['#8C3714', '#9B2226', '#B23A22', '#D9480F', '#6F4E37'];
            const leafCols = 22;
            const leafStep = (targetW + 20) / leafCols;
            for (let r = 0; r < 2; r++) {
                for (let i = 0; i <= leafCols; i++) {
                    const lx = (cx - targetW / 2 - 10) + i * leafStep;
                    const elevation = Math.sin((i / leafCols) * Math.PI) * 12 + (r * 7);
                    const ly = y - 22 + (i % 2 === 0 ? -4 : 3) - elevation;
                    ctx.save();
                    ctx.translate(lx, ly);
                    ctx.rotate((i * 0.4) - 0.8);
                    ctx.fillStyle = backLeafColors[(i + r * 3) % backLeafColors.length];
                    ctx.beginPath();
                    ctx.ellipse(0, 0, leafStep * 0.95, 8, 0, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.restore();
                }
            }

            // Bullseye golden target ring on leaf basin bed
            ctx.strokeStyle = 'rgba(255, 215, 0, 0.9)';
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.ellipse(cx, y - 6, bullseyeW * 0.5, 9, 0, 0, Math.PI * 2);
            ctx.stroke();
        } else if (season === 'summer') {
            // === SUMMER: Swimming Pool deep water basin & back deck ===
            ctx.fillStyle = '#CBD5E1';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 12, y - 28, targetW + 24, 54, 18);
            ctx.fill();

            // Pool interior deep turquoise water gradient
            const poolGrad = ctx.createLinearGradient(cx, y - 20, cx, y + 20);
            poolGrad.addColorStop(0, '#0077B6');
            poolGrad.addColorStop(1, '#03045E');
            ctx.fillStyle = poolGrad;
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 2, y - 20, targetW + 4, 40, 12);
            ctx.fill();

            // Pool bottom swimming lane markings & center bullseye float ring
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.35)';
            ctx.lineWidth = 2;
            ctx.strokeRect(cx - targetW * 0.35, y - 14, targetW * 0.7, 26);

            ctx.fillStyle = 'rgba(255, 209, 102, 0.85)';
            ctx.beginPath();
            ctx.ellipse(cx, y - 2, bullseyeW * 0.5, 8, 0, 0, Math.PI * 2);
            ctx.fill();

            // Stainless steel ladder rails on left rim
            ctx.strokeStyle = '#E2E8F0';
            ctx.lineWidth = 3;
            const lX = cx - targetW * 0.42;
            ctx.beginPath();
            ctx.moveTo(lX - 6, y - 28);
            ctx.lineTo(lX - 6, y - 4);
            ctx.moveTo(lX + 6, y - 28);
            ctx.lineTo(lX + 6, y - 4);
            ctx.moveTo(lX - 6, y - 20); ctx.lineTo(lX + 6, y - 20);
            ctx.moveTo(lX - 6, y - 12); ctx.lineTo(lX + 6, y - 12);
            ctx.stroke();
        } else if (season === 'halloween') {
            // === HALLOWEEN: Spooky Witch's Cauldron interior & boiling brew ===
            ctx.shadowColor = '#8338EC';
            ctx.shadowBlur = 24;

            // Cauldron heavy cast-iron interior wall
            ctx.fillStyle = '#181824';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 14, y - 28, targetW + 28, 56, 24);
            ctx.fill();

            // Glowing toxic potion brew
            const brewGrad = ctx.createRadialGradient(cx, y - 8, 4, cx, y - 8, targetW * 0.5);
            brewGrad.addColorStop(0, '#CCFF33');
            brewGrad.addColorStop(0.5, '#39FF14');
            brewGrad.addColorStop(1, '#0D5C1E');
            ctx.fillStyle = brewGrad;
            ctx.beginPath();
            ctx.ellipse(cx, y - 8, targetW * 0.48, 18, 0, 0, Math.PI * 2);
            ctx.fill();

            // Swirling cauldron bullseye rune
            ctx.strokeStyle = '#FFFFFF';
            ctx.lineWidth = 2.5;
            ctx.beginPath();
            ctx.ellipse(cx, y - 8, bullseyeW * 0.5, 9, 0, 0, Math.PI * 2);
            ctx.stroke();
        } else if (season === 'christmas') {
            // === CHRISTMAS: Holiday Celebration Gift Box & Evergreen boughs ===
            ctx.fillStyle = '#8B0000';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 10, y - 24, targetW + 20, 50, 16);
            ctx.fill();

            // Pine needle boughs with twinkling holiday lights
            const pineColors = ['#1B4332', '#2D6A4F', '#40916C', '#52B788'];
            const pCols = 18;
            const pStep = (targetW + 16) / pCols;
            for (let i = 0; i <= pCols; i++) {
                const px = (cx - targetW / 2 - 8) + i * pStep;
                const py = y - 24 + (i % 2 === 0 ? -5 : 2);
                ctx.fillStyle = pineColors[i % pineColors.length];
                ctx.beginPath();
                ctx.moveTo(px, py - 10);
                ctx.lineTo(px + pStep * 0.6, py + 8);
                ctx.lineTo(px - pStep * 0.6, py + 8);
                ctx.closePath();
                ctx.fill();

                const lightCols = ['#FFD166', '#FF4D6D', '#00B4D8', '#FFE066'];
                ctx.fillStyle = lightCols[i % lightCols.length];
                ctx.beginPath();
                ctx.arc(px, py - 6, 3, 0, Math.PI * 2);
                ctx.fill();
            }

            // Gold star bullseye ring
            ctx.strokeStyle = '#FFD166';
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.ellipse(cx, y - 6, bullseyeW * 0.5, 9, 0, 0, Math.PI * 2);
            ctx.stroke();
        } else if (season === 'winter') {
            // === WINTER: Glacial snowdrift shelf ===
            ctx.fillStyle = '#0077B6';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 8, y - 24, targetW + 16, 48, 18);
            ctx.fill();

            ctx.strokeStyle = '#90E0EF';
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.ellipse(cx, y - 6, bullseyeW * 0.5, 9, 0, 0, Math.PI * 2);
            ctx.stroke();
        } else {
            // === SPRING: Lush grassy meadow mound ===
            ctx.fillStyle = '#386641';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 8, y - 24, targetW + 16, 48, 18);
            ctx.fill();

            ctx.strokeStyle = '#FF8FA3';
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.ellipse(cx, y - 6, bullseyeW * 0.5, 9, 0, 0, Math.PI * 2);
            ctx.stroke();
        }

        ctx.restore();
    }

    function drawTargetPlatformFront() {
        const { targetW, bullseyeW, cx, y, season } = getPlatformMetrics();
        ctx.save();

        const customTargetImg = getLoadedImage(currentTheme.targetPlatformImageUrl);
        if (customTargetImg) {
            ctx.restore();
            return;
        }

        if (season === 'fall') {
            // === AUTUMN FRONT: Massive multi-layered crunchy leaf pile overlapping characters ===
            ctx.shadowColor = 'rgba(0, 0, 0, 0.4)';
            ctx.shadowBlur = 12;

            // Raised leaf drifts on left and right side rims
            ctx.fillStyle = '#9B2226';
            ctx.beginPath();
            ctx.ellipse(cx - targetW / 2, y - 10, 18, 14, -0.3, 0, Math.PI * 2);
            ctx.ellipse(cx + targetW / 2, y - 10, 18, 14, 0.3, 0, Math.PI * 2);
            ctx.fill();

            // 3 dense rows of vibrant front leaves overlapping characters' lower bodies
            const frontLeafColors = ['#D9480F', '#E76F51', '#F4A261', '#E9C46A', '#9B2226', '#B23A22'];
            const leafCols = 24;
            const leafStep = (targetW + 24) / leafCols;
            for (let tier = 0; tier < 3; tier++) {
                const tierOffsetY = -8 + tier * 7;
                for (let i = 0; i <= leafCols; i++) {
                    const lx = (cx - targetW / 2 - 12) + i * leafStep + ((tier % 2) * (leafStep * 0.5));
                    const ly = y + tierOffsetY + (i % 2 === 0 ? -3 : 3);
                    const col = frontLeafColors[(i + tier * 2) % frontLeafColors.length];
                    ctx.save();
                    ctx.translate(lx, ly);
                    ctx.rotate((i * 0.5) - 1.2 + (tier * 0.3));
                    ctx.fillStyle = col;
                    ctx.beginPath();
                    ctx.ellipse(0, 0, leafStep * 0.95, 7.5, 0, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.restore();
                }
            }
        } else if (season === 'summer') {
            // === SUMMER FRONT: Raised pool side rims & translucent animated water line ===
            // Shimmering translucent water covering landed characters' lower halves
            const waveTime = performance.now() * 0.003;
            ctx.fillStyle = 'rgba(0, 180, 216, 0.55)';
            ctx.beginPath();
            ctx.moveTo(cx - targetW / 2 + 2, y + 16);
            for (let xOffset = -targetW / 2 + 2; xOffset <= targetW / 2 - 2; xOffset += 8) {
                const waveY = y - 4 + Math.sin(waveTime + xOffset * 0.05) * 3;
                ctx.lineTo(cx + xOffset, waveY);
            }
            ctx.lineTo(cx + targetW / 2 - 2, y + 16);
            ctx.closePath();
            ctx.fill();

            // Raised pool side-walls on left and right (rims to bounce off or over)
            ctx.fillStyle = '#F8FAFC';
            ctx.strokeStyle = '#94A3B8';
            ctx.lineWidth = 2;
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 14, y - 24, 16, 44, 8);
            ctx.roundRect(cx + targetW / 2 - 2, y - 24, 16, 44, 8);
            ctx.fill();
            ctx.stroke();

            // Front pool coping curb
            ctx.fillStyle = '#E2E8F0';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 10, y + 8, targetW + 20, 16, 8);
            ctx.fill();
            ctx.stroke();
        } else if (season === 'halloween') {
            // === HALLOWEEN FRONT: Heavy cast-iron cauldron belly, handles & bubbling froth ===
            // Side ring handles
            ctx.strokeStyle = '#343A40';
            ctx.lineWidth = 6;
            ctx.beginPath();
            ctx.arc(cx - targetW / 2 - 12, y - 8, 12, 0, Math.PI * 2);
            ctx.arc(cx + targetW / 2 + 12, y - 8, 12, 0, Math.PI * 2);
            ctx.stroke();

            // Front cauldron wall
            ctx.fillStyle = '#212529';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 12, y - 2, targetW + 24, 28, 12);
            ctx.fill();

            // Glowing toxic potion froth overlapping players
            const frothTime = performance.now() * 0.004;
            ctx.fillStyle = '#39FF14';
            for (let fx = -targetW / 2; fx <= targetW / 2; fx += 16) {
                const fy = y - 4 + Math.sin(frothTime + fx * 0.1) * 3;
                ctx.beginPath();
                ctx.arc(cx + fx, fy, 6, 0, Math.PI * 2);
                ctx.fill();
            }
        } else if (season === 'christmas') {
            // === CHRISTMAS FRONT: Deep fluffy snowbank, pine garland & ribbon bow ===
            ctx.fillStyle = '#F8FAFC';
            const snowCols = 16;
            const sStep = (targetW + 20) / snowCols;
            for (let i = 0; i <= snowCols; i++) {
                ctx.beginPath();
                ctx.arc((cx - targetW / 2 - 8) + i * sStep, y + 2, 11, 0, Math.PI * 2);
                ctx.fill();
            }

            // Pine garland along front rim
            ctx.strokeStyle = '#2D6A4F';
            ctx.lineWidth = 6;
            ctx.beginPath();
            ctx.moveTo(cx - targetW / 2 - 6, y + 4);
            ctx.quadraticCurveTo(cx, y + 16, cx + targetW / 2 + 6, y + 4);
            ctx.stroke();

            // Big red velvet ribbon bow in center
            ctx.fillStyle = '#D90429';
            ctx.beginPath();
            ctx.ellipse(cx - 14, y + 10, 12, 6, -0.4, 0, Math.PI * 2);
            ctx.ellipse(cx + 14, y + 10, 12, 6, 0.4, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#FFD166';
            ctx.beginPath();
            ctx.arc(cx, y + 10, 6, 0, Math.PI * 2);
            ctx.fill();
        } else if (season === 'winter') {
            // === WINTER FRONT: Deep snowdrift puffs ===
            ctx.fillStyle = '#FFFFFF';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 8, y + 2, targetW + 16, 24, 12);
            ctx.fill();
            ctx.strokeStyle = '#BAE6FD';
            ctx.lineWidth = 2;
            ctx.stroke();
        } else {
            // === SPRING FRONT: Fresh green clover & cherry blossom petals ===
            ctx.fillStyle = '#55A630';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 - 6, y + 2, targetW + 12, 22, 10);
            ctx.fill();

            const sakuraColors = ['#FF8FA3', '#FFCCD5', '#FFF0F3', '#FF4D6D'];
            const pStep = targetW / 12;
            for (let i = 0; i <= 12; i++) {
                ctx.fillStyle = sakuraColors[i % sakuraColors.length];
                ctx.beginPath();
                ctx.ellipse((cx - targetW / 2) + i * pStep, y + 6 + (i % 2 === 0 ? -2 : 3), 7, 4, i * 0.6, 0, Math.PI * 2);
                ctx.fill();
            }
        }

        // Target badge
        ctx.font = '700 14px Fredoka, Rubik, sans-serif';
        ctx.fillStyle = '#FFFFFF';
        ctx.textAlign = 'center';
        ctx.shadowColor = 'rgba(0,0,0,0.85)';
        ctx.shadowBlur = 6;
        ctx.fillText('TARGET', cx, y + 26);

        ctx.restore();
    }

    const brightenColor = (commonModule && typeof commonModule.brightenColor === 'function')
        ? commonModule.brightenColor
        : function (hex) { return hex || '#FFFFFF'; };

    const drawSeasonalDefaultPenguin = (commonModule && typeof commonModule.drawSeasonalDefaultPenguin === 'function')
        ? commonModule.drawSeasonalDefaultPenguin
        : function () { };

    function drawParachutist(drop, dropAlpha = 1.0, isSessionLeader = false, dtSec = 0.016) {
        const px = drop.x * width;
        const py = drop.y * height;
        const theme = drop.theme || currentTheme;
        const charRadius = Math.max(24, Math.min(width, height) * 0.032);
        const canopyRadius = charRadius * 1.65;
        const canopyOffsetY = -charRadius * 2.1;

        ctx.save();
        ctx.globalAlpha = Math.max(0, Math.min(1.0, dropAlpha));
        ctx.translate(px, py);
        let currentRotation = 0;
        if (!drop.completed && drop.parachuteDestroyed) {
            drop.tumbleAngle = (drop.tumbleAngle || 0) + 16.0 * dtSec;
            currentRotation = drop.tumbleAngle;
        } else {
            currentRotation = drop.tiltAngle || 0;
        }
        ctx.rotate(currentRotation);

        // Dynamic canopy deployment scale (PixelPlush deploys when falling, folds when moving upward or near ceiling)
        let targetCanopyScale = 1.0;
        if (drop.completed || drop.parachuteDestroyed) {
            targetCanopyScale = 0.0;
        } else if (drop.y < 0.08 || (typeof drop.vy === 'number' && drop.vy < 0)) {
            // Fold canopy shut when near ceiling OR when knocked upward against gravity!
            targetCanopyScale = 0.0;
        } else if (drop.y < 0.14) {
            targetCanopyScale = Math.min(1.0, Math.max(0.0, (drop.y - 0.08) / 0.06));
        } else {
            targetCanopyScale = 1.0;
        }

        if (drop.canopyScale === undefined) {
            drop.canopyScale = (drop.y >= 0.10) ? 1.0 : 0.0;
        }

        const prevCanopyScale = drop.canopyScale;
        drop.canopyScale += (targetCanopyScale - drop.canopyScale) * Math.min(1.0, 14.0 * dtSec);

        // Parachute flap/open sound when opening from closed state
        if (prevCanopyScale < 0.05 && drop.canopyScale >= 0.05 && !drop.completed && !drop.parachuteDestroyed) {
            const flapBundle = (drop.sounds && drop.sounds.parachuteFlap) || (SoundManager.getBundle() && SoundManager.getBundle().parachuteFlap);
            if (flapBundle) {
                SoundManager.play(flapBundle, 'parachuteFlap', true);
            } else if ((drop.theme && drop.theme.parachuteFlapSoundUrl) || (currentTheme && currentTheme.parachuteFlapSoundUrl)) {
                playSound((drop.theme && drop.theme.parachuteFlapSoundUrl) || currentTheme.parachuteFlapSoundUrl, 0.65);
            } else {
                const popBundle = (drop.sounds && drop.sounds.parachutePop) || (SoundManager.getBundle() && SoundManager.getBundle().parachutePop);
                if (popBundle) {
                    SoundManager.play(popBundle, 'parachutePop', true);
                } else if ((drop.theme && drop.theme.parachutePopSoundUrl) || (currentTheme && currentTheme.parachutePopSoundUrl)) {
                    playSound((drop.theme && drop.theme.parachutePopSoundUrl) || currentTheme.parachutePopSoundUrl, 0.65);
                }
            }
        }

        // Suspension cords from canopy to character
        if (!drop.completed && !drop.parachuteDestroyed && drop.canopyScale > 0.02) {
            const cScale = drop.canopyScale;
            const scaledCanopyRadius = canopyRadius * cScale;
            const scaledCanopyOffsetY = canopyOffsetY * cScale;

            ctx.strokeStyle = 'rgba(255, 255, 255, 0.85)';
            ctx.lineWidth = Math.max(1, 2 * cScale);
            const cordOffsets = [-0.88, -0.42, 0.42, 0.88];
            for (let i = 0; i < cordOffsets.length; i++) {
                ctx.beginPath();
                ctx.moveTo(cordOffsets[i] * scaledCanopyRadius, scaledCanopyOffsetY);
                ctx.lineTo(cordOffsets[i] * charRadius * 0.45, -charRadius * 0.5);
                ctx.stroke();
            }

            // Parachute canopy
            const customCanopy = getLoadedImage(theme.parachuteCanopyImageUrl);
            if (customCanopy) {
                ctx.drawImage(customCanopy, -scaledCanopyRadius, scaledCanopyOffsetY - scaledCanopyRadius * 0.9, scaledCanopyRadius * 2, scaledCanopyRadius * 1.1);
            } else {
                ctx.save();
                ctx.shadowColor = 'rgba(0,0,0,0.35)';
                ctx.shadowBlur = 10 * cScale;

                // Outer dome
                ctx.fillStyle = theme.primaryColor || '#FF8FA3';
                ctx.beginPath();
                ctx.arc(0, scaledCanopyOffsetY, scaledCanopyRadius, Math.PI, 0, false);
                ctx.closePath();
                ctx.fill();

                // Center stripe panel
                ctx.fillStyle = theme.secondaryColor || '#FFB7C5';
                ctx.beginPath();
                ctx.ellipse(0, scaledCanopyOffsetY, scaledCanopyRadius * 0.48, scaledCanopyRadius, 0, Math.PI, 0, false);
                ctx.closePath();
                ctx.fill();

                ctx.lineWidth = 2.5 * cScale;
                ctx.strokeStyle = '#FFFFFF';
                ctx.beginPath();
                ctx.arc(0, scaledCanopyOffsetY, scaledCanopyRadius, Math.PI, 0, false);
                ctx.stroke();
                ctx.restore();
            }
        }

        // Character or Emote Sprite
        const charImg = getLoadedImage(drop.characterImageUrl);
        if (charImg) {
            ctx.save();
            ctx.shadowColor = isSessionLeader ? 'rgba(255, 215, 0, 0.75)' : 'rgba(0,0,0,0.45)';
            ctx.shadowBlur = isSessionLeader ? 16 : 8;
            ctx.drawImage(charImg, -charRadius, -charRadius, charRadius * 2, charRadius * 2);
            ctx.restore();
        } else {
            drawSeasonalDefaultPenguin(ctx, charRadius * 0.85, (theme.seasonPreset || 'spring').toLowerCase());
        }

        // Dizzy stars orbiting crash-landed character
        if (drop.parachuteDestroyed && drop.completed) {
            const starTime = performance.now() * 0.005;
            ctx.fillStyle = '#FFE066';
            for (let s = 0; s < 3; s++) {
                const sAngle = starTime + (s * Math.PI * 2 / 3);
                const sx = Math.cos(sAngle) * charRadius * 0.9;
                const sy = -charRadius * 0.8 + Math.sin(sAngle) * (charRadius * 0.3);
                ctx.beginPath();
                ctx.arc(sx, sy, 3, 0, Math.PI * 2);
                ctx.fill();
            }
        }

        // Leader Crown above character
        if (isSessionLeader && drop.completed) {
            ctx.save();
            ctx.font = '700 22px "Segoe UI Emoji", Fredoka, sans-serif';
            ctx.textAlign = 'center';
            ctx.shadowColor = '#FFD700';
            ctx.shadowBlur = 10;
            ctx.fillText('👑', 0, -charRadius - 8);
            ctx.restore();
        }

        // High-visibility viewer name (no cramped pill box; heavy outline & drop shadow)
        ctx.rotate(-currentRotation); // Keep username horizontal for readability
        const fontSize = Math.max(12, Math.min(36, drop.nameFontSize || headerConfig.nameFontSize || 18));
        ctx.font = `900 ${fontSize}px Fredoka, Rubik, "Segoe UI", sans-serif`;
        const label = isSessionLeader && drop.completed
            ? `${drop.displayName} (${drop.score} pts)`
            : (drop.completed && drop.score > 0 ? `${drop.displayName} (${drop.score})` : drop.displayName);

        const textY = charRadius + fontSize + 2;
        const strokeWidth = Math.max(4.5, fontSize * 0.26);

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

        // High-contrast text fill: Gold for leader, otherwise vibrant brightened userColor (preserving identity)
        const rawColor = isSessionLeader ? '#FFD700' : (drop.userColor || theme.textColor || '#FFFFFF');
        const nameColor = isSessionLeader ? '#FFD700' : brightenColor(rawColor);
        ctx.fillStyle = nameColor;
        ctx.fillText(label, 0, textY);

        ctx.restore();
    }

    function drawDroplet(droplet) {
        const dx = droplet.x * width;
        const dy = droplet.y * height;
        const rPx = Math.max(12, (droplet.radius || 0.028) * Math.min(width, height));

        ctx.save();
        if (droplet.alpha !== undefined) {
            ctx.globalAlpha = Math.max(0, Math.min(1.0, droplet.alpha));
        }
        ctx.translate(dx, dy);
        ctx.rotate(droplet.rotation);

        const img = getLoadedImage(droplet.imageUrl);
        if (img) {
            ctx.drawImage(img, -rPx, -rPx, rPx * 2, rPx * 2);
        } else {
            const season = (currentTheme.seasonPreset || 'spring').toLowerCase();
            ctx.shadowColor = 'rgba(0,0,0,0.35)';
            ctx.shadowBlur = 8;

            if (season === 'summer') {
                // Tropical water balloon / beach ball
                ctx.fillStyle = '#00B4D8';
                ctx.beginPath();
                ctx.arc(0, 0, rPx, 0, Math.PI * 2);
                ctx.fill();
                ctx.fillStyle = '#FFD166';
                ctx.beginPath();
                ctx.arc(0, 0, rPx * 0.55, 0, Math.PI);
                ctx.fill();
            } else if (season === 'fall') {
                // Autumn acorn / harvest orb
                ctx.fillStyle = '#E76F51';
                ctx.beginPath();
                ctx.arc(0, 0, rPx, 0, Math.PI * 2);
                ctx.fill();
                ctx.fillStyle = '#8D5B4C';
                ctx.beginPath();
                ctx.arc(0, -rPx * 0.35, rPx * 0.85, Math.PI, 0);
                ctx.fill();
            } else if (season === 'winter') {
                // Frosty snowball
                ctx.fillStyle = '#CAF0F8';
                ctx.beginPath();
                ctx.arc(0, 0, rPx, 0, Math.PI * 2);
                ctx.fill();
                ctx.strokeStyle = '#FFFFFF';
                ctx.lineWidth = 2.5;
                ctx.stroke();
            } else {
                // Teardrop / blossom droplet
                ctx.fillStyle = currentTheme.primaryColor || '#60A5FA';
                ctx.beginPath();
                ctx.moveTo(0, -rPx * 1.2);
                ctx.bezierCurveTo(rPx * 1.1, -rPx * 0.2, rPx, rPx, 0, rPx);
                ctx.bezierCurveTo(-rPx, rPx, -rPx * 1.1, -rPx * 0.2, 0, -rPx * 1.2);
                ctx.fill();
                ctx.strokeStyle = '#FFFFFF';
                ctx.lineWidth = 1.8;
                ctx.stroke();
            }
        }

        ctx.restore();
    }

    let lastFrameTime = performance.now();

    function animate(now) {
        try {
            const dtSec = Math.min(0.05, Math.max(0.001, (now - lastFrameTime) / 1000));
            lastFrameTime = now;

        ctx.clearRect(0, 0, width, height);

        const showArena = activeDrops.length > 0
            || activeDroplets.length > 0
            || sessionState.isActive
            || (sessionState.endedAtMs && (now - sessionState.endedAtMs) < 6000)
            || now < targetState.visibleUntil;

        if (showArena) {
            drawAmbientParticles(dtSec);
            drawTargetPlatformBack();
        }

        // Update & draw interactive droplets with bouncy physics & settling
        for (let i = activeDroplets.length - 1; i >= 0; i--) {
            const d = activeDroplets[i];
            if (now < d.spawnTimeMs) continue;

            if (!d.spawned) {
                d.spawned = true;
                d.spawnActualTime = now;
            }

            const r = d.radius || 0.028;

            // If settled, handle settling pause & smooth fade out
            if (d.isSettled) {
                d.settledTimer = (d.settledTimer || 0) + dtSec;
                if (d.settledTimer > 3.5) {
                    d.alpha = Math.max(0, 1.0 - (d.settledTimer - 3.5) / 1.0);
                    if (d.alpha <= 0) {
                        activeDroplets.splice(i, 1);
                        continue;
                    }
                }
            } else {
                // Apply dynamic 2D bouncy physics
                // Gravity & clean kinematic integration (zero air friction, matching Matter.js in PixelPlush)
                d.vy += (d.gravity || 0.32) * dtSec;

                // Position integration
                d.x += d.vx * dtSec;
                d.y += d.vy * dtSec;
                d.rotation += (d.angularVel || 2.0) * dtSec;

                // 1. Boundary bounce: Left and Right walls
                if (d.x < r) {
                    d.x = r;
                    d.vx = Math.abs(d.vx) * (d.restitution || 1.0);
                    d.angularVel = -d.angularVel * 0.7;
                } else if (d.x > 1.0 - r) {
                    d.x = 1.0 - r;
                    d.vx = -Math.abs(d.vx) * (d.restitution || 1.0);
                    d.angularVel = -d.angularVel * 0.7;
                }

                // 2. Target Platform bounce
                const targetHalfW = targetState.targetHalfWidth || 0.09;
                const targetWNorm = targetHalfW * 2;
                const minPadX = (targetWNorm * 0.5) + 24 / width;
                const maxPadX = 1.0 - (targetWNorm * 0.5) - 24 / width;
                const targetCx = minPadX < maxPadX ? Math.max(minPadX, Math.min(maxPadX, targetState.centerX || 0.5)) : 0.5;
                const targetTopY = (height - 58) / height;

                if (d.vy > 0 && d.y + r >= targetTopY && d.y - r <= targetTopY + 0.03) {
                    if (Math.abs(d.x - targetCx) <= targetHalfW + r * 0.5) {
                        d.y = targetTopY - r;
                        if (Math.abs(d.vy) > 0.05) {
                            d.vy = -d.vy * Math.min(0.95, (d.restitution || 1.0));
                            d.vx += (Math.random() - 0.5) * 0.06;
                            d.angularVel = (Math.random() - 0.5) * 8.0;
                        } else {
                            d.vy = 0;
                            d.vx *= 0.75;
                            if (Math.abs(d.vx) < 0.01) {
                                d.vx = 0;
                                d.angularVel = 0;
                                d.isSettled = true;
                            }
                        }
                    }
                }

                // 3. Floor bounce
                const floorY = 0.995;
                if (d.y + r >= floorY) {
                    d.y = floorY - r;
                    if (Math.abs(d.vy) > 0.05) {
                        d.vy = -d.vy * (d.restitution * 0.85);
                        d.vx *= 0.85;
                        d.angularVel = -d.angularVel * 0.6;
                    } else {
                        d.vy = 0;
                        d.vx *= 0.70;
                        if (Math.abs(d.vx) < 0.01) {
                            d.vx = 0;
                            d.angularVel = 0;
                            d.isSettled = true;
                        }
                    }
                }

                // 4. Elastic collision with falling parachutists (character figurine body only, matching original PixelPlush)
                const rPx = Math.max(12, (d.radius || 0.028) * Math.min(width, height));
                const charRadiusPx = Math.max(24, Math.min(width, height) * 0.032);
                const dPxX = d.x * width;
                const dPxY = d.y * height;

                for (let j = 0; j < activeDrops.length; j++) {
                    const drop = activeDrops[j];
                    if (!drop || drop.completed) continue;

                    // Test against player character body (figurine only)
                    const bodyPxX = drop.x * width;
                    const bodyPxY = drop.y * height;
                    const bodyMinDist = charRadiusPx + rPx;
                    const bDx = bodyPxX - dPxX;
                    const bDy = bodyPxY - dPxY;
                    const bDistSq = bDx * bDx + bDy * bDy;
                    if (bDistSq < bodyMinDist * bodyMinDist && bDistSq > 0.0001) {
                        const dist = Math.sqrt(bDistSq);
                        const nx = bDx / dist;
                        const ny = bDy / dist;
                        d.x = (bodyPxX - nx * bodyMinDist) / width;
                        d.y = (bodyPxY - ny * bodyMinDist) / height;

                        const MAX_DROP_VX = 0.65;
                        const relVx = (drop.vx || 0) - d.vx;
                        const relVy = (drop.vy || 0.12) - d.vy;
                        const vDot = relVx * nx + relVy * ny;
                        if (vDot < 0) {
                            const impulse = -(1 + (d.restitution || 1.0)) * vDot;
                            d.vx -= impulse * nx * 0.4;
                            d.vy -= impulse * ny * 0.4;
                            d.angularVel = (Math.random() - 0.5) * 10.0;
                            d.hitCount = (d.hitCount || 0) + 1;

                            if (!drop.completed) {
                                const hasServerCollisions = Array.isArray(drop.collisions) && drop.collisions.length > 0;
                                const canKnock = (!drop.lastDropletHitTime || (now - drop.lastDropletHitTime) > 140) && !hasServerCollisions;
                                if (canKnock) {
                                    drop.lastDropletHitTime = now;
                                    const massFactor = Math.max(0.5, Math.min(6.0, (d.mass || 3.2) / Math.max(0.2, drop.mass || 1.0)));
                                    const bounceScale = Math.max(0.1, Math.min(2.5, drop.bounciness || 0.85)) / 0.85;
                                    const deltaVx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, nx * impulse * 0.85 * massFactor * bounceScale));
                                    const deltaVy = Math.max(-0.25, Math.min(0.40, ny * impulse * 0.65 * massFactor * bounceScale));
                                    drop.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (drop.vx || 0) + deltaVx));
                                    drop.vy = Math.max(-0.15, Math.min(0.55, (drop.vy || 0.12) + deltaVy));
                                    drop.tiltAngle = Math.max(-0.65, Math.min(0.65, drop.vx * 2.2));
                                }
                            }

                            if (currentTheme.dropletHitSoundUrl && (!d.lastSfxTime || (now - d.lastSfxTime) > 180)) {
                                playSound(currentTheme.dropletHitSoundUrl, 0.35);
                                d.lastSfxTime = now;
                            }
                        }
                    }
                }

                // 5. Droplet-to-droplet elastic bouncing
                for (let k = i - 1; k >= 0; k--) {
                    const other = activeDroplets[k];
                    if (now < other.spawnTimeMs || other.isSettled) continue;
                    const oRPx = Math.max(12, (other.radius || 0.028) * Math.min(width, height));
                    const oDx = (other.x - d.x) * width;
                    const oDy = (other.y - d.y) * height;
                    const oMinDist = rPx + oRPx;
                    const oDistSq = oDx * oDx + oDy * oDy;
                    if (oDistSq < oMinDist * oMinDist && oDistSq > 0.0001) {
                        const oDist = Math.sqrt(oDistSq);
                        const nx = oDx / oDist;
                        const ny = oDy / oDist;
                        const overlapPx = Math.min(2.5, 0.5 * (oMinDist - oDist));
                        d.x -= (nx * overlapPx) / width;
                        d.y -= (ny * overlapPx) / height;
                        other.x += (nx * overlapPx) / width;
                        other.y += (ny * overlapPx) / height;

                        const dvx = d.vx - other.vx;
                        const dvy = d.vy - other.vy;
                        const p = (nx * dvx + ny * dvy);
                        if (p > 0) {
                            d.vx -= p * nx * 0.85;
                            d.vy -= p * ny * 0.85;
                            other.vx += p * nx * 0.85;
                            other.vy += p * ny * 0.85;
                        }
                    }
                }
            }

            // Absolute bounds cleanup or max lifetime cap (25s)
            const ageMs = now - (d.spawnActualTime || d.spawnTimeMs);
            if (d.x < -0.2 || d.x > 1.2 || d.y > 1.2 || ageMs > 25000) {
                activeDroplets.splice(i, 1);
                continue;
            }

            drawDroplet(d);
        }

        // Update & draw active rockets
        for (let i = activeRockets.length - 1; i >= 0; i--) {
            const r = activeRockets[i];
            if (now < r.spawnTimeMs) continue;

            if (!r.hasLaunched) {
                r.hasLaunched = true;
                const launchBundle = SoundManager.getBundle() && SoundManager.getBundle().rocketLaunch;
                if (launchBundle) {
                    SoundManager.play(launchBundle, 'rocketLaunch', false);
                } else if (currentTheme && currentTheme.rocketLaunchSoundUrl) {
                    playSound(currentTheme.rocketLaunchSoundUrl, 0.60);
                }
            }

            // Kinematic flight
            r.vy += (r.accelerationY || 0.04) * dtSec;
            r.x += r.vx * dtSec;
            r.y += r.vy * dtSec;

            // Emit trail sparks
            if (!r.sparks) r.sparks = [];
            for (let s = 0; s < 3; s++) {
                r.sparks.push({
                    x: r.x * width + (Math.random() - 0.5) * 4,
                    y: r.y * height + (Math.random() - 0.5) * 4,
                    vx: (Math.random() - 0.5) * 35 - (r.vx * width * 0.15),
                    vy: (Math.random() - 0.5) * 35 - (r.vy * height * 0.15) + 20,
                    size: 1.5 + Math.random() * 2.5,
                    alpha: 1.0,
                    color: Math.random() > 0.4 ? r.color : '#FFFFFF',
                    life: 0,
                    maxLife: 0.35 + Math.random() * 0.25
                });
            }

            // Draw spark trail
            for (let s = r.sparks.length - 1; s >= 0; s--) {
                const sp = r.sparks[s];
                sp.life += dtSec;
                if (sp.life >= sp.maxLife) {
                    r.sparks.splice(s, 1);
                    continue;
                }
                sp.x += sp.vx * dtSec;
                sp.y += sp.vy * dtSec;
                sp.alpha = Math.max(0, 1.0 - (sp.life / sp.maxLife));

                ctx.save();
                ctx.globalAlpha = sp.alpha;
                ctx.fillStyle = sp.color;
                ctx.beginPath();
                ctx.arc(sp.x, sp.y, sp.size, 0, Math.PI * 2);
                ctx.fill();
                ctx.restore();
            }

            // Draw rocket projectile
            const rPxX = r.x * width;
            const rPxY = r.y * height;
            const flightAngle = Math.atan2(r.vy * height, r.vx * width);

            ctx.save();
            ctx.translate(rPxX, rPxY);
            ctx.rotate(flightAngle);

            // Rocket body
            ctx.fillStyle = r.color || '#FF3366';
            ctx.beginPath();
            ctx.roundRect(-12, -4, 20, 8, 3);
            ctx.fill();

            // Nose cone
            ctx.fillStyle = '#FFFFFF';
            ctx.beginPath();
            ctx.moveTo(8, -4);
            ctx.lineTo(16, 0);
            ctx.lineTo(8, 4);
            ctx.closePath();
            ctx.fill();

            // Fins
            ctx.fillStyle = '#FFD166';
            ctx.beginPath();
            ctx.moveTo(-12, -4);
            ctx.lineTo(-16, -8);
            ctx.lineTo(-8, -4);
            ctx.closePath();
            ctx.fill();

            ctx.beginPath();
            ctx.moveTo(-12, 4);
            ctx.lineTo(-16, 8);
            ctx.lineTo(-8, 4);
            ctx.closePath();
            ctx.fill();

            // Engine flame
            ctx.fillStyle = '#FFA500';
            ctx.beginPath();
            ctx.moveTo(-12, -2.5);
            ctx.lineTo(-18 - Math.random() * 8, 0);
            ctx.lineTo(-12, 2.5);
            ctx.closePath();
            ctx.fill();

            ctx.restore();

            // Check detonation condition (fuse reached, hit target ceiling/vicinity, or reached upward apex)
            const targetY = (r.targetY !== undefined && r.targetY !== null) ? r.targetY : 0.10;
            const shouldExplode = (now >= r.fuseTimeMs) || (r.y <= targetY) || (r.y <= 0.04) || (r.vy >= 0 && r.y < 0.85);
            if (shouldExplode) {
                spawnRocketExplosion(rPxX, rPxY, r.color, 45);
                const explodeBundle = SoundManager.getBundle() && SoundManager.getBundle().rocketExplode;
                if (explodeBundle) {
                    SoundManager.play(explodeBundle, 'rocketExplode', false);
                } else if (currentTheme && currentTheme.rocketExplodeSoundUrl) {
                    playSound(currentTheme.rocketExplodeSoundUrl, 0.70);
                }

                // If this rocket detonated near an active parachutist canopy whose parachute was destroyed
                if (r.targetDropToken) {
                    const targetDrop = activeDrops.find(d => d.dropToken === r.targetDropToken);
                    if (targetDrop && targetDrop.parachuteDestroyed && !targetDrop.canopyPopped) {
                        targetDrop.canopyPopped = true;
                        const charRadius = Math.max(24, Math.min(width, height) * 0.032);
                        spawnCanopyFragments(targetDrop.x * width, targetDrop.y * height - charRadius * 2.1, targetDrop.theme || currentTheme);
                        const popBundle = (targetDrop.sounds && targetDrop.sounds.parachutePop) || (SoundManager.getBundle() && SoundManager.getBundle().parachutePop);
                        if (popBundle) {
                            SoundManager.play(popBundle, 'parachutePop', true);
                        } else if ((targetDrop.theme && targetDrop.theme.parachutePopSoundUrl) || (currentTheme && currentTheme.parachutePopSoundUrl)) {
                            playSound((targetDrop.theme && targetDrop.theme.parachutePopSoundUrl) || currentTheme.parachutePopSoundUrl, 0.70);
                        }
                    }
                }

                activeRockets.splice(i, 1);
            }
        }

        // Update & draw rocket explosions
        for (let i = rocketExplosions.length - 1; i >= 0; i--) {
            const p = rocketExplosions[i];
            p.life += dtSec;
            if (p.life >= p.maxLife) {
                rocketExplosions.splice(i, 1);
                continue;
            }
            p.vx *= (p.drag || 0.96);
            p.vy = (p.vy * (p.drag || 0.96)) + (p.gravity || 140) * dtSec;
            p.x += p.vx * dtSec;
            p.y += p.vy * dtSec;
            p.alpha = Math.max(0, 1.0 - (p.life / p.maxLife));

            ctx.save();
            ctx.globalAlpha = p.alpha;
            ctx.fillStyle = p.color;
            ctx.shadowColor = p.color;
            ctx.shadowBlur = 6;
            ctx.beginPath();
            ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
        }

        // Update & draw canopy fragments
        for (let i = canopyFragments.length - 1; i >= 0; i--) {
            const f = canopyFragments[i];
            f.life += dtSec;
            if (f.life >= f.maxLife) {
                canopyFragments.splice(i, 1);
                continue;
            }
            f.x += f.vx * dtSec;
            f.y += f.vy * dtSec;
            f.vy += 120 * dtSec;
            f.rotation += (f.spin || 4) * dtSec;
            f.alpha = Math.max(0, 1.0 - (f.life / f.maxLife));

            ctx.save();
            ctx.globalAlpha = f.alpha;
            ctx.translate(f.x, f.y);
            ctx.rotate(f.rotation);
            ctx.fillStyle = f.color;
            ctx.beginPath();
            ctx.ellipse(0, 0, f.size, f.size * 0.5, 0, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
        }

        // Determine session leader (highest score among landed parachutists on target)
        // Must land on target with score > 0 to qualify for the crown!
        let leaderDropToken = null;
        let maxLandedScore = 0;
        if (sessionState.isActive) {
            for (let k = 0; k < activeDrops.length; k++) {
                const d = activeDrops[k];
                if (d.completed && (d.isHit || d.isBullseye) && (d.score || 0) > maxLandedScore) {
                    maxLandedScore = d.score || 0;
                    leaderDropToken = d.dropToken;
                }
            }
        }

        // Step active parachutists physics (directional descent, wall bounce, tilt)
        for (let i = 0; i < activeDrops.length; i++) {
            const drop = activeDrops[i];
            if (drop.completed) continue;

            const elapsedSec = (now - drop.startTimeMs) / 1000;
            drop.currentTick = Math.max(0, Math.min(drop.totalTicks, elapsedSec * TICK_RATE));

            if (drop.pendingParachuteDestroyed && !drop.parachuteDestroyed) {
                if (drop.freeFallStartTick === undefined || drop.freeFallStartTick === null || drop.currentTick >= drop.freeFallStartTick) {
                    drop.parachuteDestroyed = true;
                    drop.pendingParachuteDestroyed = false;
                }
            }

            // Clamp velocity within physical readable limits
            const MAX_DROP_VX = 0.55;
            const MAX_DROP_VY = 0.45;
            const initialVy = drop.initialVy || 0.12;

            if (drop.parachuteDestroyed) {
                // High-speed comedic free-fall! Terminal velocity vy = 0.98, gravity = 1.10
                drop.vy = Math.min(0.98, (drop.vy || 0.12) + 1.10 * dtSec);
                const decayFreeFallVx = Math.pow(0.994, dtSec * 60);
                drop.vx = Math.max(-MAX_DROP_VX * 1.5, Math.min(MAX_DROP_VX * 1.5, (drop.vx || 0) * decayFreeFallVx));

                if (!drop.canopyPopped) {
                    drop.canopyPopped = true;
                    const charRadius = Math.max(24, Math.min(width, height) * 0.032);
                    spawnCanopyFragments(drop.x * width, drop.y * height - charRadius * 2.1, drop.theme || currentTheme);
                    const popBundle = (drop.sounds && drop.sounds.parachutePop) || (SoundManager.getBundle() && SoundManager.getBundle().parachutePop);
                    if (popBundle) {
                        SoundManager.play(popBundle, 'parachutePop', true);
                    } else if ((drop.theme && drop.theme.parachutePopSoundUrl) || (currentTheme && currentTheme.parachutePopSoundUrl)) {
                        playSound((drop.theme && drop.theme.parachutePopSoundUrl) || currentTheme.parachutePopSoundUrl, 0.70);
                    }
                }
            } else {
                const decayVx = Math.pow(0.998, dtSec * 60);
                drop.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (drop.vx || 0) * decayVx));
                if (drop.vy < initialVy) {
                    // Ballistic upward arc decelerates under gravity back to terminal glide (PixelPlush Matter.js physics)
                    drop.vy = Math.min(initialVy, drop.vy + 1.20 * dtSec);
                } else {
                    const decayVy = Math.pow(0.96, dtSec * 60);
                    drop.vy = Math.max(0.04, Math.min(0.45, initialVy + ((drop.vy || initialVy) - initialVy) * decayVy));
                }
            }

            // Move along current velocity vector (no sinusoidal sway)
            drop.x += drop.vx * dtSec;
            drop.y += drop.vy * dtSec;
            if (drop.y < 0.04) {
                drop.y = 0.04;
                drop.vy = Math.max(0.0, drop.vy);
            }

            // Clean bounce off left / right screen borders with configurable bounciness
            const wallRestitution = Math.max(0.15, Math.min(2.5, (drop.bounciness || 0.85) * 1.05));
            if (drop.x < 0.05) {
                drop.x = 0.05;
                drop.vx = Math.min(MAX_DROP_VX, Math.abs(drop.vx || 0.12) * wallRestitution);
                if (currentTheme.dropSoundUrl && (!drop.lastWallSfx || (now - drop.lastWallSfx) > 200)) {
                    playSound(currentTheme.dropSoundUrl, 0.35);
                    drop.lastWallSfx = now;
                }
            } else if (drop.x > 0.95) {
                drop.x = 0.95;
                drop.vx = -Math.min(MAX_DROP_VX, Math.abs(drop.vx || 0.12) * wallRestitution);
                if (currentTheme.dropSoundUrl && (!drop.lastWallSfx || (now - drop.lastWallSfx) > 200)) {
                    playSound(currentTheme.dropSoundUrl, 0.35);
                    drop.lastWallSfx = now;
                }
            }

            // Interactive Target Rim / Wall Bounce (Pool / Cauldron / Leaf Pile side lip)
            if (!drop.bouncedOffTargetRim && drop.y >= (drop.landingY - 0.038) && drop.y < drop.landingY) {
                const targetCx = typeof targetState.centerX === 'number' ? targetState.centerX : 0.5;
                const dx = drop.x - targetCx;
                const dist = Math.abs(dx);
                const halfW = targetState.targetHalfWidth || 0.09;
                if (dist >= halfW * 0.93 && dist <= halfW * 1.07) {
                    drop.bouncedOffTargetRim = true;
                    if (Math.sign(drop.vx) === Math.sign(dx) && Math.abs(drop.vx) > 0.01) {
                        drop.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, -drop.vx * (drop.bounciness || 0.85))); // Bounced off outer wall!
                    } else {
                        drop.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (dx > 0 ? -1 : 1) * Math.max(0.04, Math.abs(drop.vx) * 0.95 * (drop.bounciness || 0.85)))); // Bounced over lip into the pool!
                    }
                    drop.vy = Math.max(-0.65, drop.vy * Math.max(0.25, Math.min(1.0, 0.65 * (drop.bounciness || 0.85))));
                    if (currentTheme.dropletHitSoundUrl && (!drop.lastWallSfx || (now - drop.lastWallSfx) > 200)) {
                        playSound(currentTheme.dropletHitSoundUrl, 0.4);
                        drop.lastWallSfx = now;
                    }
                }
            }

            // Tilt corresponds directly to lateral flight speed
            const targetTilt = Math.max(-0.65, Math.min(0.65, (drop.vx || 0) * 2.2));
            drop.tiltAngle = (drop.tiltAngle || 0) * 0.88 + targetTilt * 0.12;

            // Trigger any server-recorded collision impulses at their exact tick
            if (Array.isArray(drop.collisions)) {
                drop.triggeredCollisionIndices = drop.triggeredCollisionIndices || new Set();
                for (let c = 0; c < drop.collisions.length; c++) {
                    const col = drop.collisions[c];
                    if (drop.currentTick >= col.tick && !drop.triggeredCollisionIndices.has(c)) {
                        drop.triggeredCollisionIndices.add(c);
                        drop.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (drop.vx || 0) + (col.deltaVx || 0)));
                        const maxAllowedVy = drop.parachuteDestroyed ? 0.98 : 0.45;
                        drop.vy = Math.max(-1.20, Math.min(maxAllowedVy, (drop.vy || initialVy) + (col.deltaVy || 0)));
                        drop.tiltAngle = (col.deltaVx >= 0 ? 1 : -1) * 0.35;
                    }
                }
            }
        }

        // Active Parachuter-to-Parachuter Mid-Air 2D Elastic Bouncing
        // Parachute canopies do not collide with each other; only figurines/emotes bounce when they make physical contact
        const charRadiusPx = Math.max(24, Math.min(width, height) * 0.032);
        const minDistPx = charRadiusPx * 1.85;

        for (let i = 0; i < activeDrops.length; i++) {
            const dropA = activeDrops[i];
            if (dropA.completed) continue;

            for (let j = i + 1; j < activeDrops.length; j++) {
                const dropB = activeDrops[j];
                if (dropB.completed) continue;

                // True pixel distance between figurine centers
                const dxPx = (dropA.x - dropB.x) * width;
                const dyPx = (dropA.y - dropB.y) * height;
                const distSqPx = dxPx * dxPx + dyPx * dyPx;

                if (distSqPx < minDistPx * minDistPx && distSqPx > 0.0001) {
                    const distPx = Math.sqrt(distSqPx);
                    const nx = dxPx / distPx;
                    const ny = dyPx / distPx;

                    // Positional separation in pixel space, capped per frame to prevent teleporting
                    const overlapPx = Math.min(2.5, 0.5 * (minDistPx - distPx));
                    dropA.x += (nx * overlapPx) / width;
                    dropA.y += (ny * overlapPx) / height;
                    dropB.x -= (nx * overlapPx) / width;
                    dropB.y -= (ny * overlapPx) / height;

                    // Elastic bounce impulse (in normalized speed per second)
                    const rvx = (dropA.vx || 0) - (dropB.vx || 0);
                    const rvy = (dropA.vy || 0) - (dropB.vy || 0);
                    const vDot = rvx * nx + rvy * ny;

                    if (vDot < 0) {
                        const MAX_DROP_VX = 0.65;
                        const eBounce = Math.max(0.5, Math.min(2.0, ((dropA.bounciness || 0.85) + (dropB.bounciness || 0.85)) * 0.5));
                        const mA = Math.max(0.1, dropA.mass || 1.0);
                        const mB = Math.max(0.1, dropB.mass || 1.0);
                        const totalMass = mA + mB;
                        const impulse = -(1 + eBounce) * vDot;
                        dropA.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (dropA.vx || 0) + nx * impulse * (mB / totalMass)));
                        dropA.vy = Math.max(-0.95, Math.min(0.55, (dropA.vy || 0.12) + ny * impulse * (mB / totalMass)));
                        dropB.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (dropB.vx || 0) - nx * impulse * (mA / totalMass)));
                        dropB.vy = Math.max(-0.95, Math.min(0.55, (dropB.vy || 0.12) - ny * impulse * (mA / totalMass)));

                        if (currentTheme.dropletHitSoundUrl && (!dropA.lastBounceSfx || (now - dropA.lastBounceSfx) > 150)) {
                            playSound(currentTheme.dropletHitSoundUrl, 0.45);
                            dropA.lastBounceSfx = now;
                        }

                        dropA.tiltAngle = Math.max(-0.65, Math.min(0.65, dropA.vx * 2.2));
                        dropB.tiltAngle = Math.max(-0.65, Math.min(0.65, dropB.vx * 2.2));
                    }
                }
            }
        }

        // Draw and finalize parachutists
        for (let i = activeDrops.length - 1; i >= 0; i--) {
            const drop = activeDrops[i];

            if (!drop.completed && drop.y >= drop.landingY) {
                drop.completed = true;
                drop.y = drop.landingY;
                drop.tiltAngle = 0;
                drop.landedAtMs = now;
                drop.landingX = Math.round(Math.max(0.05, Math.min(0.95, drop.x)) * 10000) / 10000;
                drop.x = drop.landingX;

                // Evaluate landing score (0 - 100 scale)
                const targetCx = typeof targetState.centerX === 'number' ? targetState.centerX : 0.5;
                const distFromCenter = Math.abs(drop.landingX - targetCx);
                const halfW = targetState.targetHalfWidth || 0.09;
                drop.isHit = distFromCenter <= halfW;
                if (drop.isHit) {
                    const ratio = Math.min(1.0, Math.max(0.0, distFromCenter / Math.max(0.0001, halfW)));
                    drop.score = Math.max(1, Math.min(100, Math.round(100.0 - (ratio * 99.0))));
                    drop.isBullseye = drop.score === 100;
                    drop.isWin = true;
                } else {
                    drop.score = 0;
                    drop.isBullseye = false;
                    drop.isWin = false;
                }

                // If this newly landed drop landed on target and beats previous high score, update leader
                if (sessionState.isActive && (drop.isHit || drop.isBullseye) && (drop.score || 0) > maxLandedScore) {
                    maxLandedScore = drop.score || 0;
                    leaderDropToken = drop.dropToken;
                }

                // Send completion token and validated landed X to server
                sendCompletion(drop.dropToken, drop.landingX, drop.parachuteDestroyed);

                // Crash landing outcome resolution
                if (drop.parachuteDestroyed) {
                    drop.crashLanded = true;
                    if (drop.isHit) {
                        spawnCannonballSplash(drop.x * width, drop.y * height);
                        const cannonBundle = (drop.sounds && drop.sounds.cannonball) || (SoundManager.getBundle() && SoundManager.getBundle().cannonball);
                        if (cannonBundle) {
                            SoundManager.play(cannonBundle, 'cannonball', true);
                        } else if ((drop.theme && drop.theme.cannonballSoundUrl) || (currentTheme && currentTheme.cannonballSoundUrl)) {
                            playSound((drop.theme && drop.theme.cannonballSoundUrl) || currentTheme.cannonballSoundUrl, 0.75);
                        }
                    } else {
                        spawnCrashSplat(drop.x * width, drop.y * height);
                        const splatBundle = (drop.sounds && drop.sounds.crashLand) || (SoundManager.getBundle() && SoundManager.getBundle().crashLand);
                        if (splatBundle) {
                            SoundManager.play(splatBundle, 'crashLand', true);
                        } else if ((drop.theme && drop.theme.crashLandSoundUrl) || (currentTheme && currentTheme.crashLandSoundUrl)) {
                            playSound((drop.theme && drop.theme.crashLandSoundUrl) || currentTheme.crashLandSoundUrl, 0.60);
                        }
                    }
                }

                // Celebration or miss popup
                let popupText = '';
                let popupColor = '';

                if (drop.parachuteDestroyed) {
                    if (drop.isBullseye) {
                        popupText = 'CRASH BULLSEYE! +' + drop.score;
                        popupColor = '#FFD166';
                    } else if (drop.isHit) {
                        popupText = drop.score > 0 ? 'CRASH LAND! +' + drop.score : 'CRASH LAND! 0 PTS';
                        popupColor = drop.score > 0 ? '#FFA500' : '#EF4444';
                    } else {
                        popupText = 'SPLAT!';
                        popupColor = '#EF4444';
                    }
                } else {
                    popupText = drop.isBullseye
                        ? 'BULLSEYE! +' + drop.score
                        : (drop.isHit ? 'HIT! +' + drop.score : 'MISS');
                    popupColor = drop.isBullseye
                        ? '#FFD166'
                        : (drop.isHit ? '#6BCB77' : '#94A3B8');
                }

                landingPopups.push({
                    x: drop.x * width,
                    y: drop.y * height - 40,
                    text: popupText,
                    subText: drop.displayName,
                    color: popupColor,
                    alpha: 1.0,
                    createdAt: now
                });

                // Trigger seasonal landing puff when touching down on or near target
                const dropSeason = ((drop.theme && drop.theme.seasonPreset) || (currentTheme && currentTheme.seasonPreset) || 'spring').toLowerCase();
                if ((drop.isHit || drop.isBullseye) && !drop.parachuteDestroyed) {
                    spawnLandingPuff(drop.x * width, drop.y * height, dropSeason, drop.isBullseye);
                }

                if (!drop.parachuteDestroyed) {
                    if (drop.isWin) {
                        spawnSplashBurst(drop.x * width, drop.y * height, '#FFD166', 28, 1.35);
                        const winSound = (drop.sounds && drop.sounds.win) || (SoundManager.getBundle() && SoundManager.getBundle().win);
                        if (winSound) {
                            SoundManager.play(winSound, 'win', true);
                        } else if (drop.theme && drop.theme.winSoundUrl) {
                            playSound(drop.theme.winSoundUrl, 0.55);
                        }
                    } else if (drop.theme && drop.theme.loseSoundUrl) {
                        playSound(drop.theme.loseSoundUrl, 0.45);
                    }
                }
            }

            const isLeader = sessionState.isActive && drop.completed && (drop.isHit || drop.isBullseye) && drop.score > 0 && drop.dropToken === leaderDropToken;
            let dropAlpha = 1.0;
            let shouldRemove = false;

            if (drop.completed) {
                const landedTime = now - drop.landedAtMs;
                if (sessionState.isActive) {
                    if (isLeader) {
                        // Leader stays fully 100% visible on landing pad
                        dropAlpha = 1.0;
                        shouldRemove = false;
                    } else {
                        // Other players dim slightly into background (semi-transparent at 0.40 opacity) so they remain visible throughout the round
                        const dimDelay = 3500;
                        const dimDuration = 2000;
                        if (landedTime > dimDelay) {
                            const t = Math.min(1.0, (landedTime - dimDelay) / dimDuration);
                            dropAlpha = 1.0 - (0.60 * t); // Dims to 0.40 opacity, never vanishes!
                        } else {
                            dropAlpha = 1.0;
                        }
                        shouldRemove = false; // Do not remove players during active session!
                    }
                } else if (sessionState.endedAtMs) {
                    // Round has ended: allow all players to stay visible during post-round fanfare then fade together
                    const postRoundAge = (now - sessionState.endedAtMs) / 1000;
                    if (postRoundAge > 6.0) {
                        shouldRemove = true;
                    } else if (postRoundAge > 3.5) {
                        dropAlpha = Math.max(0.0, 1.0 - ((postRoundAge - 3.5) / 2.5));
                    }
                } else {
                    // Non-timed single drop mode: generous display time then gentle fade
                    const lingerTime = 8000;
                    const fadeTime = 2500;
                    if (landedTime > lingerTime) {
                        dropAlpha = Math.max(0.0, 1.0 - ((landedTime - lingerTime) / fadeTime));
                        if (dropAlpha <= 0.0) {
                            shouldRemove = true;
                        }
                    }
                }
            }

            drawParachutist(drop, dropAlpha, isLeader, dtSec);

            if (shouldRemove) {
                activeDrops.splice(i, 1);
            }
        }

        if (showArena) {
            drawTargetPlatformFront();
        }

        // Update & draw splash/confetti bursts
        for (let i = splashBursts.length - 1; i >= 0; i--) {
            const b = splashBursts[i];
            b.life += dtSec;
            if (b.life >= b.maxLife) {
                splashBursts.splice(i, 1);
                continue;
            }
            b.x += b.vx * dtSec;
            b.y += b.vy * dtSec;
            b.vy += 100 * dtSec;
            b.alpha = 1.0 - (b.life / b.maxLife);

            ctx.save();
            ctx.globalAlpha = Math.max(0, b.alpha);
            ctx.translate(b.x, b.y);

            if (b.shape === 'leaf') {
                ctx.rotate(b.rotation || 0);
                b.rotation += (b.spin || 2) * dtSec;
                ctx.fillStyle = b.color;
                ctx.beginPath();
                ctx.moveTo(0, -b.size);
                ctx.quadraticCurveTo(b.size * 1.1, 0, 0, b.size);
                ctx.quadraticCurveTo(-b.size * 1.1, 0, 0, -b.size);
                ctx.fill();
            } else if (b.shape === 'candies') {
                // Halloween Candy Corn
                ctx.rotate(b.rotation || 0);
                b.rotation += (b.spin || 3) * dtSec;
                const s = b.size;
                // Yellow base
                ctx.fillStyle = '#FFE066';
                ctx.beginPath();
                ctx.moveTo(-s * 0.7, s);
                ctx.lineTo(s * 0.7, s);
                ctx.lineTo(s * 0.45, s * 0.25);
                ctx.lineTo(-s * 0.45, s * 0.25);
                ctx.closePath();
                ctx.fill();
                // Orange center
                ctx.fillStyle = '#FF7518';
                ctx.beginPath();
                ctx.moveTo(-s * 0.45, s * 0.25);
                ctx.lineTo(s * 0.45, s * 0.25);
                ctx.lineTo(s * 0.22, -s * 0.45);
                ctx.lineTo(-s * 0.22, -s * 0.45);
                ctx.closePath();
                ctx.fill();
                // White tip
                ctx.fillStyle = '#FFFFFF';
                ctx.beginPath();
                ctx.moveTo(-s * 0.22, -s * 0.45);
                ctx.lineTo(s * 0.22, -s * 0.45);
                ctx.lineTo(0, -s);
                ctx.closePath();
                ctx.fill();
            } else if (b.shape === 'confetti') {
                // Holiday star sparkle or confetti ribbon
                ctx.rotate(b.rotation || 0);
                b.rotation += (b.spin || 3) * dtSec;
                ctx.fillStyle = b.color;
                ctx.fillRect(-b.size * 0.7, -b.size * 0.35, b.size * 1.4, b.size * 0.7);
            } else if (b.shape === 'petal') {
                ctx.rotate(b.rotation || 0);
                b.rotation += (b.spin || 2) * dtSec;
                ctx.fillStyle = b.color;
                ctx.beginPath();
                ctx.ellipse(0, 0, b.size, b.size * 0.55, 0, 0, Math.PI * 2);
                ctx.fill();
            } else if (b.shape === 'snow') {
                ctx.fillStyle = b.color;
                ctx.shadowColor = 'rgba(255, 255, 255, 0.8)';
                ctx.shadowBlur = 6;
                ctx.beginPath();
                ctx.arc(0, 0, b.size * 0.7, 0, Math.PI * 2);
                ctx.fill();
            } else if (b.shape === 'water') {
                ctx.fillStyle = b.color;
                ctx.beginPath();
                ctx.ellipse(0, 0, b.size * 0.65, b.size * 1.15, 0, 0, Math.PI * 2);
                ctx.fill();
            } else {
                ctx.fillStyle = b.color;
                ctx.beginPath();
                ctx.arc(0, 0, b.size, 0, Math.PI * 2);
                ctx.fill();
            }
            ctx.restore();
        }

        // Update & draw landing score popups
        for (let i = landingPopups.length - 1; i >= 0; i--) {
            const pop = landingPopups[i];
            const age = (now - pop.createdAt) / 1000;
            if (age > 2.4) {
                landingPopups.splice(i, 1);
                continue;
            }
            ctx.save();
            ctx.globalAlpha = Math.max(0, 1.0 - Math.max(0, age - 1.4));
            ctx.font = '700 26px Fredoka, Rubik, sans-serif';
            ctx.textAlign = 'center';
            ctx.shadowColor = 'rgba(0,0,0,0.85)';
            ctx.shadowBlur = 8;
            ctx.fillStyle = pop.color;
            ctx.fillText(pop.text, pop.x, pop.y - age * 36);
            ctx.restore();
        }

            drawSessionBanner(now);
        } catch (err) {
            console.error('[Parachute] Render error:', err);
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
        // Smooth vertical transition towards target Y
        const targetY = typeof headerConfig.yOffset === 'number' ? headerConfig.yOffset : 18;
        currentBannerY += (targetY - currentBannerY) * 0.15;
        const bannerY = Math.round(currentBannerY);

        if (!sessionState.isActive) {
            if (sessionState.endedAtMs) {
                const ageSec = (now - sessionState.endedAtMs) / 1000;
                if (ageSec <= 6.0) {
                    if (headerConfig.showBanner === false) return;

                    // Flash completed round banner and fade out
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
                    ctx.fillText(`Round Closed! Total Drops: ${sessionState.totalDrops}`, bannerCenterX, bannerY + bannerH / 2);
                    ctx.restore();
                    return;
                } else {
                    // Round fanfare has ended: clear drops and reset state so everything disappears cleanly
                    sessionState.endedAtMs = 0;
                    sessionState.totalDrops = 0;
                    return;
                }
            }

            // Free Play mode: only show while drops are actively on screen!
            const hasActiveDrop = activeDrops.some(d => !d.completed || (now - d.landedAtMs) < 6000);
            if (hasActiveDrop && sessionState.totalDrops > 0 && headerConfig.showFreePlayCounter !== false) {
                ctx.save();
                const pillW = 210;
                const pillH = 38;
                const pillX = getBannerX(pillW);

                ctx.fillStyle = 'rgba(15, 23, 42, 0.78)';
                ctx.strokeStyle = currentTheme.primaryColor || '#60A5FA';
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
                ctx.fillText(`Drops: ${sessionState.totalDrops}`, pillX + pillW / 2, bannerY + pillH / 2);
                ctx.restore();
            } else if (!hasActiveDrop && activeDrops.length === 0) {
                sessionState.totalDrops = 0;
            }
            return;
        }

        if (headerConfig.showBanner === false) return;

        const showTimer = headerConfig.showTimer !== false;
        const showCount = headerConfig.showCount !== false;

        // Dynamic banner width based on visible components
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
            bannerText = `Round Ends: ${timeStr}  |  Drops: ${sessionState.totalDrops}`;
        } else if (showTimer) {
            bannerText = `Round Ends: ${timeStr}`;
        } else if (showCount) {
            bannerText = `Drops: ${sessionState.totalDrops}`;
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

    syncAmbientParticles();
    if (typeof window !== 'undefined') {
        window.__activeDrops = activeDrops;
        window.__activeRockets = activeRockets;
        window.__activeDroplets = activeDroplets;
    }
    requestAnimationFrame(animate);
})();

