(function (global) {
    'use strict';
    const clamp = (value, minimum, maximum) => Math.max(minimum, Math.min(maximum, value));
    const ease = value => value * value * (3 - 2 * value);
    const mix = (start, end, progress) => start + (end - start) * ease(clamp(progress, 0, 1));

    function safeBounds(width, leftGap, rightGap) {
        const left = Math.max(0, Number(leftGap) || 0);
        const right = Math.max(0, Number(rightGap) || 0);
        const requested = left + right;
        const scale = requested > 0 ? Math.min(1, Math.max(0, width - 400) / requested) : 1;
        return { left: left * scale, right: right * scale };
    }

    function seededUnit(seed) {
        let hash = 2166136261;
        for (const character of String(seed)) {
            hash ^= character.charCodeAt(0);
            hash = Math.imul(hash, 16777619);
        }
        return (hash >>> 0) / 4294967296;
    }

    function shouldSlip(seed, chancePercent) {
        return clamp(Number(chancePercent) || 0, 0, 100) > seededUnit(seed) * 100;
    }

    function suspenseSequence(seed, entryCount, maximumDrops = 3) {
        const maximum = Math.max(0, Math.min(5, Math.trunc(Number(maximumDrops) || 0)));
        if (entryCount < 1 || maximum === 0) return [];
        const dropCount = Math.min(entryCount, 1 + Math.floor(seededUnit(`${seed}:drops`) * maximum));
        const order = Array.from({ length: entryCount }, (_, index) => index);
        for (let index = order.length - 1; index > 0; index--) {
            const target = Math.floor(seededUnit(`${seed}:shuffle:${index}`) * (index + 1));
            [order[index], order[target]] = [order[target], order[index]];
        }
        return order.slice(0, dropCount);
    }

    function distanceBetween(start, end) { return Math.hypot(end.x - start.x, end.y - start.y); }
    function atProgress(start, end, progress) {
        const amount = clamp(progress, 0, 1);
        return { x: start.x + (end.x - start.x) * amount, y: start.y + (end.y - start.y) * amount };
    }

    function buildSuspensePlan(winner, feintPrizes, width, leftGap, rightGap, suspenseLiftY) {
        const bounds = safeBounds(width, leftGap, rightGap);
        const center = (bounds.left + width - bounds.right) / 2;
        const speed = 300;
        const extras = feintPrizes.slice(1);
        const cycles = [];
        let time = 9.25;
        let origin = { x: center, y: suspenseLiftY };
        for (const ball of extras) {
            const target = { x: ball.x, y: ball.y - 66 };
            const liftY = Math.max(390, (ball.y - 66 + 284) / 2);
            const lifted = { x: center, y: liftY };
            const approachEnd = time + Math.max(0.25, distanceBetween(origin, target) / speed);
            const catchEnd = approachEnd + 0.3;
            const liftEnd = catchEnd + Math.max(0.25, distanceBetween(target, lifted) / speed);
            const end = liftEnd + 0.5;
            cycles.push({ ball, origin, target, lifted, start: time, approachEnd, catchEnd, liftEnd, end });
            time = end;
            origin = lifted;
        }
        const searchEnd = time + (extras.length === 0 ? 1.1 : 0);
        const winnerTarget = { x: winner.x, y: winner.y - 66 };
        const winnerApproachEnd = searchEnd + Math.max(0.25, distanceBetween(origin, winnerTarget) / speed);
        const lockTime = winnerApproachEnd + 0.3;
        const animationEnd = lockTime + Math.max(0.5, distanceBetween(winnerTarget, { x: center, y: 284 }) / speed);
        return { cycles, feintEnd: time, searchEnd, winnerApproachEnd, lockTime, animationEnd, winnerTarget, finalStart: origin, center };
    }

    function animationEndFor(tease, reducedMotion, slipChancePercent, grabId, feintPrizes, winner, width = 1920, leftGap = 0, rightGap = 0) {
        const slip = tease && !reducedMotion && feintPrizes.length > 0 && shouldSlip(grabId, slipChancePercent);
            if (!slip) return 15; 
        const bounds = safeBounds(width, leftGap, rightGap);
        const liftY = clamp((feintPrizes[0].y - 66 + 284) / 2, 390, 650);
        return buildSuspensePlan(winner, feintPrizes, width, leftGap, rightGap, liftY).animationEnd;
    }

    function effectiveDurationFor(baseDuration, tease, reducedMotion, slipChancePercent, grabId, feintPrizes, winner, width = 1920, leftGap = 0, rightGap = 0) {
        const slip = tease && !reducedMotion && feintPrizes.length > 0 && shouldSlip(grabId, slipChancePercent);
            if (!slip) return baseDuration; 
        const animationEnd = animationEndFor(tease, reducedMotion, slipChancePercent, grabId, feintPrizes, winner, width, leftGap, rightGap);
        return baseDuration * animationEnd / 15;
    }

    function variedColor(color, index) {
        const match = /^#([0-9a-f]{6})$/i.exec(String(color));
        if (!match || index % 8 === 0) return color;
        const hex = match[1];
        const red = parseInt(hex.slice(0, 2), 16) / 255;
        const green = parseInt(hex.slice(2, 4), 16) / 255;
        const blue = parseInt(hex.slice(4, 6), 16) / 255;
        const maximum = Math.max(red, green, blue);
        const minimum = Math.min(red, green, blue);
        const lightness = (maximum + minimum) / 2;
        const delta = maximum - minimum;
        let hue = 0;
        let saturation = 0;
        if (delta) {
            saturation = delta / (1 - Math.abs(2 * lightness - 1));
            if (maximum === red) hue = ((green - blue) / delta) % 6;
            else if (maximum === green) hue = (blue - red) / delta + 2;
            else hue = (red - green) / delta + 4;
            hue *= 60;
        }
        const offsets = [0, 38, -31, 70, -61, 112, -103, 180];
        const variedHue = (hue + offsets[index % offsets.length] + 360) % 360;
        const variedSaturation = Math.max(62, saturation * 100);
        const variedLightness = clamp(lightness * 100 + (index % 3 - 1) * 6, 34, 72);
        return `hsl(${variedHue} ${variedSaturation}% ${variedLightness}%)`;
    }

    function layout(entries, width = 1920, leftGap = 0, rightGap = 0) {
        const count = entries.length;
        const bounds = safeBounds(width, leftGap, rightGap);
        const contentLeft = bounds.left + 200;
        const contentRight = Math.max(contentLeft, width - bounds.right - 200);
        const usableWidth = Math.max(1, contentRight - contentLeft);
        return entries.map((entry, index) => ({ ...entry,
            radius: count < 10 ? 89 : 68,
            color: variedColor(entry.color, index),
            x: count === 1 ? (contentLeft + contentRight) / 2 : contentLeft + ((index * 263) % usableWidth),
            y: Math.min(count < 10 ? 905 : 926, 914 - Math.floor(index / Math.max(8, Math.ceil(count / 4))) * 67 + Math.sin(index * 4.1) * 18)
        }));
    }

    function frameAt(seconds, prize, tease, reducedMotion, speed, width = 1920, leftGap = 0, rightGap = 0,
        slipChancePercent = 20, grabId = '', feintPrizes = [prize]) {
        const bounds = safeBounds(width, leftGap, rightGap);
        const left = bounds.left + 200;
        const right = Math.max(left, width - bounds.right - 200);
        const center = (left + right) / 2;
        const slip = tease && !reducedMotion && feintPrizes.length > 0 && shouldSlip(grabId, slipChancePercent);
        const extras = feintPrizes.slice(1);
        const suspenseStart = 8.8;
        const firstDropEnd = 9.25;
        const initialPrize = slip && feintPrizes.length ? feintPrizes[0] : prize;
        const suspenseLiftY = clamp((initialPrize.y - 66 + 284) / 2, 390, 650);
        const plan = slip ? buildSuspensePlan(prize, feintPrizes, width, leftGap, rightGap, suspenseLiftY) : null;
        const feintsEnd = plan?.feintEnd ?? 15;
        const winnerLockTime = plan?.lockTime ?? 15;
        const animationEnd = plan?.animationEnd ?? 15;
        const time = clamp(seconds, 0, animationEnd);
        let phase = time >= animationEnd ? 'Revealing' : time >= 6.5 ? 'Ascending' : time >= 6 ? 'Closing' : time >= 3 ? 'Descending' : 'Cruising';
        const approach = clamp(initialPrize.x - 120, left, right);
        let x = mix(center, approach, time / 3) + (time < 3 && !reducedMotion ? Math.sin(time * 4 * (speed || 1)) * 55 * Math.sin(time / 3 * Math.PI) : 0);
        let clawY = 275;
        if (time >= 3 && time < 6.5) {
            x = mix(approach, initialPrize.x, (time - 3) / 3.5);
            clawY = mix(275, initialPrize.y - 66, (time - 3) / 3);
        } else if (time >= 6.5 && time < suspenseStart) {
            const progress = (time - 6.5) / (suspenseStart - 6.5);
            x = mix(initialPrize.x, center, progress);
            clawY = mix(initialPrize.y - 66, suspenseLiftY, progress);
        } else if (time >= suspenseStart) {
            x = center;
            clawY = suspenseLiftY;
        }
        let captured = time >= 6.5 && (!slip || time < suspenseStart);
        let heldPrize = captured ? initialPrize : null;
        let decoyCaptured = false;
        let decoy = initialPrize;
        let decoyX = center;
        let decoyY = clawY + 66;
        let grip = mix(0, 1, (time - 6) / 0.5);
        const dropped = [];
        if (slip && time >= firstDropEnd) dropped.push({ prize: initialPrize, x: center, y: initialPrize.y });
        if (slip && time >= suspenseStart && time < firstDropEnd) {
            phase = 'InitialDrop';
            captured = false;
            heldPrize = null;
            const progress = (time - suspenseStart) / (firstDropEnd - suspenseStart);
            decoyCaptured = true;
            decoyX = center;
            decoyY = mix(suspenseLiftY + 66, initialPrize.y, progress);
            grip = mix(1, 0, progress);
        }
        if (slip && time >= firstDropEnd && time < feintsEnd && extras.length === 0) {
            phase = 'Searching';
            heldPrize = null;
            const progress = (time - firstDropEnd) / (plan.searchEnd - firstDropEnd);
            x = center + Math.sin(progress * Math.PI * 4) * (right - left) * 0.18;
            clawY = suspenseLiftY;
            grip = 0;
        }
        if (slip && time >= firstDropEnd && time < feintsEnd && extras.length > 0) {
            heldPrize = null;
            const sequenceIndex = plan.cycles.findIndex(cycle => time < cycle.end);
            const cycleIndex = sequenceIndex < 0 ? plan.cycles.length - 1 : sequenceIndex;
            const cycle = plan.cycles[cycleIndex];
            for (let index = 0; index < cycleIndex; index++) dropped.push({ prize: extras[index], x: center, y: extras[index].y });
            const ball = cycle.ball;
            const target = cycle.target;
            if (time < cycle.approachEnd) {
                phase = `FeintDrop${cycleIndex}`;
                const position = atProgress(cycle.origin, target, (time - cycle.start) / (cycle.approachEnd - cycle.start));
                x = position.x;
                clawY = position.y;
                grip = 0;
            } else if (time < cycle.catchEnd) {
                phase = `FeintCatch${cycleIndex}`;
                x = target.x;
                clawY = target.y;
                decoyCaptured = time >= cycle.catchEnd - 0.1;
                decoy = ball;
                decoyX = x;
                decoyY = clawY + 66;
                grip = mix(0, 1, (time - cycle.approachEnd) / (cycle.catchEnd - cycle.approachEnd));
            } else if (time < cycle.liftEnd) {
                phase = `FeintLift${cycleIndex}`;
                const position = atProgress(target, cycle.lifted, (time - cycle.catchEnd) / (cycle.liftEnd - cycle.catchEnd));
                x = position.x;
                clawY = position.y;
                decoyCaptured = true;
                decoy = ball;
                decoyX = x;
                decoyY = clawY + 66;
                grip = 1;
            } else {
                phase = `FeintRelease${cycleIndex}`;
                const progress = (time - cycle.liftEnd) / (cycle.end - cycle.liftEnd);
                x = center;
                clawY = cycle.lifted.y;
                decoyCaptured = true;
                decoy = ball;
                decoyX = center;
                decoyY = cycle.lifted.y + 66 + (ball.y - cycle.lifted.y - 66) * progress;
                grip = mix(1, 0, progress);
                if (time >= cycle.end - 0.05) dropped.push({ prize: ball, x: center, y: ball.y });
            }
        }
        if (slip && time >= feintsEnd && time < winnerLockTime) {
            heldPrize = null;
            for (const ball of extras) dropped.push({ prize: ball, x: center, y: ball.y });
            if (extras.length === 0 && time < plan.searchEnd) {
                phase = 'Searching';
                const progress = (time - feintsEnd) / Math.max(0.001, plan.searchEnd - feintsEnd);
                x = center + Math.sin(progress * Math.PI * 4) * (right - left) * 0.18;
                clawY = suspenseLiftY;
                captured = false;
                grip = 0;
            } else if (time < plan.winnerApproachEnd) {
                phase = 'Recatching';
                const progress = (time - plan.searchEnd) / (plan.winnerApproachEnd - plan.searchEnd);
                const position = atProgress(plan.finalStart, plan.winnerTarget, progress);
                x = position.x;
                clawY = position.y;
                captured = false;
                grip = 0;
            } else {
                phase = 'Recatching';
                x = prize.x;
                clawY = prize.y - 66;
                captured = time >= winnerLockTime - 0.3;
                heldPrize = captured ? prize : null;
                grip = mix(0, 1, (time - plan.winnerApproachEnd) / (winnerLockTime - plan.winnerApproachEnd));
            }
        } else if (slip && time >= winnerLockTime && time < animationEnd) {
            phase = 'Retracting';
            const progress = (time - winnerLockTime) / (animationEnd - winnerLockTime);
            const position = atProgress(plan.winnerTarget, { x: center, y: 284 }, progress);
            x = position.x;
            clawY = position.y;
            captured = true;
            heldPrize = prize;
            grip = 1;
        } else if (!slip && time >= 6.5) {
            const progress = (time - 6.5) / (15 - 6.5);
            x = mix(prize.x, center, progress);
            clawY = mix(prize.y - 66, 284, progress);
            captured = true;
            heldPrize = prize;
            grip = 1;
        }
        if (slip && time >= animationEnd) {
            phase = 'Revealing';
            x = center;
            clawY = 284;
            captured = true;
            heldPrize = prize;
            grip = 1;
        }
        const recatch = tease && !slip && !reducedMotion && time > 10 && time < 11 ? Math.sin((time - 10) * Math.PI) * 24 : 0;
        const locked = slip ? time >= winnerLockTime : time >= 15;
        return { phase, x, clawY, captured, heldPrize, decoyCaptured, decoy, decoyX, decoyY, dropped, slip, grip,
            timing: plan, lockTime: slip ? winnerLockTime : 15, animationEnd, locked,
            prizeX: captured ? x + (!reducedMotion ? Math.sin(time * 5) * 12 * Math.max(0, 11 - time) / 4 : 0) : prize.x,
            prizeY: captured ? clawY + 66 + recatch : prize.y, reveal: time >= animationEnd };
    }

    function wrapLabel(text, maximumWidth, maximumLines, measure) {
        const lines = [];
        let line = '';
        for (const word of String(text).trim().split(/\s+/).filter(Boolean)) {
            const candidate = line ? line + ' ' + word : word;
            if (measure(candidate) <= maximumWidth) { line = candidate; continue; }
            if (line) { lines.push(line); line = ''; }
            for (const character of Array.from(word)) {
                if (line && measure(line + character) > maximumWidth) { lines.push(line); line = ''; }
                line += character;
            }
        }
        if (line) lines.push(line);
        if (lines.length > maximumLines) {
            lines.length = maximumLines;
            const ending = Array.from(lines[maximumLines - 1]);
            while (ending.length && measure(ending.join('') + '...') > maximumWidth) ending.pop();
            lines[maximumLines - 1] = ending.join('').trimEnd() + '...';
        }
        return lines;
    }

    global.UfoClawScene = { layout, frameAt, safeBounds, variedColor, shouldSlip, suspenseSequence, animationEndFor, effectiveDurationFor, wrapLabel };
    if (typeof document === 'undefined') return;
    const canvas = document.getElementById('claw-scene');
    if (!canvas) return;
    const overlayKey = global.location ? new URLSearchParams(global.location.search).get('key') || '' : '';
    const context = canvas.getContext('2d');
    let sceneWidth = 1920;
    let layoutDirty = true;
    function resizeCanvas() {
        const viewportWidth = Math.max(1, Number(global.innerWidth) || 1920);
        const viewportHeight = Math.max(1, Number(global.innerHeight) || 1080);
        const pixelRatio = Math.max(1, Number(global.devicePixelRatio) || 1);
        sceneWidth = 1080 * viewportWidth / viewportHeight;
        canvas.width = Math.round(viewportWidth * pixelRatio);
        canvas.height = Math.round(viewportHeight * pixelRatio);
        context.setTransform(canvas.width / sceneWidth, 0, 0, canvas.height / 1080, 0, 0);
        layoutDirty = true;
    }
    resizeCanvas();
    global.addEventListener('resize', resizeCanvas);
    const result = document.getElementById('result');
    const reducedMotion = global.matchMedia('(prefers-reduced-motion: reduce)');
    const images = new Map();
    const voices = new Set();
    const replacements = new Set();
    let pool = [];
    let layoutTarget = [];
    let layoutFromById = new Map();
    let layoutBounds = { left: 0, right: 0 };
    let layoutBoundsFrom = { left: 0, right: 0 };
    let layoutBoundsTarget = { left: 0, right: 0 };
    let layoutWidth = sceneWidth;
    let layoutTransitionStarted = 0;
    let feintIndexes = [];
    let feintPrizes = [];
    let selected = null;
    let state = null;
    let startedAt = null;
    let visible = false;
    let audible = false;
    let lastPhase = '';
    let audioContext;
    let motor;
    let motorAudio;
    let motorGain;
    let soundGeneration = 0;
    let completionAcknowledgedFor = '';
    let completionAttemptFor = '';
    let completionRetryAfter = 0;
    let completionInFlight = false;

    async function acknowledgeAnimation(grabId) {
        if (!overlayKey || !grabId || completionAcknowledgedFor === grabId || completionInFlight) return;
        if (completionAttemptFor === grabId && Date.now() < completionRetryAfter) return;
        completionAttemptFor = grabId;
        completionInFlight = true;
        try {
            const response = await fetch('/api/overlay/ufoclaw-complete', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ grabId, overlayKey })
            });
            if (response.ok || response.status === 401 || response.status === 409) {
                completionAcknowledgedFor = grabId;
                completionAttemptFor = '';
                completionRetryAfter = 0;
            } else {
                completionRetryAfter = Date.now() + 1500;
            }
        } catch (_) {
            completionRetryAfter = Date.now() + 1500;
        } finally {
            completionInFlight = false;
        }
    }

    function updateLayout(now, settings) {
        if (layoutDirty) {
            const widthScale = sceneWidth / layoutWidth;
            const previous = new Map(pool.map(prize => [prize.id, { x: prize.x * widthScale, y: prize.y }]));
            layoutBoundsFrom = { left: layoutBounds.left * widthScale, right: layoutBounds.right * widthScale };
            layoutBoundsTarget = safeBounds(sceneWidth, settings.leftSafeGapPixels, settings.rightSafeGapPixels);
            layoutTarget = layout(state.entries, sceneWidth, settings.leftSafeGapPixels, settings.rightSafeGapPixels);
            layoutFromById = new Map(layoutTarget.map(prize => [prize.id,
                previous.get(prize.id) ?? { x: prize.x, y: 1080 + prize.radius + 24 }]));
            feintIndexes = state.grab ? suspenseSequence(state.grab.id, layoutTarget.length, settings.maxSuspenseDrops) : [];
            layoutTransitionStarted = now;
            layoutWidth = sceneWidth;
            layoutDirty = false;
        }

        const progress = clamp((now - layoutTransitionStarted) / 420, 0, 1);
        if (progress < 1 && layoutFromById.size) {
            pool = layoutTarget.map(prize => {
                const from = layoutFromById.get(prize.id);
                return from ? { ...prize, x: mix(from.x, prize.x, progress), y: mix(from.y, prize.y, progress) } : prize;
            });
        } else {
            pool = layoutTarget;
        }
        layoutBounds = {
            left: mix(layoutBoundsFrom.left, layoutBoundsTarget.left, progress),
            right: mix(layoutBoundsFrom.right, layoutBoundsTarget.right, progress)
        };
        selected = state.grab ? pool[state.grab.winningIndex] : null;
        feintPrizes = feintIndexes.map(index => pool[index]).filter(Boolean);
        return layoutBounds;
    }

    function imageFor(url) {
        if (!url) return null;
        if (!images.has(url)) { const image = new Image(); image.src = url; images.set(url, image); }
        const image = images.get(url);
        return image.complete && image.naturalWidth > 0 ? image : null;
    }

    function drawImageContained(image, x, y, width, height) {
        const scale = Math.min(width / image.naturalWidth, height / image.naturalHeight);
        context.drawImage(image, x - image.naturalWidth * scale / 2, y - image.naturalHeight * scale / 2, image.naturalWidth * scale, image.naturalHeight * scale);
    }

    function stopMotor() {
        soundGeneration++;
        if (motor) { try { motor.stop(); } catch (_) { } motor = null; }
        if (motorAudio) { motorAudio.pause(); motorAudio.currentTime = 0; replacements.delete(motorAudio); motorAudio = null; }
    }

    function silence() {
        stopMotor();
        voices.forEach(voice => { try { voice.stop(); } catch (_) { } });
        voices.clear();
        replacements.forEach(audio => audio.pause());
        replacements.clear();
    }

    function canPlay() { return state && visible && audible && state.settings.soundEnabled && state.settings.volume > 0; }

    function tone(frequency, duration, delay, type) {
        if (!audioContext || !canPlay()) return;
        const oscillator = audioContext.createOscillator();
        const gain = audioContext.createGain();
        const start = audioContext.currentTime + (delay || 0);
        oscillator.type = type || 'sine';
        oscillator.frequency.setValueAtTime(frequency, start);
        oscillator.frequency.exponentialRampToValueAtTime(frequency * 0.7, start + duration);
        gain.gain.setValueAtTime(0.0001, start);
        gain.gain.exponentialRampToValueAtTime(Math.max(0.0001, 0.13 * state.settings.volume), start + 0.015);
        gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
        oscillator.connect(gain).connect(audioContext.destination);
        oscillator.start(start);
        oscillator.stop(start + duration + 0.03);
        voices.add(oscillator);
        oscillator.onended = () => voices.delete(oscillator);
    }

    function sweep(startFrequency, endFrequency, duration, delay, type, peak) {
        if (!audioContext || !canPlay()) return;
        const oscillator = audioContext.createOscillator();
        const gain = audioContext.createGain();
        const start = audioContext.currentTime + (delay || 0);
        oscillator.type = type || 'sine';
        oscillator.frequency.setValueAtTime(Math.max(1, startFrequency), start);
        oscillator.frequency.exponentialRampToValueAtTime(Math.max(1, endFrequency), start + duration);
        gain.gain.setValueAtTime(0.0001, start);
        gain.gain.exponentialRampToValueAtTime(Math.max(0.0001, (peak || 0.12) * state.settings.volume), start + 0.02);
        gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
        oscillator.connect(gain).connect(audioContext.destination);
        oscillator.start(start);
        oscillator.stop(start + duration + 0.03);
        voices.add(oscillator);
        oscillator.onended = () => voices.delete(oscillator);
    }

    function playDropCue() {
        replacement('/media/ufo_claw_drop.wav', () => {
            sweep(680, 150, 0.32, 0, 'sine', 0.16);
            tone(280, 0.1, 0.28, 'triangle');
        });
    }

    function playLiftCue() {
        replacement('/media/ufo_claw_lift.wav', () => {
            sweep(190, 510, 0.55, 0, 'triangle', 0.12);
            tone(740, 0.12, 0.42, 'sine');
        });
    }

    function playCatchCue() {
        replacement('/media/ufo_claw_catch.wav', () => {
            tone(460, 0.09, 0, 'triangle');
            tone(690, 0.11, 0.08, 'triangle');
            tone(920, 0.16, 0.17, 'sine');
        });
    }

    async function enableAudio() {
        if (!canPlay() || motor || motorAudio) return;
        const generation = soundGeneration;
        try {
            const AudioContextClass = global.AudioContext || global.webkitAudioContext;
            if (!AudioContextClass) return;
            audioContext = audioContext || new AudioContextClass();
            await audioContext.resume();
            if (generation !== soundGeneration || !canPlay() || motor || motorAudio || lastPhase === 'Revealing') return;
            motorAudio = new Audio('/media/ufo_claw_motor.wav');
            motorAudio.loop = true;
            motorAudio.volume = state.settings.volume * 0.42;
            replacements.add(motorAudio);
            try {
                await motorAudio.play();
                if (generation !== soundGeneration || !canPlay()) {
                    motorAudio.pause();
                    replacements.delete(motorAudio);
                    motorAudio = null;
                }
                return;
            } catch (_) {
                replacements.delete(motorAudio);
                motorAudio = null;
            }
            motor = audioContext.createOscillator();
            motorGain = audioContext.createGain();
            motor.type = 'triangle'; motor.frequency.value = 68;
            motorGain.gain.value = state.settings.volume * 0.05;
            motor.connect(motorGain).connect(audioContext.destination); motor.start();
        } catch (_) { }
    }

    function replacement(url, fallback, defaultUrl = null) {
        if (!canPlay()) return;
        const audio = new Audio(url || defaultUrl || '');
        if (!url && !defaultUrl) { fallback(); return; }
        audio.volume = state.settings.volume;
        let fallbackPlayed = false;
        const playFallback = () => {
            if (fallbackPlayed) return;
            fallbackPlayed = true;
            fallback();
        };
        replacements.add(audio);
        audio.onended = () => replacements.delete(audio);
        audio.onerror = () => { replacements.delete(audio); playFallback(); };
        audio.play().catch(() => { replacements.delete(audio); playFallback(); });
    }

    function ellipse(x, y, width, height, fill, stroke) {
        context.beginPath(); context.ellipse(x, y, width, height, 0, 0, Math.PI * 2);
        if (fill) { context.fillStyle = fill; context.fill(); }
        if (stroke) { context.strokeStyle = stroke; context.stroke(); }
    }

    function drawPrize(prize, x, y, held) {
        context.save(); context.translate(x, y);
        const radius = prize.radius;
        const gradient = context.createRadialGradient(-radius * 0.3, -radius * 0.4, 5, 0, 0, radius);
        gradient.addColorStop(0, '#ffffff'); gradient.addColorStop(0.27, prize.color); gradient.addColorStop(1, '#243746');
        context.shadowColor = '#00000060'; context.shadowBlur = held ? 26 : 10; context.shadowOffsetY = 9;
        ellipse(0, 0, radius, radius, gradient, '#ffffff90');
        context.shadowBlur = 0; context.shadowOffsetY = 0;
        ellipse(-radius * 0.25, -radius * 0.52, radius * 0.32, radius * 0.13, '#ffffff75');
        const artwork = imageFor(prize.imageUrl);
        if (artwork) drawImageContained(artwork, 0, -radius * 0.3, radius * 1.2, radius * 0.8);
        context.fillStyle = '#13242d'; context.fillRect(-radius * 0.88, -23, radius * 1.76, 57);
        context.font = '600 ' + (radius > 80 ? 22 : 18) + 'px Rubik, sans-serif';
        context.fillStyle = '#ffffff'; context.textAlign = 'center';
        const lines = wrapLabel(prize.label, radius * 1.65, 2, text => context.measureText(text).width);
        lines.forEach((line, index) => context.fillText(line, 0, (lines.length === 1 ? 12 : 0) + index * 24));
        context.restore();
    }

    function drawUfo(frame, time) {
        const x = frame.x;
        const clawY = frame.clawY;
        const metal = context.createLinearGradient(0, 90, 0, 200);
        metal.addColorStop(0, '#f3fbfb'); metal.addColorStop(0.4, '#9dafb9'); metal.addColorStop(0.7, '#344853'); metal.addColorStop(1, '#c4e4e6');
        context.lineWidth = 8; context.strokeStyle = '#3b525b';
        context.beginPath(); context.moveTo(x, 175); context.lineTo(x, clawY - 30); context.stroke();
        context.lineWidth = 3; context.strokeStyle = '#b4d4d7'; context.stroke();
        const artwork = imageFor(state.settings.vesselImageUrl);
        if (artwork) drawImageContained(artwork, x, 120, 400, 155);
        else {
            ellipse(x, 114, 103, 68, '#85e4d5b0', '#e0fff8');
            ellipse(x, 148, 200, 46, metal, '#cceded'); ellipse(x, 170, 112, 19, '#20393d', '#4b7276');
            for (let light = 0; light < 9; light++) {
                const offset = (light - 4) * 39;
                ellipse(x + offset, 149 + Math.cos(offset / 200) * 12, 8, 5, light % 2 ? '#ffe27a' : '#7affcd');
            }
            ellipse(x - 24, 105, 8, 11, '#183b35'); ellipse(x + 24, 105, 8, 11, '#183b35');
        }
        context.lineWidth = 11; context.lineCap = 'round';
        [-1, 1].forEach(side => {
            const spread = 65 - frame.grip * 25;
            context.strokeStyle = '#adc7cb'; context.beginPath();
            context.moveTo(x + side * 17, clawY); context.lineTo(x + side * spread, clawY + 35);
            context.lineTo(x + side * (spread - 10), clawY + 91); context.lineTo(x + side * (spread - 34), clawY + 102); context.stroke();
            ellipse(x + side * spread, clawY + 35, 9, 9, '#405e65', '#e6f7ee');
        });
        context.strokeStyle = '#68868d'; context.lineWidth = 10; context.beginPath();
        context.moveTo(x, clawY); context.lineTo(x + (reducedMotion.matches ? 0 : 6 * Math.sin(time)), clawY + 64); context.stroke();
        ellipse(x, clawY, 28, 22, metal, '#e0f4ed');
    }

    function draw(now) {
        context.clearRect(0, 0, sceneWidth, 1080);
        const elapsed = startedAt === null ? 0 : Math.max(0, (now - startedAt) / 1000);
        const settings = state?.settings;
        const bounds = state ? updateLayout(now, settings) : layoutBounds;
        const effectiveDuration = state?.grab
            ? effectiveDurationFor(settings.durationSeconds, settings.tease, reducedMotion.matches, settings.slipChancePercent, state.grab.id, feintPrizes, selected, sceneWidth, settings.leftSafeGapPixels, settings.rightSafeGapPixels)
            : settings?.durationSeconds ?? 0;
        visible = !!state && state.visible && !(state.grab && settings.autoHide && elapsed >= effectiveDuration + settings.revealSeconds);
        result.hidden = true;
        if (!visible) silence();
        if (visible) {
            const pitLeft = bounds.left;
            const pitWidth = Math.max(0, sceneWidth - bounds.left - bounds.right);
            if (result.style) {
                result.style.left = (pitLeft / sceneWidth * 100) + '%';
                result.style.width = (pitWidth / sceneWidth * 100) + '%';
            }
            const animationEnd = state.grab
                ? animationEndFor(settings.tease, reducedMotion.matches, settings.slipChancePercent, state.grab.id, feintPrizes, selected, sceneWidth, settings.leftSafeGapPixels, settings.rightSafeGapPixels)
                : 15;
            const seconds = state.grab ? elapsed / effectiveDuration * animationEnd : 0;
            const frame = !state.grab || !selected ? {x:sceneWidth / 2, clawY:275, grip:0, captured:false, heldPrize:null, decoyCaptured:false, dropped:[], locked:false, reveal:false} :
                frameAt(seconds, selected, settings.tease, reducedMotion.matches, settings.movementSpeed, sceneWidth,
                    settings.leftSafeGapPixels, settings.rightSafeGapPixels, settings.slipChancePercent, state.grab.id, feintPrizes);
            if (settings.pitBackgroundEnabled !== false) {
                const pit = context.createLinearGradient(0, 700, 0, 1050);
                pit.addColorStop(0, '#24383b00'); pit.addColorStop(1, '#102e39e8');
                context.fillStyle = pit; context.fillRect(pitLeft, 690, pitWidth, 355);
            }
            const droppedById = new Map(frame.dropped.map(item => [item.prize.id, item]));
            pool.forEach(prize => {
                const winnerHeld = frame.heldPrize && prize.id === frame.heldPrize.id && frame.captured;
                const alternateHeld = frame.decoyCaptured && frame.decoy && prize.id === frame.decoy.id;
                const landing = droppedById.get(prize.id);
                if (!winnerHeld && !alternateHeld) drawPrize(prize, landing?.x ?? prize.x, landing?.y ?? prize.y, false);
            });
            if (settings.pitBackgroundEnabled !== false) {
                context.fillStyle = '#17656a'; context.fillRect(pitLeft, 1000, pitWidth, 28);
                context.fillStyle = '#82e8ce'; context.fillRect(pitLeft, 997, pitWidth, 5);
            }
            if (frame.decoyCaptured) drawPrize(frame.decoy, frame.decoyX, frame.decoyY, true);
            if (frame.captured && frame.heldPrize) drawPrize(frame.heldPrize, frame.prizeX, frame.prizeY, true);
            drawUfo(frame, seconds);
            if (frame.reveal && selected) {
                result.textContent = selected.label;
                result.hidden = false;
            }
            if (state.grab && frame.reveal) void acknowledgeAnimation(state.grab.id);
            if (state.grab && frame.phase !== lastPhase) {
                lastPhase = frame.phase;
                if (frame.phase === 'Descending') sweep(420, 190, 0.65, 0, 'triangle', 0.12);
                if (frame.phase === 'Closing') replacement(state.settings.grabSoundUrl, () => { tone(130, 0.12, 0, 'square'); tone(195, 0.1, 0.16, 'triangle'); }, '/media/ufo_claw_grab.wav');
                if (frame.phase === 'Ascending') sweep(210, 440, 0.65, 0, 'sawtooth', 0.1);
                if (frame.phase === 'InitialDrop' || frame.phase.startsWith('FeintDrop') || frame.phase.startsWith('FeintRelease')) playDropCue();
                if (frame.phase.startsWith('FeintCatch')) { tone(580, 0.08, 0, 'square'); tone(760, 0.12, 0.09, 'triangle'); }
                if (frame.phase.startsWith('FeintLift')) playLiftCue();
                if (frame.phase === 'Recatching') playCatchCue();
                if (frame.reveal) { stopMotor(); replacement(state.settings.revealSoundUrl, () => [523.25,659.25,783.99,1046.5].forEach((frequency,index) => tone(frequency,0.4,index*0.12)), '/media/ufo_claw_reveal.wav'); }
            }
        }
        global.requestAnimationFrame(draw);
    }

    function acceptSnapshot(snapshot, live) {
        if (!snapshot || snapshot.type !== 'ufo_state' || !Array.isArray(snapshot.entries) || !snapshot.settings) return;
        if (state && snapshot.revision < state.revision) return;
        const changedGrab = snapshot.grab?.id !== state?.grab?.id;
        const wasVisible = visible;
        state = snapshot;
        layoutDirty = true;
        const age = snapshot.grab ? Math.max(0,(Date.parse(snapshot.serverNowUtc)-Date.parse(snapshot.grab.startedAtUtc))/1000) : 0;
        startedAt = snapshot.grab ? global.performance.now() - age * 1000 : null;
        visible = snapshot.visible;
        if (changedGrab) { silence(); lastPhase = ''; audible = !!live && age < 2; }
        if (!live || !visible || (!wasVisible && !changedGrab)) { audible = false; silence(); }
        if (changedGrab && visible && audible) void enableAudio();
        if (motorGain && audioContext) motorGain.gain.setTargetAtTime(snapshot.settings.soundEnabled ? snapshot.settings.volume * 0.05 : 0,audioContext.currentTime,0.02);
    }

    global.UfoClawOverlay = { acceptSnapshot };
    const socket = global.createWsSocket('UfoClaw', [global.WS_TOPICS.ufoclaw]);
    let requestNumber = 0;
    async function fetchState() {
        const request = ++requestNumber;
        try {
            const response = await fetch('/api/overlay/ufoclaw-state', {cache:'no-store'});
            if (!response.ok) throw new Error('Snapshot request failed');
            const snapshot = await response.json();
            if (request === requestNumber) acceptSnapshot(snapshot,false);
        } catch (_) { global.wsStatusShow(false); }
    }
    socket.onopen = () => { global.wsStatusShow(true); void fetchState(); };
    socket.onclose = () => { audible = false; silence(); global.wsStatusShow(false); };
    socket.onmessage = event => {
        if (event.data === 'ping') { socket.send('pong'); return; }
        try { acceptSnapshot(JSON.parse(event.data),true); } catch (_) { }
    };
    document.addEventListener('visibilitychange', () => { if (document.hidden) { audible = false; silence(); } });
    document.addEventListener('pointerdown', () => { if (state?.grab && audible) void enableAudio(); });
    global.addEventListener('pagehide', silence);
    void fetchState();
    global.requestAnimationFrame(draw);
})(typeof window !== 'undefined' ? window : globalThis);