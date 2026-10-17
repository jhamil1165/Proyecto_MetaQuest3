"""
Compone la música del video de INMERSIA: una pieza original, sintetizada aquí mismo, sin
derechos de terceros.

Estilo de anuncio corto (reel), pero sobrio: 120 BPM, un compás = 2 s. Empieza con un reloj
y un fondo tenso (son las 7:00, la cirugía es a las 9:00), suena un clic por cada corte que se
revisa «uno por uno», sube hasta un golpe en Re mayor cuando aparece INMERSIA y entra el ritmo
durante la demostración. Respira en la captura real, vuelve con más fuerza en la hoja de ruta
y cierra con un último golpe en el logo, que se apaga en silencio. Los cambios caen en los
segundos de CUES, que son los del guion de src/main.js.

Uso (desde VideoApertura):
  python3 tools/compose_music.py out/musica.wav
"""

import sys

import numpy as np
from scipy.signal import butter, fftconvolve, sosfilt

SR = 48000
DUR = 61.0
BEAT = 0.5          # 120 BPM
BAR = 4 * BEAT      # 2 s
RNG = np.random.default_rng(11)

# Escenas del guion (segundos del video; deben coincidir con src/main.js).
CUES = {
    "hook": 0.0,        # quirófano 3, 7:00 → 9:00
    "count": 3.95,      # «267» cuenta desde cero
    "problem": 6.0,     # una por una… en la cabeza
    "question": 10.0,   # ¿y si pudieran verlo completo?
    "brand": 16.0,      # INMERSIA (golpe)
    "demo": 20.0,       # órganos, mano, plano, tres planos
    "real": 34.0,       # captura real (respiro)
    "stats": 38.0,      # cifras
    "road": 42.0,       # hoja de ruta
    "ask": 48.0,        # buscamos un aliado clínico
    "close": 52.0,      # construyámoslo junto a la clínica
    "logo": 56.0,       # logo (golpe final)
}
# Entradas de bloques de texto: un soplo suave en cada cambio de escena.
WHOOSH = [3.4, 6.0, 10.1, 20.0, 23.0, 26.0, 30.4, 34.1, 38.1, 42.1, 48.0, 52.0]

N = int(SR * DUR)
t = np.arange(N) / SR
music = np.zeros((N, 2))   # armonía: colchones, bajo, campanas (con reverberación larga)
drums = np.zeros((N, 2))   # ritmo (casi seco)
fx = np.zeros((N, 2))      # reloj, clics, soplos, golpes


def hz(note):
    """'D3' -> frecuencia."""
    names = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3, "G": -2,
             "G#": -1, "A": 0, "A#": 1, "B": 2}
    name, octave = note[:-1], int(note[-1])
    return 440.0 * 2 ** ((names[name] + 12 * (octave - 4)) / 12)


def env_adsr(n, a, r, sustain=1.0):
    e = np.ones(n) * sustain
    na, nr = min(int(a * SR), n), min(int(r * SR), n)
    if na:
        e[:na] = np.linspace(0, sustain, na) ** 1.5
    if nr:
        e[-nr:] *= np.linspace(1, 0, nr) ** 2
    return e


def place(buf, start, sig, gain=1.0):
    i0 = int(round(start * SR))
    if i0 >= len(buf):
        return
    sig = sig[: len(buf) - i0]
    if sig.ndim == 1:
        sig = np.stack([sig, sig], axis=1)
    buf[i0:i0 + len(sig)] += sig * gain


def band(x, lo=None, hi=None, order=2):
    if lo and hi:
        sos = butter(order, [lo, hi], btype="band", fs=SR, output="sos")
    elif lo:
        sos = butter(order, lo, btype="high", fs=SR, output="sos")
    else:
        sos = butter(order, hi, btype="low", fs=SR, output="sos")
    return sosfilt(sos, x, axis=0)


