// Línea de tiempo del video de INMERSIA (pitch académico para la Clínica Anglo Americana).
//
// Todo el video es una función del tiempo: renderAt(t) deja la escena exactamente como debe
// verse en el segundo t. La vista previa la llama en tiempo real; el exportador la llama
// fotograma a fotograma y captura la pantalla.

import { createEngine, W, H } from './gl.js';

// Para cambiar textos sin tocar la animación.
const CONFIG = {
  name: 'INMERSIA',
  subtitle: 'Cirugía inmersiva',
  team: 'Proyecto de investigación · Ingeniería Biomédica · PUCP',
  clinic: 'Clínica Anglo Americana',
};
export const DURATION = 66;
window.__duration = DURATION;

const capture = new URLSearchParams(location.search).has('capture');
if (capture) document.body.classList.add('capture');

// ---------- utilidades de animación ----------
const clamp01 = (x) => Math.min(1, Math.max(0, x));
const prog = (t, a, b) => clamp01((t - a) / (b - a));
const lerp = (a, b, x) => a + (b - a) * x;
const easeOut = (x) => 1 - Math.pow(1 - x, 3);
const easeInOut = (x) => (x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2);
const easeBack = (x) => 1 + 2.2 * Math.pow(x - 1, 3) + 1.2 * Math.pow(x - 1, 2);
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
// La música va a 120 BPM (un compás = 2 s); sus cambios (CUES en tools/compose_music.py) y las
// frases de la voz (VOICE_CUTS en tools/mix_audio.py) usan estos mismos segundos, y el texto
// aparece cuando la voz lo dice.
//
//    0  gancho: son las 7:00, cirugía a las 9:00, 267 imágenes
//    8  el problema: una por una… y el cuerpo se arma en la cabeza
//   14  ¿y si pudieran verlo completo? (los cortes forman el cuerpo)
//   19  les presentamos INMERSIA · Cirugía inmersiva (golpe en 20)
//   24  cada órgano con su volumen · lo toman con la mano · lo cortan · tres planos
//   37  ya funciona en Meta Quest 3
//   41  lo que viene: modo cirugía (concepto): zona, márgenes, abordaje
//   50  hoy, un prototipo · buscamos un aliado clínico
//   56  construyámoslo juntos, con la clínica
//   60  logo (quieto desde 61,8)

// Cada bloque: cuándo entra, cuándo sale, dónde va y sus líneas (con el retraso de cada una).
// Las palabras entran una por una. <b>…</b> = azul y subrayado; <s>…</s> = tachado;
// <n>267</n> = número que cuenta desde cero.
const BLOCKS = [
  { at: 0.25, out: 3.9, pos: 'left', lines: [['kicker', 'Quirófano 3'], ['h1', 'Son las 7:00.'], ['h1 dim', 'Cirugía a las <b>9:00</b>.']], gaps: [0, 0.25, 1.85] },
  { at: 4.0, out: 7.9, pos: 'left', lines: [['h1', 'Y todo el caso está en'], ['big', '<n>267</n>'], ['h1', 'imágenes.']], gaps: [0, 1.2, 2.85] },
  { at: 8.3, out: 13.8, pos: 'left', lines: [['h1', 'Hoy se revisan'], ['h1', '<b>una por una</b>,'], ['h1', 'y el cuerpo se arma'], ['h1', '<s>en la cabeza</s>.']], gaps: [0, 1.0, 2.15, 4.4], strike: 13.15 },
  { at: 14.35, out: 18.45, pos: 'left', lines: [['q', '¿Y si pudieran'], ['q', 'verlo <b>completo</b>?']], gaps: [0, 0.75] },
  { at: 24.25, out: 27.6, pos: 'left', lines: [['kicker', '01 · Anatomía'], ['h2', 'Cada órgano,'], ['h2', '<b>con su volumen.</b>']], gaps: [0, 0.05, 1.95] },
  { at: 27.95, out: 30.15, pos: 'left', lines: [['kicker', '02 · Interacción'], ['h2', 'Lo toman'], ['h2', '<b>con la mano.</b>']], gaps: [0, 0.05, 0.6] },
  { at: 30.35, out: 33.7, pos: 'left', lines: [['kicker', '03 · Plano de corte'], ['h2', 'Lo cortan'], ['h2', '<b>donde quieran.</b>'], ['sub', 'Y la tomografía los acompaña.']], gaps: [0, 0.05, 0.65, 1.7] },
  { at: 33.85, out: 36.9, pos: 'tri', lines: [['kicker', '04 · Tres planos'], ['h2', 'Tres planos,'], ['h2', '<b>un mismo punto.</b>']], gaps: [0, 0.05, 0.9] },
  { at: 37.45, out: 40.75, pos: 'left', lines: [['kicker', 'Realidad mixta'], ['h2', 'Ya funciona en'], ['h1', '<b>Meta Quest 3</b>.'], ['sub', 'Prototipo en desarrollo.']], gaps: [0, 0.15, 1.45, 2.2] },
  { at: 41.45, out: 48.05, pos: 'surg', lines: [['kicker', 'Lo que viene · concepto'], ['xl', '<b>Modo cirugía</b>']], gaps: [0, 1.4] },
  { at: 48.3, out: 50.25, pos: 'surg', lines: [['h2', 'Antes de entrar'], ['h2', '<b>al quirófano.</b>']], gaps: [0, 0.85] },
  { at: 50.7, out: 53.25, pos: 'ask', lines: [['kicker', 'Proyecto de investigación'], ['h1', 'Hoy, un <b>prototipo</b>.']], gaps: [0, 0.35] },
  { at: 53.45, out: 56.35, pos: 'ask', lines: [['kicker', 'Lo que buscamos'], ['h1', 'Un <b>aliado clínico</b>.']], gaps: [0, 0.8] },
  { at: 56.5, out: 59.6, pos: 'left', lines: [['h2', 'Construyámoslo juntos,'], ['h2', 'con la'], ['h1', `<b>${CONFIG.clinic}.</b>`]], gaps: [0.05, 1.45, 1.75] },
];
const WORD_STAGGER = 0.065, WORD_IN = 0.42;

