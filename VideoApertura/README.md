# INMERSIA · video de apertura

Pitch de 66 s para presentar el proyecto, de carácter **académico**, a la Clínica Anglo
Americana: horizontal (1920 × 1080, 30 fps), con locución (ElevenLabs, voz «Camila», frases
fluidas), texto que entra palabra por palabra cuando la voz lo dice y música original a 120 BPM.
Estilo de reel: fondo negro con brillo azul, plataforma holográfica bajo el cuerpo, polvo de luz,
etiquetas con el volumen de cada órgano (como en el visor) y un modo cirugía como concepto.
Todo se dibuja con código a partir de los datos reales del proyecto.

## Guion

| Segundo | Escena | Voz / qué se ve |
|---|---|---|
| 0 – 8 | Gancho | «Son las siete de la mañana. La cirugía es a las nueve… y todo el caso está en **267** imágenes.» La pantalla de cortes flota inclinada y el número cuenta desde cero. |
| 8 – 14 | El problema | «Hoy se revisan una por una, y el cuerpo se termina armando… ~~en la cabeza~~.» Un clic por cada corte que avanza. |
| 14 – 19 | La pregunta | «¿Y si pudieran verlo completo?» El corte sale de la pantalla y los cortes forman el cuerpo sobre la plataforma de luz. |
| 19 – 24 | Marca | «Les presentamos **Inmersia**. Cirugía inmersiva.» Golpe de la música en 20 s, destello y logo. |
| 24 – 37 | Demostración | «Cada órgano aparece segmentado, con su volumen» (Hígado 1665 mL · Estómago 436 mL · Páncreas 67 mL · Vesícula 12 mL) · «Lo toman con la mano» · «lo cortan donde quieran… y la tomografía los acompaña» · «en tres planos». |
| 37 – 41 | Meta Quest 3 | «Y esto ya funciona en Meta Quest 3.» El cuerpo completo con sus órganos y el plano. |
| 41 – 50,5 | Modo cirugía (concepto) | «Lo que viene es el modo cirugía: marcar la zona a tratar, ver sus márgenes y planificar el abordaje… antes de entrar al quirófano.» La mano pulsa el botón, el hígado se vuelve de vidrio y aparecen la zona, los márgenes y tres trayectorias. |
| 50,5 – 56 | Lo que buscamos | «Hoy es un prototipo. Para llevarlo al quirófano, buscamos un aliado clínico.» Hoja de ruta corta y tres píldoras: cirujanos que lo prueben y nos guíen, casos anonimizados para investigar, validarlo juntos con rigor académico. |
| 56 – 60 | Invitación | «Construyámoslo juntos, con la Clínica Anglo Americana.» |
| 60 – 66 | Logo | «Inmersia.» INMERSIA · Cirugía inmersiva · Proyecto de investigación · Ingeniería Biomédica · PUCP. Quieto desde 61,8 s. |

## Qué es real, qué es ilustración y qué es concepto

Una etiqueta discreta abajo a la izquierda lo dice en cada escena:

- **Real:** los cortes de la tomografía del proyecto. Los volúmenes de los órganos son los que
  mide el visor sobre la segmentación (`OrganTag` en la escena de Unity).
- **Ilustración con datos del proyecto:** el cuerpo en 3D, los órganos, los planos y los paneles.
- **Representación:** la mano que toma el hígado y la que pulsa el botón.
- **Concepto en desarrollo:** el modo cirugía. La zona, los márgenes y las trayectorias son
  ilustrativos: no marcan ningún hallazgo del paciente.

No se muestran resultados clínicos, navegación quirúrgica, datos de pacientes, logos de la
clínica ni de instituciones que financien. No se usan las capturas del visor.

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

En Linux se usa `python3`. Con tarjeta gráfica toma unos minutos; sin ella, más de una hora.

Para revisar sin exportar todo:

- `npm run fotos` guarda fotogramas sueltos en `out/stills/`.
- Vista previa en el navegador: `npx http-server -p 8080` (o cualquier servidor estático) y
  abrir `http://localhost:8080/src/index.html`. Con `?t=36` abre en ese segundo. Si existe
  `out/banda_sonora.mp3`, suena con la voz y la música.

## Dónde cambiar cosas

- **Textos y tiempos:** `BLOCKS` en `src/main.js`. `<b>` pone la palabra en azul y la subraya,
  `<s>` la tacha y `<n>267</n>` hace que el número cuente.
- **Nombre, lema, equipo y clínica:** `CONFIG` en `src/main.js`.
- **Volúmenes de los órganos, hoja de ruta y lo que buscamos:** `src/index.html` (`.otag`,
  `#road`, `#chips`).
- **Escenas 3D:** se animan en su propio reloj y `WARP` las encaja en el guion. El modo cirugía
  está en `buildSurgery` y `updateSurgery`.
- **Luz:** la plataforma, el polvo y el brillo (bloom) están en `src/gl.js`.
- **Voz:** `assets/audio/locucion_inmersia.mp3` es una sola toma; `VOICE_CUTS` en
  `tools/mix_audio.py` la corta en los silencios entre frases y dice en qué segundo entra cada una.
- **Música:** `tools/compose_music.py`. Si mueves una escena, mueve también su segundo en
  `CUES` para que el golpe siga cayendo en el cambio.

Las versiones anteriores siguen en el historial de git.
