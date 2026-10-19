"""
Arma la banda sonora del video: locución + música, y la pone en el MP4.

La locución (assets/audio/locucion_daniela.mp3) llega en una sola toma. Se separa solo en sus
pausas naturales y cada frase entra cuando su escena lo pide: la voz nunca se acelera ni se
recorta dentro de una frase. La música (tools/compose_music.py) baja sola cuando habla la voz.
Mientras no esté la toma, sale solo la versión con música.

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
VOICE = "assets/audio/locucion_daniela.mp3"
MUSIC = "out/musica.wav"

# (inicio en la toma, fin en la toma, segundo del video donde entra)
# Toma de ElevenLabs v4, voz «Daniela - Soothing & Relaxed»: suave, tranquila y cálida. Se corta
# solo en los silencios entre frases y el texto de src/main.js aparece cuando la voz lo dice.
# Vacía hasta que llegue la toma.
VOICE_CUTS = []
TEMPO = 1.0    # la voz va a su velocidad natural
SR = 48000
FADE = 0.015   # fundido en cada corte, para que no haga clic
# Procesado ligero: sin graves de más, menos «s», compresión suave y nivel parejo.
SOFT = ("highpass=f=80,deesser=i=0.3,"
        "acompressor=threshold=-22dB:ratio=1.6:attack=20:release=250,"
        "loudnorm=I=-17:TP=-2:LRA=9,aformat=sample_rates=48000:channel_layouts=stereo")


def place_voice(path_in, path_out):
    """Corta la toma en sus silencios y pone cada frase en su segundo del video."""
    raw = subprocess.run([FFMPEG, "-v", "error", "-i", path_in, "-af", f"atempo={TEMPO}",
                          "-ac", "1", "-ar", str(SR), "-f", "s16le", "-"], check=True, capture_output=True).stdout
    take = np.frombuffer(raw, "<i2").astype(np.float32) / 32768
    out = np.zeros(int(DURATION * SR), np.float32)
    nf = int(FADE * SR)
    ramp = np.linspace(0, 1, nf, dtype=np.float32)
    for a, b, at in VOICE_CUTS:
        seg = take[int(a / TEMPO * SR):int(b / TEMPO * SR)].copy()
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
