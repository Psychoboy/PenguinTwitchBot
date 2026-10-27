from array import array
from math import pi, sin, cos, exp, floor
from pathlib import Path
import random
import wave

SAMPLE_RATE = 44100
OUTPUT = Path(__file__).resolve().parents[1] / "PenguinTwitchBot" / "wwwroot" / "media"


def clamp(val, low=-1.0, high=1.0):
    return max(low, min(high, val))


def write_wav(filename, samples_stereo):
    output_path = OUTPUT / filename
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(output_path), "wb") as wav:
        wav.setnchannels(2)
        wav.setsampwidth(2)
        wav.setframerate(SAMPLE_RATE)
        wav.writeframes(samples_stereo.tobytes())
    print(f"Generated {filename}: {len(samples_stereo) // 2 / SAMPLE_RATE:.2f}s, stereo PCM16")


def pack_stereo(left, right, target_peak=0.82):
    peak = max(max(abs(v) for v in left), max(abs(v) for v in right), 1e-9)
    scale = target_peak / peak
    out = array("h")
    for l_val, r_val in zip(left, right):
        out.append(round(clamp(l_val * scale) * 32767))
        out.append(round(clamp(r_val * scale) * 32767))
    return out


def generate_start_sound():
    # 3 warning beeps at 440 Hz (0.0s, 0.35s, 0.70s), then high vibrant GO chime at 1.05s (880 Hz + 1760 Hz)
    total_duration = 1.65
    frame_count = int(total_duration * SAMPLE_RATE)
    left = [0.0] * frame_count
    right = [0.0] * frame_count

    beeps = [
        (0.00, 0.14, 440.0, 0.40, 0.0),
        (0.35, 0.14, 440.0, 0.40, 0.0),
        (0.70, 0.14, 440.0, 0.45, 0.0),
        (1.05, 0.55, 880.0, 0.60, 0.0),
    ]

    for start_t, dur_t, freq, gain, pan in beeps:
        start_idx = int(start_t * SAMPLE_RATE)
        sample_count = int(dur_t * SAMPLE_RATE)
        phase = 0.0
        is_go = (freq > 800)
        for i in range(sample_count):
            t = i / SAMPLE_RATE
            phase += 2.0 * pi * freq / SAMPLE_RATE
            # Sine + slight square overtone for arcade beep punch
            wave_val = sin(phase) * 0.85 + (1.0 if sin(phase) > 0 else -1.0) * 0.15
            if is_go:
                # Add higher harmonic for sparkling victory GO tone
                wave_val += sin(phase * 2.0) * 0.30

            # Attack / decay envelope
            attack = min(1.0, t / 0.008)
            decay = exp(-t * (5.5 if is_go else 12.0))
            env = attack * decay

            sample = wave_val * env * gain
            idx = start_idx + i
            if idx < frame_count:
                left[idx] += sample * (1.0 - max(0.0, pan))
                right[idx] += sample * (1.0 + min(0.0, pan))

    return pack_stereo(left, right)


def generate_end_sound():
    # Race finished fanfare: energetic descending-ascending triumphant brass arpeggio
    # C5, G4, C5, E5, G5, C6
    notes = [
        (0.00, 0.18, 523.25, 0.35, -0.15),
        (0.12, 0.18, 392.00, 0.32, 0.15),
        (0.24, 0.20, 523.25, 0.38, -0.10),
        (0.38, 0.22, 659.25, 0.42, 0.10),
        (0.54, 0.24, 783.99, 0.45, -0.05),
        (0.72, 0.52, 1046.50, 0.55, 0.0),
    ]
    total_duration = 1.35
    frame_count = int(total_duration * SAMPLE_RATE)
    left = [0.0] * frame_count
    right = [0.0] * frame_count

    for start_t, dur_t, freq, gain, pan in notes:
        start_idx = int(start_t * SAMPLE_RATE)
        sample_count = int(dur_t * SAMPLE_RATE)
        phase = 0.0
        for i in range(sample_count):
            t = i / SAMPLE_RATE
            phase += 2.0 * pi * freq / SAMPLE_RATE
            # Brass synth: fundamental + 2nd and 3rd harmonics
            wave_val = sin(phase) + 0.35 * sin(phase * 2.0) + 0.18 * sin(phase * 3.0)
            attack = min(1.0, t / 0.015)
            decay = exp(-t * 4.2)
            env = attack * decay
            sample = wave_val * env * gain
            idx = start_idx + i
            if idx < frame_count:
                left[idx] += sample * (1.0 - max(0.0, pan))
                right[idx] += sample * (1.0 + min(0.0, pan))

    return pack_stereo(left, right)


