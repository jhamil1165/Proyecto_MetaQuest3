"""
Arma la banda sonora del video: locución + música, y la pone en el MP4.

La locución (assets/audio/locucion.mp3) llega en una sola toma. Se separa solo en sus pausas
naturales y cada frase entra cuando su escena lo pide: la voz nunca se acelera ni se recorta
dentro de una frase. La música (tools/compose_music.py) baja sola cuando habla la voz.

Uso (desde VideoApertura):
  python3 tools/compose_music.py out/musica.wav
  python3 tools/mix_audio.py out/video_apertura.mp4
Genera:
  assets/audio/banda_sonora.mp3      voz + música (también la usa la vista previa)
  out/video_apertura_voz.mp4         video con voz y música
  out/video_apertura_musica.mp4      video solo con música, para narrar en vivo
"""

import subprocess
import sys

try:
    import imageio_ffmpeg
    FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
except ImportError:
    FFMPEG = "ffmpeg"

DURATION = 57.0
VOICE = "assets/audio/locucion.mp3"
MUSIC = "out/musica.wav"

# (inicio en la toma, fin en la toma, segundo del video donde entra)
# Toma de ElevenLabs v4, voz «Camila», 39,5 s. Cortes en sus pausas (silencedetect).
VOICE_CUTS = [
    (0.00, 2.85, 1.2),    # Cientos de imágenes… una sola pantalla.
    (3.60, 5.66, 5.0),    # ¿Y si pudieras tenerlas… frente a ti?
    (6.59, 10.32, 9.4),   # Cada corte encuentra su lugar… y aparece el cuerpo.
    (10.87, 14.82, 14.4),  # Con realidad mixta, la anatomía está aquí. En tu espacio.
    (15.20, 17.38, 19.6),  # Activa solo lo que necesitas ver.
    (18.11, 19.38, 22.8),  # Tómalo… y gíralo.
    (19.92, 23.02, 26.2),  # Desliza un plano… y la tomografía te sigue.
    (23.63, 25.83, 30.8),  # Tres planos. Un mismo punto.
    (26.47, 28.31, 35.0),  # Y ya funciona en Meta Quest 3.
    (29.03, 36.10, 39.0),  # Hoy, es un prototipo. Nuestra meta… cirugías. Y queremos construirlo… con ustedes.
    (36.37, 37.17, 47.6),  # Medical Viewer.
    (37.52, 39.52, 49.0),  # Exploremos juntos el siguiente paso.
]
PRE, POST = 0.05, 0.12


def voice_graph():
    parts, labels = [], []
    for i, (a, b, at) in enumerate(VOICE_CUTS):
        a0, b1 = max(0.0, a - PRE), b + POST
        d = b1 - a0
        ms = int(round((at - (a - a0)) * 1000))
        parts.append(f"[0:a]atrim={a0:.3f}:{b1:.3f},asetpts=PTS-STARTPTS,"
                     f"afade=t=in:d=0.02,afade=t=out:st={d - 0.05:.3f}:d=0.05,adelay={ms}|{ms}[v{i}]")
        labels.append(f"[v{i}]")
    g = ";".join(parts)
    g += (f";{''.join(labels)}amix=inputs={len(labels)}:normalize=0,"
          "highpass=f=80,equalizer=f=200:t=q:w=1.0:g=1.0,equalizer=f=7000:t=q:w=1.2:g=-1.0,"
          "acompressor=threshold=-22dB:ratio=2.2:attack=15:release=200:makeup=1.5,"
          f"loudnorm=I=-16:TP=-2:LRA=9,apad,atrim=0:{DURATION},aformat=sample_rates=48000:channel_layouts=stereo")
    return g


def run(args):
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", *args], check=True)


def main(video):
    # 1) voz colocada
    run(["-i", VOICE, "-filter_complex", voice_graph() + "[out]", "-map", "[out]", "out/voz_colocada.wav"])
    # 2) música nivelada y, con la voz como llave, agachada cuando hay locución
    run(["-i", MUSIC, "-i", "out/voz_colocada.wav", "-filter_complex",
         "[0:a]loudnorm=I=-21:TP=-3:LRA=11,aformat=sample_rates=48000:channel_layouts=stereo[m];"
         "[1:a]asplit=2[key][v];"
         "[m][key]sidechaincompress=threshold=0.02:ratio=5:attack=40:release=500:makeup=1[md];"
         "[v][md]amix=inputs=2:normalize=0,alimiter=limit=0.89[out]",
         "-map", "[out]", "-t", str(DURATION), "out/banda_sonora.wav"])
    run(["-i", "out/banda_sonora.wav", "-c:a", "libmp3lame", "-b:a", "192k", "assets/audio/banda_sonora.mp3"])
    # 3) al video
    for audio, out in (("out/banda_sonora.wav", "out/video_apertura_voz.mp4"), (MUSIC, "out/video_apertura_musica.mp4")):
        run(["-i", video, "-i", audio, "-map", "0:v", "-map", "1:a", "-c:v", "copy",
             "-c:a", "aac", "-b:a", "192k", "-t", str(DURATION), "-movflags", "+faststart", out])
        print("listo:", out)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
