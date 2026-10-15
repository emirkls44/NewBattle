"""
Malatya davul-zurna halay havasinda OZGUN bir ezgi (sifirdan bestelendi; hicbir
sarkidan alinmadi). Kayisi / Malatya temasi icin.

  - Zurna: genizden gelen tiz ses (formant), vibrato, notalar arasi kayma, carpma
    (ustten susleme) ve sondaki tril. Ezgi tek bir surekli nefes gibi sentezlenir.
  - Dem zurnasi: arkada surekli La.
  - Davul: pes "gum" (tokmak) ve tiz "tak" (cubuk), 2/4 halay usulu.
  - Dizi: La Huseyni, Anadolu ara sesleriyle (Si koma bemol, Fa ara ses).

Calistir:   python Tools/Audio/generate_malatya_ezgisi.py
Cikti:      Assets/GameArt/Audio/Music/malatya_ezgisi.wav  (dongu)
"""

import os
import wave

import numpy as np

RATE = 32000
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "GameArt", "Audio", "Music")
rng = np.random.default_rng(1453)

BPM = 138
SIXTEENTH = 60.0 / BPM / 4
BAR = 8  # 2/4 = sekiz onaltilik

# La Huseyni: sent (La = 0). Si ve Fa Bati dizisinde olmayan ara seslerdir.
CENTS = {"A": 0, "B": 150, "C": 300, "D": 500, "E": 700, "F": 850, "G": 1000,
         "a": 1200, "b": 1350, "c": 1500, "R": None}
ORDER = ["A", "B", "C", "D", "E", "F", "G", "a", "b", "c"]
ZURNA_LA = 440.0

# Ozgun ezgi: (nota, onaltilik). "R" = sus. Her olcu 8 onaltilik.
INTRO = [
    [("a", 8)],
    [("G", 2), ("a", 2), ("G", 1), ("F", 1), ("E", 2)],
    [("F", 2), ("E", 2), ("D", 1), ("C", 1), ("B", 2)],
    [("A", 6), ("R", 2)],
]
VERSE = [
    [("E", 2), ("E", 2), ("F", 1), ("E", 1), ("D", 2)],
    [("C", 2), ("D", 2), ("B", 2), ("C", 2)],
    [("D", 1), ("C", 1), ("B", 2), ("A", 2), ("B", 2)],
    [("A", 6), ("R", 2)],
    [("E", 2), ("E", 2), ("F", 1), ("E", 1), ("D", 2)],
    [("C", 2), ("D", 2), ("E", 4)],
    [("D", 1), ("C", 1), ("B", 1), ("C", 1), ("B", 2), ("A", 2)],
    [("A", 8)],
]
CHORUS = [
    [("a", 2), ("G", 2), ("a", 2), ("G", 1), ("F", 1)],
    [("E", 2), ("F", 2), ("G", 4)],
    [("F", 1), ("G", 1), ("F", 1), ("E", 1), ("D", 2), ("E", 2)],
    [("E", 6), ("R", 2)],
    [("a", 2), ("G", 2), ("F", 1), ("G", 1), ("F", 1), ("E", 1)],
    [("D", 2), ("E", 2), ("D", 1), ("C", 1), ("B", 2)],
    [("C", 1), ("B", 1), ("A", 2), ("B", 1), ("C", 1), ("B", 2)],
    [("A", 8)],
]
SONG = INTRO + VERSE + CHORUS + VERSE + CHORUS


def hz(name, octave=0):
    return ZURNA_LA * 2 ** ((CENTS[name] / 1200) + octave)


def step_above(name):
    i = ORDER.index(name)
    return ORDER[min(i + 1, len(ORDER) - 1)]


# ---------------------------------------------------------------- zurna

