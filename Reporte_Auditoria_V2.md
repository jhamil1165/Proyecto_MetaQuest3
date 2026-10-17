# Reporte de Auditoría de Rendimiento V2 · Meta Quest 3

| Campo | Valor |
|---|---|
| Proyecto | Proyecto_MetaQuest3, rama `claude/inspiring-lamport-nqjcn9` |
| Fecha | 16/10/2026 |
| Referencia | `Reporte_Auditoria_Quest3.pdf` (V1, commit `eb52efbb`). Se conservan los mismos ID de hallazgo. |
| Entorno | Unity 2022.3.62f1 · URP 14.0.12 · XRI 2.6.5 · OpenXR 1.14.3 (Single Pass Instanced, Multiview en Android) |

## Alcance y limitaciones

- Esta versión revisa las correcciones aplicadas a R1/R2, U1, U2/R5, SH1 y S1, y vuelve a revisar `Assets` en busca de efectos secundarios.
- **No se ha compilado ni ejecutado nada en Unity ni en el visor**: este entorno no tiene editor de Unity, carpeta `Library` ni Quest 3. Los 6 scripts modificados pasan un análisis sintáctico de C# (tree-sitter, sin errores). La validación semántica (tipos, API de Unity) y la compilación de los shaders HLSL quedan pendientes y deben hacerse abriendo el proyecto en Unity.
- Las mejoras de rendimiento son razonadas a partir del código y no están medidas. Para confirmarlas: Frame Debugger (SRP Batcher), Memory Profiler (texturas) y OVR Metrics Tool (GPU/fps) en el dispositivo.

## Resumen de la verificación

| ID V1 | Problema | Estado V2 | Motivo |
|---|---|---|---|
| R1 | Fugas de `UnityWebRequest` al mover el slider | **Resuelto en la ruta de la build** | Cada carga aborta y libera la anterior, y se libera al terminar y en `OnDestroy`. Queda un caso acotado (ver 2.1). |
| R2 | Texturas legibles (copia en RAM) | **Resuelto** | `GetTexture(url, nonReadable: true)` en las 3 llamadas. |
| SH1 | Shaders Holo sin soporte de Single Pass Instanced | **Resuelto en el código; falta validarlo en Link** | Los 3 shaders tienen ya las 6 macros e inicializan la salida. |
| S1 | Saturación del bucle de raymarching | **Mitigado parcialmente** | Se elimina la transformación de matriz por paso (coste de ALU). El cuello de botella principal, de hasta 8 lecturas de textura 3D por paso, sigue igual. |
| U1 | `MaterialPropertyBlock` desactiva el SRP Batcher | **Mitigado parcialmente** | El plano de corte ya no usa MPB, pero siguen 7 scripts que escriben MPB en los mismos órganos, el volumen y los paneles. |
| U2 / R5 | Trabajo y asignaciones por frame | **Resuelto** | Sin arrays nuevos por frame. Las líneas 2D solo se recalculan si algo cambia. |

## 1. Cambios aplicados

