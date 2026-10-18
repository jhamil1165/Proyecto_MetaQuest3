"""
Arma la banda sonora del video: locución + música, y la pone en el MP4.

La locución (assets/audio/locucion_inmersia.mp3) llega en una sola toma. Se separa solo en sus
pausas naturales y cada frase entra cuando su escena lo pide: la voz nunca se acelera ni se
recorta dentro de una frase. La música (tools/compose_music.py) baja sola cuando habla la voz.

Uso (desde VideoApertura):
  python3 tools/compose_music.py out/musica.wav
  python3 tools/mix_audio.py out/video_apertura.mp4
Genera:
  out/inmersia.mp4             el video final, con voz y música
  out/inmersia_musica.mp4      solo con música, para narrar en vivo
  out/banda_sonora.mp3         voz + música (la usa la vista previa)
"""

import subprocess
import sys
import wave

import numpy as np

try:
    import imageio_ffmpeg
    FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
except ImportError:
    FFMPEG = "ffmpeg"

DURATION = 66.0
VOICE = "assets/audio/locucion_inmersia.mp3"
MUSIC = "out/musica.wav"

# (inicio en la toma, fin en la toma, segundo del video donde entra)
# Toma de ElevenLabs v4, voz «Camila», 52,6 s, dirigida como la del video de 57 s: suave, de
# anuncio, frases cortas con pausas. Se corta solo en los silencios entre frases (todos por
# debajo de -75 dB) y el texto de src/main.js aparece cuando la voz lo dice.
VOICE_CUTS = [
    (0.18, 16.265, 0.45),      # Toda cirugía… empieza antes del quirófano. … 267 imágenes… ¿Y si pudieras verlo completo… frente a ti?
    (16.265, 20.545, 18.425),  # Te presentamos… Inmersia. Cirugía inmersiva. («Inmersia» con el golpe, en 20 s)
    (20.545, 24.36, 24.205),   # Cada órgano, segmentado… y con su volumen.
    (24.74, 26.3, 28.04),      # Tómalo con la mano…
    (26.3, 29.785, 30.38),     # córtalo donde quieras… y la tomografía te sigue.
    (29.785, 32.26, 33.945),   # Tres planos. Un mismo punto.
    (32.26, 34.565, 37.54),    # Y ya funciona en Meta Quest tres.
    (34.565, 43.61, 40.845),   # Lo que viene… es el modo cirugía. Marca la zona… antes de entrar al quirófano.
    (43.61, 48.305, 50.88),    # Hoy, es un prototipo. Para llevarlo al quirófano… buscamos un aliado clínico.
    (48.305, 51.75, 56.475),   # Construyámoslo juntos… con la Clínica Anglo Americana.
    (51.75, 52.64, 60.06),     # Inmersia.
]
SR = 48000
FADE = 0.015   # fundido en cada corte, para que no haga clic


def place_voice(path_in, path_out):
    """Corta la toma en sus silencios y pone cada frase en su segundo del video (sin acelerarla)."""
    raw = subprocess.run([FFMPEG, "-v", "error", "-i", path_in, "-ac", "1", "-ar", str(SR), "-f", "s16le", "-"],
                         check=True, capture_output=True).stdout
    take = np.frombuffer(raw, "<i2").astype(np.float32) / 32768
    out = np.zeros(int(DURATION * SR), np.float32)
    nf = int(FADE * SR)
    ramp = np.linspace(0, 1, nf, dtype=np.float32)
    for a, b, at in VOICE_CUTS:
        seg = take[int(a * SR):int(b * SR)].copy()
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
    # 1) voz colocada, ecualizada y nivelada
    place_voice(VOICE, "out/voz_cortes.wav")
    run(["-i", "out/voz_cortes.wav", "-af",
         "highpass=f=80,equalizer=f=200:t=q:w=1.0:g=1.0,equalizer=f=7000:t=q:w=1.2:g=-1.0,"
         "acompressor=threshold=-22dB:ratio=2.2:attack=15:release=200:makeup=1.5,"
         "loudnorm=I=-16:TP=-2:LRA=9,aformat=sample_rates=48000:channel_layouts=stereo",
         "-t", str(DURATION), "out/voz_colocada.wav"])
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
