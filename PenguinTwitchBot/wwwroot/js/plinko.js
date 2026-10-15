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

    function normalizeImageUrl(url) {
        if (!url || typeof url !== 'string') return url;
        if (url.includes('static-cdn.jtvnw.net/emoticons/v2/')) {
            return url.replace('/static/', '/default/');
        }
        return url;
    }

    function detectMimeType(buffer) {
        if (!buffer || buffer.byteLength < 12) return 'image/gif';
        const bytes = new Uint8Array(buffer.slice(0, 12));
        if (bytes[0] === 0x47 && bytes[1] === 0x49 && bytes[2] === 0x46) return 'image/gif';
        if (bytes[0] === 0x52 && bytes[1] === 0x49 && bytes[2] === 0x46 && bytes[3] === 0x46 &&
            bytes[8] === 0x57 && bytes[9] === 0x45 && bytes[10] === 0x42 && bytes[11] === 0x50) return 'image/webp';
        if (bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4E && bytes[3] === 0x47) return 'image/png';
        return 'image/gif';
    }

    async function loadAnimatedFrames(url, entry) {
        try {
            let fetchUrl = url;
            if (url.startsWith('http://') || url.startsWith('https://')) {
                try {
                    const parsed = new URL(url);
                    if (parsed.hostname === window.location.hostname || parsed.hostname === 'localhost' || parsed.hostname === '127.0.0.1') {
                        fetchUrl = parsed.pathname + parsed.search;
                    } else {
                        fetchUrl = `/api/chat/proxy-image?url=${encodeURIComponent(url)}`;
                    }
                } catch (_) {
                    fetchUrl = `/api/chat/proxy-image?url=${encodeURIComponent(url)}`;
                }
            }

            const resp = await fetch(fetchUrl);
            if (!resp.ok) {
                console.warn('[Plinko] Image fetch returned status', resp.status, 'for', fetchUrl);
                return;
            }
            const buffer = await resp.arrayBuffer();
            const headerType = resp.headers.get('content-type');
            const mimeType = (headerType && headerType.startsWith('image/'))
                ? headerType.split(';')[0].trim()
                : detectMimeType(buffer);

            // 1. WebCodecs ImageDecoder (fast GPU hardware-decoded frames)
            if (typeof window.ImageDecoder !== 'undefined') {
                try {
                    let isSupported = true;
                    if (typeof ImageDecoder.isTypeSupported === 'function') {
                        isSupported = await ImageDecoder.isTypeSupported(mimeType);
                    }

                    if (isSupported) {
                        const decoder = new ImageDecoder({
                            data: buffer,
                            type: mimeType,
                            preferAnimation: true
                        });

                        // Standard WebCodecs Promise is decoder.tracks.ready
                        await decoder.tracks.ready;

                        if (decoder.completed) {
                            try { await decoder.completed; } catch (_) { }
                        }

                        const track = decoder.tracks.selectedTrack;
                        let frameCount = track ? track.frameCount : 1;
                        const isAnimated = track ? track.animated : (frameCount > 1);

                        if (isAnimated && frameCount > 1) {
                            const frames = [];
                            let totalDurationMs = 0;
                            for (let i = 0; i < frameCount; i++) {
                                const frameResult = await decoder.decode({ frameIndex: i });
                                const durationMs = Math.max(20, (frameResult.image.duration ? frameResult.image.duration / 1000 : 100));
                                const bmp = await createImageBitmap(frameResult.image);
                                frameResult.image.close();
                                frames.push({ bmp, duration: durationMs });
                                totalDurationMs += durationMs;
                            }

                            if (frames.length > 1) {
                                entry.frames = frames;
                                entry.totalDurationMs = totalDurationMs;
                                entry.isAnimated = true;
                                console.log('[Plinko] Successfully decoded ' + frames.length + ' frames via WebCodecs for ' + url);
                                decoder.close();
                                return;
                            }
                        }
                        decoder.close();
                    }
                } catch (decErr) {
                    console.warn('[Plinko] WebCodecs decode failed for ' + url + ', trying GIF fallback:', decErr);
                }
            }

            // 2. Pure-JS GIF parser fallback (works universally in all OBS / browser versions)
            if (mimeType === 'image/gif' || detectMimeType(buffer) === 'image/gif') {
                try {
                    const gifFrames = await parseAndDecodeGif(buffer);
                    if (gifFrames && gifFrames.length > 1) {
                        entry.frames = gifFrames;
                        entry.totalDurationMs = gifFrames.reduce((acc, f) => acc + f.duration, 0);
                        entry.isAnimated = true;
                        console.log('[Plinko] Successfully decoded ' + gifFrames.length + ' frames via JS GIF decoder for ' + url);
                        return;
                    }
                } catch (gifErr) {
                    console.warn('[Plinko] JS GIF decode failed for ' + url + ':', gifErr);
                }
            }
        } catch (err) {
            console.warn('[Plinko] loadAnimatedFrames error for ' + url + ':', err);
        }
    }

    async function parseAndDecodeGif(arrayBuffer) {
        const u8 = new Uint8Array(arrayBuffer);
        if (u8.length < 14) return null;
        if (u8[0] !== 0x47 || u8[1] !== 0x49 || u8[2] !== 0x46) return null; // "GIF"

        let p = 6;
        const screenWidth = u8[p] | (u8[p + 1] << 8); p += 2;
        const screenHeight = u8[p] | (u8[p + 1] << 8); p += 2;
        if (screenWidth <= 0 || screenHeight <= 0) return null;

        const packed = u8[p++];
        const gctFlag = (packed & 0x80) !== 0;
        const gctSize = 1 << ((packed & 0x07) + 1);
        p++; // bgIndex
        p++; // pixel aspect ratio

        let globalPalette = null;
        if (gctFlag) {
            globalPalette = new Uint32Array(gctSize);
            for (let i = 0; i < gctSize; i++) {
                const r = u8[p++];
                const g = u8[p++];
                const b = u8[p++];
                globalPalette[i] = (255 << 24) | (b << 16) | (g << 8) | r;
            }
        }

        const rawFrames = [];
        let disposalMethod = 0;
        let transparentIndex = -1;
        let delayMs = 100;

        while (p < u8.length) {
            const blockType = u8[p++];
            if (blockType === 0x3B) break; // Trailer

            if (blockType === 0x21) {
                const extLabel = u8[p++];
                if (extLabel === 0xF9) {
                    p++; // block size 4
                    const gcPacked = u8[p++];
                    disposalMethod = (gcPacked >> 2) & 0x07;
                    const hasTransparency = (gcPacked & 0x01) !== 0;
                    const rawDelay = u8[p] | (u8[p + 1] << 8); p += 2;
                    delayMs = Math.max(20, rawDelay * 10);
                    const transIdx = u8[p++];
                    transparentIndex = hasTransparency ? transIdx : -1;
                    p++; // terminator 0x00
                } else {
                    while (p < u8.length) {
                        const subSize = u8[p++];
                        if (subSize === 0) break;
                        p += subSize;
                    }
                }
            } else if (blockType === 0x2C) {
                const left = u8[p] | (u8[p + 1] << 8); p += 2;
                const top = u8[p] | (u8[p + 1] << 8); p += 2;
                const width = u8[p] | (u8[p + 1] << 8); p += 2;
                const height = u8[p] | (u8[p + 1] << 8); p += 2;
                const imgPacked = u8[p++];
                const lctFlag = (imgPacked & 0x80) !== 0;
                const lctSize = lctFlag ? (1 << ((imgPacked & 0x07) + 1)) : 0;
                let activePalette = globalPalette;

                if (lctFlag) {
                    activePalette = new Uint32Array(lctSize);
                    for (let i = 0; i < lctSize; i++) {
                        const r = u8[p++];
                        const g = u8[p++];
                        const b = u8[p++];
                        activePalette[i] = (255 << 24) | (b << 16) | (g << 8) | r;
                    }
                }

                if (!activePalette) return null;

                const minCodeSize = u8[p++];
                const lzwData = [];
                while (p < u8.length) {
                    const subSize = u8[p++];
                    if (subSize === 0) break;
                    for (let i = 0; i < subSize; i++) lzwData.push(u8[p++]);
                }

                const pixelCount = width * height;
                const pixels = new Uint8Array(pixelCount);
                decodeLzw(minCodeSize, lzwData, pixels);

                rawFrames.push({
                    left, top, width, height,
                    pixels,
                    palette: activePalette,
                    transparentIndex,
                    disposalMethod,
                    delayMs
                });

                disposalMethod = 0;
                transparentIndex = -1;
                delayMs = 100;
            }
        }

        if (rawFrames.length <= 1) return null;

        const compCanvas = document.createElement('canvas');
        compCanvas.width = screenWidth;
        compCanvas.height = screenHeight;
        const compCtx = compCanvas.getContext('2d', { willReadFrequently: true });

        const prevCanvas = document.createElement('canvas');
        prevCanvas.width = screenWidth;
        prevCanvas.height = screenHeight;
        const prevCtx = prevCanvas.getContext('2d', { willReadFrequently: true });

        const resultFrames = [];

        for (let i = 0; i < rawFrames.length; i++) {
            const rf = rawFrames[i];

            if (rf.disposalMethod === 3) {
                prevCtx.clearRect(0, 0, screenWidth, screenHeight);
                prevCtx.drawImage(compCanvas, 0, 0);
            }

            const frameImgData = compCtx.createImageData(rf.width, rf.height);
            const data32 = new Uint32Array(frameImgData.data.buffer);

            for (let px = 0; px < rf.pixels.length; px++) {
                const colorIdx = rf.pixels[px];
                if (colorIdx === rf.transparentIndex) {
                    data32[px] = 0;
                } else {
                    data32[px] = rf.palette[colorIdx] || 0;
                }
            }

            const patchCanvas = document.createElement('canvas');
            patchCanvas.width = rf.width;
            patchCanvas.height = rf.height;
            patchCanvas.getContext('2d').putImageData(frameImgData, 0, 0);

            compCtx.drawImage(patchCanvas, rf.left, rf.top);

            const bmp = await createImageBitmap(compCanvas);
            resultFrames.push({ bmp, duration: rf.delayMs });

            if (rf.disposalMethod === 2) {
                compCtx.clearRect(rf.left, rf.top, rf.width, rf.height);
            } else if (rf.disposalMethod === 3) {
                compCtx.clearRect(0, 0, screenWidth, screenHeight);
                compCtx.drawImage(prevCanvas, 0, 0);
            }
        }

        return resultFrames;
    }

    function decodeLzw(minCodeSize, inputBytes, outputPixels) {
        const clearCode = 1 << minCodeSize;
        const eoiCode = clearCode + 1;
        let codeSize = minCodeSize + 1;
        let codeMask = (1 << codeSize) - 1;

        const dictPrefix = new Int32Array(4096);
        const dictSuffix = new Uint8Array(4096);

        function initDict() {
            for (let i = 0; i < clearCode; i++) {
                dictPrefix[i] = -1;
                dictSuffix[i] = i;
            }
        }
        initDict();

        let dictEntries = eoiCode + 1;
        let bytePos = 0;
        let bitBuffer = 0;
        let bitCount = 0;
        let outPos = 0;
        let prevCode = -1;
        const stack = new Uint8Array(4096);

        while (bytePos < inputBytes.length && outPos < outputPixels.length) {
            while (bitCount < codeSize && bytePos < inputBytes.length) {
                bitBuffer |= (inputBytes[bytePos++] << bitCount);
                bitCount += 8;
            }

            if (bitCount < codeSize) break;

            const code = bitBuffer & codeMask;
            bitBuffer >>= codeSize;
            bitCount -= codeSize;

            if (code === clearCode) {
                codeSize = minCodeSize + 1;
                codeMask = (1 << codeSize) - 1;
                dictEntries = eoiCode + 1;
                prevCode = -1;
                initDict();
                continue;
            }

            if (code === eoiCode) break;

            let stackPtr = 0;
            if (code < dictEntries) {
                let cur = code;
                while (cur >= 0) {
                    stack[stackPtr++] = dictSuffix[cur];
                    cur = dictPrefix[cur];
                }
            } else if (code === dictEntries && prevCode >= 0) {
                let cur = prevCode;
                stack[stackPtr++] = dictSuffix[cur];
                while (cur >= 0) {
                    stack[stackPtr++] = dictSuffix[cur];
                    cur = dictPrefix[cur];
                }
                const firstByte = stack[stackPtr - 1];
                stack[0] = firstByte;
            } else {
                break;
            }

            for (let i = stackPtr - 1; i >= 0 && outPos < outputPixels.length; i--) {
                outputPixels[outPos++] = stack[i];
            }

            if (prevCode >= 0 && dictEntries < 4096) {
                dictPrefix[dictEntries] = prevCode;
                dictSuffix[dictEntries] = stack[stackPtr - 1];
                dictEntries++;

                if (dictEntries > codeMask && codeSize < 12) {
                    codeSize++;
                    codeMask = (1 << codeSize) - 1;
                }
            }

            prevCode = code;
        }
    }

    function getLoadedImage(rawUrl) {
        const url = normalizeImageUrl(rawUrl);
        if (!url) return null;
        let entry = imageCache.get(url);
        if (!entry) {
            const img = new Image();
            entry = { img, loaded: false, error: false, frames: null, totalDurationMs: 0, isAnimated: false };
            img.onload = function () { entry.loaded = true; };
            img.onerror = function () { entry.error = true; };
            img.src = url;
            imageCache.set(url, entry);
            loadAnimatedFrames(url, entry);
            return null;
        }

        if (entry.isAnimated && entry.frames && entry.frames.length > 1) {
            const now = performance.now();
            let elapsed = now % entry.totalDurationMs;
            for (let i = 0; i < entry.frames.length; i++) {
                if (elapsed < entry.frames[i].duration) {
                    return entry.frames[i].bmp;
                }
                elapsed -= entry.frames[i].duration;
            }
            return entry.frames[entry.frames.length - 1].bmp;
        }

        return entry.loaded ? entry.img : null;
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
        pegTransparency: 15,
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

    try {
        const urlParams = new URLSearchParams(window.location.search);
        if (urlParams.has('pegTransparency')) {
            const parsed = parseInt(urlParams.get('pegTransparency'), 10);
            if (!isNaN(parsed)) {
                boardConfig.pegTransparency = Math.max(0, Math.min(100, parsed));
            }
        }
    } catch (_) {}

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

    function sendCompletion(token, winningBucketIndex) {
        if (!token) return;
        const payload = { dropToken: token };
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
        }
    }

    function flushPendingCompletions() {
        if (!socket || socket.readyState !== WebSocket.OPEN) return;
        while (pendingCompletions.length > 0) {
            const item = pendingCompletions.shift();
            try {
                const payload = { dropToken: item.token };
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
            if (typeof b.pegTransparency === 'number') {
                boardConfig.pegTransparency = Math.max(0, Math.min(100, b.pegTransparency));
            }
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
                rotation: (Math.random() - 0.5) * Math.PI,
                spinSpeed: 3.5 + Math.random() * 3.5,
                bounceAmp: 0.016 + Math.random() * 0.010,
                lastRippleSegment: -1,
                completed: false,
                completedAtMs: 0
            });
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
            sessionState.isActive = true;
            sessionState.totalDurationSeconds = s.totalDurationSeconds || 100;
            sessionState.startedAtMs = performance.now();
            sessionState.endsAtMs = performance.now() + (s.remainingSeconds || s.totalDurationSeconds || 100) * 1000;
            if (typeof s.totalDrops === 'number') {
                sessionState.totalDrops = s.totalDrops;
            }
            if (s.theme) currentTheme = s.theme;
            if (msg.board) {
                if (typeof msg.board.rowCount === 'number') boardConfig.rowCount = msg.board.rowCount;
                if (typeof msg.board.pegTransparency === 'number') {
                    boardConfig.pegTransparency = Math.max(0, Math.min(100, msg.board.pegTransparency));
                }
                if (Array.isArray(msg.board.buckets) && msg.board.buckets.length) {
                    boardConfig.buckets = msg.board.buckets;
                }
            }
        } else if (msg.type === 'session_end' && msg.session) {
            sessionState.isActive = false;
            if (typeof msg.session.totalDrops === 'number') {
                sessionState.totalDrops = msg.session.totalDrops;
            }
            sessionState.endedAtMs = performance.now();
        }

        if (msg.session && sessionState) {
            if (typeof msg.session.totalDrops === 'number') {
                sessionState.totalDrops = msg.session.totalDrops;
            }
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
        } else if (season === 'halloween') {
            // Spooky Witch Hat
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
            // Festive Santa Hat
            ctx.fillStyle = '#D90429';
            ctx.beginPath();
            ctx.moveTo(-r * 0.68, -r * 0.65);
            ctx.lineTo(r * 0.68, -r * 0.65);
            ctx.quadraticCurveTo(r * 0.85, -r * 1.35, r * 0.45, -r * 1.45);
            ctx.quadraticCurveTo(r * 0.1, -r * 1.15, -r * 0.68, -r * 0.65);
            ctx.fill();
            ctx.fillStyle = '#FFFFFF';
            ctx.beginPath();
            ctx.roundRect(-r * 0.74, -r * 0.75, r * 1.48, r * 0.26, 6);
            ctx.fill();
            ctx.beginPath();
            ctx.arc(r * 0.52, -r * 1.46, r * 0.24, 0, Math.PI * 2);
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
        const pegTransparencyLevel = typeof boardConfig.pegTransparency === 'number'
            ? Math.max(0, Math.min(100, boardConfig.pegTransparency))
            : 15;
        const pegAlphaFactor = pegTransparencyLevel / 100;

        if (pegAlphaFactor > 0) {
            ctx.save();
            ctx.globalAlpha = alpha * pegAlphaFactor;

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

            ctx.restore();
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

        // Update token positions along waypoints with deflection
        for (let i = 0; i < activeTokens.length; i++) {
            const t = activeTokens[i];
            if (t.completed) continue;

            const elapsed = now - t.startTimeMs;
            const progress = Math.min(1.0, Math.max(0.0, elapsed / t.durationMs));
            t.progress = progress;

            const segCount = t.waypoints.length - 1;
            const scaled = progress * segCount;
            const segIdx = Math.min(segCount - 1, Math.floor(scaled));
            const localT = scaled - segIdx;

            const wpA = t.waypoints[segIdx];
            const wpB = t.waypoints[segIdx + 1];

            // Base position along waypoints
            const baseX = wpA.x + (wpB.x - wpA.x) * localT;
            const linearY = wpA.y + (wpB.y - wpA.y) * localT;
            const arcBounce = -Math.sin(localT * Math.PI) * (t.bounceAmp || 0.020);
            const baseY = linearY + arcBounce;

            // Apply physical deflection offset from collisions
            t.offsetX = (t.offsetX || 0) + (t.vx || 0) * dtSec;
            t.offsetY = (t.offsetY || 0) + (t.vy || 0) * dtSec;
            t.vx = (t.vx || 0) * 0.94;
            t.vy = (t.vy || 0) * 0.94;

            const bucketCount = (boardConfig.buckets && boardConfig.buckets.length) ? boardConfig.buckets.length : 7;
            const halfBucketW = 0.5 / bucketCount;
            const minTokenX = halfBucketW;
            const maxTokenX = 1.0 - halfBucketW;

            t.x = Math.max(minTokenX, Math.min(maxTokenX, baseX + (t.offsetX || 0)));
            t.y = baseY + (t.offsetY || 0);

            t.rotation += ((wpB.x >= wpA.x ? 1 : -1) * (t.spinSpeed || 5.0) + (t.vx || 0) * 15) * dtSec;

            if (segIdx !== t.lastRippleSegment && segIdx > 0) {
                t.lastRippleSegment = segIdx;
                pegRipples.push({
                    x: padX + wpA.x * boardW,
                    y: wpA.y * height,
                    age: 0
                });
            }
        }

        // Active Token-to-Token 2D Bouncing
        const tokenRadiusNorm = (tokenRadius * 1.05) / boardW;
        for (let i = 0; i < activeTokens.length; i++) {
            const tA = activeTokens[i];
            if (tA.completed) continue;

            for (let j = i + 1; j < activeTokens.length; j++) {
                const tB = activeTokens[j];
                if (tB.completed) continue;

                const dx = tA.x - tB.x;
                const dy = (tA.y - tB.y) * (height / boardW);
                const distSq = dx * dx + dy * dy;
                const minDist = tokenRadiusNorm * 2.0;

                if (distSq < minDist * minDist && distSq > 0.000001) {
                    const dist = Math.sqrt(distSq);
                    const nx = dx / dist;
                    const ny = dy / dist;

                    const bucketCount = (boardConfig.buckets && boardConfig.buckets.length) ? boardConfig.buckets.length : 7;
                    const halfBucketW = 0.5 / bucketCount;
                    const minTokenX = halfBucketW;
                    const maxTokenX = 1.0 - halfBucketW;

                    // Positional separation
                    const overlap = 0.5 * (minDist - dist);
                    tA.offsetX = (tA.offsetX || 0) + nx * overlap;
                    tB.offsetX = (tB.offsetX || 0) - nx * overlap;
                    tA.x = Math.max(minTokenX, Math.min(maxTokenX, tA.x + nx * overlap));
                    tB.x = Math.max(minTokenX, Math.min(maxTokenX, tB.x - nx * overlap));

                    // Elastic bounce impulse
                    const rvx = (tA.vx || 0) - (tB.vx || 0);
                    const rvy = (tA.vy || 0) - (tB.vy || 0);
                    const vDot = rvx * nx + rvy * ny;

                    const impulse = 0.32 + Math.abs(vDot) * 0.45;
                    tA.vx = (tA.vx || 0) + nx * impulse;
                    tA.vy = (tA.vy || 0) + ny * impulse * 0.4;
                    tB.vx = (tB.vx || 0) - nx * impulse;
                    tB.vy = (tB.vy || 0) - ny * impulse * 0.4;

                    tA.spinSpeed = (tA.spinSpeed || 5.0) + (Math.random() - 0.5) * 8.0;
                    tB.spinSpeed = (tB.spinSpeed || 5.0) + (Math.random() - 0.5) * 8.0;

                    // Clink SFX (debounced)
                    if (tA.theme && tA.theme.dropSoundUrl && (!tA.lastClink || (now - tA.lastClink) > 140)) {
                        playSound(tA.theme.dropSoundUrl, 0.45);
                        tA.lastClink = now;
                    }
                }
            }
        }

        // Draw and finalize tokens
        for (let i = activeTokens.length - 1; i >= 0; i--) {
            const t = activeTokens[i];

            if (!t.completed && ((t.progress || 0) >= 1.0 || t.y >= 0.88)) {
                t.completed = true;
                t.completedAtMs = now;
                t.y = 0.88;

                const buckets = boardConfig.buckets || [];
                let landedBucketIdx;
                if (typeof t.winningBucketIndex === 'number' && buckets.length > 0) {
                    landedBucketIdx = Math.max(0, Math.min(buckets.length - 1, t.winningBucketIndex));
                } else if (buckets.length > 0) {
                    const slotW = 1.0 / buckets.length;
                    landedBucketIdx = Math.max(0, Math.min(buckets.length - 1, Math.floor(t.x / slotW)));
                } else {
                    landedBucketIdx = 0;
                }

                if (buckets.length > 0) {
                    const slotW = 1.0 / buckets.length;
                    const landedBucket = buckets[landedBucketIdx];
                    t.winningBucketIndex = landedBucketIdx;
                    t.winningBucketLabel = landedBucket.label;
                    t.score = landedBucket.score;
                    t.isWin = !!landedBucket.isWin;
                    t.x = (landedBucketIdx + 0.5) * slotW; // Center inside the landed bucket slot!
                    sendCompletion(t.dropToken, landedBucketIdx);
                } else {
                    t.x = t.waypoints[t.waypoints.length - 1].x;
                    sendCompletion(t.dropToken);
                }

                // If this newly landed token sets a new high score in active session, update leader
                if (sessionState.isActive && (t.score || 0) > maxLandedScore) {
                    maxLandedScore = t.score || 0;
                    leaderDropToken = t.dropToken;
                }

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
        if (!sessionState.isActive) {
            if (sessionState.endedAtMs) {
                const ageSec = (now - sessionState.endedAtMs) / 1000;
                if (ageSec <= 6.0) {
                    // Flash completed round banner
                    const bannerW = Math.min(width * 0.72, 480);
                    const bannerH = 46;
                    const bannerX = (width - bannerW) / 2;
                    const bannerY = 18;
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
                    ctx.fillText(`🎉 Round Closed! Total Tokens Dropped: ${sessionState.totalDrops}`, width / 2, bannerY + bannerH / 2);
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
            if (hasActiveToken && sessionState.totalDrops > 0) {
                ctx.save();
                const pillW = 210;
                const pillH = 38;
                const pillX = (width - pillW) / 2;
                const pillY = 16;

                ctx.fillStyle = 'rgba(15, 23, 42, 0.78)';
                ctx.strokeStyle = currentTheme.primaryColor || '#FF8FA3';
                ctx.lineWidth = 2;
                ctx.beginPath();
                ctx.roundRect(pillX, pillY, pillW, pillH, 19);
                ctx.fill();
                ctx.stroke();

                ctx.font = '700 16px Fredoka, Rubik, sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillStyle = '#FFFFFF';
                ctx.shadowColor = 'rgba(0,0,0,0.85)';
                ctx.shadowBlur = 6;
                ctx.fillText(`🪙 Tokens: ${sessionState.totalDrops}`, width / 2, pillY + pillH / 2);
                ctx.restore();
            } else if (!hasActiveToken && activeTokens.length === 0) {
                sessionState.totalDrops = 0;
            }
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

