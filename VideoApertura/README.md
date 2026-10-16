# Video de apertura · Medical Viewer en Meta Quest 3

Pieza de unos 60 segundos (1920 × 1080, 30 fps, sin audio) para proyectar antes de la exposición en la Clínica Anglo Americana. Toda la animación está hecha con código y usa datos reales del proyecto:

- la serie de TAC `serie_cuerpo` de `Assets/StreamingAssets/CT`, con 267 cortes axiales;
- las máscaras de segmentación del hígado, el estómago, el páncreas y la vesícula;
- dos capturas reales del visor.

## Concepto

**De la imagen al espacio.** El video sigue el recorrido de un solo corte de tomografía. Empieza en una pantalla plana, se separa de ella y se apila con los demás cortes hasta formar el cuerpo. Después ese volumen se puede explorar: se encienden los órganos, se toma uno con la mano, se desliza un plano de corte y se cruzan tres planos. Al final aparece una captura real del visor y una invitación abierta a explorar juntos las aplicaciones.

Cada escena nace de la anterior. No hay cortes secos entre diapositivas: la misma imagen del TAC atraviesa todo el video.

## Guion con tiempos

| Tiempo | Texto en pantalla | Acción visual | Audio opcional |
|---|---|---|---|
| 0–3,4 s | **Cientos de cortes.** / *Una imagen a la vez.* | Un panel de vidrio oscuro recorre los cortes axiales reales, del cuello al abdomen, con su contador. | Pulso grave suave; un tic muy leve por cada cambio de corte. |
| 3,7–6,8 s | **¿Y si pudieras explorarlos *frente a ti*?** | El panel se detiene en un corte del abdomen (hígado y estómago). | El pulso se sostiene y sube un poco. |
| 6,8–12,8 s | **De cortes planos…** / **…a un *volumen*.** | El corte sale de la pantalla, se inclina y se coloca en su lugar. Los demás cortes aparecen a su alrededor y forman el cuerpo; la pila se funde en el volumen 3D. | Barrido ascendente; el pad musical entra al formarse el volumen. |
| 12,8–17 s | REALIDAD MIXTA · META QUEST 3 / **La anatomía aparece *en tu espacio*.** / Sin perder de vista la sala. | El volumen gira despacio, con una sombra suave que lo sitúa en el espacio. | Pad estable. |
| 17–21 s | 01 · ÓRGANOS / **Activa solo lo que quieres ver.** | Aparece el panel «Sistemas» del prototipo; se encienden Hígado, Estómago, Páncreas y Vesícula, y cada órgano aparece dentro del cuerpo. | Un clic suave por interruptor. |
| 21–25 s | **Agárralo. Gíralo.** / Míralo desde cualquier ángulo. | Un gesto de pinza toma el hígado, lo acerca y lo gira unos 160°. | Un *swoosh* corto al girar. |
| 25–33 s | 02 · PLANO DE CORTE / **Desliza el plano. La tomografía *te sigue*.** | El plano azul baja por el cuerpo y lo corta. A la derecha, el corte axial real correspondiente con la segmentación en color. | Tono continuo que baja con el plano. |
| 33–38 s | 03 · TRES PLANOS / **Tres cortes, *un mismo punto*.** | Planos axial, coronal y sagital cruzados en el abdomen; tres vistas 2D con la mira en el mismo punto. | Tres notas, una por plano. |
| 38–42 s | CAPTURA REAL DEL VISOR / **Ya corre en Meta Quest 3.** / Prototipo en desarrollo. | Dos capturas reales del prototipo en el visor. | El pad baja y queda en segundo plano. |
| 42–51,5 s | LO QUE SIGUE / **¿Dónde podría aportar *en la clínica*?** / tres posibilidades / *Posibilidades por explorar y validar juntos.* | El conjunto (volumen, órganos y plano) gira despacio. Aparecen: «Revisar un caso en equipo, en 3D», «Enseñar anatomía con estudios reales» y «Explicar un estudio al paciente». | Pad abierto, sin percusión. |
| 52–60 s | INGENIERÍA BIOMÉDICA · PUCP / **Medical Viewer** / Imágenes médicas en realidad mixta / ***Exploremos juntos el siguiente paso.*** | Cierre con el conjunto a la derecha. Desde el segundo 55,4 la imagen queda totalmente quieta. | Acorde final que se apaga hacia el segundo 56; silencio después. |

