# Medical Viewer / INMERSIA — Meta Quest 3

Visor médico en realidad mixta para Meta Quest 3 (curso "Proyecto XR en Cirugía", Ing. Biomédica
PUCP-UPCH). Muestra una tomografía (DICOM) en 2D, su segmentación, órganos 3D y un volumen 3D
(volume rendering), con la habitación real de fondo (passthrough). Se presenta como **INMERSIA**.

Repo: `jhamil1165/Proyecto_MetaQuest3`. Equipo: Jhamil (jhamil1165), Sebaa_21, Bárbara.
Todo en español: código, comentarios, commits y respuestas.

## Reglas del equipo

- **No borrar trabajo ajeno.** Ej.: el objeto `Sphere` (esfera gris de 20 cm, commit "esfera" de
  Sebaa_21) se ve en todas las vistas; no se ha quitado porque es suyo: preguntar.
- **Unity cerrado** para correr pasos en batchmode (si está abierto, el batchmode falla).
- Antes de subir: `git fetch origin main` y fusionar si hay commits nuevos.
- `main` y `feat/holographic-ui` se mantienen iguales: tras cada commit,
  `git push origin feat/holographic-ui && git push origin feat/holographic-ui:main`.
- No subir `VideoApertura/referencias/` (vídeos de referencia) ni `Builds/`.
- Estilo de interfaz: vidrio oscuro (dark glass). No volver a paneles blancos.

## Entorno

- Unity **2022.3.62f1**: `C:/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor/Unity.exe`
- Escena única: `Assets/Scenes/SampleScene.unity`
- adb (no está en el PATH):
  `C:/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe`
- Librerías: Meta XR SDK 201 (`com.meta.xr.sdk.all`: cámara, passthrough, OVRHand/OVRInput;
  el Interaction SDK de Meta está instalado pero **no se usa**), XR Interaction Toolkit 2.6.5
  (rayos, agarre, UI), OpenXR 1.14.3, URP 14, TextMeshPro, Klak NDI 2.1.6 (vídeo del encoder),
  fo-dicom (NuGet).

## Cómo se trabaja: pasos de editor en batchmode

Los cambios de escena se hacen con scripts de editor (`Assets/Editor/*.cs`, menú
`MedicalViewer/StepNN`), idempotentes, que dejan un informe en `Logs/EditorAutomation/stepNN_output.txt`.

```bash
"/c/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor/Unity.exe" -batchmode -projectPath . \
  -executeMethod MedicalHandTuning.Apply -quit -logFile Logs/EditorAutomation/step103_log.txt
grep "error CS" Logs/EditorAutomation/step103_log.txt   # compilación
```

- **Compilar APK:** `MedicalBuild.Apply` (Step95) → `Builds/MedicalViewer_AAAAMMDD_HHMM.apk` (~300 MB).
- **Prueba en modo juego:** `Assets/Tests/PlayMode/ViewSwitchTest.cs` arranca la escena, pulsa
  todos los botones de Modelo 3D, cambia de vista y escribe qué queda encendido en
  `Logs/EditorAutomation/vistas.txt`:
  `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/EditorAutomation/playmode_results.xml`
- Los paneles que no se pueden ver en batchmode se verifican renderizando a PNG (ver Step99, Step100).
- Instalar en el Quest: Meta Quest Developer Hub o SideQuest (arrastrar el .apk), o
  `adb install -r <apk>`. Desde el navegador del propio Quest NO se instala.
- Para ver el visor a distancia: el Quest transmite a `oculus.com/casting` en Chrome y se
  comparte esa pestaña en una videollamada.

## Arquitectura (lo esencial)

- **Rig:** `[BuildingBlock] Camera Rig` (OVRCameraRig + OVRManager) está **girado 180° y desplazado**
  (-0.23, 0, 2.54). Todo lo que use poses de seguimiento debe pasar por `TrackingSpace`.
- **Vistas:** `MedicalMenuActions` (Assets/MedicalMenuActions.cs) enciende un grupo por vista
  (DICOM, Segmentación, Modelo 3D) y al cambiar llama `ResetTools()` (quita corte, tres planos,
  deja de medir). Arranca en "solo menú". "Salir" cierra la app.
- **Manos:** puente casero `OVRHandXRController` (OVRHand → controlador XRI): pinza 0.45/0.25,
  rayo desde `PointerPose` con raíz en TrackingSpace, 6° hacia arriba, suavizado, congelado al
  empezar y al soltar la pinza. `HandNearFar`: de cerca agarra el `XRDirectInteractor` (PinchGrab)
  y el rayo se aparta. `RayReticle`: círculo siempre visible y suave (sustituye al láser).
  `InteractorModeSwitch`: manos ↔ mandos por movimiento. Modelos de mano negros ocultos.
