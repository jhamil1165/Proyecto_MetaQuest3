"""
Compone la música del video: una pieza original, sintetizada aquí mismo, sin derechos de terceros.

Estilo de anuncio de tecnología médica: un colchón cálido en Re mayor, un pulso suave que da
avance durante la demostración, campanitas de cristal arriba y un acorde final que se apaga
dejando silencio para que hable el expositor. Los cambios caen en los cortes de escena del
video (CUES, en segundos del video).

Uso (desde VideoApertura):
  python3 tools/compose_music.py out/musica.wav
"""

import sys

import numpy as np
from scipy.signal import fftconvolve

SR = 48000
DUR = 57.0
RNG = np.random.default_rng(7)

# Cortes de escena del video (deben coincidir con src/main.js).
CUES = {
    "intro": 0.0,       # pantalla con cortes: intriga
    "question": 4.9,    # la pregunta
    "bloom": 9.3,       # los cortes forman el cuerpo
    "space": 14.3,      # en tu espacio
    "demo": 19.5,       # órganos, plano, tres planos
    "real": 34.9,       # captura real
    "vision": 38.9,     # la meta: cirugía, con ustedes
    "close": 47.4,      # nombre y cierre
    "end": 53.0,        # todo en silencio desde aquí
}

N = int(SR * DUR)
t = np.arange(N) / SR


def hz(note):
    """'D3' -> frecuencia."""
    names = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3, "G": -2,
             "G#": -1, "A": 0, "A#": 1, "B": 2}
    name, octave = note[:-1], int(note[-1])
    return 440.0 * 2 ** ((names[name] + 12 * (octave - 4)) / 12)


def env_adsr(n, a, r, sustain=1.0):
    e = np.ones(n) * sustain
    na, nr = int(a * SR), int(r * SR)
    na, nr = min(na, n), min(nr, n)
    if na:
        e[:na] = np.linspace(0, sustain, na) ** 1.5
    if nr:
        e[-nr:] *= np.linspace(1, 0, nr) ** 2
    return e


def place(buf, start, sig, gain=1.0):
    i0 = int(start * SR)
    if i0 >= len(buf):
        return
    sig = sig[: len(buf) - i0]
    buf[i0:i0 + len(sig)] += sig * gain


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
    # brillo que respira despacio
    out *= (0.85 + 0.15 * np.sin(2 * np.pi * 0.12 * tt + RNG.uniform(0, 6)))[:, None]
    return out / (3 * harmonics ** 0.5)


def pad_chord(notes, start, dur, gain, a=2.5, r=3.0):
    n = int(dur * SR)
    acc = np.zeros((n, 2))
    for note in notes:
        acc += pad_note(hz(note), dur)
    acc *= env_adsr(n, a, r)[:, None]
    place(music, start, acc, gain)


def bell(note, start, gain=0.12, decay=2.2, pan=0.0):
    f = hz(note)
    n = int(decay * 2.5 * SR)
    tt = np.arange(n) / SR
    s = (np.sin(2 * np.pi * f * tt) + 0.35 * np.sin(2 * np.pi * f * 2.0 * tt) * np.exp(-tt * 3)
         + 0.12 * np.sin(2 * np.pi * f * 3.01 * tt) * np.exp(-tt * 6))
    s *= np.exp(-tt / decay) * env_adsr(n, 0.004, 0.05)
    st = np.stack([s * (1 - pan) * 0.7, s * (1 + pan) * 0.7], axis=1)
    place(music, start, st, gain)


def pulse(note, start, gain=0.25, length=0.32):
    f = hz(note)
    n = int(length * SR)
    tt = np.arange(n) / SR
    s = np.sin(2 * np.pi * f * tt) + 0.25 * np.sin(2 * np.pi * 2 * f * tt)
    s *= np.exp(-tt / (length / 3)) * env_adsr(n, 0.01, 0.05)
    place(music, start, np.stack([s, s], axis=1), gain)


def noise_swell(start, dur, gain=0.08, rising=True):
    n = int(dur * SR)
    w = RNG.standard_normal((n, 2))
    # paso bajo de un polo con corte que sube o baja
    cut = np.linspace(250, 4200, n) if rising else np.linspace(4200, 250, n)
    alpha = 1 - np.exp(-2 * np.pi * cut / SR)
    y = np.zeros_like(w)
    acc = np.zeros(2)
    for i in range(n):
        acc += alpha[i] * (w[i] - acc)
        y[i] = acc
    e = np.linspace(0, 1, n) ** 2 if rising else np.linspace(1, 0, n) ** 2
    place(music, start, y * e[:, None], gain)


def reverb(x, seconds=3.2, wet=0.35):
    n = int(seconds * SR)
    tt = np.arange(n) / SR
    ir = RNG.standard_normal((n, 2)) * np.exp(-tt / (seconds / 6.9))[:, None]
    ir[:, 0] *= 1.0
    ir /= np.sqrt((ir ** 2).sum(axis=0))
    y = np.stack([fftconvolve(x[:, c], ir[:, c])[: len(x)] for c in range(2)], axis=1)
    return x * (1 - wet) + y * wet * 2.2