# ---------- instrumentos ----------
def pad_note(f, dur, detune_cents=7, harmonics=9, bright=1.6):
    n = int(dur * SR)
    tt = np.arange(n) / SR
    out = np.zeros((n, 2))
    for ch, sign in enumerate((-1, 1)):
        for d in (-detune_cents, 0, detune_cents):
            ff = f * 2 ** ((d + sign * 2) / 1200)
            ph = RNG.uniform(0, 2 * np.pi)
            for k in range(1, harmonics + 1):
                out[:, ch] += np.sin(2 * np.pi * ff * k * tt + ph * k) / k ** bright
    out *= (0.85 + 0.15 * np.sin(2 * np.pi * 0.12 * tt + RNG.uniform(0, 6)))[:, None]
    return out / (3 * harmonics ** 0.5)


def pad_chord(notes, start, dur, gain, a=1.2, r=1.5):
    n = int(dur * SR)
    acc = np.zeros((n, 2))
    for note in notes:
        acc += pad_note(hz(note), dur)
    acc *= env_adsr(n, a, r)[:, None]
    place(music, start, acc, gain)


def bell(note, start, gain=0.1, decay=1.8, pan=0.0, buf=None):
    f = hz(note)
    n = int(decay * 2.5 * SR)
    tt = np.arange(n) / SR
    s = (np.sin(2 * np.pi * f * tt) + 0.35 * np.sin(2 * np.pi * f * 2.0 * tt) * np.exp(-tt * 3)
         + 0.12 * np.sin(2 * np.pi * f * 3.01 * tt) * np.exp(-tt * 6))
    s *= np.exp(-tt / decay) * env_adsr(n, 0.003, 0.05)
    place(music if buf is None else buf, start, np.stack([s * (1 - pan) * 0.7, s * (1 + pan) * 0.7], axis=1), gain)


def bass(note, start, length, gain=0.2):
    f = hz(note)
    n = int(length * SR)
    tt = np.arange(n) / SR
    s = sum(np.sin(2 * np.pi * f * k * tt) / k for k in range(1, 8))
    s = band(s, hi=520) * np.exp(-tt / (length * 0.7)) * env_adsr(n, 0.006, 0.04)
    place(music, start, s, gain)


def _kick():
    n = int(0.42 * SR)
    tt = np.arange(n) / SR
    f = 46 + 80 * np.exp(-tt * 30)
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 7.5)
    s[: int(0.004 * SR)] += RNG.standard_normal(int(0.004 * SR)) * 0.25
    return s


def _clap():
    n = int(0.3 * SR)
    tt = np.arange(n) / SR
    e = np.zeros(n)
    for k, off in enumerate((0.0, 0.012, 0.024)):
        i = int(off * SR)
        e[i:] += np.exp(-(tt[: n - i]) / (0.008 if k < 2 else 0.11)) * (0.7 if k < 2 else 1.0)
    return band(RNG.standard_normal(n), 900, 4200) * e


def _hat(open_=False):
    n = int((0.22 if open_ else 0.06) * SR)
    tt = np.arange(n) / SR
    return band(RNG.standard_normal(n), lo=7500) * np.exp(-tt / (0.07 if open_ else 0.014))


KICK, CLAP, HAT, OHAT = _kick(), _clap(), _hat(), _hat(True)


def tick(start, high=True, gain=0.12):
    n = int(0.03 * SR)
    tt = np.arange(n) / SR
    s = band(RNG.standard_normal(n), 2600 if high else 1500, 6500 if high else 3800) * np.exp(-tt / 0.005)
    s += 0.5 * np.sin(2 * np.pi * (3400 if high else 2300) * tt) * np.exp(-tt / 0.004)
    place(fx, start, s, gain)


def swell(start, dur, gain, lo=250, hi=5000, rising=True, buf=None):
    """Ruido filtrado que sube (o baja): aire antes de un cambio."""
    n = int(dur * SR)
    w = RNG.standard_normal((n, 2))
    cut = np.geomspace(lo, hi, n) if rising else np.geomspace(hi, lo, n)
    alpha = 1 - np.exp(-2 * np.pi * cut / SR)
    y = np.zeros_like(w)
    acc = np.zeros(2)
    for i in range(n):
        acc += alpha[i] * (w[i] - acc)
        y[i] = acc
    e = np.linspace(0, 1, n) ** 2.2 if rising else np.linspace(1, 0, n) ** 2
    place(fx if buf is None else buf, start, y * e[:, None], gain)


