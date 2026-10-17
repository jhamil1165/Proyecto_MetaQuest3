// Línea de tiempo del video de INMERSIA (pitch para la clínica).
//
// Todo el video es una función del tiempo: renderAt(t) deja la escena exactamente como debe
// verse en el segundo t. La vista previa la llama en tiempo real; el exportador la llama
// fotograma a fotograma y captura la pantalla.

import { createEngine, W, H } from './gl.js';

// Para cambiar textos sin tocar la animación.
const CONFIG = {
  name: 'INMERSIA',
  subtitle: 'Cirugía inmersiva',
  team: 'Ingeniería Biomédica · PUCP',
  clinic: 'Clínica Anglo Americana',
};
export const DURATION = 63;
window.__duration = DURATION;

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

// ---------- guion ----------
// La música va a 120 BPM (un compás = 2 s) y cada escena empieza en un tiempo del compás:
// los cambios de tools/compose_music.py (CUES) usan estos mismos segundos. Cada frase de la
// locución entra en su escena (VOICE_CUTS en tools/mix_audio.py) y el texto aparece cuando
// la voz lo dice.
//
//    0  gancho: quirófano a las 9:00, 267 imágenes       20  órganos segmentados
//    6  el problema: una por una, en la cabeza           23  tómalos con la mano
//   10  ¿y si pudieran verlo completo? (nace el cuerpo)  26  corta donde quieras
//   16  INMERSIA · Cirugía inmersiva                     30  tres planos, un mismo punto
//   34  captura real en Meta Quest 3                     38  el prototipo en cifras
//   42  hoja de ruta: el siguiente paso, el quirófano    50  buscamos un aliado clínico
//   54  construyámoslo con la clínica                    58  logo (quieto desde 59,6)

// Cada bloque: cuándo entra, cuándo sale, dónde va y sus líneas (con el retraso de cada una).
// Las palabras entran una por una. <b>…</b> = azul y subrayado; <s>…</s> = tachado;
// <n>267</n> = número que cuenta desde cero.
const BLOCKS = [
  { at: 0.15, out: 2.6, pos: 'left', lines: [['kicker', 'Quirófano 3'], ['h1', 'Son las 7:00.'], ['h1 dim', 'Cirugía a las <b>9:00</b>.']], gaps: [0, 0.25, 1.2] },
  { at: 2.85, out: 6.35, pos: 'left', lines: [['h1', 'Y el caso está en'], ['big', '<n>267</n>'], ['h1', 'imágenes.']], gaps: [0, 1.35, 2.9] },
  { at: 6.55, out: 10.35, pos: 'left', lines: [['h1', 'Hoy se revisan'], ['h1', '<b>una por una</b>…'], ['h1', 'y el cuerpo se arma'], ['h1', '<s>en la cabeza</s>.']], gaps: [0, 1.0, 1.9, 2.98], strike: 10.05 },
  { at: 10.45, out: 14.9, pos: 'left', lines: [['q', '¿Y si pudieran'], ['q', 'verlo <b>completo</b>?']], gaps: [0, 0.8] },
  { at: 20.1, out: 22.75, pos: 'left', lines: [['kicker', '01 · Anatomía'], ['h2', 'Órganos'], ['h2', '<b>segmentados.</b>']], gaps: [0, 0.1, 0.5] },
  { at: 23.1, out: 25.75, pos: 'left', lines: [['kicker', '02 · Interacción'], ['h2', 'Tómalos'], ['h2', '<b>con la mano.</b>']], gaps: [0, 0.1, 0.5] },
  { at: 26.1, out: 29.75, pos: 'left', lines: [['kicker', '03 · Plano de corte'], ['h2', 'Corta'], ['h2', '<b>donde quieras.</b>'], ['sub', 'La tomografía sigue al plano.']], gaps: [0, 0.1, 0.45, 1.3] },
  { at: 30.45, out: 33.75, pos: 'tri', lines: [['kicker', '04 · Tres planos'], ['h2', 'Tres planos.'], ['h2', '<b>Un mismo punto.</b>']], gaps: [0, 0.1, 1.0] },
  { at: 34.15, out: 37.75, pos: 'top', lines: [['kicker', 'Captura real del visor'], ['h2', 'Ya funciona en <b>Meta Quest 3</b>.'], ['sub', 'Prototipo en desarrollo.']], gaps: [0, 0.1, 1.5] },
  { at: 38.15, out: 41.75, pos: 'stats', lines: [['kicker', 'El prototipo, hoy']], gaps: [0] },
  { at: 42.15, out: 49.35, pos: 'road', lines: [['kicker', 'Hoja de ruta'], ['h2', 'El siguiente paso:'], ['h2', '<b>el quirófano.</b>']], gaps: [0, 0.15, 1.6] },
  { at: 47.9, out: 49.35, pos: 'footnote', lines: [['sub', 'Fases por desarrollar y validar junto a la clínica.']], gaps: [0] },
  { at: 49.7, out: 53.7, pos: 'ask', lines: [['kicker', 'Lo que buscamos'], ['h1', 'Un <b>aliado clínico.</b>']], gaps: [0, 1.5] },
  { at: 54.1, out: 57.75, pos: 'left', lines: [['h2', 'Construyámoslo juntos…'], ['h2', 'con la'], ['h1', `<b>${CONFIG.clinic}.</b>`]], gaps: [0, 1.6, 1.9] },
];
const WORD_STAGGER = 0.065, WORD_IN = 0.42;

