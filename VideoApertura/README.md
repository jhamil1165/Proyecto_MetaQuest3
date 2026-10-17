# Video de apertura · Medical Viewer en Meta Quest 3

Pieza de 60 segundos (1920 × 1080, 30 fps) con locución, para proyectar antes de la exposición en la Clínica Anglo Americana. Toda la animación está hecha con código y usa datos reales del proyecto:

- la serie de TAC `serie_cuerpo` de `Assets/StreamingAssets/CT`, con 267 cortes axiales;
- las máscaras de segmentación del hígado, el estómago, el páncreas y la vesícula;
- dos capturas reales del visor.

Se exportan dos versiones: `out/video_apertura_voz.mp4` (con locución) y `out/video_apertura.mp4` (sin audio, por si se narra en vivo).

## Concepto

**De la imagen al espacio.** El video sigue el recorrido de un solo corte de tomografía. Empieza en una pantalla plana, se separa de ella y se apila con los demás cortes hasta formar el cuerpo. Después ese volumen se puede explorar: se encienden los órganos, se toma uno con la mano, se desliza un plano de corte y se cruzan tres planos. Al final aparece una captura real del visor y una invitación abierta a explorar juntos las aplicaciones.

Cada escena nace de la anterior. No hay cortes secos entre diapositivas: la misma imagen del TAC atraviesa todo el video.

## Guion con tiempos

El montaje sigue a la voz, no al revés. La locución entra completa en el segundo 0,5, a su velocidad natural. Cada texto aparece cuando la voz lo dice y cada animación se estira o se acorta para que su momento clave coincida con la palabra que lo nombra.

| Tiempo | Locución y dirección | Texto en pantalla | Acción visual |
|---|---|---|---|
| 0,5–6,7 s | *Curiosidad, suave.* «Una tomografía tiene cientos de imágenes… y casi siempre… las vemos de una en una.» | **Cientos de imágenes.** / *De una en una.* | Un panel de vidrio oscuro recorre los cortes axiales reales, del cuello al abdomen, con su contador. |
| 7,5–11,3 s | *Intriga.* «¿Y si pudieras explorarlas… frente a ti?» | **¿Y si pudieras explorarlas *frente a ti*?** | El panel se detiene en un corte del abdomen (hígado y estómago). |
| 12,5–16,9 s | *Calidez, descubrimiento.* «Cada corte encuentra su lugar… y aparece un cuerpo completo.» | **Cada corte, en su lugar…** / **…y aparece un *cuerpo*.** | El corte sale de la pantalla y se coloca en el cuerpo; los demás cortes aparecen alrededor y la pila se funde en el volumen 3D. |
| 17,6–23,9 s | *Entusiasmo contenido.* «Con realidad mixta, la anatomía está aquí, en tu espacio… sin dejar de ver la sala.» | REALIDAD MIXTA · META QUEST 3 / **La anatomía, aquí, *en tu espacio*.** / Sin dejar de ver la sala. | El volumen gira despacio, con una sombra suave que lo sitúa en el espacio. |
| 24,7–27,4 s | *Claridad, confianza.* «Activas solo los órganos que necesitas ver.» | 01 · ÓRGANOS / **Activa solo los órganos que necesitas ver.** | Panel «Sistemas» del prototipo; se encienden Hígado, Estómago, Páncreas y Vesícula. |
| 28,2–31 s | «Tomas el hígado con la mano… y lo **giras**.» | **Tómalo con la mano… …y *gíralo*.** | Un gesto de pinza toma el hígado, lo acerca y lo gira unos 160°. |
| 31,8–37 s | «Deslizas un plano por el cuerpo… y la tomografía te sigue, corte a corte.» | 02 · PLANO DE CORTE / **Desliza el plano… y la tomografía *te sigue*.** | El plano azul baja por el cuerpo; a la derecha, el corte axial real correspondiente con la segmentación en color. |
| 37,6–40,8 s | «Tres planos… que se cruzan en un **mismo** punto.» | 03 · TRES PLANOS / **Tres planos, *un mismo punto*.** | Planos axial, coronal y sagital cruzados en el abdomen, y tres vistas 2D con la mira en el mismo punto. |
| 41,5–44,5 s | *Cálida.* «Y esto… ya funciona en un Meta Quest 3.» | CAPTURA REAL DEL VISOR / **Ya funciona en Meta Quest 3.** / Prototipo en desarrollo. | Dos capturas reales del prototipo en el visor. |
| 45,9–52,1 s | *Sincera.* «Hoy es un prototipo. Y creemos que puede llegar mucho más lejos… con la mirada de ustedes.» | LO QUE SIGUE / **Hoy es un prototipo. *¿Hasta dónde puede llegar?*** / tres posibilidades / *Posibilidades por explorar y validar con ustedes.* | El conjunto (volumen, órganos y plano) gira despacio. Aparecen «Revisar un caso en equipo, en 3D», «Enseñar anatomía con estudios reales» y «Explicar un estudio al paciente». |
| 52,9–55,6 s | *Convicción, invitación cálida.* «Exploremos juntos… el siguiente paso.» | INGENIERÍA BIOMÉDICA · PUCP / **Medical Viewer** / Imágenes médicas en realidad mixta / ***Exploremos juntos el siguiente paso.*** | Cierre con el conjunto a la derecha. |
| 55,6–60 s | Silencio | (igual) | La imagen queda totalmente quieta desde el segundo 56,4 para que empiece la exposición. |