// Las escenas 3D se animan en su propio reloj; aquí se estira o encoge cada tramo para que
// caiga en su escena del guion. [tiempo de la animación 3D, tiempo del video]. Donde el
// reloj 3D salta (37,2 s, 41 s y 50,5 s) hay un corte, tapado con un barrido de luz.
const WARP = [
  [0, 0], [1.0, 1.0], [6.75, 14.0], [12.8, 18.6], [16.6, 23.6], [18.2, 24.3], [20.1, 26.0], [20.7, 27.8],
  [21.5, 28.2], [24.4, 30.0], [25.2, 30.5], [26.4, 31.0], [31.8, 33.8], [33.2, 34.3], [37.6, 37.2],
  [42.3, 37.2], [44.0, 41.0],                  // el cuerpo completo: ya funciona en Meta Quest 3
  [60.0, 41.0], [69.5, 50.5],                  // modo cirugía
  [44.0, 50.5], [49.5, 56.0], [53.5, 60.0], [55.4, 61.8], [55.4, DURATION],
];
function warp(t) {
  if (t <= 0) return 0;
  for (let i = 0; i < WARP.length - 1; i++) {
    const [a0, b0] = WARP[i], [a1, b1] = WARP[i + 1];
    if (t <= b1) return a0 + (a1 - a0) * (t - b0) / (b1 - b0);
  }
  return WARP[WARP.length - 1][0];
}
const CUTS = [37.2, 41.0, 50.5];