def whoosh(at, gain=0.05):
    """Soplo corto centrado en `at`: sube y baja en 0,5 s."""
    n = int(0.5 * SR)
    w = RNG.standard_normal((n, 2))
    x = np.linspace(0, 1, n)
    cut = 500 + 5500 * np.sin(np.pi * x) ** 2
    alpha = 1 - np.exp(-2 * np.pi * cut / SR)
    y = np.zeros_like(w)
    acc = np.zeros(2)
    for i in range(n):
        acc += alpha[i] * (w[i] - acc)
        y[i] = acc
    pan = np.stack([1 - 0.4 * x, 0.6 + 0.4 * x], axis=1)
    place(fx, at - 0.3, y * (np.sin(np.pi * x) ** 2)[:, None] * pan, gain)


def impact(at, gain=1.0):
    """Golpe: bombo, sub que cae y un platillo suave."""
    place(drums, at, KICK, 0.5 * gain)
    n = int(2.4 * SR)
    tt = np.arange(n) / SR
    f = 34 + 30 * np.exp(-tt * 6)
    place(fx, at, np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt / 0.7), 0.3 * gain)
    m = int(2.2 * SR)
    tc = np.arange(m) / SR
    crash = band(RNG.standard_normal((m, 2)), lo=4500) * np.exp(-tc / 0.55)[:, None]
    place(fx, at, crash, 0.05 * gain)
    swell(at - 1.6, 1.6, 0.06 * gain, lo=400, hi=9000)