## Qué es real y qué es representación

En la esquina inferior izquierda, una etiqueta discreta indica en todo momento qué se está viendo.

| Momento | Qué es |
|---|---|
| 0–11 s, panel de cortes | **Real.** Son los cortes de la tomografía del proyecto, sin modificar. |
| 11–41 s y 45–60 s, volumen, órganos y planos | **Ilustración en 3D con datos reales.** El volumen sale de los mismos 267 cortes y los órganos se reconstruyen a partir de las máscaras de segmentación del proyecto. El dibujo lo hace el navegador, no el visor: el aspecto se parece al del prototipo, pero no es una grabación. |
| 24–28 s, panel «Sistemas» | **Recreación fiel de la interfaz del prototipo**, con los mismos textos, órganos, interruptores y colores (#1E2228, #F0F2F5, #A8B1BC, #4C8DFF). |
| 28–31 s, gesto de pinza | **Representación de la interacción.** Es un indicador gráfico, no una mano grabada. |
| 32–41 s, paneles 2D | **Reales.** Son los cortes axial, coronal y sagital de la serie, con las máscaras reales en color. |
| 41–45 s | **Captura real del prototipo en el Meta Quest 3.** Sale de una grabación del 30/09/2026; está comprimida por WhatsApp y se nota. |

El video no muestra navegación quirúrgica ni anatomía registrada sobre un paciente. Tampoco incluye cifras de exactitud, mediciones, NDI, logos ni avales. La herramienta de medición se deja fuera.

## Frase para enlazar con la exposición

> «Lo que acaban de ver sale de nuestro prototipo: una tomografía real convertida en un cuerpo que se puede recorrer con las manos. Hoy les queremos contar cómo funciona y, sobre todo, escuchar dónde creen ustedes que podría aportar.»

## Tomas que conviene grabar

Las capturas reales que tenemos son de baja calidad. Con estas tomas se puede reemplazar o reforzar cada escena:

1. **Volumen sobre una mesa real, con passthrough** (8–10 s): toma principal para «en tu espacio» (17,6–23,9 s). La cabeza debe moverse despacio alrededor del cuerpo.
2. **Pinza y giro del hígado con la mano** (6–8 s): para 28–31 s. Mano bien iluminada, sin el menú del sistema de Meta a la vista.
3. **Barrido del plano de corte** (8 s): para 32–37 s. El plano baja de forma continua y se ve la imagen del TAC sobre él.
4. **Tres planos cruzados** (5–6 s): para 37–41 s.
5. **Panel «Sistemas» encendiendo órganos** (5 s): para 24–28 s.
6. **Toma externa de una persona con el visor** usando las manos (5 s, con cámara o celular): puede abrir el bloque de la captura real.

Recomendaciones de grabación:
- Graba desde el visor con la mayor resolución de captura disponible, o transmite a la PC y graba con OBS.
- Pasa los archivos por cable o por Drive, no por WhatsApp.
- Antes de grabar, quita los paneles de depuración.
- Que no aparezcan datos de pacientes.

Para usar una toma nueva, colócala en `assets/captures/` y cambia los nombres de archivo en `src/main.js` (`capA` y `capB`). Los videos se pueden incluir con un `<video>` en el mismo panel `#capture`.

## Audio

### Locución

`assets/audio/locucion.mp3` se generó con **ElevenLabs v4** y la voz de biblioteca «Fernanda Sanmiguel» (español latinoamericano neutro, expresiva). Dura 55 s y se usa tal cual, sin acelerar ni cortar. El modelo v4 interpreta las indicaciones entre corchetes y no las pronuncia. Se comprobó con la transcripción con tiempos por palabra (`assets/audio/locucion_palabras.json`): todas las indicaciones caen en silencios.

Texto enviado, con la dirección de locución:

```text
[curious] [softly] Una tomografía tiene cientos de imágenes… [short pause] y casi siempre… las vemos de una en una.
[pause] [intrigued] ¿Y si pudieras explorarlas… [short pause] frente a ti?
[long pause] [warmly] Cada corte encuentra su lugar… [short pause] y aparece un cuerpo completo.
[pause] [excited] [softly] Con realidad mixta, la anatomía está aquí, [short pause] en tu espacio… sin dejar de ver la sala.
[pause] [confident] Activas solo los órganos que necesitas ver. [short pause] Tomas el hígado con la mano… [short pause] y lo GIRAS.
[pause] Deslizas un plano por el cuerpo… [short pause] y la tomografía te sigue, corte a corte.
[pause] Tres planos… [short pause] que se cruzan en un MISMO punto.
[pause] [warmly] Y esto… ya funciona en un Meta Quest 3.
[long pause] [sincere] Hoy es un prototipo. [short pause] Y creemos que puede llegar mucho más lejos… [short pause] con la mirada de ustedes.
[pause] [warmly] [confident] Exploremos juntos… el siguiente paso.
```

Para ponerla en el video: `python3 tools/mix_voice.py assets/audio/locucion.mp3 out/video_apertura.mp4 out/video_apertura_voz.mp4`. Limpia los graves, suaviza un poco los agudos, aplica una compresión ligera y nivela a -16 LUFS.

**Si se cambia la toma:**
1. Transcríbela con tiempos por palabra (por ejemplo, ElevenLabs Scribe).
2. Actualiza los tiempos de `BLOCKS` y de `WARP` en `src/main.js`. `WARP` empareja cada momento clave de la animación con el segundo en que la voz lo nombra.
3. Revisa con `npm run fotos` y exporta otra vez.

### Música (opcional)

El video se entiende sin sonido. Si quieren música bajo la voz:
- Usen una pista ambiental de 70–80 BPM, con pad y algún piano suave, sin voz ni percusión marcada, a unos -24 LUFS para no tapar la locución.
- Fuentes sin costo: la Biblioteca de audio de YouTube o Pixabay Music. **Revisen la licencia de cada pista** antes de usarla.

## Cambiar textos

- Los textos del cierre (nombre del proyecto, subtítulo, equipo y frase final) están en `CONFIG`, al inicio de `src/main.js`.
- Los demás textos y sus tiempos están en `BLOCKS`, en el mismo archivo, sincronizados con la locución. Una palabra entre `<b>…</b>` se pinta de azul.
- «Medical Viewer» es el nombre que muestra el menú del prototipo. Si el proyecto tiene otro nombre, cámbienlo en `CONFIG.name`.

## Generar el video

Requisitos:
- Node 18 o superior.
- Python 3 con `numpy pillow scipy scikit-image imageio-ffmpeg`.
- Google Chrome instalado. Para usar otro Chromium, indica su ruta en la variable `CHROMIUM`.
- En Windows, `python` en lugar de `python3`: `python tools/prepare_data.py`.

```bash
cd VideoApertura
npm install
pip install numpy pillow scipy scikit-image imageio-ffmpeg
npm run datos          # genera assets/ desde Assets/StreamingAssets/CT (unos 20 s)
npm run fotos          # fotogramas sueltos de control en out/stills/
npm run video          # out/video_apertura.mp4 (sin audio)
python3 tools/mix_voice.py assets/audio/locucion.mp3 out/video_apertura.mp4 out/video_apertura_voz.mp4
```

`npm run video` dibuja los 1800 fotogramas uno a uno y se los pasa a ffmpeg. En una PC usa la GPU y tarda unos minutos; en Linux sin GPU dibuja por software y puede tardar más de una hora (con `--gpu` se fuerza la GPU). Para exportar solo un tramo: `node tools/render.mjs --from 17 --to 25 --out out/tramo.mp4`.

### Vista previa interactiva

```bash
cd VideoApertura
npx http-server -p 8080 .     # o: python3 -m http.server 8080
# abrir http://localhost:8080/src/index.html  (con ?t=23 salta a ese segundo)
```

La vista previa reproduce la animación en tiempo real, con la locución, y tiene una barra para moverse por el video. En una laptop sin GPU dedicada puede ir a saltos. **El archivo final es el MP4**: la vista previa sirve para revisar y ajustar.

## Archivos

- `src/index.html`: escenario de 1920 × 1080, estilos y paneles.
- `src/main.js`: línea de tiempo, textos y cámara. `renderAt(t)` dibuja cualquier instante.
- `src/gl.js`: motor 3D (three.js). Hace el raymarching del volumen, dibuja las mallas de los órganos y los planos de corte.
- `tools/prepare_data.py`: convierte la serie de TAC y las máscaras en volumen, mallas y cortes.
- `tools/render.mjs`: exporta fotogramas o el MP4.
- `tools/mix_voice.py`: pone la locución en el MP4.
- `assets/audio/`: locución y sus tiempos por palabra (versionados).
- `assets/captures/`: capturas reales del visor (versionadas). El resto de `assets/` se genera.

## Revisión antes de entregar

- **Legibilidad:** los títulos van de 64 a 112 px y el texto secundario es de al menos 30 px, todo en blanco o azul sobre #1E2228. Cada bloque principal de texto queda en pantalla 2,5 s o más.
- **Continuidad:** el corte de la pantalla es el mismo que se coloca en el volumen. La pila se funde en el volumen y el mismo cuerpo sostiene todas las demostraciones.
- **Voz y montaje:** cada texto aparece con la palabra que lo dice y cada demostración coincide con la acción que nombra la voz (revisado sobre fotogramas anotados con la transcripción).
- **Final:** la voz termina en el segundo 55,6 y la imagen queda quieta desde el 56,4 hasta el 60.
- **Referencias:** los reels de Instagram de referencia no se pudieron reproducir ni escuchar desde el entorno de trabajo, así que tampoco se analizó su audio. La estructura y la dirección de voz se basan en la descripción del encargo: situación reconocible, demostración paso a paso y cierre sencillo, con texto a la izquierda y demostración a la derecha.
