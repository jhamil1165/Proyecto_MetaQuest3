"""
Pone la locución en el video.

La voz entra completa y a su velocidad natural en VOICE_OFFSET; es el montaje el que se
ajusta a ella (ver WARP y BLOCKS en src/main.js, sincronizados con
assets/audio/locucion_palabras.json). Aquí solo se limpia y se nivela:
graves sin retumbe, agudos un poco más cálidos, compresión ligera y -16 LUFS.

Uso (desde VideoApertura):
  python3 tools/mix_voice.py assets/audio/locucion.mp3 out/video_apertura.mp4 out/video_apertura_voz.mp4
"""

import subprocess
import sys

try:
    import imageio_ffmpeg
    FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
except ImportError:
    FFMPEG = "ffmpeg"

DURATION = 60.0
VOICE_OFFSET = 0.5  # igual que VOICE_OFFSET en src/main.js


def main(voice, video, out):
    ms = int(VOICE_OFFSET * 1000)
    chain = (f"[0:a]adelay={ms}|{ms},"
             "highpass=f=80,equalizer=f=200:t=q:w=1.0:g=1.0,equalizer=f=7000:t=q:w=1.2:g=-1.5,"
             "acompressor=threshold=-22dB:ratio=2:attack=20:release=200:makeup=1.5,"
             f"loudnorm=I=-16:TP=-1.5:LRA=11,apad,atrim=0:{DURATION},"
             "aformat=sample_rates=48000:channel_layouts=stereo[voz]")
    cmd = [FFMPEG, "-y", "-loglevel", "error", "-i", voice, "-i", video,
           "-filter_complex", chain, "-map", "1:v", "-map", "[voz]",
           "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart",
           "-t", str(DURATION), out]
    subprocess.run(cmd, check=True)
    print("listo:", out)


if __name__ == "__main__":
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    main(*sys.argv[1:])