def melody_curves(song):
    """Tum ezgi icin tek surekli perde ve ses egrisi (zurnaci nefes almadan calar gibi)."""
    total = sum(d for bar in song for _, d in bar) * SIXTEENTH
    n = int(RATE * total)
    f = np.full(n, hz("A"))
    amp = np.zeros(n)
    t0 = 0.0
    last = hz("A")
    events = [(name, d) for bar in song for name, d in bar]
    for idx, (name, dur) in enumerate(events):
        seconds = dur * SIXTEENTH
        s, e = int(RATE * t0), int(RATE * (t0 + seconds))
        t0 += seconds
        if name == "R":
            amp[s:e] = 0.0
            continue
        target = hz(name)
        seg = e - s
        tt = np.arange(seg) / RATE
        curve = np.full(seg, target)

        # Onceki notadan kisa kayma (portamento)
        glide = min(seg, int(RATE * 0.025))
        curve[:glide] = np.linspace(last, target, glide)

        # Carpma: uzunca notalarin basinda ustten kisa susleme
        if dur >= 2 and rng.random() < 0.55:
            grace = min(seg // 3, int(RATE * 0.05))
            curve[:grace] = hz(step_above(name))

        # Son La'da tril (La - Si)
        is_last = idx == len(events) - 1 or (dur >= 6 and name == "A")
        if is_last:
            trill_len = int(seg * 0.55)
            flip = (np.arange(trill_len) // int(RATE * 0.055)) % 2
            curve[:trill_len] = np.where(flip == 0, target, hz("B"))

        # Vibrato uzun notalarda derinlesir
        depth = 0.004 if dur < 3 else 0.012
        curve *= 1 + depth * np.sin(2 * np.pi * 6.2 * tt) * np.clip(tt / 0.12, 0, 1)
        f[s:e] = curve
        last = target

        # Ses: dil vurusu (tekrarlanan notada belirgin), hafif sonum
        env = np.ones(seg)
        attack = min(seg, int(RATE * 0.012))
        env[:attack] = np.linspace(0.35, 1.0, attack)
        dip = min(seg, int(RATE * 0.02))
        env[-dip:] *= np.linspace(1.0, 0.7, dip)
        amp[s:e] = env * (0.92 + 0.08 * rng.random())
    return f, amp


def zurna(f, amp, brightness=1.0):
    """Genizden gelen cift kamisli ses: zengin harmonikler + iki formant + nefes."""
    phase = 2 * np.pi * np.cumsum(f) / RATE
    out = np.zeros(len(f))
    base = float(np.median(f))
    for h in range(1, 18):
        fh = h * base
        if fh > RATE / 2 - 500:
            break
        formant = 1 + 2.4 * np.exp(-((fh - 1250) / 420) ** 2) + 1.6 * np.exp(-((fh - 2700) / 650) ** 2)
        weight = formant / h ** (0.75 / brightness)
        out += weight * np.sin(h * phase + h * 0.7)
    out = np.tanh(out * 0.9)  # kamis sertligi
    breath = rng.normal(0, 1, len(f))
    breath = np.convolve(breath, np.ones(6) / 6, mode="same")
    return (out + 0.05 * breath) * amp


# ---------------------------------------------------------------- davul

def gum(seconds=0.45):
    t = np.arange(int(RATE * seconds)) / RATE
    pitch = 55 + 70 * np.exp(-t * 22)
    body = np.sin(2 * np.pi * np.cumsum(pitch) / RATE) * np.exp(-t * 6.5)
    thump = rng.normal(0, 1, len(t)) * np.exp(-t * 60)
    thump = np.convolve(thump, np.ones(12) / 12, mode="same")
    return 1.0 * body + 0.6 * thump


def tak(seconds=0.12, strength=1.0):
    t = np.arange(int(RATE * seconds)) / RATE
    noise = rng.normal(0, 1, len(t))
    hp = noise - np.convolve(noise, np.ones(4) / 4, mode="same")  # tiz
    skin = np.sin(2 * np.pi * 330 * t) * np.exp(-t * 45)
    return strength * (0.8 * hp * np.exp(-t * 55) + 0.5 * skin)


def drums(bars, intro_bars):
    n = int(RATE * bars * BAR * SIXTEENTH) + RATE
    out = np.zeros(n)

    def put(sound, sixteenth):
        s = int(RATE * sixteenth * SIXTEENTH)
        e = min(n, s + len(sound))
        out[s:e] += sound[:e - s]

    for bar in range(bars):
        base = bar * BAR
        if bar < intro_bars:
            # Girisde sadece tek tuk gum: zurna serbest acar
            if bar == intro_bars - 1:
                put(gum(), base + 4)
                put(tak(), base + 6)
                put(tak(strength=0.8), base + 7)
            continue
        # Halay usulu: GUM . tak tak | GUM tak . tak  (+ hafif ara vuruslar)
        put(gum(), base + 0)
        put(tak(), base + 2)
        put(tak(), base + 3)
        put(gum(), base + 4)
        put(tak(), base + 5)
        put(tak(), base + 7)
        put(tak(strength=0.35), base + 1)
        put(tak(strength=0.35), base + 6)
        # Her dort olcude bir doldurma
        if bar % 4 == 3:
            for k in (4.5, 5, 5.5, 6, 6.5, 7, 7.5):
                put(tak(strength=0.6), base + k)
    return out


# ---------------------------------------------------------------- karisim

def build():
    f, amp = melody_curves(SONG)
    lead = zurna(f, amp)

    # Dem zurnasi: bir oktav pes La, nefes nefes hafif dalgalanir
    n = len(f)
    tt = np.arange(n) / RATE
    dem_f = np.full(n, hz("A", -1)) * (1 + 0.003 * np.sin(2 * np.pi * 5.1 * tt))
    dem_amp = 0.85 + 0.15 * np.sin(2 * np.pi * 0.3 * tt)
    intro_end = int(RATE * len(INTRO) * BAR * SIXTEENTH)
    dem_amp[:intro_end] *= np.linspace(0.0, 1.0, intro_end)
    dem = zurna(dem_f, dem_amp, brightness=0.8)

    bars = len(SONG)
    perc = drums(bars, len(INTRO))[:n]

    mix = 0.60 * lead + 0.22 * dem + 0.75 * perc

    # Acik hava yankisi (meydan)
    for delay, gain in ((0.07, 0.22), (0.16, 0.14), (0.29, 0.07)):
        d = int(RATE * delay)
        mix[d:] += gain * mix[:-d]

    fade = int(RATE * 0.05)
    mix[:fade] *= np.linspace(0, 1, fade)
    mix[-fade:] *= np.linspace(1, 0, fade)
    return mix / (np.max(np.abs(mix)) + 1e-9) * 0.88


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
