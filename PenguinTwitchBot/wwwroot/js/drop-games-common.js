/**
 * Shared Common Utilities for Drop Games (Plinko, Parachute, etc.)
 * Includes:
 * - Animated emote & image caching engine (GPU WebCodecs ImageDecoder + pure-JS fallback GIF decoder)
 * - Color contrast & luminance brightening for high readability of Twitch chatter names
 * - Seasonal vector fallback penguin sprite renderer
 */
(function (global) {
    'use strict';

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

    async function parseAndDecodeGif(arrayBuffer) {
        if (!arrayBuffer || typeof Uint8Array === 'undefined') return null;
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
        if (typeof document === 'undefined' || typeof createImageBitmap === 'undefined') return null;

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

    async function loadAnimatedFrames(url, entry, tag = 'DropGames') {
        try {
            let fetchUrl = url;
            if (url.startsWith('http://') || url.startsWith('https://')) {
                try {
                    const parsed = new URL(url);
                    if (typeof window !== 'undefined' && (parsed.hostname === window.location.hostname || parsed.hostname === 'localhost' || parsed.hostname === '127.0.0.1')) {
                        fetchUrl = parsed.pathname + parsed.search;
                    } else {
                        fetchUrl = `/api/chat/proxy-image?url=${encodeURIComponent(url)}`;
                    }
                } catch (_) {
                    fetchUrl = `/api/chat/proxy-image?url=${encodeURIComponent(url)}`;
                }
            }

            if (typeof fetch === 'undefined') return;
            const resp = await fetch(fetchUrl);
            if (!resp.ok) {
                console.warn(`[${tag}] Image fetch returned status`, resp.status, 'for', fetchUrl);
                return;
            }
            const buffer = await resp.arrayBuffer();
            const headerType = resp.headers.get('content-type');
            const mimeType = (headerType && headerType.startsWith('image/'))
                ? headerType.split(';')[0].trim()
                : detectMimeType(buffer);

            // 1. WebCodecs ImageDecoder (fast GPU hardware-decoded frames)
            if (typeof window !== 'undefined' && typeof window.ImageDecoder !== 'undefined') {
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
                                console.log(`[${tag}] Successfully decoded ` + frames.length + ' frames via WebCodecs for ' + url);
                                decoder.close();
                                return;
                            }
                        }
                        decoder.close();
                    }
                } catch (decErr) {
                    console.warn(`[${tag}] WebCodecs decode failed for ` + url + ', trying GIF fallback:', decErr);
                }
            }

            // 2. Pure-JS GIF parser fallback
            if (mimeType === 'image/gif' || detectMimeType(buffer) === 'image/gif') {
                try {
                    const gifFrames = await parseAndDecodeGif(buffer);
                    if (gifFrames && gifFrames.length > 1) {
                        entry.frames = gifFrames;
                        entry.totalDurationMs = gifFrames.reduce((acc, f) => acc + f.duration, 0);
                        entry.isAnimated = true;
                        console.log(`[${tag}] Successfully decoded ` + gifFrames.length + ' frames via JS GIF decoder for ' + url);
                        return;
                    }
                } catch (gifErr) {
                    console.warn(`[${tag}] JS GIF decode failed for ` + url + ':', gifErr);
                }
            }
        } catch (err) {
            console.warn(`[${tag}] loadAnimatedFrames error for ` + url + ':', err);
        }
    }

    function createImageLoader(tag = 'DropGames') {
        const imageCache = new Map();

        function getLoadedImage(rawUrl) {
            const url = normalizeImageUrl(rawUrl);
            if (!url) return null;
            let entry = imageCache.get(url);
            if (!entry) {
                if (typeof Image === 'undefined') return null;
                const img = new Image();
                entry = { img, loaded: false, error: false, frames: null, totalDurationMs: 0, isAnimated: false };
                img.onload = function () { entry.loaded = true; };
                img.onerror = function () { entry.error = true; };
                img.src = url;
                imageCache.set(url, entry);
                loadAnimatedFrames(url, entry, tag);
                return null;
            }

            if (entry.isAnimated && entry.frames && entry.frames.length > 1) {
                const now = (typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now();
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

        return {
            getLoadedImage: getLoadedImage,
            getImage: getLoadedImage,
            getImageCache: function () { return imageCache; },
            normalizeImageUrl: normalizeImageUrl
        };
    }

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
            if (typeof ctx.roundRect === 'function') {
                ctx.roundRect(-r * 0.78, r * 0.22, r * 1.56, r * 0.28, 6);
            } else {
                ctx.rect(-r * 0.78, r * 0.22, r * 1.56, r * 0.28);
            }
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
            ctx.fillStyle = '#FFFFFF';
            ctx.beginPath();
            if (typeof ctx.roundRect === 'function') {
                ctx.roundRect(-r * 0.74, -r * 0.75, r * 1.48, r * 0.26, 6);
            } else {
                ctx.rect(-r * 0.74, -r * 0.75, r * 1.48, r * 0.26);
            }
            ctx.fill();
            ctx.beginPath();
            ctx.arc(r * 0.52, -r * 1.46, r * 0.24, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
        }
    }

    /**
     * Renders a high-visibility, dual-pass outlined chatter username tag with drop shadow,
     * leader highlighting, optional counter-rotation, and luminance-boosted colors.
     * 
     * @param {CanvasRenderingContext2D} ctx - Canvas 2D context
     * @param {Object} options
     * @param {string} options.name - Chatter username / display name
     * @param {string} [options.userColor='#FFFFFF'] - Chatter username color
     * @param {number} [options.fontSize=18] - Desired font size (clamped 12-36)
     * @param {number} [options.score=0] - Player's score
     * @param {boolean} [options.completed=false] - Whether the drop/throw is completed
     * @param {boolean} [options.isLeader=false] - Whether the player is the round/session leader
     * @param {string} [options.position='below'] - 'below' (default) or 'above' relative to avatar center
     * @param {number} [options.radius=24] - Avatar/token radius in pixels (used to calculate Y offset)
     * @param {number} [options.x=0] - X anchor coordinate (default 0)
     * @param {number} [options.y] - Explicit Y anchor coordinate (overrides position/radius calculation)
     * @param {number} [options.rotation=0] - Current avatar rotation/tilt in radians (auto counter-rotates if provided)
     * @param {boolean} [options.showCrown=true] - Whether to draw gold crown 👑 emoji for leader
     */
    function drawOutlinedName(ctx, options) {
        if (!ctx || !options || !options.name) return;

        const fontSize = Math.max(12, Math.min(36, typeof options.fontSize === 'number' ? options.fontSize : 18));
        const strokeWidth = Math.max(4.5, fontSize * 0.26);
        const isLeader = !!options.isLeader;
        const completed = !!options.completed;
        const score = typeof options.score === 'number' ? options.score : 0;

        let label = options.name;
        if (isLeader && completed) {
            label = `${options.name} (${score} pts)`;
        } else if (completed && score > 0) {
            label = `${options.name} (${score})`;
        }

        const rotation = typeof options.rotation === 'number' ? options.rotation : 0;
        const r = typeof options.radius === 'number' ? options.radius : 20;

        let textY;
        if (typeof options.y === 'number') {
            textY = options.y;
        } else if (options.position === 'above') {
            textY = -r - fontSize * 0.7 - 2;
        } else {
            textY = r + fontSize + 2;
        }

        const textX = typeof options.x === 'number' ? options.x : 0;

        ctx.save();

        // Optional counter-rotation to keep username horizontal
        if (rotation !== 0) {
            ctx.rotate(-rotation);
        }

        ctx.font = `900 ${fontSize}px Fredoka, Rubik, "Segoe UI", sans-serif`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.lineJoin = 'round';
        ctx.miterLimit = 2;

        // Draw Leader Crown if applicable
        if (isLeader && completed && options.showCrown !== false) {
            ctx.save();
            ctx.font = `700 ${fontSize + 4}px "Segoe UI Emoji", Fredoka, sans-serif`;
            ctx.textAlign = 'center';
            ctx.shadowColor = '#FFD700';
            ctx.shadowBlur = 10;
            const crownY = options.position === 'above' ? textY - fontSize * 0.9 : -r - fontSize * 0.7;
            ctx.fillText('👑', textX, crownY);
            ctx.restore();
        }

        // Pass 1: Dark drop shadow behind outline
        ctx.save();
        ctx.shadowColor = 'rgba(0, 0, 0, 0.95)';
        ctx.shadowBlur = Math.max(6, fontSize * 0.4);
        ctx.shadowOffsetX = 0;
        ctx.shadowOffsetY = 2;
        ctx.strokeStyle = '#000000';
        ctx.lineWidth = strokeWidth + 1.5;
        ctx.strokeText(label, textX, textY);
        ctx.restore();

        // Pass 2: Razor-sharp crisp black contrast border
        ctx.strokeStyle = '#000000';
        ctx.lineWidth = strokeWidth;
        ctx.strokeText(label, textX, textY);

        // Pass 3: Luminance-boosted vibrant color fill (preserves user's identity color)
        const rawColor = options.userColor || options.textColor || '#FFFFFF';
        const nameColor = brightenColor(rawColor);
        ctx.fillStyle = nameColor;
        ctx.fillText(label, textX, textY);

        ctx.restore();
    }

    function drawRoundRect(ctx, x, y, w, h, radii) {
        if (!ctx) return;
        if (typeof ctx.roundRect === 'function') {
            ctx.roundRect(x, y, w, h, radii);
        } else if (typeof ctx.rect === 'function') {
            ctx.rect(x, y, w, h);
        }
    }

    function getBannerX(canvasWidth, bannerWidth, position) {
        const pos = (position || 'top-center').toLowerCase().replace(/[-_]/g, '');
        if (pos === 'topleft') return 24;
        if (pos === 'topright') return canvasWidth - bannerWidth - 24;
        return (canvasWidth - bannerWidth) / 2;
    }

    const MINIGAME_RANKS = {
        parachute: 0,
        plinko: 1,
        balloon: 2,
        curling: 3,
        golf: 4,
        ufo: 5,
        raceway: 6,
        submarine: 7
    };

    function calculateBannerStackSlot(currentGame, activeOtherGames) {
        if (!currentGame || !activeOtherGames) return 0;
        const myRank = MINIGAME_RANKS[String(currentGame).toLowerCase()] ?? 99;
        let slot = 0;
        const games = (activeOtherGames instanceof Set)
            ? Array.from(activeOtherGames)
            : (Array.isArray(activeOtherGames) ? activeOtherGames : Object.keys(activeOtherGames));

        for (let i = 0; i < games.length; i++) {
            const g = String(games[i]).toLowerCase();
            if (g === String(currentGame).toLowerCase()) continue;
            const otherRank = MINIGAME_RANKS[g] ?? 99;
            if (otherRank < myRank) {
                slot++;
            }
        }
        return slot;
    }

    function drawSessionBanner(ctx, options) {
        if (!ctx || !options) return { currentBannerY: 18, bannerY: 18 };

        const now = typeof options.now === 'number' ? options.now : Date.now();
        const width = typeof options.width === 'number' ? options.width : 1920;
        const headerConfig = options.headerConfig || {};
        const sessionState = options.sessionState || { isActive: false, totalDrops: 0 };
        const itemPluralName = options.itemPluralName || 'Drops';
        const primaryColor = options.primaryColor || '#60A5FA';
        const accentColor = options.accentColor || '#FFE066';

        let targetY = typeof headerConfig.yOffset === 'number' ? headerConfig.yOffset : 18;
        if (headerConfig.autoAvoidOverlap !== false) {
            let slot = 0;
            if (options.currentGame && options.activeOtherGames) {
                slot = calculateBannerStackSlot(options.currentGame, options.activeOtherGames);
            } else if (options.isOtherGameActive) {
                slot = 1;
            }

            if (options.otherDropsActiveUntilMs && now < options.otherDropsActiveUntilMs && slot === 0) {
                slot = 1;
            }

            targetY += slot * 54;
        }

        let currentBannerY = typeof options.currentBannerY === 'number' ? options.currentBannerY : targetY;
        currentBannerY += (targetY - currentBannerY) * 0.15;
        const bannerY = Math.round(currentBannerY);

        if (!sessionState.isActive) {
            if (sessionState.endedAtMs) {
                const ageSec = (now - sessionState.endedAtMs) / 1000;
                if (ageSec <= 6.0) {
                    if (headerConfig.showBanner !== false) {
                        const bannerW = Math.min(width * 0.72, 480);
                        const bannerH = 46;
                        const bannerX = getBannerX(width, bannerW, headerConfig.position);
                        const bannerCenterX = bannerX + bannerW / 2;

                        ctx.save();
                        const alpha = Math.max(0, 1.0 - (ageSec / 6.0));
                        ctx.globalAlpha = alpha;

                        ctx.fillStyle = 'rgba(15, 23, 42, 0.88)';
                        ctx.strokeStyle = '#10B981';
                        ctx.lineWidth = 2;
                        ctx.beginPath();
                        drawRoundRect(ctx, bannerX, bannerY, bannerW, bannerH, 23);
                        ctx.fill();
                        ctx.stroke();

                        ctx.font = '700 17px Fredoka, Rubik, sans-serif';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'middle';
                        ctx.fillStyle = '#34D399';
                        ctx.shadowColor = 'rgba(0,0,0,0.85)';
                        ctx.shadowBlur = 6;
                        const closedText = options.closedBannerText || `Round Closed! Total ${itemPluralName}: ${sessionState.totalDrops}`;
                        ctx.fillText(closedText, bannerCenterX, bannerY + bannerH / 2);
                        ctx.restore();
                    }
                    return { currentBannerY, bannerY };
                } else {
                    sessionState.endedAtMs = 0;
                    sessionState.totalDrops = 0;
                    return { currentBannerY, bannerY };
                }
            }

            // Free Play mode
            const hasActiveDrops = !!options.hasActiveDrops;
            if (hasActiveDrops && sessionState.totalDrops > 0 && headerConfig.showFreePlayCounter !== false) {
                ctx.save();
                const pillW = 210;
                const pillH = 38;
                const pillX = getBannerX(width, pillW, headerConfig.position);

                ctx.fillStyle = 'rgba(15, 23, 42, 0.78)';
                ctx.strokeStyle = primaryColor;
                ctx.lineWidth = 2;
                ctx.beginPath();
                drawRoundRect(ctx, pillX, bannerY, pillW, pillH, 19);
                ctx.fill();
                ctx.stroke();

                ctx.font = '700 16px Fredoka, Rubik, sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillStyle = '#FFFFFF';
                ctx.shadowColor = 'rgba(0,0,0,0.85)';
                ctx.shadowBlur = 6;
                ctx.fillText(`${itemPluralName}: ${sessionState.totalDrops}`, pillX + pillW / 2, bannerY + pillH / 2);
                ctx.restore();
            } else if (!hasActiveDrops && options.clearsFreePlayWhenInactive) {
                sessionState.totalDrops = 0;
            }
            return { currentBannerY, bannerY };
        }

        if (headerConfig.showBanner === false) {
            return { currentBannerY, bannerY };
        }

        const showTimer = headerConfig.showTimer !== false;
        const showCount = headerConfig.showCount !== false;

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
        const bannerX = getBannerX(width, bannerW, headerConfig.position);
        const bannerCenterX = bannerX + bannerW / 2;

        ctx.save();
        const remainingSec = Math.max(0, Math.ceil(((sessionState.endsAtMs || 0) - now) / 1000));
        const totalSec = Math.max(1, sessionState.totalDurationSeconds || 60);
        const progress = Math.max(0, Math.min(1, remainingSec / totalSec));

        const mins = Math.floor(remainingSec / 60);
        const secs = remainingSec % 60;
        const timeStr = `${mins}:${secs < 10 ? '0' : ''}${secs}`;

        // Semi-transparent pill backdrop
        ctx.fillStyle = 'rgba(15, 23, 42, 0.78)';
        ctx.strokeStyle = primaryColor;
        ctx.lineWidth = 2;
        ctx.beginPath();
        drawRoundRect(ctx, bannerX, bannerY, bannerW, bannerH, 23);
        ctx.fill();
        ctx.stroke();

        // Progress bar at bottom of pill
        const barW = (bannerW - 32) * progress;
        ctx.fillStyle = accentColor;
        ctx.beginPath();
        drawRoundRect(ctx, bannerX + 16, bannerY + bannerH - 8, barW, 4, 2);
        ctx.fill();

        let bannerText = '';
        if (showTimer && showCount) {
            bannerText = `Round Ends: ${timeStr}  |  ${itemPluralName}: ${sessionState.totalDrops}`;
        } else if (showTimer) {
            bannerText = `Round Ends: ${timeStr}`;
        } else if (showCount) {
            bannerText = `${itemPluralName}: ${sessionState.totalDrops}`;
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

        return { currentBannerY, bannerY };
    }

    function updateAndDrawPopups(ctx, popups, options) {
        if (!ctx || !Array.isArray(popups) || popups.length === 0) return;

        const opt = options || {};
        const now = typeof opt.now === 'number' ? opt.now : Date.now();
        const dt = typeof opt.dt === 'number' ? opt.dt : 0.016;
        const width = typeof opt.width === 'number' ? opt.width : 1920;
        const height = typeof opt.height === 'number' ? opt.height : 1080;
        const maxAge = typeof opt.maxAge === 'number' ? opt.maxAge : 2.4;
        const fadeStart = typeof opt.fadeStart === 'number' ? opt.fadeStart : 1.2;
        const isNormalized = !!opt.isNormalized;
        const font = opt.font || '700 24px Fredoka, Rubik, sans-serif';

        for (let i = popups.length - 1; i >= 0; i--) {
            const p = popups[i];
            const createdTime = p.createdAtMs || p.createdAt || now;
            const age = (now - createdTime) / 1000;
            if (age > maxAge) {
                popups.splice(i, 1);
                continue;
            }

            // Apply upward drift if dt is provided and driftSpeed is configured
            if (typeof p.y === 'number' && typeof opt.driftSpeed === 'number') {
                if (isNormalized) {
                    p.y -= (opt.driftSpeed / height) * dt;
                } else {
                    p.y -= opt.driftSpeed * dt;
                }
            }

            let alpha;
            if (typeof opt.calculateAlpha === 'function') {
                alpha = opt.calculateAlpha(age, maxAge);
            } else if (age <= fadeStart) {
                alpha = 1.0;
            } else {
                alpha = Math.max(0, 1.0 - ((age - fadeStart) / (maxAge - fadeStart)));
            }

            const px = isNormalized ? p.x * width : p.x;
            const py = isNormalized ? p.y * height : (opt.driftSpeed ? p.y : p.y - age * (opt.yDriftPixels || 32));

            ctx.save();
            ctx.globalAlpha = Math.max(0, Math.min(1, alpha));
            ctx.font = font;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillStyle = p.color || '#FFE066';
            ctx.shadowColor = 'rgba(0,0,0,0.85)';
            ctx.shadowBlur = 8;
            ctx.fillText(p.text || '', px, py);
            ctx.restore();
        }
    }

    function calculateDropAlpha(landedAtMs, now, sessionState, isLeader, options) {
        const elapsed = now - (landedAtMs || now);
        const sState = sessionState || {};

        if (sState.isActive) {
            if (isLeader) {
                return { alpha: 1.0, shouldRemove: false };
            }
            const dimDelay = (options && options.dimDelay) || 3500;
            const dimDuration = (options && options.dimDuration) || 2000;
            const minAlpha = (options && typeof options.minAlpha === 'number') ? options.minAlpha : 0.40;

            if (elapsed > dimDelay) {
                const t = Math.min(1.0, (elapsed - dimDelay) / dimDuration);
                return { alpha: 1.0 - ((1.0 - minAlpha) * t), shouldRemove: false };
            }
            return { alpha: 1.0, shouldRemove: false };
        }

        if (sState.endedAtMs) {
            const postRoundAge = (now - sState.endedAtMs) / 1000;
            if (postRoundAge > 6.0) {
                return { alpha: 0.0, shouldRemove: true };
            }
            if (postRoundAge > 3.5) {
                return { alpha: Math.max(0.0, 1.0 - ((postRoundAge - 3.5) / 2.5)), shouldRemove: false };
            }
            return { alpha: 1.0, shouldRemove: false };
        }

        // Single drop / free play mode
        const lingerTime = (options && options.lingerTime) || 8000;
        const fadeTime = (options && options.fadeTime) || 2500;
        if (elapsed > lingerTime) {
            const alpha = Math.max(0.0, 1.0 - ((elapsed - lingerTime) / fadeTime));
            return { alpha: alpha, shouldRemove: alpha <= 0.0 };
        }
        return { alpha: 1.0, shouldRemove: false };
    }

    function sortDropsForDraw(items) {
        if (!Array.isArray(items)) return items;
        return items.sort((a, b) => {
            if (!!a.isLeader !== !!b.isLeader) return a.isLeader ? 1 : -1;
            if (!!a.completed !== !!b.completed) return a.completed ? -1 : 1;
            return (a.score || 0) - (b.score || 0);
        });
    }

    function drawCircularDecal(ctx, img, radius, options) {
        const opts = options || {};
        const innerRadius = radius || 16;
        ctx.save();
        ctx.beginPath();
        ctx.arc(0, 0, innerRadius, 0, Math.PI * 2);
        ctx.clip();

        let drawn = false;
        const imgW = img ? (img.naturalWidth || img.width || 0) : 0;
        if (img && imgW > 0) {
            try {
                ctx.drawImage(img, -innerRadius, -innerRadius, innerRadius * 2, innerRadius * 2);
                drawn = true;
            } catch (_) {}
        }

        if (!drawn) {
            if (opts.fallbackPenguin && typeof drawSeasonalDefaultPenguin === 'function') {
                drawSeasonalDefaultPenguin(ctx, innerRadius, opts.themeId || 'default');
            } else {
                ctx.fillStyle = opts.userColor || '#3498db';
                ctx.fillRect(-innerRadius, -innerRadius, innerRadius * 2, innerRadius * 2);
                ctx.fillStyle = '#ffffff';
                ctx.font = `bold ${Math.round(innerRadius * 1.1)}px 'Rubik', sans-serif`;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                const initial = (opts.displayName || 'P').charAt(0).toUpperCase();
                ctx.fillText(initial, 0, 1);
            }
        }
        ctx.restore();

        if (opts.strokeBorder) {
            ctx.beginPath();
            ctx.arc(0, 0, innerRadius, 0, Math.PI * 2);
            ctx.strokeStyle = opts.borderColor || 'rgba(255, 255, 255, 0.4)';
            ctx.lineWidth = opts.borderWidth || 1.2;
            ctx.stroke();
        }
    }

    function drawInwardNametag(ctx, displayName, cx, cy, radius, stripPosition, options) {
        ctx.save();
        const opts = options || {};
        const pos = (stripPosition || 'bottom').toLowerCase();

        let labelX = cx;
        let labelY = cy;
        let textAlign = 'center';
        let textBaseline = 'middle';

        const offsetDist = radius + (opts.offsetDist || 16);

        if (pos === 'top') {
            labelX = cx;
            labelY = cy + offsetDist;
            textAlign = 'center';
            textBaseline = 'top';
        } else if (pos === 'left') {
            labelX = cx + offsetDist;
            labelY = cy;
            textAlign = 'left';
            textBaseline = 'middle';
        } else if (pos === 'right') {
            labelX = cx - offsetDist;
            labelY = cy;
            textAlign = 'right';
            textBaseline = 'middle';
        } else {
            // Default: bottom
            labelX = cx;
            labelY = cy - offsetDist;
            textAlign = 'center';
            textBaseline = 'bottom';
        }

        const scoreText = (opts.completed && typeof opts.score === 'number') ? ` (${opts.score} pts)` : '';
        const leaderCrown = opts.isLeader ? '👑 ' : '';
        const displayText = `${leaderCrown}${displayName}${scoreText}`;

        ctx.font = `bold ${opts.fontSize || 15}px 'Rubik', sans-serif`;
        ctx.textAlign = textAlign;
        ctx.textBaseline = textBaseline;

        let nameColor = opts.userColor || '#ffffff';
        if (typeof brightenColor === 'function') {
            nameColor = brightenColor(nameColor);
        }

        ctx.shadowColor = 'rgba(0, 0, 0, 0.95)';
        ctx.shadowBlur = 4;
        ctx.shadowOffsetX = 1;
        ctx.shadowOffsetY = 1;

        ctx.fillStyle = opts.isLeader ? '#FFD700' : nameColor;
        ctx.fillText(displayText, labelX, labelY);
        ctx.restore();
    }

    /**
     * Resolves an isotropic 2D elastic collision between two circular bodies.
     * Updates positions (anti-overlap separation) and velocity vectors in-place according to conservation of momentum.
     *
     * @param {Object} posA - { x: number, y: number }
     * @param {Object} posB - { x: number, y: number }
     * @param {Object} velA - { vx: number, vy: number }
     * @param {Object} velB - { vx: number, vy: number }
     * @param {number} radiusA - Radius of body A
     * @param {number} radiusB - Radius of body B
     * @param {Object} [options] - Optional collision parameters
     * @param {number} [options.restitution=0.82] - Coefficient of restitution (0.0 = inelastic, 1.0 = perfectly elastic)
     * @param {number} [options.massA=1.0] - Mass of body A
     * @param {number} [options.massB=1.0] - Mass of body B
     * @param {boolean} [options.separatePositions=true] - Whether to separate overlapping bodies
     * @param {number} [options.separationPercent=0.50] - Ratio of separation applied to body A vs B
     * @returns {Object} Collision result: { collided: boolean, separating: boolean, impulse: number, normal: { nx, ny }, overlap: number }
     */
    function resolveCircleCollision(posA, posB, velA, velB, radiusA, radiusB, options) {
        if (!posA || !posB || !velA || !velB) return { collided: false, impulse: 0 };
        const opts = options || {};
        const rA = typeof radiusA === 'number' && radiusA > 0 ? radiusA : 10;
        const rB = typeof radiusB === 'number' && radiusB > 0 ? radiusB : 10;
        const minDist = rA + rB;

        const dx = posB.x - posA.x;
        const dy = posB.y - posA.y;
        const distSq = dx * dx + dy * dy;

        if (distSq >= minDist * minDist || distSq <= 0.00001) {
            return { collided: false, impulse: 0 };
        }

        const dist = Math.sqrt(distSq);
        const overlap = minDist - dist;
        const nx = dx / dist;
        const ny = dy / dist;

        const separate = opts.separatePositions !== false;
        if (separate) {
            const sepFrac = typeof opts.separationPercent === 'number' ? opts.separationPercent : 0.50;
            posA.x -= nx * overlap * sepFrac;
            posA.y -= ny * overlap * sepFrac;
            posB.x += nx * overlap * (1.0 - sepFrac);
            posB.y += ny * overlap * (1.0 - sepFrac);
        }

        // Relative velocity along collision normal
        const rvx = velB.vx - velA.vx;
        const rvy = velB.vy - velA.vy;
        const velAlongNormal = rvx * nx + rvy * ny;

        // Do not resolve impulse if objects are already separating
        if (velAlongNormal >= 0) {
            return {
                collided: true,
                separating: true,
                impulse: 0,
                normal: { nx: nx, ny: ny },
                overlap: overlap
            };
        }

        const restitution = typeof opts.restitution === 'number' ? opts.restitution : 0.82;
        const mA = typeof opts.massA === 'number' && opts.massA > 0 ? opts.massA : 1.0;
        const mB = typeof opts.massB === 'number' && opts.massB > 0 ? opts.massB : 1.0;
        const invMassA = 1.0 / mA;
        const invMassB = 1.0 / mB;
        const invMassSum = invMassA + invMassB;

        const impulse = -(1.0 + restitution) * velAlongNormal / (invMassSum > 0 ? invMassSum : 1.0);

        velA.vx -= impulse * invMassA * nx;
        velA.vy -= impulse * invMassA * ny;
        velB.vx += impulse * invMassB * nx;
        velB.vy += impulse * invMassB * ny;

        return {
            collided: true,
            separating: false,
            impulse: impulse,
            normal: { nx: nx, ny: ny },
            overlap: overlap
        };
    }

    function getSeasonPreset(theme) {
        if (theme) {
            if (typeof theme.seasonPreset === 'string' && theme.seasonPreset.length > 0) {
                return theme.seasonPreset.toLowerCase();
            }
            if (typeof theme.id === 'string' && theme.id.length > 0) {
                const idLower = theme.id.toLowerCase();
                for (const s of ['spring', 'summer', 'fall', 'winter', 'halloween', 'christmas']) {
                    if (idLower.includes(s)) return s;
                }
            }
        }
        return 'spring';
    }

    function syncAmbientParticles(ambientParticles, options) {
        if (!ambientParticles || !Array.isArray(ambientParticles)) return;
        const opts = options || {};
        const width = typeof opts.width === 'number' ? opts.width : 1920;
        const height = typeof opts.height === 'number' ? opts.height : 1080;
        const currentTheme = opts.currentTheme;
        const isVisible = opts.isVisible !== false;
        const maxLimit = typeof opts.maxDensity === 'number' ? opts.maxDensity : 60;

        const isEnabled = isVisible && currentTheme && currentTheme.enableBackgroundParticles !== false && currentTheme.particleType !== 'none';
        const desired = isEnabled
            ? Math.min(maxLimit, Math.max(0, currentTheme.particleDensity || 32))
            : 0;

        const pType = (currentTheme && currentTheme.particleType) || 'petals';

        while (ambientParticles.length < desired) {
            const isBat = pType === 'bats';
            ambientParticles.push({
                x: Math.random() * width,
                y: Math.random() * height,
                vx: isBat ? (Math.random() < 0.5 ? -1 : 1) * (40 + Math.random() * 50) : (Math.random() - 0.5) * 30,
                vy: isBat ? (Math.random() - 0.5) * 20 : (pType === 'bubbles' ? -(20 + Math.random() * 35) : (20 + Math.random() * 40)),
                size: isBat ? (10 + Math.random() * 7) : (4 + Math.random() * 6),
                angle: Math.random() * Math.PI * 2,
                spin: isBat ? 0 : (Math.random() - 0.5) * 2.0,
                alpha: 0.35 + Math.random() * 0.45
            });
        }
        if (ambientParticles.length > desired) {
            if (!isVisible) {
                ambientParticles.length = 0;
            } else {
                ambientParticles.length = desired;
            }
        }
    }

    function updateAndDrawAmbientParticles(ctx, ambientParticles, options) {
        if (!ctx || !ambientParticles || !Array.isArray(ambientParticles)) return;
        const opts = options || {};
        const dtSec = typeof opts.dtSec === 'number' ? opts.dtSec : 0.016;
        const width = typeof opts.width === 'number' ? opts.width : 1920;
        const height = typeof opts.height === 'number' ? opts.height : 1080;
        const currentTheme = opts.currentTheme;
        const isVisible = opts.isVisible !== false;

        if (!isVisible) {
            if (ambientParticles.length > 0) ambientParticles.length = 0;
            return;
        }
        if (!ambientParticles.length || !currentTheme || currentTheme.enableBackgroundParticles === false || currentTheme.particleType === 'none') return;
        const pType = currentTheme.particleType || 'petals';

        ctx.save();
        for (let i = 0; i < ambientParticles.length; i++) {
            const p = ambientParticles[i];
            p.x += p.vx * dtSec;
            p.y += p.vy * dtSec;
            p.angle += p.spin * dtSec;

            if (pType === 'bats') {
                p.y += Math.sin(p.x * 0.05) * 0.6;
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
                ctx.ellipse(0, 0, p.size, p.size * 0.55, 0, 0, Math.PI * 2);
                ctx.fill();
            } else if (pType === 'bubbles') {
                ctx.strokeStyle = currentTheme.primaryColor || '#00B4D8';
                ctx.lineWidth = 1.5;
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
                ctx.fillStyle = currentTheme.secondaryColor || '#7B2CBF';
                const flap = Math.sin((p.x + (typeof performance !== 'undefined' ? performance.now() : Date.now()) * 0.02) * 0.4) * (p.size * 0.35);
                ctx.beginPath();
                ctx.ellipse(0, 0, p.size * 0.35, p.size * 0.22, 0, 0, Math.PI * 2);
                ctx.fill();
                ctx.beginPath();
                ctx.moveTo(-p.size * 0.2, 0);
                ctx.quadraticCurveTo(-p.size * 0.7, -p.size * 0.7 - flap, -p.size * 1.2, -p.size * 0.2 - flap);
                ctx.quadraticCurveTo(-p.size * 0.7, p.size * 0.2, -p.size * 0.2, 0);
                ctx.fill();
                ctx.beginPath();
                ctx.moveTo(p.size * 0.2, 0);
                ctx.quadraticCurveTo(p.size * 0.7, -p.size * 0.7 - flap, p.size * 1.2, -p.size * 0.2 - flap);
                ctx.quadraticCurveTo(p.size * 0.7, p.size * 0.2, p.size * 0.2, 0);
                ctx.fill();
            } else if (pType === 'stars') {
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

    function spawnSeasonalBurst(particlesArray, options) {
        if (!particlesArray || !Array.isArray(particlesArray)) return;
        const opts = options || {};
        const x = typeof opts.x === 'number' ? opts.x : 0;
        const y = typeof opts.y === 'number' ? opts.y : 0;
        const season = (opts.season || 'spring').toLowerCase();
        const isBullseye = !!opts.isBullseye;
        const isNormalized = !!opts.isNormalized;
        const count = typeof opts.count === 'number' ? opts.count : (isBullseye ? 35 : 18);

        let colors = opts.colors;
        let type = opts.type || 'spark';
        if (!colors) {
            if (season === 'spring') {
                colors = ['#FF8FA3', '#FFB7C5', '#FFE066', '#FFFFFF', '#FFCCD5'];
                type = 'petal';
            } else if (season === 'summer') {
                colors = ['#FFD166', '#00B4D8', '#FF9F1C', '#FFFFFF', '#06D6A0'];
                type = isBullseye ? 'confetti' : 'spark';
            } else if (season === 'fall') {
                colors = ['#E76F51', '#E9C46A', '#D90429', '#F4A261', '#9B2226'];
                type = 'leaf';
            } else if (season === 'winter') {
                colors = ['#CAF0F8', '#90E0EF', '#00B4D8', '#FFFFFF', '#E0F2FE'];
                type = 'snow';
            } else if (season === 'halloween') {
                colors = ['#39FF14', '#7B2CBF', '#FF7900', '#9D4EDD', '#240046'];
                type = isBullseye ? 'spark' : 'confetti';
            } else if (season === 'christmas') {
                colors = ['#D90429', '#2B9348', '#FFD700', '#FFFFFF', '#55A630'];
                type = isBullseye ? 'confetti' : 'spark';
            } else {
                colors = ['#FFD700', '#3498DB', '#2ECC71', '#E74C3C'];
            }
        }

        const scale = isNormalized ? 0.001 : 1.0;
        for (let i = 0; i < count; i++) {
            const angle = Math.random() * Math.PI * 2;
            const spd = (30 + Math.random() * (isBullseye ? 110 : 65)) * scale;
            particlesArray.push({
                x: x,
                y: y,
                vx: Math.cos(angle) * spd,
                vy: Math.sin(angle) * spd - (isBullseye ? 25 : 10) * scale,
                size: (3.5 + Math.random() * (isBullseye ? 5.5 : 3.5)),
                color: colors[Math.floor(Math.random() * colors.length)],
                alpha: 1.0,
                decay: 0.018 + Math.random() * 0.022,
                type: type,
                rotation: Math.random() * Math.PI * 2,
                rotSpeed: (Math.random() - 0.5) * 8.0,
                isNormalized: isNormalized
            });
        }
    }

    function updateAndDrawParticles(ctx, particlesArray, options) {
        if (!ctx || !particlesArray || !Array.isArray(particlesArray)) return;
        const opts = options || {};
        const dt = typeof opts.dt === 'number' ? opts.dt : 0.016;
        const width = typeof opts.width === 'number' ? opts.width : 1920;
        const height = typeof opts.height === 'number' ? opts.height : 1080;
        const gravity = typeof opts.gravity === 'number' ? opts.gravity : 35;

        for (let i = particlesArray.length - 1; i >= 0; i--) {
            const p = particlesArray[i];
            if (typeof p.maxLife === 'number' && p.maxLife > 0) {
                p.life = (p.life || 0) + dt;
                if (p.life >= p.maxLife) {
                    particlesArray.splice(i, 1);
                    continue;
                }
                p.alpha = 1.0 - (p.life / p.maxLife);
            } else {
                p.alpha -= (p.decay || 0.02);
                if (p.alpha <= 0) {
                    particlesArray.splice(i, 1);
                    continue;
                }
            }

            p.x += p.vx * dt;
            p.y += p.vy * dt;
            p.vy += (p.gravity ?? (p.isNormalized ? gravity * 0.01 : gravity)) * dt;

            ctx.save();
            ctx.globalAlpha = Math.max(0, p.alpha);
            ctx.fillStyle = p.color;
            const drawX = p.isNormalized ? p.x * width : p.x;
            const drawY = p.isNormalized ? p.y * height : p.y;
            ctx.translate(drawX, drawY);

            const pType = p.type || p.shape || 'spark';
            const rotSpeed = p.rotSpeed ?? p.spin ?? 2;

            if (pType === 'confetti' || pType === 'rubber') {
                p.rotation = (p.rotation || 0) + rotSpeed * dt;
                ctx.rotate(p.rotation);
                ctx.fillRect(-p.size / 2, -p.size / 4, p.size, p.size / 2);
            } else if (pType === 'petal') {
                p.rotation = (p.rotation || 0) + rotSpeed * dt;
                ctx.rotate(p.rotation);
                ctx.beginPath();
                ctx.ellipse(0, 0, p.size, p.size * 0.55, 0, 0, Math.PI * 2);
                ctx.fill();
            } else if (pType === 'leaf') {
                p.rotation = (p.rotation || 0) + rotSpeed * dt;
                ctx.rotate(p.rotation);
                ctx.beginPath();
                ctx.moveTo(0, -p.size);
                ctx.quadraticCurveTo(p.size * 0.8, 0, 0, p.size);
                ctx.quadraticCurveTo(-p.size * 0.8, 0, 0, -p.size);
                ctx.fill();
            } else if (pType === 'candies') {
                p.rotation = (p.rotation || 0) + rotSpeed * dt;
                ctx.rotate(p.rotation);
                const s = p.size;
                ctx.fillStyle = '#FFE066';
                ctx.beginPath();
                ctx.moveTo(-s * 0.7, s);
                ctx.lineTo(s * 0.7, s);
                ctx.lineTo(s * 0.45, s * 0.25);
                ctx.lineTo(-s * 0.45, s * 0.25);
                ctx.closePath();
                ctx.fill();
                ctx.fillStyle = '#FF7518';
                ctx.beginPath();
                ctx.moveTo(-s * 0.45, s * 0.25);
                ctx.lineTo(s * 0.45, s * 0.25);
                ctx.lineTo(s * 0.22, -s * 0.45);
                ctx.lineTo(-s * 0.22, -s * 0.45);
                ctx.closePath();
                ctx.fill();
                ctx.fillStyle = '#FFFFFF';
                ctx.beginPath();
                ctx.moveTo(-s * 0.22, -s * 0.45);
                ctx.lineTo(s * 0.22, -s * 0.45);
                ctx.lineTo(0, -s);
                ctx.closePath();
                ctx.fill();
            } else if (pType === 'water') {
                ctx.beginPath();
                ctx.ellipse(0, 0, p.size * 0.65, p.size * 1.15, 0, 0, Math.PI * 2);
                ctx.fill();
            } else {
                ctx.beginPath();
                ctx.arc(0, 0, p.size, 0, Math.PI * 2);
                ctx.fill();
            }
            ctx.restore();
        }
    }

    function drawGolfGreen(ctx, options) {
        if (!ctx || !options) return;
        const cx = typeof options.cx === 'number' ? options.cx : 0;
        const cy = typeof options.cy === 'number' ? options.cy : 0;
        const greenRadius = typeof options.greenRadius === 'number' && options.greenRadius > 0 ? options.greenRadius : 48;
        const cupRadius = typeof options.cupRadius === 'number' && options.cupRadius > 0 ? options.cupRadius : Math.max(7, greenRadius * 0.20);
        const alpha = typeof options.alpha === 'number' ? Math.max(0, Math.min(1, options.alpha)) : 0.45;
        const theme = options.theme || {};
        const season = getSeasonPreset(theme);
        const greenImage = options.greenImage;
        const flagImage = options.flagImage;
        const now = typeof options.now === 'number' ? options.now : Date.now();

        if (alpha <= 0.005) return;

        let fringeCol = theme.secondaryColor || '#15803D';
        let turfInner = '#4ADE80';
        let turfMid = '#22C55E';
        let turfOuter = '#16A34A';
        let cupRimCol = '#E2E8F0';
        let poleCol = '#F8FAFC';
        let flagCol = theme.accentColor || '#EF4444';

        if (season === 'spring') {
            fringeCol = '#F472B6';
            turfInner = '#6EE7B7';
            turfMid = '#34D399';
            turfOuter = '#10B981';
            cupRimCol = '#FBCFE8';
            flagCol = '#FB7185';
        } else if (season === 'summer') {
            fringeCol = '#FBBF24';
            turfInner = '#86EFAC';
            turfMid = '#22C55E';
            turfOuter = '#15803D';
            cupRimCol = '#38BDF8';
            flagCol = '#FACC15';
        } else if (season === 'fall') {
            fringeCol = '#D97706';
            turfInner = '#84CC16';
            turfMid = '#4D7C0F';
            turfOuter = '#3F6212';
            cupRimCol = '#F59E0B';
            flagCol = '#EA580C';
        } else if (season === 'winter') {
            fringeCol = '#7DD3FC';
            turfInner = '#E0F2FE';
            turfMid = '#BAE6FD';
            turfOuter = '#38BDF8';
            cupRimCol = '#FFFFFF';
            flagCol = '#0284C7';
        } else if (season === 'halloween') {
            fringeCol = '#7E22CE';
            turfInner = '#4ADE80';
            turfMid = '#15803D';
            turfOuter = '#3B0764';
            cupRimCol = '#A3E635';
            flagCol = '#F97316';
        } else if (season === 'christmas') {
            fringeCol = '#DC2626';
            turfInner = '#22C55E';
            turfMid = '#15803D';
            turfOuter = '#14532D';
            cupRimCol = '#FACC15';
            flagCol = '#EF4444';
        }

        ctx.save();
        ctx.translate(cx, cy);

        // Putting green surface (custom sprite or compact lush radial green + fringe)
        ctx.save();
        ctx.globalAlpha = alpha;
        if (greenImage && ((greenImage.naturalWidth || greenImage.width || 0) > 0)) {
            ctx.drawImage(greenImage, -greenRadius * 1.15, -greenRadius * 1.15, greenRadius * 2.3, greenRadius * 2.3);
        } else {
            // Outer fringe collar
            ctx.beginPath();
            ctx.arc(0, 0, greenRadius * 1.08, 0, Math.PI * 2);
            ctx.fillStyle = fringeCol;
            ctx.fill();

            // Inner putting surface
            const grad = ctx.createRadialGradient(-greenRadius * 0.2, -greenRadius * 0.2, greenRadius * 0.1, 0, 0, greenRadius);
            grad.addColorStop(0, turfInner);
            grad.addColorStop(0.7, turfMid);
            grad.addColorStop(1, turfOuter);
            ctx.beginPath();
            ctx.arc(0, 0, greenRadius, 0, Math.PI * 2);
            ctx.fillStyle = grad;
            ctx.fill();

            // Subtle closest-to-the-pin yardage guide rings
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.38)';
            ctx.lineWidth = 1.25;
            [0.38, 0.68, 1.0].forEach((ratio) => {
                ctx.beginPath();
                ctx.arc(0, 0, greenRadius * ratio, 0, Math.PI * 2);
                ctx.stroke();
            });
        }
        ctx.restore();

        // Sunken Cup (high visibility even when green is semi-transparent)
        const cupAlpha = Math.min(1.0, Math.max(0.75, alpha * 1.65));
        ctx.save();
        ctx.globalAlpha = cupAlpha;
        ctx.beginPath();
        ctx.arc(0, 0, cupRadius + 1.6, 0, Math.PI * 2);
        ctx.fillStyle = cupRimCol;
        ctx.fill();

        ctx.beginPath();
        ctx.arc(0, 0, cupRadius, 0, Math.PI * 2);
        ctx.fillStyle = '#0F172A';
        ctx.fill();

        // Inner cup shadow rim
        ctx.beginPath();
        ctx.arc(0, 1.2, cupRadius * 0.82, 0, Math.PI * 2);
        ctx.fillStyle = '#020617';
        ctx.fill();

        // Flagstick + Pennant
        const poleHeight = Math.max(26, Math.min(48, greenRadius * 0.85));
        if (flagImage && ((flagImage.naturalWidth || flagImage.width || 0) > 0)) {
            ctx.drawImage(flagImage, -poleHeight * 0.35, -poleHeight, poleHeight * 0.9, poleHeight * 1.05);
        } else {
            // Flagpole shadow
            ctx.strokeStyle = 'rgba(0, 0, 0, 0.35)';
            ctx.lineWidth = 2.2;
            ctx.beginPath();
            ctx.moveTo(0, 0);
            ctx.lineTo(poleHeight * 0.35, -poleHeight * 0.18);
            ctx.stroke();

            // Flagpole
            ctx.strokeStyle = poleCol;
            ctx.lineWidth = 2.4;
            ctx.beginPath();
            ctx.moveTo(0, 0);
            ctx.lineTo(0, -poleHeight);
            ctx.stroke();

            // Fluttering pennant flag
            const wave = Math.sin(now * 0.005) * 2.5;
            ctx.fillStyle = flagCol;
            ctx.strokeStyle = '#FFFFFF';
            ctx.lineWidth = 1.2;
            ctx.beginPath();
            ctx.moveTo(0, -poleHeight);
            ctx.quadraticCurveTo(poleHeight * 0.25, -poleHeight + 4 + wave, poleHeight * 0.48, -poleHeight + 7 - wave * 0.5);
            ctx.lineTo(0, -poleHeight + 15);
            ctx.closePath();
            ctx.fill();
            ctx.stroke();
        }
        ctx.restore();

        ctx.restore();
    }

    function drawGolfBall(ctx, options) {
        if (!ctx || !options) return;
        const x = typeof options.x === 'number' ? options.x : 0;
        const y = typeof options.y === 'number' ? options.y : 0;
        const radius = typeof options.radius === 'number' && options.radius > 0 ? options.radius : 15;
        const heightZ = typeof options.heightZ === 'number' ? Math.max(0, options.heightZ) : 0;
        const sinkScale = typeof options.sinkScale === 'number' ? Math.max(0.1, Math.min(1, options.sinkScale)) : 1.0;
        const rotation = typeof options.rotation === 'number' ? options.rotation : 0;
        const userColor = options.userColor || '#22C55E';
        const avatarImg = options.avatarImg;
        const ballImg = options.ballImg;
        const themeId = options.themeId || 'default';
        const displayName = options.displayName || 'Player';
        const isLeader = !!options.isLeader;
        const isHoleInOne = !!options.isHoleInOne;

        // Perspective lift when ball is airborne (heightZ in [0..1])
        const liftPx = heightZ * Math.max(36, radius * 3.2);
        const visualScale = (1.0 + heightZ * 0.34) * sinkScale;
        const drawR = radius * visualScale;

        ctx.save();

        // Ground shadow tracks directly on the turf (x, y), shrinking & softening with heightZ
        const shadowScale = Math.max(0.45, 1.0 - heightZ * 0.45) * sinkScale;
        const shadowAlpha = Math.max(0.18, 0.52 - heightZ * 0.28) * sinkScale;
        ctx.save();
        ctx.translate(x + heightZ * 6, y + 3 + heightZ * 4);
        ctx.fillStyle = `rgba(0, 0, 0, ${shadowAlpha.toFixed(3)})`;
        ctx.beginPath();
        ctx.ellipse(0, 0, radius * 0.95 * shadowScale, radius * 0.62 * shadowScale, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();

        // Translate to elevated ball center
        ctx.translate(x, y - liftPx);

        // Leader / Hole-in-One golden aura
        if (isLeader || isHoleInOne) {
            ctx.save();
            ctx.shadowColor = '#FFD700';
            ctx.shadowBlur = 14;
            ctx.strokeStyle = '#FFD700';
            ctx.lineWidth = 2.2;
            ctx.beginPath();
            ctx.arc(0, 0, drawR + 2.5, 0, Math.PI * 2);
            ctx.stroke();
            ctx.restore();
        }

        ctx.save();
        ctx.rotate(rotation);

        if (ballImg && ((ballImg.naturalWidth || ballImg.width || 0) > 0)) {
            ctx.drawImage(ballImg, -drawR, -drawR, drawR * 2, drawR * 2);
        } else {
            // 3D dimpled golf ball body
            const ballGrad = ctx.createRadialGradient(-drawR * 0.3, -drawR * 0.3, drawR * 0.1, 0, 0, drawR);
            ballGrad.addColorStop(0, '#FFFFFF');
            ballGrad.addColorStop(0.72, '#F1F5F9');
            ballGrad.addColorStop(1, '#CBD5E1');
            ctx.beginPath();
            ctx.arc(0, 0, drawR, 0, Math.PI * 2);
            ctx.fillStyle = ballGrad;
            ctx.fill();

            // Subtle dimple texture dots around outer rim
            ctx.fillStyle = 'rgba(100, 116, 139, 0.28)';
            const dimpleAngles = [0, 0.9, 1.8, 2.7, 3.6, 4.5, 5.4];
            for (let i = 0; i < dimpleAngles.length; i++) {
                const a = dimpleAngles[i];
                const dx = Math.cos(a) * (drawR * 0.78);
                const dy = Math.sin(a) * (drawR * 0.78);
                ctx.beginPath();
                ctx.arc(dx, dy, Math.max(1.2, drawR * 0.09), 0, Math.PI * 2);
                ctx.fill();
            }

            // Player color identification stripe ring
            ctx.beginPath();
            ctx.arc(0, 0, drawR * 0.68, 0, Math.PI * 2);
            ctx.strokeStyle = brightenColor(userColor);
            ctx.lineWidth = Math.max(2.0, drawR * 0.15);
            ctx.stroke();

            // Crisp outer ball border
            ctx.beginPath();
            ctx.arc(0, 0, drawR, 0, Math.PI * 2);
            ctx.strokeStyle = 'rgba(15, 23, 42, 0.65)';
            ctx.lineWidth = 1.4;
            ctx.stroke();
        }
        ctx.restore();

        // Center avatar / emote / penguin decal (kept upright so emotes are clearly readable)
        drawCircularDecal(ctx, avatarImg, drawR * 0.64, {
            fallbackPenguin: true,
            themeId: themeId,
            userColor: userColor,
            displayName: displayName,
            strokeBorder: true,
            borderColor: 'rgba(255, 255, 255, 0.7)',
            borderWidth: 1.1
        });

        ctx.restore();
    }

    const DropGamesCommon = {
        normalizeImageUrl: normalizeImageUrl,
        detectMimeType: detectMimeType,
        parseAndDecodeGif: parseAndDecodeGif,
        decodeLzw: decodeLzw,
        loadAnimatedFrames: loadAnimatedFrames,
        createImageLoader: createImageLoader,
        brightenColor: brightenColor,
        drawSeasonalDefaultPenguin: drawSeasonalDefaultPenguin,
        drawOutlinedName: drawOutlinedName,
        drawRoundRect: drawRoundRect,
        getBannerX: getBannerX,
        drawSessionBanner: drawSessionBanner,
        updateAndDrawPopups: updateAndDrawPopups,
        calculateDropAlpha: calculateDropAlpha,
        sortDropsForDraw: sortDropsForDraw,
        drawCircularDecal: drawCircularDecal,
        drawInwardNametag: drawInwardNametag,
        resolveCircleCollision: resolveCircleCollision,
        MINIGAME_RANKS: MINIGAME_RANKS,
        calculateBannerStackSlot: calculateBannerStackSlot,
        getSeasonPreset: getSeasonPreset,
        syncAmbientParticles: syncAmbientParticles,
        updateAndDrawAmbientParticles: updateAndDrawAmbientParticles,
        spawnSeasonalBurst: spawnSeasonalBurst,
        updateAndDrawParticles: updateAndDrawParticles,
        drawGolfGreen: drawGolfGreen,
        drawGolfBall: drawGolfBall
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = DropGamesCommon;
    }
    if (typeof global !== 'undefined') {
        global.DropGamesCommon = DropGamesCommon;
    }
    if (typeof window !== 'undefined') {
        window.DropGamesCommon = DropGamesCommon;
    }
    if (typeof globalThis !== 'undefined') {
        globalThis.DropGamesCommon = DropGamesCommon;
    }
})(typeof window !== 'undefined' ? window : this);