## Qué es real y qué es representación

En la esquina inferior izquierda, una etiqueta discreta indica en todo momento qué se está viendo.

| Momento | Qué es |
|---|---|
| 0–7 s, panel de cortes | **Real.** Son los cortes de la tomografía del proyecto, sin modificar. |
| 7–38 s y 42–60 s, volumen, órganos y planos | **Ilustración en 3D con datos reales.** El volumen sale de los mismos 267 cortes y los órganos se reconstruyen a partir de las máscaras de segmentación del proyecto. El dibujo lo hace el navegador, no el visor: el aspecto se parece al del prototipo, pero no es una grabación. |
| 17–21 s, panel «Sistemas» | **Recreación fiel de la interfaz del prototipo**, con los mismos textos, órganos, interruptores y colores (#1E2228, #F0F2F5, #A8B1BC, #4C8DFF). |
| 21–25 s, gesto de pinza | **Representación de la interacción.** Es un indicador gráfico, no una mano grabada. |
| 25–38 s, paneles 2D | **Reales.** Son los cortes axial, coronal y sagital de la serie, con las máscaras reales en color. |
| 38–42 s | **Captura real del prototipo en el Meta Quest 3.** Sale de una grabación del 30/09/2026; está comprimida por WhatsApp y se nota. |

El video no muestra navegación quirúrgica ni anatomía registrada sobre un paciente. Tampoco incluye cifras de exactitud, mediciones, NDI, logos ni avales. La herramienta de medición se deja fuera.

## Frase para enlazar con la exposición

> «Lo que acaban de ver sale de nuestro prototipo: una tomografía real convertida en un cuerpo que se puede recorrer con las manos. Hoy les queremos contar cómo funciona y, sobre todo, escuchar dónde creen ustedes que podría aportar.»

## Tomas que conviene grabar

Las capturas reales que tenemos son de baja calidad. Con estas tomas se puede reemplazar o reforzar cada escena:

1. **Volumen sobre una mesa real, con passthrough** (8–10 s): toma principal para «en tu espacio» (12,8–17 s). La cabeza debe moverse despacio alrededor del cuerpo.
2. **Pinza y giro del hígado con la mano** (6–8 s): para 21–25 s. Mano bien iluminada, sin el menú del sistema de Meta a la vista.
3. **Barrido del plano de corte** (8 s): para 25–33 s. El plano baja de forma continua y se ve la imagen del TAC sobre él.
4. **Tres planos cruzados** (5–6 s): para 33–38 s.
5. **Panel «Sistemas» encendiendo órganos** (5 s): para 17–21 s.
6. **Toma externa de una persona con el visor** usando las manos (5 s, con cámara o celular): puede abrir el bloque de la captura real.

Recomendaciones de grabación:
- Graba desde el visor con la mayor resolución de captura disponible, o transmite a la PC y graba con OBS.
- Pasa los archivos por cable o por Drive, no por WhatsApp.
- Antes de grabar, quita los paneles de depuración.
- Que no aparezcan datos de pacientes.

Para usar una toma nueva, colócala en `assets/captures/` y cambia los nombres de archivo en `src/main.js` (`capA` y `capB`). Los videos se pueden incluir con un `<video>` en el mismo panel `#capture`.

## Audio

**Locución incluida:** `assets/audio/voz_issa.mp3`, generada con ElevenLabs (modelo v4, voz «Issavoces», mujer joven peruana, tono calmado). `tools/mix_voice.py` la corta por sus pausas, coloca cada frase cuando aparece su texto, le da un tratamiento suave y la normaliza a -16 LUFS:

```bash
python3 tools/mix_voice.py assets/audio/voz_issa.mp3 out/video_apertura.mp4 out/video_apertura_voz.mp4
```

Si se cambia la toma de voz, hay que actualizar los cortes (`PHRASES`) con las pausas nuevas (`ffmpeg -i voz.mp3 -af silencedetect=noise=-40dB:d=0.25 -f null -`).

### Música (opcional)

El video se entiende sin sonido. Si quieren música:
- Usen una pista ambiental de 70–80 BPM, con pad y algún piano suave, sin voz ni percusión marcada.
- Fuentes sin costo: la Biblioteca de audio de YouTube o Pixabay Music. **Revisen la licencia de cada pista** antes de usarla.
- Efectos: el tic de los cortes, los clics de los interruptores, un barrido al formarse el volumen y silencio en la pausa final.

Locución opcional, en unos 55 s y a ritmo tranquilo:

> «Una tomografía son cientos de cortes, y hoy los vemos uno a uno. ¿Y si pudieras explorarlos frente a ti? Con realidad mixta, esos cortes se vuelven un cuerpo que aparece en tu espacio. Puedes encender los órganos que quieres ver, tomarlos y girarlos. Deslizar un plano y ver la imagen del TAC que le corresponde. Cruzar tres planos en un mismo punto. Ya corre en Meta Quest 3. Es un prototipo, y queremos explorar con ustedes dónde puede aportar. Exploremos juntos el siguiente paso.»

## Cambiar textos

- Los textos del cierre (nombre del proyecto, subtítulo, equipo y frase final) están en `CONFIG`, al inicio de `src/main.js`.
- Los demás textos y sus tiempos están en `BLOCKS`, en el mismo archivo. Una palabra entre `<b>…</b>` se pinta de azul.
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
npm run video          # out/video_apertura.mp4
```

`npm run video` dibuja los 1800 fotogramas uno a uno y se los pasa a ffmpeg. En una PC usa la GPU y tarda unos minutos; en Linux sin GPU dibuja por software y puede tardar más de una hora (con `--gpu` se fuerza la GPU). Para exportar solo un tramo: `node tools/render.mjs --from 17 --to 25 --out out/tramo.mp4`.

### Vista previa interactiva

```bash
cd VideoApertura
npx http-server -p 8080 .     # o: python3 -m http.server 8080
# abrir http://localhost:8080/src/index.html  (con ?t=23 salta a ese segundo)
```

La vista previa reproduce la animación en tiempo real y tiene una barra para moverse por el video. En una laptop sin GPU dedicada puede ir a saltos. **El archivo final es el MP4**: la vista previa sirve para revisar y ajustar.

## Archivos

- `src/index.html`: escenario de 1920 × 1080, estilos y paneles.
- `src/main.js`: línea de tiempo, textos y cámara. `renderAt(t)` dibuja cualquier instante.
- `src/gl.js`: motor 3D (three.js). Hace el raymarching del volumen, dibuja las mallas de los órganos y los planos de corte.
- `tools/prepare_data.py`: convierte la serie de TAC y las máscaras en volumen, mallas y cortes.
- `tools/render.mjs`: exporta fotogramas o el MP4.
- `assets/captures/`: capturas reales del visor (versionadas). El resto de `assets/` se genera.

## Revisión antes de entregar

- **Legibilidad:** los títulos van de 64 a 112 px y el texto secundario es de al menos 30 px, todo en blanco o azul sobre #1E2228. Cada bloque de texto queda en pantalla 2,5 s o más.
- **Continuidad:** el corte de la pantalla es el mismo que se coloca en el volumen. La pila se funde en el volumen y el mismo cuerpo sostiene todas las demostraciones.
- **Final:** la imagen queda quieta desde el segundo 55,4 hasta el 60.
- **Referencias:** no se reprodujeron los reels de Instagram de referencia. La estructura se basa en la descripción del encargo: situación reconocible, demostración paso a paso y cierre sencillo, con texto a la izquierda y demostración a la derecha.
