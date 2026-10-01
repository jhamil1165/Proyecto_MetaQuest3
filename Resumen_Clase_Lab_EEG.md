# Resumen – Clase y Laboratorio de EEG

**Tema:** Electroencefalograma: ritmos, medición, adquisición y canales
**Docentes:** M.Sc. Lewis De La Cruz · M.Sc. Moisés Meza · Lic. J. Alonso Cáceres
**Laboratorio:** BITalino (r)evolution – *Home-Guide #3: Electroencephalography (EEG) – Exploring Brain Signals*
**Fecha del lab:** 25/09/2026

---

## Parte 1 – Clase teórica

### 1.1 Conceptos fisioanatómicos

**Organización del sistema nervioso**

| División | Componentes | Función |
|---|---|---|
| **SNC** | Encéfalo + médula espinal (cubiertos por las meninges) | Correlación e integración de la información |
| **SNP** | 12 pares de nervios craneales + 31 pares de nervios raquídeos (raíz anterior y posterior) + ganglios | Comunicación con el resto del cuerpo |
| ↳ Somático | — | Actividad **voluntaria**, músculo esquelético; vía de **una** neurona motora |
| ↳ Autónomo | — | Funciones **involuntarias** (músculo liso, cardíaco, glándulas); **dos** neuronas: pre y postganglionar |

**Neuronas y corteza**
- Las **neuronas multipolares** son las más abundantes en el cerebro y la médula (integran información).
- La **corteza cerebral tiene 6 capas** con distintos tipos neuronales:
  - **Piramidales** (excitatorias, Golgi tipo I).
  - **Interneuronas inhibitorias** (Golgi tipo II): axón corto, aspecto estrellado; modulan la sincronización y el ritmo de la actividad local (relacionadas con alfa y beta).
- Las **capas II/III y V** son las que más aportan al EEG: generan **potenciales postsinápticos sumatorios** detectables en el cuero cabelludo.
- Las piramidales están orientadas **perpendicularmente a la superficie cortical**, así que sus potenciales se suman y llegan al electrodo.
- El EEG registra sobre todo la actividad del **árbol dendrítico**, no los potenciales de acción axonales.

**Meninges**
- 3 capas: **duramadre, aracnoides, piamadre**.
- Funciones: protección mecánica, soporte de vasos sanguíneos, circulación del **LCR**.
- Espacio **subdural**: entre duramadre y aracnoides. Espacio **epidural**: entre hueso y duramadre.
- Meninges, cráneo y cuero cabelludo **atenúan y dispersan** la señal, así que el EEG capta una versión **amortiguada y difusa** de la actividad real. Por eso se mide en **µV**.

**Sinapsis**
- El **neurotransmisor determina** si la neurona postsináptica se excita o se inhibe.
- La suma de entradas excitadoras e inhibidoras debe llevar el potencial de membrana a unos **−50 mV (umbral)** para disparar.

**Estructuras del encéfalo**
- **Diencéfalo** (núcleo central) y **telencéfalo** (hemisferios, separados por la hoz cerebral).
- **Tálamo**: decide qué señales llegan a la conciencia y cuáles quedan para el aprendizaje y la memoria. Con la corteza forma los **circuitos tálamo-corticales**, clave en la generación de los ritmos EEG (alfa en vigilia, delta en sueño no REM).
- **Tronco encefálico** (bulbo, protuberancia, mesencéfalo): conecta la médula con los centros superiores y controla funciones involuntarias (p. ej. el ritmo respiratorio).

**Lóbulos cerebrales**

| Lóbulo | Funciones |
|---|---|
| **Frontal** | Habla, juicio, razonamiento, resolución de problemas, planificación, movimiento voluntario, emociones |
| **Parietal** | Tacto, orientación espacial, conciencia corporal, integración sensorial |
| **Temporal** | Audición, habla/lenguaje, memoria, reconocimiento auditivo |
| **Occipital** | Procesamiento visual (posturas, gestos, expresiones, color) |

### 1.2 Patologías frecuentes