def groove(t0, t1, kick=True, clap=True, hats=True, open_hats=True, level=1.0):
    s = t0
    i = 0
    while s < t1 - 1e-6:
        beat = i % 4
        if kick and beat in (0, 2):
            place(drums, s, KICK, 0.42 * level)
        if kick and beat == 3 and (i // 4) % 2 == 1:
            place(drums, s + BEAT / 2, KICK, 0.22 * level)
        if clap and beat in (1, 3):
            place(drums, s, CLAP, 0.16 * level)
        if hats:
            place(drums, s + BEAT / 2, OHAT if open_hats else HAT, (0.05 if open_hats else 0.06) * level)
            place(drums, s, HAT, 0.035 * level)
        s += BEAT
        i += 1


C = CUES
# Progresión de la demostración: Bm7 · Gmaj7 · D · A (un compás cada uno)
PROG = [
    (["B2", "F#3", "A3", "D4"], "B1"),
    (["G2", "D3", "F#3", "B3"], "G1"),
    (["D3", "F#3", "A3", "E4"], "D2"),
    (["A2", "E3", "G3", "C#4"], "A1"),
]


def harmony(t0, t1, level, bass_level=0.2, eighths=True, bells_on=True):
    s, k = t0, 0
    arp = ["F#5", "A5", "D6", "A5", "E5", "F#5", "B5", "A5"]
    while s < t1 - 1e-6:
        notes, root = PROG[k % 4]
        length = min(BAR, t1 - s)
        pad_chord(notes, s, length + 0.6, level, a=0.15, r=0.7)
        steps = int(round(length / (BEAT / 2 if eighths else BEAT)))
        for j in range(steps):
            st = s + j * (BEAT / 2 if eighths else BEAT)
            bass(root if j % 4 != 3 else root.replace("1", "2"), st, 0.24, bass_level * (1.0 if j % 2 == 0 else 0.7))
        if bells_on:
            for j in range(4):
                bell(arp[(k * 4 + j) % len(arp)], s + j * BEAT + BEAT / 2, gain=0.03, decay=0.9, pan=0.4 * np.sin(k + j))
        s += length
        k += 1


# --- Gancho: reloj y un fondo tenso en Si menor ---
pad_chord(["B1", "F#2", "B2", "F#3"], 0.0, C["question"] + 0.8, 0.32, a=0.25, r=1.2)
for s in np.arange(0.0, C["count"] - 0.1, 1.0):
    tick(s, high=int(s) % 2 == 0, gain=0.22)
    place(drums, s, KICK, 0.16)
# «267»: los clics se aceleran mientras cuenta
times = C["count"] + 1.1 * (1 - (1 - np.linspace(0, 1, 30)) ** 2.2)
for j, s in enumerate(times):
    tick(s, high=True, gain=0.08 + 0.06 * j / 30)
bell("F#5", C["count"] + 1.15, gain=0.05, decay=1.6)
# «una por una»: un clic por cada corte que avanza (uno por tiempo)
for s in np.arange(C["problem"] + 0.5, C["question"] - 0.4, BEAT):
    tick(s, high=False, gain=0.17)
pad_chord(["G1", "D2", "B2", "D3"], C["problem"], C["question"] - C["problem"] + 0.8, 0.2, a=0.8, r=1.0)
place(drums, C["problem"], KICK, 0.35)
# «en la cabeza» tachado: un golpe seco
place(drums, 8.85, KICK, 0.3)

# --- La pregunta: se abre, y sube hacia la marca ---
pad_chord(["E2", "B2", "D3", "F#3", "G3"], C["question"], 2 * BAR + 0.6, 0.22, a=0.6, r=0.8)
pad_chord(["G2", "D3", "F#3", "A3", "B3"], C["question"] + 2 * BAR, BAR + 0.4, 0.23, a=0.3, r=0.5)
for j, s in enumerate(np.arange(C["question"], C["brand"] - 0.1, BEAT)):
    place(drums, s, KICK, 0.12 + 0.18 * j / 12)
swell(C["brand"] - 3.0, 3.0, 0.07, lo=200, hi=7000)
for j, s in enumerate(np.arange(C["brand"] - 2.0, C["brand"] - 0.05, BEAT / 2)):
    tick(s, high=True, gain=0.03 + 0.05 * j / 8)

# --- INMERSIA: golpe en Re mayor y espacio ---
impact(C["brand"])
pad_chord(["D2", "A2", "D3", "F#3", "A3", "E4"], C["brand"], C["demo"] - C["brand"] + 0.5, 0.24, a=0.03, r=1.2)
for j, nte in enumerate(["D5", "A5", "F#6", "A6"]):
    bell(nte, C["brand"] + 0.06 + 0.12 * j, gain=0.08, decay=2.6, pan=(-0.35, 0.0, 0.35, 0.0)[j])
for s in np.arange(C["brand"] + BAR, C["demo"], BEAT):
    bass("D2", s, 0.3, 0.12)
groove(C["brand"] + BAR, C["demo"], kick=True, clap=False, hats=True, open_hats=False, level=0.7)
swell(C["demo"] - 1.0, 1.0, 0.04)

# --- Demostración: el ritmo completo ---
harmony(C["demo"], C["real"], 0.15)
groove(C["demo"], C["real"])

# --- Captura real: respiro, sin batería ---
pad_chord(["G2", "D3", "F#3", "B3", "E4"], C["real"], BAR + 0.6, 0.22, a=0.3, r=0.8)
pad_chord(["A2", "E3", "G3", "C#4", "E4"], C["real"] + BAR, BAR + 0.4, 0.22, a=0.3, r=0.5)
for s in np.arange(C["real"], C["stats"], BEAT):
    place(drums, s + BEAT / 2, HAT, 0.03)
for j, nte in enumerate(["B5", "A5", "F#5", "E5", "C#6", "A5", "E5", "A5"]):
    bell(nte, C["real"] + j * BEAT, gain=0.04, decay=1.4, pan=0.3 * np.cos(j))
swell(C["stats"] - 1.4, 1.4, 0.05)

# --- Cifras, hoja de ruta y lo que buscamos: vuelve el ritmo, más abierto ---
impact(C["stats"], gain=0.5)
harmony(C["stats"], C["close"], 0.16, bass_level=0.22)
groove(C["stats"], C["close"] - BEAT)
for s in np.arange(C["road"], C["close"] - BAR, BEAT / 2):   # brillo extra en la hoja de ruta
    bell(["D6", "A5", "F#6", "E6"][int((s - C["road"]) / (BEAT / 2)) % 4], s, gain=0.018, decay=0.5, pan=0.5 * np.sin(s))
# redoble hacia el cierre
for j, s in enumerate(np.arange(C["close"] - BEAT, C["close"], BEAT / 4)):
    place(drums, s, CLAP, 0.06 + 0.04 * j)

# --- Cierre: la batería se va, queda la invitación; sube hacia el logo ---
impact(C["close"], gain=0.45)
pad_chord(["B2", "F#3", "A3", "D4", "E4"], C["close"], BAR + 0.5, 0.24, a=0.05, r=0.6)
pad_chord(["G2", "D3", "F#3", "B3", "E4"], C["close"] + BAR, BAR + 0.3, 0.24, a=0.3, r=0.4)
for s in np.arange(C["close"], C["logo"], BEAT):
    bass(["B1", "B1", "B1", "B1", "G1", "G1", "A1", "A1"][int((s - C["close"]) / BEAT)], s, 0.4, 0.14)
swell(C["logo"] - 2.0, 2.0, 0.06, lo=300, hi=8000)

# --- Logo: golpe final en Re mayor que se apaga ---
impact(C["logo"], gain=1.1)
final = ["D2", "A2", "D3", "A3", "C#4", "E4", "F#4", "A4"]
pad_chord(final, C["logo"], DUR - C["logo"], 0.26, a=0.03, r=4.2)
for j, nte in enumerate(["D5", "A5", "F#6", "D6"]):
    bell(nte, C["logo"] + 0.06 + 0.16 * j, gain=0.09, decay=3.0, pan=(-0.3, 0.0, 0.3, 0.0)[j])
bass("D1", C["logo"], 2.6, 0.3)

for w in WHOOSH:
    whoosh(w)


def reverb(x, seconds, wet):
    n = int(seconds * SR)
    tt = np.arange(n) / SR
    ir = RNG.standard_normal((n, 2)) * np.exp(-tt / (seconds / 6.9))[:, None]
    ir /= np.sqrt((ir ** 2).sum(axis=0))
    y = np.stack([fftconvolve(x[:, c], ir[:, c])[: len(x)] for c in range(2)], axis=1)
    return x * (1 - wet) + y * wet * 2.2


# --- mezcla ---
# la armonía «respira» con el bombo durante el ritmo (bombeo suave)
pump = np.ones(N)
kick_times = [s for t0, t1 in ((C["demo"], C["real"]), (C["stats"], C["close"] - BEAT))
              for s in np.arange(t0, t1, 2 * BEAT)]
for s in kick_times:
    i0 = int(s * SR)
    n = min(int(0.4 * SR), N - i0)
    pump[i0:i0 + n] *= 1 - 0.3 * np.exp(-np.arange(n) / SR / 0.11)
mix = reverb(music, 3.0, 0.32) * pump[:, None] + reverb(drums, 0.9, 0.08) + reverb(fx, 2.2, 0.22)
fade = np.ones(N)
f0 = int((DUR - 1.2) * SR)
fade[f0:] = np.linspace(1, 0, N - f0) ** 2
fade[: int(0.02 * SR)] = np.linspace(0, 1, int(0.02 * SR))
mix *= fade[:, None]

# Nivel: el ritmo de la demostración queda en GROOVE_RMS y los picos (golpes, bombos) se
# redondean con un limitador suave por encima de KNEE, sin pasar de CEIL (-2,5 dB). Queda en
# unos -16 LUFS, el nivel habitual para video en línea y proyección.
GROOVE_RMS, KNEE, CEIL = -17.0, 0.5, 0.75
a, b = int(C["demo"] * SR), int(C["real"] * SR)
mix *= 10 ** (GROOVE_RMS / 20) / np.sqrt((mix[a:b] ** 2).mean())
over = np.abs(mix) > KNEE * CEIL
k0 = KNEE * CEIL
mix[over] = np.sign(mix[over]) * (k0 + (CEIL - k0) * np.tanh((np.abs(mix[over]) - k0) / (CEIL - k0)))


def write_wav(path, x):
    import wave
    x16 = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(x16.tobytes())


if __name__ == "__main__":
    write_wav(sys.argv[1] if len(sys.argv) > 1 else "out/musica.wav", mix)
    print("listo")
