/**
 * Shared Audio Manager for Drop Games (Plinko, Parachute, etc.)
 * Provides Web Audio API playback with AudioBuffer preloading & caching,
 * smooth 8ms micro-fade de-clicking to avoid pop/clicks on voice interruptions,
 * concurrency limiting with voice stealing (no queuing), and HTML5 Audio fallback.
 */
(function (global) {
    'use strict';

    function createSoundManager() {
        const audioCache = new Map();
        const bufferCache = new Map();
        const activeVoices = [];
        let currentBundle = null;
        let audioCtx = null;

        function getAudioContext() {
            if (typeof window === 'undefined') return null;
            const AudioContextClass = window.AudioContext || window.webkitAudioContext;
            if (!AudioContextClass) return null;
            if (!audioCtx) {
                try {
                    audioCtx = new AudioContextClass();
                } catch (_) { }
            }
            if (audioCtx && audioCtx.state === 'suspended') {
                try { audioCtx.resume(); } catch (_) { }
            }
            return audioCtx;
        }

        function preload(url) {
            if (!url) return;

            // 1. Preload HTML5 Audio as fallback
            if (typeof Audio !== 'undefined' && !audioCache.has(url)) {
                try {
                    const a = new Audio();
                    a.preload = 'auto';
                    a.src = url;
                    if (typeof a.load === 'function') a.load();
                    audioCache.set(url, a);
                } catch (_) { }
            }

            // 2. Decode into Web Audio AudioBuffer for zero-latency, pop-free playback
            const ctx = getAudioContext();
            if (ctx && typeof fetch !== 'undefined' && !bufferCache.has(url)) {
                bufferCache.set(url, 'loading');
                fetch(url)
                    .then(function (res) {
                        if (!res.ok) throw new Error('Fetch failed');
                        return res.arrayBuffer();
                    })
                    .then(function (arrayBuffer) {
                        return new Promise(function (resolve, reject) {
                            ctx.decodeAudioData(arrayBuffer, resolve, reject);
                        });
                    })
                    .then(function (decoded) {
                        bufferCache.set(url, decoded);
                    })
                    .catch(function () {
                        bufferCache.delete(url);
                    });
            }
        }

        function updateBundle(bundle) {
            if (!bundle) return;
            currentBundle = bundle;
            Object.keys(bundle).forEach(function (k) {
                const s = bundle[k];
                if (s && typeof s === 'object' && s.enabled && s.url) {
                    preload(s.url);
                }
            });
        }

        function stopVoice(v) {
            if (!v) return;
            // Web Audio source: smooth micro-fade de-click ramp (8ms)
            if (v.source && v.gain && audioCtx) {
                try {
                    const now = audioCtx.currentTime;
                    v.gain.gain.cancelScheduledValues(now);
                    v.gain.gain.setValueAtTime(v.gain.gain.value, now);
                    // 8ms linear ramp down to 0 eliminates waveform DC jump and speaker pops
                    v.gain.gain.linearRampToValueAtTime(0.0001, now + 0.008);
                    v.source.stop(now + 0.008);
                } catch (_) { }
            } else if (v.audio) {
                try {
                    // HTML5 Audio: ramp volume to 0 before pause and DO NOT seek to currentTime = 0
                    if (typeof v.audio.volume === 'number') {
                        v.audio.volume = 0;
                    }
                    v.audio.pause();
                } catch (_) { }
            }
        }

        function play(soundDto, key, isHighPriority) {
            key = key || 'drop';
            isHighPriority = !!isHighPriority;
            if (!soundDto || !soundDto.enabled || !soundDto.url) return;
            const ctx = getAudioContext();
            if (!ctx && typeof Audio === 'undefined') return;

            // Ensure sound asset is queued for decoding if not yet cached
            if (ctx && !bufferCache.has(soundDto.url)) {
                preload(soundDto.url);
            }

            const maxVoices = (currentBundle && typeof currentBundle.maxConcurrentSounds === 'number' && currentBundle.maxConcurrentSounds >= 1)
                ? currentBundle.maxConcurrentSounds
                : 1;

            // Clean up ended voices
            for (let i = activeVoices.length - 1; i >= 0; i--) {
                const v = activeVoices[i];
                if (v.isEnded || (v.audio && (v.audio.ended || v.audio.paused))) {
                    activeVoices.splice(i, 1);
                }
            }

            // Enforce concurrency limit with immediate voice stealing (no queuing!)
            while (activeVoices.length >= maxVoices) {
                let victimIdx = 0;
                if (!isHighPriority) {
                    const nonHighIdx = activeVoices.findIndex(function (v) { return !v.isHighPriority; });
                    if (nonHighIdx !== -1) victimIdx = nonHighIdx;
                }
                const victim = activeVoices.splice(victimIdx, 1)[0];
                stopVoice(victim);
            }

            const volFraction = Math.max(0, Math.min(1, (typeof soundDto.volume === 'number' ? soundDto.volume : 80) / 100));

            // Log for tests / telemetry if tracked
            if (typeof window !== 'undefined' && window.__audioPlayed) {
                window.__audioPlayed.push({ src: soundDto.url, volume: volFraction });
            }

            // 1. Try Web Audio buffer source (pop-free, zero-latency)
            const cachedBuffer = bufferCache.get(soundDto.url);
            if (ctx && cachedBuffer && typeof cachedBuffer === 'object' && cachedBuffer.duration) {
                try {
                    const source = ctx.createBufferSource();
                    source.buffer = cachedBuffer;

                    const gain = ctx.createGain();
                    gain.gain.setValueAtTime(volFraction, ctx.currentTime);

                    source.connect(gain);
                    gain.connect(ctx.destination);

                    const voiceRecord = { source, gain, key, isHighPriority, isEnded: false };
                    activeVoices.push(voiceRecord);

                    source.onended = function () {
                        voiceRecord.isEnded = true;
                        const idx = activeVoices.indexOf(voiceRecord);
                        if (idx !== -1) activeVoices.splice(idx, 1);
                    };

                    source.start(0);
                    return;
                } catch (_) { }
            }

            // 2. Fallback: HTML5 Audio
            if (typeof Audio !== 'undefined') {
                try {
                    const audio = new Audio(soundDto.url);
                    audio.volume = volFraction;
                    audio.preload = 'auto';

                    const voiceRecord = { audio, key, isHighPriority, isEnded: false };
                    activeVoices.push(voiceRecord);

                    if (typeof audio.addEventListener === 'function') {
                        audio.addEventListener('ended', function () {
                            voiceRecord.isEnded = true;
                            const idx = activeVoices.indexOf(voiceRecord);
                            if (idx !== -1) activeVoices.splice(idx, 1);
                        });
                    }

                    audio.play().catch(function () { });
                } catch (_) { }
            }
        }

        function playKey(key, isHighPriority) {
            if (!currentBundle) return;
            const dto = currentBundle[key];
            if (dto) {
                play(dto, key, isHighPriority);
            }
        }

        return {
            preload: preload,
            updateBundle: updateBundle,
            play: play,
            playKey: playKey,
            getBundle: function () { return currentBundle; },
            getActiveCount: function () { return activeVoices.length; }
        };
    }

    function createAudioEngine() {
        let audioCtx = null;
        function getContext() {
            if (typeof window === 'undefined') return null;
            const AudioContextClass = window.AudioContext || window.webkitAudioContext;
            if (!AudioContextClass) return null;
            if (!audioCtx) {
                try { audioCtx = new AudioContextClass(); } catch (_) { }
            }
            if (audioCtx && audioCtx.state === 'suspended') {
                try { audioCtx.resume(); } catch (_) { }
            }
            return audioCtx;
        }

        function playProceduralChime(freq = 440, volume = 0.65) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                const now = ctx.currentTime;
                osc.type = 'sine';
                osc.frequency.setValueAtTime(freq, now);
                gain.gain.setValueAtTime(volume * 0.4, now);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.35);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + 0.36);
            } catch (_) { }
        }

        function playBalloonLaunch(volume = 0.6) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sine';
                osc.frequency.setValueAtTime(240, now);
                osc.frequency.exponentialRampToValueAtTime(620, now + 0.3);
                gain.gain.setValueAtTime(0.001, now);
                gain.gain.linearRampToValueAtTime(volume * 0.45, now + 0.04);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.32);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + 0.33);
            } catch (_) { }
        }

        function playDartPop(volume = 0.75) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const bufferSize = Math.floor(ctx.sampleRate * 0.05);
                const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
                const data = buffer.getChannelData(0);
                for (let i = 0; i < bufferSize; i++) {
                    data[i] = (Math.random() * 2 - 1) * Math.exp(-i / (bufferSize * 0.2));
                }
                const noise = ctx.createBufferSource();
                noise.buffer = buffer;
                const filter = ctx.createBiquadFilter();
                filter.type = 'bandpass';
                filter.frequency.setValueAtTime(1400, now);
                filter.Q.setValueAtTime(2.0, now);
                const gain = ctx.createGain();
                gain.gain.setValueAtTime(volume * 0.7, now);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.08);
                noise.connect(filter);
                filter.connect(gain);
                gain.connect(ctx.destination);
                noise.start(now);
                noise.stop(now + 0.09);

                const osc = ctx.createOscillator();
                const oscGain = ctx.createGain();
                osc.type = 'triangle';
                osc.frequency.setValueAtTime(450, now);
                osc.frequency.exponentialRampToValueAtTime(80, now + 0.07);
                oscGain.gain.setValueAtTime(volume * 0.6, now);
                oscGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.08);
                osc.connect(oscGain);
                oscGain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + 0.09);
            } catch (_) { }
        }

        function playAnvilBonk(volume = 0.7) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                [180, 290, 420].forEach((freq, idx) => {
                    const osc = ctx.createOscillator();
                    const gain = ctx.createGain();
                    osc.type = idx === 0 ? 'sine' : 'triangle';
                    osc.frequency.setValueAtTime(freq, now);
                    osc.frequency.exponentialRampToValueAtTime(freq * 0.75, now + 0.35);
                    const v = volume * (idx === 0 ? 0.6 : 0.3);
                    gain.gain.setValueAtTime(v, now);
                    gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.38);
                    osc.connect(gain);
                    gain.connect(ctx.destination);
                    osc.start(now);
                    osc.stop(now + 0.4);
                });
            } catch (_) { }
        }

        function playNudgeBump(volume = 0.55) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sine';
                osc.frequency.setValueAtTime(220, now);
                osc.frequency.linearRampToValueAtTime(380, now + 0.04);
                osc.frequency.exponentialRampToValueAtTime(140, now + 0.22);
                gain.gain.setValueAtTime(volume * 0.5, now);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.24);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + 0.25);
            } catch (_) { }
        }

        function playWindGust(volume = 0.5) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const dur = 0.45;
                const bufferSize = Math.floor(ctx.sampleRate * dur);
                const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
                const data = buffer.getChannelData(0);
                for (let i = 0; i < bufferSize; i++) {
                    data[i] = (Math.random() * 2 - 1);
                }
                const noise = ctx.createBufferSource();
                noise.buffer = buffer;
                const filter = ctx.createBiquadFilter();
                filter.type = 'bandpass';
                filter.frequency.setValueAtTime(320, now);
                filter.frequency.exponentialRampToValueAtTime(750, now + 0.2);
                filter.frequency.exponentialRampToValueAtTime(260, now + dur);
                filter.Q.setValueAtTime(2.5, now);
                const gain = ctx.createGain();
                gain.gain.setValueAtTime(0.001, now);
                gain.gain.linearRampToValueAtTime(volume * 0.4, now + 0.12);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + dur);
                noise.connect(filter);
                filter.connect(gain);
                gain.connect(ctx.destination);
                noise.start(now);
                noise.stop(now + dur + 0.02);
            } catch (_) { }
        }

        function playCloudLanding(volume = 0.65) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const notes = [523.25, 659.25, 783.99, 1046.50];
                notes.forEach((freq, idx) => {
                    const osc = ctx.createOscillator();
                    const gain = ctx.createGain();
                    const startTime = now + idx * 0.06;
                    osc.type = 'sine';
                    osc.frequency.setValueAtTime(freq, startTime);
                    gain.gain.setValueAtTime(0.001, startTime);
                    gain.gain.linearRampToValueAtTime(volume * 0.35, startTime + 0.02);
                    gain.gain.exponentialRampToValueAtTime(0.0001, startTime + 0.6);
                    osc.connect(gain);
                    gain.connect(ctx.destination);
                    osc.start(startTime);
                    osc.stop(startTime + 0.62);
                });
            } catch (_) { }
        }

        function playBullseyeFanfare(volume = 0.75) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const chord = [523.25, 659.25, 783.99, 1046.50, 1318.51];
                chord.forEach((freq, idx) => {
                    const osc = ctx.createOscillator();
                    const gain = ctx.createGain();
                    const startTime = now + (idx < 3 ? idx * 0.05 : 0.22);
                    osc.type = idx >= 3 ? 'triangle' : 'sine';
                    osc.frequency.setValueAtTime(freq, startTime);
                    gain.gain.setValueAtTime(0.001, startTime);
                    gain.gain.linearRampToValueAtTime(volume * 0.38, startTime + 0.03);
                    gain.gain.exponentialRampToValueAtTime(0.0001, startTime + 0.85);
                    osc.connect(gain);
                    gain.connect(ctx.destination);
                    osc.start(startTime);
                    osc.stop(startTime + 0.9);
                });
            } catch (_) { }
        }

        function playCurlingSlide(volume = 0.55) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const dur = 0.8;
                const bufferSize = Math.floor(ctx.sampleRate * dur);
                const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
                const data = buffer.getChannelData(0);
                for (let i = 0; i < bufferSize; i++) {
                    data[i] = (Math.random() * 2 - 1);
                }
                const noise = ctx.createBufferSource();
                noise.buffer = buffer;
                const filter = ctx.createBiquadFilter();
                filter.type = 'bandpass';
                filter.frequency.setValueAtTime(650, now);
                filter.frequency.linearRampToValueAtTime(450, now + dur);
                filter.Q.setValueAtTime(1.5, now);
                const gain = ctx.createGain();
                gain.gain.setValueAtTime(0.001, now);
                gain.gain.linearRampToValueAtTime(volume * 0.35, now + 0.1);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + dur);
                noise.connect(filter);
                filter.connect(gain);
                gain.connect(ctx.destination);
                noise.start(now);
                noise.stop(now + dur + 0.02);

                const osc = ctx.createOscillator();
                const oscGain = ctx.createGain();
                osc.type = 'triangle';
                osc.frequency.setValueAtTime(92, now);
                osc.frequency.linearRampToValueAtTime(80, now + dur);
                oscGain.gain.setValueAtTime(volume * 0.25, now);
                oscGain.gain.exponentialRampToValueAtTime(0.0001, now + dur);
                osc.connect(oscGain);
                oscGain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + dur + 0.02);
            } catch (_) { }
        }

        function playCurlingSweep(volume = 0.5) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                [0, 0.18, 0.36].forEach((st) => {
                    const dur = 0.14;
                    const bufferSize = Math.floor(ctx.sampleRate * dur);
                    const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
                    const data = buffer.getChannelData(0);
                    for (let i = 0; i < bufferSize; i++) {
                        data[i] = (Math.random() * 2 - 1);
                    }
                    const noise = ctx.createBufferSource();
                    noise.buffer = buffer;
                    const filter = ctx.createBiquadFilter();
                    filter.type = 'bandpass';
                    filter.frequency.setValueAtTime(1800, now + st);
                    filter.Q.setValueAtTime(2.0, now + st);
                    const gain = ctx.createGain();
                    gain.gain.setValueAtTime(0.001, now + st);
                    gain.gain.linearRampToValueAtTime(volume * 0.3, now + st + 0.04);
                    gain.gain.exponentialRampToValueAtTime(0.0001, now + st + dur);
                    noise.connect(filter);
                    filter.connect(gain);
                    gain.connect(ctx.destination);
                    noise.start(now + st);
                    noise.stop(now + st + dur + 0.01);
                });
            } catch (_) { }
        }

        function playCurlingHit(volume = 0.7) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const bufferSize = Math.floor(ctx.sampleRate * 0.03);
                const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
                const data = buffer.getChannelData(0);
                for (let i = 0; i < bufferSize; i++) {
                    data[i] = (Math.random() * 2 - 1) * Math.exp(-i / (bufferSize * 0.25));
                }
                const noise = ctx.createBufferSource();
                noise.buffer = buffer;
                const gain = ctx.createGain();
                gain.gain.setValueAtTime(volume * 0.6, now);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.05);
                noise.connect(gain);
                gain.connect(ctx.destination);
                noise.start(now);
                noise.stop(now + 0.06);

                [340, 680].forEach((freq, idx) => {
                    const osc = ctx.createOscillator();
                    const oscGain = ctx.createGain();
                    osc.type = idx === 0 ? 'sine' : 'triangle';
                    osc.frequency.setValueAtTime(freq, now);
                    oscGain.gain.setValueAtTime(volume * (idx === 0 ? 0.5 : 0.25), now);
                    oscGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.16);
                    osc.connect(oscGain);
                    oscGain.connect(ctx.destination);
                    osc.start(now);
                    osc.stop(now + 0.18);
                });
            } catch (_) { }
        }

        function playCurlingScore(volume = 0.65) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                [783.99, 1046.50].forEach((freq, idx) => {
                    const osc = ctx.createOscillator();
                    const gain = ctx.createGain();
                    const st = now + idx * 0.12;
                    osc.type = 'sine';
                    osc.frequency.setValueAtTime(freq, st);
                    gain.gain.setValueAtTime(0.001, st);
                    gain.gain.linearRampToValueAtTime(volume * 0.35, st + 0.02);
                    gain.gain.exponentialRampToValueAtTime(0.0001, st + 0.55);
                    osc.connect(gain);
                    gain.connect(ctx.destination);
                    osc.start(st);
                    osc.stop(st + 0.58);
                });
            } catch (_) { }
        }

        function playGolfStrike(volume = 0.65) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                // Crisp club impact transient + whoosh
                const dur = 0.04;
                const bufferSize = Math.floor(ctx.sampleRate * dur);
                const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
                const data = buffer.getChannelData(0);
                for (let i = 0; i < bufferSize; i++) {
                    data[i] = (Math.random() * 2 - 1) * Math.exp(-i / (bufferSize * 0.2));
                }
                const noise = ctx.createBufferSource();
                noise.buffer = buffer;
                const filter = ctx.createBiquadFilter();
                filter.type = 'bandpass';
                filter.frequency.setValueAtTime(2200, now);
                filter.Q.setValueAtTime(1.8, now);
                const gain = ctx.createGain();
                gain.gain.setValueAtTime(volume * 0.7, now);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + dur);
                noise.connect(filter);
                filter.connect(gain);
                gain.connect(ctx.destination);
                noise.start(now);
                noise.stop(now + dur + 0.01);

                const osc = ctx.createOscillator();
                const oscGain = ctx.createGain();
                osc.type = 'triangle';
                osc.frequency.setValueAtTime(520, now);
                osc.frequency.exponentialRampToValueAtTime(140, now + 0.09);
                oscGain.gain.setValueAtTime(volume * 0.55, now);
                oscGain.gain.exponentialRampToValueAtTime(0.0001, now + 0.1);
                osc.connect(oscGain);
                oscGain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + 0.11);
            } catch (_) { }
        }

        function playGolfBounce(volume = 0.45) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sine';
                osc.frequency.setValueAtTime(175, now);
                osc.frequency.exponentialRampToValueAtTime(65, now + 0.09);
                gain.gain.setValueAtTime(volume * 0.5, now);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.1);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + 0.11);
            } catch (_) { }
        }

        function playGolfCup(volume = 0.75) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                // Classic golf cup rattle (rapid hollow plastic/metal clinks)
                [0, 0.055, 0.105].forEach((offset, idx) => {
                    const osc = ctx.createOscillator();
                    const gain = ctx.createGain();
                    const st = now + offset;
                    osc.type = idx === 1 ? 'triangle' : 'sine';
                    osc.frequency.setValueAtTime(idx === 0 ? 690 : (idx === 1 ? 920 : 1180), st);
                    osc.frequency.exponentialRampToValueAtTime(idx === 2 ? 780 : 480, st + 0.08);
                    gain.gain.setValueAtTime(volume * (0.55 - idx * 0.1), st);
                    gain.gain.exponentialRampToValueAtTime(0.0001, st + 0.09);
                    osc.connect(gain);
                    gain.connect(ctx.destination);
                    osc.start(st);
                    osc.stop(st + 0.1);
                });
            } catch (_) { }
        }

        function playGolfLipOut(volume = 0.6) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'triangle';
                osc.frequency.setValueAtTime(440, now);
                osc.frequency.linearRampToValueAtTime(240, now + 0.22);
                gain.gain.setValueAtTime(volume * 0.45, now);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.24);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + 0.25);
            } catch (_) { }
        }

        function playGolfWin(volume = 0.65) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                [659.25, 880.0, 1046.5].forEach((freq, idx) => {
                    const osc = ctx.createOscillator();
                    const gain = ctx.createGain();
                    const st = now + idx * 0.09;
                    osc.type = 'sine';
                    osc.frequency.setValueAtTime(freq, st);
                    gain.gain.setValueAtTime(0.001, st);
                    gain.gain.linearRampToValueAtTime(volume * 0.35, st + 0.02);
                    gain.gain.exponentialRampToValueAtTime(0.0001, st + 0.45);
                    osc.connect(gain);
                    gain.connect(ctx.destination);
                    osc.start(st);
                    osc.stop(st + 0.48);
                });
            } catch (_) { }
        }

        function playRaceCountdownBeep(isFinal = false, volume = 0.6) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = isFinal ? 'sawtooth' : 'sine';
                osc.frequency.setValueAtTime(isFinal ? 880 : 440, now);
                const dur = isFinal ? 0.45 : 0.16;
                gain.gain.setValueAtTime(0.001, now);
                gain.gain.linearRampToValueAtTime(volume * (isFinal ? 0.45 : 0.3), now + 0.02);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + dur);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + dur + 0.02);
            } catch (_) { }
        }

        function playCarBump(volume = 0.5) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'square';
                osc.frequency.setValueAtTime(140, now);
                osc.frequency.exponentialRampToValueAtTime(45, now + 0.12);
                gain.gain.setValueAtTime(volume * 0.4, now);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.14);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + 0.15);
            } catch (_) { }
        }

        function playTireScreech(volume = 0.5) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const dur = 0.28;
                const bufferSize = Math.floor(ctx.sampleRate * dur);
                const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
                const data = buffer.getChannelData(0);
                for (let i = 0; i < bufferSize; i++) data[i] = (Math.random() * 2 - 1);
                const noise = ctx.createBufferSource();
                noise.buffer = buffer;
                const filter = ctx.createBiquadFilter();
                filter.type = 'bandpass';
                filter.frequency.setValueAtTime(2800, now);
                filter.frequency.linearRampToValueAtTime(3400, now + dur);
                filter.Q.setValueAtTime(5.0, now);
                const gain = ctx.createGain();
                gain.gain.setValueAtTime(0.001, now);
                gain.gain.linearRampToValueAtTime(volume * 0.35, now + 0.05);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + dur);
                noise.connect(filter);
                filter.connect(gain);
                gain.connect(ctx.destination);
                noise.start(now);
                noise.stop(now + dur + 0.02);
            } catch (_) { }
        }

        function playBoostPad(volume = 0.6) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sawtooth';
                osc.frequency.setValueAtTime(260, now);
                osc.frequency.exponentialRampToValueAtTime(950, now + 0.35);
                gain.gain.setValueAtTime(volume * 0.35, now);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.38);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + 0.4);
            } catch (_) { }
        }

        function playRaceWin(volume = 0.7) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                [523.25, 659.25, 783.99, 1046.50].forEach((freq, idx) => {
                    const osc = ctx.createOscillator();
                    const gain = ctx.createGain();
                    const st = now + idx * 0.11;
                    osc.type = 'triangle';
                    osc.frequency.setValueAtTime(freq, st);
                    gain.gain.setValueAtTime(0.001, st);
                    gain.gain.linearRampToValueAtTime(volume * 0.45, st + 0.03);
                    gain.gain.exponentialRampToValueAtTime(0.0001, st + 0.55);
                    osc.connect(gain);
                    gain.connect(ctx.destination);
                    osc.start(st);
                    osc.stop(st + 0.58);
                });
            } catch (_) { }
        }

        function playEngineRev(volume = 0.5) {
            const ctx = getContext();
            if (!ctx) return;
            try {
                const now = ctx.currentTime;
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sawtooth';
                osc.frequency.setValueAtTime(80, now);
                osc.frequency.exponentialRampToValueAtTime(260, now + 0.6);
                gain.gain.setValueAtTime(0.001, now);
                gain.gain.linearRampToValueAtTime(volume * 0.35, now + 0.05);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.7);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start(now);
                osc.stop(now + 0.72);
            } catch (_) { }
        }

        return {
            playProceduralChime: playProceduralChime,
            playBalloonLaunch: playBalloonLaunch,
            playDartPop: playDartPop,
            playAnvilBonk: playAnvilBonk,
            playNudgeBump: playNudgeBump,
            playWindGust: playWindGust,
            playCloudLanding: playCloudLanding,
            playBullseyeFanfare: playBullseyeFanfare,
            playCurlingSlide: playCurlingSlide,
            playCurlingSweep: playCurlingSweep,
            playCurlingHit: playCurlingHit,
            playCurlingScore: playCurlingScore,
            playGolfStrike: playGolfStrike,
            playGolfBounce: playGolfBounce,
            playGolfCup: playGolfCup,
            playGolfLipOut: playGolfLipOut,
            playGolfWin: playGolfWin,
            playRaceCountdownBeep: playRaceCountdownBeep,
            playCarBump: playCarBump,
            playTireScreech: playTireScreech,
            playBoostPad: playBoostPad,
            playRaceWin: playRaceWin,
            playEngineRev: playEngineRev
        };
    }

    const DropGamesAudio = {
        createSoundManager: createSoundManager,
        createAudioEngine: createAudioEngine
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = DropGamesAudio;
    }
    if (typeof global !== 'undefined') {
        global.DropGamesAudio = DropGamesAudio;
    }
    if (typeof window !== 'undefined') {
        window.DropGamesAudio = DropGamesAudio;
    }
    if (typeof globalThis !== 'undefined') {
        globalThis.DropGamesAudio = DropGamesAudio;
    }
})(typeof window !== 'undefined' ? window : this);

