"""
Coloca la locución en los tiempos del video y la mezcla con el MP4.

La locución llega en una sola toma. Aquí se corta por sus pausas y cada frase se pone
justo cuando su texto aparece en pantalla. Después se le da un tratamiento suave
(graves limpios, agudos algo más cálidos, compresión ligera) y se normaliza a -16 LUFS.

Uso (desde VideoApertura):
  python3 tools/mix_voice.py assets/audio/voz_issa.mp3 out/video_apertura.mp4 out/video_apertura_voz.mp4
"""

import subprocess
import sys

try:
    import imageio_ffmpeg
    FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
except ImportError:
    FFMPEG = "ffmpeg"

DURATION = 60.0

# (inicio en la toma, fin en la toma, segundo del video donde entra) — toma de ElevenLabs,
# voz "Issavoces". Los cortes salen de las pausas detectadas con silencedetect.
PHRASES = [
    (0.00, 2.75, 0.60),    # Una tomografía son cientos de cortes…
    (3.34, 5.63, 3.85),    # ¿Y si pudieras explorarlos frente a ti?
    (6.45, 7.74, 7.70),    # Los cortes se ordenan…
    (8.31, 9.78, 9.40),    # …y se vuelven un volumen.
    (10.40, 13.22, 13.00),  # Con realidad mixta, aparece en tu espacio.
    (13.72, 16.26, 17.60),  # Enciendes solo los órganos que quieres ver.
    (16.93, 18.30, 21.30),  # Los tomas con la mano.
    (18.78, 19.55, 23.00),  # Los giras.
    (20.00, 22.07, 26.20),  # Deslizas un plano por el cuerpo…
    (22.56, 24.21, 28.90),  # …y la tomografía te sigue.
    (24.80, 27.63, 33.60),  # Tres cortes que se cruzan en un mismo punto.
    (28.30, 30.70, 38.60),  # Y ya corre en Meta Quest 3.
    (31.27, 32.30, 43.00),  # Es un prototipo.
    (32.79, 37.14, 44.60),  # Y queremos descubrir, con ustedes, dónde puede aportar en la clínica.
    (37.63, 40.24, 54.10),  # Exploremos juntos el siguiente paso.
]

PRE, POST = 0.06, 0.14  # margen para no cortar la primera consonante ni la cola


def main(voice, video, out):
    parts, labels = [], []
    for i, (a, b, at) in enumerate(PHRASES):
        a0, b1 = max(0.0, a - PRE), b + POST
        d = b1 - a0
        ms = int(round((at - (a - a0)) * 1000))
        parts.append(
            f"[0:a]atrim={a0:.3f}:{b1:.3f},asetpts=PTS-STARTPTS,"
            f"afade=t=in:d=0.03,afade=t=out:st={d - 0.06:.3f}:d=0.06,"
            f"adelay={ms}|{ms}[p{i}]")
        labels.append(f"[p{i}]")
    graph = ";".join(parts)
    graph += (f";{''.join(labels)}amix=inputs={len(labels)}:normalize=0,"
              # tratamiento suave: sin retumbe, un poco de cuerpo, agudos menos filosos
              "highpass=f=85,equalizer=f=220:t=q:w=1.0:g=1.5,equalizer=f=6500:t=q:w=1.2:g=-2,"
              "acompressor=threshold=-22dB:ratio=2.2:attack=15:release=180:makeup=2,"
              # un poco de sala para que no suene "pegada" al oído
              "aecho=0.85:0.6:28|47:0.10|0.06,"
              f"loudnorm=I=-16:TP=-1.5:LRA=9,apad,atrim=0:{DURATION},"
              "aformat=sample_rates=48000:channel_layouts=stereo[voz]")

    cmd = [FFMPEG, "-y", "-loglevel", "error", "-i", voice, "-i", video,
           "-filter_complex", graph, "-map", "1:v", "-map", "[voz]",
           "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart",
           "-t", str(DURATION), out]
    subprocess.run(cmd, check=True)
    print("listo:", out)


if __name__ == "__main__":
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    main(*sys.argv[1:])