*Fuente: Global Burden of Disease 1990–2016 (Feigin et al., Lancet Neurology 2019). Se midió con **DALYs** = años de vida perdidos + **YLDs** (años vividos con discapacidad).*
Las principales causas son el ACV, la migraña, el Alzheimer, la meningitis y las demencias. Entre los factores de riesgo están la presión sistólica alta, el bajo peso al nacer, la gestación corta y el riesgo metabólico.

| Patología | Puntos clave |
|---|---|
| **Alzheimer** | Trastorno progresivo por **placas de amiloide** y **ovillos de tau** en el hipocampo y zonas temporales. Es la causa más común de demencia. Primer síntoma: dificultad para recordar información reciente. **Incurable**: solo hay tratamiento paliativo. |
| **ACV** | Interrupción de la irrigación cerebral. **Isquémico** (trombótico/embólico) o **hemorrágico** (intracerebral/subaracnoideo). Secuelas: parálisis parcial, trastornos del habla, problemas cognitivos, depresión o ansiedad. |
| **Meningitis** | Inflamación de las meninges (bacteriana, viral o no infecciosa, p. ej. cáncer). Cerca del **70 %** de los casos bacterianos son en menores de 5 años. Prevención: vacunas, higiene de alimentos, lavado de manos. |
| **Epilepsia** | Convulsiones recurrentes. Criterio: **2 convulsiones no provocadas separadas por más de 24 h**. Síntomas: confusión, crisis de ausencia, rigidez, espasmos, pérdida de conciencia, ansiedad. |
| **Epilepsia fotosensible** | Convulsiones provocadas por patrones de luz y oscuridad contrastantes (sol, TV, cine, discotecas, videojuegos). 1 de cada 100 personas tiene epilepsia y al menos el 3 % de ellas es fotosensible. Prevención: evitar el estímulo, lentes (blue Z1), monitores LCD. |

### 1.3 Principios del EEG

- **Definición:** estudio que mide la actividad eléctrica del encéfalo con electrodos para apoyar el diagnóstico de trastornos neurológicos. Se registra en **µV** porque la calota ofrece resistencia.
- **Usos:** síndromes epilépticos y convulsivos, muerte cerebral, tumores, encefalopatías, trastornos del sueño, encefalitis, ACV, coma.

**Tipos de EEG**

| Tipo | Duración | Para qué |
|---|---|---|
| Rutina | 20–30 min | Incluye luces intermitentes e hiperventilación |
| Prolongado | > 1 h | Síntomas que dependen del tiempo (lapsos de memoria) |
| Ambulatorio | 1 día o más | Convulsiones y trastornos del sueño, ajuste de tratamiento |
| Video-EEG | 3–5 días | Video + EEG para capturar y clasificar crisis |

**Materiales clínicos:** equipo de al menos 20 canales, cámara y micrófono, monitor, electrodos de copa de oro, pasta conductora, pasta abrasiva, gasa y alcohol.

### 1.4 Sistema internacional 10-20 y canales

- Distribuye los electrodos al **10 % o 20 %** de la distancia **nasion** (puente de la nariz) → **inion** (protuberancia occipital) y de oreja a oreja. Así se mantiene la simetría aunque el tamaño del cráneo varíe.
- **Letra = región:** Fp (prefrontal), F (frontal), T (temporal), C (central), P (parietal), O (occipital).
- **Número = hemisferio:** **impar = izquierdo**, **par = derecho**, **z (cero) = línea media**.
- **Canal:** diferencia de potencial entre **dos electrodos**. El estándar clínico es de **16 a 32 canales**. Más canales permiten localizar mejor la actividad focal o difusa, pero implican más artefactos y más procesamiento.
- **Monopolar:** un electrodo por zona más una referencia. **Bipolar:** dos electrodos de medida (IN+ / IN−) más una referencia en zona ósea. Este es el montaje del sensor BITalino.

**Términos para investigar (tarea):** *bipolar montages, chain, common average reference montage, polarity rules, phase reversal*.

