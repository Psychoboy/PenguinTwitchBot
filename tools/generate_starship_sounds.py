from array import array
from math import pi, sin
from pathlib import Path
import wave

SAMPLE_RATE = 44100
OUTPUT = Path(__file__).resolve().parents[1] / "PenguinTwitchBot" / "wwwroot" / "media"

# Each cue is a short original layered-tone motif: start, duration, frequency, gain, pan.
CUES = {
    "starship_route_choice.wav": (0.75, [(0.00, 0.16, 523.25, 0.36, -0.25), (0.11, 0.19, 659.25, 0.34, 0.0), (0.25, 0.34, 783.99, 0.31, 0.25)]),
    "starship_station_dock.wav": (0.82, [(0.00, 0.28, 392.00, 0.30, -0.15), (0.16, 0.46, 523.25, 0.28, 0.15), (0.38, 0.30, 659.25, 0.18, 0.0)]),
    "starship_signal_detected.wav": (0.94, [(0.02, 0.14, 740.00, 0.30, -0.1), (0.28, 0.17, 880.00, 0.32, 0.1), (0.61, 0.21, 987.77, 0.29, 0.0)]),
    "starship_salvage_found.wav": (0.88, [(0.00, 0.18, 392.00, 0.25, -0.2), (0.10, 0.23, 587.33, 0.31, 0.0), (0.25, 0.43, 783.99, 0.25, 0.2), (0.42, 0.30, 1174.66, 0.12, 0.0)]),
    "starship_hazard_warning.wav": (0.90, [(0.00, 0.22, 440.00, 0.34, -0.08), (0.30, 0.24, 349.23, 0.33, 0.08), (0.63, 0.22, 440.00, 0.30, 0.0)]),
    "starship_courier_relay.wav": (0.72, [(0.00, 0.14, 698.46, 0.28, -0.2), (0.14, 0.18, 932.33, 0.29, 0.0), (0.32, 0.27, 1174.66, 0.24, 0.2)]),
    "starship_survey_cache.wav": (0.86, [(0.00, 0.22, 329.63, 0.27, -0.1), (0.18, 0.22, 493.88, 0.25, 0.0), (0.39, 0.35, 783.99, 0.27, 0.1)]),
    "starship_debris_crossing.wav": (0.88, [(0.00, 0.18, 587.33, 0.31, -0.08), (0.25, 0.18, 415.30, 0.31, 0.08), (0.50, 0.18, 587.33, 0.28, -0.08), (0.74, 0.12, 415.30, 0.25, 0.08)]),
    "starship_custom_event.wav": (0.58, [(0.00, 0.17, 587.33, 0.27, -0.1), (0.17, 0.28, 783.99, 0.24, 0.1)]),
    "starship_emergency_return.wav": (1.10, [(0.00, 0.25, 659.25, 0.25, 0.0), (0.27, 0.27, 493.88, 0.30, 0.0), (0.58, 0.40, 329.63, 0.34, 0.0), (0.86, 0.18, 246.94, 0.22, 0.0)]),
    "starship_boarding_open.wav": (0.92, [(0.00, 0.18, 523.25, 0.28, -0.15), (0.16, 0.20, 659.25, 0.30, 0.0), (0.36, 0.36, 783.99, 0.27, 0.15)]),
}


def envelope(age, duration):
    attack = min(1.0, age / 0.012)
    release = min(1.0, max(0.0, (duration - age) / min(0.20, duration * 0.45)))
    return attack * release * (0.82 + 0.18 * (1.0 - age / duration))


def render(duration, notes):
    frame_count = round(duration * SAMPLE_RATE)
    left = [0.0] * frame_count
    right = [0.0] * frame_count
    for start, note_duration, frequency, gain, pan in notes:
        first = round(start * SAMPLE_RATE)
        count = min(round(note_duration * SAMPLE_RATE), frame_count - first)
        left_gain = gain * (1.0 - max(0.0, pan))
        right_gain = gain * (1.0 + min(0.0, pan))
        phase = 0.0
        for offset in range(count):
            age = offset / SAMPLE_RATE
            phase += 2.0 * pi * frequency / SAMPLE_RATE
            fundamental = sin(phase)
            harmonic = sin(phase * 2.01) * 0.16
            sample = (fundamental + harmonic) * envelope(age, note_duration)
            index = first + offset
            left[index] += sample * left_gain
            right[index] += sample * right_gain

    peak = max(max(abs(value) for value in left), max(abs(value) for value in right), 1e-9)
    scale = 0.78 / peak
    output = array("h")
    for l_value, r_value in zip(left, right):
        output.append(round(max(-1.0, min(1.0, l_value * scale)) * 32767))
        output.append(round(max(-1.0, min(1.0, r_value * scale)) * 32767))
    return output


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for filename, (duration, notes) in CUES.items():
        samples = render(duration, notes)
        with wave.open(str(OUTPUT / filename), "wb") as wav:
            wav.setnchannels(2)
            wav.setsampwidth(2)
            wav.setframerate(SAMPLE_RATE)
            wav.writeframes(samples.tobytes())
        print(f"{filename}: {duration:.2f}s, stereo PCM16, {SAMPLE_RATE} Hz")


if __name__ == "__main__":
    main()
