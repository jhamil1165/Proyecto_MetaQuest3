// Línea de tiempo del video de apertura.
//
// Todo el video es una función del tiempo: renderAt(t) deja la escena exactamente como debe
// verse en el segundo t. La vista previa la llama en tiempo real; el exportador la llama
// fotograma a fotograma y captura la pantalla.

import { createEngine, W, H } from './gl.js';

// Para cambiar textos sin tocar la animación.
const CONFIG = {
  name: 'Medical Viewer',
  subtitle: 'Imágenes médicas en realidad mixta',
  team: 'Ingeniería Biomédica · PUCP',
  closing: 'Exploremos juntos el siguiente paso.',
};
export const DURATION = 60;

const capture = new URLSearchParams(location.search).has('capture');
if (capture) document.body.classList.add('capture');

// ---------- utilidades de animación ----------
const clamp01 = (x) => Math.min(1, Math.max(0, x));
const prog = (t, a, b) => clamp01((t - a) / (b - a));
const lerp = (a, b, x) => a + (b - a) * x;
const easeOut = (x) => 1 - Math.pow(1 - x, 3);
const easeInOut = (x) => (x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2);
const DEG = Math.PI / 180;

// Pista de valores: [[t, v], ...]. Entre dos claves se interpola con suavizado.
function track(t, keys) {
  if (t <= keys[0][0]) return keys[0][1];
  for (let i = 0; i < keys.length - 1; i++) {
    const [t0, v0] = keys[i], [t1, v1] = keys[i + 1];
    if (t <= t1) return lerp(v0, v1, easeInOut(prog(t, t0, t1)));
  }
  return keys[keys.length - 1][1];
}
// Envolvente de aparición/desaparición.
const env = (t, a, b, din = 0.7, dout = 0.5) => easeOut(prog(t, a, a + din)) * (1 - easeInOut(prog(t, b, b + dout)));

// ---------- textos ----------
// Cada bloque: cuándo entra, cuándo sale, dónde va y sus líneas. <b> = palabra en azul.
// Los tiempos siguen a la locución (assets/audio/locucion.mp3, que entra en VOICE_OFFSET):
// cada línea aparece cuando la voz la dice. <b> = palabra en azul.
const BLOCKS = [
  { at: 1.6, out: 6.6, pos: 'left', lines: [['h1', 'Cientos de imágenes.'], ['h1 dim', 'De una en una.']], gaps: [0, 3.5] },
  { at: 7.5, out: 11.3, pos: 'left', lines: [['q', '¿Y si pudieras'], ['q', 'explorarlas'], ['q', '<b>frente a ti?</b>']], gaps: [0, 0.85, 2.6] },
  { at: 12.5, out: 16.9, pos: 'left', lines: [['h1', 'Cada corte, en su lugar…'], ['h1', '…y aparece un <b>cuerpo</b>.']], gaps: [0, 2.45] },
  { at: 17.55, out: 23.8, pos: 'left', lines: [['kicker', 'Realidad mixta · Meta Quest 3'], ['h1', 'La anatomía, aquí,'], ['h1', '<b>en tu espacio.</b>'], ['sub', 'Sin dejar de ver la sala.']], gaps: [0, 1.6, 3.4, 4.9] },
  { at: 24.6, out: 27.7, pos: 'left', lines: [['kicker', '01 · Órganos'], ['h2', 'Activa solo los órganos'], ['h2', 'que necesitas ver.']], gaps: [0, 0.1, 1.3] },
  { at: 28.0, out: 31.3, pos: 'left', lines: [['kicker', '01 · Órganos'], ['h2', 'Tómalo con la mano…'], ['h2', '…y <b>gíralo</b>.']], gaps: [0, 0.1, 2.2] },
  { at: 31.6, out: 37.0, pos: 'left', lines: [['kicker', '02 · Plano de corte'], ['h2', 'Desliza el plano…'], ['h2', 'y la tomografía'], ['h2', '<b>te sigue.</b>']], gaps: [0, 0.15, 2.6, 3.7] },
  { at: 37.5, out: 40.9, pos: 'tri', lines: [['kicker', '03 · Tres planos'], ['h2', 'Tres planos,'], ['h2', '<b>un mismo punto.</b>']], gaps: [0, 0.1, 2.35] },
  { at: 41.4, out: 44.7, pos: 'top', lines: [['kicker', 'Captura real del visor'], ['h2', 'Ya funciona en Meta Quest 3.'], ['sub', 'Prototipo en desarrollo.']], gaps: [0, 0.95, 1.5] },
  { at: 45.7, out: 52.0, pos: 'possible', lines: [['kicker', 'Lo que sigue'], ['h2', 'Hoy es un prototipo.'], ['h2', '<b>¿Hasta dónde puede llegar?</b>']], gaps: [0, 0.15, 2.0] },
  { at: 50.6, out: 52.0, pos: 'footnote', lines: [['sub', 'Posibilidades por explorar y validar con ustedes.']], gaps: [0] },
  { at: 52.3, out: 99, pos: 'left', lines: [['kicker', CONFIG.team], ['name', CONFIG.name], ['sub', CONFIG.subtitle], ['closing', CONFIG.closing]], gaps: [0, 0.15, 0.4, 0.75] },
];

