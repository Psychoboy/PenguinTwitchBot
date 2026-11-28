/**
 * Shared Audio Manager for Drop Games (Plinko, Parachute, etc.)
 * Provides Web Audio API playback with AudioBuffer preloading & caching,
 * smooth 8ms micro-fade de-clicking to avoid pop/clicks on voice interruptions,
 * concurrency limiting with voice stealing (no queuing), and HTML5 Audio fallback.
 */
(function (global) {
    'use strict';

    function toPerceptualGain(volumeFraction) {
        const clamped = Math.max(0, Math.min(1, Number(volumeFraction) || 0));
        return clamped * clamped;
    }

    function percentToGain(volumePercent, defaultPercent) {
        const pct = (typeof volumePercent === 'number' && isFinite(volumePercent))
            ? volumePercent
            : (typeof defaultPercent === 'number' && isFinite(defaultPercent) ? defaultPercent : 80);
        return toPerceptualGain(pct / 100);
    }

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
            // Web Audio source: smooth de-click ramp (25ms) without gain spike
            if (v.source && v.gain && audioCtx) {
                try {
                    const now = audioCtx.currentTime;
                    const currentVal = typeof v.targetGain === 'number'
                        ? Math.min(v.gain.gain.value, v.targetGain)
                        : v.gain.gain.value;
                    if (typeof v.gain.gain.cancelAndHoldAtTime === 'function') {
                        v.gain.gain.cancelAndHoldAtTime(now);
                    } else {
                        v.gain.gain.cancelScheduledValues(now);
                    }
                    v.gain.gain.setValueAtTime(Math.max(0, currentVal), now);
                    // 25ms linear ramp down to 0 eliminates waveform DC jump and low-frequency speaker pops
                    v.gain.gain.linearRampToValueAtTime(0, now + 0.025);
                    v.source.stop(now + 0.03);
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

            const volFraction = percentToGain(soundDto.volume, 80);
            if (volFraction <= 0) return;

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
                    const now = ctx.currentTime;
                    const attackSec = Math.min(0.003, cachedBuffer.duration * 0.25);
                    const releaseSec = Math.min(0.015, cachedBuffer.duration * 0.25);
                    const endTime = now + cachedBuffer.duration;
                    const releaseStart = Math.max(now + attackSec, endTime - releaseSec);
                    gain.gain.setValueAtTime(0, now);
                    gain.gain.linearRampToValueAtTime(volFraction, now + attackSec);
                    if (releaseStart < endTime) {
                        gain.gain.setValueAtTime(volFraction, releaseStart);
                        gain.gain.linearRampToValueAtTime(0, endTime);
                    }

                    source.connect(gain);
                    gain.connect(ctx.destination);

                    const voiceRecord = { source, gain, targetGain: volFraction, key, isHighPriority, isEnded: false };
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

                    const removeVoice = function () {
                        voiceRecord.isEnded = true;
                        const idx = activeVoices.indexOf(voiceRecord);
                        if (idx !== -1) activeVoices.splice(idx, 1);
                    };

                    if (typeof audio.addEventListener === 'function') {
                        audio.addEventListener('ended', removeVoice);
                        audio.addEventListener('error', removeVoice);
                    }

                    const playResult = audio.play();
                    if (playResult && typeof playResult.catch === 'function') {
                        playResult.catch(removeVoice);
                    }
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
            getActiveCount: function () {
                let count = 0;
                for (let i = 0; i < activeVoices.length; i++) {
                    const v = activeVoices[i];
                    if (!v.isEnded && !(v.audio && (v.audio.ended || v.audio.paused))) {
                        count++;
                    }
                }
                return count;
            }
        };
    }


    const DropGamesAudio = {
        createSoundManager: createSoundManager,
        toPerceptualGain: toPerceptualGain,
        percentToGain: percentToGain
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

