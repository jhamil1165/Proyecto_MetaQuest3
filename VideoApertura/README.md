# Video de apertura · Medical Viewer en Meta Quest 3

Pieza de 57 segundos (1920 × 1080, 30 fps) para proyectar antes de la exposición en la Clínica Anglo Americana. Tiene locución y música originales. Toda la animación está hecha con código y usa datos reales del proyecto:

- la serie de TAC `serie_cuerpo` de `Assets/StreamingAssets/CT`, con 267 cortes axiales;
- las máscaras de segmentación del hígado, el estómago, el páncreas y la vesícula;
- dos capturas reales del visor.

Versiones que se exportan:
- `out/video_apertura_voz.mp4`: con locución y música. **Es la principal.**
- `out/video_apertura_musica.mp4`: solo música, por si prefieren narrar en vivo.
- `out/video_apertura.mp4`: sin audio.

## Concepto

**De la imagen al espacio, y de ahí al quirófano.** El video sigue el recorrido de un solo corte de tomografía. Empieza en una pantalla plana, se separa de ella y se apila con los demás cortes hasta formar el cuerpo. Después ese volumen se explora: se encienden los órganos, se toma uno con la mano, se desliza un plano de corte y se cruzan tres planos. Una captura real demuestra que ya funciona en el visor. El cierre plantea la meta, la planificación de cirugías, y una invitación a construirla con la clínica.

Tiene el tono de un anuncio, con ritmo, música y una voz cálida y segura. Lo que «vende», sin embargo, es una visión y una alianza, no un producto terminado: muestra solo lo que el prototipo ya hace y presenta la cirugía como meta.

## Guion con tiempos

El montaje sigue a la voz. La locución es una sola toma de ElevenLabs y no se acelera. Solo se separa en sus pausas naturales para que la música respire entre escenas. Cada texto aparece cuando la voz lo dice, y cada animación se ajusta para que su momento clave coincida con la palabra que lo nombra.

| Tiempo | Locución (dirección) | Texto en pantalla | Acción visual | Música |
|---|---|---|---|---|
| 0–4,6 s | *Suave, intrigante.* «Cientos de imágenes… una sola pantalla.» | **Cientos de imágenes.** / *Una sola pantalla.* | Un panel de vidrio oscuro recorre los cortes axiales reales con su contador. | Fondo grave y campanitas sueltas. |
| 4,9–7,9 s | *Curiosa.* «¿Y si pudieras tenerlas… frente a ti?» | **¿Y si pudieras tenerlas *frente a ti*?** | El panel se detiene en un corte del abdomen. | Se abre un acorde; sube un aire. |
| 9,3–13,6 s | *Cálida.* «Cada corte encuentra su lugar… y aparece el cuerpo.» | **Cada corte, en su lugar… …y aparece el *cuerpo*.** | El corte sale de la pantalla y se coloca en el cuerpo; los demás cortes lo rodean y se funden en el volumen 3D. | Entra el acorde completo (Re mayor). |
| 14,3–18,9 s | *Segura, suave.* «Con realidad mixta, la anatomía está aquí. En tu espacio.» | REALIDAD MIXTA · META QUEST 3 / **La anatomía, aquí. *En tu espacio.*** | El volumen gira despacio sobre su sombra. | Arpegio de cristal. |
| 19,5–22,4 s | *Clara.* «Activa solo lo que necesitas ver.» | 01 · ÓRGANOS / **Activa solo lo que necesitas ver.** | Panel «Sistemas» del prototipo; se encienden los órganos. | Entra un pulso suave a 80 BPM. |
| 22,7–25,6 s | «Tómalo… y gíralo.» | **Tómalo… …y *gíralo*.** | La pinza toma el hígado y lo gira. | Pulso. |
| 26,1–30 s | «Desliza un plano… y la tomografía te sigue.» | 02 · PLANO DE CORTE / **Desliza un plano… y la tomografía *te sigue*.** | El plano baja por el cuerpo; a la derecha, el corte axial real con la segmentación. | Pulso. |
| 30,7–34,3 s | «Tres planos. Un **mismo** punto.» | 03 · TRES PLANOS / **Tres planos. *Un mismo punto.*** | Planos axial, coronal y sagital cruzados; tres vistas 2D con la mira. | Pulso. |
| 34,9–38,3 s | *Cálida.* «Y ya funciona en Meta Quest 3.» | CAPTURA REAL DEL VISOR / **Ya funciona en Meta Quest 3.** / Prototipo en desarrollo. | Dos capturas reales del prototipo. | Respiro, sin pulso. |
| 38,9–46,9 s | *Sincera.* «Hoy, es un prototipo. Nuestra meta… es llevarlo a la planificación de cirugías. Y queremos construirlo… con ustedes.» | LO QUE SIGUE / **Hoy, un prototipo. La meta: *planificar cirugías.*** / «Preparar una cirugía en 3D, en equipo», «Revisar el caso antes de entrar al quirófano», «Explicar el procedimiento al paciente» / *Objetivos por desarrollar y validar con ustedes.* | El conjunto (volumen, órganos y plano) gira despacio. | Vuelve el pulso y sube hacia el cierre. |
| 47,4–51 s | *Cálida, convencida.* «Medical Viewer. Exploremos juntos el siguiente paso.» | INGENIERÍA BIOMÉDICA · PUCP / **Medical Viewer** / Imágenes médicas en realidad mixta / ***Exploremos juntos el siguiente paso.*** | Cierre con el conjunto a la derecha. | Acorde final que se apaga hacia el segundo 53. |
| 51–57 s | Silencio | (igual) | Imagen quieta desde el segundo 52 para que empiece la exposición. | Silencio desde el 53. |