// Las escenas 3D se animaron en su propio reloj; aquí se estira o encoge cada tramo para que
// caiga en su escena del guion. [tiempo de la animación 3D, tiempo del video]
const WARP = [
  [0, 0], [6.75, 10.0], [12.8, 15.0], [13.6, 16.0], [16.6, 19.6], [17.4, 20.0], [20.6, 23.0],
  [25.2, 26.0], [26.2, 26.5], [31.8, 29.9], [33.2, 30.6], [37.6, 34.0], [41.7, 38.0],
  [51.6, 54.0], [55.4, 59.6], [55.4, DURATION],
];
function warp(t) {
  if (t <= 0) return 0;
  for (let i = 0; i < WARP.length - 1; i++) {
    const [a0, b0] = WARP[i], [a1, b1] = WARP[i + 1];
    if (t <= b1) return a0 + (a1 - a0) * (t - b0) / (b1 - b0);
  }
  return WARP[WARP.length - 1][0];
}

const POS = {
  left: 'left:150px; top:540px; transform:translateY(-50%);',
  tri: 'left:1150px; top:120px;',
  top: 'left:180px; top:92px;',
  stats: 'left:150px; top:170px;',
  road: 'left:150px; top:150px;',
  footnote: 'left:150px; top:880px;',
  ask: 'left:150px; top:250px;',
};

// Arma una línea palabra por palabra.
function buildLine(lineEl, html, b) {
  let parent = lineEl, group = null;
  for (const tok of html.split(/(<\/?[bsn]>)/)) {
    if (tok === '<b>' || tok === '<s>') {
      group = { el: document.createElement('span'), bar: document.createElement('i'), kind: tok[1], words: [] };
      group.el.className = tok === '<b>' ? 'hl' : 'st';
      lineEl.appendChild(group.el);
      parent = group.el;
      b.groups.push(group);
    } else if (tok === '</b>' || tok === '</s>') {
      group.el.appendChild(group.bar);
      parent = lineEl; group = null;
    } else if (tok === '<n>' || tok === '</n>') {
      b.nextIsCount = tok === '<n>';
    } else {
      for (const w of tok.split(/(\s+)/)) {
        if (!w) continue;
        if (/^\s+$/.test(w)) { parent.appendChild(document.createTextNode(' ')); continue; }
        const span = document.createElement('span');
        span.className = 'w';
        span.textContent = w;
        parent.appendChild(span);
        const word = { el: span, count: b.nextIsCount ? +w : null };
        if (word.count !== null) span.textContent = '0';
        b.words.push(word);
        if (group) group.words.push(word);
        word.line = b.lineEls.length;
      }
    }
  }
}

