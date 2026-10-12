"""
NewBattle ses ureteci: oyunun tum ses efektlerini ve menu muzigini sifirdan
(sentezle) uretir. Indirme yok, lisans derdi yok.

Calistir:   python Tools/Audio/generate_sounds.py
Cikti:      Assets/GameArt/Audio/Sfx/*.wav  ve  Assets/GameArt/Audio/Music/*.wav

Bir sesi begenmezsen ayni ADLA baska bir .wav koyman yeterli; ya da Unity'de
SesKutuphanesi dosyasindan o sese baska bir klip ata.
"""

import os
import wave

import numpy as np

RATE = 22050
ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "GameArt", "Audio")
rng = np.random.default_rng(7)


# ---------------------------------------------------------------- temel parcalar

def t_axis(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def env(seconds, attack=0.005, decay=None, curve=4.0):
    """Hizli yukselip ustel sonen zarf."""
    t = t_axis(seconds)
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    d = np.exp(-t * (curve / (decay or seconds)))
    return a * d


def adsr(seconds, attack=0.01, release=0.08):
    t = t_axis(seconds)
    a = np.clip(t / attack, 0, 1)
    r = np.clip((seconds - t) / release, 0, 1)
    return a * r


def sine(freq, seconds, phase=0.0):
    t = t_axis(seconds)
    if np.ndim(freq):
        return np.sin(2 * np.pi * np.cumsum(freq) / RATE + phase)
    return np.sin(2 * np.pi * freq * t + phase)


def square(freq, seconds, duty=0.5):
    t = t_axis(seconds)
    ph = np.cumsum(np.full(len(t), freq) if not np.ndim(freq) else freq) / RATE
    return np.where((ph % 1.0) < duty, 1.0, -1.0)


def triangle(freq, seconds):
    t = t_axis(seconds)
    ph = (np.cumsum(np.full(len(t), freq) if not np.ndim(freq) else freq) / RATE) % 1.0
    return 4 * np.abs(ph - 0.5) - 1


def saw(freq, seconds):
    t = t_axis(seconds)
    ph = np.cumsum(np.full(len(t), freq) if not np.ndim(freq) else freq) / RATE
    return 2 * (ph % 1.0) - 1


def noise(seconds):
    return rng.uniform(-1, 1, int(RATE * seconds))


def lowpass(x, cutoff):
    """Tek kutuplu alcak geciren (cutoff sabit ya da dizi)."""
    y = np.zeros_like(x)
    cut = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    alpha = 1 - np.exp(-2 * np.pi * cut / RATE)
    acc = 0.0
    for i in range(len(x)):
        acc += alpha[i] * (x[i] - acc)
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def sweep(f0, f1, seconds, curve=1.0):
    t = t_axis(seconds) / seconds
    return f0 + (f1 - f0) * t ** curve


def pad(x, seconds):
    n = int(RATE * seconds)
    return np.concatenate([x, np.zeros(max(0, n - len(x)))])


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[: len(p)] += p
    return out


def at(x, start, total):
    out = np.zeros(int(RATE * total))
    s = int(RATE * start)
    e = min(len(out), s + len(x))
    out[s:e] += x[: e - s]
    return out


def echo(x, delay=0.12, feedback=0.35, taps=3):
    out = x.copy()
    d = int(RATE * delay)
    for k in range(1, taps + 1):
        shifted = np.zeros_like(out)
        if d * k < len(out):
            shifted[d * k:] = x[: len(out) - d * k] * (feedback ** k)
        out += shifted
    return out


def note(n):
    """MIDI nota -> Hz."""
    return 440.0 * 2 ** ((n - 69) / 12)


def normalize(x, peak=0.9):
    m = np.max(np.abs(x)) or 1.0
    return x / m * peak


def write(folder, name, x, peak=0.9):
    path = os.path.join(ROOT, folder)
    os.makedirs(path, exist_ok=True)
    x = normalize(x, peak)
    # kenar tiklamasin
    fade = min(len(x) // 10, int(RATE * 0.004))
    if fade > 0:
        x[:fade] *= np.linspace(0, 1, fade)
        x[-fade:] *= np.linspace(1, 0, fade)
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(path, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())
    print(f"  {folder}/{name}.wav  ({len(x) / RATE:.2f} sn)")


def sfx(name, x, peak=0.9):
    write("Sfx", name, x, peak)


# ---------------------------------------------------------------- arayuz

def ui_sounds():
    # Buton: yumusak, kisa "pop".
    d = 0.09
    f = sweep(900, 520, d, 0.6)
    sfx("buton", sine(f, d) * env(d, 0.002, d, 6) + 0.25 * sine(f * 2, d) * env(d, 0.001, d, 9), 0.7)

    # Kart / ziplama: hafif "whup".
    d = 0.16
    sfx("kart", sine(sweep(260, 620, d, 0.5), d) * env(d, 0.01, d, 5) * 0.8
        + lowpass(noise(d), 1800) * env(d, 0.005, d, 8) * 0.4, 0.6)

    # Para: iki parlak nota + parilti.
    d = 0.5
    a = (sine(note(88), 0.25) + 0.4 * sine(note(100), 0.25)) * env(0.25, 0.002, 0.25, 6)
    b = (sine(note(93), 0.35) + 0.4 * sine(note(105), 0.35)) * env(0.35, 0.002, 0.35, 5)
    sfx("para", mix(at(a, 0, d), at(b, 0.07, d)), 0.7)

    # Satin alma: kasa "cha-ching".
    d = 0.8
    hit = lowpass(noise(0.08), 4000) * env(0.08, 0.001, 0.08, 10)
    bell = sum(sine(note(n), 0.6) * env(0.6, 0.002, 0.6, 4) * g for n, g in ((91, 1), (96, 0.6), (103, 0.3)))
    bell2 = sum(sine(note(n), 0.6) * env(0.6, 0.002, 0.6, 4) * g for n, g in ((95, 1), (100, 0.6), (107, 0.3)))
    sfx("satin_al", mix(at(hit, 0, d), at(bell, 0.03, d), at(bell2, 0.14, d)), 0.75)

    # Hata: kisa, alcak "bzz".
    d = 0.22
    sfx("hata", lowpass(square(150, d) * 0.6 + square(158, d) * 0.6, 1200) * adsr(d, 0.005, 0.05), 0.5)

    # XP: yukselen pirilti.
    d = 0.45
    out = np.zeros(int(RATE * d))
    for i, n in enumerate((76, 81, 86, 91)):
        tone = (triangle(note(n), 0.18) * 0.7 + sine(note(n + 12), 0.18) * 0.3) * env(0.18, 0.002, 0.18, 5)
        out += at(tone, i * 0.06, d)
    sfx("xp", out, 0.7)

    # Odul / hediye acilisi: sihirli arpej + parilti.
    d = 1.1
    out = np.zeros(int(RATE * d))
    for i, n in enumerate((72, 76, 79, 84, 88, 91)):
        tone = (sine(note(n), 0.5) + 0.3 * sine(note(n) * 2.01, 0.5)) * env(0.5, 0.003, 0.5, 4)
        out += at(tone, i * 0.07, d)
    sparkle = highpass(noise(d), 6000) * env(d, 0.05, d, 3) * 0.25
    sfx("odul", out + sparkle, 0.75)

    # Seviye atlama: kisa fanfar.
    d = 1.4
    out = np.zeros(int(RATE * d))
    seq = ((0.0, 67, 0.14), (0.14, 72, 0.14), (0.28, 76, 0.14), (0.42, 79, 0.9))
    for start, n, length in seq:
        tone = (saw(note(n), length) * 0.35 + square(note(n), length, 0.25) * 0.25 + sine(note(n + 12), length) * 0.2)
        tone = lowpass(tone, 3500) * adsr(length, 0.01, min(0.25, length * 0.5))
        out += at(tone, start, d)
    chord = sum(sine(note(n), 0.9) for n in (60, 64, 67)) * env(0.9, 0.02, 0.9, 2.5) * 0.35
    sfx("seviye_atla", out + at(chord, 0.42, d), 0.8)


# ---------------------------------------------------------------- lobi / inis

def lobby_sounds():
    # Geri sayim bip.
    d = 0.16
    sfx("geri_sayim", (square(880, d, 0.5) * 0.4 + sine(880, d) * 0.6) * adsr(d, 0.003, 0.06), 0.6)

    # Basla: korna (iki akor, vibratolu).
    d = 0.9
    vib = 1 + 0.006 * np.sin(2 * np.pi * 6 * t_axis(d))
    horn = sum(saw(note(n) * vib, d) for n in (55, 62, 67)) / 3
    horn = lowpass(horn, 2200) * adsr(d, 0.03, 0.25)
    sfx("basla", horn, 0.8)

    # Ruzgar dongusu (serbest dusus): dalgalanan filtreli gurultu, dikissiz dongu.
    d = 3.0
    t = t_axis(d)
    cutoff = 700 + 500 * np.sin(2 * np.pi * t / d) + 250 * np.sin(2 * np.pi * 3 * t / d + 1)
    wind = lowpass(noise(d), cutoff)
    wind = highpass(wind, 120)
    # dongu: bastan ve sondan capraz gecis
    x = int(RATE * 0.3)
    wind[:x] = wind[:x] * np.linspace(0, 1, x) + wind[-x:] * np.linspace(1, 0, x)
    wind = wind[: len(wind) - x]
    write("Sfx", "ruzgar", wind, 0.7)

    # Parasut acilisi: "fumpp".
    d = 0.5
    body = lowpass(noise(d), sweep(2500, 300, d, 0.5)) * env(d, 0.01, d, 5)
    thump = sine(sweep(140, 60, d), d) * env(d, 0.005, d, 7) * 0.8
    sfx("parasut", body + thump, 0.8)

    # Inis: tok "gum" + toz.
    d = 0.5
    thud = sine(sweep(110, 45, d, 0.7), d) * env(d, 0.002, d, 7)
    dust = lowpass(noise(d), 900) * env(d, 0.003, d, 9) * 0.6
    sfx("inis", thud + dust, 0.85)


# ---------------------------------------------------------------- savas

def gun(name, crack_cut, body_freq, length, tail=0.0, boom=0.0, peak=0.85):
    d = length + tail
    crack = lowpass(noise(d), crack_cut) * env(d, 0.0008, length, 7)
    body = sine(sweep(body_freq, body_freq * 0.45, d, 0.5), d) * env(d, 0.001, length, 6) * 0.9
    low = sine(sweep(80, 40, d), d) * env(d, 0.002, length * 1.5, 5) * boom
    x = crack + body + low
    if tail > 0:
        x = echo(x, 0.09, 0.3, 3)
    sfx(name, x, peak)


def combat_sounds():
    gun("ates_tufek", 5000, 220, 0.16, tail=0.12, boom=0.4)
    gun("ates_agir", 3800, 160, 0.24, tail=0.2, boom=0.9, peak=0.95)
    gun("ates_tabanca", 6500, 320, 0.1, tail=0.06, boom=0.2, peak=0.8)
    gun("ates_pompali", 2800, 120, 0.3, tail=0.18, boom=1.0, peak=0.95)

    # Yumruk: hava "vuuş" + tok vurus.
    d = 0.25
    whoosh = lowpass(noise(d), sweep(600, 3000, d)) * env(d, 0.04, d, 6) * 0.5
    punch = sine(sweep(160, 70, 0.12), 0.12) * env(0.12, 0.001, 0.12, 8)
    sfx("yumruk", mix(whoosh, at(punch, 0.08, d)), 0.8)

    # Isabet: vurdugunu hissettiren kuru "tik".
    d = 0.07
    sfx("isabet", (sine(1900, d) * 0.6 + highpass(noise(d), 3000) * 0.4) * env(d, 0.0005, d, 9), 0.55)

    # Oldurme: iki notali zil.
    d = 0.9
    def bell(n, length):
        return (sine(note(n), length) + 0.5 * sine(note(n) * 2.76, length) + 0.25 * sine(note(n) * 5.4, length)) \
            * env(length, 0.001, length, 4)
    sfx("oldurme", mix(at(bell(84, 0.6), 0, d), at(bell(91, 0.7), 0.1, d)), 0.75)

    # Hasar alma: bogucu darbe.
    d = 0.2
    sfx("hasar_al", (sine(sweep(200, 90, d), d) + lowpass(noise(d), 700) * 0.5) * env(d, 0.002, d, 7), 0.75)

    # Olum: inen ton.
    d = 1.0
    tone = (triangle(sweep(520, 140, d, 0.8), d) * 0.6 + sine(sweep(260, 70, d, 0.8), d) * 0.5)
    sfx("olum", lowpass(tone, 2000) * adsr(d, 0.01, 0.4), 0.7)

    # Esya toplama: kisa parlak "bling".
    d = 0.3
    sfx("toplama", mix(at(sine(note(86), 0.2) * env(0.2, 0.002, 0.2, 6), 0, d),
                       at(sine(note(93), 0.25) * env(0.25, 0.002, 0.25, 6), 0.05, d)), 0.6)

    # Adim: yumusak toprak/cim.
    d = 0.08
    sfx("adim", lowpass(noise(d), 1100) * env(d, 0.003, d, 9) + sine(90, d) * env(d, 0.001, d, 12) * 0.4, 0.5)


# ---------------------------------------------------------------- alan / sonuc

def zone_and_result_sounds():
    # Alan daraliyor: iki tonlu siren.
    d = 1.6
    t = t_axis(d)
    f = np.where((t * 2.5) % 1 < 0.5, 620, 470)
    siren = lowpass(square(f, d, 0.5) * 0.4 + sine(f, d) * 0.6, 2500) * adsr(d, 0.02, 0.2)
    sfx("alan_daraliyor", siren, 0.65)

    # Alan disi kalp atisi: cift tok vurus.
    d = 0.6
    beat = lambda: sine(sweep(70, 40, 0.14), 0.14) * env(0.14, 0.002, 0.14, 6)
    sfx("alan_kalp", mix(at(beat(), 0, d), at(beat() * 0.7, 0.17, d)), 0.85)

    # Zafer jingle.
    d = 2.4
    out = np.zeros(int(RATE * d))
    melody = ((0.0, 72, 0.15), (0.15, 72, 0.15), (0.3, 72, 0.15), (0.45, 76, 0.45),
              (0.9, 74, 0.15), (1.05, 76, 0.15), (1.2, 79, 1.1))
    for start, n, length in melody:
        tone = (square(note(n), length, 0.25) * 0.35 + triangle(note(n), length) * 0.5)
        out += at(lowpass(tone, 4000) * adsr(length, 0.008, min(0.2, length * 0.5)), start, d)
    bass = mix(at(triangle(note(48), 0.9) * adsr(0.9, 0.01, 0.2), 0, d),
               at(triangle(note(55), 1.2) * adsr(1.2, 0.01, 0.4), 1.2, d)) * 0.6
    sfx("zafer", out + bass, 0.8)

    # Elenme jingle: yavas inen.
    d = 2.0
    out = np.zeros(int(RATE * d))
    for i, n in enumerate((67, 66, 65, 62)):
        length = 0.38 if i < 3 else 0.9
        tone = triangle(note(n), length) * 0.6 + sine(note(n - 12), length) * 0.3
        out += at(lowpass(tone, 2500) * adsr(length, 0.01, 0.15), i * 0.38, d)
    sfx("elenme", out, 0.7)


# ---------------------------------------------------------------- menu muzigi

def menu_music():
    """110 bpm, 8 olcu, dikissiz donen neseli chiptune."""
    bpm = 110
    beat = 60 / bpm
    bars = 8
    total = bars * 4 * beat
    out = np.zeros(int(RATE * total))

    chords = [(48, 52, 55), (45, 48, 52), (41, 45, 48), (43, 47, 50)] * 2  # C Am F G
    melody = [
        76, None, 79, 76, 74, None, 72, None,
        72, None, 74, 76, 74, None, None, None,
        72, None, 76, 77, 79, None, 77, 76,
        74, None, 72, 74, 71, None, None, None,
        76, None, 79, 81, 79, None, 76, None,
        72, None, 74, 76, 72, None, None, None,
        77, None, 76, 74, 72, None, 74, 76,
        74, None, None, 71, 72, None, None, None,
    ]
    eighth = beat / 2

    for bar, chord in enumerate(chords):
        bar_start = bar * 4 * beat
        # bas: kok + besli, sekizlik
        for i in range(8):
            n = chord[0] - 12 if i % 2 == 0 else chord[2] - 12
            tone = triangle(note(n), eighth * 0.95) * adsr(eighth * 0.95, 0.005, 0.03) * 0.55
            out += at(tone, bar_start + i * eighth, total)
        # akor: yumusak kare, vurus 2 ve 4
        for b in (1, 3):
            length = beat * 0.8
            tone = sum(square(note(n), length, 0.25) for n in chord) / 3
            out += at(lowpass(tone, 1800) * adsr(length, 0.005, 0.1) * 0.22, bar_start + b * beat, total)
        # davul: kick 1 ve 3, snare 2 ve 4, hi-hat sekizlik
        for b in (0, 2):
            kick = sine(sweep(120, 45, 0.15), 0.15) * env(0.15, 0.001, 0.15, 6) * 0.8
            out += at(kick, bar_start + b * beat, total)
        for b in (1, 3):
            snare = (highpass(noise(0.12), 1500) * 0.6 + sine(190, 0.12) * 0.3) * env(0.12, 0.001, 0.12, 8) * 0.45
            out += at(snare, bar_start + b * beat, total)
        for i in range(8):
            hat = highpass(noise(0.04), 7000) * env(0.04, 0.001, 0.04, 10) * (0.18 if i % 2 else 0.1)
            out += at(hat, bar_start + i * eighth, total)

    for i, n in enumerate(melody):
        if n is None:
            continue
        length = eighth * 0.9
        # sonraki bos ise notayi uzat
        j = i + 1
        while j < len(melody) and melody[j] is None and j - i < 4:
            length += eighth
            j += 1
        tone = square(note(n), length, 0.5) * 0.3 + triangle(note(n), length) * 0.4
        out += at(lowpass(tone, 3200) * adsr(length, 0.006, 0.06) * 0.5, i * eighth, total)

    write("Music", "menu_muzigi", out, 0.75)


if __name__ == "__main__":
    print("Sesler uretiliyor...")
    ui_sounds()
    lobby_sounds()
    combat_sounds()
    zone_and_result_sounds()
    menu_music()
    print("Bitti.")