### 1.5 Ritmos cerebrales

Cuanto más **coordinadas** estén las neuronas, **menor frecuencia y mayor amplitud** tiene la señal. Los PPS excitatorios producen deflexión (+) y los inhibitorios deflexión (−).

| Ritmo | Frecuencia (clase) | Frecuencia (guía BITalino) | Amplitud | Estado asociado |
|---|---|---|---|---|
| **Delta δ** | 0,5–4 Hz | 0–4 Hz | Muy alta (100–200 µV) | Máxima coordinación. Sueño profundo (N3) |
| **Theta θ** | 4–8 Hz | 4–8 Hz | > 30 µV | Somnolencia, sueño ligero. Tareas de memoria (N-back), navegación en realidad virtual |
| **Alfa α** | 8–13 Hz | 8–12 Hz | 30–50 µV | Vigilia relajada, **ojos cerrados**, meditación. Se suprime al abrir los ojos o con actividad mental |
| **Beta β** | 13–30 Hz | 12–25 Hz | < 20 µV | Máxima descoordinación. Mente activa, concentración, control motor |
| **Gamma γ** | 30–100 Hz | > 25 Hz | Muy baja | Atención, percepción, memoria, conciencia. Integración rápida de información (estudios de microsacadas) |

> Los límites de las bandas cambian un poco según la fuente. Conviene citar cuál se usa en el informe.

### 1.6 Ciclos del sueño

- Cada ciclo dura **90–110 min** y se repite **4 a 6 veces** por noche.

| Fase | EEG | Características |
|---|---|---|
| **N1** | Baja alfa, aparece theta | Transición vigilia-sueño, sensación de estar "medio despierto" |
| **N2** | Theta, **husos de sueño** y **complejos K** | Sueño ligero, cerca del 50 % del total |
| **N3** | **Delta** de gran amplitud | Sueño profundo y regenerativo, consolida la memoria declarativa |
| **REM** | Mixta, baja amplitud y alta frecuencia (parecida a beta/gamma) | Sueños intensos, parálisis muscular. Se alarga en cada ciclo |

### 1.7 Usos clínicos y temas actuales

- **Apnea del sueño:** episodios de cese (apnea) o reducción (hipopnea) del flujo aéreo. Signos: ronquidos, somnolencia diurna, IMC alto, cuello ancho. Hay que diferenciar la **apnea obstructiva** (hay esfuerzo respiratorio pero la vía aérea colapsa) de la **apnea central** (falla el impulso del SNC y no hay esfuerzo).
- **Polisomnografía (gold standard):** EEG + flujo oronasal + bandas toracoabdominales + oximetría + EMG de extremidades. Tratamiento de primera línea: **CPAP**. Las pruebas en casa (HSAT) pueden subestimar la gravedad porque no registran EEG. Los *wearables* (Fitbit, WHOOP) no distinguen bien la vigilia de las fases del sueño.
- **Estimulación magnética transcraneal (EMT/TMS):** neuromodulación no invasiva con pulsos electromagnéticos (ley de Faraday) para depresión resistente, TOC o dolor crónico. El EEG mide la reactividad cortical a cada pulso. Está aprobada por la FDA, pero se debate su eficacia real: hay sesgo de publicación y un alto efecto placebo, sobre todo en jóvenes.
- **BCI (interfaz cerebro-computadora):** extrae *features* del EEG (p. ej. potencia alfa) para controlar dispositivos como prótesis, robots o computadoras. Sirve a pacientes con lesión medular, ACV de tronco o ELA.

### 1.8 Lecturas recomendadas post-clase

- Padmanaban et al. (2019). *Clinical advances in photosensitive epilepsy.* Brain Research.
- Amer & Belhaouari (2023). *EEG Signal Processing for Medical Diagnosis, Healthcare, and Monitoring: A Comprehensive Review.* IEEE Access. Idea clave: *"La calidad del diagnóstico basado en EEG depende de la calidad de la señal, la reducción del ruido y la selección adecuada de características."*
- Liu et al. (2023). *The Feature, Performance, and Prospect of Advanced Electrodes for EEG.* Biosensors. Compara electrodos húmedos, semisecos, secos de contacto, secos sin contacto y de micro-agujas (impedancia, calidad de señal, artefactos).

