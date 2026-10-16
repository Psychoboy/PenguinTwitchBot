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
            const tokenGravity = typeof b.gravity === 'number' ? Math.max(0.1, Math.min(10.0, b.gravity)) : 1.0;
            const tokenBounciness = typeof b.bounciness === 'number' ? Math.max(0.1, Math.min(5.0, b.bounciness)) : 0.80;
            const tokenMass = typeof b.mass === 'number' ? Math.max(0.1, Math.min(10.0, b.mass)) : 1.0;
            const durationSec = b.durationSeconds || (4.5 / tokenGravity);
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
                userColor: msg.userColor || msg.color || null,
                characterImageUrl: msg.characterImageUrl || '',
                theme: msg.theme || currentTheme,
                startTimeMs: performance.now(),
                durationMs: durationSec * 1000,
                gravity: tokenGravity,
                bounciness: tokenBounciness,
                mass: tokenMass,
                nameFontSize: typeof b.nameFontSize === 'number' ? b.nameFontSize : (msg.nameFontSize || headerConfig.nameFontSize || 18),
                waypoints: waypoints,
                winningBucketIndex: b.winningBucketIndex || 0,
                winningBucketLabel: b.winningBucketLabel || '',
                score: b.score || 0,
                isWin: !!b.isWin,
                x: waypoints[0].x,
                y: waypoints[0].y,
                rotation: (Math.random() - 0.5) * Math.PI,
                spinSpeed: 3.5 + Math.random() * 3.5,
                bounceAmp: (0.016 + Math.random() * 0.010) * (tokenBounciness / 0.80),
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

    function brightenColor(hex, minLightness = 0.62, minLuminance = 0.42) {
        if (!hex || typeof hex !== 'string') return '#FFFFFF';
        let clean = hex.trim();
        if (!clean.startsWith('#')) clean = '#' + clean;
        if (clean.length === 4) {
            clean = '#' + clean[1] + clean[1] + clean[2] + clean[2] + clean[3] + clean[3];
        }
        if (clean.length < 7) return hex;

        const r = parseInt(clean.substring(1, 3), 16) / 255;
        const g = parseInt(clean.substring(3, 5), 16) / 255;
        const b = parseInt(clean.substring(5, 7), 16) / 255;
        if (isNaN(r) || isNaN(g) || isNaN(b)) return hex;

        const lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;

        const max = Math.max(r, g, b);
        const min = Math.min(r, g, b);
        let h = 0, s = 0, l = (max + min) / 2;

        if (max !== min) {
            const d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            switch (max) {
                case r: h = (g - b) / d + (g < b ? 6 : 0); break;
                case g: h = (b - r) / d + 2; break;
                case b: h = (r - g) / d + 4; break;
            }
            h /= 6;
        }

        if (lum >= minLuminance && l >= 0.48) {
            return clean;
        }

        let targetL = Math.max(l, minLightness);
        let targetS = s;
        if (s > 0.08) {
            targetS = Math.max(s, 0.65);
        } else {
            targetL = Math.max(targetL, 0.85);
        }

        function hslToRgb(h, s, l) {
            if (s === 0) return [l, l, l];
            const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            const p = 2 * l - q;
            const hue2rgb = (p, q, t) => {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1 / 6) return p + (q - p) * 6 * t;
                if (t < 1 / 2) return q;
                if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
                return p;
            };
            return [hue2rgb(p, q, h + 1 / 3), hue2rgb(p, q, h), hue2rgb(p, q, h - 1 / 3)];
        }

        let rgb = hslToRgb(h, targetS, targetL);
        let curLum = 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];

        while (curLum < minLuminance && targetL < 0.88) {
            targetL += 0.04;
            rgb = hslToRgb(h, targetS, targetL);
            curLum = 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
        }

        const toHex = (n) => {
            const hexVal = Math.round(Math.max(0, Math.min(255, n * 255))).toString(16);
            return hexVal.length === 1 ? '0' + hexVal : hexVal;
        };

        return '#' + toHex(rgb[0]) + toHex(rgb[1]) + toHex(rgb[2]);
    }

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

        const padX = Math.max(12, Math.round(width * 0.012));
        const padY = height * 0.04;
        const boardW = width - padX * 2;
        const boardH = height - padY * 2;

        // Optional custom background image only (transparent background by default)
        const bgImg = getLoadedImage(currentTheme.plinkoBoardBackgroundImageUrl);
        if (bgImg) {
            ctx.drawImage(bgImg, padX, padY, boardW, boardH);
        }


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

        const padX = Math.max(12, Math.round(width * 0.012));
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

                    // Constrain completed tokens inside their landed slot margins
                    if (tA.completed && typeof tA.winningBucketIndex === 'number') {
                        const slotW = 1.0 / bucketCount;
                        const margin = Math.min(0.035, slotW * 0.22);
                        tA.x = Math.max(tA.winningBucketIndex * slotW + margin, Math.min((tA.winningBucketIndex + 1) * slotW - margin, tA.x));
                    }
                    if (tB.completed && typeof tB.winningBucketIndex === 'number') {
                        const slotW = 1.0 / bucketCount;
                        const margin = Math.min(0.035, slotW * 0.22);
                        tB.x = Math.max(tB.winningBucketIndex * slotW + margin, Math.min((tB.winningBucketIndex + 1) * slotW - margin, tB.x));
                    }

                    // Elastic bounce impulse with configurable bounciness and mass
                    const rvx = (tA.vx || 0) - (tB.vx || 0);
                    const rvy = (tA.vy || 0) - (tB.vy || 0);
                    const vDot = rvx * nx + rvy * ny;

                    const eBounce = Math.max(0.1, Math.min(5.0, ((tA.bounciness || 0.80) + (tB.bounciness || 0.80)) * 0.5));
                    const mA = Math.max(0.1, tA.mass || 1.0);
                    const mB = Math.max(0.1, tB.mass || 1.0);
                    const totalMass = mA + mB;
                    const impulse = (0.32 + Math.abs(vDot) * 0.45) * (eBounce / 0.80);
                    tA.vx = (tA.vx || 0) + nx * impulse * (2.0 * mB / totalMass);
                    tA.vy = (tA.vy || 0) + ny * impulse * 0.4 * (2.0 * mB / totalMass);
                    tB.vx = (tB.vx || 0) - nx * impulse * (2.0 * mA / totalMass);
                    tB.vy = (tB.vy || 0) - ny * impulse * 0.4 * (2.0 * mA / totalMass);

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

            const tokenLandingY = height - tokenRadius - 4;
            const tokenLandingYNorm = Math.round((tokenLandingY / height) * 10000) / 10000;

            if (!t.completed && ((t.progress || 0) >= 1.0 || t.y >= tokenLandingYNorm)) {
                t.completed = true;
                t.completedAtMs = now;
                t.y = tokenLandingYNorm;

                const buckets = boardConfig.buckets || [];
                let landedBucketIdx;
                if (buckets.length > 0 && Math.abs(t.offsetX || 0) > 0.001 && typeof t.x === 'number') {
                    const slotW = 1.0 / buckets.length;
                    landedBucketIdx = Math.max(0, Math.min(buckets.length - 1, Math.floor(t.x / slotW)));
                } else if (typeof t.winningBucketIndex === 'number' && buckets.length > 0) {
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

                    // Natural landing spread across slot width
                    const margin = Math.min(0.035, slotW * 0.22);
                    const minSlotX = landedBucketIdx * slotW + margin;
                    const maxSlotX = (landedBucketIdx + 1) * slotW - margin;

                    if (typeof t.x === 'number' && t.x >= (landedBucketIdx * slotW) && t.x <= ((landedBucketIdx + 1) * slotW)) {
                        t.x = Math.max(minSlotX, Math.min(maxSlotX, t.x));
                    } else {
                        const jitter = (Math.random() - 0.5) * (slotW - 2 * margin) * 0.65;
                        t.x = (landedBucketIdx + 0.5) * slotW + jitter;
                    }
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
            const rawColor = isLeader ? '#FFD700' : (userColor || tokenTheme.textColor || currentTheme.textColor || '#FFFFFF');
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

    requestAnimationFrame(animate);
})();