| Archivo | Cambio |
|---|---|
| `Assets/Scripts/CTPlaneView.cs` | Campos `_req` y `_overlayReq`; `CancelReq()` y `CancelOverlayReq()` (Abort + Dispose + null). Se llaman al inicio de `LoadSlice`/`LoadOverlay`, al fallar o terminar cada carga, en `SetOverlay(null)` y en `OnDestroy`. `GetTexture(url, nonReadable: true)`. Se quitan los bloques `using`. |
| `Assets/Scripts/CTSliceViewer.cs` | Lo mismo con un único `_req` (no está en la escena de build). |
| `Assets/ClippingPlane/Scripts/ClippingPlaneController.cs` | Sin `MaterialPropertyBlock`: `Shader.SetGlobalVector("_PlaneNormal")` y `Shader.SetGlobalFloat("_PlaneDistance")` una vez por frame. Nuevo `IsClipping` público. `OnDisable` deja el corte desactivado. |
| `Assets/Scripts/VolumeCutSlice.cs` | Ya no escribe el plano en el volumen (se elimina su MPB). Lee `controller.IsClipping` en lugar de deducir el estado del MPB. Mantiene el MPB de la rebanada (`_VolumeWorldToObject`), que no es el plano. |
| `Assets/ClippingPlane/Shaders/ClippingPlane_URP.shader` | `_PlaneNormal`/`_PlaneDistance` salen del bloque Properties y del CBUFFER `UnityPerMaterial` y pasan a ser uniforms globales. |
| `Assets/Shaders/VolumeRender.shader` | Igual que el anterior, más el cambio S1: el plano se pasa a espacio objeto en el vertex shader (`planeOS`, TEXCOORD2) y el bucle compara `dot(position, planeOS.xyz) + planeOS.w`. |
| 7 `.mat` (`Mat_ClippingPlane`, `Mat_Clip_*` ×5, `Mat_Volumen`) | Se eliminan las entradas guardadas `_PlaneDistance`/`_PlaneNormal` (2 líneas por archivo). |
| `Assets/Shaders/HoloOutline.shader`, `HoloGlassPanel.shader`, `HoloPlatform.shader` | `#pragma multi_compile_instancing`, `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `Varyings OUT = (Varyings)0`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO` y `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`. |
| `Assets/Scripts/CTMeasure2D.cs` | `LateUpdate` envuelto en `if (transform.hasChanged \|\| LinesOutdated())`. Cada medida guarda el tamaño de imagen con el que se dibujó (`drawnFor`). |
| `Assets/Scripts/OrganInfoPanelController.cs` | Lista de renderers creada en `Awake` y rellenada en `Show()` al cambiar de órgano. `LateUpdate` solo recoloca si cambió el órgano o la cámara (`hasChanged`). |

### Desviaciones respecto a las instrucciones literales (y por qué)

1. **Dos peticiones en `CTPlaneView`, no una.** `Refresh()` lanza la carga del corte y, a continuación, la de la máscara. Con un único `_req`, el `CancelReq()` del inicio de `LoadOverlay` abortaría la imagen recién pedida y el visor no mostraría ningún corte con la máscara activa.
2. **Sin `using`.** Con `using`, la petición se liberaría dos veces (al salir del bloque y en `CancelReq`). Ahora se libera una sola vez con `CancelReq()`.
3. **Las globales exigían tocar los shaders y los `.mat`.** Una propiedad declarada en Properties/`UnityPerMaterial` toma el valor del material, y `SetGlobal*` solo se aplica cuando el material no la define. Por eso se sacaron de Properties y del CBUFFER. No he podido confirmar si un valor guardado en el `.mat` para una propiedad ya no declarada sigue teniendo prioridad sobre la global, así que se eliminaron esas entradas de los 7 materiales para no depender de ello.
4. **`transform.hasChanged` del propio objeto no basta.**
   - En `CTMeasure2D`, el componente no se mueve cuando la imagen cambia de tamaño o se añade una medida. Solo con `transform.hasChanged`, las medidas nuevas se quedarían con la línea de largo cero, que es el error que documenta el propio script. Se añade la comprobación de tamaño por medida.
   - En `OrganInfoPanelController`, el panel lo mueve el mismo `LateUpdate`. Mirar su propio `hasChanged` dejaría el panel quieto al agarrar el órgano o mover la cabeza, así que se miran el órgano y la cámara.
5. **"Cachear en `Awake`".** El órgano mostrado solo se conoce al llamar a `Show()`. En `Awake` se crea la lista y en `Show()` se rellena, sin crear arrays.
6. **"Inicializa la salida".** Se interpreta como `Varyings OUT = (Varyings)0;` en `vert`, que es donde se crea la estructura de salida; el fragment shader no tiene estructura de salida que inicializar.

## 2. Verificación detallada

### 2.1 Fugas de texturas `UnityWebRequest` (antiguo R1 y R2)

**Antes:** `StopCoroutine` cortaba la corrutina dentro del `using` y el `Dispose` nunca se ejecutaba. La petición seguía descargando y decodificando, y la textura nativa de `DownloadHandlerTexture` vivía hasta que actuaba el finalizador del GC.

**Ahora** (`CTPlaneView.cs`):
- Inicio de cada carga: `CancelReq()` en la línea 250 y `CancelOverlayReq()` en la 220 abortan y liberan la petición anterior.
- Carga terminada: tras `GetContent`, se libera la petición (líneas 238 y 268); la textura obtenida pasa a ser del script, que ya la destruía al cambiar de corte.
- Carga fallida: se libera antes de `yield break` (líneas 233 y 263).
- Guardia `request != _req` / `request != _overlayReq` (líneas 228 y 258): si otra carga ya liberó la petición, no se toca un objeto liberado.
- `SetOverlay(null)` (línea 188) y `OnDestroy` (líneas 100-101) liberan lo que quede.
- `nonReadable: true` (líneas 225 y 255): sin copia de píxeles en RAM. Comprobado que ningún script lee píxeles de esas texturas (`grep GetPixel` sin resultados); `CTMeasure2D` y `MillimetresPerPixel` solo usan `width`/`height`.