const textRoot = document.getElementById('text');
for (const b of BLOCKS) {
  const el = document.createElement('div');
  el.className = 'block';
  el.style.cssText = POS[b.pos];
  b.el = el; b.words = []; b.groups = []; b.lineEls = [];
  b.lines.forEach(([cls, html]) => {
    const l = document.createElement('div');
    l.className = 'line ' + cls;
    l.style.opacity = 1;
    el.appendChild(l);
    buildLine(l, html, b);
    b.lineEls.push(l);
  });
  // cuándo entra cada palabra
  const nInLine = {};
  for (const w of b.words) {
    const k = nInLine[w.line] = (nInLine[w.line] ?? -1) + 1;
    w.at = b.at + b.gaps[w.line] + k * WORD_STAGGER;
  }
  for (const g of b.groups) g.at = g.kind === 's' ? b.strike : g.words[g.words.length - 1].at + 0.25;
  textRoot.appendChild(el);
}

function updateText(t) {
  for (const b of BLOCKS) {
    const on = t >= b.at - 0.05 && t <= b.out + 0.5;
    b.el.style.display = on ? '' : 'none';
    if (!on) continue;
    const vout = easeInOut(prog(t, b.out, b.out + 0.4));
    b.el.style.opacity = (1 - vout).toFixed(4);
    b.el.style.filter = vout > 0 ? `blur(${(vout * 8).toFixed(2)}px)` : 'none';
    for (const w of b.words) {
      const vin = easeOut(prog(t, w.at, w.at + WORD_IN));
      w.el.style.opacity = vin.toFixed(4);
      w.el.style.transform = `translateY(${((1 - vin) * 34 - vout * 22).toFixed(2)}px)`;
      w.el.style.filter = vin < 1 ? `blur(${((1 - vin) * 10).toFixed(2)}px)` : 'none';
      if (w.count !== null) w.el.textContent = Math.round(w.count * easeOut(prog(t, w.at, w.at + 1.1)));
    }
    for (const g of b.groups) {
      const k = easeInOut(prog(t, g.at, g.at + 0.45));
      g.bar.style.transform = `scaleX(${k.toFixed(4)})`;
      if (g.kind === 's') g.el.style.color = `rgba(168, 177, 188, ${lerp(1, 0.75, k).toFixed(3)})`;
    }
  }
}

