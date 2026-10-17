// Exporta el video cuadro a cuadro: abre la animación en Chromium sin ventana, dibuja cada
// instante con window.__render(t), captura la pantalla y se la pasa a ffmpeg.
//
//   node tools/render.mjs                         -> out/video_apertura.mp4 (60 s, 30 fps)
//   node tools/render.mjs --stills 3,10,22.5      -> out/stills/t_03.00.png ...
//   node tools/render.mjs --from 17 --to 25 --out out/tramo.mp4
//
// ffmpeg: usa $FFMPEG, o el de `pip install imageio-ffmpeg`, o el del sistema.

import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright-core';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = Object.fromEntries(process.argv.slice(2).reduce((acc, a, i, all) => {
  if (a.startsWith('--')) acc.push([a.slice(2), all[i + 1] && !all[i + 1].startsWith('--') ? all[i + 1] : true]);
  return acc;
}, []));
const FPS = +(args.fps || 30);
const FROM = +(args.from || 0);
let TO = args.to ? +args.to : null; // por defecto, la duración que declara la página

// ---------- servidor estático ----------
const TYPES = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json',
  '.jpg': 'image/jpeg', '.png': 'image/png', '.woff2': 'font/woff2', '.bin': 'application/octet-stream' };
const server = http.createServer((req, res) => {
  const p = path.join(ROOT, decodeURIComponent(new URL(req.url, 'http://x').pathname));
  if (!p.startsWith(ROOT) || !fs.existsSync(p) || fs.statSync(p).isDirectory()) { res.writeHead(404); return res.end(); }
  res.writeHead(200, { 'Content-Type': TYPES[path.extname(p)] || 'application/octet-stream' });
  fs.createReadStream(p).pipe(res);
});
await new Promise((r) => server.listen(0, '127.0.0.1', r));
const url = `http://127.0.0.1:${server.address().port}/src/index.html?capture=1`;

// ---------- navegador ----------
function findChromium() {
  if (process.env.CHROMIUM) return process.env.CHROMIUM;
  const base = process.env.PLAYWRIGHT_BROWSERS_PATH || '/opt/pw-browsers';
  for (const d of fs.existsSync(base) ? fs.readdirSync(base) : []) {
    const c = path.join(base, d, 'chrome-linux', 'chrome');
    if (d.startsWith('chromium-') && fs.existsSync(c)) return c;
  }
  return undefined;
}
// En un servidor Linux sin GPU se dibuja por software (SwiftShader). En una PC normal se usa
// la GPU y el Chrome instalado, salvo que se indique otro con CHROMIUM=ruta.
const exe = findChromium();
const software = process.platform === 'linux' && !args.gpu;
const browser = await chromium.launch({
  executablePath: exe,
  channel: exe ? undefined : 'chrome',
  args: [...(software ? ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : []),
    '--ignore-gpu-blocklist', '--force-color-profile=srgb'],
});
const page = await browser.newPage({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 1 });
// los 404 son las máscaras de cortes sin órganos: es normal que falten
page.on('console', (m) => { if ((m.type() === 'error' || m.type() === 'warning') && !m.text().includes('404')) console.log('[página]', m.text()); });
page.on('pageerror', (e) => console.log('[error]', e.message));
await page.goto(url);
await page.waitForFunction(() => window.__ready === true, null, { timeout: 180000 });
const stage = await page.$('#stage');
if (TO === null) TO = await page.evaluate(() => window.__duration || 60);

async function frame(t) {
  await page.evaluate((tt) => window.__render(tt), t);
  return stage.screenshot({ type: 'png' });
}

if (args.stills) {
  const dir = path.join(ROOT, 'out', 'stills');
  fs.mkdirSync(dir, { recursive: true });
  for (const s of String(args.stills).split(',').map(Number)) {
    const t0 = Date.now();
    fs.writeFileSync(path.join(dir, `t_${s.toFixed(2).padStart(5, '0')}.png`), await frame(s));
    console.log(`t=${s}s  ${Date.now() - t0} ms`);
  }
} else {
  let ffmpeg = process.env.FFMPEG;
  if (!ffmpeg) {
    for (const py of ['python3', 'python']) {
      try { ffmpeg = execSync(`${py} -c "import imageio_ffmpeg as f; print(f.get_ffmpeg_exe())"`, { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim(); break; }
      catch { /* probar el siguiente */ }
    }
    ffmpeg ||= 'ffmpeg';
  }
  const out = path.resolve(args.out || path.join(ROOT, 'out', 'video_apertura.mp4'));
  fs.mkdirSync(path.dirname(out), { recursive: true });
  const ff = spawn(ffmpeg, ['-y', '-f', 'image2pipe', '-framerate', String(FPS), '-i', '-',
    '-c:v', 'libx264', '-preset', 'slow', '-crf', '15', '-pix_fmt', 'yuv420p',
    '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709',
    '-movflags', '+faststart', out], { stdio: ['pipe', 'inherit', 'inherit'] });
  const n = Math.round((TO - FROM) * FPS);
  const t0 = Date.now();
  for (let i = 0; i < n; i++) {
    const png = await frame(FROM + i / FPS);
    if (!ff.stdin.write(png)) await new Promise((r) => ff.stdin.once('drain', r));
    if (i % 30 === 0) {
      const el = (Date.now() - t0) / 1000;
      console.log(`cuadro ${i}/${n}  ${(FROM + i / FPS).toFixed(1)} s  · ${el.toFixed(0)} s transcurridos, faltan ~${((el / (i + 1)) * (n - i - 1) / 60).toFixed(1)} min`);
    }
  }
  ff.stdin.end();
  await new Promise((r) => ff.on('close', r));
  console.log('listo:', out);
}

await browser.close();
server.close();