**Efecto esperado:** como máximo hay 1 petición de imagen y 1 de máscara vivas por plano (6 por visor), en lugar de una por cada tick del slider. La memoria por textura viva baja aproximadamente a la mitad (1 MB por corte de 512×512 en lugar de 2 MB), según la documentación de `nonReadable`.

**Riesgo residual (bajo):**
- Si el GameObject del visor se desactiva a mitad de una carga (cambio de vista en el menú), Unity detiene la corrutina y la petición queda viva hasta la siguiente carga o el `OnDestroy`. Está acotado a una petición por plano. Se resolvería con `CancelReq()` en `OnDisable`, que no se ha añadido por no estar pedido.
- `DicomVolumeViewer.cs:201` y `:220` mantienen `using` dentro de una corrutina. Solo se usa en `DICOM_Test.unity`, fuera de la build.

### 2.2 Single Pass Instanced en los shaders Holo (antiguo SH1)

| Shader | multi_compile_instancing | INPUT_INSTANCE_ID | OUTPUT_STEREO | (Varyings)0 | SETUP_INSTANCE_ID | INIT_VERTEX_OUTPUT_STEREO | SETUP_STEREO_EYE_INDEX_POST_VERTEX |
|---|---|---|---|---|---|---|---|
| HoloOutline | ✔ 31 | ✔ 38 | ✔ 44 | ✔ 55 | ✔ 56 | ✔ 57 | ✔ 68 |
| HoloGlassPanel | ✔ 33 | ✔ 40 | ✔ 48 | ✔ 63 | ✔ 64 | ✔ 65 | ✔ 76 |
| HoloPlatform | ✔ 30 | ✔ 37 | ✔ 44 | ✔ 56 | ✔ 57 | ✔ 58 | ✔ 67 |

Los 3 shaders siguen ahora el patrón que Unity documenta para Single Pass Instanced: macro en la entrada, macro estéreo en la salida e inicialización en `vert`. Con esto, todos los shaders propios de `Assets/Shaders` y `Assets/ClippingPlane/Shaders` declaran las macros estéreo.

**Pendiente de verificar en Unity:**
1. Que compilan para Vulkan (Android) y D3D11 (Standalone).
2. En Quest Link, que los paneles, las peanas y el contorno de selección aparecen en los dos ojos. El shader `XR/StereoEyeIndexColor` sirve de control.

**Fuera del alcance de esta corrección:** 7 shaders de `Assets/TextMesh Pro/Shaders` no declaran `UNITY_VERTEX_OUTPUT_STEREO` (`TMP_Bitmap*`, `TMP_SDF-Surface*`, `TMP_SDF-Mobile Masking`, `TMP_SDF-Mobile SSD`). El material del proyecto `Mat_TMP_Holo` usa `TMP_SDF-Mobile`, que sí las tiene. No he comprobado si las fuentes de la escena usan alguno de esos 7.

### 2.3 Bucle `for` de VolumeRender (antiguo S1)

**Cambio:** el plano se pasa una sola vez, en el vertex shader, de mundo a coordenadas del cubo (`VolumeRender.shader:133-137`). Con `p_mundo = A·p + t`, la condición `dot(n, p_mundo) + d > 0` equivale a `dot(Aᵀn, p) + (dot(n, t) + d) > 0`. La normal resultante no se normaliza porque solo importa el signo. Dentro del bucle (línea 229) queda un producto escalar de 3 componentes y una suma.

| Coste por paso del bucle (`_Steps = 128`) | Antes | Ahora |
|---|---|---|
| Transformación objeto→mundo (`TransformObjectToWorld`, matriz 4×4) | 1 | **0** |
| Prueba del plano | dot3 + suma | dot3 + suma |
| Lecturas 3D base (densidad + órganos) | 2 | 2 |
| Lecturas 3D del gradiente (solo si alfa > 0,02) | 6 | 6 |
| Transformaciones en `Lighting()` (solo si alfa > 0,02) | 2 + 2 normalize | 2 + 2 normalize |

