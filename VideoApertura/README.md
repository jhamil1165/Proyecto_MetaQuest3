# INMERSIA · video de apertura

Pitch de 63 s para presentar el proyecto a la Clínica Anglo Americana: horizontal (1920 × 1080,
30 fps), con locución (ElevenLabs, voz «Camila»), texto que entra palabra por palabra cuando la
voz lo dice y música original a 120 BPM.
Todo se dibuja con código a partir de los datos reales del proyecto (tomografía, órganos
segmentados y capturas del visor).

## Guion

| Segundo | Escena | Qué se ve |
|---|---|---|
| 0 – 6 | Gancho | «Son las 7:00. Cirugía a las 9:00.» → «Y el caso está en **267** imágenes.» (cuenta desde cero). Pantalla con los cortes reales. |
| 6 – 10 | El problema | «Hoy se revisan una por una… y el cuerpo se arma ~~en la cabeza~~.» Los cortes avanzan de a uno, con un clic por corte. |
| 10 – 16 | La pregunta | «¿Y si pudieran verlo completo?» El corte sale de la pantalla y los cortes forman el cuerpo. |
| 16 – 20 | Marca | Les presentamos **INMERSIA** · Cirugía inmersiva (golpe de la música). |
| 20 – 34 | Demostración | Órganos segmentados · Tómalos con la mano · Corta donde quieras… y la tomografía te sigue · Tres planos, un mismo punto. |
| 34 – 38 | Captura real | «Ya funciona en Meta Quest 3.» Prototipo en desarrollo. |
| 38 – 42 | Cifras | «Un cuerpo completo en 3D, a partir de la tomografía.» 267 cortes · 4 órganos segmentados · 3 planos sincronizados. |
| 42 – 49 | Hoja de ruta | «El siguiente paso… es el quirófano. Precisión, planificación con cirujanos… y validación clínica.» Cada fase se enciende cuando la voz la nombra. «Fases por desarrollar y validar junto a la clínica.» |
| 50 – 54 | Lo que buscamos | «Para eso, buscamos un aliado clínico»: cirujanos que lo prueben y nos guíen, casos anonimizados, apoyo para la siguiente fase. |
| 54 – 58 | Invitación | «Construyámoslo juntos… con la Clínica Anglo Americana.» |
| 58 – 63 | Logo | «Inmersia.» INMERSIA · Cirugía inmersiva · Ingeniería Biomédica · PUCP. Quieto desde 59,6 s. |

## Qué es real y qué es ilustración

Una etiqueta discreta abajo a la izquierda lo dice en cada escena:

- **Real:** los cortes de la tomografía del proyecto y las capturas del visor (`assets/captures/`).
- **Ilustración con datos del proyecto:** el cuerpo en 3D, los órganos, los planos y los paneles.
- **Representación:** el gesto de la mano que toma el hígado.

No se muestran resultados clínicos, navegación quirúrgica, datos de pacientes ni logos de la
clínica. Las cifras (267 cortes, 4 órganos, 3 planos) son del prototipo. La hoja de ruta se
presenta como fases por desarrollar y validar.

## Cómo generarlo

Desde la carpeta `VideoApertura` (la primera vez: `npm install`,
`pip install numpy pillow scipy scikit-image imageio-ffmpeg` y `python tools/prepare_data.py`):

```
npm run video                                      # imagen → out/video_apertura.mp4
python tools/compose_music.py out/musica.wav       # música original
python tools/mix_audio.py out/video_apertura.mp4   # video final → out/inmersia.mp4
```

Quedan dos versiones: `out/inmersia.mp4` (voz y música) y `out/inmersia_musica.mp4` (solo
música, por si se narra en vivo).

En Linux se usa `python3`. Con tarjeta gráfica toma unos minutos; sin ella, alrededor de una hora.

Para revisar sin exportar todo:

- `npm run fotos` guarda fotogramas sueltos en `out/stills/`.
- Vista previa en el navegador: `npx http-server -p 8080` (o cualquier servidor estático) y
  abrir `http://localhost:8080/src/index.html`. Con `?t=36` abre en ese segundo. Si existe
  `out/banda_sonora.mp3`, suena con la voz y la música.

## Dónde cambiar cosas

- **Textos y tiempos:** `BLOCKS` en `src/main.js`. `<b>` pone la palabra en azul y la subraya,
  `<s>` la tacha y `<n>267</n>` hace que el número cuente.
- **Nombre, lema, equipo y clínica:** `CONFIG` en `src/main.js`.
- **Hoja de ruta, cifras y lo que buscamos:** `src/index.html` (`#road`, `#stats`, `#chips`).
- **Escenas 3D:** se animan en su propio reloj y `WARP` las encaja en el guion.
- **Voz:** `assets/audio/locucion_inmersia.mp3` es una sola toma; `VOICE_CUTS` en
  `tools/mix_audio.py` la corta en sus pausas y dice en qué segundo entra cada frase.
- **Música:** `tools/compose_music.py`. Si mueves una escena, mueve también su segundo en
  `CUES` para que el golpe siga cayendo en el cambio.

La versión anterior, con locución («Medical Viewer», 57 s), sigue en el historial de git.
