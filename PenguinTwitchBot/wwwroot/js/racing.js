/**
 * Racing Minigame Overlay Client
 * Non-intrusive stream overlay featuring:
 * - Perimeter Border Ring, Edge Strip Oval, Figure-8, and Drag Strip track layouts
 * - Mass-start Grand Prix with configurable lobby countdown, Solo Time Trial with Ghost Car, and Elimination Mode
 * - Vehicle-to-vehicle drafting/slipstream boosts, bump collisions, and rubber-banding
 * - Interactive debris & hazards (oil slicks / ice spinouts, traffic cones, turbo boost pads)
 * - Driver emote integration with seasonal vector penguin fallback
 * - Client as authoritative source of truth reporting authenticated completion tokens
 */
(function (global) {
    'use strict';

    const canvas = document.getElementById('racing-canvas');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');

    const urlParams = new URLSearchParams(window.location.search);
    const overlayKey = urlParams.get('key') || '';

    let dpr = window.devicePixelRatio || 1;
    let width = window.innerWidth;
    let height = window.innerHeight;

    function resize() {
        dpr = window.devicePixelRatio || 1;
        width = window.innerWidth;
        height = window.innerHeight;
        canvas.width = Math.floor(width * dpr);
        canvas.height = Math.floor(height * dpr);
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }
    window.addEventListener('resize', resize);
    resize();

    // Audio & Common dependencies
    const soundManager = (typeof DropGamesAudio !== 'undefined' && DropGamesAudio.createSoundManager)
        ? DropGamesAudio.createSoundManager() : null;
    const commonModule = typeof DropGamesCommon !== 'undefined' ? DropGamesCommon : null;

    // WebSocket connection & completion queue
    const socket = (typeof window !== 'undefined' && typeof window.createWsSocket === 'function' && window.WS_TOPICS)
        ? window.createWsSocket('racing', [window.WS_TOPICS.racing || 'racing'])
        : null;

    const completionChannel = (commonModule && typeof commonModule.createCompletionChannel === 'function')
        ? commonModule.createCompletionChannel({
            game: 'racing',
            socket: socket,
            overlayKey: overlayKey,
            fields: ['dropToken', 'racePlacement', 'lapTimeSeconds', 'wreckCount', 'score', 'isWin', 'racingMode', 'lapsCompleted', 'knockoutCount', 'killerDropToken', 'isTotaled'],
            getActiveTokens: () => Array.isArray(activeRacers)
                ? activeRacers.filter(r => r && r.dropToken && !r.finished).map(r => r.dropToken)
                : []
        }) : null;

    if (completionChannel) {
        completionChannel.attach(msg => handleWsMessage(msg));
    }

    // Game state
    let gameState = 'idle'; // 'idle', 'lobby', 'countdown', 'racing', 'podium'
    let overlayAlpha = 0.0;
    let targetOverlayAlpha = 0.0;

    let currentSettings = {
        trackType: 'Perimeter',
        stripPosition: 'Bottom',
        totalLaps: 2,
        gameMode: 'GrandPrix',
        trackAlpha: 0.55,
        showTrackBorders: true,
        trackThickness: 44,
        baseSpeed: 0.5,
        enableCollisions: true,
        enableDrafting: true,
        enableDebris: true,
        debrisDensity: 6,
        enableRubberBanding: true,
        rubberBandStrength: 0.25,
        enableVehicleDamage: false,
        enableItemPickups: true,
        startingHealth: 100,
        ramDamageMultiplier: 1.0,
        derbyKnockoutPoints: 20
    };

    let currentTheme = null;
    let currentSounds = null;
    let currentMessages = null;

    // Lobby state
    let lobbyRemainingMs = 0;
    let lobbyTotalMs = 60000;
    let lobbyRacers = [];

    // Active race state
    let activeRacers = [];
    let finishedRacers = [];
    let nextFrontPlacement = 1;
    let nextBackPlacement = 1;
    let trackHazards = [];
    let particles = [];
    let detachedParts = [];
    let raceStartTime = 0;
    let raceElapsedTime = 0;
    let countdownStep = 0; // 3, 2, 1, GO
    let countdownTimer = 0;
    let podiumTimer = 0;
    let eliminatedLaps = new Set();
    let eliminationBanner = null;

    // Time trial state & ghost
    let timeTrialGhost = null;
    let lastRecordedLapMs = null;
    let lastRecordedLapRacer = null;
    let currentHeatInfo = null;

    function getBestLapStorageKey(track) {
        return 'racing_best_lap_' + (track || 'perimeter').toLowerCase();
    }
    function loadBestLap(track) {
        try {
            const raw = localStorage.getItem(getBestLapStorageKey(track));
            if (raw) {
                const parsed = JSON.parse(raw);
                if (parsed && typeof parsed.timeMs === 'number') return parsed;
            }
        } catch (e) {}
        return null;
    }
    function saveBestLap(track, timeMs, racerName) {
        try {
            localStorage.setItem(getBestLapStorageKey(track), JSON.stringify({
                timeMs: timeMs,
                racerName: racerName || 'Racer',
                date: Date.now()
            }));
        } catch (e) {}
    }

    // Preloaded image cache
    const imageCache = new Map();
    function getImage(url) {
        if (!url) return null;
        if (imageCache.has(url)) return imageCache.get(url);
        const img = new Image();
        img.crossOrigin = 'anonymous';
        img.src = url;
        imageCache.set(url, img);
        return img;
    }

    // Sound player helper
    function playSound(type) {
        try {
            // 1. Resolve sound configuration DTO from currentSounds bundle
            let soundDto = null;
            let isHighPriority = false;

            if (currentSounds) {
                if (type === 'countdown_beep') {
                    soundDto = currentSounds.racingCountdown;
                } else if (type === 'start' || type === 'countdown_go') {
                    soundDto = currentSounds.racingStart || currentSounds.start;
                    isHighPriority = true;
                } else if (type === 'end') {
                    soundDto = currentSounds.racingEnd || currentSounds.end;
                    isHighPriority = true;
                } else if (type === 'engine') {
                    soundDto = currentSounds.racingEngine;
                } else if (type === 'screech') {
                    soundDto = currentSounds.racingScreech;
                } else if (type === 'bump' || type === 'crash') {
                    soundDto = currentSounds.racingCrash;
                } else if (type === 'boost') {
                    soundDto = currentSounds.racingBoost;
                } else if (type === 'win') {
                    soundDto = currentSounds.racingWin || currentSounds.win;
                    isHighPriority = true;
                }
            }

            // Respect explicit mute in settings
            if (soundDto && soundDto.enabled === false) {
                return;
            }

            // 2. Play configured audio asset URL via soundManager or Audio
            if (soundDto && soundDto.enabled && soundDto.url) {
                if (soundManager && typeof soundManager.play === 'function') {
                    soundManager.play(soundDto, type, isHighPriority);
                    return;
                } else if (typeof Audio !== 'undefined') {
                    const volFraction = Math.max(0, Math.min(1, (typeof soundDto.volume === 'number' ? soundDto.volume : 70) / 100));
                    const audio = new Audio(soundDto.url);
                    audio.volume = volFraction;
                    audio.play().catch(() => {});
                    return;
                }
            }
        } catch (_) { }
    }

    // ================= TRACK GEOMETRY =================
    // Evaluates point on track spline at progress t in [0, 1)
    function getTrackPoint(t, trackType, stripPos) {
        t = ((t % 1) + 1) % 1;
        const padX = 54;
        const padY = 54;
        const w = width - padX * 2;
        const h = height - padY * 2;
        const cornerR = Math.min(80, Math.min(w, h) * 0.25);

        if (trackType === 'EdgeOval') {
            let cx, cy, rx, ry;
            const stripThick = Math.min(130, Math.min(width, height) * 0.18);
            if (stripPos === 'Top') {
                cx = width / 2; cy = stripThick / 2 + 10; rx = width * 0.44; ry = stripThick * 0.38;
            } else if (stripPos === 'Left') {
                cx = stripThick / 2 + 10; cy = height / 2; rx = stripThick * 0.38; ry = height * 0.44;
            } else if (stripPos === 'Right') {
                cx = width - stripThick / 2 - 10; cy = height / 2; rx = stripThick * 0.38; ry = height * 0.44;
            } else {
                cx = width / 2; cy = height - stripThick / 2 - 10; rx = width * 0.44; ry = stripThick * 0.38;
            }
            const angle = t * Math.PI * 2;
            const x = cx + Math.cos(angle) * rx;
            const y = cy + Math.sin(angle) * ry;
            const dx = -Math.sin(angle) * rx;
            const dy = Math.cos(angle) * ry;
            return { x, y, angle: Math.atan2(dy, dx), normalX: -Math.sin(Math.atan2(dy, dx)), normalY: Math.cos(Math.atan2(dy, dx)) };
        }

        if (trackType === 'Figure8') {
            const cx = width / 2;
            const cy = height / 2;
            const scaleX = Math.min(width * 0.42, 600);
            const scaleY = Math.min(height * 0.32, 320);
            const theta = t * Math.PI * 2;
            const x = cx + Math.sin(theta) * scaleX;
            const y = cy + (Math.sin(theta * 2) / 2) * scaleY;
            const dt = 0.002;
            const nextTheta = (t + dt) * Math.PI * 2;
            const nx = cx + Math.sin(nextTheta) * scaleX;
            const ny = cy + (Math.sin(nextTheta * 2) / 2) * scaleY;
            const heading = Math.atan2(ny - y, nx - x);
            return { x, y, angle: heading, normalX: -Math.sin(heading), normalY: Math.cos(heading) };
        }

        if (trackType === 'DerbyBowl') {
            const cx = width / 2;
            const cy = height / 2;
            const rx = Math.min(width * 0.44, 700);
            const ry = Math.min(height * 0.38, 420);
            const theta = t * Math.PI * 2;
            const rMod = 1.0 - 0.16 * Math.cos(theta * 2);
            const x = cx + Math.cos(theta) * rx * rMod;
            const y = cy + Math.sin(theta) * ry * rMod;
            const dt = 0.002;
            const nextTheta = (t + dt) * Math.PI * 2;
            const nextRMod = 1.0 - 0.16 * Math.cos(nextTheta * 2);
            const nx = cx + Math.cos(nextTheta) * rx * nextRMod;
            const ny = cy + Math.sin(nextTheta) * ry * nextRMod;
            const heading = Math.atan2(ny - y, nx - x);
            return { x, y, angle: heading, normalX: -Math.sin(heading), normalY: Math.cos(heading) };
        }

        if (trackType === 'DragStrip') {
            const y = (stripPos === 'Top') ? padY + 20 : (height - padY - 20);
            const startX = padX + 30;
            const endX = width - padX - 30;
            const x = startX + t * (endX - startX);
            return { x, y, angle: 0, normalX: 0, normalY: 1 };
        }

        // Default: Perimeter Track (Rounded border circuit hugging screen edges)
        // Perimeter path has 4 straight edges + 4 rounded corner quarter-arcs
        const straightW = w - 2 * cornerR;
        const straightH = h - 2 * cornerR;
        const arcLen = (Math.PI * 2 * cornerR) / 4;
        const totalPerimeter = (straightW * 2) + (straightH * 2) + (arcLen * 4);
        let dist = t * totalPerimeter;

        let x = 0, y = 0, heading = 0;

        // Top straight (left to right)
        if (dist < straightW) {
            x = padX + cornerR + dist;
            y = padY;
            heading = 0;
        } else if ((dist -= straightW) < arcLen) {
            // Top-right corner
            const a = -Math.PI / 2 + (dist / arcLen) * (Math.PI / 2);
            x = padX + cornerR + straightW + Math.cos(a) * cornerR;
            y = padY + cornerR + Math.sin(a) * cornerR;
            heading = a + Math.PI / 2;
        } else if ((dist -= arcLen) < straightH) {
            // Right straight (top to bottom)
            x = padX + w;
            y = padY + cornerR + dist;
            heading = Math.PI / 2;
        } else if ((dist -= straightH) < arcLen) {
            // Bottom-right corner
            const a = 0 + (dist / arcLen) * (Math.PI / 2);
            x = padX + cornerR + straightW + Math.cos(a) * cornerR;
            y = padY + cornerR + straightH + Math.sin(a) * cornerR;
            heading = a + Math.PI / 2;
        } else if ((dist -= arcLen) < straightW) {
            // Bottom straight (right to left)
            x = padX + cornerR + straightW - dist;
            y = padY + h;
            heading = Math.PI;
        } else if ((dist -= straightW) < arcLen) {
            // Bottom-left corner
            const a = Math.PI / 2 + (dist / arcLen) * (Math.PI / 2);
            x = padX + cornerR + Math.cos(a) * cornerR;
            y = padY + cornerR + straightH + Math.sin(a) * cornerR;
            heading = a + Math.PI / 2;
        } else if ((dist -= arcLen) < straightH) {
            // Left straight (bottom to top)
            x = padX;
            y = padY + cornerR + straightH - dist;
            heading = -Math.PI / 2;
        } else {
            // Top-left corner
            dist -= straightH;
            const a = Math.PI + (dist / arcLen) * (Math.PI / 2);
            x = padX + cornerR + Math.cos(a) * cornerR;
            y = padY + cornerR + Math.sin(a) * cornerR;
            heading = a + Math.PI / 2;
        }

        return {
            x,
            y,
            angle: heading,
            normalX: -Math.sin(heading),
            normalY: Math.cos(heading)
        };
    }

    // ================= VEHICLE DAMAGE & DETACHABLE PARTS =================
    function spawnDetachedPart(x, y, angle, type, color) {
        const speed = 40 + Math.random() * 60;
        const partAngle = angle + (Math.random() * 1.4 - 0.7);
        detachedParts.push({
            x: x,
            y: y,
            vx: Math.cos(partAngle) * speed,
            vy: Math.sin(partAngle) * speed,
            rot: angle,
            vRot: (Math.random() * 8 - 4),
            type: type, // 'bumper', 'spoiler', 'wheel'
            color: color || '#1e232a',
            alpha: 1.0,
            life: 14.0 // stays on track for 14 seconds
        });
    }

    function checkPartLoss(racer, x, y, angle) {
        const maxHp = racer.maxHp || 100;
        const curHp = racer.hp ?? maxHp;
        const hpPercent = curHp / maxHp;

        if (hpPercent <= 0.75 && !racer.lostFrontBumper) {
            racer.lostFrontBumper = true;
            spawnDetachedPart(x, y, angle, 'bumper', '#1e232a');
        }
        if (hpPercent <= 0.50 && !racer.lostSpoiler) {
            racer.lostSpoiler = true;
            spawnDetachedPart(x, y, angle + Math.PI, 'spoiler', '#1e232a');
        }
        if (hpPercent <= 0.25 && !racer.lostWheel) {
            racer.lostWheel = true;
            spawnDetachedPart(x, y, angle + Math.PI / 2, 'wheel', '#111315');
        }
    }

    function drawDetachedParts() {
        if (detachedParts.length === 0) return;
        ctx.save();
        detachedParts.forEach(p => {
            ctx.save();
            ctx.globalAlpha = p.alpha * overlayAlpha;
            ctx.translate(p.x, p.y);
            ctx.rotate(p.rot);
            ctx.fillStyle = p.color;
            if (p.type === 'wheel') {
                ctx.beginPath();
                ctx.roundRect(-4, -2, 8, 4, 2);
                ctx.fill();
            } else if (p.type === 'bumper') {
                ctx.beginPath();
                ctx.roundRect(-2, -8, 4, 16, 2);
                ctx.fill();
            } else if (p.type === 'spoiler') {
                ctx.beginPath();
                ctx.roundRect(-2, -9, 4, 18, 2);
                ctx.fill();
            }
            ctx.restore();
        });
        ctx.restore();
    }

    // Spawns debris, power-ups and boost pads along the track
    function generateHazards(density, trackType, stripPos) {
        const list = [];
        if (density <= 0) return list;
        const count = Math.min(16, density);
        const isDerby = currentSettings.gameMode === 'DestructionDerby';
        for (let i = 0; i < count; i++) {
            const t = 0.08 + (i + 0.5) / count * 0.84 + (Math.random() * 0.04 - 0.02);
            let types = ['oil', 'cone', 'boost', 'cone'];
            if (isDerby) {
                // Demolition Derby hazards: NO shields! Boosts, oil slicks, repair wrenches, bombs, and spikes
                types = ['boost', 'oil', 'wrench', 'bomb', 'spike', 'wrench'];
            } else if (currentSettings.enableItemPickups) {
                types = ['oil', 'cone', 'boost', 'wrench', 'cone', 'shield'];
            }
            const type = types[i % types.length];
            const laneOffset = (Math.random() * 2 - 1) * (currentSettings.trackThickness * (isDerby ? 0.38 : 0.32));
            list.push({
                t: t % 1.0,
                type: type,
                laneOffset: laneOffset,
                active: true,
                respawnTimer: 0
            });
        }
        return list;
    }

    // ================= THEMED STYLING UTILITIES =================

    function getThemeTrackStyle() {
        const season = (currentTheme && currentTheme.seasonPreset) ? currentTheme.seasonPreset.toLowerCase() : 'spring';
        const pColor = (currentTheme && (currentTheme.primaryColor || currentTheme.PrimaryColor)) || '#FF5252';
        const sColor = (currentTheme && (currentTheme.secondaryColor || currentTheme.SecondaryColor)) || '#FFFFFF';

        switch (season) {
            case 'winter':
                return {
                    surfaceColor: '#344754',
                    innerGlow: 'rgba(180, 230, 255, 0.35)',
                    curb1: '#FFFFFF',
                    curb2: '#00D2D3',
                    dashColor: 'rgba(230, 248, 255, 0.8)',
                    dashPattern: [10, 10],
                    finishColor1: '#344754',
                    finishColor2: '#B3E5FC',
                    season
                };
            case 'halloween':
                return {
                    surfaceColor: '#1a1424',
                    innerGlow: 'rgba(128, 0, 128, 0.32)',
                    curb1: '#FF7518',
                    curb2: '#39FF14',
                    dashColor: 'rgba(255, 117, 24, 0.75)',
                    dashPattern: [8, 12],
                    finishColor1: '#1a1424',
                    finishColor2: '#FF7518',
                    season
                };
            case 'christmas':
                return {
                    surfaceColor: '#1b2920',
                    innerGlow: 'rgba(0, 255, 128, 0.18)',
                    curb1: '#FF2A4B',
                    curb2: '#FFFFFF',
                    dashColor: 'rgba(255, 230, 150, 0.8)',
                    dashPattern: [12, 8],
                    finishColor1: '#FF2A4B',
                    finishColor2: '#FFFFFF',
                    season
                };
            case 'fall':
                return {
                    surfaceColor: '#362a20',
                    innerGlow: 'rgba(220, 110, 30, 0.22)',
                    curb1: '#E65100',
                    curb2: '#FFD54F',
                    dashColor: 'rgba(255, 200, 100, 0.7)',
                    dashPattern: [8, 12],
                    finishColor1: '#362a20',
                    finishColor2: '#FFD54F',
                    season
                };
            case 'summer':
                return {
                    surfaceColor: '#1d2730',
                    innerGlow: 'rgba(0, 210, 255, 0.2)',
                    curb1: '#FFD166',
                    curb2: '#06D6A0',
                    dashColor: 'rgba(255, 255, 255, 0.65)',
                    dashPattern: [8, 12],
                    finishColor1: '#1d2730',
                    finishColor2: '#06D6A0',
                    season
                };
            case 'spring':
                return {
                    surfaceColor: '#26202a',
                    innerGlow: 'rgba(255, 143, 163, 0.25)',
                    curb1: '#FF8FA3',
                    curb2: '#FFF0F5',
                    dashColor: 'rgba(255, 255, 255, 0.6)',
                    dashPattern: [8, 12],
                    finishColor1: '#26202a',
                    finishColor2: '#FF8FA3',
                    season
                };
            default:
                return {
                    surfaceColor: '#22272e',
                    innerGlow: null,
                    curb1: pColor,
                    curb2: sColor || '#FFFFFF',
                    dashColor: 'rgba(255, 255, 255, 0.5)',
                    dashPattern: [8, 12],
                    finishColor1: '#000000',
                    finishColor2: '#FFFFFF',
                    season
                };
        }
    }

    function spawnTireSpray(racer, pt, angle, season) {
        season = (season || 'spring').toLowerCase();
        const rx = racer.currentX || (pt.x + pt.normalX * racer.laneOffset);
        const ry = racer.currentY || (pt.y + pt.normalY * racer.laneOffset);

        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        // Pick left or right rear tire
        const side = (Math.random() < 0.5 ? 7 : -7);
        const spawnX = rx - cos * 14 - sin * side;
        const spawnY = ry - sin * 14 + cos * side;

        const baseSpeed = -(14 + Math.random() * 20);
        const spread = (Math.random() - 0.5) * 12;
        const vx = (cos * baseSpeed - sin * spread) * 0.05;
        const vy = (sin * baseSpeed + cos * spread) * 0.05;

        let color, shape, size, fade;
        switch (season) {
            case 'winter':
                color = Math.random() < 0.65 ? '#FFFFFF' : '#B3E5FC';
                shape = 'snow';
                size = 1.6 + Math.random() * 2.2;
                fade = 0.035 + Math.random() * 0.02;
                break;
            case 'fall':
                const autumnColors = ['#FF6F00', '#D84315', '#FBC02D', '#8D6E63'];
                color = autumnColors[Math.floor(Math.random() * autumnColors.length)];
                shape = 'leaf';
                size = 2.4 + Math.random() * 2.4;
                fade = 0.028 + Math.random() * 0.015;
                break;
            case 'halloween':
                color = Math.random() < 0.5 ? '#BF55EC' : '#39FF14';
                shape = 'spark';
                size = 1.6 + Math.random() * 2.0;
                fade = 0.032 + Math.random() * 0.018;
                break;
            case 'spring':
                color = Math.random() < 0.7 ? '#FFB7C5' : '#FFF0F5';
                shape = 'petal';
                size = 2.0 + Math.random() * 2.0;
                fade = 0.028 + Math.random() * 0.015;
                break;
            case 'christmas':
                color = Math.random() < 0.4 ? '#FF2A4B' : (Math.random() < 0.5 ? '#00E676' : '#FFFFFF');
                shape = 'snow';
                size = 1.6 + Math.random() * 2.2;
                fade = 0.035 + Math.random() * 0.02;
                break;
            default:
                color = Math.random() < 0.5 ? '#718096' : '#A0AEC0';
                shape = 'smoke';
                size = 2.0 + Math.random() * 2.5;
                fade = 0.04 + Math.random() * 0.02;
                break;
        }

        particles.push({
            x: spawnX,
            y: spawnY,
            vx,
            vy,
            color,
            shape,
            size,
            alpha: 0.85,
            fade,
            rotation: Math.random() * Math.PI * 2,
            rotSpeed: (Math.random() - 0.5) * 0.15
        });
    }

    // ================= DRAWING UTILITIES =================

    function drawTrack() {
        const thickness = currentSettings.trackThickness || 44;
        const samples = 140;
        const points = [];
        for (let i = 0; i <= samples; i++) {
            points.push(getTrackPoint(i / samples, currentSettings.trackType, currentSettings.stripPosition));
        }

        const trackStyle = getThemeTrackStyle();

        ctx.save();
        ctx.globalAlpha = (currentSettings.trackAlpha || 0.55) * overlayAlpha;

        // 1. Road Surface (themed or custom track texture)
        const trackImgUrl = currentTheme && (currentTheme.racingTrackImageUrl || currentTheme.RacingTrackImageUrl);
        const customTrack = getImage(trackImgUrl);

        ctx.beginPath();
        points.forEach((p, idx) => {
            if (idx === 0) ctx.moveTo(p.x, p.y);
            else ctx.lineTo(p.x, p.y);
        });
        ctx.lineWidth = thickness;
        let surfaceStyle = trackStyle.surfaceColor;
        if (customTrack && customTrack.complete && customTrack.naturalWidth > 0) {
            try {
                const pattern = ctx.createPattern(customTrack, 'repeat');
                if (pattern) surfaceStyle = pattern;
            } catch (_) { }
        }
        ctx.strokeStyle = surfaceStyle;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.stroke();

        // 1b. Themed Inner Road Glow (e.g. frost sheen, neon glow, evergreen tint)
        if (trackStyle.innerGlow && (!customTrack || !customTrack.complete || customTrack.naturalWidth === 0)) {
            ctx.beginPath();
            points.forEach((p, idx) => {
                if (idx === 0) ctx.moveTo(p.x, p.y);
                else ctx.lineTo(p.x, p.y);
            });
            ctx.lineWidth = Math.max(8, thickness - 10);
            ctx.strokeStyle = trackStyle.innerGlow;
            ctx.stroke();
        }

        // 2. Curbs / Rumble Strips (Outer & Inner edges)
        if (currentSettings.showTrackBorders) {
            const curbThick = 4;
            const pColor = trackStyle.curb1;
            const sColor = trackStyle.curb2;

            for (let i = 0; i < samples; i++) {
                const p1 = points[i];
                const p2 = points[i + 1] || points[0];
                const isEven = (i % 2 === 0);
                const col = isEven ? pColor : sColor;

                // Outer border curb
                ctx.beginPath();
                ctx.moveTo(p1.x + p1.normalX * (thickness / 2), p1.y + p1.normalY * (thickness / 2));
                ctx.lineTo(p2.x + p2.normalX * (thickness / 2), p2.y + p2.normalY * (thickness / 2));
                ctx.lineWidth = curbThick;
                ctx.strokeStyle = col;
                ctx.stroke();

                // Inner border curb
                ctx.beginPath();
                ctx.moveTo(p1.x - p1.normalX * (thickness / 2), p1.y - p1.normalY * (thickness / 2));
                ctx.lineTo(p2.x - p2.normalX * (thickness / 2), p2.y - p2.normalY * (thickness / 2));
                ctx.lineWidth = curbThick;
                ctx.strokeStyle = col;
                ctx.stroke();
            }
        }

        // 3. Center Dashed Line
        ctx.beginPath();
        points.forEach((p, idx) => {
            if (idx === 0) ctx.moveTo(p.x, p.y);
            else ctx.lineTo(p.x, p.y);
        });
        ctx.lineWidth = 2;
        ctx.setLineDash(trackStyle.dashPattern || [8, 12]);
        ctx.strokeStyle = trackStyle.dashColor || 'rgba(255, 255, 255, 0.4)';
        ctx.stroke();
        ctx.setLineDash([]);

        // 4. Start / Finish Line Banner
        const finishPt = getTrackPoint(0.0, currentSettings.trackType, currentSettings.stripPosition);
        ctx.save();
        ctx.translate(finishPt.x, finishPt.y);
        ctx.rotate(finishPt.angle);

        // Checkered line strip
        const checkW = 14;
        const checkH = thickness;
        const sq = 4;
        for (let r = -checkH / 2; r < checkH / 2; r += sq) {
            for (let c = -checkW / 2; c < checkW / 2; c += sq) {
                const isFirstCol = (Math.floor(r / sq) + Math.floor(c / sq)) % 2 === 0;
                ctx.fillStyle = isFirstCol ? trackStyle.finishColor1 : trackStyle.finishColor2;
                ctx.fillRect(c, r, sq, sq);
            }
        }
        ctx.restore();

        ctx.restore();
    }

    function drawHazards() {
        ctx.save();
        ctx.globalAlpha = overlayAlpha;
        trackHazards.forEach(h => {
            if (!h.active) return;
            const pt = getTrackPoint(h.t, currentSettings.trackType, currentSettings.stripPosition);
            const hx = pt.x + pt.normalX * h.laneOffset;
            const hy = pt.y + pt.normalY * h.laneOffset;

            ctx.save();
            ctx.translate(hx, hy);

            const themeSeason = (currentTheme && currentTheme.seasonPreset) ? currentTheme.seasonPreset.toLowerCase() : 'spring';
            const hazardImgUrl = currentTheme && (currentTheme.racingHazardImageUrl || currentTheme.RacingHazardImageUrl);
            const customHazard = getImage(hazardImgUrl);

            if (h.type === 'oil') {
                if (customHazard && customHazard.complete && customHazard.naturalWidth > 0) {
                    ctx.drawImage(customHazard, -14, -14, 28, 28);
                } else if (themeSeason === 'winter') {
                    // Ice Slick: Transparent crystalline frost patch with crack detail
                    ctx.fillStyle = 'rgba(180, 235, 255, 0.78)';
                    ctx.shadowColor = '#B3E5FC';
                    ctx.shadowBlur = 6;
                    ctx.beginPath();
                    ctx.ellipse(0, 0, 16, 10, 0.25, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.shadowBlur = 0;
                    ctx.lineWidth = 1.5;
                    ctx.strokeStyle = '#FFFFFF';
                    ctx.stroke();
                    ctx.strokeStyle = 'rgba(255, 255, 255, 0.85)';
                    ctx.beginPath();
                    ctx.moveTo(-6, -2); ctx.lineTo(0, 1); ctx.lineTo(6, -3);
                    ctx.stroke();
                } else if (themeSeason === 'halloween') {
                    // Toxic Slime Puddle: Bubbling neon slime
                    ctx.fillStyle = 'rgba(57, 255, 20, 0.82)';
                    ctx.shadowColor = '#39FF14';
                    ctx.shadowBlur = 8;
                    ctx.beginPath();
                    ctx.ellipse(0, 0, 16, 10, -0.2, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.shadowBlur = 0;
                    ctx.fillStyle = '#FFFFFF';
                    ctx.beginPath();
                    ctx.arc(-4, -2, 2.5, 0, Math.PI * 2);
                    ctx.arc(5, 3, 2, 0, Math.PI * 2);
                    ctx.fill();
                } else if (themeSeason === 'fall') {
                    // Autumn Leaf Pile
                    const leafCols = ['#FF6F00', '#D84315', '#FBC02D', '#BF360C'];
                    for (let li = 0; li < 6; li++) {
                        const ang = (li / 6) * Math.PI * 2 + 0.3;
                        const dist = (li % 2 === 0 ? 8 : 4);
                        ctx.fillStyle = leafCols[li % leafCols.length];
                        ctx.beginPath();
                        ctx.ellipse(Math.cos(ang) * dist, Math.sin(ang) * dist, 6, 3, ang, 0, Math.PI * 2);
                        ctx.fill();
                    }
                } else if (themeSeason === 'christmas') {
                    // Dropped holiday coal / fruitcake
                    ctx.fillStyle = '#212121';
                    ctx.beginPath();
                    ctx.roundRect(-10, -8, 20, 16, 4);
                    ctx.fill();
                    ctx.fillStyle = '#FF2A4B';
                    ctx.fillRect(-2, -8, 4, 16);
                } else {
                    // Standard Oil Slick
                    ctx.fillStyle = 'rgba(20, 20, 25, 0.88)';
                    ctx.beginPath();
                    ctx.ellipse(0, 0, 15, 9, 0.3, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.lineWidth = 1.5;
                    ctx.strokeStyle = '#3d3d44';
                    ctx.stroke();
                }
            } else if (h.type === 'cone') {
                if (customHazard && customHazard.complete && customHazard.naturalWidth > 0) {
                    ctx.drawImage(customHazard, -12, -12, 24, 24);
                } else if (themeSeason === 'winter') {
                    // Mini Snowman Hazard
                    ctx.fillStyle = '#FFFFFF';
                    ctx.beginPath();
                    ctx.arc(0, 4, 7, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.beginPath();
                    ctx.arc(0, -4, 5, 0, Math.PI * 2);
                    ctx.fill();
                    // Carrot nose
                    ctx.fillStyle = '#FF9100';
                    ctx.beginPath();
                    ctx.moveTo(0, -4); ctx.lineTo(5, -3); ctx.lineTo(0, -2);
                    ctx.fill();
                    // Coal eyes
                    ctx.fillStyle = '#000000';
                    ctx.fillRect(-2, -6, 1.5, 1.5);
                    ctx.fillRect(1, -6, 1.5, 1.5);
                } else if (themeSeason === 'halloween') {
                    // Mini Jack-o'-lantern Hazard
                    ctx.fillStyle = '#FF7518';
                    ctx.beginPath();
                    ctx.arc(0, 1, 8, 0, Math.PI * 2);
                    ctx.fill();
                    // Green stem
                    ctx.fillStyle = '#39FF14';
                    ctx.fillRect(-1.5, -8, 3, 3);
                    // Carved eyes
                    ctx.fillStyle = '#000000';
                    ctx.beginPath();
                    ctx.moveTo(-4, -1); ctx.lineTo(-2, -4); ctx.lineTo(0, -1);
                    ctx.moveTo(4, -1); ctx.lineTo(2, -4); ctx.lineTo(0, -1);
                    ctx.moveTo(-4, 3); ctx.lineTo(0, 5); ctx.lineTo(4, 3);
                    ctx.fill();
                } else if (themeSeason === 'christmas') {
                    // Wrapped Present Box Hazard
                    ctx.fillStyle = '#FF2A4B';
                    ctx.fillRect(-7, -7, 14, 14);
                    ctx.fillStyle = '#FFD166';
                    ctx.fillRect(-2, -7, 4, 14);
                    ctx.fillRect(-7, -2, 14, 4);
                } else {
                    // Road Cone
                    ctx.fillStyle = '#FF6B00';
                    ctx.beginPath();
                    ctx.moveTo(0, -11);
                    ctx.lineTo(8, 7);
                    ctx.lineTo(-8, 7);
                    ctx.closePath();
                    ctx.fill();
                    ctx.fillStyle = '#FFFFFF';
                    ctx.fillRect(-5, 0, 10, 3);
                }
            } else if (h.type === 'boost') {
                // Turbo Boost Arrow
                ctx.rotate(pt.angle);
                const boostImgUrl = currentTheme && (currentTheme.racingBoostImageUrl || currentTheme.RacingBoostImageUrl);
                const customBoost = getImage(boostImgUrl);
                if (customBoost && customBoost.complete && customBoost.naturalWidth > 0) {
                    ctx.drawImage(customBoost, -14, -14, 28, 28);
                } else {
                    let boostCol = '#00E676';
                    if (themeSeason === 'winter') boostCol = '#00E5FF';
                    else if (themeSeason === 'halloween') boostCol = '#BF55EC';
                    else if (themeSeason === 'christmas') boostCol = '#FF2A4B';
                    else if (themeSeason === 'spring') boostCol = '#FF4081';

                    ctx.fillStyle = boostCol;
                    ctx.shadowColor = boostCol;
                    ctx.shadowBlur = 10;
                    ctx.beginPath();
                    ctx.moveTo(9, 0);
                    ctx.lineTo(-4, -9);
                    ctx.lineTo(-4, -3.5);
                    ctx.lineTo(-11, -3.5);
                    ctx.lineTo(-11, 3.5);
                    ctx.lineTo(-4, 3.5);
                    ctx.lineTo(-4, 9);
                    ctx.closePath();
                    ctx.fill();
                    ctx.shadowBlur = 0;

                    // Themed inner insignia
                    ctx.fillStyle = '#FFFFFF';
                    if (themeSeason === 'winter') {
                        ctx.font = 'bold 8px sans-serif';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'middle';
                        ctx.fillText('❄', -4, 0);
                    } else if (themeSeason === 'christmas') {
                        ctx.fillStyle = '#00E676';
                        ctx.fillRect(-9, -2, 4, 4);
                    } else {
                        ctx.fillRect(-8, -1.5, 3, 3);
                    }
                }
            } else if (h.type === 'wrench') {
                // Repair Kit Wrench
                ctx.fillStyle = '#00E676';
                ctx.shadowColor = '#00E676';
                ctx.shadowBlur = 8;
                ctx.beginPath();
                ctx.arc(0, 0, 11, 0, Math.PI * 2);
                ctx.fill();
                ctx.shadowBlur = 0;
                ctx.fillStyle = '#FFFFFF';
                ctx.font = 'bold 12px sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText('🔧', 0, 1);
            } else if (h.type === 'shield') {
                // Spiked Shield Power-up
                ctx.fillStyle = '#00E5FF';
                ctx.shadowColor = '#00E5FF';
                ctx.shadowBlur = 10;
                ctx.beginPath();
                ctx.arc(0, 0, 11, 0, Math.PI * 2);
                ctx.fill();
                ctx.shadowBlur = 0;
                ctx.fillStyle = '#FFFFFF';
                ctx.font = 'bold 12px sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText('🛡️', 0, 1);
            } else if (h.type === 'bomb') {
                // Explosive TNT / Bomb Barrel (Derby Mode)
                ctx.fillStyle = '#D50000';
                ctx.shadowColor = '#FF3D00';
                ctx.shadowBlur = 10;
                ctx.beginPath();
                ctx.arc(0, 0, 11, 0, Math.PI * 2);
                ctx.fill();
                ctx.shadowBlur = 0;
                ctx.fillStyle = '#FFFFFF';
                ctx.font = 'bold 12px sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText('💣', 0, 1);
            } else if (h.type === 'spike') {
                // Caltrop / Spike Trap (Derby Mode)
                ctx.fillStyle = '#37474F';
                ctx.shadowColor = '#FF5252';
                ctx.shadowBlur = 8;
                ctx.beginPath();
                ctx.arc(0, 0, 10, 0, Math.PI * 2);
                ctx.fill();
                ctx.shadowBlur = 0;
                ctx.fillStyle = '#FF5252';
                ctx.font = 'bold 12px sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText('⚠️', 0, 1);
            }

            ctx.restore();
        });
        ctx.restore();
    }

    // Draws an individual vehicle (Kart with driver emote / penguin)
    function drawVehicle(racer) {
        const pt = getTrackPoint(racer.progress, currentSettings.trackType, currentSettings.stripPosition);
        const vx = pt.x + pt.normalX * racer.laneOffset;
        const vy = pt.y + pt.normalY * racer.laneOffset;
        racer.currentX = vx;
        racer.currentY = vy;

        if (racer.isEliminated || racer.isTotaled) {
            ctx.save();
            ctx.globalAlpha = 0.35 * overlayAlpha;
        }

        ctx.save();
        ctx.translate(vx, vy);
        const headingAngle = pt.angle + (racer.dir === -1 ? Math.PI : 0);
        ctx.rotate(headingAngle + racer.spinAngle + racer.driftAngle + (racer.steerAngle || 0));

        const chassisLen = 34;
        const chassisWid = 20;

        // Spiked Shield Aura
        if (racer.shieldTimer > 0) {
            ctx.save();
            ctx.strokeStyle = '#00E5FF';
            ctx.shadowColor = '#00E5FF';
            ctx.shadowBlur = 12;
            ctx.lineWidth = 2.5;
            ctx.beginPath();
            ctx.arc(0, 0, 24, 0, Math.PI * 2);
            ctx.stroke();
            ctx.restore();
        }

        // Exhaust Particles
        const themeSeason = (currentTheme && currentTheme.seasonPreset) ? currentTheme.seasonPreset.toLowerCase() : 'spring';
        if (racer.isBoosting) {
            let flameCol = '#00E5FF';
            if (themeSeason === 'halloween') flameCol = '#BF55EC';
            else if (themeSeason === 'christmas') flameCol = '#FF2A4B';
            else if (themeSeason === 'fall') flameCol = '#FF6D00';
            else if (themeSeason === 'spring') flameCol = '#FF4081';

            ctx.fillStyle = flameCol;
            ctx.shadowColor = flameCol;
            ctx.shadowBlur = 12;
            ctx.beginPath();
            ctx.moveTo(-chassisLen / 2 - 2, -4);
            ctx.lineTo(-chassisLen / 2 - 14, 0);
            ctx.lineTo(-chassisLen / 2 - 2, 4);
            ctx.fill();
        } else if (racer.isDrafting) {
            ctx.fillStyle = '#FF9100';
            ctx.beginPath();
            ctx.arc(-chassisLen / 2 - 4, 0, 4, 0, Math.PI * 2);
            ctx.fill();
        }

        // Custom Vehicle Sprite or Procedural Kart Chassis
        const vehicleImgUrl = currentTheme && (currentTheme.racingVehicleImageUrl || currentTheme.RacingVehicleImageUrl);
        const customVehicle = getImage(vehicleImgUrl);
        let drewCustomVehicle = false;

        if (customVehicle && customVehicle.complete && customVehicle.naturalWidth > 0) {
            ctx.save();
            if (racer.isTotaled) {
                ctx.filter = 'grayscale(100%) brightness(35%)';
            } else if ((racer.hitCooldown || 0) > 0.65) {
                ctx.filter = 'brightness(200%)';
            }
            const vW = 38;
            const vH = 22;
            ctx.drawImage(customVehicle, -vW / 2, -vH / 2, vW, vH);
            ctx.restore();
            drewCustomVehicle = true;
        }

        if (!drewCustomVehicle) {
            // 1. Kart Chassis (charred if totaled, white flash on impact)
            let primaryColor = racer.isTotaled ? '#26282e' : (racer.userColor || '#FFD166');
            if (!racer.isTotaled && (racer.hitCooldown || 0) > 0.65) {
                primaryColor = '#FFFFFF';
            }
            ctx.fillStyle = primaryColor;
            ctx.shadowColor = 'rgba(0,0,0,0.4)';
            ctx.shadowBlur = 6;

            ctx.beginPath();
            ctx.roundRect(-chassisLen / 2, -chassisWid / 2, chassisLen, chassisWid, [6, 10, 10, 6]);
            ctx.fill();
            ctx.shadowBlur = 0;

            // Front bumper (if not detached)
            if (!racer.lostFrontBumper && !racer.isTotaled) {
                ctx.fillStyle = '#1e232a';
                ctx.fillRect(chassisLen / 2 - 4, -chassisWid / 2 - 2, 5, chassisWid + 4);
            }

            // Rear spoiler (if not detached)
            if (!racer.lostSpoiler && !racer.isTotaled) {
                ctx.fillStyle = '#1e232a';
                ctx.fillRect(-chassisLen / 2 - 2, -chassisWid / 2 - 3, 4, chassisWid + 6);
            }

            // Seasonal Kart Roof / Spoiler Toppers
            if (!racer.isTotaled && !racer.lostSpoiler) {
                if (themeSeason === 'christmas') {
                    // Mini Santa Hat on spoiler!
                    ctx.fillStyle = '#FF2A4B';
                    ctx.beginPath();
                    ctx.moveTo(-chassisLen / 2 + 1, -chassisWid / 2 + 2);
                    ctx.lineTo(-chassisLen / 2 - 6, -chassisWid / 2 - 4);
                    ctx.lineTo(-chassisLen / 2 + 3, -chassisWid / 2 - 6);
                    ctx.fill();
                    ctx.fillStyle = '#FFFFFF';
                    ctx.beginPath();
                    ctx.arc(-chassisLen / 2 - 6, -chassisWid / 2 - 4, 2, 0, Math.PI * 2);
                    ctx.fill();
                } else if (themeSeason === 'halloween') {
                    // Mini Bat Wings on rear spoiler
                    ctx.fillStyle = '#BF55EC';
                    ctx.beginPath();
                    ctx.moveTo(-chassisLen / 2, -chassisWid / 2 - 1);
                    ctx.lineTo(-chassisLen / 2 - 5, -chassisWid / 2 - 6);
                    ctx.lineTo(-chassisLen / 2 + 1, -chassisWid / 2 - 4);
                    ctx.moveTo(-chassisLen / 2, chassisWid / 2 + 1);
                    ctx.lineTo(-chassisLen / 2 - 5, chassisWid / 2 + 6);
                    ctx.lineTo(-chassisLen / 2 + 1, chassisWid / 2 + 4);
                    ctx.fill();
                } else if (themeSeason === 'winter') {
                    // Snow dusting on rear spoiler
                    ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';
                    ctx.fillRect(-chassisLen / 2 - 2, -chassisWid / 2 - 3, 4, 3);
                    ctx.fillRect(-chassisLen / 2 - 2, chassisWid / 2, 4, 3);
                } else if (themeSeason === 'spring') {
                    // Tiny Sakura Flower on spoiler
                    ctx.fillStyle = '#FF8FA3';
                    ctx.beginPath();
                    ctx.arc(-chassisLen / 2 - 1, -chassisWid / 2 - 2, 2.5, 0, Math.PI * 2);
                    ctx.fill();
                } else if (themeSeason === 'fall') {
                    // Tiny Autumn Leaf on spoiler
                    ctx.fillStyle = '#FF6F00';
                    ctx.beginPath();
                    ctx.ellipse(-chassisLen / 2 - 1, -chassisWid / 2 - 2, 3, 1.8, 0.4, 0, Math.PI * 2);
                    ctx.fill();
                }
            }

            // 4 Wheels (omit wheel 3 if detached)
            ctx.fillStyle = '#111315';
            const wheelLen = 8;
            const wheelWid = 4;
            const wheelCoords = [
                [-chassisLen / 2 + 5, -chassisWid / 2 - 2],
                [-chassisLen / 2 + 5, chassisWid / 2 - 2],
                [chassisLen / 2 - 10, -chassisWid / 2 - 2],
                [chassisLen / 2 - 10, chassisWid / 2 - 2]
            ];
            wheelCoords.forEach(([wx, wy], widx) => {
                if (racer.lostWheel && widx === 3) return;
                ctx.fillRect(wx, wy, wheelLen, wheelWid);
            });
        }

        // 2. Driver Emote / Mascot in Cockpit
        const driverR = 10;
        ctx.beginPath();
        ctx.arc(2, 0, driverR, 0, Math.PI * 2);
        ctx.fillStyle = '#FFFFFF';
        ctx.fill();

        let drewCustom = false;
        if (racer.emoteUrl) {
            const emoteImg = getImage(racer.emoteUrl);
            if (emoteImg && emoteImg.complete && emoteImg.naturalWidth > 0) {
                ctx.save();
                ctx.beginPath();
                ctx.arc(2, 0, driverR - 1, 0, Math.PI * 2);
                ctx.clip();
                ctx.drawImage(emoteImg, 2 - (driverR - 1), -(driverR - 1), (driverR - 1) * 2, (driverR - 1) * 2);
                ctx.restore();
                drewCustom = true;
            }
        }

        if (!drewCustom && commonModule && commonModule.drawSeasonalDefaultPenguin) {
            ctx.save();
            ctx.translate(2, 0);
            ctx.scale(0.35, 0.35);
            commonModule.drawSeasonalDefaultPenguin(ctx, 22, (currentTheme && currentTheme.seasonPreset) || 'spring');
            ctx.restore();
        }

        // Helmet Visor Overlay
        ctx.lineWidth = 2;
        ctx.strokeStyle = '#22272e';
        ctx.beginPath();
        ctx.arc(2, 0, driverR, -Math.PI / 3, Math.PI / 3);
        ctx.stroke();

        ctx.restore();

        // 3. Driver Nametag, Health Bar & Position Badge
        ctx.save();
        ctx.translate(vx, vy - 26);
        ctx.font = 'bold 12px "Fredoka", "Rubik", sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';

        const baseName = racer.displayName || racer.username || 'Driver';
        let name = baseName;
        if (racer.isTotaled || racer.isEliminated) {
            name = `💀 ${baseName}`;
        } else if (racer.kills > 0) {
            name = `💥${racer.kills} ${baseName}`;
        }
        const textWidth = ctx.measureText(name).width;

        ctx.fillStyle = 'rgba(18, 22, 28, 0.78)';
        ctx.beginPath();
        ctx.roundRect(-textWidth / 2 - 6, -9, textWidth + 12, 18, 5);
        ctx.fill();

        ctx.fillStyle = (racer.isTotaled || racer.isEliminated) ? '#FF8A80' : (racer.userColor || '#FFFFFF');
        ctx.fillText(name, 0, 0);

        // Mini Health Bar (Combat mode or Derby)
        const isCombat = currentSettings.enableVehicleDamage || currentSettings.gameMode === 'DestructionDerby';
        if (isCombat && !racer.isEliminated && !racer.isTotaled) {
            const barW = Math.max(34, textWidth + 6);
            const barH = 4;
            const curHp = Math.max(0, racer.hp ?? 100);
            const maxHp = racer.maxHp || 100;
            const frac = Math.max(0, Math.min(1, curHp / maxHp));

            ctx.fillStyle = 'rgba(15, 18, 24, 0.85)';
            ctx.beginPath();
            ctx.roundRect(-barW / 2, 11, barW, barH, 2);
            ctx.fill();

            const hpColor = frac > 0.5 ? '#00E676' : (frac > 0.25 ? '#FFD166' : '#FF5252');
            ctx.fillStyle = hpColor;
            ctx.beginPath();
            ctx.roundRect(-barW / 2, 11, barW * frac, barH, 2);
            ctx.fill();
        }

        ctx.restore();

        if (racer.isEliminated || racer.isTotaled) {
            ctx.restore();
        }
    }

    // Draws Ghost Car for Time Trial
    function drawGhostCar() {
        if (!timeTrialGhost) return;
        const pt = getTrackPoint(timeTrialGhost.progress, currentSettings.trackType, currentSettings.stripPosition);
        ctx.save();
        ctx.globalAlpha = 0.45 * overlayAlpha;
        ctx.translate(pt.x, pt.y);
        ctx.rotate(pt.angle);
        ctx.fillStyle = timeTrialGhost.isPacer ? '#FFD166' : '#00E5FF';
        ctx.beginPath();
        ctx.roundRect(-16, -9, 32, 18, 5);
        ctx.fill();
        ctx.fillStyle = '#FFFFFF';
        ctx.font = 'bold 10px sans-serif';
        ctx.textAlign = 'center';
        const label = timeTrialGhost.isPacer ? 'PACER' : `GHOST: ${(timeTrialGhost.racerName || 'RECORD').substring(0, 8)}`;
        ctx.fillText(label, 0, -14);
        ctx.restore();
    }

    // Draws Lobby UI on Starting Grid
    function drawLobbyUI() {
        if (gameState !== 'lobby') return;
        ctx.save();
        ctx.globalAlpha = overlayAlpha;

        const bannerW = Math.min(460, width * 0.75);
        const bannerH = 74;
        const bx = (width - bannerW) / 2;
        const by = 24;

        ctx.fillStyle = 'rgba(18, 22, 28, 0.88)';
        ctx.shadowColor = 'rgba(0,0,0,0.5)';
        ctx.shadowBlur = 12;
        ctx.beginPath();
        ctx.roundRect(bx, by, bannerW, bannerH, 10);
        ctx.fill();
        ctx.shadowBlur = 0;

        const trackStyle = getThemeTrackStyle();
        ctx.strokeStyle = trackStyle.curb1 || '#FF5252';
        ctx.lineWidth = 2.5;
        ctx.stroke();

        ctx.fillStyle = '#FFFFFF';
        ctx.font = 'bold 20px "Fredoka", "Rubik", sans-serif';
        ctx.textAlign = 'center';
        const titleTpl = (currentMessages && (currentMessages.overlayLobbyTitle || currentMessages.OverlayLobbyTitle)) || '🏁 RACE LOBBY: {mode}';
        const lobbyTitle = titleTpl.replace('{mode}', currentSettings.gameMode.toUpperCase());
        ctx.fillText(lobbyTitle, width / 2, by + 26);

        const secLeft = Math.max(0, Math.ceil(lobbyRemainingMs / 1000));
        ctx.font = '14px "Fredoka", "Rubik", sans-serif';
        ctx.fillStyle = '#FFD166';
        const subTpl = (currentMessages && (currentMessages.overlayLobbySubtitle || currentMessages.OverlayLobbySubtitle)) || 'Starting in {seconds}s  •  Type !race [emote] to join! ({count}/{max})';
        const lobbySubtitle = subTpl
            .replace('{seconds}', secLeft.toString())
            .replace('{count}', lobbyRacers.length.toString())
            .replace('{max}', (currentSettings.maxRacers || 12).toString());
        ctx.fillText(lobbySubtitle, width / 2, by + 52);

        ctx.restore();
    }

    // Draws 3-2-1-GO Lights Countdown
    function drawCountdownUI() {
        if (gameState !== 'countdown') return;
        ctx.save();
        ctx.globalAlpha = overlayAlpha;

        const cx = width / 2;
        const cy = height / 2;

        if (currentHeatInfo && currentHeatInfo.title) {
            ctx.fillStyle = 'rgba(18, 22, 28, 0.88)';
            ctx.beginPath();
            ctx.roundRect(cx - 160, cy - 130, 320, 38, 8);
            ctx.fill();
            ctx.fillStyle = '#FFD166';
            ctx.font = 'bold 16px "Fredoka", sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText(currentHeatInfo.title, cx, cy - 105);
        }

        ctx.fillStyle = 'rgba(18, 22, 28, 0.85)';
        ctx.beginPath();
        ctx.arc(cx, cy, 70, 0, Math.PI * 2);
        ctx.fill();
        ctx.lineWidth = 4;
        const countdownStyle = getThemeTrackStyle();
        ctx.strokeStyle = countdownStep === 0 ? '#00E676' : (countdownStyle.curb1 || '#FF5252');
        ctx.stroke();

        ctx.font = 'bold 44px "Fredoka", "Rubik", sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = countdownStep === 0 ? '#00E676' : '#FFFFFF';
        ctx.fillText(countdownStep === 0 ? 'GO!' : countdownStep.toString(), cx, cy);

        ctx.restore();
    }

    // Draws Live Stopwatch & Leaderboard
    function drawRaceHUD() {
        if (gameState !== 'racing') return;
        ctx.save();
        ctx.globalAlpha = overlayAlpha;

        // Heat title banner top center
        if (currentHeatInfo && currentHeatInfo.title) {
            ctx.fillStyle = 'rgba(18, 22, 28, 0.78)';
            ctx.beginPath();
            ctx.roundRect((width - 340) / 2, 16, 340, 32, 6);
            ctx.fill();
            ctx.fillStyle = '#FFD166';
            ctx.font = 'bold 15px "Fredoka", sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText(currentHeatInfo.title, width / 2, 38);
        }

        // Elimination alert banner
        if (eliminationBanner && eliminationBanner.timer > 0) {
            const ebW = Math.min(420, width * 0.85);
            ctx.fillStyle = 'rgba(18, 22, 28, 0.92)';
            ctx.shadowColor = eliminationBanner.color || '#FF5252';
            ctx.shadowBlur = 14;
            ctx.beginPath();
            ctx.roundRect((width - ebW) / 2, 54, ebW, 36, 6);
            ctx.fill();
            ctx.shadowBlur = 0;
            ctx.strokeStyle = eliminationBanner.color || '#FF5252';
            ctx.lineWidth = 2;
            ctx.stroke();

            ctx.fillStyle = eliminationBanner.color || '#FF5252';
            ctx.font = 'bold 16px "Fredoka", "Rubik", sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText(eliminationBanner.text, width / 2, 77);
        }

        if (currentSettings.gameMode === 'TimeTrial') {
            const timeSec = (raceElapsedTime / 1000).toFixed(2);
            const savedRecord = loadBestLap(currentSettings.trackType);
            const bestText = savedRecord ? `${(savedRecord.timeMs / 1000).toFixed(2)}s` : '--';
            const prevText = lastRecordedLapMs ? `${(lastRecordedLapMs / 1000).toFixed(2)}s` : '--';

            ctx.fillStyle = 'rgba(18, 22, 28, 0.82)';
            ctx.beginPath();
            ctx.roundRect(width - 190, 16, 175, 78, 8);
            ctx.fill();
            ctx.strokeStyle = '#00E5FF';
            ctx.lineWidth = 1.5;
            ctx.stroke();

            ctx.fillStyle = '#00E5FF';
            ctx.font = 'bold 17px monospace';
            ctx.textAlign = 'left';
            ctx.fillText(`⏱️ ${timeSec}s`, width - 178, 38);

            ctx.font = '12px "Fredoka", sans-serif';
            ctx.fillStyle = '#FFD166';
            ctx.fillText(`🏆 Best: ${bestText}`, width - 178, 58);

            ctx.fillStyle = '#B0BEC5';
            ctx.fillText(`⏮️ Prev: ${prevText}`, width - 178, 76);

            ctx.restore();
            return;
        }

        // Leader progress for display
        let maxProg = 0;
        activeRacers.forEach(r => { if (r.progress > maxProg) maxProg = r.progress; });
        const totalLaps = currentSettings.totalLaps || 2;
        const curLapNum = Math.min(totalLaps, Math.floor(maxProg) + 1);

        // Stopwatch & Lap counter in top right
        const timeSec = (raceElapsedTime / 1000).toFixed(2);
        ctx.fillStyle = 'rgba(18, 22, 28, 0.78)';
        ctx.beginPath();
        ctx.roundRect(width - 150, 20, 130, 48, 6);
        ctx.fill();
        ctx.fillStyle = '#00E5FF';
        ctx.font = 'bold 16px monospace';
        ctx.textAlign = 'center';
        ctx.fillText(`⏱️ ${timeSec}s`, width - 85, 42);

        ctx.fillStyle = '#FFD166';
        ctx.font = 'bold 12px "Fredoka", sans-serif';
        let lapLabel = currentSettings.gameMode === 'Elimination' ? `⚡ LAP ${curLapNum}/${totalLaps}` : `LAP ${curLapNum}/${totalLaps}`;
        if (currentSettings.gameMode === 'DestructionDerby') {
            const aliveCount = activeRacers.filter(r => !r.isTotaled).length;
            lapLabel = `💥 ${aliveCount}/${activeRacers.length} ALIVE`;
        }
        ctx.fillText(lapLabel, width - 85, 60);

        // Leaderboard sidebar
        const isElim = currentSettings.gameMode === 'Elimination';
        const isCombat = currentSettings.enableVehicleDamage || currentSettings.gameMode === 'DestructionDerby';
        const sorted = [...activeRacers].sort((a, b) => {
            if (currentSettings.gameMode === 'DestructionDerby') {
                if (a.isTotaled !== b.isTotaled) return a.isTotaled ? 1 : -1;
                if ((b.kills || 0) !== (a.kills || 0)) return (b.kills || 0) - (a.kills || 0);
            }
            return b.progress - a.progress;
        });
        const topN = sorted.slice(0, 6);
        let sy = 80;

        // Find currently last-place unfinished racer for danger tag in Elimination
        const unfinished = sorted.filter(r => !r.finished);
        const dangerRacer = (isElim && unfinished.length > 1) ? unfinished[unfinished.length - 1] : null;

        topN.forEach((r, idx) => {
            ctx.fillStyle = 'rgba(18, 22, 28, 0.65)';
            ctx.beginPath();
            ctx.roundRect(width - 150, sy, 130, 24, 4);
            ctx.fill();

            const isDanger = dangerRacer && dangerRacer.dropToken === r.dropToken;
            if (r.isTotaled) {
                ctx.fillStyle = '#FF5252';
                ctx.font = 'bold 10px sans-serif';
                ctx.textAlign = 'left';
                ctx.fillText(`💥 ${(r.displayName || r.username).substring(0, 8)} (DEAD)`, width - 142, sy + 16);
            } else if (r.isEliminated) {
                ctx.fillStyle = '#FF5252';
                ctx.font = 'bold 10px sans-serif';
                ctx.textAlign = 'left';
                ctx.fillText(`💀 ${(r.displayName || r.username).substring(0, 9)} (OUT)`, width - 142, sy + 16);
            } else if (isDanger) {
                ctx.fillStyle = '#FF5252';
                ctx.font = 'bold 11px sans-serif';
                ctx.textAlign = 'left';
                ctx.fillText(`⚠️ ${(r.displayName || r.username).substring(0, 8)}`, width - 142, sy + 16);
            } else if (isCombat) {
                ctx.fillStyle = idx === 0 ? '#FFD166' : '#FFFFFF';
                ctx.font = 'bold 10px sans-serif';
                ctx.textAlign = 'left';
                const name = (r.displayName || r.username).substring(0, 6);
                const hp = Math.max(0, Math.ceil(r.hp ?? 100));
                const ko = r.kills ? ` ${r.kills}💀` : '';
                ctx.fillText(`${idx + 1}. ${name} ${hp}HP${ko}`, width - 142, sy + 16);
            } else {
                ctx.fillStyle = idx === 0 ? '#FFD166' : '#FFFFFF';
                ctx.font = 'bold 11px sans-serif';
                ctx.textAlign = 'left';
                const name = r.displayName || r.username;
                ctx.fillText(`${idx + 1}. ${name.substring(0, 10)}`, width - 142, sy + 16);
            }
            sy += 28;
        });

        ctx.restore();
    }

    // Draws Finish Podium
    function drawPodiumUI() {
        if (gameState !== 'podium' || finishedRacers.length === 0) return;
        finishedRacers.sort((a, b) => (a.placement || 999) - (b.placement || 999));
        ctx.save();
        ctx.globalAlpha = overlayAlpha;

        if (currentSettings.gameMode === 'TimeTrial') {
            const r = finishedRacers[0];
            const pw = Math.min(500, width * 0.88);
            const ph = 160;
            const px = (width - pw) / 2;
            const py = height / 2 - ph / 2;

            ctx.fillStyle = 'rgba(18, 22, 28, 0.94)';
            ctx.shadowColor = 'rgba(0,0,0,0.6)';
            ctx.shadowBlur = 18;
            ctx.beginPath();
            ctx.roundRect(px, py, pw, ph, 12);
            ctx.fill();
            ctx.shadowBlur = 0;

            ctx.strokeStyle = r.isNewRecord ? '#00E676' : '#00E5FF';
            ctx.lineWidth = 3;
            ctx.stroke();

            ctx.fillStyle = '#FFFFFF';
            ctx.font = 'bold 18px "Fredoka", "Rubik", sans-serif';
            ctx.textAlign = 'center';
            const ttTitle = (currentMessages && (currentMessages.overlayTimeTrialTitle || currentMessages.OverlayTimeTrialTitle)) || '⏱️ TIME TRIAL RESULT';
            ctx.fillText(ttTitle, width / 2, py + 32, pw - 30);

            ctx.fillStyle = r.userColor || '#FFD166';
            ctx.font = 'bold 16px "Fredoka", sans-serif';
            ctx.fillText(r.displayName || r.username, width / 2, py + 62, pw - 40);

            ctx.fillStyle = '#00E5FF';
            ctx.font = 'bold 24px monospace';
            ctx.fillText(`${(r.lapTime || 0).toFixed(2)}s`, width / 2, py + 94, pw - 40);

            if (r.isNewRecord) {
                ctx.fillStyle = '#00E676';
                ctx.font = 'bold 14px "Fredoka", sans-serif';
                const newRec = (currentMessages && (currentMessages.overlayNewRecord || currentMessages.OverlayNewRecord)) || '🏆 NEW TRACK RECORD! 🏆';
                ctx.fillText(newRec, width / 2, py + 124, pw - 30);
            } else if (r.prevBestMs) {
                const diff = (r.lapTime - (r.prevBestMs / 1000)).toFixed(2);
                ctx.fillStyle = '#FF8A80';
                ctx.font = '13px "Fredoka", sans-serif';
                ctx.fillText(`Track Record: ${(r.prevBestMs / 1000).toFixed(2)}s (+${diff}s)`, width / 2, py + 124, pw - 30);
            }

            ctx.restore();
            return;
        }

        // If this was a preliminary heat (not final)
        if (currentHeatInfo && !currentHeatInfo.isFinal) {
            const pw = Math.min(580, width * 0.9);
            const ph = 185;
            const px = (width - pw) / 2;
            const py = height / 2 - ph / 2;

            ctx.fillStyle = 'rgba(18, 22, 28, 0.94)';
            ctx.shadowColor = 'rgba(0,0,0,0.6)';
            ctx.shadowBlur = 18;
            ctx.beginPath();
            ctx.roundRect(px, py, pw, ph, 12);
            ctx.fill();
            ctx.shadowBlur = 0;

            ctx.strokeStyle = '#448AFF';
            ctx.lineWidth = 3;
            ctx.stroke();

            ctx.fillStyle = '#FFD166';
            ctx.font = 'bold 20px "Fredoka", "Rubik", sans-serif';
            ctx.textAlign = 'center';
            const heatTitle = `🏁 ${currentHeatInfo.title ? currentHeatInfo.title.toUpperCase() : 'HEAT'} RESULTS`;
            ctx.fillText(heatTitle, width / 2, py + 36, pw - 32);

            const displayCount = Math.min(4, finishedRacers.length);
            const colWidth = (pw - 24) / Math.max(1, displayCount);

            finishedRacers.slice(0, displayCount).forEach((r, idx) => {
                const rx = px + 12 + colWidth * idx + colWidth / 2;
                ctx.fillStyle = idx < 2 ? '#00E676' : '#FFD166';
                ctx.font = 'bold 13px "Fredoka", sans-serif';
                ctx.fillText(idx < 2 ? 'QUALIFIED 🟢' : `${idx + 1}th`, rx, py + 76, colWidth - 10);

                ctx.fillStyle = r.userColor || '#FFFFFF';
                ctx.font = '13px "Fredoka", sans-serif';
                ctx.fillText((r.displayName || r.username).substring(0, 12), rx, py + 104, colWidth - 10);

                ctx.fillStyle = '#00E5FF';
                ctx.font = '11px monospace';
                ctx.fillText(`${(r.lapTime || 0).toFixed(2)}s`, rx, py + 130, colWidth - 10);
            });

            ctx.restore();
            return;
        }

        // Championship Grand Final Podium
        const pw = Math.min(580, width * 0.9);
        const ph = 185;
        const px = (width - pw) / 2;
        const py = height / 2 - ph / 2;

        ctx.fillStyle = 'rgba(18, 22, 28, 0.94)';
        ctx.shadowColor = 'rgba(0,0,0,0.6)';
        ctx.shadowBlur = 18;
        ctx.beginPath();
        ctx.roundRect(px, py, pw, ph, 12);
        ctx.fill();
        ctx.shadowBlur = 0;

        ctx.strokeStyle = '#FFD166';
        ctx.lineWidth = 3;
        ctx.stroke();

        let podTitle = (currentMessages && (currentMessages.overlayPodiumTitle || currentMessages.OverlayPodiumTitle)) || '🏆 CHAMPIONSHIP FINAL PODIUM 🏆';
        if (currentSettings.gameMode === 'Elimination') {
            podTitle = '🏆 ELIMINATION CHAMPION 🏆';
        } else if (currentSettings.gameMode === 'DestructionDerby') {
            podTitle = (currentMessages && (currentMessages.overlayDerbyPodiumTitle || currentMessages.OverlayDerbyPodiumTitle)) || '🏆 DEMOLITION DERBY CHAMPION 🏆';
        }

        ctx.fillStyle = '#FFD166';
        ctx.font = 'bold 20px "Fredoka", "Rubik", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(podTitle, width / 2, py + 36, pw - 32);

        const displayCount = Math.min(3, finishedRacers.length);
        const colWidth = (pw - 24) / Math.max(1, displayCount);
        const medals = ['🥇 1st', '🥈 2nd', '🥉 3rd'];

        finishedRacers.slice(0, displayCount).forEach((r, idx) => {
            const rx = px + 12 + colWidth * idx + colWidth / 2;

            ctx.fillStyle = '#FFFFFF';
            ctx.font = 'bold 15px "Fredoka", sans-serif';
            ctx.fillText(medals[idx], rx, py + 78, colWidth - 10);

            ctx.fillStyle = r.userColor || '#FFD166';
            ctx.font = '14px "Fredoka", sans-serif';
            ctx.fillText(r.displayName || r.username, rx, py + 106, colWidth - 12);

            ctx.fillStyle = '#00E5FF';
            ctx.font = '12px monospace';
            let statText = `${(r.lapTime || 0).toFixed(2)}s`;
            if (currentSettings.gameMode === 'DestructionDerby') {
                statText = r.isTotaled
                    ? `TOTALED (${r.kills || 0} KO${(r.kills || 0) === 1 ? '' : 's'})`
                    : `SURVIVED (${r.kills || 0} KO${(r.kills || 0) === 1 ? '' : 's'})`;
            } else if (r.kills && r.kills > 0) {
                statText = `${(r.lapTime || 0).toFixed(2)}s • ${r.kills} KO${r.kills === 1 ? '' : 's'}`;
            }
            ctx.fillText(statText, rx, py + 132, colWidth - 10);
        });

        ctx.restore();
    }

    // ================= MAIN GAME LOOP =================
    let lastTime = performance.now();

    // Checks clearance around candidate lane offset (returns minimum 2D distance to any other car)
    function getLaneClearance(racerIndex, targetLane) {
        const r = activeRacers[racerIndex];
        if (!r) return 999;
        const rPt = getTrackPoint(r.progress, currentSettings.trackType, currentSettings.stripPosition);
        const candX = rPt.x + rPt.normalX * targetLane;
        const candY = rPt.y + rPt.normalY * targetLane;

        let minD = 999;
        for (let k = 0; k < activeRacers.length; k++) {
            if (k === racerIndex) continue;
            const other = activeRacers[k];
            if (other.finished || other.isTotaled) continue;

            const oPt = getTrackPoint(other.progress, currentSettings.trackType, currentSettings.stripPosition);
            const ox = oPt.x + oPt.normalX * other.laneOffset;
            const oy = oPt.y + oPt.normalY * other.laneOffset;

            const d = Math.hypot(candX - ox, candY - oy);
            if (d < minD) minD = d;
        }
        return minD;
    }

    // Checks if a candidate lane offset is occupied by another car near racer's position using true 2D distance
    function isLaneOccupied(racerIndex, targetLane) {
        return getLaneClearance(racerIndex, targetLane) < 26;
    }

    // Finds the safest clear lane closest to the preferred lane, avoiding side collisions
    function pickSafestClearLane(racerIndex, preferredLane, maxLane) {
        const r = activeRacers[racerIndex];
        if (!r) return 0;

        const testOffsets = [
            preferredLane,
            preferredLane * 0.6,
            0,
            -preferredLane * 0.6,
            -preferredLane,
            maxLane * 0.7,
            -maxLane * 0.7,
            maxLane * 0.35,
            -maxLane * 0.35
        ];

        let bestLane = r.laneOffset;
        let bestScore = -999999;

        for (const cand of testOffsets) {
            const clamped = Math.max(-maxLane * 0.8, Math.min(maxLane * 0.8, cand));
            const clearance = getLaneClearance(racerIndex, clamped);
            // Clearance penalty if dangerously close (< 26px)
            const dangerPenalty = clearance < 26 ? (26 - clearance) * 100 : 0;
            // Preference score: prioritize open clearance while respecting preferred path
            const score = clearance * 2 - Math.abs(clamped - preferredLane) - dangerPenalty;
            if (score > bestScore) {
                bestScore = score;
                bestLane = clamped;
            }
        }

        return bestLane;
    }

    function updatePhysics(dt) {
        if (gameState === 'lobby') {
            lobbyRemainingMs -= dt;
            if (lobbyRemainingMs < 0) lobbyRemainingMs = 0;
            return;
        }

        if (gameState === 'countdown') {
            countdownTimer -= dt;
            if (countdownTimer <= 0) {
                if (countdownStep > 1) {
                    countdownStep--;
                    countdownTimer = 1000;
                    playSound('countdown_beep');
                } else if (countdownStep === 1) {
                    countdownStep = 0;
                    countdownTimer = 600;
                    playSound('countdown_go');
                    playSound('engine');
                } else {
                    gameState = 'racing';
                    raceStartTime = performance.now();
                }
            }
            return;
        }

        if (gameState === 'podium') {
            podiumTimer -= dt;
            if (podiumTimer <= 0) {
                gameState = 'idle';
                targetOverlayAlpha = 0.0;
            }
            return;
        }

        if (gameState !== 'racing') return;

        raceElapsedTime = performance.now() - raceStartTime;
        const dtSec = dt / 1000;
        const totalLaps = currentSettings.totalLaps || 2;

        if (eliminationBanner && eliminationBanner.timer > 0) {
            eliminationBanner.timer -= dtSec;
            if (eliminationBanner.timer <= 0) {
                eliminationBanner = null;
            }
        }

        // Update Ghost Car in Time Trial
        if (timeTrialGhost) {
            if (timeTrialGhost.targetLapMs) {
                timeTrialGhost.progress += (1.0 / (timeTrialGhost.targetLapMs / 1000)) * dtSec;
            } else if (timeTrialGhost.speed) {
                timeTrialGhost.progress += timeTrialGhost.speed * dtSec;
            }
        }

        // Leader progress for rubber banding & lap tracking
        let maxProgress = 0;
        activeRacers.forEach(r => { if (r.progress > maxProgress) maxProgress = r.progress; });

        // Elimination mode per-lap knockout check
        if (currentSettings.gameMode === 'Elimination') {
            const unfinished = activeRacers.filter(r => !r.finished);
            if (unfinished.length > 1) {
                const currentLap = Math.floor(maxProgress);
                if (currentLap >= 1 && !eliminatedLaps.has(currentLap)) {
                    eliminatedLaps.add(currentLap);

                    // Find unfinished racer with lowest progress
                    let lowestRacer = null;
                    let lowestProg = Infinity;
                    for (const r of unfinished) {
                        if (r.progress < lowestProg) {
                            lowestProg = r.progress;
                            lowestRacer = r;
                        }
                    }

                    if (lowestRacer) {
                        lowestRacer.finished = true;
                        lowestRacer.isEliminated = true;
                        lowestRacer.lapTime = raceElapsedTime / 1000;
                        lowestRacer.placement = nextBackPlacement--;
                        finishedRacers.push(lowestRacer);
                        reportRacerCompletion(lowestRacer, lowestRacer.placement);
                        playSound('screech');
                        eliminationBanner = {
                            text: `💀 ${lowestRacer.displayName || lowestRacer.username} ELIMINATED!`,
                            timer: 3.5,
                            color: '#FF5252'
                        };

                        // If only 1 racer remains active -> they win!
                        const remaining = activeRacers.filter(r => !r.finished);
                        if (remaining.length === 1) {
                            const champion = remaining[0];
                            champion.finished = true;
                            champion.lapTime = raceElapsedTime / 1000;
                            champion.placement = nextFrontPlacement++;
                            finishedRacers.push(champion);
                            reportRacerCompletion(champion, champion.placement);
                            playSound('win');
                            eliminationBanner = {
                                text: `🏆 ${champion.displayName || champion.username} WINS!`,
                                timer: 4.5,
                                color: '#00E676'
                            };
                            if (commonModule && commonModule.spawnSeasonalBurst) {
                                commonModule.spawnSeasonalBurst(particles, {
                                    x: width / 2,
                                    y: height / 2,
                                    season: (currentTheme && currentTheme.seasonPreset) || 'spring',
                                    count: 45,
                                    isBullseye: true
                                });
                            }
                        }
                    }
                }
            }
        }

        const maxLaneOffset = (currentSettings.trackThickness || 44) * (currentSettings.gameMode === 'DestructionDerby' ? 0.44 : 0.35);

        // Update flying detached vehicle parts (bumpers, spoilers, wheels)
        for (let i = detachedParts.length - 1; i >= 0; i--) {
            const p = detachedParts[i];
            p.x += p.vx * dtSec;
            p.y += p.vy * dtSec;
            p.vx *= Math.pow(0.85, dtSec * 60);
            p.vy *= Math.pow(0.85, dtSec * 60);
            p.rot += p.vRot * dtSec;
            p.vRot *= Math.pow(0.88, dtSec * 60);
            p.life -= dtSec;
            if (p.life < 1.0) {
                p.alpha = Math.max(0, p.life);
            }
            if (p.life <= 0) {
                detachedParts.splice(i, 1);
            }
        }

        // Destruction Derby & Combat Racing: check for sole survivor victory condition
        const isCombat = currentSettings.enableVehicleDamage || currentSettings.gameMode === 'DestructionDerby';
        if (isCombat && activeRacers.length > 1) {
            const alive = activeRacers.filter(r => !r.isTotaled);
            if (alive.length === 1 && !alive[0].finished) {
                const champion = alive[0];
                champion.finished = true;
                champion.isChampionReported = true;
                champion.lapTime = raceElapsedTime / 1000;
                champion.placement = nextFrontPlacement++;
                finishedRacers.push(champion);
                reportRacerCompletion(champion, champion.placement);
                playSound('win');
                eliminationBanner = {
                    text: `🏆 ${champion.displayName || champion.username} DEMOLITION CHAMPION!`,
                    timer: 5.0,
                    color: '#FFD166'
                };
                if (commonModule && commonModule.spawnSeasonalBurst) {
                    commonModule.spawnSeasonalBurst(particles, {
                        x: width / 2,
                        y: height / 2,
                        season: (currentTheme && currentTheme.seasonPreset) || 'summer',
                        count: 50,
                        isBullseye: true
                    });
                }
            } else if (alive.length === 0 && finishedRacers.length > 0 && !finishedRacers.some(r => r.isChampionReported)) {
                finishedRacers.sort((a, b) => (a.placement || 999) - (b.placement || 999));
                const champion = finishedRacers[0];
                champion.isChampionReported = true;
                if (!champion.placement) {
                    champion.placement = 1;
                }
                reportRacerCompletion(champion, champion.placement);
                playSound('win');
            }
        }

        // Destruction Derby Standoff Prevention: Maintain opposing traffic flow
        if (currentSettings.gameMode === 'DestructionDerby' && activeRacers.length > 1) {
            const aliveRacers = activeRacers.filter(r => !r.isTotaled && !r.finished);
            if (aliveRacers.length > 1) {
                const firstDir = aliveRacers[0].dir || 1;
                const allSameDir = aliveRacers.every(x => (x.dir || 1) === firstDir);
                if (allSameDir) {
                    // Standoff detected: invert direction of half the surviving cars!
                    for (let k = 0; k < aliveRacers.length; k += 2) {
                        const flipper = aliveRacers[k];
                        flipper.dir = -firstDir;
                        flipper.driftAngle = (Math.random() < 0.5 ? 0.35 : -0.35);
                        flipper.laneChangeTimer = 0.4;
                        playSound('screech');
                    }
                }
            }
        }

        // Respawn collected hazards over time so multi-lap races stay lively
        if (currentSettings.enableDebris) {
            trackHazards.forEach(h => {
                if (!h.active) {
                    h.respawnTimer = (h.respawnTimer || 0) - dtSec;
                    if (h.respawnTimer <= 0) {
                        h.active = true;
                        h.laneOffset = (Math.random() * 2 - 1) * maxLaneOffset * 0.85;
                    }
                }
            });
        }

        // Update active racers
        for (let i = 0; i < activeRacers.length; i++) {
            const r = activeRacers[i];
            if (r.finished) continue;

            if (r.shieldTimer > 0) {
                r.shieldTimer -= dtSec;
                if (r.shieldTimer < 0) r.shieldTimer = 0;
            }

            if (r.hitCooldown > 0) {
                r.hitCooldown -= dtSec;
                if (r.hitCooldown < 0) r.hitCooldown = 0;
            }

            // 1. Calculate effective speed with driver pace wave
            const basePace = (0.28 * currentSettings.baseSpeed);
            const paceVariation = 1.0 + Math.sin(r.progress * 16.0 + (r.seed || (i * 17))) * 0.06;
            let speed = basePace * paceVariation;

            // Drafting boost
            if (r.isBoosting) {
                speed *= 1.55;
                r.boostTimer -= dtSec;
                if (r.boostTimer <= 0) {
                    r.isBoosting = false;
                    r.targetLane = 0;
                }
            } else if (r.isDrafting) {
                speed *= 1.25;
            }

            // Rubber-banding catch-up to ensure close racing
            if (currentSettings.enableRubberBanding && maxProgress > r.progress) {
                const distBehind = Math.min(1.0, maxProgress - r.progress);
                speed *= (1.0 + distBehind * (currentSettings.rubberBandStrength || 0.25));
            }

            // Wreck spinout penalty
            if (r.isWrecked) {
                speed *= 0.15;
                r.spinAngle += Math.PI * 4 * dtSec;
                r.wreckTimer -= dtSec;
                if (r.wreckTimer <= 0) {
                    r.isWrecked = false;
                    r.spinAngle = 0;
                }
            }

            // 2. Autonomous Lane Switching & Proactive Collision Avoidance / Derby Targeting
            if (!r.isWrecked && !r.isTotaled && !r.isBoosting) {
                const isDerby = currentSettings.gameMode === 'DestructionDerby';
                const rPt = getTrackPoint(r.progress, currentSettings.trackType, currentSettings.stripPosition);
                const rx = rPt.x + rPt.normalX * r.laneOffset;
                const ry = rPt.y + rPt.normalY * r.laneOffset;
                let isAvoiding = false;

                if (isDerby) {
                    // DEMOLITION DERBY: Aggressive Opponent Targeting AI
                    let targetOpponent = null;
                    let minOpponentDist = Infinity;

                    for (let j = 0; j < activeRacers.length; j++) {
                        if (i === j) continue;
                        const other = activeRacers[j];
                        if (other.finished || other.isTotaled) continue;

                        const oPt = getTrackPoint(other.progress, currentSettings.trackType, currentSettings.stripPosition);
                        const ox = oPt.x + oPt.normalX * other.laneOffset;
                        const oy = oPt.y + oPt.normalY * other.laneOffset;
                        const dist2D = Math.hypot(rx - ox, ry - oy);

                        if (dist2D < minOpponentDist) {
                            minOpponentDist = dist2D;
                            targetOpponent = other;
                        }
                    }

                    // Seek repair wrench if injured
                    let soughtPickup = false;
                    if (currentSettings.enableDebris && trackHazards.length > 0) {
                        const curNorm = ((r.progress % 1.0) + 1.0) % 1.0;
                        for (const h of trackHazards) {
                            if (!h.active) continue;
                            let hDist = Math.abs(h.t - curNorm);
                            if (hDist > 0.5) hDist = 1.0 - hDist;
                            if (hDist < 0.045) {
                                if (h.type === 'wrench' && (r.hp || 100) < 75) {
                                    r.targetLaneOffset = h.laneOffset;
                                    r.laneChangeTimer = 0.6;
                                    soughtPickup = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!soughtPickup) {
                        if (targetOpponent && minOpponentDist < 180) {
                            // Intercept target: steer directly into opponent's lane to ram!
                            r.targetLaneOffset = targetOpponent.laneOffset;
                            r.laneChangeTimer = 0.35;
                            // Pursuit acceleration: speed up when hunting down an opponent!
                            const isSameDir = (r.dir || 1) === (targetOpponent.dir || 1);
                            if (minOpponentDist < 90) {
                                speed *= (isSameDir ? 1.35 : 1.25);
                            }
                        } else {
                            // Roam aggressively across lanes to sweep track width
                            r.laneChangeTimer = (r.laneChangeTimer || 0) - dtSec;
                            if (r.laneChangeTimer <= 0) {
                                r.laneChangeTimer = 0.8 + Math.random() * 1.2;
                                r.targetLaneOffset = (Math.random() * 2 - 1) * maxLaneOffset * 0.85;
                            }
                        }

                        // Periodically allow roaming cars to flip direction if hunting
                        r.uTurnTimer = (r.uTurnTimer || (5 + Math.random() * 5)) - dtSec;
                        if (r.uTurnTimer <= 0) {
                            r.uTurnTimer = 6 + Math.random() * 6;
                            if (Math.random() < 0.4) {
                                r.dir = -(r.dir || 1);
                                r.driftAngle = (Math.random() < 0.5 ? 0.3 : -0.3);
                                playSound('screech');
                            }
                        }
                    }

                    if (r.targetLaneOffset != null) {
                        const steerRate = 4.8 * dtSec;
                        r.laneOffset += (r.targetLaneOffset - r.laneOffset) * steerRate;
                        r.steerAngle = Math.max(-0.2, Math.min(0.2, (r.targetLaneOffset - r.laneOffset) * 0.02));
                    }
                } else {
                    // Standard Racing: Proactive collision avoidance & safe passing
                    r.laneChangeTimer = (r.laneChangeTimer || 0) - dtSec;
                    if (r.laneChangeTimer <= 0) {
                        r.laneChangeTimer = 2.5 + Math.random() * 3.0;
                        const possibleOffsets = [-maxLaneOffset * 0.7, -maxLaneOffset * 0.35, 0, maxLaneOffset * 0.35, maxLaneOffset * 0.7];
                        const randomPref = possibleOffsets[Math.floor(Math.random() * possibleOffsets.length)];
                        r.targetLaneOffset = pickSafestClearLane(i, randomPref, maxLaneOffset);
                    }

                    // Check other cars: proactive avoidance of cars ahead AND side-by-side spacing in curves
                    for (let j = 0; j < activeRacers.length; j++) {
                        if (i === j) continue;
                        const other = activeRacers[j];
                        if (other.finished || other.isTotaled) continue;

                        const oPt = getTrackPoint(other.progress, currentSettings.trackType, currentSettings.stripPosition);
                        const ox = oPt.x + oPt.normalX * other.laneOffset;
                        const oy = oPt.y + oPt.normalY * other.laneOffset;
                        const dist2D = Math.hypot(rx - ox, ry - oy);

                        let distAhead = other.progress - r.progress;
                        if (distAhead < -0.5) distAhead += 1.0;
                        else if (distAhead > 0.5) distAhead -= 1.0;

                        // Close proximity danger (covers curves, corners, and straight convergence)
                        if (dist2D < 34) {
                            const isAggressive = (r.shieldTimer || 0) > 0;
                            if (!isAggressive) {
                                if (distAhead > -0.005) {
                                    speed *= 0.93;
                                }
                                const repulse = (other.laneOffset >= r.laneOffset) ? -maxLaneOffset * 0.75 : maxLaneOffset * 0.75;
                                const safeLane = pickSafestClearLane(i, repulse, maxLaneOffset);
                                if (Math.abs(safeLane - r.laneOffset) > 2) {
                                    r.targetLaneOffset = safeLane;
                                    r.laneChangeTimer = 0.8;
                                    isAvoiding = true;
                                }
                            }
                        } else if (distAhead > 0.008 && distAhead < 0.05 && dist2D < 48) {
                            if (Math.abs(other.laneOffset - r.laneOffset) < 16) {
                                const prefAvoid = (other.laneOffset >= 0) ? -maxLaneOffset * 0.7 : maxLaneOffset * 0.7;
                                const safeLane = pickSafestClearLane(i, prefAvoid, maxLaneOffset);
                                if (Math.abs(safeLane - r.laneOffset) > 3) {
                                    r.targetLaneOffset = safeLane;
                                    r.laneChangeTimer = 1.0;
                                    isAvoiding = true;
                                } else {
                                    speed *= 0.94;
                                }
                            }
                        }
                    }

                    // Hazard avoidance & Item seeking
                    if (currentSettings.enableDebris && trackHazards.length > 0) {
                        const curNorm = ((r.progress % 1.0) + 1.0) % 1.0;
                        for (const h of trackHazards) {
                            if (!h.active) continue;
                            let hDist = h.t - curNorm;
                            if (hDist < 0) hDist += 1.0;

                            if (hDist > 0.008 && hDist < 0.045) {
                                if (h.type === 'wrench' && (r.hp || 100) < 85) {
                                    r.targetLaneOffset = pickSafestClearLane(i, h.laneOffset, maxLaneOffset);
                                    r.laneChangeTimer = 0.8;
                                    break;
                                } else if (h.type === 'shield' && (r.shieldTimer || 0) <= 0) {
                                    r.targetLaneOffset = pickSafestClearLane(i, h.laneOffset, maxLaneOffset);
                                    r.laneChangeTimer = 0.8;
                                    break;
                                } else if ((h.type === 'oil' || h.type === 'cone') && Math.abs(h.laneOffset - r.laneOffset) < 14) {
                                    const swerveDir = (h.laneOffset >= 0) ? -maxLaneOffset * 0.75 : maxLaneOffset * 0.75;
                                    r.targetLaneOffset = pickSafestClearLane(i, swerveDir, maxLaneOffset);
                                    r.laneChangeTimer = 0.8;
                                    break;
                                }
                            }
                        }
                    }

                    if (r.targetLaneOffset != null) {
                        const steerRate = (isAvoiding ? 5.2 : 2.8) * dtSec;
                        r.laneOffset += (r.targetLaneOffset - r.laneOffset) * steerRate;
                        r.steerAngle = Math.max(-0.15, Math.min(0.15, (r.targetLaneOffset - r.laneOffset) * 0.015));
                    }
                }
            } else if (r.steerAngle) {
                r.steerAngle *= Math.max(0, 1 - dtSec * 6);
            }

            // 3. Drafting & Slingshot Overtaking
            if (currentSettings.enableDrafting && !r.isBoosting && !r.isWrecked) {
                let foundDraft = false;
                for (let j = 0; j < activeRacers.length; j++) {
                    if (i === j) continue;
                    const other = activeRacers[j];
                    if (other.finished) continue;
                    const distAhead = other.progress - r.progress;
                    if (distAhead > 0.012 && distAhead < 0.065 && Math.abs(other.laneOffset - r.laneOffset) < 14) {
                        foundDraft = true;
                        // Tuck in behind the car ahead to gain slipstream
                        r.laneOffset += (other.laneOffset - r.laneOffset) * (1.8 * dtSec);
                        r.draftCharge = (r.draftCharge || 0) + dtSec;

                        // Slingshot Overtake Burst!
                        if (r.draftCharge > 0.9) {
                            r.isBoosting = true;
                            r.boostTimer = 1.3;
                            r.draftCharge = 0;
                            // Swerve out cleanly to pass into open lane
                            const prefPassLane = (other.laneOffset >= 0) ? -maxLaneOffset * 0.75 : maxLaneOffset * 0.75;
                            r.laneOffset = pickSafestClearLane(i, prefPassLane, maxLaneOffset);
                            r.targetLaneOffset = r.laneOffset;
                            playSound('boost');
                        }
                        break;
                    }
                }
                r.isDrafting = foundDraft;
                if (!foundDraft) {
                    r.draftCharge = Math.max(0, (r.draftCharge || 0) - dtSec * 0.6);
                }
            }

            // 4. Car-to-Car Bumping & Combat Collisions
            if (currentSettings.enableCollisions && !r.isTotaled) {
                const isCombat = currentSettings.enableVehicleDamage || currentSettings.gameMode === 'DestructionDerby';
                if (!isCombat && r.isWrecked) {
                    // Non-combat pure racing skips collision during wreck spinout
                } else {
                    const rPt = getTrackPoint(r.progress, currentSettings.trackType, currentSettings.stripPosition);
                    const rx = rPt.x + rPt.normalX * r.laneOffset;
                    const ry = rPt.y + rPt.normalY * r.laneOffset;

                    for (let j = i + 1; j < activeRacers.length; j++) {
                        const other = activeRacers[j];
                        if (other.finished || other.isTotaled) continue;
                        if (!isCombat && other.isWrecked) continue;

                        const oPt = getTrackPoint(other.progress, currentSettings.trackType, currentSettings.stripPosition);
                        const ox = oPt.x + oPt.normalX * other.laneOffset;
                        const oy = oPt.y + oPt.normalY * other.laneOffset;

                        // Physical 2D distance between vehicles (true chassis contact)
                        const dist2D = Math.hypot(rx - ox, ry - oy);
                        const hitThreshold = isCombat ? 27 : 21;

                        if (dist2D < hitThreshold) {
                        // Deflection push: bounce lanes apart
                        const push = (r.laneOffset <= other.laneOffset) ? -8 : 8;
                        r.laneOffset = Math.max(-maxLaneOffset, Math.min(maxLaneOffset, r.laneOffset + push));
                        other.laneOffset = Math.max(-maxLaneOffset, Math.min(maxLaneOffset, other.laneOffset - push));

                        // Retain deflected lanes so cars do not immediately snap back into collision
                        r.targetLaneOffset = r.laneOffset;
                        other.targetLaneOffset = other.laneOffset;
                        r.laneChangeTimer = (currentSettings.gameMode === 'DestructionDerby') ? 0.35 : 1.2;
                        other.laneChangeTimer = (currentSettings.gameMode === 'DestructionDerby') ? 0.35 : 1.2;

                        // Momentary speed friction separating the cars along track
                        speed *= 0.92;

                        // Comic drift angle visual
                        r.driftAngle = (push > 0) ? 0.22 : -0.22;
                        other.driftAngle = (push > 0) ? -0.22 : 0.22;

                        // Cooldown guard: discrete impact events with recovery window
                        const canDamage = isCombat && (r.hitCooldown || 0) <= 0 && (other.hitCooldown || 0) <= 0;

                        if (canDamage) {
                            const cd = (currentSettings.gameMode === 'DestructionDerby') ? 0.7 : 0.85;
                            r.hitCooldown = cd;
                            other.hitCooldown = cd;

                            playSound('bump');

                            const mult = currentSettings.ramDamageMultiplier ?? 1.0;
                                const oPt = getTrackPoint(other.progress, currentSettings.trackType, currentSettings.stripPosition);
                                const ox = oPt.x + oPt.normalX * other.laneOffset;
                                const oy = oPt.y + oPt.normalY * other.laneOffset;

                                // Impact sparks at midpoint
                                const midX = (rx + ox) / 2;
                                const midY = (ry + oy) / 2;
                                const isHeadOn = (r.dir || 1) !== (other.dir || 1);
                                const sparkCount = isHeadOn ? 12 : 6;
                                for (let sp = 0; sp < sparkCount; sp++) {
                                    particles.push({
                                        x: midX,
                                        y: midY,
                                        vx: (Math.random() - 0.5) * (isHeadOn ? 100 : 60),
                                        vy: (Math.random() - 0.5) * (isHeadOn ? 100 : 60),
                                        alpha: 1.0,
                                        size: isHeadOn ? 3.2 : 2.5,
                                        color: Math.random() < 0.5 ? '#FFD166' : '#FF5252'
                                    });
                                }

                                const rHasShield = (r.shieldTimer || 0) > 0;
                                const oHasShield = (other.shieldTimer || 0) > 0;

                                // Calibrated discrete impact damage (balanced for prolonged multi-hit demolition battles)
                                const baseDmg = (currentSettings.gameMode === 'DestructionDerby') ? (6 + Math.random() * 5) : (4 + Math.random() * 3);
                                let dmgToR = baseDmg * mult;
                                let dmgToOther = baseDmg * mult;

                                if (isHeadOn) {
                                    dmgToR *= 1.4;
                                    dmgToOther *= 1.4;
                                }

                                // Vulnerable spinning cars take extra damage when struck!
                                if (r.isWrecked) dmgToR *= 1.2;
                                if (other.isWrecked) dmgToOther *= 1.2;

                                if (r.isBoosting) dmgToOther *= 1.3;
                                if (other.isBoosting) dmgToR *= 1.3;

                                if (rHasShield) {
                                    dmgToR = 0;
                                    dmgToOther *= 2.0;
                                }
                                if (oHasShield) {
                                    dmgToOther = 0;
                                    dmgToR *= 2.0;
                                }

                                r.hp = Math.max(0, (r.hp ?? (currentSettings.startingHealth || 100)) - dmgToR);
                                other.hp = Math.max(0, (other.hp ?? (currentSettings.startingHealth || 100)) - dmgToOther);

                                const rAngle = rPt.angle + (r.dir === -1 ? Math.PI : 0);
                                const oAngle = oPt.angle + (other.dir === -1 ? Math.PI : 0);

                                checkPartLoss(r, rx, ry, rAngle);
                                checkPartLoss(other, ox, oy, oAngle);

                                // Check if other got totaled
                                if (other.hp <= 0 && !other.isTotaled) {
                                    other.isTotaled = true;
                                    other.finished = true;
                                    other.killerToken = r.dropToken;
                                    r.kills = (r.kills || 0) + 1;
                                    other.lapTime = raceElapsedTime / 1000;
                                    other.placement = nextBackPlacement--;
                                    finishedRacers.push(other);
                                    reportRacerCompletion(other, other.placement);
                                    playSound('screech');
                                    eliminationBanner = {
                                        text: `💥 ${other.displayName || other.username} TOTALED by ${r.displayName || r.username}!`,
                                        timer: 3.5,
                                        color: '#FF3D00'
                                    };
                                    spawnDetachedPart(ox, oy, oAngle, 'bumper', '#1e232a');
                                    spawnDetachedPart(ox, oy, oAngle + Math.PI, 'spoiler', '#1e232a');
                                    spawnDetachedPart(ox, oy, oAngle + Math.PI / 2, 'wheel', '#111315');
                                }

                                // Check if r got totaled
                                if (r.hp <= 0 && !r.isTotaled) {
                                    r.isTotaled = true;
                                    r.finished = true;
                                    r.killerToken = other.dropToken;
                                    other.kills = (other.kills || 0) + 1;
                                    r.lapTime = raceElapsedTime / 1000;
                                    r.placement = nextBackPlacement--;
                                    finishedRacers.push(r);
                                    reportRacerCompletion(r, r.placement);
                                    playSound('screech');
                                    eliminationBanner = {
                                        text: `💥 ${r.displayName || r.username} TOTALED by ${other.displayName || other.username}!`,
                                        timer: 3.5,
                                        color: '#FF3D00'
                                    };
                                    spawnDetachedPart(rx, ry, rAngle, 'bumper', '#1e232a');
                                    spawnDetachedPart(rx, ry, rAngle + Math.PI, 'spoiler', '#1e232a');
                                    spawnDetachedPart(rx, ry, rAngle + Math.PI / 2, 'wheel', '#111315');
                                    break;
                                }
                            } else if (!isCombat) {
                                playSound('bump');
                            }
                            break;
                        }
                    }
                }
            }
            if (r.driftAngle) {
                    r.driftAngle *= Math.max(0, 1 - dtSec * 6);
                }

            // 4. Track Hazards Collisions with Respawning
            if (currentSettings.enableDebris && !r.isWrecked && !r.isTotaled) {
                const rPt = getTrackPoint(r.progress, currentSettings.trackType, currentSettings.stripPosition);
                const rx = rPt.x + rPt.normalX * r.laneOffset;
                const ry = rPt.y + rPt.normalY * r.laneOffset;
                const rAngle = rPt.angle + (r.dir === -1 ? Math.PI : 0);
                const frontX = rx + Math.cos(rAngle) * 14;
                const frontY = ry + Math.sin(rAngle) * 14;

                const curNorm = ((r.progress % 1.0) + 1.0) % 1.0;

                trackHazards.forEach(h => {
                    if (!h.active) return;

                    // Progress pre-filter: ignore hazards far away along spline
                    let progDiff = Math.abs(curNorm - h.t);
                    if (progDiff > 0.5) progDiff = 1.0 - progDiff;
                    if (progDiff > 0.012) return;

                    const hPt = getTrackPoint(h.t, currentSettings.trackType, currentSettings.stripPosition);
                    const hx = hPt.x + hPt.normalX * h.laneOffset;
                    const hy = hPt.y + hPt.normalY * h.laneOffset;

                    const dCenter = Math.hypot(rx - hx, ry - hy);
                    const dFront = Math.hypot(frontX - hx, frontY - hy);

                    // Physical visual contact: triggers only when front bumper or chassis contacts hazard
                    if (dFront < 15 || dCenter < 18) {
                        h.active = false;
                        h.respawnTimer = 4.5 + Math.random() * 3.0; // Automatically respawns in 4.5-7.5s!
                        if (h.type === 'oil') {
                            r.isWrecked = true;
                            r.wreckTimer = 1.4;
                            r.wrecks = (r.wrecks || 0) + 1;
                            playSound('screech');
                        } else if (h.type === 'bomb') {
                            // Explosive TNT / Bomb Barrel in Derby mode
                            const bombDmg = 10 + Math.random() * 5;
                            r.hp = Math.max(0, (r.hp ?? 100) - bombDmg);
                            r.isWrecked = true;
                            r.wreckTimer = 1.2;
                            r.wrecks = (r.wrecks || 0) + 1;
                            playSound('screech');
                            checkPartLoss(r, rx, ry, rAngle);
                            for (let sp = 0; sp < 12; sp++) {
                                particles.push({
                                    x: rx,
                                    y: ry,
                                    vx: (Math.random() - 0.5) * 90,
                                    vy: (Math.random() - 0.5) * 90,
                                    alpha: 1.0,
                                    size: 3.5,
                                    color: Math.random() < 0.5 ? '#FF3D00' : '#FFD166'
                                });
                            }
                            if (r.hp <= 0 && !r.isTotaled) {
                                r.isTotaled = true;
                                r.finished = true;
                                r.lapTime = raceElapsedTime / 1000;
                                r.placement = nextBackPlacement--;
                                finishedRacers.push(r);
                                reportRacerCompletion(r, r.placement);
                                eliminationBanner = {
                                    text: `💥 ${r.displayName || r.username} BLOWN UP by a bomb!`,
                                    timer: 3.5,
                                    color: '#FF3D00'
                                };
                                spawnDetachedPart(rx, ry, rAngle, 'bumper', '#1e232a');
                                spawnDetachedPart(rx, ry, rAngle + Math.PI, 'spoiler', '#1e232a');
                                spawnDetachedPart(rx, ry, rAngle + Math.PI / 2, 'wheel', '#111315');
                            }
                        } else if (h.type === 'spike') {
                            // Spike Trap in Derby mode
                            const spikeDmg = 6 + Math.random() * 4;
                            r.hp = Math.max(0, (r.hp ?? 100) - spikeDmg);
                            r.wrecks = (r.wrecks || 0) + 1;
                            r.driftAngle = Math.random() < 0.5 ? 0.3 : -0.3;
                            playSound('bump');
                            checkPartLoss(r, rx, ry, rAngle);
                            if (r.hp <= 0 && !r.isTotaled) {
                                r.isTotaled = true;
                                r.finished = true;
                                r.lapTime = raceElapsedTime / 1000;
                                r.placement = nextBackPlacement--;
                                finishedRacers.push(r);
                                reportRacerCompletion(r, r.placement);
                                eliminationBanner = {
                                    text: `💥 ${r.displayName || r.username} SHREDDED by spikes!`,
                                    timer: 3.5,
                                    color: '#FF3D00'
                                };
                                spawnDetachedPart(rx, ry, rAngle, 'bumper', '#1e232a');
                                spawnDetachedPart(rx, ry, rAngle + Math.PI, 'spoiler', '#1e232a');
                                spawnDetachedPart(rx, ry, rAngle + Math.PI / 2, 'wheel', '#111315');
                            }
                        } else if (h.type === 'cone') {
                            r.progress -= 0.005;
                            r.wrecks = (r.wrecks || 0) + 1;
                            r.driftAngle = (Math.random() > 0.5 ? 0.22 : -0.22);
                            playSound('bump');
                        } else if (h.type === 'boost') {
                            r.isBoosting = true;
                            r.boostTimer = 1.5;
                            playSound('boost');
                        } else if (h.type === 'wrench') {
                            const maxHp = r.maxHp || 100;
                            r.hp = Math.min(maxHp, (r.hp ?? maxHp) + 35);
                            playSound('boost');
                            for (let k = 0; k < 8; k++) {
                                particles.push({
                                    x: rx + (Math.random() * 16 - 8),
                                    y: ry + (Math.random() * 16 - 8),
                                    vx: (Math.random() - 0.5) * 40,
                                    vy: (Math.random() - 0.5) * 40,
                                    alpha: 0.9,
                                    size: 3,
                                    color: '#00E676'
                                });
                            }
                        } else if (h.type === 'shield') {
                            r.shieldTimer = 5.0;
                            playSound('boost');
                            for (let k = 0; k < 10; k++) {
                                particles.push({
                                    x: rx + (Math.random() * 20 - 10),
                                    y: ry + (Math.random() * 20 - 10),
                                    vx: (Math.random() - 0.5) * 50,
                                    vy: (Math.random() - 0.5) * 50,
                                    alpha: 0.9,
                                    size: 3.5,
                                    color: '#00E5FF'
                                });
                            }
                        }
                    }
                });
            }

            // Ensure cars stay strictly on track surface (clamp to curb boundaries, no forced center magnetic pull)
            if (r.laneOffset > maxLaneOffset) {
                r.laneOffset = maxLaneOffset;
            } else if (r.laneOffset < -maxLaneOffset) {
                r.laneOffset = -maxLaneOffset;
            }

            // Move racer along spline and check race finish (after avoidance, drafting, bumping, and hazard hits)
            if (!r.finished && !r.isTotaled) {
                r.progress += (r.dir || 1) * (speed * dtSec);

                // Themed tire spray & skid particles
                if (!r.isEliminated) {
                    const pt = getTrackPoint(r.progress, currentSettings.trackType, currentSettings.stripPosition);
                    const carAngle = pt.angle + (r.dir === -1 ? Math.PI : 0) + r.spinAngle + r.driftAngle + (r.steerAngle || 0);
                    const isHighActivity = r.isBoosting || r.isDrafting || r.isWrecked || Math.abs(r.driftAngle) > 0.04;
                    if (isHighActivity ? Math.random() < 0.5 : Math.random() < 0.1) {
                        spawnTireSpray(r, pt, carAngle, (currentTheme && currentTheme.seasonPreset) || 'spring');
                    }
                }

                // Check lap completion and race finish (Destruction Derby is Last-Car-Standing, not lap-limited!)
                if (currentSettings.gameMode !== 'DestructionDerby' && r.progress >= totalLaps) {
                    r.finished = true;
                    r.lapTime = raceElapsedTime / 1000;
                    r.placement = nextFrontPlacement++;
                    finishedRacers.push(r);

                    if (currentSettings.gameMode === 'TimeTrial') {
                        const lapMs = raceElapsedTime;
                        const savedRecord = loadBestLap(currentSettings.trackType);
                        const isNewRecord = !savedRecord || lapMs < savedRecord.timeMs;
                        r.isNewRecord = isNewRecord;
                        r.prevBestMs = savedRecord ? savedRecord.timeMs : null;
                        lastRecordedLapMs = lapMs;
                        lastRecordedLapRacer = r.displayName || r.username;
                        if (isNewRecord) {
                            saveBestLap(currentSettings.trackType, lapMs, r.displayName || r.username);
                        }
                    }

                    if (currentSettings.gameMode === 'Elimination') {
                        reportRacerCompletion(r, r.placement);
                        playSound('win');
                        eliminationBanner = {
                            text: `🏆 ${r.displayName || r.username} WINS!`,
                            timer: 4.5,
                            color: '#00E676'
                        };
                        if (commonModule && commonModule.spawnSeasonalBurst) {
                            commonModule.spawnSeasonalBurst(particles, {
                                x: width / 2,
                                y: height / 2,
                                season: (currentTheme && currentTheme.seasonPreset) || 'spring',
                                count: 45,
                                isBullseye: true
                            });
                        }

                        // Any other unfinished racers finish in reverse order of progress
                        const remaining = activeRacers.filter(x => !x.finished).sort((a, b) => b.progress - a.progress);
                        remaining.forEach((rem) => {
                            rem.finished = true;
                            rem.lapTime = raceElapsedTime / 1000;
                            rem.placement = nextFrontPlacement++;
                            finishedRacers.push(rem);
                            reportRacerCompletion(rem, rem.placement);
                        });
                    } else {
                        // Report completion back to server (sole source of truth)
                        reportRacerCompletion(r, r.placement);

                        if (r.placement === 1) {
                            playSound('win');
                            if (commonModule && commonModule.spawnSeasonalBurst) {
                                commonModule.spawnSeasonalBurst(particles, {
                                    x: width / 2,
                                    y: height / 2,
                                    season: (currentTheme && currentTheme.seasonPreset) || 'spring',
                                    count: 45,
                                    isBullseye: true
                                });
                            }
                        }
                    }
                }
            }
        }

        // Check if all racers finished
        if (activeRacers.length > 0 && activeRacers.every(r => r.finished)) {
            if (gameState !== 'podium') {
                playSound('end');
            }
            gameState = 'podium';
            podiumTimer = 12000;
        }
    }

    // Reports completed racer back to WebSocket server
    function reportRacerCompletion(racer, placement) {
        if (!racer || !racer.dropToken) return;
        racer.placement = placement;

        let score = 2;
        if (placement === 1) score = 25;
        else if (placement === 2) score = 18;
        else if (placement === 3) score = 15;

        const payload = {
            dropToken: racer.dropToken,
            overlayKey: overlayKey,
            racePlacement: placement,
            lapTimeSeconds: racer.lapTime || 0.0,
            wreckCount: racer.wrecks || 0,
            score: score,
            isWin: (placement === 1),
            racingMode: currentSettings.gameMode || 'GrandPrix',
            lapsCompleted: currentSettings.totalLaps || 2,
            knockoutCount: racer.kills || 0,
            killerDropToken: racer.killerToken || null,
            isTotaled: !!racer.isTotaled
        };

        if (completionChannel && typeof completionChannel.send === 'function') {
            completionChannel.send(payload);
        } else if (socket && socket.readyState === WebSocket.OPEN) {
            socket.send(JSON.stringify(payload));
        } else if (typeof window.WS !== 'undefined' && window.WS.send) {
            window.WS.send(JSON.stringify(payload));
        }
    }

    function gameLoop(now) {
        const dt = Math.min(100, now - lastTime);
        lastTime = now;

        updatePhysics(dt);

        // Smooth fade in / fade out of entire overlay
        overlayAlpha += (targetOverlayAlpha - overlayAlpha) * 0.08;

        ctx.clearRect(0, 0, width, height);

        if (overlayAlpha > 0.01) {
            drawTrack();
            drawHazards();
            drawDetachedParts();
            drawGhostCar();

            // Render all active racers
            activeRacers.forEach(r => drawVehicle(r));

            // Render particles
            for (let i = particles.length - 1; i >= 0; i--) {
                const p = particles[i];
                p.x += (p.vx || 0);
                p.y += (p.vy || 0);
                p.alpha = (p.alpha || 1.0) - (p.fade || 0.02);
                if (p.alpha <= 0) {
                    particles.splice(i, 1);
                } else {
                    ctx.save();
                    ctx.globalAlpha = p.alpha * overlayAlpha;
                    ctx.fillStyle = p.color || '#FFD166';
                    if (p.shape === 'leaf') {
                        ctx.translate(p.x, p.y);
                        ctx.rotate(p.rotation || 0);
                        p.rotation = (p.rotation || 0) + (p.rotSpeed || 0.05);
                        ctx.beginPath();
                        ctx.ellipse(0, 0, p.size || 3.5, (p.size || 3.5) * 0.5, 0, 0, Math.PI * 2);
                        ctx.fill();
                    } else if (p.shape === 'petal') {
                        ctx.translate(p.x, p.y);
                        ctx.rotate(p.rotation || 0);
                        p.rotation = (p.rotation || 0) + (p.rotSpeed || 0.04);
                        ctx.beginPath();
                        ctx.ellipse(0, 0, p.size || 3.2, (p.size || 3.2) * 0.65, 0.4, 0, Math.PI * 2);
                        ctx.fill();
                    } else if (p.shape === 'snow') {
                        ctx.beginPath();
                        ctx.arc(p.x, p.y, p.size || 2.2, 0, Math.PI * 2);
                        ctx.fill();
                    } else if (p.shape === 'spark') {
                        ctx.beginPath();
                        ctx.arc(p.x, p.y, p.size || 1.8, 0, Math.PI * 2);
                        ctx.fill();
                    } else {
                        ctx.beginPath();
                        ctx.arc(p.x, p.y, p.size || 3, 0, Math.PI * 2);
                        ctx.fill();
                    }
                    ctx.restore();
                }
            }

            drawLobbyUI();
            drawCountdownUI();
            drawRaceHUD();
            drawPodiumUI();
        }

        requestAnimationFrame(gameLoop);
    }
    requestAnimationFrame(gameLoop);

    // ================= WEBSOCKET MESSAGE HANDLER =================
    function handleWsMessage(msg) {
        if (!msg || !msg.type) return;

        if (msg.type === 'race_lobby_start') {
            gameState = 'lobby';
            targetOverlayAlpha = 1.0;
            currentTheme = msg.theme;
            currentSounds = msg.sounds;
            if (msg.messages) currentMessages = msg.messages;
            if (soundManager && msg.sounds) soundManager.updateBundle(msg.sounds);
            currentSettings.trackType = msg.trackType || 'Perimeter';
            currentSettings.stripPosition = msg.stripPosition || 'Bottom';
            currentSettings.totalLaps = msg.totalLaps ?? 2;
            currentSettings.gameMode = msg.gameMode || 'GrandPrix';
            currentSettings.trackAlpha = msg.trackAlpha ?? 0.55;
            currentSettings.showTrackBorders = msg.showTrackBorders ?? true;
            if (msg.trackThicknessPercent != null) {
                currentSettings.trackThickness = Math.max(16, Math.min(width, height) * (msg.trackThicknessPercent / 100));
            }
            currentSettings.maxRacers = msg.maxRacers ?? 12;
            currentSettings.enableVehicleDamage = msg.enableVehicleDamage ?? currentSettings.enableVehicleDamage ?? false;
            currentSettings.enableItemPickups = msg.enableItemPickups ?? currentSettings.enableItemPickups ?? false;
            currentSettings.startingHealth = msg.startingHealth ?? currentSettings.startingHealth ?? 100;
            currentSettings.ramDamageMultiplier = msg.ramDamageMultiplier ?? currentSettings.ramDamageMultiplier ?? 1.0;
            currentSettings.derbyKnockoutPoints = msg.derbyKnockoutPoints ?? currentSettings.derbyKnockoutPoints ?? 50;

            lobbyTotalMs = (msg.lobbyDurationSeconds ?? 60) * 1000;
            lobbyRemainingMs = lobbyTotalMs;
            lobbyRacers = [];
            activeRacers = [];
            finishedRacers = [];
            nextFrontPlacement = 1;
            nextBackPlacement = 1;
            detachedParts = [];
            eliminatedLaps.clear();
            eliminationBanner = null;
            timeTrialGhost = null;
            trackHazards = [];
            playSound('countdown_beep');
        } else if (msg.type === 'race_grid_update') {
            if (msg.maxRacers != null) {
                currentSettings.maxRacers = msg.maxRacers;
            }
            const incoming = [];
            if (msg.newRacer) incoming.push(msg.newRacer);
            if (Array.isArray(msg.racers)) {
                for (const r of msg.racers) {
                    if (r) incoming.push(r);
                }
            }

            for (const racer of incoming) {
                if (!racer || !racer.dropToken) continue;
                if (lobbyRacers.some(lr => lr.dropToken === racer.dropToken)) continue;

                lobbyRacers.push(racer);
                // Grid slots behind starting line
                const gridIdx = lobbyRacers.length - 1;
                const lane = (gridIdx % 2 === 0) ? -10 : 10;
                const progressOffset = -0.015 * Math.floor(gridIdx / 2);
                activeRacers.push({
                    dropToken: racer.dropToken,
                    username: racer.username,
                    displayName: racer.displayName,
                    userColor: racer.userColor,
                    emoteUrl: racer.emoteUrl,
                    progress: progressOffset,
                    laneOffset: lane,
                    targetLaneOffset: lane,
                    laneChangeTimer: 1.5 + Math.random() * 2.5,
                    steerAngle: 0,
                    driftAngle: 0,
                    spinAngle: 0,
                    finished: false,
                    wrecks: 0,
                    hp: currentSettings.startingHealth || 100,
                    maxHp: currentSettings.startingHealth || 100,
                    isTotaled: false,
                    kills: 0,
                    shieldTimer: 0,
                    hitCooldown: 0,
                    lostFrontBumper: false,
                    lostSpoiler: false,
                    lostWheel: false,
                    killerToken: null
                });
            }
        } else if (msg.type === 'race_start') {
            currentTheme = msg.theme;
            currentSounds = msg.sounds;
            if (msg.messages) currentMessages = msg.messages;
            if (soundManager && msg.sounds) soundManager.updateBundle(msg.sounds);
            currentSettings.trackType = msg.trackType || currentSettings.trackType;
            currentSettings.stripPosition = msg.stripPosition || currentSettings.stripPosition;
            currentSettings.totalLaps = msg.totalLaps ?? currentSettings.totalLaps;
            currentSettings.gameMode = msg.gameMode || currentSettings.gameMode;
            currentSettings.baseSpeed = msg.baseSpeed ?? 0.5;
            currentSettings.trackAlpha = msg.trackAlpha ?? currentSettings.trackAlpha;
            currentSettings.showTrackBorders = msg.showTrackBorders ?? currentSettings.showTrackBorders;
            if (msg.trackThicknessPercent != null) {
                currentSettings.trackThickness = Math.max(16, Math.min(width, height) * (msg.trackThicknessPercent / 100));
            }
            currentSettings.enableCollisions = msg.enableCollisions ?? true;
            currentSettings.enableDrafting = msg.enableDrafting ?? true;
            currentSettings.enableDebris = msg.enableDebris ?? true;
            currentSettings.enableRubberBanding = msg.enableRubberBanding ?? true;
            currentSettings.rubberBandStrength = msg.rubberBandStrength ?? 0.25;
            currentSettings.enableVehicleDamage = msg.enableVehicleDamage ?? currentSettings.enableVehicleDamage ?? false;
            currentSettings.enableItemPickups = msg.enableItemPickups ?? currentSettings.enableItemPickups ?? false;
            currentSettings.startingHealth = msg.startingHealth ?? currentSettings.startingHealth ?? 100;
            currentSettings.ramDamageMultiplier = msg.ramDamageMultiplier ?? currentSettings.ramDamageMultiplier ?? 1.0;
            currentSettings.derbyKnockoutPoints = msg.derbyKnockoutPoints ?? currentSettings.derbyKnockoutPoints ?? 50;
            detachedParts = [];

            currentHeatInfo = {
                title: msg.heatTitle || '',
                heatNumber: msg.heatNumber || 1,
                totalHeats: msg.totalHeats || 1,
                isFinal: msg.isFinal !== false
            };
            timeTrialGhost = null;

            // Generate track hazards
            trackHazards = generateHazards(msg.debrisDensity || 6, currentSettings.trackType, currentSettings.stripPosition);

            // Populate racers if provided
            if (msg.racers && msg.racers.length > 0) {
                const isDerby = (msg.gameMode || currentSettings.gameMode) === 'DestructionDerby';
                const totalRacers = msg.racers.length;
                activeRacers = msg.racers.map((r, idx) => {
                    const dir = isDerby ? (idx % 2 === 0 ? 1 : -1) : 1;
                    const startProg = isDerby ? (idx / totalRacers) : (-0.015 * Math.floor(idx / 2));
                    const initialLane = (idx % 2 === 0) ? -8 : 8;
                    return {
                        dropToken: r.dropToken,
                        username: r.username,
                        displayName: r.displayName,
                        userColor: r.userColor,
                        emoteUrl: r.emoteUrl,
                        progress: startProg,
                        dir: dir,
                        laneOffset: initialLane,
                        targetLaneOffset: initialLane,
                        laneChangeTimer: isDerby ? (0.6 + Math.random() * 0.8) : (1.5 + Math.random() * 2.5),
                        steerAngle: 0,
                        driftAngle: 0,
                        spinAngle: 0,
                        finished: false,
                        wrecks: 0,
                        seed: Math.random() * 200,
                        targetLane: 0,
                        draftCharge: 0,
                        isBoosting: false,
                        boostTimer: 0,
                        hp: currentSettings.startingHealth || 100,
                        maxHp: currentSettings.startingHealth || 100,
                        isTotaled: false,
                        kills: 0,
                        shieldTimer: 0,
                        hitCooldown: 0,
                        lostFrontBumper: false,
                        lostSpoiler: false,
                        lostWheel: false,
                        killerToken: null
                    };
                });
            }

            finishedRacers = [];
            nextFrontPlacement = 1;
            nextBackPlacement = activeRacers.length;
            eliminatedLaps.clear();
            eliminationBanner = null;
            gameState = 'countdown';
            countdownStep = 3;
            countdownTimer = 1000;
            targetOverlayAlpha = 1.0;
            playSound('countdown_beep');
        } else if (msg.type === 'timetrial_start') {
            gameState = 'racing';
            targetOverlayAlpha = 1.0;
            currentTheme = msg.theme;
            currentSounds = msg.sounds;
            if (msg.messages) currentMessages = msg.messages;
            if (soundManager && msg.sounds) soundManager.updateBundle(msg.sounds);
            currentSettings.trackType = msg.trackType || 'Perimeter';
            currentSettings.stripPosition = msg.stripPosition || 'Bottom';
            currentSettings.totalLaps = 1;
            currentSettings.gameMode = 'TimeTrial';
            currentSettings.baseSpeed = msg.baseSpeed ?? 0.5;
            currentSettings.trackAlpha = msg.trackAlpha ?? currentSettings.trackAlpha;
            currentSettings.showTrackBorders = msg.showTrackBorders ?? currentSettings.showTrackBorders;
            if (msg.trackThicknessPercent != null) {
                currentSettings.trackThickness = Math.max(16, Math.min(width, height) * (msg.trackThicknessPercent / 100));
            }
            currentSettings.enableCollisions = false;
            currentSettings.enableDrafting = false;
            currentSettings.enableDebris = msg.enableDebris ?? true;
            currentSettings.enableRubberBanding = false;
            currentHeatInfo = null;

            trackHazards = generateHazards(msg.debrisDensity || 5, currentSettings.trackType, currentSettings.stripPosition);

            activeRacers = [{
                dropToken: msg.dropToken,
                username: msg.username,
                displayName: msg.displayName,
                userColor: msg.userColor,
                emoteUrl: msg.emoteUrl,
                progress: 0.0,
                laneOffset: 0,
                driftAngle: 0,
                spinAngle: 0,
                finished: false,
                wrecks: 0
            }];

            const savedRecord = loadBestLap(currentSettings.trackType);
            if (savedRecord && savedRecord.timeMs) {
                timeTrialGhost = {
                    progress: 0.0,
                    targetLapMs: savedRecord.timeMs,
                    racerName: savedRecord.racerName,
                    isPacer: false
                };
            } else {
                timeTrialGhost = {
                    progress: 0.0,
                    targetLapMs: null,
                    speed: (0.28 * currentSettings.baseSpeed),
                    racerName: 'Benchmark',
                    isPacer: true
                };
            }

            finishedRacers = [];
            nextFrontPlacement = 1;
            nextBackPlacement = activeRacers.length;
            eliminatedLaps.clear();
            eliminationBanner = null;
            raceStartTime = performance.now();
            playSound('countdown_go');
        } else if (msg.type === 'race_end') {
            if (gameState !== 'podium') {
                targetOverlayAlpha = 0.0;
                gameState = 'idle';
            }
        }
    }

    window.addEventListener('drop_game_ws_message', function (e) {
        if (e && e.detail) handleWsMessage(e.detail);
    });

})(typeof window !== 'undefined' ? window : this);