---

## Parte 2 – Laboratorio (BITalino Home-Guide #3)

### 2.1 Objetivos
- Hacer adquisiciones de EEG en tiempo real.
- Probar distintas posiciones de electrodos para comparar áreas cerebrales.
- Ver cómo cambia la señal con la actividad neuronal.
- Familiarizarse con las bandas de frecuencia de interés.

### 2.2 Materiales
- Software **OpenSignals (r)evolution**.
- **BITalino (r)evolution Core BT** con su batería.
- **Sensor EEG ensamblado** (bipolar: IN+, IN− y REF).
- Cable de 1 electrodo para la **referencia**.
- 3 **electrodos Ag/AgCl** autoadhesivos con gel: 2 para el sensor y 1 para la referencia.
- Dongle Bluetooth.
- *En nuestro lab también se usaron* **antifaz** (para tapar la vista) y **audífonos** (para aislar el ruido) en el sujeto.

### 2.3 Consideraciones del sensor
- La señal es la **diferencia amplificada** entre IN+ e IN−, con **ganancia ≈ 40 000** y un **filtro pasa-banda de 0,8–48 Hz**.
- Por la alta ganancia es **muy sensible a artefactos**: luz, movimiento, red eléctrica (50/60 Hz), **parpadeos, movimientos oculares, mandíbula y cuello**.
- Hay que **limpiar la piel con alcohol** y usar electrodos nuevos en cada repetición.
- El sujeto debe estar **relajado**. Si la tarea no es visual, conviene fijar la mirada en un punto (p. ej. una cruz) para evitar movimientos oculares.
- Se recomienda anotar o grabar en video los movimientos para identificar artefactos después.

### 2.4 Montaje
- **IN+ / IN−** sobre la frente en **Fp2** (encima del ojo derecho) o **Fp1** (ojo izquierdo).
- **REF** sobre **hueso detrás de la oreja** (mastoides).
- Sensor conectado a un canal analógico del BITalino. En nuestro registro OpenSignals mostraba el **canal A4 – EEG a 1000 Hz**.
- **Frecuencia de muestreo:** según **Nyquist-Shannon**, fs ≥ 2 × fmáx. Con un filtro hasta 48 Hz bastaría con ~100 Hz, así que 1000 Hz cumple con margen.

### 2.5 Protocolo de adquisición
1. Conectar y probar el BITalino Core BT.
2. Conectar el sensor EEG y el cable de referencia a canales analógicos.
3. Colocar los electrodos con gel en el sensor y en la referencia.
4. Poner el sensor en **Fp2** y la referencia detrás de la oreja.
5. Empezar a grabar en OpenSignals.
6. **Línea base de 30 s**: sin movimiento, respiración normal, ojos cerrados.
7. **5 ciclos de ojos abiertos / ojos cerrados**, 5 s cada fase.
8. **Otra línea base de 30 s.**
9. **12 cálculos mentales** que el compañero lee en voz alta, con la mirada fija.
10. Detener y guardar.
11. **Repetir en Fp2, Fp1 y O2 (occipital).**

### 2.6 Lo que observamos en el lab

> Lo que sigue sale de las fotos y videos del laboratorio. Complétalo con tus propios datos y notas.

