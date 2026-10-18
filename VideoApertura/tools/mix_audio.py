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
# Toma de ElevenLabs v4, voz «Camila», 48,4 s, con frases enteras y fluidas. Se corta solo en
# los silencios entre frases (medidos: todos por debajo de -80 dB) y el texto de src/main.js
# aparece cuando la voz lo dice.
VOICE_CUTS = [
    (0.085, 7.785, 0.425),    # Son las siete de la mañana. La cirugía es a las nueve… y todo el caso está en 267 imágenes.
    (7.785, 13.14, 8.245),    # Hoy se revisan una por una, y el cuerpo se termina armando… en la cabeza.
    (13.14, 15.355, 13.86),   # ¿Y si pudieran verlo completo?
    (15.355, 18.855, 18.875),  # Les presentamos Inmersia. Cirugía inmersiva. («Inmersia» con el golpe, en 20 s)
    (18.855, 22.145, 24.203),  # Cada órgano aparece segmentado, con su volumen.
    (22.145, 23.435, 27.985),  # Lo toman con la mano,
    (23.435, 28.54, 30.315),  # lo cortan donde quieran… y la tomografía los acompaña, en tres planos.
    (28.54, 31.11, 37.5),     # Y esto ya funciona en Meta Quest tres.
    (31.11, 39.71, 41.43),    # Lo que viene es el modo cirugía: marcar la zona… antes de entrar al quirófano.
    (39.71, 44.39, 50.95),    # Hoy es un prototipo. Para llevarlo al quirófano, buscamos un aliado clínico.
    (44.39, 47.705, 56.59),   # Construyámoslo juntos, con la Clínica Anglo Americana.
    (47.705, 48.4, 60.095),   # Inmersia.
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
