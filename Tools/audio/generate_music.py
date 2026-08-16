"""Synthesise the two background music loops.

Why generate rather than download
---------------------------------
The brief allows free third-party audio, but generating it keeps the soundtrack original, removes
any licence to verify, and makes the music reproducible from the repository. It also suits the
material: this is slow dark-ambient drone, which is a good fit for additive synthesis and a poor
fit for anything needing performance.

What it makes
-------------
menu_theme.wav    sparse and still, for the title screen
combat_theme.wav  the same palette with a low pulse and a slightly brighter bell line

Both are built from the same ingredients: a detuned sine/triangle pad on the root, a fifth above,
a slow amplitude LFO so the pad breathes, a filtered noise wash for air, and sparse bell tones from
a minor pentatonic. The last few seconds are cross-faded into the opening so the file loops without
a click.

Usage
-----
    Doc/.venv/Scripts/python Tools/audio/generate_music.py

Writes into Assets/_Project/Audio/. Unity imports them as Vorbis, so the uncompressed size stays in
the repository rather than in the build.
"""

from __future__ import annotations

import pathlib
import struct
import wave

import numpy as np

SAMPLE_RATE = 32_000
OUT = pathlib.Path(__file__).resolve().parents[2] / "Assets" / "_Project" / "Audio"

# D minor pentatonic, low. Chosen because every pair of these sounds consonant together, so
# sparse random ordering cannot produce a wrong note.
ROOT = 73.42  # D2
PENTATONIC = [1.0, 6 / 5, 4 / 3, 3 / 2, 9 / 5]


def _time(seconds: float) -> np.ndarray:
    return np.linspace(0.0, seconds, int(SAMPLE_RATE * seconds), endpoint=False)


def _pad(seconds: float, freq: float, *, detune: float = 0.006, level: float = 0.2) -> np.ndarray:
    """Two slightly detuned sines. The beating between them is what stops it sounding synthetic."""
    t = _time(seconds)
    a = np.sin(2 * np.pi * freq * t)
    b = np.sin(2 * np.pi * freq * (1.0 + detune) * t)
    third = 0.35 * np.sin(2 * np.pi * freq * 2 * t)
    return level * (a + b + third) / 2.35


def _breathe(seconds: float, period: float, depth: float, phase: float = 0.0) -> np.ndarray:
    t = _time(seconds)
    return 1.0 - depth + depth * (0.5 + 0.5 * np.sin(2 * np.pi * t / period + phase))


def _air(seconds: float, level: float, rng: np.random.Generator) -> np.ndarray:
    """Noise run through a cheap one-pole low pass, for a sense of room rather than hiss."""
    noise = rng.standard_normal(int(SAMPLE_RATE * seconds))
    out = np.zeros_like(noise)
    alpha = 0.0009
    running = 0.0
    for i, sample in enumerate(noise):
        running += alpha * (sample - running)
        out[i] = running
    peak = np.max(np.abs(out)) or 1.0
    return level * out / peak


def _bell(seconds: float, freq: float, start: float, level: float, decay: float) -> np.ndarray:
    """One struck tone with an exponential tail, mixed in at `start` seconds."""
    length = min(decay * 3.0, seconds - start)
    if length <= 0:
        return np.zeros(int(SAMPLE_RATE * seconds))

    t = _time(length)
    envelope = np.exp(-t / decay)
    tone = (
        np.sin(2 * np.pi * freq * t)
        + 0.45 * np.sin(2 * np.pi * freq * 2.01 * t)
        + 0.18 * np.sin(2 * np.pi * freq * 3.02 * t)
    )
    voice = level * envelope * tone / 1.63

    out = np.zeros(int(SAMPLE_RATE * seconds))
    begin = int(SAMPLE_RATE * start)
    out[begin:begin + len(voice)] += voice
    return out


def _seamless(signal: np.ndarray, fade: float = 4.0) -> np.ndarray:
    """Cross-fade the tail into the head so the loop point is inaudible."""
    n = int(SAMPLE_RATE * fade)
    if n * 2 >= len(signal):
        return signal

    head = signal[:n].copy()
    tail = signal[-n:].copy()
    ramp = np.linspace(0.0, 1.0, n)

    body = signal[:-n].copy()
    body[:n] = head * ramp + tail * (1.0 - ramp)
    return body


def _normalise(signal: np.ndarray, peak: float = 0.72) -> np.ndarray:
    highest = float(np.max(np.abs(signal))) or 1.0
    return signal * (peak / highest)


def _write(path: pathlib.Path, signal: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.clip(signal, -1.0, 1.0)
    frames = (pcm * 32767.0).astype("<i2")

    with wave.open(str(path), "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(SAMPLE_RATE)
        handle.writeframes(frames.tobytes())

    rms = float(np.sqrt(np.mean(pcm**2)))
    seam = abs(float(pcm[0]) - float(pcm[-1]))
    print(f"  {path.name}: {len(pcm) / SAMPLE_RATE:.0f}s  "
          f"peak {float(np.max(np.abs(pcm))):.3f}  rms {rms:.3f}  loop seam {seam:.4f}")


def menu_theme(seconds: float = 64.0) -> np.ndarray:
    rng = np.random.default_rng(20260817)

    signal = _pad(seconds, ROOT, level=0.30) * _breathe(seconds, 19.0, 0.45)
    signal += _pad(seconds, ROOT * 1.5, level=0.16) * _breathe(seconds, 27.0, 0.55, phase=1.4)
    signal += _pad(seconds, ROOT * 0.5, detune=0.003, level=0.18) * _breathe(seconds, 33.0, 0.30)
    signal += _air(seconds, 0.05, rng)

    # Sparse: a note every seven seconds or so, high up, so it reads as distant.
    at = 5.0
    while at < seconds - 6.0:
        step = PENTATONIC[int(rng.integers(0, len(PENTATONIC)))]
        signal += _bell(seconds, ROOT * 4 * step, at, level=0.16, decay=2.6)
        at += float(rng.uniform(6.0, 9.0))

    return _seamless(_normalise(signal, 0.62))


def combat_theme(seconds: float = 64.0) -> np.ndarray:
    rng = np.random.default_rng(20260818)

    signal = _pad(seconds, ROOT, level=0.32) * _breathe(seconds, 15.0, 0.40)
    signal += _pad(seconds, ROOT * 1.5, level=0.18) * _breathe(seconds, 21.0, 0.50, phase=0.8)
    signal += _pad(seconds, ROOT * 2, detune=0.009, level=0.10) * _breathe(seconds, 11.0, 0.6)
    signal += _air(seconds, 0.06, rng)

    # A slow heartbeat under everything: present, but well below the pads.
    t = _time(seconds)
    pulse_env = np.clip(np.sin(2 * np.pi * t / 2.0), 0.0, None) ** 6
    signal += 0.20 * pulse_env * np.sin(2 * np.pi * (ROOT * 0.5) * t)

    # Denser and a little higher than the menu, to feel like something is happening.
    at = 3.0
    while at < seconds - 5.0:
        step = PENTATONIC[int(rng.integers(0, len(PENTATONIC)))]
        octave = 4 if rng.random() < 0.7 else 6
        signal += _bell(seconds, ROOT * octave * step, at, level=0.13, decay=2.0)
        at += float(rng.uniform(3.5, 6.0))

    return _seamless(_normalise(signal, 0.70))


def main() -> None:
    print("Synthesising music into Assets/_Project/Audio/")
    _write(OUT / "menu_theme.wav", menu_theme())
    _write(OUT / "combat_theme.wav", combat_theme())
    print("done")


if __name__ == "__main__":
    main()
