// Plinko Bounce Overlay Engine
(function () {
    'use strict';

    const canvas = document.getElementById('plinko-canvas');
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
        buckets: [
            { label: 'MISS', score: 0, isWin: false, color: '#64748B' },
            { label: '50 PTS', score: 50, isWin: true, color: '#3B82F6' },
            { label: '100 PTS', score: 100, isWin: true, color: '#10B981' },
            { label: 'JACKPOT', score: 500, isWin: true, color: '#F59E0B' },
            { label: '100 PTS', score: 100, isWin: true, color: '#10B981' },
            { label: '50 PTS', score: 50, isWin: true, color: '#3B82F6' },
            { label: 'MISS', score: 0, isWin: false, color: '#64748B' }
        ],
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

    const activeTokens = [];
    const pegRipples = [];
    const bucketHighlights = [];
    const confettiParticles = [];
    const pendingCompletions = [];

    const socket = window.createWsSocket('plinko', [window.WS_TOPICS.plinko]);

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

        if (msg.type === 'plinko_drop' && msg.board) {
            if (msg.theme) {
                currentTheme = msg.theme;
            }

            const b = msg.board;
            boardConfig.rowCount = b.rowCount || 8;
            if (Array.isArray(b.buckets) && b.buckets.length) {
                boardConfig.buckets = b.buckets;
            }
            const durationSec = b.durationSeconds || 4.5;
            boardConfig.visibleUntil = performance.now() + (durationSec + 4.0) * 1000;

            getLoadedImage(msg.characterImageUrl);
            if (currentTheme.plinkoBoardBackgroundImageUrl) getLoadedImage(currentTheme.plinkoBoardBackgroundImageUrl);
            if (currentTheme.plinkoPegImageUrl) getLoadedImage(currentTheme.plinkoPegImageUrl);

            if (currentTheme.dropSoundUrl) {
                playSound(currentTheme.dropSoundUrl, 0.45);
            }

            const waypoints = Array.isArray(b.waypoints) && b.waypoints.length >= 2
                ? b.waypoints
                : [{ x: 0.5, y: 0.08, row: -1 }, { x: 0.5, y: 0.88, row: boardConfig.rowCount }];

            activeTokens.push({
                dropToken: msg.dropToken,
                username: msg.username || 'viewer',
                displayName: msg.displayName || msg.username || 'Viewer',
                characterImageUrl: msg.characterImageUrl || '',
                theme: msg.theme || currentTheme,
                startTimeMs: performance.now(),
                durationMs: durationSec * 1000,
                waypoints: waypoints,
                winningBucketIndex: b.winningBucketIndex || 0,
                winningBucketLabel: b.winningBucketLabel || '',
                score: b.score || 0,
                isWin: !!b.isWin,
                x: waypoints[0].x,
                y: waypoints[0].y,
                rotation: 0,
                lastRippleSegment: -1,
                completed: false,
                completedAtMs: 0
            });
        } else if (msg.type === 'session_start' && msg.session) {
            const s = msg.session;
            sessionState.isActive = true;
            sessionState.totalDurationSeconds = s.totalDurationSeconds || 100;
            sessionState.startedAtMs = performance.now();
            sessionState.endsAtMs = performance.now() + (s.remainingSeconds || s.totalDurationSeconds || 100) * 1000;
            sessionState.totalDrops = s.totalDrops || 0;
            if (s.theme) currentTheme = s.theme;
        } else if (msg.type === 'session_end' && msg.session) {
            sessionState.isActive = false;
            sessionState.totalDrops = msg.session.totalDrops || sessionState.totalDrops;
            sessionState.endedAtMs = performance.now();
        }

        if (msg.session && sessionState) {
            sessionState.totalDrops = msg.session.totalDrops || sessionState.totalDrops;
            if (!sessionState.isActive && msg.session.isActive) {
                sessionState.isActive = true;
                sessionState.totalDurationSeconds = msg.session.totalDurationSeconds || 100;
                sessionState.startedAtMs = performance.now();
                sessionState.endsAtMs = performance.now() + (msg.session.remainingSeconds || 100) * 1000;
            }
        }
    });

    function drawSeasonalDefaultPenguin(ctx, r, season) {
        ctx.fillStyle = '#2B2D42';
        ctx.beginPath();
        ctx.arc(0, 0, r, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = '#F8F9FA';
        ctx.beginPath();
        ctx.ellipse(0, r * 0.14, r * 0.65, r * 0.72, 0, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = '#111';
        ctx.beginPath();
        ctx.arc(-r * 0.3, -r * 0.2, r * 0.12, 0, Math.PI * 2);
        ctx.arc(r * 0.3, -r * 0.2, r * 0.12, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = '#FFB703';
        ctx.beginPath();
        ctx.moveTo(-r * 0.18, -r * 0.05);
        ctx.lineTo(r * 0.18, -r * 0.05);
        ctx.lineTo(0, r * 0.18);
        ctx.closePath();
        ctx.fill();

        if (season === 'spring') {
            ctx.fillStyle = '#FF8FA3';
            ctx.beginPath();
            ctx.arc(0, -r * 0.85, r * 0.24, 0, Math.PI * 2);
            ctx.fill();
        } else if (season === 'summer') {
            ctx.fillStyle = '#00B4D8';
            ctx.fillRect(-r * 0.55, -r * 0.34, r * 0.44, r * 0.22);
            ctx.fillRect(r * 0.10, -r * 0.34, r * 0.44, r * 0.22);
        } else if (season === 'fall') {
            ctx.fillStyle = '#E76F51';
            ctx.fillRect(-r * 0.72, r * 0.22, r * 1.44, r * 0.24);
        } else if (season === 'winter') {
            ctx.fillStyle = '#48CAE4';
            ctx.beginPath();
            ctx.arc(0, -r * 0.5, r * 0.7, Math.PI, 0);
            ctx.fill();
        }
    }

    function drawBoard(alpha) {
        ctx.save();
        ctx.globalAlpha = alpha;

        const padX = width * 0.06;
        const padY = height * 0.04;
        const boardW = width - padX * 2;
        const boardH = height - padY * 2;

        // Optional custom background image only (transparent background by default)
        const bgImg = getLoadedImage(currentTheme.plinkoBoardBackgroundImageUrl);
        if (bgImg) {
            ctx.drawImage(bgImg, padX, padY, boardW, boardH);
        }

        // Header banner with shadow for legibility over transparent stream background
        ctx.save();
        ctx.font = '700 20px Fredoka, Rubik, sans-serif';
        ctx.fillStyle = currentTheme.accentColor || '#FFE066';
        ctx.textAlign = 'center';
        ctx.shadowColor = 'rgba(0, 0, 0, 0.85)';
        ctx.shadowBlur = 8;
        ctx.fillText('✨ PLINKO BOUNCE ✨', width * 0.5, padY + 34);
        ctx.restore();

        // Peg Grid
        const rows = boardConfig.rowCount || 8;
        const cols = Math.max(6, (boardConfig.buckets ? boardConfig.buckets.length : 7));
        const pegRadius = Math.max(5, Math.min(width, height) * 0.0085);
        const pegImg = getLoadedImage(currentTheme.plinkoPegImageUrl);

        for (let r = 0; r < rows; r++) {
            const progress = (r + 1) / rows;
            const rowY = (0.16 + progress * 0.64) * height;
            const isOdd = (r % 2 === 1);
            const countInRow = isOdd ? cols - 1 : cols;

            for (let c = 0; c < countInRow; c++) {
                const normX = isOdd
                    ? (c + 1.0) / cols
                    : (c + 0.5) / cols;
                const pegX = padX + normX * boardW;

                if (pegImg) {
                    ctx.drawImage(pegImg, pegX - pegRadius * 1.4, rowY - pegRadius * 1.4, pegRadius * 2.8, pegRadius * 2.8);
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

        // Bottom Prize Buckets
        const buckets = boardConfig.buckets || [];
        if (buckets.length > 0) {
            const bucketY = 0.84 * height;
            const bucketH = 0.095 * height;
            const slotW = boardW / buckets.length;

            for (let i = 0; i < buckets.length; i++) {
                const b = buckets[i];
                const isWinSlot = !!b.isWin;
                const bx = padX + i * slotW + 4;
                const bw = slotW - 8;

                ctx.save();
                ctx.globalAlpha = alpha * (isWinSlot ? 1.0 : 0.6);
                ctx.shadowColor = 'rgba(0, 0, 0, 0.6)';
                ctx.shadowBlur = 6;
                ctx.fillStyle = isWinSlot ? (b.color || '#10B981') : '#475569';
                ctx.beginPath();
                ctx.roundRect(bx, bucketY, bw, bucketH, 10);
                ctx.fill();

                ctx.lineWidth = isWinSlot ? 2.5 : 1.5;
                ctx.strokeStyle = isWinSlot ? '#FFFFFF' : 'rgba(255,255,255,0.3)';
                ctx.stroke();

                ctx.fillStyle = '#FFFFFF';
                ctx.font = '700 13px Fredoka, Rubik, sans-serif';
                ctx.textAlign = 'center';
                ctx.fillText(b.label || '', bx + bw / 2, bucketY + bucketH * 0.45);

                ctx.font = '500 11px Fredoka, Rubik, sans-serif';
                ctx.fillStyle = isWinSlot ? 'rgba(255,255,255,0.95)' : 'rgba(255,255,255,0.65)';
                ctx.fillText(isWinSlot ? ((b.score || 0) + ' pts') : 'NO WIN', bx + bw / 2, bucketY + bucketH * 0.76);
                ctx.restore();
            }
        }

        ctx.restore();
    }

    function spawnConfetti(xPx, yPx) {
        const colors = ['#FFD166', '#FF8FA3', '#00B4D8', '#6BCB77', '#FFFFFF'];
        for (let i = 0; i < 30; i++) {
            const angle = -Math.PI * 0.15 - Math.random() * Math.PI * 0.7;
            const speed = 110 + Math.random() * 190;
            confettiParticles.push({
                x: xPx,
                y: yPx,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                size: 5 + Math.random() * 5,
                color: colors[i % colors.length],
                life: 0,
                maxLife: 0.9 + Math.random() * 0.4
            });
        }
    }

    let lastFrameTime = performance.now();

    function animate(now) {
        const dtSec = Math.min(0.05, Math.max(0.001, (now - lastFrameTime) / 1000));
        lastFrameTime = now;

        ctx.clearRect(0, 0, width, height);

        const isVisible = activeTokens.length > 0
            || sessionState.isActive
            || (sessionState.endedAtMs && (now - sessionState.endedAtMs) < 6000)
            || now < boardConfig.visibleUntil;

        if (!isVisible) {
            requestAnimationFrame(animate);
            return;
        }

        const fadeAlpha = (activeTokens.length > 0 || sessionState.isActive || (sessionState.endedAtMs && (now - sessionState.endedAtMs) < 6000))
            ? 1.0
            : Math.min(1.0, Math.max(0.0, (boardConfig.visibleUntil - now) / 800));

        drawBoard(fadeAlpha);

        const padX = width * 0.06;
        const boardW = width - padX * 2;
        const tokenRadius = Math.max(18, Math.min(width, height) * 0.028);

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
            ctx.arc(rp.x, rp.y, 6 + progress * 22, 0, Math.PI * 2);
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

        // Update & draw tokens
        for (let i = activeTokens.length - 1; i >= 0; i--) {
            const t = activeTokens[i];
            const elapsed = now - t.startTimeMs;
            const progress = Math.min(1.0, Math.max(0.0, elapsed / t.durationMs));

            if (!t.completed) {
                const segCount = t.waypoints.length - 1;
                const scaled = progress * segCount;
                const segIdx = Math.min(segCount - 1, Math.floor(scaled));
                const localT = scaled - segIdx;

                const wpA = t.waypoints[segIdx];
                const wpB = t.waypoints[segIdx + 1];

                // Parabolic bounce arc between peg rows
                t.x = wpA.x + (wpB.x - wpA.x) * localT;
                const linearY = wpA.y + (wpB.y - wpA.y) * localT;
                const arcBounce = -Math.sin(localT * Math.PI) * 0.018;
                t.y = linearY + arcBounce;
                t.rotation += (wpB.x >= wpA.x ? 1 : -1) * dtSec * 4.5;

                if (segIdx !== t.lastRippleSegment && segIdx > 0) {
                    t.lastRippleSegment = segIdx;
                    pegRipples.push({
                        x: padX + wpA.x * boardW,
                        y: wpA.y * height,
                        age: 0
                    });
                }

                if (progress >= 1.0) {
                    t.completed = true;
                    t.completedAtMs = now;
                    t.x = t.waypoints[t.waypoints.length - 1].x;
                    t.y = t.waypoints[t.waypoints.length - 1].y;

                    // If this newly landed token sets a new high score in active session, update leader
                    if (sessionState.isActive && (t.score || 0) > maxLandedScore) {
                        maxLandedScore = t.score || 0;
                        leaderDropToken = t.dropToken;
                    }

                    sendCompletion(t.dropToken);

                    const landPxX = padX + t.x * boardW;
                    const landPxY = t.y * height;

                    const highlightText = t.isWin
                        ? ('🎉 ' + t.winningBucketLabel + (t.score > 0 ? ' (+' + t.score + ')' : ''))
                        : ('❌ ' + t.winningBucketLabel);

                    bucketHighlights.push({
                        x: landPxX,
                        y: landPxY - 32,
                        text: highlightText,
                        color: t.isWin ? '#FFD166' : '#94A3B8',
                        createdAt: now
                    });

                    if (t.isWin) {
                        spawnConfetti(landPxX, landPxY);
                        if (t.theme && t.theme.winSoundUrl) playSound(t.theme.winSoundUrl, 0.55);
                    } else if (t.theme && t.theme.loseSoundUrl) {
                        playSound(t.theme.loseSoundUrl, 0.45);
                    }
                }
            }

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

            // Outer glowing coin ring
            ctx.shadowColor = isLeader ? 'rgba(255, 215, 0, 0.75)' : 'rgba(0,0,0,0.5)';
            ctx.shadowBlur = isLeader ? 16 : 10;
            ctx.fillStyle = isLeader ? '#FFD700' : ((t.theme && t.theme.primaryColor) || '#FF8FA3');
            ctx.beginPath();
            ctx.arc(0, 0, tokenRadius, 0, Math.PI * 2);
            ctx.fill();

            ctx.lineWidth = isLeader ? 3.5 : 3;
            ctx.strokeStyle = isLeader ? '#FFF' : '#FFFFFF';
            ctx.stroke();

            ctx.save();
            ctx.beginPath();
            ctx.arc(0, 0, tokenRadius - 3, 0, Math.PI * 2);
            ctx.clip();
            ctx.rotate(t.rotation * 0.35);

            const charImg = getLoadedImage(t.characterImageUrl);
            if (charImg) {
                ctx.drawImage(charImg, -tokenRadius + 3, -tokenRadius + 3, (tokenRadius - 3) * 2, (tokenRadius - 3) * 2);
            } else {
                drawSeasonalDefaultPenguin(ctx, tokenRadius * 0.78, ((t.theme && t.theme.seasonPreset) || 'spring').toLowerCase());
            }
            ctx.restore();

            // DisplayName tag above token with crown for leader
            if (isLeader && t.completed) {
                ctx.save();
                ctx.font = '700 18px "Segoe UI Emoji", Fredoka, sans-serif';
                ctx.textAlign = 'center';
                ctx.shadowColor = '#FFD700';
                ctx.shadowBlur = 10;
                ctx.fillText('👑', 0, -tokenRadius - 24);
                ctx.restore();
            }

            const label = isLeader && t.completed
                ? `👑 ${t.displayName} (${t.score} pts)`
                : (t.completed && t.score > 0 ? `${t.displayName} (${t.score})` : t.displayName);

            ctx.font = '700 13px Fredoka, Rubik, sans-serif';
            ctx.fillStyle = isLeader ? '#FFD700' : '#FFFFFF';
            ctx.textAlign = 'center';
            ctx.shadowColor = isLeader ? 'rgba(0,0,0,0.95)' : 'rgba(0,0,0,0.9)';
            ctx.shadowBlur = 6;
            ctx.fillText(label, 0, -tokenRadius - 8);

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
            ctx.fillText(`⏳ Round Ends: ${timeStr}  |  🪙 Tokens: ${sessionState.totalDrops}`, width / 2, bannerY + bannerH / 2 - 2);
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
            ctx.fillText(`🎉 Round Closed! Total Tokens Dropped: ${sessionState.totalDrops}`, width / 2, bannerY + bannerH / 2);
        }
        ctx.restore();
    }

    requestAnimationFrame(animate);
})();

