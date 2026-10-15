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
        const minPadX = (targetW / 2) + 24;
        const maxPadX = width - (targetW / 2) - 24;
        const rawCx = (targetState.centerX || 0.5) * width;
        const cx = minPadX < maxPadX ? Math.max(minPadX, Math.min(maxPadX, rawCx)) : width / 2;
        const y = 0.895 * height;
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

        // Target badge with seasonal icon
        const targetEmoji = season === 'fall' ? '🍂' : (season === 'winter' ? '❄️' : (season === 'summer' ? '🏊' : (season === 'halloween' ? '🎃' : (season === 'christmas' ? '🎄' : '🌸'))));
        ctx.font = '700 14px Fredoka, Rubik, sans-serif';
        ctx.fillStyle = '#FFFFFF';
        ctx.textAlign = 'center';
        ctx.shadowColor = 'rgba(0,0,0,0.85)';
        ctx.shadowBlur = 6;
        ctx.fillText(`${targetEmoji} TARGET`, cx, y + 34);

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
        } else if (season === 'halloween') {
            // Spooky Witch Hat
            ctx.save();
            ctx.fillStyle = '#212529';
            // Pointed cone
            ctx.beginPath();
            ctx.moveTo(-r * 0.72, -r * 0.65);
            ctx.lineTo(r * 0.72, -r * 0.65);
            ctx.lineTo(r * 0.22, -r * 1.55);
            ctx.closePath();
            ctx.fill();
            // Wide brim
            ctx.beginPath();
            ctx.ellipse(0, -r * 0.65, r * 0.88, r * 0.25, 0, 0, Math.PI * 2);
            ctx.fill();
            // Purple band
            ctx.fillStyle = '#7B2CBF';
            ctx.fillRect(-r * 0.45, -r * 0.85, r * 0.9, r * 0.22);
            // Gold buckle
            ctx.fillStyle = '#FFD166';
            ctx.fillRect(-r * 0.16, -r * 0.88, r * 0.32, r * 0.28);
            ctx.restore();
        } else if (season === 'christmas') {
            // Festive Santa Hat
            ctx.save();
            ctx.fillStyle = '#D90429';
            ctx.beginPath();
            ctx.moveTo(-r * 0.68, -r * 0.65);
            ctx.lineTo(r * 0.68, -r * 0.65);
            ctx.quadraticCurveTo(r * 0.85, -r * 1.35, r * 0.45, -r * 1.45);
            ctx.quadraticCurveTo(r * 0.1, -r * 1.15, -r * 0.68, -r * 0.65);
            ctx.fill();
            // White fur trim
            ctx.fillStyle = '#FFFFFF';
            ctx.beginPath();
            ctx.roundRect(-r * 0.74, -r * 0.75, r * 1.48, r * 0.26, 6);
            ctx.fill();
            // White pom-pom
            ctx.beginPath();
            ctx.arc(r * 0.52, -r * 1.46, r * 0.24, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
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

            // Interactive Target Rim / Wall Bounce (Pool / Cauldron / Leaf Pile side lip)
            if (!drop.bouncedOffTargetRim && drop.y >= (drop.landingY - 0.038) && drop.y < drop.landingY) {
                const targetWNorm = (targetState.targetHalfWidth || 0.09) * 2;
                const minPadX = (targetWNorm * 0.5) + 24 / width;
                const maxPadX = 1.0 - (targetWNorm * 0.5) - 24 / width;
                const targetCx = minPadX < maxPadX ? Math.max(minPadX, Math.min(maxPadX, targetState.centerX || 0.5)) : 0.5;
                const dx = drop.x - targetCx;
                const dist = Math.abs(dx);
                const halfW = targetState.targetHalfWidth || 0.09;
                if (dist >= halfW * 0.93 && dist <= halfW * 1.07) {
                    drop.bouncedOffTargetRim = true;
                    if (Math.sign(drop.vx) === Math.sign(dx) && Math.abs(drop.vx) > 0.01) {
                        drop.vx = -drop.vx * 0.85; // Bounced off outer wall!
                    } else {
                        drop.vx = (dx > 0 ? -1 : 1) * Math.max(0.04, Math.abs(drop.vx) * 0.8); // Bounced over lip into the pool!
                    }
                    drop.vy = Math.max(0.08, drop.vy * 0.65);
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