def generate_engine_sound():
    # Engine revving up: low rumble transitioning into high-RPM throttle burst
    total_duration = 1.10
    frame_count = int(total_duration * SAMPLE_RATE)
    left = [0.0] * frame_count
    right = [0.0] * frame_count

    phase1 = 0.0
    phase2 = 0.0
    for i in range(frame_count):
        t = i / SAMPLE_RATE
        # Sweep RPM from 85 Hz to 240 Hz
        freq = 85.0 + 155.0 * (t / total_duration) ** 1.4
        # Add subtle engine cylinder flutter (14 Hz vibrato)
        freq += 6.0 * sin(2.0 * pi * 14.0 * t)

        phase1 += 2.0 * pi * freq / SAMPLE_RATE
        phase2 += 2.0 * pi * (freq * 1.5) / SAMPLE_RATE

        # Sawtooth / triangle combo
        saw = 2.0 * (phase1 / (2.0 * pi) - floor(phase1 / (2.0 * pi) + 0.5))
        tri = 2.0 * abs(2.0 * (phase2 / (2.0 * pi) - floor(phase2 / (2.0 * pi) + 0.5))) - 1.0

        # Sub-bass rumble
        sub = sin(phase1 * 0.5) * 0.4

        # Slight cylinder noise
        noise = (random.random() * 2.0 - 1.0) * 0.08

        # Envelope: rev up with gentle swell then taper
        env = min(1.0, t / 0.08) * (1.0 - 0.25 * (t / total_duration))

        val = (saw * 0.55 + tri * 0.30 + sub + noise) * env
        left[i] = val * 0.95
        right[i] = val * 1.05

    return pack_stereo(left, right)


def generate_screech_sound():
    # Tire skid / screech: friction resonance 2600-3400 Hz with fast frequency modulation
    total_duration = 0.68
    frame_count = int(total_duration * SAMPLE_RATE)
    left = [0.0] * frame_count
    right = [0.0] * frame_count

    mod_phase = 0.0
    carrier_phase = 0.0
    for i in range(frame_count):
        t = i / SAMPLE_RATE
        # Modulation frequency (chatter)
        mod_phase += 2.0 * pi * 140.0 / SAMPLE_RATE
        freq = 2800.0 + 500.0 * sin(mod_phase) + (t / total_duration) * 350.0

        carrier_phase += 2.0 * pi * freq / SAMPLE_RATE
        tone = sin(carrier_phase) * 0.6 + sin(carrier_phase * 1.5) * 0.25
        friction_noise = (random.random() * 2.0 - 1.0) * 0.35

        # Envelope: punchy bite, holding sustain, fast release
        attack = min(1.0, t / 0.02)
        release = max(0.0, (total_duration - t) / 0.15)
        env = attack * min(1.0, release)

        val = (tone + friction_noise) * env
        left[i] = val * (0.8 + 0.4 * sin(t * 18.0))
        right[i] = val * (0.8 - 0.4 * sin(t * 18.0))

    return pack_stereo(left, right)