// La locución y el montaje: la animación de cada escena se estira o encoge (sin tocar la voz)
// para que sus momentos clave caigan cuando la voz los nombra. [tiempo de animación, tiempo del video]
export const VOICE_OFFSET = 0.5;
const WARP = [
  [0, 0], [0.7, 0.55], [3.35, 6.7], [3.75, 7.5], [6.75, 11.4], [7.7, 12.55], [11.4, 16.4],
  [12.8, 17.6], [16.6, 23.9], [17.4, 24.7], [18.3, 25.85], [20.1, 27.2], [21.1, 28.1],
  [25.6, 31.75], [26.4, 32.4], [31.8, 36.4], [33.4, 37.55], [37.6, 40.9], [38.3, 41.5],
  [41.6, 44.6], [42.6, 45.8], [51.4, 52.2], [52.3, 52.75], [55.4, 56.4], [60, 60],
];
function warp(t) {
  if (t <= 0) return 0;
  for (let i = 0; i < WARP.length - 1; i++) {
    const [a0, b0] = WARP[i], [a1, b1] = WARP[i + 1];
    if (t <= b1) return a0 + (a1 - a0) * (t - b0) / (b1 - b0);
  }
  return 60;
}

const POS = {
  left: 'left:150px; top:540px; transform:translateY(-50%);',
  tri: 'left:1150px; top:120px;',
  top: 'left:180px; top:92px;',
  possible: 'left:150px; top:200px;',
  footnote: 'left:150px; top:930px;',
};

const textRoot = document.getElementById('text');
for (const b of BLOCKS) {
  const el = document.createElement('div');
  el.className = 'block';
  el.style.cssText = POS[b.pos];
  b.lineEls = b.lines.map(([cls, html]) => {
    const l = document.createElement('div');
    l.className = 'line ' + cls;
    l.innerHTML = html.replace(/<b>/g, '<span class="acc">').replace(/<\/b>/g, '</span>');
    el.appendChild(l);
    return l;
  });
  textRoot.appendChild(el);
}

function updateText(t) {
  for (const b of BLOCKS) {
    b.lineEls.forEach((el, i) => {
      const start = b.at + (b.gaps ? b.gaps[i] : i * b.gap);
      const vin = easeOut(prog(t, start, start + 0.75));
      const vout = easeInOut(prog(t, b.out, b.out + 0.55));
      el.style.opacity = (vin * (1 - vout)).toFixed(4);
      el.style.transform = `translateY(${((1 - vin) * 30 - vout * 16).toFixed(2)}px)`;
    });
  }
}