const POS = {
  left: 'left:150px; top:540px; transform:translateY(-50%);',
  tri: 'left:1150px; top:120px;',
  surg: 'left:150px; top:150px;',
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
      w.el.style.transform = `translateY(${((1 - vin) * 34 - vout * 22).toFixed(2)}px) scale(${lerp(0.92, 1, vin).toFixed(4)})`;
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

// Etiqueta discreta que dice qué es real, qué es ilustración y qué es concepto.
const TAGS = [
  [0.3, 13.9, 'Cortes reales de la tomografía del proyecto', ''],
  [14.2, 24.0, 'Ilustración en 3D con los datos del proyecto', ''],
  [24.2, 27.7, 'Ilustración en 3D · volúmenes medidos en la segmentación del proyecto', ''],
  [27.9, 30.3, 'Representación de la interacción · órganos segmentados del proyecto', ''],
  [30.4, 40.8, 'Ilustración en 3D con los datos del proyecto', ''],
  [41.3, 50.2, 'Concepto en desarrollo · la zona y las trayectorias son ilustrativas', 'concept'],
  [50.8, 59.5, 'Ilustración en 3D con los datos del proyecto', ''],
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

// Presentación de la marca con el golpe de la música en t0; al salir se acerca y se desenfoca.
function brandAt(tv, t0, outAt, zoom) {
  const el = document.getElementById('brand');
  const out = easeInOut(prog(tv, outAt, outAt + 0.4));
  const vis = easeOut(prog(tv, t0 - 1.1, t0 - 0.6)) * (1 - out);
  el.style.opacity = vis.toFixed(4);
  if (vis <= 0) return 0;
  el.style.transform = `scale(${(lerp(0.985, 1.025, easeOut(prog(tv, t0, t0 + zoom))) + out * 0.35).toFixed(5)})`;
  el.style.filter = out > 0 ? `blur(${(out * 12).toFixed(2)}px)` : 'none';
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
  const sub = easeOut(prog(tv, t0 + 0.75, t0 + 1.35));
  const subEl = document.getElementById('brandSub');
  subEl.style.opacity = sub.toFixed(4);
  subEl.style.transform = `translateY(${((1 - sub) * 18).toFixed(2)}px)`;
  // destello horizontal en el golpe
  const st = prog(tv, t0, t0 + 0.7);
  const streak = document.getElementById('streak');
  streak.style.opacity = (st > 0 && st < 1 ? (1 - st) : 0).toFixed(3);
  streak.style.transform = `scaleX(${easeOut(st).toFixed(4)})`;
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

// ---------- líneas guía (etiquetas de órganos y del modo cirugía) ----------
const SVGNS = 'http://www.w3.org/2000/svg';
const linesEl = document.getElementById('lines');
const leaders = {};
// Dibuja una línea de a → codo → b, revelada hasta k (0..1), con un punto en el origen.
function leader(id, a, b, k, color, glow) {
  let L = leaders[id];
  if (!L) {
    L = leaders[id] = { path: document.createElementNS(SVGNS, 'path'), dot: document.createElementNS(SVGNS, 'circle') };
    L.path.setAttribute('fill', 'none'); L.path.setAttribute('stroke-width', '2.5'); L.path.setAttribute('stroke-linecap', 'round');
    L.dot.setAttribute('r', '6');
    linesEl.append(L.path, L.dot);
  }
  if (k <= 0.001) { L.path.style.display = L.dot.style.display = 'none'; return; }
  L.path.style.display = L.dot.style.display = '';
  const ex = b.x - Math.sign(b.x - a.x) * 36, ey = b.y;
  const len = Math.hypot(ex - a.x, ey - a.y) + Math.abs(b.x - ex);
  L.path.setAttribute('d', `M${a.x.toFixed(1)} ${a.y.toFixed(1)} L${ex.toFixed(1)} ${ey.toFixed(1)} L${b.x.toFixed(1)} ${b.y.toFixed(1)}`);
  L.path.setAttribute('stroke', color);
  L.path.setAttribute('stroke-dasharray', `${(len * k).toFixed(1)} ${len.toFixed(1)}`);
  L.path.style.filter = glow ? `drop-shadow(0 0 6px ${glow})` : 'none';
  L.dot.setAttribute('cx', a.x.toFixed(1)); L.dot.setAttribute('cy', a.y.toFixed(1));
  L.dot.setAttribute('fill', color);
  L.dot.style.filter = glow ? `drop-shadow(0 0 8px ${glow})` : 'none';
  L.dot.style.opacity = Math.min(1, k * 3).toFixed(3);
}
// Trayectoria punteada de a hacia b, que avanza hasta k y cuyos trazos fluyen hacia b.
function trajectory(id, a, b, k, tv) {
  let L = leaders[id];
  if (!L) {
    L = leaders[id] = { path: document.createElementNS(SVGNS, 'path'), dot: document.createElementNS(SVGNS, 'circle') };
    L.path.setAttribute('fill', 'none'); L.path.setAttribute('stroke', '#FFC870'); L.path.setAttribute('stroke-width', '3.5');
    L.path.setAttribute('stroke-linecap', 'round'); L.path.setAttribute('stroke-dasharray', '2 11');
    L.path.style.filter = 'drop-shadow(0 0 6px rgba(255,181,71,0.95))';
    L.dot.setAttribute('r', '5'); L.dot.setAttribute('fill', '#FFFFFF');
    L.dot.style.filter = 'drop-shadow(0 0 8px #FFB547)';
    linesEl.append(L.path, L.dot);
  }
  if (k <= 0.001) { L.path.style.display = L.dot.style.display = 'none'; return; }
  L.path.style.display = L.dot.style.display = '';
  const ex = lerp(a.x, b.x, k), ey = lerp(a.y, b.y, k);
  L.path.setAttribute('d', `M${a.x.toFixed(1)} ${a.y.toFixed(1)} L${ex.toFixed(1)} ${ey.toFixed(1)}`);
  L.path.setAttribute('stroke-dashoffset', (-tv * 40).toFixed(1));
  L.dot.setAttribute('cx', ex.toFixed(1)); L.dot.setAttribute('cy', ey.toFixed(1));
  L.dot.style.opacity = k < 0.999 ? '1' : '0';
}
function hideLeaders(prefix) { for (const [id, L] of Object.entries(leaders)) if (id.startsWith(prefix)) L.path.style.display = L.dot.style.display = 'none'; }

// ---------- montaje ----------
const $ = (id) => document.getElementById(id);
let E; // motor 3D
let SURG; // piezas 3D del modo cirugía

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
  for (const o of Object.values(E.organs)) {
    o.mat.clippingPlanes = [clipPlane];
    o.mesh.geometry.computeBoundingBox();
  }
  abdomenY = Object.values(E.organs).reduce((s, o) => s + o.pivot.userData.home.y, 0) / 4;
  SURG = buildSurgery();

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
  await document.fonts.ready;
  $('loading').remove();
}

let clipPlane, abdomenY = 0;
const STACK = 56;
const HANDOFF_I = 24;                                     // corte de la pila que viene de la pantalla
const HANDOFF = Math.round(((HANDOFF_I + 0.5) / STACK) * NSLICES - 0.5);
const sliceY = (z) => E.half.y - ((z + 0.5) / NSLICES) * E.size.y;

// ---------- modo cirugía (concepto) ----------
// Un punto dentro del hígado (lo más lejos posible de su superficie), tres puertos sobre la
// piel del abdomen y las trayectorias de cada puerto a la zona. Es ilustrativo: no marca ningún
// hallazgo del paciente.
function buildSurgery() {
  const T = E.THREE;
  E.body.updateMatrixWorld(true);
  const liver = E.organs.higado;
  const mesh = liver.mesh, g = mesh.geometry, bb = g.boundingBox;
  const side = mesh.material.side;
  mesh.material.side = T.DoubleSide;
  const ray = new T.Raycaster();
  const dir = new T.Vector3(1, 0.013, 0.021).normalize();
  const inside = (pw) => { ray.set(pw, dir); return ray.intersectObject(mesh, false).length % 2 === 1; };
  const pos = g.attributes.position;
  const verts = [];
  for (let i = 0; i < pos.count; i += 13) verts.push(new T.Vector3().fromBufferAttribute(pos, i).applyMatrix4(mesh.matrixWorld));
  let best = null, bestD = -1;
  const N = 6;
  for (let ix = 0; ix < N; ix++) for (let iy = 0; iy < N; iy++) for (let iz = 0; iz < N; iz++) {
    const p = new T.Vector3(
      lerp(bb.min.x, bb.max.x, 0.12 + 0.76 * ix / (N - 1)),
      lerp(bb.min.y, bb.max.y, 0.12 + 0.76 * iy / (N - 1)),
      lerp(bb.min.z, bb.max.z, 0.12 + 0.76 * iz / (N - 1))).applyMatrix4(mesh.matrixWorld);
    if (!inside(p)) continue;
    let d = Infinity;
    for (const v of verts) d = Math.min(d, v.distanceToSquared(p));
    if (d > bestD) { bestD = d; best = p; }
  }
  mesh.material.side = side;
  const target = best || liver.pivot.position.clone();

  // piel anterior: primer vóxel con tejido, entrando desde el frente
  const { NX, NY, NZ } = E.dims, raw = E.raw, h = E.half;
  const skinZ = (x, y) => {
    const ix = Math.max(0, Math.min(NX - 1, Math.round(((x + h.x) / (2 * h.x)) * NX)));
    const iz = Math.max(0, Math.min(NZ - 1, Math.round((1 - (y + h.y) / (2 * h.y)) * NZ)));
    for (let iy = 0; iy < NY - 1; iy++) {
      if (raw[ix + NX * (iy + NY * iz)] > 34 && raw[ix + NX * (iy + 1 + NY * iz)] > 34) return h.z - 2 * h.z * ((iy + 0.5) / NY);
    }
    return h.z * 0.5;
  };

  const amber = new T.Color('#FFB547');
  const glowMat = (color, rim, core) => new T.ShaderMaterial({
    uniforms: { uColor: { value: color }, uOpacity: { value: 0 }, uRim: { value: rim }, uCore: { value: core } },
    vertexShader: `varying vec3 vN; varying vec3 vV; void main(){ vec4 mv = modelViewMatrix * vec4(position, 1.0);
      vN = normalize(normalMatrix * normal); vV = normalize(-mv.xyz); gl_Position = projectionMatrix * mv; }`,
    fragmentShader: `uniform vec3 uColor; uniform float uOpacity, uRim, uCore; varying vec3 vN; varying vec3 vV;
      void main(){ float f = 1.0 - abs(dot(normalize(vN), normalize(vV)));
        float a = uCore + uRim * pow(f, 2.5); gl_FragColor = vec4(uColor * a * uOpacity, 1.0); }`,
    transparent: true, depthTest: false, depthWrite: false, blending: T.AdditiveBlending, side: T.DoubleSide,
  });
  const core = new T.Mesh(new T.SphereGeometry(0.016, 40, 20), glowMat(amber, 1.4, 0.55));
  const margin = new T.Mesh(new T.SphereGeometry(0.032, 48, 24), glowMat(new T.Color('#9CC4FF'), 1.6, 0.04));
  const ringGeo = new T.RingGeometry(0.022, 0.0245, 64);
  const pulse = new T.Mesh(ringGeo, new T.MeshBasicMaterial({ color: amber, transparent: true, depthTest: false, blending: T.AdditiveBlending }));
  core.position.copy(target); margin.position.copy(target); pulse.position.copy(target);
  for (const m of [core, margin, pulse]) { m.renderOrder = 10; E.overlay.add(m); }

  const ports = [[-0.085, -0.02], [-0.01, -0.095], [0.065, -0.055]].map(([dx, dy], i) => {
    const x = target.x + dx, y = target.y + dy;
    const p = new T.Vector3(x, y, skinZ(x, y) + 0.004);
    const grp = new T.Group();
    grp.position.copy(p);
    const ring = new T.Mesh(new T.RingGeometry(0.008, 0.0115, 40), new T.MeshBasicMaterial({ color: amber, transparent: true, depthTest: false, blending: T.AdditiveBlending, side: T.DoubleSide }));
    const dot = new T.Mesh(new T.CircleGeometry(0.0038, 24), new T.MeshBasicMaterial({ color: '#FFFFFF', transparent: true, depthTest: false, blending: T.AdditiveBlending, side: T.DoubleSide }));
    grp.add(ring, dot);
    grp.renderOrder = 11;
    E.overlay.add(grp);
    return { grp, ring, dot, p, i };
  });
  return { target, core, margin, pulse, ports };
}

// Cámara: [t, azimut, elevación, distancia, objetivo ('abd' = abdomen, 'liv' = zona del
// modo cirugía, o una altura), x en pantalla]
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
  // modo cirugía
  [60.0, -16, 14, 1.75, 'liv', 1120],
  [61.9, -20, 15, 1.68, 'liv', 1120],
  [63.2, -34, 17, 1.12, 'liv', 1040],
  [69.5, -46, 20, 1.02, 'liv', 1040],
];
function camAt(t) {
  const tgt = (r, axis) => (r[4] === 'liv' ? SURG.target[axis] : axis === 'y' ? (r[4] === 'abd' ? abdomenY : r[4]) : 0);
  const col = (f) => CAM.map((r) => [r[0], f(r)]);
  return {
    az: track(t, col((r) => r[1])), el: track(t, col((r) => r[2])), dist: track(t, col((r) => r[3])),
    tx: track(t, col((r) => tgt(r, 'x'))), ty: track(t, col((r) => tgt(r, 'y'))), tz: track(t, col((r) => tgt(r, 'z'))),
    cx: track(t, col((r) => r[5])),
  };
}

function placeCamera(t) {
  const c = camAt(t);
  const a = c.az * DEG, e = c.el * DEG;
  E.camera.position.set(c.tx + Math.sin(a) * Math.cos(e) * c.dist, c.ty + Math.sin(e) * c.dist, c.tz + Math.cos(a) * Math.cos(e) * c.dist);
  E.camera.lookAt(c.tx, c.ty, c.tz);
  E.camera.setViewOffset(W, H, -(c.cx - W / 2), 0, W, H);
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

// Golpe del bombo durante el ritmo (cae en cada segundo entero): da pulso a la plataforma.
function kick(tv) {
  const on = (tv >= 24 && tv < 37) || (tv >= 42.8 && tv < 55.5);
  return on ? Math.exp(-(tv % 1) / 0.12) : 0;
}

function renderAt(tv) {
  // tv: tiempo del video (guion, textos, paneles 2D). t: reloj de la animación 3D (ver WARP).
  const t = warp(tv);
  const T = E.THREE;
  const surg = t >= 59.5;          // modo cirugía (tramo 60–69,5 del reloj 3D)
  placeCamera(t);

  // ===== 1 · pantalla con cortes (0 – 14 s del video) =====
  const card = $('ctCard');
  const cardIn = easeOut(prog(t, 0.2, 1.0));
  const cardOut = prog(t, 6.75, 7.05);
  card.style.opacity = (cardIn * (1 - cardOut)).toFixed(3);
  // flota inclinada en 3D y se endereza antes de soltar el corte
  const tilt = 1 - easeInOut(prog(tv, 11.5, 13.6));
  card.style.transform = `perspective(1700px) translateY(${((1 - cardIn) * 30 + Math.sin(tv * 1.3) * 6 * tilt).toFixed(2)}px) ` +
    `rotateY(${(-17 * tilt).toFixed(3)}deg) rotateX(${(4 * tilt).toFixed(3)}deg) scale(${lerp(0.96, 1, cardIn).toFixed(4)})`;
  // pasa rápido mientras cuenta «267» y luego avanza de a un corte por tiempo de la música
  const z = tv < 8 ? Math.round(track(tv, [[0.3, 22], [5.0, 44], [6.4, HANDOFF - 12], [7.8, HANDOFF - 9]]))
    : Math.min(HANDOFF, HANDOFF - 9 + Math.floor((tv - 8) / 0.5));
  const step = tv >= 8.5 && tv < 13 ? Math.exp(-((tv - 8) % 0.5) / 0.1) : 0;
  card.style.borderColor = `rgba(127, 178, 255, ${(0.22 + 0.5 * step).toFixed(3)})`;
  const scan = $('ctScan');
  const fast = tv >= 5.2 && tv < 6.4;
  scan.style.opacity = (fast ? 0.9 : step * 0.9).toFixed(3);
  scan.style.top = `${(118 + 600 * (fast ? ((tv - 5.2) * 2.5) % 1 : 1 - step)).toFixed(1)}px`;
  if (t < 7.2) {
    drawSlice($('ctCanvas'), z);
    $('ctCount').textContent = `Corte ${pad(z + 1, 3)} / ${NSLICES}`;
  }

  // ===== corte suelto que se convierte en volumen =====
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

  // ===== pila de cortes → volumen =====
  const stackFade = 1 - easeInOut(prog(t, 11.6, 12.9));
  E.stack.forEach((m, i) => {
    const d = Math.abs(i - HANDOFF_I);
    const st = 8.5 + (d / 32) * 2.4;
    const k = easeOut(prog(t, st, st + 0.6));
    const vis = k * stackFade * (t < 20 ? 1 : 0);
    m.visible = vis > 0.002;
    m.material.uniforms.uOpacity.value = vis * (i === HANDOFF_I ? 1 - E.loose.material.opacity * 0.9 : 1);
    m.position.y = lerp(sliceY(HANDOFF), m.userData.homeY, easeOut(prog(t, st, st + 0.9)));
  });

  // ===== volumen =====
  const vol = E.volMat.uniforms;
  vol.uOpacity.value = track(t, [[11.4, 0], [12.8, 1], [20.8, 1], [21.6, 0.16], [24.8, 0.16], [25.8, 1], [33.0, 1], [34.0, 0.32], [37.8, 0.32], [38.4, 0], [41.8, 0], [42.8, 1]]);
  vol.uDensity.value = track(t, [[17.4, 1], [18.6, 0.55], [25.0, 0.55], [25.8, 0.7], [41.8, 0.7], [42.6, 0.5]]);
  vol.uBone.value = 0;
  const glass = surg ? easeInOut(prog(tv, 42.9, 43.8)) : 0;   // tras el clic: el hígado se vuelve vidrio
  if (surg) {
    vol.uOpacity.value = lerp(0.85, 0.14, glass);
    vol.uDensity.value = 0.6;
  }

  // ===== órganos =====
  const toggles = { higado: 18.3, estomago: 18.9, pancreas: 19.5, vesicula: 20.1 };
  for (const [name, o] of Object.entries(E.organs)) {
    const ton = toggles[name];
    let a = easeOut(prog(t, ton, ton + 0.55));
    a *= 1 - easeInOut(prog(t, 33.0, 33.8));          // fuera en "tres planos"
    a = t > 38 ? easeOut(prog(t, 42.2, 43.2)) : a;     // vuelven para el cuerpo completo
    if (surg) a = name === 'higado' ? lerp(1, 0.09, glass) : lerp(1, 0.02, glass);   // brillan en HDR: basta muy poca opacidad
    o.mat.opacity = a;
    o.mesh.visible = a > 0.003;
    // three.js compila aparte la versión opaca del material: al cambiar hay que recompilarlo
    if (o.mat.transparent !== a < 0.999) { o.mat.transparent = a < 0.999; o.mat.needsUpdate = true; }
    o.mat.depthWrite = a > 0.5;
    const s = lerp(0.9, 1, easeOut(prog(t, ton, ton + 0.6)));
    o.pivot.scale.setScalar(t > 38 ? 1 : s);
    o.pivot.position.copy(o.pivot.userData.home);
    o.pivot.rotation.set(0, 0, 0);
    o.mat.emissive.set(0x000000);
  }

  // agarrar y girar el hígado
  const liver = E.organs.higado;
  const grab = easeInOut(prog(t, 21.5, 22.3)) * (1 - easeInOut(prog(t, 24.3, 25.2)));
  if (grab > 0) {
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
  let axVis = env(t, 25.6, 37.6, 0.7, 0.5) + (t > 38 ? env(t, 42.4, 99, 0.9) : 0);
  const axZ = t > 38 ? 118 : track(t, [[26.4, 58], [31.8, 126], [34.6, 126], [37.4, 112]]);
  const corRow = track(t, [[34.6, 238], [37.4, 262]]);  // fila (0..511, 0 = frente)
  const sagCol = track(t, [[34.6, 300], [37.4, 252]]);  // columna (0..511, 0 = derecha del paciente)
  P.axial.group.position.y = sliceY(axZ);
  P.coronal.group.position.z = E.half.z - ((corRow + 0.5) / 512) * E.size.z;
  P.sagital.group.position.x = -E.half.x + ((sagCol + 0.5) / 512) * E.size.x;
  const triVis = env(t, 33.2, 37.6, 0.7, 0.5);
  if (surg) axVis = 0;
  setPlane(P.axial, axVis, lerp(1, 0.35, triVis));
  setPlane(P.coronal, triVis, 0.35);
  setPlane(P.sagital, triVis, 0.35);

  // el plano axial corta el volumen y los órganos, salvo en "tres planos"
  const clipOn = t >= 25.6 && t < 33.4;
  const clipY = clipOn ? sliceY(axZ) : 10;
  vol.uClipD.value = clipY;
  clipPlane.constant = clipY;
  E.body.updateMatrixWorld(true);

  // ===== luz: plataforma, polvo y brillo =====
  const k = kick(tv);
  E.platU.uTime.value = tv;
  // más tenue cuando la cámara está cerca (la plataforma queda grande en primer plano)
  E.platU.uOpacity.value = track(tv, [[16.0, 0], [18.6, 0.75], [23.6, 0.75], [24.3, 0.4], [33.6, 0.4], [34.4, 0.75],
    [41.0, 0.75], [43.2, 0.4], [50.5, 0.75], [59.6, 0.75], [60.4, 0.2]]);
  E.platU.uPulse.value = k * 0.7;
  E.dustU.uTime.value = tv;
  E.dustU.uOpacity.value = track(tv, [[0, 0], [1.5, 0.7], [14, 0.7], [18.6, 1]]);
  E.bloom.uStrength.value = (tv > 14 && tv < 19 ? 0.4 : surg ? 0.5 : 0.7) + 0.2 * k;

  // ===== paneles 2D =====
  // etiquetas de los órganos: nombre y volumen medido, como en el visor
  const otagOn = { higado: 24.39, estomago: 24.93, pancreas: 25.46, vesicula: 26.0 };
  const OFF = { higado: [-170, -150], estomago: [150, -170], pancreas: [230, -50], vesicula: [-110, 130] };
  document.querySelectorAll('.otag').forEach((el) => {
    const name = el.dataset.organ, o = E.organs[name];
    const outAt = name === 'higado' ? 30.1 : 27.55;
    const v = env(tv, otagOn[name] + 0.1, outAt, 0.45, 0.35);
    el.style.opacity = v.toFixed(3);
    if (v <= 0) { leader('o-' + name, { x: 0, y: 0 }, { x: 0, y: 0 }, 0); return; }
    o.pivot.updateMatrixWorld(true);
    const top = new T.Vector3(0, o.mesh.geometry.boundingBox.max.y * 0.85, 0).applyMatrix4(o.pivot.matrixWorld);
    const a = E.project(top);
    const b = { x: a.x + OFF[name][0], y: a.y + OFF[name][1] };
    const pop = easeBack(prog(tv, otagOn[name] + 0.1, otagOn[name] + 0.55));
    el.style.transform = `translate(${(b.x - el.offsetWidth / 2).toFixed(1)}px, ${(b.y - el.offsetHeight).toFixed(1)}px) scale(${lerp(0.6, 1, pop).toFixed(4)})`;
    el.querySelector('span').textContent = Math.round(+el.dataset.ml * easeOut(prog(tv, otagOn[name] + 0.1, otagOn[name] + 0.9)));
    leader('o-' + name, a, b, easeOut(prog(tv, otagOn[name], otagOn[name] + 0.35)) * v, 'rgba(255,255,255,0.75)', 'rgba(127,178,255,0.8)');
  });

  // corte axial que sigue al plano
  const ax = $('axCard');
  const axCardVis = env(t, 26.0, 32.8, 0.7, 0.5);
  ax.style.opacity = axCardVis.toFixed(3);
  ax.style.transform = `perspective(1700px) translateX(${((1 - easeOut(prog(t, 26.0, 26.8))) * 60).toFixed(2)}px) rotateY(${(-10 * (1 - easeOut(prog(t, 26.0, 27.2)))).toFixed(2)}deg)`;
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

  // velo que oscurece la escena detrás de la marca y del logo
  $('dim').style.opacity = Math.max(0.68 * env(tv, 18.9, 23.3, 0.5, 0.4), 0.9 * env(tv, 59.6, 99, 0.45)).toFixed(3);

  // ===== modo cirugía (concepto) =====
  updateSurgery(tv, surg);

  // hoja de ruta corta: hoy prototipo → validación con cirujanos → quirófano
  const road = $('road');
  road.style.opacity = env(tv, 50.8, 53.25, 0.35, 0.4).toFixed(3);
  const fill = track(tv, [[51.3, 0], [52.3, 0.5], [53.0, 1]]);
  $('roadFill').style.transform = `scaleX(${fill.toFixed(4)})`;
  road.querySelectorAll('.node').forEach((nd, i) => {
    const a = [50.9, 52.15, 52.85][i];
    const kk = easeOut(prog(tv, a, a + 0.45));
    nd.style.opacity = kk.toFixed(3);
    nd.style.transform = `translateY(${((1 - kk) * 24).toFixed(2)}px)`;
    if (i > 0) nd.querySelector('b').style.borderColor = `rgba(127, 178, 255, ${(0.3 + 0.7 * kk).toFixed(3)})`;
  });

  // lo que buscamos: píldoras que se apilan
  document.querySelectorAll('.chip').forEach((c, i) => {
    const a = 54.55 + i * 0.4;
    const v = env(tv, a, 56.35, 0.5, 0.4);
    const pop = easeBack(prog(tv, a, a + 0.5));
    c.style.opacity = v.toFixed(3);
    c.style.transform = `translateY(${((1 - easeOut(prog(tv, a, a + 0.5))) * 40).toFixed(2)}px) scale(${lerp(0.7, 1, pop).toFixed(4)})`;
  });

  // gesto de pinza sobre el hígado (representación)
  const pinch = $('pinch');
  const pv = env(t, 21.2, 24.5, 0.4, 0.4);
  if (!surg) {
    pinch.style.opacity = pv.toFixed(3);
    if (pv > 0) {
      liver.pivot.updateMatrixWorld(true);
      const wp = new T.Vector3().setFromMatrixPosition(liver.pivot.matrixWorld);
      const sp = E.project(wp);
      pinch.style.transform = `translate(${sp.x + 40}px, ${sp.y - 55}px) scale(1.25)`;
      setPinch(easeInOut(prog(t, 21.4, 21.9)));
    }
  }

  // marca: presentación (golpe en 20 s) y logo final (golpe en 60 s)
  if (tv < 40) {
    brandAt(tv, 20.0, 23.3, 3.3);
    $('brandPre').style.opacity = easeOut(prog(tv, 18.95, 19.45)).toFixed(3);
    $('brandPre').style.display = '';
    $('brandTeam').style.display = 'none';
  } else {
    brandAt(tv, 60.0, 99, 1.6);
    $('brandPre').style.display = 'none';
    $('brandTeam').style.display = '';
    const kk = easeOut(prog(tv, 60.9, 61.5));
    $('brandTeam').style.opacity = kk.toFixed(3);
    $('brandTeam').style.transform = `translateY(${((1 - kk) * 14).toFixed(2)}px)`;
  }

  // destellos y barridos: golpes de la música y cortes entre escenas
  const hit = (at, amt) => (tv >= at ? amt * Math.exp(-(tv - at) / 0.18) : 0);
  let wk = 0;
  for (const c of CUTS) wk = Math.max(wk, 1 - Math.abs(tv - c) / 0.24);
  wk = easeInOut(clamp01(wk));
  $('cam').style.transform = wk > 0 ? `scale(${(1 + 0.07 * wk).toFixed(4)})` : '';
  $('cam').style.filter = wk > 0 ? `blur(${(wk * 14).toFixed(2)}px)` : 'none';
  $('flash').style.opacity = Math.min(1, hit(20.0, 0.55) + hit(42.8, 0.3) + hit(60.0, 0.6) + 0.35 * wk).toFixed(3);

  // sombra en el suelo: la reemplaza la plataforma de luz
  E.shadow.material.opacity = 0;

  updateText(tv);
  updateTag(tv);
  E.render();
}

function setPinch(closed) {
  const sep = lerp(26, 7, closed);
  $('pinchA').setAttribute('cx', -sep * 0.7); $('pinchA').setAttribute('cy', -sep * 0.7);
  $('pinchB').setAttribute('cx', sep * 0.7); $('pinchB').setAttribute('cy', sep * 0.7);
  $('pinchRing').setAttribute('r', lerp(46, 34, closed));
}

// Botón «Modo cirugía» que la mano pulsa, y luego la zona, sus márgenes y el abordaje.
const SLABEL_POS = [[1500, 300], [1500, 470], [1500, 640]];
function updateSurgery(tv, surg) {
  const T = E.THREE;
  const S = SURG;
  const on = surg && tv < 50.6;
  const btn = $('sbtn'), ring = $('sring'), chip = $('schip');
  if (!on) {
    for (const m of [S.core, S.margin, S.pulse]) m.visible = false;
    for (const p of S.ports) p.grp.visible = false;
    hideLeaders('t-');
    btn.style.opacity = ring.style.opacity = chip.style.opacity = 0;
    document.querySelectorAll('.slabel').forEach((l) => (l.style.opacity = 0));
    hideLeaders('s-');
    return;
  }
  // botón: aparece, la mano lo pulsa (42,8 s) y se encoge hasta volverse la etiqueta de arriba
  const BX = 1120, BY = 560;
  const bin = easeOut(prog(tv, 41.5, 42.0));
  const press = tv < 42.8 ? 0 : Math.exp(-(tv - 42.8) / 0.08) * (tv < 43.0 ? 1 : 0);
  const gone = easeInOut(prog(tv, 43.05, 43.5));
  const bs = lerp(0.8, 1, easeBack(prog(tv, 41.5, 42.0))) * (1 - 0.08 * press) * lerp(1, 0.4, gone);
  btn.style.opacity = (bin * (1 - gone)).toFixed(3);
  btn.style.transform = `translate(${(BX - btn.offsetWidth / 2 + gone * 420).toFixed(1)}px, ${(BY - btn.offsetHeight / 2 - gone * 470).toFixed(1)}px) scale(${bs.toFixed(4)})`;
  const rp = prog(tv, 42.8, 43.5);
  ring.style.opacity = (rp > 0 && rp < 1 ? 0.9 * (1 - rp) : 0).toFixed(3);
  ring.style.transform = `translate(${BX}px, ${BY}px) scale(${lerp(0.6, 3.2, easeOut(rp)).toFixed(4)})`;
  chip.style.opacity = env(tv, 43.3, 50.0, 0.4, 0.4).toFixed(3);

  // la mano: llega desde abajo a la derecha, pinza y se va
  const pinch = $('pinch');
  const hv = env(tv, 41.8, 43.0, 0.3, 0.35);
  pinch.style.opacity = hv.toFixed(3);
  if (hv > 0) {
    const m = easeInOut(prog(tv, 41.8, 42.65));
    pinch.style.transform = `translate(${lerp(1640, BX + 60, m).toFixed(1)}px, ${lerp(900, BY + 40, m).toFixed(1)}px) scale(1.25)`;
    setPinch(easeInOut(prog(tv, 42.6, 42.8)) * (1 - easeInOut(prog(tv, 42.95, 43.15))));
  }

  // zona a tratar (44,3 s), márgenes (45,8 s) y abordaje (46,5 s)
  const zk = easeBack(prog(tv, 44.3, 44.8));
  const zv = clamp01(prog(tv, 44.3, 44.5)) * (1 - easeInOut(prog(tv, 50.1, 50.5)));
  S.core.visible = zv > 0;
  S.core.scale.setScalar(Math.max(0.001, zk) * (1 + 0.08 * Math.sin(tv * 6)));
  S.core.material.uniforms.uOpacity.value = zv;
  S.pulse.visible = zv > 0;
  const pp = (tv - 44.3) % 1.2 / 1.2;
  S.pulse.scale.setScalar(lerp(0.6, 2.2, pp));
  S.pulse.material.opacity = zv * (1 - pp) * 0.9;
  S.pulse.quaternion.copy(E.camera.quaternion);
  const mk = easeOut(prog(tv, 45.8, 46.5));
  S.margin.visible = mk > 0;
  S.margin.scale.setScalar(Math.max(0.001, lerp(0.5, 1, mk)));
  S.margin.material.uniforms.uOpacity.value = mk * (1 - easeInOut(prog(tv, 50.1, 50.5)));
  for (const p of S.ports) {
    const a = 46.5 + p.i * 0.25;
    const pk = easeBack(prog(tv, a, a + 0.4));
    p.grp.visible = pk > 0.001;
    p.grp.scale.setScalar(Math.max(0.001, pk));
    p.grp.quaternion.copy(E.camera.quaternion);
    const fade = 1 - easeInOut(prog(tv, 50.1, 50.5));
    p.ring.material.opacity = fade; p.dot.material.opacity = fade;
    trajectory('t-' + p.i, E.project(p.p), E.project(S.target), easeInOut(prog(tv, a + 0.15, a + 1.0)) * fade, tv);
  }

  // etiquetas con su línea guía (estilo de las ramas de luz)
  const anchors = [
    E.project(S.target),
    E.project(S.target.clone().add(new T.Vector3(0.032, 0, 0).applyQuaternion(E.camera.quaternion))),
    E.project(S.ports[2].p),
  ];
  [44.35, 45.85, 46.75].forEach((a, i) => {
    const el = $('sl' + i);
    const v = env(tv, a + 0.25, 50.05, 0.45, 0.4);
    el.style.opacity = v.toFixed(3);
    const [lx, ly] = SLABEL_POS[i];
    el.style.transform = `translate(${(lx + (1 - easeOut(prog(tv, a + 0.25, a + 0.7))) * 30).toFixed(1)}px, ${(ly - el.offsetHeight / 2).toFixed(1)}px)`;
    leader('s-' + i, anchors[i], { x: lx - 8, y: ly }, easeOut(prog(tv, a, a + 0.45)) * (1 - easeInOut(prog(tv, 50.05, 50.45))), '#FFB547', 'rgba(255,181,71,0.9)');
  });
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
  document.querySelectorAll('.otag').forEach((el) => (el.dataset.ml = el.querySelector('span').textContent));
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
