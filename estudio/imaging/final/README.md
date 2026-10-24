# ING340 – Introduction to Medical Imaging · Material para el EXAMEN FINAL (Ex2)

Recopilado el 24/10/2026.

## 1. Qué entra al final

**Sílabo 2026-2 (PAIDEIA):** nota = (34·Ta + 33·Ex1 + 33·Ex2)/100. El final (Ex2) vale **33 %**.
Ta = promedio de los quizzes semanales. El sílabo tiene 3 capítulos:

| Cap. | Tema | ¿Entra al Ex2? |
|---|---|---|
| 1 | Ultrasonido (onda, radiación, ecografía, Doppler) | Casi nada: fue el tema del parcial |
| 2 | Rayos X: producción, interacción, radiografía, **tomografía (Radon, ART, retroproyección filtrada)** | **Sí**, sobre todo la parte de CT |
| 3 | **Resonancia magnética**: Larmor, Bloch, FID, ecos, gradientes, codificación de frecuencia y fase, contraste T1/T2 | **Sí**, todo |

**En PAIDEIA** las clases van hasta la Semana 10, y el último tema subido es *Unit 2.3 – Projections and Tomography*
(en `paideia/`). Todavía falta dictar MRI (4 semanas según el sílabo).

**Finales anteriores (en `whatsapp_amadeus/`)**: todos siguen el mismo patrón:

| Examen | Preguntas |
|---|---|
| EX2 2022-2 (1/12/2022) | V/F (beamwidth −6 dB, arrays, bremsstrahlung) · CT algebraico de 4 píxeles (vector de proyecciones + **ART**) · magnetización Mz tras pulsos de 90°/135° · gradiente de **selección de corte** · K-means / ICP |
| EX2 2024-2 (2/12/2024) | **Transformada de Radon** (propiedades de desplazamiento y escala) · **retroproyección** sin filtro (g0, g90, g45) · **gradiente de selección de corte MRI** (pulso RF gaussiano) · **reconstrucción de imagen MRI / espacio-k** (diseño de secuencia) · opcional: interferómetro (OCT) |
| EX2 2025-1 (14/07/2025) | **Radiografía** (kVp/mA, HVL en dos medios) · **reconstrucción CT** a partir de proyecciones filtradas (rejilla de 3×3) · **secuencias MRI** → transformada de Fourier 2D / espacio-k · **contraste MRI** (B1 para un corte, inversion-recovery para anular la sustancia gris, TI) |

### Prioridades para estudiar
1. **CT**: Radon (definición y propiedades), proyecciones de figuras simples, retroproyección con y sin filtro (ramp), teorema de la rebanada de Fourier, ART paso a paso, escala Hounsfield.
2. **MRI, selección de corte**: Δz = Δω/(γ·G), ancho de banda del pulso RF (sinc ↔ rect, gaussiana), corte descentrado.
3. **MRI, espacio-k y secuencias**: k = (γ/2π)∫G dt, trayectorias (cartesiana, EPI, espiral/circular), diseño de secuencia (RF, Gss, Gpe, Gfe con amplitudes en mT/m y duraciones en ms).
4. **MRI, física y contraste**: Larmor, Bloch, FID, T1/T2, spin echo, inversion recovery (TI = T1·ln2 si TR ≫ T1), ponderaciones T1/T2/PD.
5. **Rayos X** (lo que ya viste en el parcial): HVL, μ, contraste, efecto de kVp/mA, rejilla antidifusora.
6. Opcional / poco frecuente: preguntas V/F de ultrasonido, interferometría/OCT.

Para practicar, usa `Compilado_Ejercicios IMI.pdf` (31 ejercicios tipo examen de CT y MRI) y `Compilado 2_Ejercicios IMI.pdf`.

## 2. Archivos

### `paideia/`
- `ING340_Unit2.3_Projections_and_Tomography.pdf`: diapositivas del último tema subido (Semana 10).

### `whatsapp_amadeus/` (chat con Amadeus, 21–23/10/2026)
| Archivo | Contenido |
|---|---|
| `EX2 22-2 (sin solución).pdf` | Final 2022-2, enunciado |
| `EX2 23-1, 23-2 y 24-2.pdf` | Finales 2023-1, 2023-2 y 2024-2 |
| `EX2 24-1 - Corregido Pregunta 2.pdf` | Final 2024-1, resuelto (P2 corregida) |
| `EX2 24-2.pdf` | Final 2024-2, enunciado |
| `EX2 25-1.pdf` | Final 2025-1 |
| `Enunciados_251.pdf` | Final 2025-1, enunciados |
| `25_2_resoo.pdf` | Final 2025-1 resuelto (examen con nota 20) |
| `WhatsApp Image 2026-07-14 at 5.42.18 PM.pdf` | Hoja de examen escaneada (4 págs.) |
| `Compilado_Ejercicios IMI.pdf` / `Compilado 2_Ejercicios IMI.pdf` | Compilados de ejercicios resueltos |
| `Ejercicios MRI.pdf` | Ejercicios de resonancia a mano |
| `Tomografia 1.0.pdf` / `Tomografia 2.0.pdf` | Apuntes y ejercicios de tomografía |
| `Apuntes .pdf` | Apuntes |
| `FINAL.pdf` | Resumen para el final (MRI: contraste T1/T2, gadolinio, etc.) |

No se encontraron quizzes de IMI en el chat. Los enunciados oficiales están en PAIDEIA → *Notas de Quizzes → Enunciados de los Quizzes*.

## 3. Mensajes sobre el final (chat con Amadeus)

El chat **no menciona la fecha del final**. Los finales anteriores fueron a inicios de diciembre (ciclo -2)
y a mediados de julio (ciclo -1), así que este debería caer hacia fines de noviembre o inicios de diciembre de 2026: confírmalo en el calendario de la facultad.
Próxima asesoría (PAIDEIA): **jueves 29/10/2026, 2:00 p. m.** (Zoom, "Asesoría ING340").

| Fecha | Quién | Mensaje |
|---|---|---|
| 16/10/2026 00:25 | Yo | quiero ver finales de imi |
| 18/10/2026 21:34 | Yo | de verdad estuve sobrepensando mucho en imi |
| 21/10/2026 19:04 | Yo | Finales bro |
| 21/10/2026 19:07 | Yo | Tienes tus fiables? Finales? |
| 21/10/2026 19:08 | Amadeus | Matías lo tiene · mi final · yo lo tengo |
| 21/10/2026 19:19 | Amadeus | (envía EX2 23-1, 23-2 y 24-2 · Enunciados 24.2 y 25-1 · 25_2_resoo · fotos de ejercicios resueltos) "ambos son misma idea" |
| 22/10/2026 16:56 | Amadeus | Se repite mucho de examen pasado |
| 22/10/2026 18:05 | Yo | Dice que no tiene tu examen |
| 23/10/2026 13:01 | Amadeus | Lo que importa es que tiene recolección de varios exámenes |
| 23/10/2026 13:32 | Yo | (sobre EX2 23-1, 23-2 y 24-2) no hay los enunciados |
| 23/10/2026 13:32 | Amadeus | Igual te mandé todos los pdf (envía compilados, EX2 22-2/24-1/24-2/25-1, MRI, Tomografía, Apuntes, FINAL) |
| 23/10/2026 14:13 | Yo | (sobre FINAL.pdf) Estaba revisando |

Conclusión del chat: **los finales repiten mucho los problemas de años anteriores**. Resuelve todos los EX2.