// Etiqueta discreta que dice qué es real y qué es ilustración.
const TAGS = [
  [0.4, 11.3, 'Cortes reales de la tomografía del proyecto', ''],
  [11.8, 27.8, 'Ilustración en 3D con los datos del proyecto', ''],
  [28.0, 31.5, 'Representación de la interacción · órganos segmentados del proyecto', ''],
  [31.8, 40.9, 'Ilustración en 3D con los datos del proyecto', ''],
  [41.4, 44.7, 'Captura real del prototipo · Meta Quest 3', 'real'],
  [45.6, 52.0, 'Ilustración en 3D con los datos del proyecto', ''],
];
const tagEl = document.getElementById('tag');
function updateTag(t) {
  let shown = null;
  for (const g of TAGS) if (t >= g[0] - 0.4 && t <= g[1] + 0.4) shown = g;
  if (!shown) { tagEl.style.opacity = 0; return; }
  tagEl.textContent = shown[2];
  tagEl.className = shown[3];
  tagEl.style.opacity = (0.85 * env(t, shown[0], shown[1], 0.4, 0.3)).toFixed(3);
}

// ---------- imágenes 2D ----------
const NSLICES = 267;
const pad = (n, k) => String(n).padStart(k, '0');
const bitmaps = { axial: [], mask: [], coronal: [], sagital: [] };
async function loadBitmap(url) {
  const r = await fetch(url);
  if (!r.ok) return null;
  return createImageBitmap(await r.blob());
}
async function loadImages() {
  const jobs = [];
  for (let z = 0; z < NSLICES; z++) {
    jobs.push(loadBitmap(`../assets/slices/axial_${pad(z, 4)}.jpg`).then((b) => (bitmaps.axial[z] = b)));
    if (z >= 90 && z <= 150) jobs.push(loadBitmap(`../assets/slices/mask_${pad(z, 4)}.png`).then((b) => (bitmaps.mask[z] = b)));
  }
  for (let i = 0; i < 80; i++) {
    jobs.push(loadBitmap(`../assets/slices/coronal_${pad(i, 3)}.jpg`).then((b) => (bitmaps.coronal[i] = b)));
    jobs.push(loadBitmap(`../assets/slices/sagital_${pad(i, 3)}.jpg`).then((b) => (bitmaps.sagital[i] = b)));
  }
  await Promise.all(jobs);
}

function drawSlice(canvas, z, maskAlpha = 0, frame = null) {
  const c = canvas.getContext('2d');
  c.fillStyle = '#000';
  c.fillRect(0, 0, canvas.width, canvas.height);
  const img = bitmaps.axial[z];
  if (img) c.drawImage(img, 0, 0, canvas.width, canvas.height);
  if (maskAlpha > 0 && bitmaps.mask[z]) {
    c.globalAlpha = maskAlpha;
    c.drawImage(bitmaps.mask[z], 0, 0, canvas.width, canvas.height);
    c.globalAlpha = 1;
  }
  if (frame) {
    c.strokeStyle = frame; c.lineWidth = 6;
    c.strokeRect(3, 3, canvas.width - 6, canvas.height - 6);
  }
}

function crosshair(c, x, y, w, h) {
  c.strokeStyle = 'rgba(76,141,255,0.95)';
  c.lineWidth = 2;
  c.beginPath(); c.moveTo(x, 0); c.lineTo(x, h); c.moveTo(0, y); c.lineTo(w, y); c.stroke();
  c.fillStyle = '#4C8DFF';
  c.beginPath(); c.arc(x, y, 5, 0, Math.PI * 2); c.fill();
}

// ---------- montaje ----------
const $ = (id) => document.getElementById(id);
let E; // motor 3D

