# INMERSIA · video de apertura

Pitch de 60 s para presentar el proyecto, de carácter **académico**, a la Clínica Anglo
Americana: horizontal (1920 × 1080, 30 fps), con locución (ElevenLabs, voz «Daniela - Soothing &
Relaxed»: suave, tranquila y cálida), texto que entra palabra por palabra cuando la voz lo dice
y música original a 120 BPM.
Estilo de reel: fondo negro con brillo azul, plataforma holográfica bajo el cuerpo, polvo de luz,
etiquetas con el volumen de cada órgano (como en el visor) y las reglas 2D y 3D. Muestra solo lo
que el prototipo ya hace. Todo se dibuja con código a partir de los datos reales del proyecto.

## Guion

| Segundo | Escena | Voz / qué se ve |
|---|---|---|
| 0 – 5 | Gancho | «Toda cirugía… empieza **antes** del quirófano.» Texto grande al centro, sobre el negro. |
| 5 – 10 | La tomografía | «Empieza en una tomografía: **267** imágenes…» La pantalla de cortes flota inclinada y el número cuenta desde cero. |
| 10 – 13 | El problema | «…que hoy se revisan una por una… imaginando el resto.» Un clic por cada corte que avanza; «imaginando el resto» queda borroso. |
| 13 – 18 | La pregunta | «¿Y si pudieras verlo completo… frente a ti?» El corte sale de la pantalla y los cortes forman el cuerpo sobre la plataforma de luz. |
| 18 – 24 | Marca | «Te presentamos… **Inmersia**. Cirugía inmersiva.» Golpe de la música en 20 s, destello y logo. |
| 24 – 37 | Demostración | «Cada órgano, segmentado… y con su volumen» (Hígado 1665 mL · Estómago 436 mL · Páncreas 67 mL · Vesícula 12 mL) · «Tómalo con la mano» · «córtalo donde quieras… y la tomografía te sigue» · «Tres planos. Un mismo punto.» |
| 37 – 41 | Regla 2D | «Mide sobre cada corte… en milímetros reales.» El corte 116 con su segmentación y una regla de lado a lado del hígado: 132,8 mm. |
| 41 – 45,5 | Regla 3D | «Y también en 3D… sobre el propio órgano.» Una regla sobre el hígado (19,5 cm); la mano lo toma y lo gira, y la medida lo acompaña. |
| 45,5 – 49,5 | Meta Quest 3 | «Y todo esto… ya funciona en Meta Quest 3.» El cuerpo completo y el panel «Avances del prototipo»: tomografía y segmentación, órganos 3D con su volumen, plano de corte y tres planos, reglas 2D y 3D, manos y mandos a tamaño real. |
| 49,5 – 54 | Invitación | «Construyámoslo juntos… con la Clínica Anglo Americana.» |
| 54 – 60 | Logo | «Inmersia.» INMERSIA · Cirugía inmersiva · Proyecto de investigación · Ingeniería Biomédica. Quieto desde 55,8 s. |

## Qué es real y qué es ilustración

Una etiqueta discreta abajo a la izquierda lo dice en cada escena:

- **Real:** los cortes de la tomografía del proyecto. Los volúmenes de los órganos son los que
  mide el visor sobre la segmentación (`OrganTag` en la escena de Unity). Las dos medidas son
  distancias reales sobre los datos: 132,8 mm sobre la máscara del corte 116 y 19,5 cm entre dos
  puntos de la superficie del hígado segmentado.
- **Ilustración con datos del proyecto:** el cuerpo en 3D, los órganos, los planos y los paneles.
- **Representación:** la mano que toma el hígado.
- Los avances del panel son los que ya tiene el visor (ver `CLAUDE.md` en la raíz del repo).

No se muestran resultados clínicos, navegación quirúrgica, planes a futuro, datos de pacientes,
logos de la clínica ni de instituciones que financien. No se usan las capturas del visor.

## Cómo generarlo

Desde la carpeta `VideoApertura` (la primera vez: `npm install`,
`pip install numpy pillow scipy scikit-image imageio-ffmpeg` y `python tools/prepare_data.py`):

```
npm run video                                      # imagen → out/video_apertura.mp4
python tools/compose_music.py out/musica.wav       # música original
python tools/mix_audio.py out/video_apertura.mp4   # video final → out/inmersia.mp4
```

Quedan dos versiones: `out/inmersia.mp4` (voz y música) y `out/inmersia_musica.mp4` (solo
música, por si se narra en vivo). Mientras no esté la toma de voz
(`assets/audio/locucion_daniela.mp3`), sale solo la de música.

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
- **Escenas 3D:** se animan en su propio reloj y `WARP` las encaja en el guion. Las reglas están
  en `RULER2D` y `buildRuler3D`; el panel de avances, en `#recap` de `src/index.html`.
- **Luz:** la plataforma, el polvo y el brillo (bloom) están en `src/gl.js`.
- **Voz:** `assets/audio/locucion_daniela.mp3` es una sola toma; `VOICE_CUTS` en
  `tools/mix_audio.py` la corta en los silencios entre frases y dice en qué segundo entra cada una.
- **Música:** `tools/compose_music.py`. Si mueves una escena, mueve también su segundo en
  `CUES` para que el golpe siga cayendo en el cambio.

Las versiones anteriores siguen en el historial de git.
