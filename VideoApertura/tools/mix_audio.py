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

try:
    import imageio_ffmpeg
    FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
except ImportError:
    FFMPEG = "ffmpeg"

DURATION = 63.0
VOICE = "assets/audio/locucion_inmersia.mp3"
MUSIC = "out/musica.wav"

# (inicio en la toma, fin en la toma, segundo del video donde entra)
# Toma de ElevenLabs v4, voz «Camila», 46 s. Cortes en sus pausas (silencedetect y tiempos
# por palabra de la transcripción); el texto de src/main.js aparece cuando la voz lo dice.
VOICE_CUTS = [
    (1.55, 2.21, 0.4),     # Son las siete.
    (2.60, 3.85, 1.4),     # Cirugía a las nueve.
    (4.48, 7.92, 2.9),     # Y el caso… está en doscientas sesenta y siete imágenes.
    (8.16, 9.86, 6.6),     # Hoy se revisan una por una…
    (10.30, 12.11, 8.45),  # y el cuerpo se arma en la cabeza.
    (12.60, 14.92, 10.5),  # ¿Y si pudieran verlo… completo?
    (15.58, 16.45, 15.05),  # Les presentamos…
    (16.88, 17.33, 16.03),  # Inmersia. (con el golpe de la música)
    (17.88, 18.76, 16.75),  # Cirugía inmersiva.
    (19.00, 20.05, 20.2),  # Órganos segmentados.
    (20.33, 21.26, 23.2),  # Tómalos con la mano.
    (21.76, 24.40, 26.2),  # Corta donde quieras… y la tomografía te sigue.
    (24.90, 25.60, 30.55),  # Tres planos.
    (25.85, 26.83, 31.45),  # Un mismo punto.
    (27.00, 28.60, 34.3),  # Y ya funciona en Meta Quest tres.
    (29.03, 32.37, 38.3),  # Un cuerpo completo en tres de, a partir de la tomografía.
    (32.55, 34.62, 42.35),  # El siguiente paso… es el quirófano.
    (34.86, 39.15, 44.65),  # Precisión, planificación con cirujanos… y validación clínica.
    (39.34, 41.49, 50.2),  # Para eso, buscamos un aliado clínico.
    (41.70, 45.10, 54.25),  # Construyámoslo juntos… con la Clínica Anglo Americana.
    (45.33, 46.08, 58.3),  # Inmersia.
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