async function init() {
  const stage = $('stage');
  if (!capture) {
    const fit = () => {
      const s = Math.min(innerWidth / W, (innerHeight - 52) / H);
      stage.style.transform = `scale(${s})`;
      stage.style.width = W + 'px';
      $('viewport').style.cssText = `position:absolute; left:${(innerWidth - W * s) / 2}px; top:${(innerHeight - 52 - H * s) / 2}px; width:${W * s}px; height:${H * s}px; display:block;`;
    };
    addEventListener('resize', fit); fit();
  }
  E = await createEngine($('gl'), { volumeScale: capture ? 0.75 : 0.5 });
  E.renderer.localClippingEnabled = true;
  clipPlane = new E.THREE.Plane(new E.THREE.Vector3(0, -1, 0), 10);
  for (const o of Object.values(E.organs)) o.mat.clippingPlanes = [clipPlane];
  abdomenY = Object.values(E.organs).reduce((s, o) => s + o.pivot.userData.home.y, 0) / 4;

  // posición exacta del lienzo del TAC dentro del escenario
  {
    const card = $('ctCard'), cv = $('ctCanvas');
    card.style.transform = 'none';
    const sr = stage.getBoundingClientRect(), cr = cv.getBoundingClientRect();
    const k = sr.width / W;
    CARD = { x: (cr.left - sr.left) / k, y: (cr.top - sr.top) / k, s: cr.width / k };
  }
  await loadImages();
  // imagen del corte que sale de la pantalla
  await new Promise((res) => {
    const img = new Image();
    img.onload = () => { E.looseTex.image = img; E.looseTex.needsUpdate = true; res(); };
    img.src = `../assets/slices/axial_${pad(HANDOFF, 4)}.jpg`;
  });
  $('capA').src = '../assets/captures/real_plano.jpg';
  $('capB').src = '../assets/captures/real_volumen.jpg';
  await Promise.all([$('capA').decode().catch(() => {}), $('capB').decode().catch(() => {}), document.fonts.ready]);
  $('loading').remove();
}

let clipPlane, abdomenY = 0;
const STACK = 56;
const HANDOFF_I = 24;                                     // corte de la pila que viene de la pantalla
const HANDOFF = Math.round(((HANDOFF_I + 0.5) / STACK) * NSLICES - 0.5);
const sliceY = (z) => E.half.y - ((z + 0.5) / NSLICES) * E.size.y;

// Cámara: [t, azimut, elevación, distancia, altura del objetivo, x en pantalla]
const CAM = [
  [0, 0, 4, 2.7, 0, 1150],
  [7.0, 0, 4, 2.7, 0, 1150],
  [9.4, -12, 36, 2.6, 0.06, 1150],
  [10.8, -20, 24, 2.55, 0.02, 1150],
  [12.6, -34, 16, 2.45, 0, 1150],
  [16.8, -52, 13, 2.4, 0, 1150],
  [18.2, -26, 14, 1.5, 'abd', 1080],
  [21.0, -12, 12, 1.42, 'abd', 1080],
  [24.6, 4, 12, 1.42, 'abd', 1080],
  [26.2, -22, 30, 2.85, 0.02, 985],
  [32.6, -40, 30, 2.8, 0.02, 985],
  [34.0, -36, 22, 2.45, 'abd', 600],
  [37.8, -50, 23, 2.4, 'abd', 600],
  [38.4, -30, 12, 2.75, 0, 1250],
  [42.0, -30, 12, 2.75, 0, 1250],
  [51.6, 8, 12, 2.7, 0, 1270],
  [55.4, 16, 12, 2.85, 0, 1430],
];
function camAt(t) {
  const col = (k) => CAM.map((r) => [r[0], r[k] === 'abd' ? abdomenY : r[k]]);
  return [1, 2, 3, 4, 5].map((k) => track(t, col(k)));
}

function placeCamera(t) {
  const [az, el, dist, ty, cx] = camAt(t);
  const a = az * DEG, e = el * DEG;
  E.camera.position.set(Math.sin(a) * Math.cos(e) * dist, ty + Math.sin(e) * dist, Math.cos(a) * Math.cos(e) * dist);
  E.camera.lookAt(0, ty, 0);
  E.camera.setViewOffset(W, H, -(cx - W / 2), 0, W, H);
  E.camera.updateMatrixWorld(true);
}

