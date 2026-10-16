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
            const keys = ['start', 'end', 'drop', 'win', 'pegCollision'];
            keys.forEach(function (k) {
                const s = bundle[k];
                if (s && s.enabled && s.url) {
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

        return {
            preload: preload,
            updateBundle: updateBundle,
            play: play,
            getBundle: function () { return currentBundle; },
            getActiveCount: function () { return activeVoices.length; }
        };
    }

    const DropGamesAudio = {
        createSoundManager: createSoundManager
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