## Qué es real y qué es representación

En la esquina inferior izquierda, una etiqueta discreta indica en todo momento qué se está viendo.

| Momento | Qué es |
|---|---|
| 0–8 s, panel de cortes | **Real.** Son los cortes de la tomografía del proyecto, sin modificar. |
| 8–34 s y 39–57 s, volumen, órganos y planos | **Ilustración en 3D con datos reales.** El volumen sale de los mismos 267 cortes y los órganos se reconstruyen a partir de las máscaras de segmentación del proyecto. El dibujo lo hace el navegador, no el visor: el aspecto se parece al del prototipo, pero no es una grabación. |
| 19–23 s, panel «Sistemas» | **Recreación fiel de la interfaz del prototipo**, con los mismos textos, órganos, interruptores y colores (#1E2228, #F0F2F5, #A8B1BC, #4C8DFF). |
| 23–26 s, gesto de pinza | **Representación de la interacción.** Es un indicador gráfico, no una mano grabada. |
| 26–34 s, paneles 2D | **Reales.** Son los cortes axial, coronal y sagital de la serie, con las máscaras reales en color. |
| 35–38 s | **Captura real del prototipo en el Meta Quest 3.** Sale de una grabación del 30/09/2026; está comprimida por WhatsApp y se nota. |
| 39–47 s, cirugía | **Meta, no función actual.** Se dice «la meta» y «objetivos por desarrollar y validar». |

El video no muestra navegación quirúrgica ni anatomía registrada sobre un paciente. Tampoco incluye cifras de exactitud, mediciones, NDI, logos ni avales. La herramienta de medición se deja fuera.

## Frase para enlazar con la exposición

> «Lo que acaban de ver sale de nuestro prototipo: una tomografía real convertida en un cuerpo que se puede recorrer con las manos. Hoy les vamos a mostrar dónde estamos y, sobre todo, cómo queremos llevar esto a la planificación de cirugías junto con ustedes.»

## Tomas que conviene grabar

Las capturas reales que tenemos son de baja calidad. Con estas tomas se puede reemplazar o reforzar cada escena:

1. **Volumen sobre una mesa real, con passthrough** (8–10 s): toma principal para «en tu espacio» (14–19 s). La cabeza debe moverse despacio alrededor del cuerpo.
2. **Pinza y giro del hígado con la mano** (6–8 s): para 23–26 s. Mano bien iluminada, sin el menú del sistema de Meta a la vista.
3. **Barrido del plano de corte** (8 s): para 26–30 s. El plano baja de forma continua y se ve la imagen del TAC sobre él.
4. **Tres planos cruzados** (5–6 s): para 31–34 s.
5. **Panel «Sistemas» encendiendo órganos** (5 s): para 19–23 s.
6. **Toma externa de una persona con el visor** usando las manos (5 s, con cámara o celular): puede abrir el bloque de la captura real.

Recomendaciones de grabación:
- Graba desde el visor con la mayor resolución de captura disponible, o transmite a la PC y graba con OBS.
- Pasa los archivos por cable o por Drive, no por WhatsApp.
- Antes de grabar, quita los paneles de depuración.
- Que no aparezcan datos de pacientes.

Para usar una toma nueva, colócala en `assets/captures/` y cambia los nombres de archivo en `src/main.js` (`capA` y `capB`). Los videos se pueden incluir con un `<video>` en el mismo panel `#capture`.

## Audio

### Locución

`assets/audio/locucion.mp3` se generó con **ElevenLabs v4** y la voz de biblioteca «Camila» (español latinoamericano, cálida, pensada para anuncios). Dura 39,5 s. El modelo v4 interpreta las indicaciones entre corchetes y no las pronuncia. Texto enviado, con la dirección:

```text
[softly] [intrigued] Cientos de imágenes… [short pause] una sola pantalla.
[pause] [curious] ¿Y si pudieras tenerlas… [short pause] frente a ti?
[long pause] [warmly] Cada corte encuentra su lugar… [short pause] y aparece el cuerpo.
[pause] [confident] [softly] Con realidad mixta, la anatomía está aquí. [short pause] En tu espacio.
[pause] [confident] Activa solo lo que necesitas ver. [short pause] Tómalo… [short pause] y gíralo.
[pause] Desliza un plano… [short pause] y la tomografía te sigue.
[pause] Tres planos. [short pause] Un MISMO punto.
[pause] [warmly] Y ya funciona en Meta Quest 3.
[long pause] [sincere] Hoy, es un prototipo. [short pause] Nuestra meta… es llevarlo a la planificación de cirugías. [short pause] Y queremos construirlo… con ustedes.
[pause] [warmly] [confident] Medical Viewer. [short pause] Exploremos juntos el siguiente paso.
```

### Música

`tools/compose_music.py` compone una pieza **original**, sintetizada por código y sin derechos de terceros: un colchón cálido en Re mayor, campanitas de cristal, un pulso suave a 80 BPM durante la demostración y un acorde final. Los cambios caen en los cortes de escena (`CUES`). Al mezclar, la música baja sola cuando habla la voz (compresión con la voz como llave).

### Mezcla

`tools/mix_audio.py` coloca cada frase de la voz en su segundo (`VOICE_CUTS`), la limpia y la nivela a -16 LUFS. Pone la música por debajo y genera `assets/audio/banda_sonora.mp3` y los MP4 con audio.

**Si se cambia la toma de voz:**
1. Saca sus pausas: `ffmpeg -i voz.mp3 -af silencedetect=noise=-38dB:d=0.18 -f null -`.
2. Actualiza `VOICE_CUTS` y, si cambian los tiempos, `BLOCKS` y `WARP` en `src/main.js` y `CUES` en `compose_music.py`.
3. Revisa con `npm run fotos` y exporta otra vez.

## Cambiar textos

- Los textos del cierre (nombre del proyecto, subtítulo, equipo y frase final) están en `CONFIG`, al inicio de `src/main.js`.
- Los demás textos y sus tiempos están en `BLOCKS`, en el mismo archivo, sincronizados con la locución. Una palabra entre `<b>…</b>` se pinta de azul.
- «Medical Viewer» es el nombre que muestra el menú del prototipo y el que dice la voz. Si cambia, hay que cambiar `CONFIG.name` y regenerar la locución.

## Generar el video

Requisitos:
- Node 18 o superior.
- Python 3 con `numpy pillow scipy scikit-image imageio-ffmpeg`.
- Google Chrome instalado. Para usar otro Chromium, indica su ruta en la variable `CHROMIUM`.
- En Windows, `python` en lugar de `python3`.

```bash
cd VideoApertura
npm install
pip install numpy pillow scipy scikit-image imageio-ffmpeg
npm run datos                               # genera assets/ desde Assets/StreamingAssets/CT (unos 20 s)
npm run fotos                               # fotogramas sueltos de control en out/stills/
npm run video                               # out/video_apertura.mp4 (sin audio)
python3 tools/compose_music.py out/musica.wav
python3 tools/mix_audio.py out/video_apertura.mp4
```

`npm run video` dibuja los 1710 fotogramas uno a uno y se los pasa a ffmpeg. En una PC usa la GPU y tarda unos minutos; en Linux sin GPU dibuja por software y puede tardar más de una hora (con `--gpu` se fuerza la GPU). Para exportar solo un tramo: `node tools/render.mjs --from 17 --to 25 --out out/tramo.mp4`.

### Vista previa interactiva

```bash
cd VideoApertura
npx http-server -p 8080 .     # o: python3 -m http.server 8080
# abrir http://localhost:8080/src/index.html  (con ?t=23 salta a ese segundo)
```

La vista previa reproduce la animación en tiempo real, con la banda sonora, y tiene una barra para moverse por el video. En una laptop sin GPU dedicada puede ir a saltos. **El archivo final es el MP4**: la vista previa sirve para revisar y ajustar.

## Archivos

- `src/index.html`: escenario de 1920 × 1080, estilos y paneles.
- `src/main.js`: línea de tiempo, textos, sincronía con la voz (`WARP`) y cámara. `renderAt(t)` dibuja cualquier instante.
- `src/gl.js`: motor 3D (three.js). Hace el raymarching del volumen, dibuja las mallas de los órganos y los planos de corte.
- `tools/prepare_data.py`: convierte la serie de TAC y las máscaras en volumen, mallas y cortes.
- `tools/render.mjs`: exporta fotogramas o el MP4.
- `tools/compose_music.py`: compone la música.
- `tools/mix_audio.py`: coloca la voz, mezcla con la música y pone el audio en los MP4.
- `assets/captures/` y `assets/audio/`: capturas reales del visor, locución y banda sonora (versionadas). El resto de `assets/` se genera.

## Revisión antes de entregar

- **Legibilidad:** los títulos van de 64 a 112 px y el texto secundario es de al menos 30 px, todo en blanco o azul sobre #1E2228.
- **Voz y montaje:** cada texto aparece con la palabra que lo dice y cada demostración coincide con la acción que nombra la voz (revisado sobre fotogramas anotados con los tiempos de la locución).
- **Audio:** voz a -16 LUFS; música unos 5 dB por debajo y más baja aún mientras habla la voz; sin saturación.
- **Final:** la voz termina en el segundo 51, la música se apaga hacia el 53 y la imagen queda quieta desde el 52 hasta el 57.
- **Referencias:** los reels de Instagram de referencia no se pudieron reproducir ni escuchar desde el entorno de trabajo, así que tampoco se analizó su audio. La estructura y la dirección de voz se basan en la descripción del encargo.