music = np.zeros((N, 2))
C = CUES

# --- Intriga: un fondo grave y campanitas sueltas, como cortes que pasan ---
pad_chord(["D2", "A2"], C["intro"], C["bloom"] - C["intro"] + 1.5, 0.22, a=3.0, r=2.0)
for i, s in enumerate(np.arange(1.2, C["question"] - 0.3, 1.25)):
    bell(["A5", "F#5", "D6", "E5"][i % 4], s, gain=0.05, decay=1.4, pan=(-0.4, 0.4)[i % 2])
# la pregunta: se abre un poco y sube un aire hacia la formación del cuerpo
pad_chord(["D3", "A3", "E4"], C["question"], C["bloom"] - C["question"] + 1.0, 0.13, a=2.0, r=1.5)
noise_swell(C["bloom"] - 2.6, 2.6, gain=0.035)

# --- El cuerpo aparece: se abre el acorde completo ---
prog = [
    (["D3", "A3", "C#4", "E4", "F#4"], 0),   # Dmaj9
    (["B2", "F#3", "A3", "D4", "E4"], 1),    # Bm11
    (["G2", "D3", "F#3", "B3", "E4"], 2),    # Gmaj9 (6/9)
    (["A2", "E3", "G3", "B3", "D4"], 3),     # A7sus
]
bar = 3.0  # segundos por acorde en la demostración
start = C["bloom"]
k = 0
while start < C["close"] - 0.5:
    notes, _ = prog[k % len(prog)]
    length = min(bar * (2 if start < C["demo"] else 1), C["close"] - start)
    level = 0.2 if start < C["demo"] else 0.17
    if C["real"] <= start < C["vision"]:
        level = 0.14
    pad_chord(notes, start, length + 2.0, level, a=1.6, r=2.4)
    start += length
    k += 1
bell("F#6", C["bloom"] + 0.1, gain=0.08, decay=2.8)
bell("A5", C["bloom"] + 0.35, gain=0.06, decay=2.8, pan=0.3)

# --- Demostración: pulso suave a 80 BPM y arpegio de cristal ---
beat = 60 / 80
roots = {0: "D2", 1: "B1", 2: "G1", 3: "A1"}
s = C["demo"]
i = 0
while s < C["real"] - 0.2:
    chord_idx = int((s - C["bloom"]) // bar) if s >= C["bloom"] else 0
    root = roots[chord_idx % 4]
    pulse(root, s, gain=0.22 if i % 2 == 0 else 0.12)
    s += beat / 2
    i += 1
arp = ["D5", "F#5", "A5", "E5", "B5", "A5", "F#5", "C#6"]
s = C["space"] + 0.4
i = 0
while s < C["real"] - 0.4:
    bell(arp[i % len(arp)], s, gain=0.035 if s < C["demo"] else 0.045, decay=1.6, pan=0.35 * np.sin(i))
    s += beat
    i += 1

# --- Captura real: respiro (sin pulso) ---
noise_swell(C["vision"] - 1.6, 1.6, gain=0.022)

# --- Visión: vuelve el pulso, más abierto, y sube hacia el nombre ---
s = C["vision"]
i = 0
while s < C["close"] - 0.4:
    pulse("D2" if (i // 8) % 2 == 0 else "B1", s, gain=0.18 if i % 2 == 0 else 0.09)
    s += beat / 2
    i += 1
s = C["vision"] + 0.3
i = 0
while s < C["close"] - 0.3:
    bell(arp[(i * 3) % len(arp)], s, gain=0.05, decay=1.8, pan=0.4 * np.cos(i))
    s += beat
    i += 1
noise_swell(C["close"] - 2.0, 2.0, gain=0.028)

# --- Cierre: acorde final que se apaga antes del silencio ---
final = ["D2", "A2", "D3", "A3", "C#4", "E4", "F#4", "A4"]
pad_chord(final, C["close"], C["end"] - C["close"] + 0.5, 0.22, a=0.25, r=4.5)
for j, nte in enumerate(["D5", "A5", "F#6"]):
    bell(nte, C["close"] + 0.05 + 0.18 * j, gain=0.09, decay=3.2, pan=(-0.3, 0.0, 0.3)[j])
pulse("D1", C["close"], gain=0.35, length=1.6)

# --- mezcla ---
music = reverb(music)
fade = np.ones(N)
f0, f1 = int(C["end"] * SR) - int(3.0 * SR), int(C["end"] * SR)
fade[f0:f1] = np.linspace(1, 0, f1 - f0) ** 1.5
fade[f1:] = 0
fade[: int(0.6 * SR)] = np.linspace(0, 1, int(0.6 * SR))
music *= fade[:, None]
music /= np.max(np.abs(music)) + 1e-9
music *= 0.6


def write_wav(path, x):
    import wave
    x16 = np.clip(x, -1, 1)
    x16 = (x16 * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(x16.tobytes())


if __name__ == "__main__":
    write_wav(sys.argv[1] if len(sys.argv) > 1 else "out/musica.wav", music)
    print("listo")
