"""
Arma la banda sonora del video: locución + música, y la pone en el MP4.

La locución (assets/audio/locucion_inmersia.mp3) llega en una sola toma. Se separa solo en sus
pausas naturales y cada frase entra cuando su escena lo pide: la voz nunca se acelera ni se
recorta dentro de una frase. La música (tools/compose_music.py) baja sola cuando habla la voz.
Si no está la toma, sale solo la versión con música.

Uso (desde VideoApertura):
  python3 tools/compose_music.py out/musica.wav
  python3 tools/mix_audio.py out/video_apertura.mp4
Genera:
  out/inmersia.mp4             el video final, con voz y música
  out/inmersia_musica.mp4      solo con música, para narrar en vivo
  out/banda_sonora.mp3         voz + música (la usa la vista previa)
"""

import os
import subprocess
import sys
import wave

import numpy as np

try:
    import imageio_ffmpeg
    FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
except ImportError:
    FFMPEG = "ffmpeg"

DURATION = 60.0
VOICE = "assets/audio/locucion.mp3"
CAMILA_2 = "assets/audio/locucion_inmersia.mp3"   # la misma voz, otra toma: dice «Inmersia»
MUSIC = "out/musica.wav"

# (inicio en la toma, fin en la toma, segundo del video donde entra)
# Toma de ElevenLabs v4, voz «Camila», 39,5 s: la del primer video, la que más gustó. Se usa tal
# cual (sin cambiar tono ni timbre), solo las frases que siguen en el guion; el texto de
# src/main.js dice lo mismo que la voz. «Te presentamos… Inmersia. Cirugía inmersiva.» y el
# «Inmersia» final vienen de otra toma de la misma voz (CAMILA_2), con el mismo nivel. El gancho,
# «una por una» y las reglas van sin voz.
VOICE_CUTS = [
    (0.0, 2.95, 4.9),       # Cientos de imágenes… una sola pantalla.
    (3.5, 5.85, 12.0),      # ¿Y si pudieras tenerlas… frente a ti?
    (6.45, 8.55, 14.4),     # Cada corte encuentra su lugar…
    (9.05, 10.5, 16.55),    # y aparece el cuerpo.
    (16.25, 17.65, 17.9, CAMILA_2, 0.88),   # Te presentamos… (más lento: suspenso antes del nombre)
    (17.75, 18.75, 19.92, CAMILA_2),   # Inmersia. (con el golpe, en 20 s)
    (18.85, 20.545, 21.1, CAMILA_2),   # Cirugía inmersiva.
    (10.75, 14.95, 24.0),   # Con realidad mixta, la anatomía está aquí. En tu espacio.
    (17.95, 19.55, 28.15),  # Tómalo… y gíralo.
    (19.8, 23.2, 30.3),     # Desliza un plano… y la tomografía te sigue.
    (23.5, 26.0, 33.95),    # Tres planos. Un mismo punto.
    (26.4, 28.5, 45.7),     # Y ya funciona en Meta Quest 3.
    (28.95, 30.5, 47.85),   # Hoy, es un prototipo.
    (33.88, 36.3, 49.7),    # Y queremos construirlo… con ustedes.
    (51.7, 52.64, 54.0, CAMILA_2),     # Inmersia. (con el golpe del logo, en 54 s)
]
TEMPO = 1.0    # la voz va a su velocidad natural
PITCH = 1.0    # y con su tono original
SR = 48000
FADE = 0.015   # fundido en cada corte, para que no haga clic
# Solo se limpia el grave y se sube el volumen (la toma venía baja), igual en todas las frases;
# la voz suena como es.
VOICE_RMS = -16.0
SOFT = ("highpass=f=70,alimiter=limit=0.89:attack=5:release=60,aformat=sample_rates=48000:channel_layouts=stereo")


def load_take(path):
    raw = subprocess.run([FFMPEG, "-v", "error", "-i", path,
                          "-af", f"aresample={SR},asetrate={SR}*{PITCH},aresample={SR},atempo={TEMPO / PITCH}",
                          "-ac", "1", "-ar", str(SR), "-f", "s16le", "-"], check=True, capture_output=True).stdout
    return np.frombuffer(raw, "<i2").astype(np.float32) / 32768