def generate_crash_sound():
    # Vehicle bumper bump / crash impact: punchy thud (160 Hz -> 40 Hz) + metallic crunch
    total_duration = 0.42
    frame_count = int(total_duration * SAMPLE_RATE)
    left = [0.0] * frame_count
    right = [0.0] * frame_count

    phase = 0.0
    for i in range(frame_count):
        t = i / SAMPLE_RATE
        # Fast pitch drop
        freq = 160.0 * exp(-t * 18.0) + 38.0
        phase += 2.0 * pi * freq / SAMPLE_RATE

        # Square wave punch + noise burst
        square = 1.0 if sin(phase) > 0 else -1.0
        noise = (random.random() * 2.0 - 1.0) * exp(-t * 22.0) * 0.85

        env = exp(-t * 9.5)
        val = (square * 0.45 + sin(phase) * 0.4 + noise) * env
        left[i] = val * 1.0
        right[i] = val * 0.9

    return pack_stereo(left, right)


def generate_boost_sound():
    # Nitro boost / slingshot whoosh: rising energy sci-fi turbo sweep 220 Hz -> 1150 Hz
    total_duration = 0.72
    frame_count = int(total_duration * SAMPLE_RATE)
    left = [0.0] * frame_count
    right = [0.0] * frame_count

    phase = 0.0
    for i in range(frame_count):
        t = i / SAMPLE_RATE
        freq = 220.0 * (1150.0 / 220.0) ** (t / total_duration)
        phase += 2.0 * pi * freq / SAMPLE_RATE

        saw = 2.0 * (phase / (2.0 * pi) - floor(phase / (2.0 * pi) + 0.5))
        sine = sin(phase * 2.0) * 0.35
        whoosh_noise = (random.random() * 2.0 - 1.0) * 0.25 * (t / total_duration)

        attack = min(1.0, t / 0.04)
        release = max(0.0, (total_duration - t) / 0.12)
        env = attack * min(1.0, release)

        pan = -0.4 + 0.8 * (t / total_duration)
        val = (saw * 0.55 + sine + whoosh_noise) * env
        left[i] = val * (1.0 - max(0.0, pan))
        right[i] = val * (1.0 + min(0.0, pan))

    return pack_stereo(left, right)


def generate_win_sound():
    # Victory fanfare: Grand majestic arpeggio (C5 -> E5 -> G5 -> C6) with brass shimmer & reverb tail
    chords = [
        (0.00, 0.22, 523.25, 0.40, -0.2),
        (0.18, 0.22, 659.25, 0.42, 0.2),
        (0.36, 0.26, 783.99, 0.46, -0.1),
        (0.58, 0.95, 1046.50, 0.58, 0.0),
        (0.58, 0.95, 1318.51, 0.35, 0.15),
    ]
    total_duration = 1.60
    frame_count = int(total_duration * SAMPLE_RATE)
    left = [0.0] * frame_count
    right = [0.0] * frame_count

    for start_t, dur_t, freq, gain, pan in chords:
        start_idx = int(start_t * SAMPLE_RATE)
        sample_count = int(dur_t * SAMPLE_RATE)
        phase = 0.0
        for i in range(sample_count):
            t = i / SAMPLE_RATE
            phase += 2.0 * pi * freq / SAMPLE_RATE
            # Bright celebratory brass/bell overtone
            tone = sin(phase) + 0.38 * sin(phase * 2.0) + 0.15 * sin(phase * 3.0) + 0.08 * sin(phase * 4.0)
            attack = min(1.0, t / 0.015)
            decay = exp(-t * (2.8 if freq > 1000 else 4.5))
            env = attack * decay
            sample = tone * env * gain
            idx = start_idx + i
            if idx < frame_count:
                left[idx] += sample * (1.0 - max(0.0, pan))
                right[idx] += sample * (1.0 + min(0.0, pan))

    return pack_stereo(left, right)


def main():
    sounds = {
        "racing_start.wav": generate_start_sound(),
        "racing_end.wav": generate_end_sound(),
        "racing_engine.wav": generate_engine_sound(),
        "racing_screech.wav": generate_screech_sound(),
        "racing_crash.wav": generate_crash_sound(),
        "racing_boost.wav": generate_boost_sound(),
        "racing_win.wav": generate_win_sound(),
    }

    for filename, data in sounds.items():
        write_wav(filename, data)


if __name__ == "__main__":
    main()

