"""
Pone la música en el video y lo deja listo para proyectar.

La música (tools/compose_music.py) ya sale nivelada (unos -16 LUFS); aquí solo se codifica y se
une al video sin volver a codificar la imagen.

Uso (desde VideoApertura):
  python3 tools/compose_music.py out/musica.wav
  python3 tools/mix_audio.py out/video_apertura.mp4
Genera:
  out/inmersia.mp4             el video final, con música
  out/musica_master.mp3        la música nivelada (la usa la vista previa)
"""

import subprocess
import sys

try:
    import imageio_ffmpeg
    FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
except ImportError:
    FFMPEG = "ffmpeg"

DURATION = 61.0
MUSIC = "out/musica.wav"
OUT = "out/inmersia.mp4"


def run(args):
    subprocess.run([FFMPEG, "-y", "-loglevel", "error", *args], check=True)


def main(video):
    run(["-i", MUSIC, "-c:a", "libmp3lame", "-b:a", "192k", "out/musica_master.mp3"])
    run(["-i", video, "-i", MUSIC, "-map", "0:v", "-map", "1:a", "-c:v", "copy",
         "-c:a", "aac", "-b:a", "192k", "-t", str(DURATION), "-movflags", "+faststart", OUT])
    print("listo:", OUT)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
