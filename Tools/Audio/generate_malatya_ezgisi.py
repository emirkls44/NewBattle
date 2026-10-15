"""
Malatya halay havasinda OZGUN bir ezgi (sifirdan bestelendi; hicbir sarkidan alinmadi).
Kayisi temasi icin: baglamaya benzeyen telli calgi (tezene tremolosu), alttan dem
(drone), davul ve def. 9/8 aksak usul (2+2+2+3), La merkezli Anadolu dizisi.

Calistir:   python Tools/Audio/generate_malatya_ezgisi.py
Cikti:      Assets/GameArt/Audio/Music/malatya_ezgisi.wav  (dongu olarak calinabilir)
"""

import os
import wave

import numpy as np

RATE = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "GameArt", "Audio", "Music")
rng = np.random.default_rng(44)

EIGHTH = 0.19                 # sekizlik (saniye)
BAR = 9                       # 9/8: 2+2+2+3
GROUPS = [0, 2, 4, 6]         # usul vuruslari (sekizlik)

# La merkezli dizi: La Si Do Re Mi Fa# Sol La' Si' Do' (derece -> yarim ses, La3 = 0)
SCALE = [0, 2, 3, 5, 7, 9, 10, 12, 14, 15]
BASE = 220.0                  # La3


def freq(degree, octave=0):
    return BASE * 2 ** ((SCALE[degree] + 12 * octave) / 12)


# Ozgun ezgi: her olcu (derece, sekizlik) ciftleri, toplam 9.
PHRASE_A = [
    [(4, 2), (5, 2), (4, 2), (3, 3)],
    [(2, 2), (3, 2), (4, 2), (2, 3)],
    [(1, 2), (2, 2), (3, 1), (2, 1), (1, 3)],
    [(0, 2), (1, 2), (2, 2), (0, 3)],
]
PHRASE_B = [
    [(7, 2), (6, 2), (5, 2), (7, 3)],
    [(6, 2), (5, 2), (4, 2), (5, 3)],
    [(4, 2), (3, 2), (2, 1), (3, 1), (4, 3)],
    [(3, 2), (2, 2), (1, 2), (0, 3)],
]
PHRASE_C = [
    [(4, 1), (5, 1), (6, 2), (5, 2), (4, 3)],
    [(7, 2), (8, 2), (7, 2), (6, 3)],
    [(5, 2), (4, 2), (3, 1), (4, 1), (5, 3)],
    [(4, 2), (3, 2), (2, 2), (1, 3)],
]
PHRASE_D = [
    [(2, 2), (3, 2), (4, 2), (5, 3)],
    [(4, 2), (3, 2), (2, 2), (3, 3)],
    [(2, 1), (1, 1), (2, 2), (1, 2), (0, 3)],
    [(0, 9)],
]
SONG = PHRASE_A + PHRASE_B + PHRASE_A + PHRASE_C + PHRASE_D


def pluck(f, seconds, brightness=0.55):
    """Karplus-Strong: baglama benzeri telli ses."""
    n = int(RATE * seconds)
    period = max(2, int(RATE / f))
    buf = rng.uniform(-1, 1, period)
    out = np.empty(n)
    for i in range(n):
        out[i] = buf[i % period]
        buf[i % period] = brightness * buf[i % period] + (1 - brightness) * buf[(i + 1) % period]
        buf[i % period] *= 0.996
    return out


def string_note(f, seconds):
    """Kisa notada tek tezene, uzun notada tremolo (art arda vurus)."""
    total = np.zeros(int(RATE * seconds))
    strokes = 1 if seconds < EIGHTH * 1.5 else int(seconds / (EIGHTH / 2))
    step = seconds / strokes
    for s in range(strokes):
        start = int(RATE * s * step)
        length = min(len(total) - start, int(RATE * min(seconds - s * step + 0.25, 0.9)))
        tone = pluck(f, length / RATE) + 0.35 * pluck(f * 2, length / RATE, 0.4)
        gain = 1.0 if s == 0 else 0.55
        total[start:start + length] += tone[:length] * gain
    return total


def drum(seconds, low):
    t = np.arange(int(RATE * seconds)) / RATE
    if low:  # dum: davul
        pitch = 70 + 60 * np.exp(-t * 30)
        body = np.sin(2 * np.pi * np.cumsum(pitch) / RATE) * np.exp(-t * 9)
        return body + 0.15 * rng.uniform(-1, 1, len(t)) * np.exp(-t * 40)
    # tek: def
    hit = rng.uniform(-1, 1, len(t)) * np.exp(-t * 35)
    ring = np.sin(2 * np.pi * 420 * t) * np.exp(-t * 25)
    return 0.6 * hit + 0.3 * ring


def place(track, sound, start):
    s = int(RATE * start)
    e = min(len(track), s + len(sound))
    if e > s:
        track[s:e] += sound[:e - s]


def build():
    total_eighths = BAR * len(SONG)
    length = total_eighths * EIGHTH
    melody = np.zeros(int(RATE * (length + 1.0)))
    drone = np.zeros_like(melody)
    perc = np.zeros_like(melody)

    position = 0
    for bar_index, bar in enumerate(SONG):
        bar_start = bar_index * BAR
        for degree, eighths in bar:
            f = freq(degree)
            place(melody, string_note(f, eighths * EIGHTH), position * EIGHTH)
            position += eighths

        # Dem: her olcunun basinda pes La, 3'luk grupta Mi
        place(drone, pluck(freq(0, -1), BAR * EIGHTH * 0.6, 0.7) * 0.8, bar_start * EIGHTH)
        place(drone, pluck(freq(4, -1), 3 * EIGHTH, 0.7) * 0.5, (bar_start + 6) * EIGHTH)

        # Usul: dum . tek . dum . tek . tek
        pattern = [(0, True), (2, False), (4, True), (6, False), (8, False)]
        for slot, low in pattern:
            place(perc, drum(0.5, low) * (1.0 if low else 0.55), (bar_start + slot) * EIGHTH)

    mix = 0.55 * melody + 0.30 * drone + 0.45 * perc
    mix = mix[:int(RATE * length)]

    # Basit oda yankisi
    for delay, gain in ((0.045, 0.25), (0.11, 0.15), (0.19, 0.08)):
        d = int(RATE * delay)
        mix[d:] += gain * mix[:-d]

    # Dongu dikisi yumusak olsun
    fade = int(RATE * 0.04)
    mix[:fade] *= np.linspace(0, 1, fade)
    mix[-fade:] *= np.linspace(1, 0, fade)
    return mix / (np.max(np.abs(mix)) + 1e-9) * 0.85


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    audio = build()
    path = os.path.join(OUT, "malatya_ezgisi.wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((audio * 32767).astype(np.int16).tobytes())
    print(f"ok {path} ({len(audio) / RATE:.1f} sn)")