// Pose del corte suelto que coincide, píxel a píxel, con la imagen de la pantalla del inicio.
let CARD = { x: 1080, y: 283, s: 600 }; // esquina sup. izq. del lienzo; se mide al iniciar
function cardPose() {
  const T = E.THREE;
  const D = 1.2;
  const pt = (sx, sy) => new T.Vector3((sx / W) * 2 - 1, -(sy / H) * 2 + 1, 0.5).unproject(E.camera)
    .sub(E.camera.position).normalize();
  const c = pt(CARD.x + CARD.s / 2, CARD.y + CARD.s / 2);
  const l = pt(CARD.x, CARD.y + CARD.s / 2), r = pt(CARD.x + CARD.s, CARD.y + CARD.s / 2);
  const fwd = new T.Vector3(0, 0, -1).applyQuaternion(E.camera.quaternion);
  const dist = (v) => D / v.dot(fwd);
  const pc = E.camera.position.clone().addScaledVector(c, dist(c));
  const pl = E.camera.position.clone().addScaledVector(l, dist(l));
  const pr = E.camera.position.clone().addScaledVector(r, dist(r));
  return { pos: pc, quat: E.camera.quaternion.clone(), scale: pl.distanceTo(pr) };
}

let ctCardLayout = null;
function renderAt(tv) {
  // tv: tiempo del video (textos, etiquetas). t: tiempo de la animación, ajustado a la voz.
  const t = warp(tv);
  const T = E.THREE;
  placeCamera(t);

  // ===== 1 · pantalla con cortes (0 – 7 s) =====
  const card = $('ctCard');
  const cardIn = easeOut(prog(t, 0.2, 1.0));
  const cardOut = prog(t, 6.75, 7.05);
  card.style.opacity = (cardIn * (1 - cardOut)).toFixed(3);
  card.style.transform = `translateY(${(1 - cardIn) * 24}px) scale(${lerp(0.97, 1, cardIn)})`;
  const z = Math.round(track(t, [[0.5, 22], [3.9, HANDOFF]]));
  if (t < 7.2) {
    drawSlice($('ctCanvas'), z);
    $('ctCount').textContent = `Corte ${pad(z + 1, 3)} / ${NSLICES}`;
  }

  // ===== corte suelto que se convierte en volumen (6.8 – 9.8 s) =====
  const lt = easeInOut(prog(t, 7.0, 9.6));
  const looseVis = t > 6.75 && t < 11.0;
  E.loose.visible = looseVis;
  if (looseVis) {
    const from = cardPose();
    E.body.rotation.set(0, 0, 0);
    E.body.updateMatrixWorld(true);
    const toPos = new T.Vector3(0, sliceY(HANDOFF), 0).applyMatrix4(E.body.matrixWorld);
    const toQuat = new T.Quaternion().setFromEuler(new T.Euler(Math.PI / 2, 0, 0));
    E.loose.position.lerpVectors(from.pos, toPos, lt);
    E.loose.quaternion.slerpQuaternions(from.quat, toQuat, lt);
    // el lienzo es cuadrado (500 x 500 mm de campo): x = ancho, y = fondo
    E.loose.scale.set(lerp(from.scale, E.size.x, lt), lerp(from.scale, E.size.z, lt), 1);
    E.loose.material.opacity = 1 - easeInOut(prog(t, 9.6, 10.8));
  }
  E.looseKey.value = easeInOut(prog(t, 6.9, 7.8));
  E.looseR.value = (HANDOFF + 0.5) / NSLICES;
  E.looseOpacity.value = E.loose.material.opacity;

  // ===== pila de cortes → volumen (8.6 – 12.8 s) =====
  const stackFade = 1 - easeInOut(prog(t, 11.6, 12.9));
  E.stack.forEach((m, i) => {
    const d = Math.abs(i - HANDOFF_I);
    const st = 8.5 + (d / 32) * 2.4;
    const k = easeOut(prog(t, st, st + 0.6));
    const vis = k * stackFade;
    m.visible = vis > 0.002;
    m.material.uniforms.uOpacity.value = vis * (i === HANDOFF_I ? 1 - E.loose.material.opacity * 0.9 : 1);
    m.position.y = lerp(sliceY(HANDOFF), m.userData.homeY, easeOut(prog(t, st, st + 0.9)));
  });

  // ===== volumen =====
  const vol = E.volMat.uniforms;
  vol.uOpacity.value = track(t, [[11.4, 0], [12.8, 1], [20.8, 1], [21.6, 0.16], [24.8, 0.16], [25.8, 1], [33.0, 1], [34.0, 0.32], [37.8, 0.32], [38.4, 0], [41.8, 0], [42.8, 1]]);
  vol.uDensity.value = track(t, [[17.4, 1], [18.6, 0.55], [25.0, 0.55], [25.8, 0.7], [41.8, 0.7], [42.6, 0.5]]);
  vol.uBone.value = 0;

  // ===== órganos =====
  const toggles = { higado: 18.3, estomago: 18.9, pancreas: 19.5, vesicula: 20.1 };
  let onCount = 0;
  for (const [name, o] of Object.entries(E.organs)) {
    const ton = toggles[name];
    if (t >= ton) onCount++;
    let a = easeOut(prog(t, ton, ton + 0.55));
    a *= 1 - easeInOut(prog(t, 33.0, 33.8));          // fuera en "tres planos"
    a = t > 38 ? easeOut(prog(t, 42.2, 43.2)) : a;     // vuelven para el conjunto final
    o.mat.opacity = a;
    o.mesh.visible = a > 0.003;
    o.mat.transparent = a < 0.999;
    o.mat.depthWrite = a > 0.5;
    const s = lerp(0.9, 1, easeOut(prog(t, ton, ton + 0.6)));
    o.pivot.scale.setScalar(t > 38 ? 1 : s);
    o.pivot.position.copy(o.pivot.userData.home);
    o.pivot.rotation.set(0, 0, 0);
    o.mat.emissive.set(0x000000);
  }

  // agarrar y girar el hígado (21.2 – 25.2 s)
  const liver = E.organs.higado;
  const grab = easeInOut(prog(t, 21.5, 22.3)) * (1 - easeInOut(prog(t, 24.3, 25.2)));
  if (grab > 0) {
    // hacia la cámara, en el espacio del cuerpo
    const toCam = E.camera.position.clone().sub(liver.pivot.userData.home).normalize();
    liver.pivot.position.copy(liver.pivot.userData.home).addScaledVector(toCam, 0.16 * grab);
    const spin = easeInOut(prog(t, 22.0, 24.4));
    liver.pivot.rotation.set(spin * 22 * DEG * grab, spin * 160 * DEG * grab, 0);
    liver.mat.emissive.set('#4C8DFF');
    liver.mat.emissiveIntensity = 0.16 * grab;
    for (const n of ['estomago', 'pancreas', 'vesicula']) E.organs[n].mat.opacity *= lerp(1, 0.45, grab);
  }

  // ===== planos de corte =====
  const P = E.planes;
  const axVis = env(t, 25.6, 37.6, 0.7, 0.5) + (t > 38 ? env(t, 42.4, 99, 0.9) : 0);
  const axZ = t > 38 ? 118 : track(t, [[26.4, 58], [31.8, 126], [34.6, 126], [37.4, 112]]);
  const corRow = track(t, [[34.6, 238], [37.4, 262]]);  // fila (0..511, 0 = frente)
  const sagCol = track(t, [[34.6, 300], [37.4, 252]]);  // columna (0..511, 0 = derecha del paciente)
  P.axial.group.position.y = sliceY(axZ);
  P.coronal.group.position.z = E.half.z - ((corRow + 0.5) / 512) * E.size.z;
  P.sagital.group.position.x = -E.half.x + ((sagCol + 0.5) / 512) * E.size.x;
  const triVis = env(t, 33.2, 37.6, 0.7, 0.5);
  const triFill = lerp(1, 0.35, triVis);
  setPlane(P.axial, axVis, triFill);
  setPlane(P.coronal, triVis, 0.35);
  setPlane(P.sagital, triVis, 0.35);

  // el plano axial corta el volumen y los órganos, salvo en "tres planos"
  const clipOn = t >= 25.6 && t < 33.4;
  const clipY = clipOn ? sliceY(axZ) : 10;
  vol.uClipD.value = clipY;
  clipPlane.constant = clipY;
  E.body.updateMatrixWorld(true);

  // ===== paneles 2D =====
  // sistemas
  const sys = $('systems');
  const sysVis = env(t, 17.4, 20.9, 0.7, 0.5);
  sys.style.opacity = sysVis.toFixed(3);
  sys.style.transform = `translateX(${(1 - easeOut(prog(t, 17.4, 18.2))) * 40}px)`;
  $('sysBadge').textContent = onCount;
  sys.querySelectorAll('.row').forEach((row) => {
    const name = row.dataset.organ;
    const on = name ? easeInOut(prog(t, toggles[name] - 0.15, toggles[name] + 0.15)) : 0;
    row.querySelector('.pill').style.background = on > 0 ? `rgba(76,141,255,${0.18 + 0.82 * on})` : 'rgba(255,255,255,0.18)';
    row.querySelector('.knob').style.transform = `translateX(${on * 26}px)`;
  });

  // corte axial que sigue al plano
  const ax = $('axCard');
  const axCardVis = env(t, 26.0, 32.8, 0.7, 0.5);
  ax.style.opacity = axCardVis.toFixed(3);
  ax.style.transform = `translateX(${(1 - easeOut(prog(t, 26.0, 26.8))) * 40}px)`;
  if (axCardVis > 0) {
    const zi = Math.round(axZ);
    drawSlice($('axCanvas'), zi, 0.55, '#4C8DFF');
    $('axCount').textContent = `Corte ${pad(zi + 1, 3)} / ${NSLICES}`;
  }

  // tres planos
  const tri = $('triCard');
  tri.style.opacity = triVis.toFixed(3);
  tri.style.transform = `translateY(${(1 - easeOut(prog(t, 33.2, 34.0))) * 30}px)`;
  if (triVis > 0) {
    const zi = Math.round(axZ);
    const ca = $('triA'), cc = $('triC'), cs = $('triS');
    drawSlice(ca, zi, 0.45);
    crosshair(ca.getContext('2d'), (sagCol / 512) * 300, (corRow / 512) * 300, 300, 300);
    const ci = Math.max(0, Math.min(79, Math.round((corRow - 96) / 4)));
    const si = Math.max(0, Math.min(79, Math.round((sagCol - 96) / 4)));
    for (const [cv, img, hx] of [[cc, bitmaps.coronal[ci], sagCol], [cs, bitmaps.sagital[si], corRow]]) {
      const c = cv.getContext('2d');
      c.fillStyle = '#000'; c.fillRect(0, 0, cv.width, cv.height);
      if (img) c.drawImage(img, 0, 0, cv.width, cv.height);
      crosshair(c, (hx / 512) * cv.width, ((zi + 0.5) / NSLICES) * cv.height, cv.width, cv.height);
    }
    $('triMeta').textContent = `${Math.round(sagCol)} · ${Math.round(corRow)} · ${zi + 1}`;
  }

  // captura real
  $('dim').style.opacity = (env(t, 37.8, 41.7, 0.6, 0.6)).toFixed(3);
  const capVis = env(t, 38.2, 41.5, 0.7, 0.5);
  const cap = $('capture');
  cap.style.opacity = capVis.toFixed(3);
  cap.style.transform = `translateY(${(1 - easeOut(prog(t, 38.2, 39.0))) * 30}px)`;
  const swap = easeInOut(prog(t, 39.9, 40.4));
  $('capA').style.opacity = (1 - swap).toFixed(3);
  $('capB').style.opacity = swap.toFixed(3);
  $('capA').style.transform = `scale(${lerp(1.0, 1.06, prog(t, 38.2, 40.4))})`;
  $('capB').style.transform = `scale(${lerp(1.02, 1.08, prog(t, 39.9, 41.8))})`;

  // posibilidades
  document.querySelectorAll('.chip').forEach((c, i) => {
    const a = 48.2 + i * 0.6;
    const v = env(tv, a, 52.0, 0.7, 0.5);
    c.style.opacity = v.toFixed(3);
    c.style.transform = `translateX(${(1 - easeOut(prog(tv, a, a + 0.7))) * -24}px)`;
  });

  // gesto de pinza sobre el hígado (representación)
  const pinch = $('pinch');
  const pv = env(t, 21.2, 24.5, 0.4, 0.4);
  pinch.style.opacity = pv.toFixed(3);
  if (pv > 0) {
    liver.pivot.updateMatrixWorld(true);
    const wp = new T.Vector3().setFromMatrixPosition(liver.pivot.matrixWorld);
    const sp = E.project(wp);
    pinch.style.transform = `translate(${sp.x + 40}px, ${sp.y - 55}px) scale(1.25)`;
    const closed = easeInOut(prog(t, 21.4, 21.9));
    const sep = lerp(26, 7, closed);
    $('pinchA').setAttribute('cx', -sep * 0.7); $('pinchA').setAttribute('cy', -sep * 0.7);
    $('pinchB').setAttribute('cx', sep * 0.7); $('pinchB').setAttribute('cy', sep * 0.7);
    $('pinchRing').setAttribute('r', lerp(46, 34, closed));
  }

  // sombra en el suelo
  E.shadow.material.opacity = track(t, [[11.0, 0], [12.8, 0.9], [37.8, 0.9], [38.3, 0], [42.0, 0], [42.8, 0.9]]);

  updateText(tv);
  updateTag(tv);
  E.render();
}