**Conclusión:** se quita del bucle del orden de una docena de multiplicaciones-suma por paso, unas 1 500 por píxel en el peor caso (estimación). El cálculo nuevo se hace una vez por vértice del cubo, que tiene 8. Es una mejora real de ALU, pero el coste dominante del shader es el ancho de banda de textura: hasta 1 024 lecturas 3D por píxel en el peor caso, y eso no ha cambiado. Por eso S1 queda **mitigado parcialmente** y sigue siendo el principal riesgo de GPU.

**Siguientes pasos para S1, por impacto estimado:**
1. Usar el `planeOS` ya disponible para recortar `tmin/tmax` de forma analítica antes del bucle y no recorrer la parte cortada del rayo.
2. Gradiente más barato (diferencias hacia delante, 3 lecturas) o precalculado en una Texture3D de baja resolución.
3. Pasos adaptativos (48-64 mientras se manipula).
4. Sacar del bucle `TransformObjectToWorldDir(rayDirOS)` en `Lighting()` (línea 183), que es constante por píxel.

### 2.4 SRP Batcher tras quitar los MaterialPropertyBlock (antiguo U1)

**Compatibilidad de los shaders (comprobada en el código):** en `ClippingPlane_URP` y `VolumeRender`, todas las propiedades del bloque Properties están en `UnityPerMaterial`. El plano es ahora un uniform global, fuera del CBUFFER y sin entrada en Properties, que es lo que el SRP Batcher admite. Los shaders Holo también cumplen.

**Compatibilidad de los renderers:** quitar el MPB del plano no basta, porque **siguen 7 escritores de MPB**. Un renderer con MPB queda fuera del SRP Batcher, según la documentación de Unity:

| Script:línea | Renderers afectados | Cuándo | Efecto |
|---|---|---|---|
| `OrganSelectionInfo.cs:113` | **Todos los renderers de cada órgano**, incluido el del material de corte | Desde el primer hover | Los órganos solo usan el SRP Batcher hasta que el usuario apunta a uno |
| `VolumeDisplay.cs:80` | Volumen 3D | Siempre (en `OnEnable`) | El volumen nunca usa el SRP Batcher (1 draw: impacto bajo) |
| `TriPlaneView.cs:109` | 3 planos del corte triple | Cada frame si está visible | Fuera del SRP Batcher |
| `VolumeCutSlice.cs:75` | Rebanada del TAC | Cada frame con el corte activo | Fuera del SRP Batcher |
| `HoloHoverGlow.cs:61` | Botones del menú | Desde el primer hover | Fuera del SRP Batcher |
| `MedicalMenuIntro.cs:114` | Paneles del menú | En cada animación de entrada | Fuera del SRP Batcher |
| `NdiScreenStatus.cs:93` | Pantalla NDI | Al cambiar el estado | Fuera del SRP Batcher (KlakNDI también usa MPB) |

**Conclusión:** **mitigado parcialmente**. La mejora clara es de CPU: antes había 6 Get/SetPropertyBlock por frame en el controlador y 3 en `VolumeCutSlice`; ahora son 2 llamadas `SetGlobal*` por frame. Los órganos solo usan el SRP Batcher mientras nadie haya apuntado a ellos.

**Para cerrarlo:**
- `OrganSelectionInfo`: llamar a `SetPropertyBlock(null)` cuando el contorno vuelva a 0, o mejor, controlar el contorno con una instancia de material propia o un renderer hijo exclusivo para el contorno. Esto resuelve también S5.
- `VolumeDisplay`: escribir los modos en una instancia de material creada una vez.
- `TriPlaneView` y `VolumeCutSlice`: `_VolumeWorldToObject` puede ir en el material del corte.

**Cómo confirmarlo:** Frame Debugger → "SRP Batch" y el motivo de cada ruptura ("MaterialPropertyBlock").

## 3. Riesgos introducidos por los cambios