- **Montaje usado:** sensor sobre la frente (Fp1/Fp2) con referencia detrás de la oreja. Los sujetos llevaban **antifaz y audífonos** para reducir estímulos visuales y auditivos.
- **Escala:** la señal oscilaba en unos **±20 µV**, lo esperable para un EEG de superficie.
- **Reposo:** se vio una señal de fondo continua, irregular y de baja amplitud.
- **Artefactos:** aparecieron **deflexiones grandes y lentas** (picos negativos marcados) que se salen de la escala del fondo. Coinciden con **parpadeos o movimientos oculares**, porque Fp1/Fp2 están justo encima de los ojos, y con **movimientos de cabeza o mandíbula** (por ejemplo, al hablar o al acomodarse los audífonos o el antifaz). En los videos se ve que estos picos aparecen cuando el sujeto se mueve o habla.
- **Conclusión práctica:** en los electrodos frontales los **artefactos oculares (EOG)** dominan la señal cruda. Para analizar ritmos (alfa, beta) hay que **descartar esos segmentos** o filtrarlos, y conviene comparar con **O2**, donde el alfa con ojos cerrados se ve mejor.

### 2.7 Qué se esperaba ver
- **Ojos cerrados:** aumento de **alfa (8–12 Hz)**, con oscilaciones regulares más visibles en zonas **occipitales** (O2) que en las frontales.
- **Ojos abiertos:** el alfa se suprime (desincronización) y aparecen artefactos de parpadeo en Fp1/Fp2.
- **Cálculo mental:** aumento de **beta** y bajada de alfa, más marcado en la zona **frontal**.
- **Fp1 vs Fp2:** señales parecidas. Las diferencias pueden venir de la lateralización de la tarea, de la colocación o de artefactos, más que de un ritmo distinto.

---

## Parte 3 – Quiz de la guía (respuestas guía)

**Q1. ¿Cuáles son las frecuencias significativas del EEG? ¿Son iguales en todas las áreas?**
Delta (0–4 Hz), theta (4–8), alfa (8–12/13), beta (12/13–25/30) y gamma (> 25/30 Hz). No predominan igual en todas las áreas: el alfa domina en las zonas **occipitales/posteriores** con ojos cerrados, el beta en las **frontales y centrales** con actividad mental o motora, y el theta frontal se asocia a tareas de memoria.

**Q2. ¿Qué filtro es esencial y por qué?**
Un **pasa-banda** (en BITalino, 0,8–48 Hz). El pasa-altos quita la deriva de línea base y los artefactos lentos. El pasa-bajos quita el EMG de alta frecuencia y el ruido. Además suele hacer falta un **notch de 50/60 Hz** contra la red eléctrica. Se necesita porque la señal es de µV y la amplificación (×40 000) también amplifica todo el ruido.

**Q3. ¿Puedes influir el EEG con tus pensamientos?**
Sí. **Cerrar los ojos y relajarse** sube la potencia **alfa**. **Concentrarse o calcular** sube **beta** y baja alfa. *(Indica si lo pudiste ver en tu registro.)*

**Q4. Captura de un segmento relevante.**
*(Insertar captura de OpenSignals: idealmente la transición ojos abiertos → cerrados o un segmento de cálculo mental.)*

**Q5. ¿Hay diferencia entre Fp1 y Fp2?**
Deberían ser parecidas. Las diferencias suelen venir de artefactos oculares asimétricos, de la colocación o la impedancia, o de una ligera lateralización (el theta tiende a ser más fuerte en el lado derecho).

**Q6. ¿Qué frecuencias cambian con las tareas? ¿Se ve en la señal cruda?**
Ojos cerrados: alfa sube. Cálculo mental: beta sube y alfa baja. En la señal **cruda** de la frente cuesta verlo porque dominan los **artefactos de parpadeo** (picos grandes y lentos). Se aprecia mejor con un análisis espectral (FFT o potencia por banda) o en **O2**.

**Q7. ¿La amplitud del EEG equivale al nivel de concentración?**
**No.** Más concentración implica **más frecuencia (beta) y menos amplitud**, porque las neuronas trabajan de forma menos sincronizada. Las amplitudes grandes corresponden a actividad **sincronizada** (alfa en reposo, delta en sueño) o a artefactos.

---

## Recursos
- OpenSignals: <https://bitalino.com/en/software>
- **PsychoPy:** para presentar estímulos en experimentos propios.
- **PhysioNet:** bases de datos públicas de EEG.

---
*Pendiente: agregar los archivos adicionales (datos, capturas, notas) cuando estén disponibles.*
