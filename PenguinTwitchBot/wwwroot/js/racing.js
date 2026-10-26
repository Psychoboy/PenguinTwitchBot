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
    const audioEngine = (typeof DropGamesAudio !== 'undefined' && DropGamesAudio.createAudioEngine)
        ? DropGamesAudio.createAudioEngine() : null;
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
            fields: ['dropToken', 'racePlacement', 'lapTimeSeconds', 'wreckCount', 'score', 'isWin', 'racingMode', 'lapsCompleted'],
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
        rubberBandStrength: 0.25
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
    let trackHazards = [];
    let particles = [];
    let raceStartTime = 0;
    let raceElapsedTime = 0;
    let countdownStep = 0; // 3, 2, 1, GO
    let countdownTimer = 0;
    let podiumTimer = 0;

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
                if (type === 'start' || type === 'countdown_go') {
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

            // 3. Fallback to procedural Web Audio engine synthesizer
            if (!audioEngine) return;
            const volume = (soundDto && typeof soundDto.volume === 'number')
                ? Math.max(0, Math.min(1, soundDto.volume / 100))
                : 0.6;

            if (type === 'countdown_beep' && audioEngine.playRaceCountdownBeep) {
                audioEngine.playRaceCountdownBeep(false, volume);
            } else if (type === 'countdown_go' && audioEngine.playRaceCountdownBeep) {
                audioEngine.playRaceCountdownBeep(true, volume);
            } else if ((type === 'bump' || type === 'crash') && audioEngine.playCarBump) {
                audioEngine.playCarBump(volume);
            } else if (type === 'screech' && audioEngine.playTireScreech) {
                audioEngine.playTireScreech(volume);
            } else if (type === 'boost' && audioEngine.playBoostPad) {
                audioEngine.playBoostPad(volume);
            } else if (type === 'win' && audioEngine.playRaceWin) {
                audioEngine.playRaceWin(volume);
            } else if (type === 'end' && audioEngine.playRaceWin) {
                audioEngine.playRaceWin(volume);
            } else if (type === 'engine' && audioEngine.playEngineRev) {
                audioEngine.playEngineRev(volume);
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

    // Spawns debris and boost pads along the track
    function generateHazards(density, trackType, stripPos) {
        const list = [];
        if (density <= 0) return list;
        const count = Math.min(16, density);
        for (let i = 0; i < count; i++) {
            const t = 0.08 + (i + 0.5) / count * 0.84 + (Math.random() * 0.04 - 0.02);
            const types = ['oil', 'cone', 'boost', 'cone'];
            const type = types[i % types.length];
            const laneOffset = (Math.random() * 2 - 1) * (currentSettings.trackThickness * 0.32);
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

    // ================= DRAWING UTILITIES =================

    function drawTrack() {
        const thickness = currentSettings.trackThickness || 44;
        const samples = 140;
        const points = [];
        for (let i = 0; i <= samples; i++) {
            points.push(getTrackPoint(i / samples, currentSettings.trackType, currentSettings.stripPosition));
        }

        ctx.save();
        ctx.globalAlpha = (currentSettings.trackAlpha || 0.55) * overlayAlpha;

        // 1. Asphalt Road Surface
        ctx.beginPath();
        points.forEach((p, idx) => {
            if (idx === 0) ctx.moveTo(p.x, p.y);
            else ctx.lineTo(p.x, p.y);
        });
        ctx.lineWidth = thickness;
        ctx.strokeStyle = '#22272e';
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.stroke();

        // 2. Curbs / Rumble Strips (Outer & Inner edges)
        if (currentSettings.showTrackBorders) {
            const curbThick = 4;
            const pColor = (currentTheme && currentTheme.primaryColor) || '#FF5252';
            const sColor = '#FFFFFF';

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
        ctx.setLineDash([8, 12]);
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.4)';
        ctx.stroke();
        ctx.setLineDash([]);

        // 4. Start / Finish Line Banner
        const finishPt = getTrackPoint(0.0, currentSettings.trackType, currentSettings.stripPosition);
        ctx.save();
        ctx.translate(finishPt.x, finishPt.y);
        ctx.rotate(finishPt.angle);

        // Checkered line strip
        const checkW = 12;
        const checkH = thickness;
        const sq = 4;
        for (let r = -checkH / 2; r < checkH / 2; r += sq) {
            for (let c = -checkW / 2; c < checkW / 2; c += sq) {
                const isBlack = (Math.floor(r / sq) + Math.floor(c / sq)) % 2 === 0;
                ctx.fillStyle = isBlack ? '#000000' : '#FFFFFF';
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

            if (h.type === 'oil') {
                // Oil Slick / Ice Patch
                ctx.fillStyle = (currentTheme && currentTheme.seasonPreset === 'winter') ? 'rgba(180, 230, 255, 0.85)' : 'rgba(20, 20, 25, 0.85)';
                ctx.beginPath();
                ctx.ellipse(0, 0, 15, 9, 0.3, 0, Math.PI * 2);
                ctx.fill();
                ctx.lineWidth = 1.5;
                ctx.strokeStyle = (currentTheme && currentTheme.seasonPreset === 'winter') ? '#FFFFFF' : '#3d3d44';
                ctx.stroke();
            } else if (h.type === 'cone') {
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
            } else if (h.type === 'boost') {
                // Turbo Boost Arrow
                ctx.rotate(pt.angle);
                ctx.fillStyle = '#00E676';
                ctx.shadowColor = '#00E676';
                ctx.shadowBlur = 8;
                ctx.beginPath();
                ctx.moveTo(8, 0);
                ctx.lineTo(-4, -8);
                ctx.lineTo(-4, -3);
                ctx.lineTo(-10, -3);
                ctx.lineTo(-10, 3);
                ctx.lineTo(-4, 3);
                ctx.lineTo(-4, 8);
                ctx.closePath();
                ctx.fill();
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

        ctx.save();
        ctx.translate(vx, vy);
        ctx.rotate(pt.angle + racer.spinAngle + racer.driftAngle);

        const chassisLen = 34;
        const chassisWid = 20;

        // Exhaust Particles
        if (racer.isBoosting) {
            ctx.fillStyle = '#00E5FF';
            ctx.shadowColor = '#00E5FF';
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

        // 1. Kart Chassis
        const primaryColor = racer.userColor || '#FFD166';
        ctx.fillStyle = primaryColor;
        ctx.shadowColor = 'rgba(0,0,0,0.4)';
        ctx.shadowBlur = 6;

        ctx.beginPath();
        ctx.roundRect(-chassisLen / 2, -chassisWid / 2, chassisLen, chassisWid, [6, 10, 10, 6]);
        ctx.fill();
        ctx.shadowBlur = 0;

        // Front bumper
        ctx.fillStyle = '#1e232a';
        ctx.fillRect(chassisLen / 2 - 4, -chassisWid / 2 - 2, 5, chassisWid + 4);

        // Rear spoiler
        ctx.fillStyle = '#1e232a';
        ctx.fillRect(-chassisLen / 2 - 2, -chassisWid / 2 - 3, 4, chassisWid + 6);

        // 4 Wheels
        ctx.fillStyle = '#111315';
        const wheelLen = 8;
        const wheelWid = 4;
        [-chassisLen / 2 + 5, chassisLen / 2 - 10].forEach(wx => {
            [-chassisWid / 2 - 2, chassisWid / 2 - 2].forEach(wy => {
                ctx.fillRect(wx, wy, wheelLen, wheelWid);
            });
        });

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

        // 3. Driver Nametag & Position Badge
        ctx.save();
        ctx.translate(vx, vy - 24);
        ctx.font = 'bold 12px "Fredoka", "Rubik", sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';

        const name = racer.displayName || racer.username || 'Driver';
        const textWidth = ctx.measureText(name).width;

        ctx.fillStyle = 'rgba(18, 22, 28, 0.78)';
        ctx.beginPath();
        ctx.roundRect(-textWidth / 2 - 6, -9, textWidth + 12, 18, 5);
        ctx.fill();

        ctx.fillStyle = racer.userColor || '#FFFFFF';
        ctx.fillText(name, 0, 0);
        ctx.restore();
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

        ctx.strokeStyle = (currentTheme && currentTheme.primaryColor) || '#FF5252';
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
        ctx.strokeStyle = countdownStep === 0 ? '#00E676' : '#FF5252';
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

        // Stopwatch in top right
        const timeSec = (raceElapsedTime / 1000).toFixed(2);
        ctx.fillStyle = 'rgba(18, 22, 28, 0.78)';
        ctx.beginPath();
        ctx.roundRect(width - 150, 20, 130, 42, 6);
        ctx.fill();
        ctx.fillStyle = '#00E5FF';
        ctx.font = 'bold 18px monospace';
        ctx.textAlign = 'center';
        ctx.fillText(`⏱️ ${timeSec}s`, width - 85, 47);

        // Leaderboard sidebar
        const sorted = [...activeRacers].sort((a, b) => b.progress - a.progress);
        const topN = sorted.slice(0, 5);
        let sy = 75;
        topN.forEach((r, idx) => {
            ctx.fillStyle = 'rgba(18, 22, 28, 0.65)';
            ctx.beginPath();
            ctx.roundRect(width - 150, sy, 130, 24, 4);
            ctx.fill();
            ctx.fillStyle = idx === 0 ? '#FFD166' : '#FFFFFF';
            ctx.font = 'bold 11px sans-serif';
            ctx.textAlign = 'left';
            const name = r.displayName || r.username;
            ctx.fillText(`${idx + 1}. ${name.substring(0, 10)}`, width - 142, sy + 16);
            sy += 28;
        });

        ctx.restore();
    }

    // Draws Finish Podium
    function drawPodiumUI() {
        if (gameState !== 'podium' || finishedRacers.length === 0) return;
        ctx.save();
        ctx.globalAlpha = overlayAlpha;

        if (currentSettings.gameMode === 'TimeTrial') {
            const r = finishedRacers[0];
            const pw = Math.min(460, width * 0.8);
            const ph = 140;
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
            ctx.fillText(ttTitle, width / 2, py + 30);

            ctx.fillStyle = r.userColor || '#FFD166';
            ctx.font = 'bold 16px "Fredoka", sans-serif';
            ctx.fillText(r.displayName || r.username, width / 2, py + 58);

            ctx.fillStyle = '#00E5FF';
            ctx.font = 'bold 24px monospace';
            ctx.fillText(`${(r.lapTime || 0).toFixed(2)}s`, width / 2, py + 88);

            if (r.isNewRecord) {
                ctx.fillStyle = '#00E676';
                ctx.font = 'bold 14px "Fredoka", sans-serif';
                const newRec = (currentMessages && (currentMessages.overlayNewRecord || currentMessages.OverlayNewRecord)) || '🏆 NEW TRACK RECORD! 🏆';
                ctx.fillText(newRec, width / 2, py + 116);
            } else if (r.prevBestMs) {
                const diff = (r.lapTime - (r.prevBestMs / 1000)).toFixed(2);
                ctx.fillStyle = '#FF8A80';
                ctx.font = '13px "Fredoka", sans-serif';
                ctx.fillText(`Track Record: ${(r.prevBestMs / 1000).toFixed(2)}s (+${diff}s)`, width / 2, py + 116);
            }

            ctx.restore();
            return;
        }

        // If this was a preliminary heat (not final)
        if (currentHeatInfo && !currentHeatInfo.isFinal) {
            const pw = Math.min(500, width * 0.8);
            const ph = 160;
            const px = (width - pw) / 2;
            const py = height / 2 - ph / 2;

            ctx.fillStyle = 'rgba(18, 22, 28, 0.92)';
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
            ctx.fillText(`🏁 ${currentHeatInfo.title ? currentHeatInfo.title.toUpperCase() : 'HEAT'} RESULTS`, width / 2, py + 32);

            const displayCount = Math.min(4, finishedRacers.length);
            finishedRacers.slice(0, displayCount).forEach((r, idx) => {
                const rx = px + (pw / displayCount) * idx + (pw / (displayCount * 2));
                ctx.fillStyle = idx < 2 ? '#00E676' : '#FFD166';
                ctx.font = 'bold 12px "Fredoka", sans-serif';
                ctx.fillText(idx < 2 ? 'QUALIFIED 🟢' : `${idx + 1}th`, rx, py + 66);

                ctx.fillStyle = r.userColor || '#FFFFFF';
                ctx.font = '13px "Fredoka", sans-serif';
                ctx.fillText((r.displayName || r.username).substring(0, 9), rx, py + 92);

                ctx.fillStyle = '#00E5FF';
                ctx.font = '11px monospace';
                ctx.fillText(`${(r.lapTime || 0).toFixed(2)}s`, rx, py + 116);
            });

            ctx.restore();
            return;
        }

        // Championship Grand Final Podium
        const pw = Math.min(500, width * 0.8);
        const ph = 160;
        const px = (width - pw) / 2;
        const py = height / 2 - ph / 2;

        ctx.fillStyle = 'rgba(18, 22, 28, 0.92)';
        ctx.shadowColor = 'rgba(0,0,0,0.6)';
        ctx.shadowBlur = 18;
        ctx.beginPath();
        ctx.roundRect(px, py, pw, ph, 12);
        ctx.fill();
        ctx.shadowBlur = 0;

        ctx.strokeStyle = '#FFD166';
        ctx.lineWidth = 3;
        ctx.stroke();

        ctx.fillStyle = '#FFD166';
        ctx.font = 'bold 22px "Fredoka", "Rubik", sans-serif';
        ctx.textAlign = 'center';
        const podTitle = (currentMessages && (currentMessages.overlayPodiumTitle || currentMessages.OverlayPodiumTitle)) || '🏆 CHAMPIONSHIP FINAL PODIUM 🏆';
        ctx.fillText(podTitle, width / 2, py + 34);

        const medals = ['🥇 1st', '🥈 2nd', '🥉 3rd'];
        finishedRacers.slice(0, 3).forEach((r, idx) => {
            const rx = px + (pw / 3) * idx + (pw / 6);
            ctx.fillStyle = '#FFFFFF';
            ctx.font = 'bold 14px "Fredoka", sans-serif';
            ctx.fillText(medals[idx], rx, py + 72);
            ctx.fillStyle = r.userColor || '#FFD166';
            ctx.font = '13px "Fredoka", sans-serif';
            ctx.fillText(r.displayName || r.username, rx, py + 98);
            ctx.fillStyle = '#00E5FF';
            ctx.font = '11px monospace';
            ctx.fillText(`${(r.lapTime || 0).toFixed(2)}s`, rx, py + 120);
        });

        ctx.restore();
    }

    // ================= MAIN GAME LOOP =================
    let lastTime = performance.now();

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

        // Update Ghost Car in Time Trial
        if (timeTrialGhost) {
            if (timeTrialGhost.targetLapMs) {
                timeTrialGhost.progress += (1.0 / (timeTrialGhost.targetLapMs / 1000)) * dtSec;
            } else if (timeTrialGhost.speed) {
                timeTrialGhost.progress += timeTrialGhost.speed * dtSec;
            }
        }

        // Leader progress for rubber banding
        let maxProgress = 0;
        activeRacers.forEach(r => { if (r.progress > maxProgress) maxProgress = r.progress; });

        const maxLaneOffset = (currentSettings.trackThickness || 44) * 0.35;

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

            // Ensure cars stay strictly on track surface (clamp to curb boundaries, no forced center magnetic pull)
            if (r.laneOffset > maxLaneOffset) {
                r.laneOffset = maxLaneOffset;
            } else if (r.laneOffset < -maxLaneOffset) {
                r.laneOffset = -maxLaneOffset;
            }

            // Move racer along spline
            r.progress += (speed * dtSec);

            // Check lap completion and race finish
            if (r.progress >= totalLaps) {
                r.finished = true;
                r.lapTime = raceElapsedTime / 1000;
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

                // Report completion back to server (sole source of truth)
                reportRacerCompletion(r, finishedRacers.length);

                // Elimination mode knockout check
                if (currentSettings.gameMode === 'Elimination' && activeRacers.filter(x => !x.finished).length === 1) {
                    const lastRacer = activeRacers.filter(x => !x.finished)[0];
                    lastRacer.finished = true;
                    finishedRacers.push(lastRacer);
                    reportRacerCompletion(lastRacer, finishedRacers.length);
                }

                if (finishedRacers.length === 1) {
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

            // 2. Drafting & Slingshot Overtaking
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
                            const passLane = (other.laneOffset >= 0) ? -maxLaneOffset * 0.75 : maxLaneOffset * 0.75;
                            r.laneOffset = passLane;
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

            // 3. Car-to-Car Bumping Collisions with Recoil & Return
            if (currentSettings.enableCollisions && !r.isWrecked) {
                for (let j = i + 1; j < activeRacers.length; j++) {
                    const other = activeRacers[j];
                    if (other.finished || other.isWrecked) continue;
                    if (Math.abs(other.progress - r.progress) < 0.016) {
                        if (Math.abs(other.laneOffset - r.laneOffset) < 12) {
                            // Gentle deflection push
                            const push = (r.laneOffset <= other.laneOffset) ? -4 : 4;
                            r.laneOffset = Math.max(-maxLaneOffset, Math.min(maxLaneOffset, r.laneOffset + push));
                            other.laneOffset = Math.max(-maxLaneOffset, Math.min(maxLaneOffset, other.laneOffset - push));

                            // Comic drift angle visual
                            r.driftAngle = (push > 0) ? 0.22 : -0.22;
                            other.driftAngle = (push > 0) ? -0.22 : 0.22;

                            playSound('bump');
                            break;
                        }
                    }
                }
                if (r.driftAngle) {
                    r.driftAngle *= Math.max(0, 1 - dtSec * 6);
                }
            }

            // 4. Track Hazards Collisions with Respawning
            if (currentSettings.enableDebris && !r.isWrecked) {
                const normProg = r.progress % 1.0;
                trackHazards.forEach(h => {
                    if (!h.active) return;
                    if (Math.abs(normProg - h.t) < 0.018 && Math.abs(r.laneOffset - h.laneOffset) < 13) {
                        h.active = false;
                        h.respawnTimer = 4.5 + Math.random() * 3.0; // Automatically respawns in 4.5-7.5s!
                        if (h.type === 'oil') {
                            r.isWrecked = true;
                            r.wreckTimer = 1.4;
                            r.wrecks = (r.wrecks || 0) + 1;
                            playSound('screech');
                        } else if (h.type === 'cone') {
                            r.progress -= 0.022;
                            r.wrecks = (r.wrecks || 0) + 1;
                            playSound('bump');
                        } else if (h.type === 'boost') {
                            r.isBoosting = true;
                            r.boostTimer = 1.5;
                            playSound('boost');
                        }
                    }
                });
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
            lapsCompleted: currentSettings.totalLaps || 2
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
            drawGhostCar();

            // Render all active racers
            activeRacers.forEach(r => drawVehicle(r));

            // Render particles
            for (let i = particles.length - 1; i >= 0; i--) {
                const p = particles[i];
                p.x += (p.vx || 0);
                p.y += (p.vy || 0);
                p.alpha = (p.alpha || 1.0) - 0.02;
                if (p.alpha <= 0) {
                    particles.splice(i, 1);
                } else {
                    ctx.save();
                    ctx.globalAlpha = p.alpha * overlayAlpha;
                    ctx.fillStyle = p.color || '#FFD166';
                    ctx.beginPath();
                    ctx.arc(p.x, p.y, p.size || 3, 0, Math.PI * 2);
                    ctx.fill();
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

            lobbyTotalMs = (msg.lobbyDurationSeconds ?? 60) * 1000;
            lobbyRemainingMs = lobbyTotalMs;
            lobbyRacers = [];
            activeRacers = [];
            finishedRacers = [];
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
                    driftAngle: 0,
                    spinAngle: 0,
                    finished: false,
                    wrecks: 0
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
                activeRacers = msg.racers.map((r, idx) => ({
                    dropToken: r.dropToken,
                    username: r.username,
                    displayName: r.displayName,
                    userColor: r.userColor,
                    emoteUrl: r.emoteUrl,
                    progress: -0.015 * Math.floor(idx / 2),
                    laneOffset: (idx % 2 === 0) ? -8 : 8,
                    driftAngle: 0,
                    spinAngle: 0,
                    finished: false,
                    wrecks: 0,
                    seed: Math.random() * 200,
                    targetLane: 0,
                    draftCharge: 0,
                    isBoosting: false,
                    boostTimer: 0
                }));
            }

            finishedRacers = [];
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