| Riesgo | Severidad | Detalle y mitigación |
|---|---|---|
| El plano global corta todo material con esos shaders, esté o no en `targets` | Baja | Hoy es equivalente: en `SampleScene` los 6 `targets` son exactamente los renderers con `Mat_Clip_*` (×5) y `Mat_Volumen` (comprobado por GUID). `targets.Count == 0` sigue desactivando el corte. |
| Las herramientas de editor reescriben el plano en los materiales | Media | `Editor/MedicalClippingSetup.cs:233-234` y `Editor/MedicalVolumeSetup.cs:72-75` vuelven a guardar `_PlaneDistance`/`_PlaneNormal` en los `.mat` si se ejecutan. Si Unity da prioridad a ese valor guardado (no confirmado), el corte dejaría de verse. `MedicalVolumeModesRender.cs:242-307` y `MedicalClippingSetup.cs:333-367` aplican el plano con MPB en sus capturas de editor; su resultado con el uniform global no está verificado. No se han modificado (fuera de lo pedido). |
| Valor inicial del plano | Baja | Antes del primer `LateUpdate`, las globales valen 0: no se corta nada. Es el comportamiento deseado. |
| Se pone `hasChanged = false` en el órgano y en la cámara | Baja | Ningún script del proyecto lee `hasChanged` (comprobado con grep). Los paquetes (XRI, Meta SDK) no se han podido revisar. En VR la cámara se mueve casi cada frame, así que el panel se sigue recolocando casi siempre; la ganancia real de R5 es que ya no se crean arrays. |
| Renderers cacheados en `OrganInfoPanelController` | Baja | La lista se toma al mostrar el órgano. Si se destruyera un renderer hijo mientras el panel está visible, `bounds` lanzaría una excepción. Hoy los órganos se activan y desactivan, no se destruyen. |
| Sin compilar | Media | Hay que abrir el proyecto en Unity y revisar la consola y la compilación de shaders para Android antes de generar el APK. |

## 4. Estado del resto de hallazgos de V1

| ID | Estado |
|---|---|
| S2 (clip en cola Geometry, Cull Off), S3 (variantes de sombra), S4 (sin ShadowCaster/DepthOnly), S5 (contorno siempre dibujado y sin recortar), S6 (derivadas tras discard), S7 | Sin cambios |
| R3 (RGBA32 para escala de grises), R4 (cadenas por tick del slider), R6, R10, R11, R12 | Sin cambios |
| R7, R8 (fo-dicom) | Sin cambios; fuera de la build |
| U3 (raycasting de UI de XRI), U4 (sombras y luces de URP), U5 | Sin cambios |

## 5. Próximos pasos recomendados

1. Abrir el proyecto en Unity 2022.3.62f1: comprobar que no hay errores de compilación de C# ni de shaders, y probar el corte (órganos y volumen), el slider de los tres planos con y sin máscara, y la regla 2D.
2. Quest Link con `XR/StereoEyeIndexColor`: confirmar SH1.
3. Frame Debugger: contar los SRP Batch antes y después de apuntar a un órgano y aplicar el cierre de U1 descrito en 2.4.
4. OVR Metrics Tool: medir el GPU con el volumen a pantalla completa y aplicar los siguientes pasos de S1 (recorte analítico de `tmin/tmax` con `planeOS`).
5. Adaptar las herramientas de editor que escriben `_PlaneNormal`/`_PlaneDistance` en materiales o MPB.

## Fuentes

- Macros de Single Pass Instancing: [Unity Manual – Single Pass Instancing](https://docs.unity3d.com/6000.1/Documentation/Manual/SinglePassInstancing.html)
- Multiview en Android: [Unity Manual 2022.3 – Android Single Pass Stereo Rendering](https://docs.unity3d.com/cn/2022.3/Manual/Android-SinglePassStereoRendering.html)
- MPB y SRP Batcher: [Unity Manual – SRP Batcher incompatibilidades](https://docs.unity.cn/Manual/SRPBatcher-Incompatible.html)
- `nonReadable` de `GetTexture`: [Unity Scripting API 2022.3 – UnityWebRequestTexture.GetTexture](https://docs.unity.cn/ScriptReference/Networking.UnityWebRequestTexture.GetTexture.html)
- Globales usadas solo si el material no define la propiedad: [Unity Scripting API – Shader.SetGlobalFloat](https://docs.unity3d.com/450/Documentation/ScriptReference/Shader.SetGlobalFloat.html)
- `finally` no ejecutado al detener corrutinas: [Unity Issue Tracker](https://issuetracker-mig.prd.it.unity3d.com/issues/finally-block-of-try-slash-finally-in-coroutine-is-not-called-when-object-is-destroyed) y [Unity Forum](https://forum.unity.com/threads/proper-way-to-stop-coroutine.704210/)