- **Mandos:** `ControllerPoseFollower` coloca el anclaje con la pose real del mando.
- **Paneles:** `PanelDistanceKeeper` en `Medical_Menu_UI`: los paneles retroceden si la cabeza
  se acerca a menos de 80 cm (la mano real no los atraviesa).
- **Volumen 3D:** `Assets/Shaders/VolumeRender.shader`, `Volumen_3D` a escala real 1:1
  (0.5 × 0.873 × 0.5 m = 512×512×267 vóxeles, 0.977 mm de píxel, 3.27 mm entre cortes),
  96 pasos. El origen del rayo se pasa por interpolador (`cameraOS`) para que cada ojo use
  su posición (single-pass instanced).
- **Plano de corte:** `ClippingPlaneInput` con `lockToAnatomical`: solo axial/coronal/sagital
  respecto al volumen; agarrado se mueve libre y al soltarlo se endereza. "Girar 90°" cicla cortes.
- **Regla 3D:** `MeasureTool` (botón A). Cada medida es hija del órgano (`Medida_N`, contenedor
  con escala 1/lossyScale). Escalas: Hígado 1.11, Estómago 1.41, Páncreas 1.97, Vesícula 4.68
  (de volúmenes reales 1665/436/67/12 mL), Corazón 1.50 aproximado (≈, corazón adulto de 12 cm),
  Volumen 1.00 (se mide sobre el plano de corte). La ayuda "Pulsa A" solo hasta el primer punto.
- **Regla 2D:** `CTMeasure2D` en el visor DICOM; la medida vive solo en su corte.
- **Etiquetas:** `OrganTag` ("Hígado · 1665 mL") aparece al apuntar o agarrar el órgano.
- **Passthrough:** OVRManager + `[BuildingBlock] Passthrough` (Underlay) + cámaras con fondo
  transparente; `PassthroughGuard` lo reaplica y deja `[Passthrough]` en logcat. Funciona.
- **Rendimiento:** `QuestPerformance` activa foveated rendering alto y dinámico.
- **Diagnóstico de manos:** `HandDebugHud` sobre la mano izquierda, ENCENDIDO para pruebas.
  Apagar con `MedicalHandDebug.Off` (Step106) antes de la versión final.

## Lo que pidió el profesor (Meta Quest) y su estado al 18/10/2026

| Punto | Estado |
|---|---|
| Mediciones 2D en la imagen, solo en su corte | Hecho y **visto funcionando en el visor** |
| Mediciones 3D hijas del modelo | Hecho y **visto en el visor** (páncreas, hígado). Falta probar corazón y volumen |
| Prueba con APK, rendimiento del volume rendering | APK probado; hubo tirones en Modelo 3D → foveado + 96 pasos. Falta confirmar |
| Vídeo demostrativo con el encoder (NDI) | **Pendiente** (lo graba el equipo; la pantalla NDI decía "sin señal") |
| Deformación del volumen al mover el plano | Causa: origen del rayo por ojo (corregido). Orientación: diagonal escalona (cortes de 3.27 mm) → plano fijo a los 3 cortes anatómicos. Falta confirmar en el visor |

Último APK: `Builds/MedicalViewer_20261018_1903.apk` (commit `643b4f1c`).

## Pendientes

1. Probar el APK `_1903`: pinza a la primera con ambas manos, arrastrar barras, paneles a
   distancia, medir corazón y sobre el corte, plano enderezándose sin deformarse.
2. Si las manos siguen fallando: **migrar las manos al Interaction SDK de Meta** (Building
   Blocks "Ray Interaction" y "Hand Grab"; los paneles necesitan PointableCanvas y los órganos
   Grabbable). Hacerlo en una rama aparte; los mandos se quedan en XRI.
3. Vídeo con el encoder para el profe.
4. Decidir qué hacer con la `Sphere` de Sebaa_21.
5. Ideas para la presentación (sección "capacidades" del documento de avances):
   - **Eye tracking:** el Quest 3 no tiene seguimiento ocular; alternativa: selección con
     la mirada de la cabeza.
   - **Comandos de voz:** el Voice SDK de Meta está instalado; necesita una cuenta en Wit.ai.
   - **Medidor de FPS, batería y temperatura:** para dar datos de autonomía y fluidez.
   - **Estadísticas del NDI:** para comparar con el Kiloview N60.

## Trampas conocidas

- El texto de la escena guarda valores serializados: cambiar un valor por defecto en C# no
  cambia la escena; hay que pasarlo por un paso de editor (SerializedObject).
- Una asmdef de pruebas no puede referenciar Assembly-CSharp: usar reflexión (`Type.GetType`).
- En batchmode no corren Awake/Start: los renders del editor muestran todo encendido aunque
  la app arranque solo con el menú.
- El tamaño que imprime el script de build (1.7 GB) es erróneo; el APK real pesa ~300 MB.
