// Parachute Drop & Interactive Droplets Overlay Engine
(function () {
    'use strict';

    const canvas = document.getElementById('parachute-canvas');
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

    const imageCache = new Map();
    function getLoadedImage(url) {
        if (!url) return null;
        if (imageCache.has(url)) {
            const entry = imageCache.get(url);
            return entry.loaded ? entry.img : null;
        }
        const img = new Image();
        img.crossOrigin = 'anonymous';
        const entry = { img, loaded: false, error: false };
        img.onload = function () { entry.loaded = true; };
        img.onerror = function () { entry.error = true; };
        img.src = url;
        imageCache.set(url, entry);
        return null;
    }

    function playSound(url, volume = 0.5) {
        if (!url) return;
        try {
            const audio = new Audio(url);
            audio.volume = volume;
            audio.play().catch(function () { });
        } catch (_) { }
    }

    // Active game entities
    const activeDrops = [];
    const activeDroplets = [];
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


    function syncAmbientParticles() {
        const desired = currentTheme && currentTheme.particleType !== 'none'
            ? Math.min(80, Math.max(0, currentTheme.particleDensity || 35))
            : 0;

        while (ambientParticles.length < desired) {
            ambientParticles.push({
                x: Math.random() * width,
                y: Math.random() * height,
                vx: (Math.random() - 0.5) * 35,
                vy: 25 + Math.random() * 45,
                size: 5 + Math.random() * 8,
                angle: Math.random() * Math.PI * 2,
                spin: (Math.random() - 0.5) * 2.2,
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
            const speed = (70 + Math.random() * 140) * scale;
            splashBursts.push({
                x: xPx,
                y: yPx,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed - 40,
                size: (4 + Math.random() * 5) * scale,
                color: color,
                shape: shape,
                rotation: Math.random() * Math.PI * 2,
                spin: (Math.random() - 0.5) * 5,
                alpha: 1.0,
                life: 0,
                maxLife: 0.55 + Math.random() * 0.35
            });
        }
    }

    function spawnLandingPuff(xPx, yPx, seasonPreset, isBullseye = false) {
        const season = (seasonPreset || 'spring').toLowerCase();
        const count = isBullseye ? 36 : 24;
        const scale = isBullseye ? 1.45 : 1.1;

        if (season === 'fall') {
            // Autumn leaf puff
            const leafColors = ['#E76F51', '#F4A261', '#E9C46A', '#D9480F', '#8C3714'];
            for (let i = 0; i < count; i++) {
                const angle = -Math.PI * 0.15 - (Math.random() * Math.PI * 0.7); // Fountains up and outward
                const speed = (90 + Math.random() * 160) * scale;
                splashBursts.push({
                    x: xPx + (Math.random() - 0.5) * 36,
                    y: yPx + (Math.random() - 0.5) * 12,
                    vx: Math.cos(angle) * speed,
                    vy: Math.sin(angle) * speed - 60,
                    size: (6 + Math.random() * 6) * scale,
                    color: leafColors[Math.floor(Math.random() * leafColors.length)],
                    shape: 'leaf',
                    rotation: Math.random() * Math.PI * 2,
                    spin: (Math.random() - 0.5) * 6,
                    alpha: 1.0,
                    life: 0,
                    maxLife: 0.75 + Math.random() * 0.45
                });
            }
        } else if (season === 'spring') {
            // Cherry blossom petal burst
            const petalColors = ['#FF8FA3', '#FFB7C5', '#FFF0F3', '#FF4D6D', '#FFCCD5'];
            for (let i = 0; i < count; i++) {
                const angle = -Math.PI * 0.12 - (Math.random() * Math.PI * 0.76);
                const speed = (80 + Math.random() * 150) * scale;
                splashBursts.push({
                    x: xPx + (Math.random() - 0.5) * 32,
                    y: yPx + (Math.random() - 0.5) * 10,
                    vx: Math.cos(angle) * speed,
                    vy: Math.sin(angle) * speed - 50,
                    size: (5 + Math.random() * 5) * scale,
                    color: petalColors[Math.floor(Math.random() * petalColors.length)],
                    shape: 'petal',
                    rotation: Math.random() * Math.PI * 2,
                    spin: (Math.random() - 0.5) * 5,
                    alpha: 1.0,
                    life: 0,
                    maxLife: 0.75 + Math.random() * 0.4
                });
            }
        } else if (season === 'winter') {
            // Snow powder poof
            const snowColors = ['#FFFFFF', '#E0F2FE', '#BAE6FD', '#F0F9FF'];
            for (let i = 0; i < count; i++) {
                const angle = -Math.PI * 0.1 - (Math.random() * Math.PI * 0.8);
                const speed = (65 + Math.random() * 140) * scale;
                splashBursts.push({
                    x: xPx + (Math.random() - 0.5) * 36,
                    y: yPx + (Math.random() - 0.5) * 10,
                    vx: Math.cos(angle) * speed,
                    vy: Math.sin(angle) * speed - 50,
                    size: (4 + Math.random() * 6) * scale,
                    color: snowColors[Math.floor(Math.random() * snowColors.length)],
                    shape: 'snow',
                    rotation: 0,
                    spin: 0,
                    alpha: 1.0,
                    life: 0,
                    maxLife: 0.65 + Math.random() * 0.4
                });
            }
        } else {
            // Summer pool water splash
            const waterColors = ['#00B4D8', '#48CAE4', '#90E0EF', '#CAF0F8', '#FFFFFF'];
            for (let i = 0; i < count; i++) {
                const angle = -Math.PI * 0.15 - (Math.random() * Math.PI * 0.7);
                const speed = (90 + Math.random() * 180) * scale;
                splashBursts.push({
                    x: xPx + (Math.random() - 0.5) * 34,
                    y: yPx + (Math.random() - 0.5) * 8,
                    vx: Math.cos(angle) * speed,
                    vy: Math.sin(angle) * speed - 70,
                    size: (4 + Math.random() * 5) * scale,
                    color: waterColors[Math.floor(Math.random() * waterColors.length)],
                    shape: 'water',
                    rotation: 0,
                    spin: 0,
                    alpha: 1.0,
                    life: 0,
                    maxLife: 0.6 + Math.random() * 0.35
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
    const pendingCompletions = [];
    const socket = window.createWsSocket('parachute', [window.WS_TOPICS.parachute]);

    function sendCompletion(token) {
        if (!token) return;
        if (socket && socket.readyState === WebSocket.OPEN) {
            try {
                socket.send(JSON.stringify({ dropToken: token }));
                return;
            } catch (_) {}
        }
        if (!pendingCompletions.includes(token)) {
            pendingCompletions.push(token);
        }
    }

    function flushPendingCompletions() {
        if (!socket || socket.readyState !== WebSocket.OPEN) return;
        while (pendingCompletions.length > 0) {
            const token = pendingCompletions.shift();
            try {
                socket.send(JSON.stringify({ dropToken: token }));
            } catch (_) {
                pendingCompletions.unshift(token);
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

    socket.addEventListener('message', function (event) {
        if (!event.data || typeof event.data !== 'string') return;
        if (event.data === 'ping') {
            socket.send('pong');
            return;
        }
        if (!event.data.startsWith('{')) return;
        let msg;
        try {
            msg = JSON.parse(event.data);
        } catch (_) {
            return;
        }

        if (msg.type === 'parachute_drop' && msg.physics) {
            if (msg.theme) {
                currentTheme = msg.theme;
                syncAmbientParticles();
            }

            const p = msg.physics;
            targetState.centerX = p.targetCenterX || 0.5;
            targetState.targetHalfWidth = p.targetHalfWidth || 0.09;
            targetState.bullseyeHalfWidth = p.bullseyeHalfWidth || 0.025;
            targetState.visibleUntil = performance.now() + ((p.durationSeconds || 6.5) + 4.5) * 1000;

            if (currentTheme.dropSoundUrl) {
                playSound(currentTheme.dropSoundUrl, 0.45);
            }

            // Preload custom images if any
            getLoadedImage(msg.characterImageUrl);
            if (currentTheme.parachuteCanopyImageUrl) getLoadedImage(currentTheme.parachuteCanopyImageUrl);
            if (currentTheme.targetPlatformImageUrl) getLoadedImage(currentTheme.targetPlatformImageUrl);

            activeDrops.push({
                dropToken: msg.dropToken,
                username: msg.username || 'viewer',
                displayName: msg.displayName || msg.username || 'Viewer',
                characterImageUrl: msg.characterImageUrl || '',
                theme: msg.theme || currentTheme,
                startTimeMs: performance.now(),
                currentTick: 0,
                totalTicks: p.totalTicks || 390,
                startX: p.startX,
                startY: p.startY,
                landingY: p.landingY || 0.88,
                x: p.startX,
                y: p.startY,
                vx: p.initialVx || 0,
                vy: p.initialVy || 0.12,
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
                collisions: [],
                triggeredCollisionTicks: new Set(),
                tiltAngle: 0,
                completed: false,
                landedAtMs: 0
            });
        } else if (msg.type === 'parachute_droplets') {
            if (msg.theme) {
                currentTheme = msg.theme;
                syncAmbientParticles();
            }

            const nowMs = performance.now();
            targetState.visibleUntil = Math.max(targetState.visibleUntil, nowMs + 5000);

            if (Array.isArray(msg.droplets)) {
                msg.droplets.forEach(function (d) {
                    if (d.imageUrl) getLoadedImage(d.imageUrl);
                    activeDroplets.push({
                        id: d.id,
                        spawnTimeMs: nowMs + (d.spawnTickOffset || 0) * DT * 1000,
                        startX: d.startX,
                        startY: d.startY,
                        x: d.startX,
                        y: d.startY,
                        vx: d.vx,
                        vy: d.vy,
                        gravity: d.gravity || 0.15,
                        radius: d.radius || 0.028,
                        sizeScale: d.sizeScale || 1.0,
                        speedScale: d.speedScale || 1.0,
                        sizeLabel: d.sizeLabel || 'Medium',
                        speedLabel: d.speedLabel || 'Normal',
                        imageUrl: d.imageUrl || '',
                        hit: false,
                        rotation: Math.random() * Math.PI * 2
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
        } else if (msg.type === 'session_start' && msg.session) {
            const s = msg.session;
            sessionState.isActive = true;
            sessionState.totalDurationSeconds = s.totalDurationSeconds || 100;
            sessionState.startedAtMs = performance.now();
            sessionState.endsAtMs = performance.now() + (s.remainingSeconds || s.totalDurationSeconds || 100) * 1000;
            sessionState.totalDrops = s.totalDrops || 0;
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
            sessionState.totalDrops = msg.session.totalDrops || sessionState.totalDrops;
            sessionState.endedAtMs = performance.now();
            if (msg.session.targetCenterX !== undefined && msg.session.targetCenterX !== null) {
                targetState.centerX = msg.session.targetCenterX;
            }
        }

        if (msg.session && sessionState) {
            sessionState.totalDrops = msg.session.totalDrops || sessionState.totalDrops;
            if (msg.session.targetCenterX !== undefined && msg.session.targetCenterX !== null) {
                targetState.centerX = msg.session.targetCenterX;
                if (msg.session.targetHalfWidth) targetState.targetHalfWidth = msg.session.targetHalfWidth;
                if (msg.session.bullseyeHalfWidth) targetState.bullseyeHalfWidth = msg.session.bullseyeHalfWidth;
            }
            if (!sessionState.isActive && msg.session.isActive) {
                sessionState.isActive = true;
                sessionState.totalDurationSeconds = msg.session.totalDurationSeconds || 100;
                sessionState.startedAtMs = performance.now();
                sessionState.endsAtMs = performance.now() + (msg.session.remainingSeconds || 100) * 1000;
            }
        }
    });

    function drawAmbientParticles(dtSec) {
        if (!ambientParticles.length || !currentTheme || currentTheme.particleType === 'none') return;
        const pType = currentTheme.particleType || 'petals';

        ctx.save();
        for (let i = 0; i < ambientParticles.length; i++) {
            const p = ambientParticles[i];
            p.x += p.vx * dtSec;
            p.y += p.vy * dtSec;
            p.angle += p.spin * dtSec;

            if (p.y > height + 20) {
                p.y = -15;
                p.x = Math.random() * width;
            }
            if (p.x < -20) p.x = width + 15;
            if (p.x > width + 20) p.x = -15;

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
            } else {
                // snow / stars
                ctx.fillStyle = '#FFFFFF';
                ctx.beginPath();
                ctx.arc(0, 0, p.size * 0.45, 0, Math.PI * 2);
                ctx.fill();
            }
            ctx.restore();
        }
        ctx.restore();
    }

    function drawTargetPlatform() {
        const targetW = targetState.targetHalfWidth * 2 * width;
        const bullseyeW = targetState.bullseyeHalfWidth * 2 * width;
        // Clamp platform center so the entire pad (including margins and edge borders) is fully visible
        const minPadX = (targetW / 2) + 24;
        const maxPadX = width - (targetW / 2) - 24;
        const rawCx = (targetState.centerX || 0.5) * width;
        const cx = minPadX < maxPadX ? Math.max(minPadX, Math.min(maxPadX, rawCx)) : width / 2;
        const y = 0.895 * height;
        const season = ((currentTheme && currentTheme.seasonPreset) || 'spring').toLowerCase();

        ctx.save();

        const customTargetImg = getLoadedImage(currentTheme.targetPlatformImageUrl);
        if (customTargetImg) {
            ctx.drawImage(customTargetImg, cx - targetW / 2, y - 28, targetW, 56);
        } else if (season === 'fall') {
            // === AUTUMN: Pile of autumn foliage & leaves ===
            ctx.shadowColor = 'rgba(0, 0, 0, 0.45)';
            ctx.shadowBlur = 14;
            ctx.shadowOffsetY = 6;

            // Earthy leaf pile base
            ctx.fillStyle = '#6F4E37';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2, y - 14, targetW, 30, 15);
            ctx.fill();

            // Layered crunchy leaves texture around outer pad
            const leafColors = ['#D9480F', '#E76F51', '#F4A261', '#E9C46A', '#9B2226'];
            const stepX = targetW / 18;
            for (let i = 0; i <= 18; i++) {
                const lx = (cx - targetW / 2) + i * stepX + (i % 2 === 0 ? -3 : 3);
                const ly = y - 10 + (i % 3 === 0 ? -4 : (i % 2 === 0 ? 2 : -1));
                const col = leafColors[i % leafColors.length];
                ctx.save();
                ctx.translate(lx, ly);
                ctx.rotate((i * 0.45) - 0.9);
                ctx.fillStyle = col;
                ctx.beginPath();
                ctx.ellipse(0, 0, stepX * 0.9, 6.5, 0, 0, Math.PI * 2);
                ctx.fill();
                ctx.restore();
            }

            // Inner leaf ring
            const midW = (targetW + bullseyeW) * 0.5;
            ctx.fillStyle = '#E76F51';
            ctx.beginPath();
            ctx.roundRect(cx - midW / 2, y - 10, midW, 22, 11);
            ctx.fill();

            // Bullseye center golden pile
            ctx.fillStyle = '#E9C46A';
            ctx.beginPath();
            ctx.roundRect(cx - bullseyeW / 2, y - 13, bullseyeW, 26, 9);
            ctx.fill();

            ctx.lineWidth = 2.5;
            ctx.strokeStyle = '#FFFFFF';
            ctx.stroke();
        } else if (season === 'spring') {
            // === SPRING: Lush grassy mound with cherry blossoms ===
            ctx.shadowColor = 'rgba(0, 0, 0, 0.4)';
            ctx.shadowBlur = 14;
            ctx.shadowOffsetY = 6;

            // Fresh green spring grass mound
            ctx.fillStyle = '#55A630';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2, y - 14, targetW, 28, 14);
            ctx.fill();

            ctx.lineWidth = 3;
            ctx.strokeStyle = '#FFFFFF';
            ctx.stroke();

            // Scattered sakura cherry blossom petals around the mound
            const sakuraColors = ['#FF8FA3', '#FFCCD5', '#FFF0F3', '#FF4D6D'];
            const petalStep = targetW / 14;
            for (let i = 0; i <= 14; i++) {
                const px = (cx - targetW / 2) + i * petalStep;
                const py = y - 8 + (i % 2 === 0 ? -3 : 2);
                ctx.save();
                ctx.translate(px, py);
                ctx.rotate(i * 0.7);
                ctx.fillStyle = sakuraColors[i % sakuraColors.length];
                ctx.beginPath();
                ctx.ellipse(0, 0, 7, 4, 0, 0, Math.PI * 2);
                ctx.fill();
                ctx.restore();
            }

            // Inner sakura pink ring
            const midW = (targetW + bullseyeW) * 0.5;
            ctx.fillStyle = '#FF8FA3';
            ctx.beginPath();
            ctx.roundRect(cx - midW / 2, y - 10, midW, 20, 10);
            ctx.fill();

            // Center blossom bullseye
            ctx.fillStyle = '#FFE066';
            ctx.beginPath();
            ctx.roundRect(cx - bullseyeW / 2, y - 12, bullseyeW, 24, 8);
            ctx.fill();
        } else if (season === 'winter') {
            // === WINTER: Fluffy snowdrift mound with frosted ice center ===
            ctx.shadowColor = 'rgba(0, 0, 0, 0.35)';
            ctx.shadowBlur = 16;
            ctx.shadowOffsetY = 6;

            // Soft white snow drift mound
            ctx.fillStyle = '#F8FAFC';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2, y - 15, targetW, 30, 15);
            ctx.fill();

            ctx.lineWidth = 3;
            ctx.strokeStyle = '#BAE6FD';
            ctx.stroke();

            // Snow puff bumps along top
            ctx.fillStyle = '#FFFFFF';
            const snowBumps = 10;
            const bumpStep = targetW / snowBumps;
            for (let i = 0; i <= snowBumps; i++) {
                ctx.beginPath();
                ctx.arc((cx - targetW / 2) + i * bumpStep, y - 14, 9, 0, Math.PI * 2);
                ctx.fill();
            }

            // Frosted ice inner ring
            const midW = (targetW + bullseyeW) * 0.5;
            ctx.fillStyle = '#90E0EF';
            ctx.beginPath();
            ctx.roundRect(cx - midW / 2, y - 11, midW, 22, 11);
            ctx.fill();

            // Bullseye center golden star / glow
            ctx.fillStyle = '#FFD166';
            ctx.beginPath();
            ctx.roundRect(cx - bullseyeW / 2, y - 13, bullseyeW, 26, 9);
            ctx.fill();

            ctx.lineWidth = 2.5;
            ctx.strokeStyle = '#FFFFFF';
            ctx.stroke();
        } else {
            // === SUMMER: Refreshing swimming pool & inflatable ring ===
            ctx.shadowColor = 'rgba(0, 0, 0, 0.45)';
            ctx.shadowBlur = 16;
            ctx.shadowOffsetY = 6;

            // Pool deck border (tiles/stone)
            ctx.fillStyle = '#E2E8F0';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2, y - 16, targetW, 32, 16);
            ctx.fill();

            // Pool turquoise water basin
            ctx.fillStyle = '#00B4D8';
            ctx.beginPath();
            ctx.roundRect(cx - targetW / 2 + 5, y - 13, targetW - 10, 26, 13);
            ctx.fill();

            // Gentle pool water ripples
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.65)';
            ctx.lineWidth = 2;
            ctx.beginPath();
            ctx.moveTo(cx - targetW * 0.35, y);
            ctx.bezierCurveTo(cx - targetW * 0.2, y - 4, cx - targetW * 0.1, y + 4, cx, y);
            ctx.bezierCurveTo(cx + targetW * 0.1, y - 4, cx + targetW * 0.2, y + 4, cx + targetW * 0.35, y);
            ctx.stroke();

            // Inner inflatable ring floatie
            const midW = (targetW + bullseyeW) * 0.5;
            ctx.fillStyle = '#FFB703';
            ctx.beginPath();
            ctx.roundRect(cx - midW / 2, y - 10, midW, 20, 10);
            ctx.fill();

            // Center pool bullseye float
            ctx.fillStyle = '#FF006E';
            ctx.beginPath();
            ctx.roundRect(cx - bullseyeW / 2, y - 12, bullseyeW, 24, 8);
            ctx.fill();

            ctx.lineWidth = 2.5;
            ctx.strokeStyle = '#FFFFFF';
            ctx.stroke();
        }

        // Target label with seasonal emoji
        const targetEmoji = season === 'fall' ? '🍂' : (season === 'winter' ? '❄️' : (season === 'summer' ? '🏊' : '🌸'));
        ctx.font = '700 14px Fredoka, Rubik, sans-serif';
        ctx.fillStyle = '#FFFFFF';
        ctx.textAlign = 'center';
        ctx.shadowColor = 'rgba(0,0,0,0.85)';
        ctx.shadowBlur = 6;
        ctx.fillText(`${targetEmoji} TARGET`, cx, y + 32);

        ctx.restore();
    }

    function drawSeasonalDefaultPenguin(ctx, r, season) {
        // Body
        ctx.fillStyle = '#2B2D42';
        ctx.beginPath();
        ctx.arc(0, 0, r, 0, Math.PI * 2);
        ctx.fill();

        // White belly
        ctx.fillStyle = '#F8F9FA';
        ctx.beginPath();
        ctx.ellipse(0, r * 0.14, r * 0.65, r * 0.72, 0, 0, Math.PI * 2);
        ctx.fill();

        // Eyes
        ctx.fillStyle = '#111';
        ctx.beginPath();
        ctx.arc(-r * 0.3, -r * 0.2, r * 0.12, 0, Math.PI * 2);
        ctx.arc(r * 0.3, -r * 0.2, r * 0.12, 0, Math.PI * 2);
        ctx.fill();

        // Beak
        ctx.fillStyle = '#FFB703';
        ctx.beginPath();
        ctx.moveTo(-r * 0.18, -r * 0.05);
        ctx.lineTo(r * 0.18, -r * 0.05);
        ctx.lineTo(0, r * 0.18);
        ctx.closePath();
        ctx.fill();

        // Seasonal accessory
        if (season === 'spring') {
            // Cherry blossom flower on head
            ctx.fillStyle = '#FF8FA3';
            ctx.beginPath();
            ctx.arc(0, -r * 0.92, r * 0.28, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#FFE066';
            ctx.beginPath();
            ctx.arc(0, -r * 0.92, r * 0.11, 0, Math.PI * 2);
            ctx.fill();
        } else if (season === 'summer') {
            // Cool sunglasses
            ctx.fillStyle = '#00B4D8';
            ctx.fillRect(-r * 0.58, -r * 0.36, r * 0.48, r * 0.26);
            ctx.fillRect(r * 0.10, -r * 0.36, r * 0.48, r * 0.26);
            ctx.strokeStyle = '#FFD166';
            ctx.lineWidth = 2;
            ctx.strokeRect(-r * 0.58, -r * 0.36, r * 0.48, r * 0.26);
            ctx.strokeRect(r * 0.10, -r * 0.36, r * 0.48, r * 0.26);
        } else if (season === 'fall') {
            // Cozy autumn scarf
            ctx.fillStyle = '#E76F51';
            ctx.beginPath();
            ctx.roundRect(-r * 0.78, r * 0.22, r * 1.56, r * 0.28, 6);
            ctx.fill();
        } else if (season === 'winter') {
            // Winter beanie hat
            ctx.fillStyle = '#48CAE4';
            ctx.beginPath();
            ctx.arc(0, -r * 0.55, r * 0.75, Math.PI, 0);
            ctx.fill();
            ctx.fillStyle = '#FFFFFF';
            ctx.beginPath();
            ctx.arc(0, -r * 1.25, r * 0.22, 0, Math.PI * 2);
            ctx.fill();
        }
    }

    function drawParachutist(drop, dropAlpha = 1.0, isSessionLeader = false) {
        const px = drop.x * width;
        const py = drop.y * height;
        const theme = drop.theme || currentTheme;
        const charRadius = Math.max(24, Math.min(width, height) * 0.032);
        const canopyRadius = charRadius * 1.65;
        const canopyOffsetY = -charRadius * 2.1;

        ctx.save();
        ctx.globalAlpha = Math.max(0, Math.min(1.0, dropAlpha));
        ctx.translate(px, py);
        ctx.rotate(drop.tiltAngle);

        // Suspension cords from canopy to character
        if (!drop.completed) {
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.85)';
            ctx.lineWidth = 2;
            const cordOffsets = [-0.88, -0.42, 0.42, 0.88];
            for (let i = 0; i < cordOffsets.length; i++) {
                ctx.beginPath();
                ctx.moveTo(cordOffsets[i] * canopyRadius, canopyOffsetY);
                ctx.lineTo(cordOffsets[i] * charRadius * 0.45, -charRadius * 0.5);
                ctx.stroke();
            }

            // Parachute canopy
            const customCanopy = getLoadedImage(theme.parachuteCanopyImageUrl);
            if (customCanopy) {
                ctx.drawImage(customCanopy, -canopyRadius, canopyOffsetY - canopyRadius * 0.9, canopyRadius * 2, canopyRadius * 1.1);
            } else {
                ctx.save();
                ctx.shadowColor = 'rgba(0,0,0,0.35)';
                ctx.shadowBlur = 10;

                // Outer dome
                ctx.fillStyle = theme.primaryColor || '#FF8FA3';
                ctx.beginPath();
                ctx.arc(0, canopyOffsetY, canopyRadius, Math.PI, 0, false);
                ctx.closePath();
                ctx.fill();

                // Center stripe panel
                ctx.fillStyle = theme.secondaryColor || '#FFB7C5';
                ctx.beginPath();
                ctx.ellipse(0, canopyOffsetY, canopyRadius * 0.48, canopyRadius, 0, Math.PI, 0, false);
                ctx.closePath();
                ctx.fill();

                ctx.lineWidth = 2.5;
                ctx.strokeStyle = '#FFFFFF';
                ctx.beginPath();
                ctx.arc(0, canopyOffsetY, canopyRadius, Math.PI, 0, false);
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

        // Leader Crown above character
        if (isSessionLeader && drop.completed) {
            ctx.save();
            ctx.font = '700 20px "Segoe UI Emoji", Fredoka, sans-serif';
            ctx.textAlign = 'center';
            ctx.shadowColor = '#FFD700';
            ctx.shadowBlur = 10;
            ctx.fillText('👑', 0, -charRadius - 6);
            ctx.restore();
        }

        // Viewer DisplayName Pill
        ctx.rotate(-drop.tiltAngle); // Keep username horizontal for readability
        ctx.font = '700 15px Fredoka, Rubik, sans-serif';
        const label = isSessionLeader && drop.completed
            ? `👑 ${drop.displayName} (${drop.score} pts)`
            : (drop.completed && drop.score > 0 ? `${drop.displayName} (${drop.score})` : drop.displayName);
        const textWidth = ctx.measureText(label).width;
        const pillW = textWidth + 18;
        const pillH = 24;
        const pillY = charRadius + 8;

        ctx.fillStyle = isSessionLeader
            ? 'rgba(30, 27, 75, 0.92)'
            : 'rgba(15, 23, 42, 0.82)';
        ctx.beginPath();
        ctx.roundRect(-pillW / 2, pillY, pillW, pillH, 12);
        ctx.fill();

        ctx.strokeStyle = isSessionLeader ? '#FFD700' : (theme.primaryColor || '#FF8FA3');
        ctx.lineWidth = isSessionLeader ? 2.5 : 1.5;
        ctx.stroke();

        ctx.fillStyle = isSessionLeader ? '#FFD700' : (theme.textColor || '#FFFFFF');
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(label, 0, pillY + pillH / 2 + 1);

        ctx.restore();
    }

    function drawDroplet(droplet) {
        const dx = droplet.x * width;
        const dy = droplet.y * height;
        const rPx = Math.max(10, droplet.radius * Math.min(width, height));

        ctx.save();
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
            drawTargetPlatform();
        }

        // Update & draw interactive droplets
        for (let i = activeDroplets.length - 1; i >= 0; i--) {
            const d = activeDroplets[i];
            if (now < d.spawnTimeMs) continue;

            const elapsedSec = (now - d.spawnTimeMs) / 1000;
            d.x = d.startX + d.vx * elapsedSec;
            d.y = d.startY + d.vy * elapsedSec + 0.5 * d.gravity * elapsedSec * elapsedSec;
            d.rotation += dtSec * 2.5 * d.speedScale;

            // Check visual collision with active parachutists
            for (let j = 0; j < activeDrops.length; j++) {
                const drop = activeDrops[j];
                if (drop.completed) continue;
                const dx = drop.x - d.x;
                const dy = drop.y - d.y;
                const minDist = 0.036 + d.radius;
                if (!d.hit && (dx * dx + dy * dy) <= minDist * minDist) {
                    d.hit = true;
                    spawnSplashBurst(drop.x * width, drop.y * height, currentTheme.accentColor || '#FFE066', 14, d.sizeScale);
                    drop.tiltAngle = (d.vx >= 0 ? 1 : -1) * Math.min(0.55, 0.22 * d.sizeScale * d.speedScale);
                    if (currentTheme.dropletHitSoundUrl) {
                        playSound(currentTheme.dropletHitSoundUrl, 0.45);
                    }
                    break;
                }
            }

            if (d.hit || d.y > 1.08 || d.x < -0.12 || d.x > 1.12) {
                activeDroplets.splice(i, 1);
                continue;
            }

            drawDroplet(d);
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

        // Update & draw parachutists
        for (let i = activeDrops.length - 1; i >= 0; i--) {
            const drop = activeDrops[i];
            const elapsedSec = (now - drop.startTimeMs) / 1000;
            const tick = Math.min(drop.totalTicks, elapsedSec * TICK_RATE);
            drop.currentTick = tick;

            if (!drop.completed) {
                const sampled = sampleKeyframes(drop.keyframes, tick);
                if (sampled) {
                    drop.x = sampled.x;
                    drop.y = sampled.y;
                    drop.vx = sampled.vx;
                    drop.vy = sampled.vy;
                }

                // Trigger any server-recorded collision bursts at their exact tick
                if (Array.isArray(drop.collisions)) {
                    for (let c = 0; c < drop.collisions.length; c++) {
                        const col = drop.collisions[c];
                        if (tick >= col.tick && !drop.triggeredCollisionTicks.has(col.tick)) {
                            drop.triggeredCollisionTicks.add(col.tick);
                            spawnSplashBurst(col.x * width, col.y * height, currentTheme.accentColor || '#FFE066', 16, col.sizeScale || 1.0);
                            drop.tiltAngle = (col.deltaVx >= 0 ? 1 : -1) * 0.35;
                        }
                    }
                }

                const swayAngle = Math.sin(elapsedSec * drop.swayFrequency + drop.swayPhase) * 0.12;
                drop.tiltAngle = drop.tiltAngle * 0.92 + (swayAngle + Math.max(-0.35, Math.min(0.35, drop.vx * 1.4))) * 0.08;

                if (tick >= drop.totalTicks || drop.y >= drop.landingY) {
                    drop.completed = true;
                    drop.x = drop.landingX;
                    drop.y = drop.landingY;
                    drop.tiltAngle = 0;
                    drop.landedAtMs = now;

                    // If this newly landed drop landed on target and beats previous high score, update leader
                    if (sessionState.isActive && (drop.isHit || drop.isBullseye) && (drop.score || 0) > maxLandedScore) {
                        maxLandedScore = drop.score || 0;
                        leaderDropToken = drop.dropToken;
                    }

                    // Send completion token to server (queued if reconnecting)
                    sendCompletion(drop.dropToken);

                    // Celebration or miss popup
                    const popupText = drop.isBullseye
                        ? '🎯 BULLSEYE! +' + drop.score
                        : (drop.isHit ? '✨ HIT! +' + drop.score : '💨 MISS');
                    const popupColor = drop.isBullseye
                        ? '#FFD166'
                        : (drop.isHit ? '#6BCB77' : '#94A3B8');

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
                    if (drop.isHit || drop.isBullseye) {
                        spawnLandingPuff(drop.x * width, drop.y * height, dropSeason, drop.isBullseye);
                    }

                    if (drop.isWin) {
                        spawnSplashBurst(drop.x * width, drop.y * height, '#FFD166', 28, 1.35);
                        if (drop.theme && drop.theme.winSoundUrl) playSound(drop.theme.winSoundUrl, 0.55);
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

            drawParachutist(drop, dropAlpha, isLeader);

            if (shouldRemove) {
                activeDrops.splice(i, 1);
            }
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
            b.vy += 260 * dtSec;
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

        requestAnimationFrame(animate);
    }

    function drawSessionBanner(now) {
        if (!sessionState.isActive && (!sessionState.endedAtMs || (now - sessionState.endedAtMs) > 6000)) {
            return;
        }

        ctx.save();
        const bannerW = Math.min(width * 0.72, 480);
        const bannerH = 46;
        const bannerX = (width - bannerW) / 2;
        const bannerY = 18;

        if (sessionState.isActive) {
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
            ctx.font = '700 17px Fredoka, Rubik, sans-serif';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillStyle = '#FFFFFF';
            ctx.shadowColor = 'rgba(0,0,0,0.85)';
            ctx.shadowBlur = 6;
            ctx.fillText(`⏳ Round Ends: ${timeStr}  |  🪂 Drops: ${sessionState.totalDrops}`, width / 2, bannerY + bannerH / 2 - 2);
        } else {
            // Flash completed banner
            const ageSec = (now - sessionState.endedAtMs) / 1000;
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
            ctx.fillText(`🎉 Round Closed! Total Parachutists: ${sessionState.totalDrops}`, width / 2, bannerY + bannerH / 2);
        }
        ctx.restore();
    }

    syncAmbientParticles();
    requestAnimationFrame(animate);
})();