// Etiqueta discreta que dice qué es real y qué es ilustración.
const TAGS = [
  [0.3, 9.9, 'Cortes reales de la tomografía del proyecto', ''],
  [10.2, 22.9, 'Ilustración en 3D con los datos del proyecto', ''],
  [23.1, 25.9, 'Representación de la interacción · órganos segmentados del proyecto', ''],
  [26.1, 33.8, 'Ilustración en 3D con los datos del proyecto', ''],
  [34.2, 37.8, 'Captura real del prototipo · Meta Quest 3', 'real'],
  [38.2, 41.8, 'Ilustración en 3D con los datos del proyecto', ''],
  [49.8, 57.6, 'Ilustración en 3D con los datos del proyecto', ''],
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

// ---------- marca ----------
// El símbolo es una esfera hecha de cortes: las imágenes que se vuelven un volumen.
const MARK_BARS = 7;
const markBars = [];
{
  const g = document.getElementById('markBars');
  for (let i = 0; i < MARK_BARS; i++) {
    const y = -1 + ((i + 0.5) * 2) / MARK_BARS;
    const hw = Math.sqrt(1 - y * y);
    const r = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    r.setAttribute('x', -hw); r.setAttribute('y', -0.085); r.setAttribute('width', 2 * hw); r.setAttribute('height', 0.17);
    r.setAttribute('rx', 0.085);
    g.appendChild(r);
    markBars.push({ el: r, y });
  }
  const name = document.getElementById('brandName');
  for (const ch of CONFIG.name) {
    const s = document.createElement('span');
    s.textContent = ch;
    name.appendChild(s);
  }
  document.getElementById('brandSub').textContent = CONFIG.subtitle;
  document.getElementById('brandTeam').textContent = CONFIG.team;
}

// Presentación de la marca a partir de t0 (golpe de la música en t0).
function brandAt(tv, t0, outAt, zoom) {
  const el = document.getElementById('brand');
  const vis = easeOut(prog(tv, t0 - 0.8, t0 - 0.3)) * (1 - easeInOut(prog(tv, outAt, outAt + 0.4)));
  el.style.opacity = vis.toFixed(4);
  if (vis <= 0) return 0;
  el.style.transform = `scale(${lerp(0.985, 1.025, easeOut(prog(tv, t0, t0 + zoom))).toFixed(5)})`;
  markBars.forEach((b, i) => {
    const s = t0 - 0.05 + Math.abs(i - (MARK_BARS - 1) / 2) * 0.07;
    const k = easeOut(prog(tv, s, s + 0.55));
    b.el.setAttribute('transform', `translate(0 ${b.y}) scale(${k.toFixed(4)} 1)`);
    b.el.setAttribute('opacity', Math.min(1, k * 1.6).toFixed(3));
  });
  const letters = document.querySelectorAll('#brandName span');
  letters.forEach((s, i) => {
    const a = t0 + 0.05 + i * 0.045;
    const k = easeOut(prog(tv, a, a + 0.6));
    s.style.opacity = k.toFixed(4);
    s.style.transform = `translateX(${((i - (letters.length - 1) / 2) * 34 * (1 - k)).toFixed(2)}px)`;
    s.style.filter = k < 1 ? `blur(${((1 - k) * 14).toFixed(2)}px)` : 'none';
  });
  const sub = easeOut(prog(tv, t0 + 0.6, t0 + 1.2));
  const subEl = document.getElementById('brandSub');
  subEl.style.opacity = sub.toFixed(4);
  subEl.style.transform = `translateY(${((1 - sub) * 18).toFixed(2)}px)`;
  return vis;
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
  [51.6, 8, 12, 2.7, 0, 1250],
  [55.4, 16, 12, 2.85, 0, 1250],
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
  // tv: tiempo del video (guion, textos, paneles 2D). t: reloj de la animación 3D (ver WARP).
  const t = warp(tv);
  const T = E.THREE;
  placeCamera(t);

  // ===== 1 · pantalla con cortes (0 – 10 s del video) =====
  const card = $('ctCard');
  const cardIn = easeOut(prog(t, 0.2, 1.0));
  const cardOut = prog(t, 6.75, 7.05);
  card.style.opacity = (cardIn * (1 - cardOut)).toFixed(3);
  card.style.transform = `translateY(${(1 - cardIn) * 24}px) scale(${lerp(0.97, 1, cardIn)})`;
  // pasa rápido mientras cuenta «267» y luego avanza de a un corte por tiempo de la música
  const z = tv < 6 ? Math.round(track(tv, [[0.3, 22], [4.2, 44], [5.3, HANDOFF - 12], [5.95, HANDOFF - 7]]))
    : Math.min(HANDOFF, HANDOFF - 7 + Math.floor((tv - 6) / 0.5));
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
  E.looseKey.value = easeInOut(prog(t, 6.85, 7.3));
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

  // velo que oscurece la escena: captura real, marca, hoja de ruta y logo
  $('dim').style.opacity = Math.max(
    env(t, 37.8, 41.7, 0.6, 0.6),
    0.7 * env(tv, 15.2, 19.5, 0.6, 0.5),
    0.8 * env(tv, 42.0, 49.35, 0.5, 0.5),
    0.9 * env(tv, 57.4, 99, 0.6)).toFixed(3);

  // captura real
  const capVis = env(t, 38.2, 41.5, 0.7, 0.5);
  const cap = $('capture');
  cap.style.opacity = capVis.toFixed(3);
  cap.style.transform = `translateY(${(1 - easeOut(prog(t, 38.2, 39.0))) * 30}px)`;
  const swap = easeInOut(prog(t, 39.9, 40.4));
  $('capA').style.opacity = (1 - swap).toFixed(3);
  $('capB').style.opacity = swap.toFixed(3);
  $('capA').style.transform = `scale(${lerp(1.0, 1.06, prog(t, 38.2, 40.4))})`;
  $('capB').style.transform = `scale(${lerp(1.02, 1.08, prog(t, 39.9, 41.8))})`;

  // el prototipo en cifras
  document.querySelectorAll('.stat').forEach((row, i) => {
    const a = 38.55 + i * 0.5;
    row.style.opacity = env(tv, a, 41.75, 0.5, 0.4).toFixed(3);
    row.style.transform = `translateX(${((1 - easeOut(prog(tv, a, a + 0.6))) * -30).toFixed(2)}px)`;
    const n = row.querySelector('.sn');
    n.textContent = Math.round(+n.dataset.n * easeOut(prog(tv, a, a + 1.0)));
  });

  // hoja de ruta: la línea avanza de hoy a la validación clínica, al ritmo de la voz
  const road = $('road');
  road.style.opacity = env(tv, 42.9, 49.35, 0.3, 0.4).toFixed(3);
  const ROAD_AT = [42.95, 44.55, 45.45, 47.6];
  const fill = track(tv, [[43.4, 0], [ROAD_AT[1], 1 / 3], [ROAD_AT[2], 2 / 3], [ROAD_AT[3], 1]]);
  $('roadFill').style.transform = `scaleX(${fill.toFixed(4)})`;
  road.querySelectorAll('.node').forEach((nd, i) => {
    const a = ROAD_AT[i] - (i ? 0.15 : 0);
    const k = easeOut(prog(tv, a, a + 0.5));
    nd.style.opacity = k.toFixed(3);
    nd.style.transform = `translateY(${((1 - k) * 24).toFixed(2)}px)`;
    // cada fase se enciende cuando la línea la alcanza
    if (i > 0) nd.querySelector('b').style.borderColor = `rgba(76, 141, 255, ${(0.3 + 0.7 * easeOut(prog(fill, i / 3 - 0.02, i / 3 + 0.08))).toFixed(3)})`;
  });

  // lo que buscamos
  document.querySelectorAll('.chip').forEach((c, i) => {
    const a = 51.7 + i * 0.35;
    const v = env(tv, a, 53.7, 0.55, 0.4);
    c.style.opacity = v.toFixed(3);
    c.style.transform = `translateX(${(1 - easeOut(prog(tv, a, a + 0.6))) * -28}px)`;
  });

  // marca: presentación (16 s) y logo final (58 s), con el golpe de la música
  if (tv < 30) {
    brandAt(tv, 16.0, 19.5, 3.8);
    $('brandPre').style.opacity = easeOut(prog(tv, 15.25, 15.75)).toFixed(3);
    $('brandPre').style.display = '';
    $('brandTeam').style.display = 'none';
  } else {
    brandAt(tv, 58.0, 99, 1.6);
    $('brandPre').style.display = 'none';
    $('brandTeam').style.display = '';
    const k = easeOut(prog(tv, 58.9, 59.5));
    $('brandTeam').style.opacity = k.toFixed(3);
    $('brandTeam').style.transform = `translateY(${((1 - k) * 14).toFixed(2)}px)`;
  }

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
  // La banda sonora (tools/mix_audio.py) manda el reloj mientras suena; sin ella, corre solo.
  const music = new Audio('../out/banda_sonora.mp3');
  const show = (t) => { renderAt(t); scrub.value = t; time.textContent = t.toFixed(2) + ' s'; };
  const stop = () => { playing = false; music.pause(); play.textContent = 'Reproducir'; };
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
    if (t < music.duration) {
      if (music.paused) { music.currentTime = t; music.play().catch(() => {}); }
      else t = music.currentTime;
    }
    if (t >= DURATION) { show(DURATION); stop(); return; }
    show(t);
    requestAnimationFrame(loop);
  }
  const t = parseFloat(new URLSearchParams(location.search).get('t'));
  if (!isNaN(t)) show(t);
});
