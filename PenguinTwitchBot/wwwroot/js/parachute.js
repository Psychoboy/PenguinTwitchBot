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
                console.warn('[Parachute] Image fetch returned status', resp.status, 'for', fetchUrl);
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
                                console.log('[Parachute] Successfully decoded ' + frames.length + ' frames via WebCodecs for ' + url);
                                decoder.close();
                                return;
                            }
                        }
                        decoder.close();
                    }
                } catch (decErr) {
                    console.warn('[Parachute] WebCodecs decode failed for ' + url + ', trying GIF fallback:', decErr);
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
                        console.log('[Parachute] Successfully decoded ' + gifFrames.length + ' frames via JS GIF decoder for ' + url);
                        return;
                    }
                } catch (gifErr) {
                    console.warn('[Parachute] JS GIF decode failed for ' + url + ':', gifErr);
                }
            }
        } catch (err) {
            console.warn('[Parachute] loadAnimatedFrames error for ' + url + ':', err);
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

    function sendCompletion(token, landingX) {
        if (!token) return;
        const payload = { dropToken: token };
        if (typeof landingX === 'number' && !isNaN(landingX)) {
            payload.landingX = Math.round(landingX * 10000) / 10000;
        }
        if (socket && socket.readyState === WebSocket.OPEN) {
            try {
                socket.send(JSON.stringify(payload));
                return;
            } catch (_) {}
        }
        if (!pendingCompletions.some(p => p.token === token)) {
            pendingCompletions.push({ token, landingX });
        }
    }

    function flushPendingCompletions() {
        if (!socket || socket.readyState !== WebSocket.OPEN) return;
        while (pendingCompletions.length > 0) {
            const item = pendingCompletions.shift();
            try {
                const payload = { dropToken: item.token };
                if (typeof item.landingX === 'number') payload.landingX = item.landingX;
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
                tiltAngle: p.initialTilt !== undefined ? p.initialTilt : (Math.random() - 0.5) * 0.45,
                completed: false,
                landedAtMs: 0
            });
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
        } else if (msg.type === 'session_start' && msg.session) {
            const s = msg.session;
            sessionState.isActive = true;
            sessionState.totalDurationSeconds = s.totalDurationSeconds || 100;
            sessionState.startedAtMs = performance.now();
            sessionState.endsAtMs = performance.now() + (s.remainingSeconds || s.totalDurationSeconds || 100) * 1000;
            if (typeof s.totalDrops === 'number') {
                sessionState.totalDrops = s.totalDrops;
            }
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
            if (typeof msg.session.totalDrops === 'number') {
                sessionState.totalDrops = msg.session.totalDrops;
            }
            sessionState.endedAtMs = performance.now();
            if (msg.session.targetCenterX !== undefined && msg.session.targetCenterX !== null) {
                targetState.centerX = msg.session.targetCenterX;
            }
        }

        if (msg.session && sessionState) {
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
                const targetWNorm = (targetState.targetHalfWidth || 0.09) * 2;
                const minPadX = (targetWNorm * 0.5) + 24 / width;
                const maxPadX = 1.0 - (targetWNorm * 0.5) - 24 / width;
                const targetCx = minPadX < maxPadX ? Math.max(minPadX, Math.min(maxPadX, targetState.centerX || 0.5)) : 0.5;
                const targetHalfW = targetState.targetHalfWidth || 0.09;
                const targetTopY = 0.88;

                if (d.vy > 0 && d.y + r >= targetTopY && d.y - r <= 0.91) {
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
                const floorY = 0.935;
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

                // 4. Elastic collision with falling parachutists and landed parachutists
                const charRadius = Math.max(24, Math.min(width, height) * 0.032) / height;
                const canopyRadius = charRadius * 1.65;
                const canopyOffsetY = -charRadius * 2.1;

                for (let j = 0; j < activeDrops.length; j++) {
                    const drop = activeDrops[j];
                    if (!drop || (drop.completed && (now - drop.landedAtMs) > 12000)) continue;

                    // Test against canopy (only if falling)
                    if (!drop.completed) {
                        const canX = drop.x;
                        const canY = drop.y + canopyOffsetY;
                        const canMinDist = canopyRadius + r;
                        const canDx = d.x - canX;
                        const canDy = d.y - canY;
                        const canDistSq = canDx * canDx + canDy * canDy;
                        if (canDistSq < canMinDist * canMinDist && canDistSq > 0.00001) {
                            const dist = Math.sqrt(canDistSq);
                            const nx = canDx / dist;
                            const ny = canDy / dist;
                            d.x = canX + nx * canMinDist;
                            d.y = canY + ny * canMinDist;

                            const MAX_DROP_VX = 0.55;
                            const relVx = d.vx - (drop.vx || 0);
                            const relVy = d.vy - (drop.vy || 0.12);
                            const vDot = relVx * nx + relVy * ny;
                            if (vDot < 0) {
                                const impulse = -(1 + (d.restitution || 1.0)) * vDot;
                                d.vx -= impulse * nx * 0.4;
                                d.vy -= impulse * ny * 0.4;
                                d.angularVel = (Math.random() - 0.5) * 10.0;
                                d.hitCount = (d.hitCount || 0) + 1;

                                const canKnock = !drop.lastDropletHitTime || (now - drop.lastDropletHitTime) > 140;
                                if (canKnock) {
                                    drop.lastDropletHitTime = now;
                                    const deltaVx = Math.max(-0.25, Math.min(0.25, nx * impulse * 0.50));
                                    drop.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (drop.vx || 0) + deltaVx));
                                    drop.vy = Math.max(0.08, Math.min(0.22, (drop.vy || 0.12) + ny * impulse * 0.15));
                                    drop.tiltAngle = Math.max(-0.65, Math.min(0.65, drop.vx * 2.2));
                                }

                                if (currentTheme.dropletHitSoundUrl && (!d.lastSfxTime || (now - d.lastSfxTime) > 180)) {
                                    playSound(currentTheme.dropletHitSoundUrl, 0.35);
                                    d.lastSfxTime = now;
                                }
                            }
                        }
                    }

                    // Test against player character body (both falling and landed)
                    const bodyX = drop.x;
                    const bodyY = drop.y;
                    const bodyMinDist = charRadius + r;
                    const bDx = d.x - bodyX;
                    const bDy = d.y - bodyY;
                    const bDistSq = bDx * bDx + bDy * bDy;
                    if (bDistSq < bodyMinDist * bodyMinDist && bDistSq > 0.00001) {
                        const dist = Math.sqrt(bDistSq);
                        const nx = bDx / dist;
                        const ny = bDy / dist;
                        d.x = bodyX + nx * bodyMinDist;
                        d.y = bodyY + ny * bodyMinDist;

                        const MAX_DROP_VX = 0.55;
                        const relVx = d.vx - (drop.vx || 0);
                        const relVy = d.vy - (drop.vy || 0.12);
                        const vDot = relVx * nx + relVy * ny;
                        if (vDot < 0) {
                            const impulse = -(1 + (d.restitution || 1.0)) * vDot;
                            d.vx -= impulse * nx * 0.4;
                            d.vy -= impulse * ny * 0.4;
                            d.angularVel = (Math.random() - 0.5) * 10.0;
                            d.hitCount = (d.hitCount || 0) + 1;

                            if (!drop.completed) {
                                const canKnock = !drop.lastDropletHitTime || (now - drop.lastDropletHitTime) > 140;
                                if (canKnock) {
                                    drop.lastDropletHitTime = now;
                                    const deltaVx = Math.max(-0.25, Math.min(0.25, nx * impulse * 0.50));
                                    drop.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (drop.vx || 0) + deltaVx));
                                    drop.vy = Math.max(0.08, Math.min(0.22, (drop.vy || 0.12) + ny * impulse * 0.15));
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
                    const oR = other.radius || 0.028;
                    const oDx = other.x - d.x;
                    const oDy = other.y - d.y;
                    const oMinDist = r + oR;
                    const oDistSq = oDx * oDx + oDy * oDy;
                    if (oDistSq < oMinDist * oMinDist && oDistSq > 0.000001) {
                        const oDist = Math.sqrt(oDistSq);
                        const nx = oDx / oDist;
                        const ny = oDy / oDist;
                        const overlap = 0.5 * (oMinDist - oDist);
                        d.x -= nx * overlap;
                        d.y -= ny * overlap;
                        other.x += nx * overlap;
                        other.y += ny * overlap;

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
        const charRadiusNorm = Math.max(24, Math.min(width, height) * 0.032) / height;
        const colRadius = charRadiusNorm * 1.45;

        for (let i = 0; i < activeDrops.length; i++) {
            const drop = activeDrops[i];
            if (drop.completed) continue;

            const elapsedSec = (now - drop.startTimeMs) / 1000;
            drop.currentTick = Math.min(drop.totalTicks, elapsedSec * TICK_RATE);

            // Clamp velocity within physical readable limits
            const MAX_DROP_VX = 0.55;
            drop.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, drop.vx || 0));
            drop.vy = Math.max(0.08, Math.min(0.22, drop.vy || 0.12));

            // Move along current velocity vector (no sinusoidal sway)
            drop.x += drop.vx * dtSec;
            drop.y += drop.vy * dtSec;

            // Clean bounce off left / right screen borders with reflection
            if (drop.x < 0.05) {
                drop.x = 0.05;
                drop.vx = Math.min(MAX_DROP_VX, Math.abs(drop.vx || 0.12) * 0.90);
                if (currentTheme.dropSoundUrl && (!drop.lastWallSfx || (now - drop.lastWallSfx) > 200)) {
                    playSound(currentTheme.dropSoundUrl, 0.35);
                    drop.lastWallSfx = now;
                }
            } else if (drop.x > 0.95) {
                drop.x = 0.95;
                drop.vx = -Math.min(MAX_DROP_VX, Math.abs(drop.vx || 0.12) * 0.90);
                if (currentTheme.dropSoundUrl && (!drop.lastWallSfx || (now - drop.lastWallSfx) > 200)) {
                    playSound(currentTheme.dropSoundUrl, 0.35);
                    drop.lastWallSfx = now;
                }
            }

            // Tilt corresponds directly to lateral flight speed
            const targetTilt = Math.max(-0.65, Math.min(0.65, (drop.vx || 0) * 2.2));
            drop.tiltAngle = (drop.tiltAngle || 0) * 0.88 + targetTilt * 0.12;

            // Trigger any server-recorded collision impulses at their exact tick
            if (Array.isArray(drop.collisions)) {
                for (let c = 0; c < drop.collisions.length; c++) {
                    const col = drop.collisions[c];
                    if (drop.currentTick >= col.tick && !drop.triggeredCollisionTicks.has(col.tick)) {
                        drop.triggeredCollisionTicks.add(col.tick);
                        drop.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (drop.vx || 0) + (col.deltaVx || 0)));
                        drop.tiltAngle = (col.deltaVx >= 0 ? 1 : -1) * 0.35;
                    }
                }
            }
        }

        // Active Parachuter-to-Parachuter Mid-Air 2D Elastic Bouncing
        for (let i = 0; i < activeDrops.length; i++) {
            const dropA = activeDrops[i];
            if (dropA.completed) continue;

            for (let j = i + 1; j < activeDrops.length; j++) {
                const dropB = activeDrops[j];
                if (dropB.completed) continue;

                const dx = dropA.x - dropB.x;
                const dy = dropA.y - dropB.y;
                const distSq = dx * dx + dy * dy;
                const minDist = colRadius * 2.0;

                if (distSq < minDist * minDist && distSq > 0.000001) {
                    const dist = Math.sqrt(distSq);
                    const nx = dx / dist;
                    const ny = dy / dist;

                    // Positional separation
                    const overlap = 0.5 * (minDist - dist);
                    dropA.x += nx * overlap;
                    dropA.y += ny * overlap;
                    dropB.x -= nx * overlap;
                    dropB.y -= ny * overlap;

                    // Elastic bounce impulse
                    const rvx = (dropA.vx || 0) - (dropB.vx || 0);
                    const rvy = (dropA.vy || 0) - (dropB.vy || 0);
                    const vDot = rvx * nx + rvy * ny;

                    if (vDot < 0) {
                        const MAX_DROP_VX = 0.55;
                        const impulse = -(1 + 0.85) * vDot * 0.5 + 0.04;
                        dropA.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (dropA.vx || 0) + nx * impulse));
                        dropA.vy = Math.max(0.08, Math.min(0.22, (dropA.vy || 0.12) + ny * impulse * 0.4));
                        dropB.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (dropB.vx || 0) - nx * impulse));
                        dropB.vy = Math.max(0.08, Math.min(0.22, (dropB.vy || 0.12) - ny * impulse * 0.4));

                        if (currentTheme.dropletHitSoundUrl && (!dropA.lastBounceSfx || (now - dropA.lastBounceSfx) > 150)) {
                            playSound(currentTheme.dropletHitSoundUrl, 0.45);
                            dropA.lastBounceSfx = now;
                        }

                        dropA.tiltAngle = Math.max(-0.65, Math.min(0.65, dropA.vx * 2.2));
                        dropB.tiltAngle = Math.max(-0.65, Math.min(0.65, dropB.vx * 2.2));
                    }
                }
            }

            // Deflect falling parachuter off already landed players
            for (let j = 0; j < activeDrops.length; j++) {
                if (i === j) continue;
                const dropB = activeDrops[j];
                if (!dropB.completed) continue;

                const dx = dropA.x - dropB.x;
                const dy = dropA.y - dropB.y;
                const distSq = dx * dx + dy * dy;
                const minDist = colRadius * 1.6;

                if (distSq < minDist * minDist && distSq > 0.00001) {
                    const dist = Math.sqrt(distSq);
                    const nx = dx / dist;
                    const MAX_DROP_VX = 0.55;
                    dropA.x += nx * (minDist - dist);
                    dropA.vx = Math.max(-MAX_DROP_VX, Math.min(MAX_DROP_VX, (dropA.vx || 0) + nx * 0.15));
                    dropA.tiltAngle = Math.max(-0.65, Math.min(0.65, (dropA.vx || 0) * 2.2));
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
                const targetWNorm = (targetState.targetHalfWidth || 0.09) * 2;
                const minPadX = (targetWNorm * 0.5) + 24 / width;
                const maxPadX = 1.0 - (targetWNorm * 0.5) - 24 / width;
                const targetCx = minPadX < maxPadX ? Math.max(minPadX, Math.min(maxPadX, targetState.centerX || 0.5)) : 0.5;
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
                sendCompletion(drop.dropToken, drop.landingX);

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
        if (!sessionState.isActive) {
            if (sessionState.endedAtMs) {
                const ageSec = (now - sessionState.endedAtMs) / 1000;
                if (ageSec <= 6.0) {
                    // Flash completed round banner and fade out
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
                    ctx.fillText(`🎉 Round Closed! Total Parachutists: ${sessionState.totalDrops}`, width / 2, bannerY + bannerH / 2);
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
            if (hasActiveDrop && sessionState.totalDrops > 0) {
                ctx.save();
                const pillW = 210;
                const pillH = 38;
                const pillX = (width - pillW) / 2;
                const pillY = 16;

                ctx.fillStyle = 'rgba(15, 23, 42, 0.78)';
                ctx.strokeStyle = currentTheme.primaryColor || '#60A5FA';
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
                ctx.fillText(`🪂 Drops: ${sessionState.totalDrops}`, width / 2, pillY + pillH / 2);
                ctx.restore();
            } else if (!hasActiveDrop && activeDrops.length === 0) {
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