function setPlane(p, v, fill = 1) {
  const on = v > 0.003;
  p.group.visible = on;
  p.ov.visible = on;
  p.face.material.uniforms.uOpacity.value = v;
  p.frameMat.opacity = v;
  p.fillMat.opacity = 0.14 * v * fill;
}

// ---------- reproducción ----------
init().then(() => {
  window.__render = (t) => { renderAt(t); return true; };
  window.__ready = true;
  renderAt(0);
  if (capture) return;
  const scrub = $('scrub'), time = $('time'), play = $('play');
  scrub.max = DURATION;
  let playing = false, t0 = 0, start = 0;
  // La voz manda el reloj de la vista previa mientras suena, para que no se desfase.
  const voice = new Audio('../assets/audio/locucion.mp3');
  const show = (t) => { renderAt(t); scrub.value = t; time.textContent = t.toFixed(2) + ' s'; };
  const stop = () => { playing = false; voice.pause(); play.textContent = 'Reproducir'; };
  scrub.addEventListener('input', () => { stop(); show(+scrub.value); });
  play.addEventListener('click', () => {
    if (playing) return stop();
    playing = true;
    play.textContent = 'Pausa';
    t0 = +scrub.value >= DURATION ? 0 : +scrub.value; start = performance.now(); requestAnimationFrame(loop);
  });
  function loop(now) {
    if (!playing) return;
    let t = t0 + (now - start) / 1000;
    const vt = t - VOICE_OFFSET;
    if (vt >= 0 && vt < voice.duration) {
      if (voice.paused) { voice.currentTime = vt; voice.play().catch(() => {}); }
      else t = voice.currentTime + VOICE_OFFSET;
    }
    if (t >= DURATION) { show(DURATION); stop(); return; }
    show(t);
    requestAnimationFrame(loop);
  }
  const t = parseFloat(new URLSearchParams(location.search).get('t'));
  if (!isNaN(t)) show(t);
});