def place_voice(path_in, path_out):
    """Corta la toma en sus silencios y pone cada frase en su segundo del video."""
    takes = {path_in: load_take(path_in)}
    out = np.zeros(int(DURATION * SR), np.float32)
    nf = int(FADE * SR)
    ramp = np.linspace(0, 1, nf, dtype=np.float32)
    for a, b, at, *opt in VOICE_CUTS:
        path = opt[0] if opt and opt[0] else path_in
        if path not in takes:
            takes[path] = load_take(path)
        seg = takes[path][int(a / TEMPO * SR):int(b / TEMPO * SR)].copy()
        if len(opt) > 1:   # esta frase, más lenta (sin cambiar el tono)
            seg = np.frombuffer(subprocess.run(
                [FFMPEG, "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", "-af", f"atempo={opt[1]}",
                 "-f", "f32le", "-"], input=seg.astype("<f4").tobytes(), check=True, capture_output=True).stdout, "<f4").copy()
        # nivel parejo: cada frase (sin contar sus silencios) queda en VOICE_RMS
        fr = seg[: len(seg) // 480 * 480].reshape(-1, 480)
        rms = np.sqrt((fr ** 2).mean(axis=1))
        seg *= 10 ** (VOICE_RMS / 20) / np.sqrt((fr[rms > rms.max() * 0.1] ** 2).mean())
        seg[:nf] *= ramp
        seg[-nf:] *= ramp[::-1]
        i0 = int(round(at * SR))
        out[i0:i0 + len(seg)] += seg[:len(out) - i0]
    with wave.open(path_out, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((np.clip(out, -1, 1) * 32767).astype("<i2").tobytes())


def run(args):
    subprocess.run([FFMPEG, "-y", "-nostdin", "-loglevel", "error", *args], check=True)


def main(video):
    if not (os.path.exists(VOICE) and VOICE_CUTS):
        print("aún no está la voz: solo la versión con música")
        if os.path.exists("out/inmersia.mp4"):
            os.remove("out/inmersia.mp4")   # la de antes tenía otro guion
        run(["-i", MUSIC, "-t", str(DURATION), "-c:a", "libmp3lame", "-b:a", "192k", "out/banda_sonora.mp3"])
        run(["-i", video, "-i", MUSIC, "-map", "0:v", "-map", "1:a", "-c:v", "copy",
             "-c:a", "aac", "-b:a", "192k", "-t", str(DURATION), "-movflags", "+faststart", "out/inmersia_musica.mp4"])
        print("listo: out/inmersia_musica.mp4")
        return
    # 1) voz colocada, nivelada
    place_voice(VOICE, "out/voz_cortes.wav")
    run(["-i", "out/voz_cortes.wav", "-af", SOFT, "-t", str(DURATION), "out/voz_colocada.wav"])
    # 2) música más baja y, con la voz como llave, agachada cuando hay locución
    run(["-i", MUSIC, "-i", "out/voz_colocada.wav", "-filter_complex",
         "[0:a]volume=-5dB[m];"
         "[1:a]asplit=2[key][v];"
         "[m][key]sidechaincompress=threshold=0.02:ratio=4:attack=40:release=450:makeup=1[md];"
         "[v][md]amix=inputs=2:normalize=0,alimiter=limit=0.89[out]",
         "-map", "[out]", "-t", str(DURATION), "out/banda_sonora.wav"])
    run(["-i", "out/banda_sonora.wav", "-c:a", "libmp3lame", "-b:a", "192k", "out/banda_sonora.mp3"])
    # 3) al video
    for audio, out in (("out/banda_sonora.wav", "out/inmersia.mp4"), (MUSIC, "out/inmersia_musica.mp4")):
        run(["-i", video, "-i", audio, "-map", "0:v", "-map", "1:a", "-c:v", "copy",
             "-c:a", "aac", "-b:a", "192k", "-t", str(DURATION), "-movflags", "+faststart", out])
        print("listo:", out)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
