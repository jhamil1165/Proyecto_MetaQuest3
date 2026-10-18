// Motor 3D del video: volumen del TAC por raymarching, órganos segmentados y planos de corte.
// Todo se dibuja en función del tiempo, sin estado oculto: render(t) siempre da el mismo
// fotograma, que es lo que permite exportar el video cuadro a cuadro.

import * as THREE from 'three';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';

export const W = 1920, H = 1080;
const ACCENT = new THREE.Color('#4C8DFF');

export async function createEngine(canvas, opts = {}) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, preserveDrawingBuffer: true });
  renderer.setPixelRatio(1);
  renderer.setSize(W, H, false);
  // Se trabaja en lineal dentro de los render targets y se codifica a sRGB al final.
  renderer.outputColorSpace = THREE.LinearSRGBColorSpace;
  renderer.toneMapping = THREE.NoToneMapping;

  const camera = new THREE.PerspectiveCamera(30, W / H, 0.05, 50);

  // ---------- datos ----------
  const meta = await (await fetch('../assets/volume.json')).json();
  const [NX, NY, NZ] = meta.dims;
  const raw = new Uint8Array(await (await fetch('../assets/volume.bin')).arrayBuffer());
  const volTex = new THREE.Data3DTexture(raw, NX, NY, NZ);
  volTex.format = THREE.RedFormat;
  volTex.type = THREE.UnsignedByteType;
  volTex.minFilter = volTex.magFilter = THREE.LinearFilter;
  volTex.unpackAlignment = 1;
  volTex.needsUpdate = true;

  // Tamaño real del volumen en metros (x: izq-der, y: pies-cabeza, z: espalda-frente).
  const size = new THREE.Vector3(
    NX * meta.spacing_mm[0] / 1000,
    NZ * meta.spacing_mm[2] / 1000,
    NY * meta.spacing_mm[1] / 1000);
  const half = size.clone().multiplyScalar(0.5);

  // Todo lo anatómico cuelga de este grupo, que es el que gira.
  const body = new THREE.Group();

  // ---------- escena principal (opaca) ----------
  const scene = new THREE.Scene();
  const pmrem = new THREE.PMREMGenerator(renderer);
  scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
  scene.add(body);

  const key = new THREE.DirectionalLight('#ffffff', 1.6);
  key.position.set(1.5, 2.5, 3);
  scene.add(key);
  const rim = new THREE.DirectionalLight('#9cc0ff', 1.2);
  rim.position.set(-2, 1, -2.5);
  scene.add(rim);
  scene.add(new THREE.HemisphereLight('#dfe8ff', '#1a1d22', 0.5));

  // ---------- órganos ----------
  const organInfo = await (await fetch('../assets/organs.json')).json();
  const organs = {};
  for (const [name, info] of Object.entries(organInfo)) {
    const buf = await (await fetch(`../assets/organ_${name}.bin`)).arrayBuffer();
    const [nv, nf] = new Uint32Array(buf, 0, 2);
    const pos = new Float32Array(buf, 8, nv * 3);
    const idx = new Uint32Array(buf, 8 + nv * 24, nf * 3);
    // mm en índices de la imagen -> metros en el espacio del cuerpo
    const p = new Float32Array(nv * 3);
    for (let i = 0; i < nv; i++) {
      p[i * 3] = pos[i * 3] / 1000 - half.x;          // columna -> x
      p[i * 3 + 1] = half.y - pos[i * 3 + 2] / 1000;  // corte (0 = cabeza) -> y
      p[i * 3 + 2] = half.z - pos[i * 3 + 1] / 1000;  // fila (0 = frente) -> z
    }
    const flipped = new Uint32Array(idx.length);
    for (let i = 0; i < idx.length; i += 3) {
      flipped[i] = idx[i]; flipped[i + 1] = idx[i + 2]; flipped[i + 2] = idx[i + 1];
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(p, 3));
    g.setIndex(new THREE.BufferAttribute(flipped, 1));
    g.computeVertexNormals();
    g.computeBoundingBox();
    const center = g.boundingBox.getCenter(new THREE.Vector3());
    g.translate(-center.x, -center.y, -center.z);

    const mat = new THREE.MeshPhysicalMaterial({
      color: info.color, roughness: 0.5, metalness: 0.0,
      clearcoat: 0.25, clearcoatRoughness: 0.45,
      sheen: 0.12, sheenColor: new THREE.Color('#ffffff'),
      transparent: true, opacity: 1,
    });
    const mesh = new THREE.Mesh(g, mat);
    const pivot = new THREE.Group();
    pivot.position.copy(center);
    pivot.userData.home = center.clone();
    pivot.add(mesh);
    body.add(pivot);
    organs[name] = { pivot, mesh, mat, color: new THREE.Color(info.color) };
  }

  // ---------- material de corte: muestra el TAC sobre cualquier plano ----------
  const sliceVert = /* glsl */`
    varying vec3 vLocal;
    uniform mat4 uBodyInv;
    void main() {
      vec4 w = modelMatrix * vec4(position, 1.0);
      vLocal = (uBodyInv * w).xyz;
      gl_Position = projectionMatrix * viewMatrix * w;
    }`;
  const sliceFrag = /* glsl */`
    precision highp sampler3D;
    uniform sampler3D uVol;
    uniform vec3 uHalf;
    uniform float uOpacity;
    uniform float uAdditive;
    varying vec3 vLocal;
    vec3 toTex(vec3 p) {
      return vec3((p.x + uHalf.x) / (2.0 * uHalf.x),
                  1.0 - (p.z + uHalf.z) / (2.0 * uHalf.z),
                  1.0 - (p.y + uHalf.y) / (2.0 * uHalf.y));
    }
    void main() {
      vec3 t = toTex(vLocal);
      if (any(lessThan(t, vec3(0.0))) || any(greaterThan(t, vec3(1.0)))) discard;
      float v = texture(uVol, t).r;
      float lin = pow(v, 2.2);
      if (uAdditive > 0.5) {
        float a = smoothstep(0.10, 0.55, v) * uOpacity;
        gl_FragColor = vec4(vec3(0.62, 0.72, 0.88) * lin * a * 0.55, 1.0);
      } else {
        if (v < 0.06 || uOpacity < 0.01) discard;
        gl_FragColor = vec4(vec3(lin) * uOpacity, 1.0);
      }
    }`;
  const bodyInv = new THREE.Matrix4();
  function sliceMaterial(additive) {
    return new THREE.ShaderMaterial({
      vertexShader: sliceVert, fragmentShader: sliceFrag,
      uniforms: {
        uVol: { value: volTex }, uHalf: { value: half }, uBodyInv: { value: bodyInv },
        uOpacity: { value: 1 }, uAdditive: { value: additive ? 1 : 0 },
      },
      side: THREE.DoubleSide,
      transparent: additive, depthWrite: !additive,
      blending: additive ? THREE.AdditiveBlending : THREE.NormalBlending,
    });
  }

  // Pila de cortes axiales para la secuencia "de cortes a volumen".
  const STACK = 56;
  const stack = [];
  for (let i = 0; i < STACK; i++) {
    const m = new THREE.Mesh(new THREE.PlaneGeometry(size.x, size.z), sliceMaterial(true));
    m.rotation.x = -Math.PI / 2;
    const y = half.y - (i + 0.5) / STACK * size.y;
    m.position.y = y;
    m.userData.homeY = y;
    m.renderOrder = 2;
    body.add(m);
    stack.push(m);
  }

  // Planos de corte (axial, coronal, sagital) con su marco azul.
  function makeCutPlane(kind) {
    const group = new THREE.Group();
    let w, h;
    if (kind === 'axial') { w = size.x * 1.25; h = size.z * 1.3; group.rotation.x = -Math.PI / 2; }
    if (kind === 'coronal') { w = size.x * 1.25; h = size.y * 1.08; }
    if (kind === 'sagital') { w = size.z * 1.35; h = size.y * 1.08; group.rotation.y = Math.PI / 2; }
    const face = new THREE.Mesh(new THREE.PlaneGeometry(w, h), sliceMaterial(false));
    group.add(face);
    body.add(group);

    // marco + relleno, dibujados encima del volumen
    const ov = new THREE.Group();
    const shape = new THREE.Shape();
    const b = 0.012;
    shape.moveTo(-w / 2, -h / 2); shape.lineTo(w / 2, -h / 2); shape.lineTo(w / 2, h / 2); shape.lineTo(-w / 2, h / 2);
    const hole = new THREE.Path();
    hole.moveTo(-w / 2 + b, -h / 2 + b); hole.lineTo(-w / 2 + b, h / 2 - b); hole.lineTo(w / 2 - b, h / 2 - b); hole.lineTo(w / 2 - b, -h / 2 + b);
    shape.holes.push(hole);
    const frameMat = new THREE.MeshBasicMaterial({ color: ACCENT, transparent: true, depthTest: false, side: THREE.DoubleSide, toneMapped: false });
    const fillMat = new THREE.MeshBasicMaterial({ color: ACCENT, transparent: true, opacity: 0.16, depthTest: false, depthWrite: false, side: THREE.DoubleSide, toneMapped: false });
    ov.add(new THREE.Mesh(new THREE.ShapeGeometry(shape), frameMat));
    ov.add(new THREE.Mesh(new THREE.PlaneGeometry(w, h), fillMat));
    overlay.add(ov);
    return { kind, group, face, ov, frameMat, fillMat, w, h };
  }
  const overlay = new THREE.Scene();
  const planes = { axial: makeCutPlane('axial'), coronal: makeCutPlane('coronal'), sagital: makeCutPlane('sagital') };

  // Corte suelto del principio (la imagen que sale de la "pantalla").
  const looseTex = new THREE.Texture();
  looseTex.colorSpace = THREE.SRGBColorSpace;
  const looseKey = { value: 0 }, looseOpacity = { value: 1 }, looseR = { value: 0.5 };
  const loose = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), new THREE.ShaderMaterial({
    uniforms: { map: { value: looseTex }, uKey: looseKey, uOpacity: looseOpacity, uVol: { value: volTex }, uR: looseR },
    vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
    // uKey: 0 = imagen completa (como en la pantalla), 1 = solo el cuerpo, sin el negro del aire
    // la transparencia sale del volumen ya limpio (sin aire ni camilla) en ese mismo corte
    fragmentShader: `precision highp sampler3D; uniform sampler2D map; uniform sampler3D uVol; uniform float uKey, uOpacity, uR; varying vec2 vUv;
      void main(){ vec3 c = texture2D(map, vUv).rgb; float body = texture(uVol, vec3(vUv.x, 1.0 - vUv.y, uR)).r;
        float a = mix(1.0, smoothstep(0.06, 0.16, body), uKey); gl_FragColor = vec4(c, a * uOpacity); }`,
    transparent: true, side: THREE.DoubleSide, depthWrite: false, depthTest: false,
  }));
  loose.renderOrder = 5;
  overlay.add(loose);

  // Sombra suave en el "suelo", para situar el cuerpo en el espacio.
  const shadowCanvas = document.createElement('canvas');
  shadowCanvas.width = shadowCanvas.height = 256;
  {
    const c = shadowCanvas.getContext('2d');
    const gr = c.createRadialGradient(128, 128, 0, 128, 128, 128);
    gr.addColorStop(0, 'rgba(0,0,0,0.55)'); gr.addColorStop(1, 'rgba(0,0,0,0)');
    c.fillStyle = gr; c.fillRect(0, 0, 256, 256);
  }
  const shadow = new THREE.Mesh(new THREE.PlaneGeometry(1.1, 0.8),
    new THREE.MeshBasicMaterial({ map: new THREE.CanvasTexture(shadowCanvas), transparent: true, depthWrite: false, toneMapped: false }));
  shadow.rotation.x = -Math.PI / 2;
  shadow.position.y = -half.y - 0.08;
  scene.add(shadow);

  // Plataforma holográfica bajo el cuerpo, como el disco que pone el visor bajo cada órgano:
  // anillos que giran, marcas y una onda que se expande. Suma luz (aditivo).
  const platU = { uTime: { value: 0 }, uOpacity: { value: 0 }, uPulse: { value: 0 } };
  const platform = new THREE.Mesh(new THREE.PlaneGeometry(1.5, 1.5), new THREE.ShaderMaterial({
    uniforms: platU,
    vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
    fragmentShader: /* glsl */`
      uniform float uTime, uOpacity, uPulse; varying vec2 vUv;
      float ring(float r, float c, float w){ return exp(-pow((r - c) / w, 2.0)); }
      void main(){
        vec2 p = (vUv - 0.5) * 2.0; float r = length(p); float a = atan(p.y, p.x) / 6.28318 + 0.5;
        float v = ring(r, 0.62, 0.010) + ring(r, 0.56, 0.004) * 0.45 + ring(r, 0.38, 0.003) * 0.25
                + smoothstep(0.64, 0.0, r) * 0.07;
        v += step(0.55, fract(a * 96.0 + uTime * 0.6)) * ring(r, 0.70, 0.010) * 0.55;
        v += step(0.62, fract(a * 3.0 - uTime * 0.05)) * ring(r, 0.78, 0.005) * 0.9;
        v += step(0.80, fract(a * 2.0 + uTime * 0.03)) * ring(r, 0.86, 0.003) * 0.6;
        float w = fract(uTime * 0.28);
        v += ring(r, w * 0.9, 0.025) * (1.0 - w) * 0.45;
        v += uPulse * ring(r, 0.62, 0.05) * 0.8;
        v *= smoothstep(1.0, 0.9, r);
        gl_FragColor = vec4(vec3(0.32, 0.58, 1.0) * v * uOpacity, 1.0);
      }`,
    transparent: true, depthWrite: false, side: THREE.DoubleSide, blending: THREE.AdditiveBlending, toneMapped: false,
  }));
  platform.rotation.x = -Math.PI / 2;
  platform.position.y = -half.y - 0.03;
  platform.renderOrder = 1;
  scene.add(platform);

  // Polvo de luz flotando alrededor: da profundidad y vida al fondo negro.
  const NP = 520;
  const pPos = new Float32Array(NP * 3), pSeed = new Float32Array(NP);
  let seed = 12345;
  const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
  for (let i = 0; i < NP; i++) {
    pPos[i * 3] = (rnd() - 0.5) * 3.2;
    pPos[i * 3 + 1] = (rnd() - 0.5) * 2.0;
    pPos[i * 3 + 2] = (rnd() - 0.5) * 3.2;
    pSeed[i] = rnd();
  }
  const pGeo = new THREE.BufferGeometry();
  pGeo.setAttribute('position', new THREE.BufferAttribute(pPos, 3));
  pGeo.setAttribute('seed', new THREE.BufferAttribute(pSeed, 1));
  const dustU = { uTime: { value: 0 }, uOpacity: { value: 0 } };
  const dust = new THREE.Points(pGeo, new THREE.ShaderMaterial({
    uniforms: dustU,
    vertexShader: /* glsl */`
      attribute float seed; uniform float uTime; varying float vA;
      void main(){
        // suben despacio y, al llegar arriba, reaparecen abajo (con fundido en los bordes)
        vec3 p = position;
        p.y = mod(position.y + 1.0 + uTime * (0.012 + seed * 0.02), 2.0) - 1.0;
        p.x += sin(uTime * 0.21 + seed * 40.0) * 0.04;
        p.z += cos(uTime * 0.17 + seed * 23.0) * 0.04;
        vec4 mv = modelViewMatrix * vec4(p, 1.0);
        gl_Position = projectionMatrix * mv;
        float s = 0.6 + seed * seed * 3.2;
        gl_PointSize = min(s * 14.0 / -mv.z, 40.0);
        vA = (0.35 + 0.65 * (0.5 + 0.5 * sin(uTime * (0.6 + seed) + seed * 50.0))) * (0.25 + seed * 0.75);
        vA *= smoothstep(1.0, 0.75, abs(p.y)) * smoothstep(0.25, 0.8, -mv.z);
      }`,
    fragmentShader: /* glsl */`
      uniform float uOpacity; varying float vA;
      void main(){ float d = length(gl_PointCoord - 0.5); float a = smoothstep(0.5, 0.0, d); a *= a;
        gl_FragColor = vec4(vec3(0.55, 0.72, 1.0) * a * vA * uOpacity * 0.55, 1.0); }`,
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
  }));
  dust.renderOrder = 1;
  scene.add(dust);

  // ---------- volumen por raymarching ----------
  const rtMain = new THREE.WebGLRenderTarget(W, H, { samples: 4, type: THREE.HalfFloatType });
  rtMain.depthTexture = new THREE.DepthTexture(W, H);
  const rtPlain = new THREE.WebGLRenderTarget(W, H, { type: THREE.HalfFloatType });
  rtPlain.depthTexture = new THREE.DepthTexture(W, H);
  const VS = opts.volumeScale || 0.6;
  const rtVol = new THREE.WebGLRenderTarget(Math.round(W * VS), Math.round(H * VS), { type: THREE.HalfFloatType });

  const volMat = new THREE.ShaderMaterial({
    uniforms: {
      uVol: { value: volTex }, uHalf: { value: half },
      uDepth: { value: rtPlain.depthTexture },
      uProjInv: { value: new THREE.Matrix4() }, uCamWorld: { value: new THREE.Matrix4() },
      uBodyInv: { value: bodyInv },
      uCamPos: { value: new THREE.Vector3() },
      uOpacity: { value: 0 }, uDensity: { value: 1 },
      uBone: { value: 0 },
      uClipN: { value: new THREE.Vector3(0, 1, 0) }, uClipD: { value: 10 },
    },
    vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }`,
    fragmentShader: /* glsl */`
      precision highp float; precision highp sampler3D;
      uniform sampler3D uVol; uniform sampler2D uDepth;
      uniform vec3 uHalf; uniform mat4 uProjInv, uCamWorld, uBodyInv; uniform vec3 uCamPos;
      uniform float uOpacity, uDensity, uBone; uniform vec3 uClipN; uniform float uClipD;
      varying vec2 vUv;
      vec3 toTex(vec3 p) {
        return vec3((p.x + uHalf.x) / (2.0 * uHalf.x), 1.0 - (p.z + uHalf.z) / (2.0 * uHalf.z), 1.0 - (p.y + uHalf.y) / (2.0 * uHalf.y));
      }
      float hash(vec2 p){ return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
      void main() {
        if (uOpacity < 0.003) { gl_FragColor = vec4(0.0); return; }
        vec4 ndcFar = vec4(vUv * 2.0 - 1.0, 1.0, 1.0);
        vec4 vf = uProjInv * ndcFar; vf /= vf.w;
        vec3 wdir = normalize((uCamWorld * vec4(vf.xyz, 0.0)).xyz);
        // distancia hasta lo opaco de la escena (órganos, cortes)
        float d = texture2D(uDepth, vUv).r;
        vec4 vp = uProjInv * vec4(vUv * 2.0 - 1.0, d * 2.0 - 1.0, 1.0); vp /= vp.w;
        vec3 wp = (uCamWorld * vec4(vp.xyz, 1.0)).xyz;
        float tScene = d >= 0.99999 ? 1e6 : dot(wp - uCamPos, wdir);

        vec3 ro = (uBodyInv * vec4(uCamPos, 1.0)).xyz;
        vec3 rd = (uBodyInv * vec4(wdir, 0.0)).xyz;
        vec3 inv = 1.0 / rd;
        vec3 t0 = (-uHalf - ro) * inv, t1 = (uHalf - ro) * inv;
        vec3 tmin = min(t0, t1), tmax = max(t0, t1);
        float tn = max(max(tmin.x, tmin.y), tmin.z), tf = min(min(tmax.x, tmax.y), tmax.z);
        tn = max(tn, 0.0); tf = min(tf, tScene);
        if (tf <= tn) { gl_FragColor = vec4(0.0); return; }
        float dt = 0.0042;
        float t = tn + dt * hash(gl_FragCoord.xy);
        vec4 acc = vec4(0.0);
        for (int i = 0; i < 420; i++) {
          if (t > tf || acc.a > 0.97) break;
          vec3 p = ro + rd * t;
          if (dot(p, uClipN) <= uClipD) {
            float v = texture(uVol, toTex(p)).r;
            // tejido: brillo tipo radiografía; hueso: lo más denso, más blanco y opaco
            float soft = smoothstep(0.16, 0.62, v) * (1.0 - uBone);
            float bone = smoothstep(0.80, 0.97, v);
            float a = (soft * 0.030 + bone * mix(0.05, 0.22, uBone)) * uDensity;
            vec3 c = mix(vec3(0.50, 0.58, 0.70), vec3(0.96, 0.97, 1.0), smoothstep(0.55, 0.95, v));
            acc.rgb += (1.0 - acc.a) * c * a;
            acc.a += (1.0 - acc.a) * a;
          }
          t += dt;
        }
        gl_FragColor = acc * uOpacity;
      }`,
    depthTest: false, depthWrite: false,
  });
  const quadGeo = new THREE.PlaneGeometry(2, 2);
  const volScene = new THREE.Scene();
  volScene.add(new THREE.Mesh(quadGeo, volMat));

  // composición final: escena + volumen (premultiplicado)
  // brillo: se extrae lo más luminoso a 1/4 de resolución, se difumina y se suma al final
  const BW = W / 4, BH = H / 4;
  const rtB1 = new THREE.WebGLRenderTarget(BW, BH, { type: THREE.HalfFloatType });
  const rtB2 = new THREE.WebGLRenderTarget(BW, BH, { type: THREE.HalfFloatType });
  const bloomU = { uStrength: { value: 0.7 }, uThreshold: { value: 0.3 } };
  const brightMat = new THREE.ShaderMaterial({
    uniforms: { uTex: { value: null }, uPx: { value: new THREE.Vector2(1 / W, 1 / H) }, uThreshold: bloomU.uThreshold },
    vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }`,
    fragmentShader: `uniform sampler2D uTex; uniform vec2 uPx; uniform float uThreshold; varying vec2 vUv;
      void main(){ vec3 c = vec3(0.0);
        for (int i = -1; i <= 2; i++) for (int j = -1; j <= 2; j++) c += texture2D(uTex, vUv + vec2(float(i) - 0.5, float(j) - 0.5) * uPx).rgb;
        c /= 16.0; float l = max(c.r, max(c.g, c.b));
        gl_FragColor = vec4(c * smoothstep(uThreshold, uThreshold + 0.35, l), 1.0); }`,
    depthTest: false, depthWrite: false,
  });
  const blurMat = new THREE.ShaderMaterial({
    uniforms: { uTex: { value: null }, uDir: { value: new THREE.Vector2() } },
    vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }`,
    fragmentShader: `uniform sampler2D uTex; uniform vec2 uDir; varying vec2 vUv;
      void main(){ vec3 c = texture2D(uTex, vUv).rgb * 0.2270;
        c += (texture2D(uTex, vUv + uDir * 1.3846).rgb + texture2D(uTex, vUv - uDir * 1.3846).rgb) * 0.3162;
        c += (texture2D(uTex, vUv + uDir * 3.2308).rgb + texture2D(uTex, vUv - uDir * 3.2308).rgb) * 0.0703;
        gl_FragColor = vec4(c, 1.0); }`,
    depthTest: false, depthWrite: false,
  });
  const copyMat = new THREE.ShaderMaterial({
    uniforms: { uTex: { value: null }, uBloom: { value: rtB1.texture }, uStrength: bloomU.uStrength },
    vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }`,
    fragmentShader: `uniform sampler2D uTex, uBloom; uniform float uStrength; varying vec2 vUv;
      vec3 toSRGB(vec3 c){ c = clamp(c, 0.0, 1.0); return mix(c * 12.92, 1.055 * pow(c, vec3(1.0/2.4)) - 0.055, step(0.0031308, c)); }
      float hash(vec2 p){ return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
      void main(){ vec3 lin = texture2D(uTex, vUv).rgb + texture2D(uBloom, vUv).rgb * uStrength;
        vec3 c = toSRGB(lin); c += (hash(gl_FragCoord.xy) - 0.5) / 255.0; gl_FragColor = vec4(c, 1.0); }`,
    depthTest: false, depthWrite: false,
  });
  const volCompMat = new THREE.ShaderMaterial({
    uniforms: { uTex: { value: rtVol.texture } },
    vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }`,
    fragmentShader: `uniform sampler2D uTex; varying vec2 vUv; void main(){ gl_FragColor = texture2D(uTex, vUv); }`,
    transparent: true, depthTest: false, depthWrite: false,
    blending: THREE.CustomBlending, blendSrc: THREE.OneFactor, blendDst: THREE.OneMinusSrcAlphaFactor,
  });
  const copyScene = new THREE.Scene(); copyScene.add(new THREE.Mesh(quadGeo, copyMat));
  const brightScene = new THREE.Scene(); brightScene.add(new THREE.Mesh(quadGeo, brightMat));
  const blurScene = new THREE.Scene(); blurScene.add(new THREE.Mesh(quadGeo, blurMat));
  const compScene = new THREE.Scene(); compScene.add(new THREE.Mesh(quadGeo, volCompMat));
  const orthoCam = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);

  // fondo
  const bgCanvas = document.createElement('canvas');
  bgCanvas.width = 960; bgCanvas.height = 540;
  {
    const c = bgCanvas.getContext('2d');
    c.fillStyle = '#05070A'; c.fillRect(0, 0, 960, 540);
    const gr = c.createRadialGradient(600, 250, 20, 600, 270, 620);
    gr.addColorStop(0, 'rgba(22,40,74,0.75)'); gr.addColorStop(0.5, 'rgba(10,18,34,0.35)'); gr.addColorStop(1, 'rgba(0,0,0,0)');
    c.fillStyle = gr; c.fillRect(0, 0, 960, 540);
  }
  const bgTex = new THREE.CanvasTexture(bgCanvas);
  bgTex.colorSpace = THREE.SRGBColorSpace;
  scene.background = bgTex;

  // ---------- API ----------
  const tmpV = new THREE.Vector3();
  function project(v3) {
    tmpV.copy(v3).project(camera);
    return { x: (tmpV.x * 0.5 + 0.5) * W, y: (-tmpV.y * 0.5 + 0.5) * H };
  }

  function render() {
    body.updateMatrixWorld(true);
    bodyInv.copy(body.matrixWorld).invert();
    camera.updateMatrixWorld(true);

    // las capas superpuestas siguen al cuerpo
    for (const p of Object.values(planes)) {
      p.group.updateMatrixWorld(true);
      p.ov.matrixAutoUpdate = false;
      p.ov.matrix.copy(p.group.matrixWorld);
      p.ov.matrixWorldNeedsUpdate = true;
    }

    const volumeOn = volMat.uniforms.uOpacity.value > 0.003;
    // 1) escena opaca con profundidad (sin MSAA, para leer su profundidad)
    if (volumeOn) {
      renderer.setRenderTarget(rtPlain);
      renderer.render(scene, camera);
      volMat.uniforms.uProjInv.value.copy(camera.projectionMatrixInverse);
      volMat.uniforms.uCamWorld.value.copy(camera.matrixWorld);
      volMat.uniforms.uCamPos.value.setFromMatrixPosition(camera.matrixWorld);
      renderer.setRenderTarget(rtVol);
      renderer.setClearColor(0x000000, 0);
      renderer.clear();
      renderer.render(volScene, orthoCam);
    }
    // 2) escena con antialiasing, volumen encima y capas superpuestas, todo en lineal
    renderer.setRenderTarget(rtMain);
    renderer.setClearColor(0x000000, 1);
    renderer.render(scene, camera);
    renderer.autoClear = false;
    if (volumeOn) renderer.render(compScene, orthoCam);
    renderer.render(overlay, camera);
    renderer.autoClear = true;
    // 3) brillo: lo luminoso, difuminado dos veces
    brightMat.uniforms.uTex.value = rtMain.texture;
    renderer.setRenderTarget(rtB1); renderer.render(brightScene, orthoCam);
    for (let k = 0; k < 2; k++) {
      blurMat.uniforms.uTex.value = rtB1.texture; blurMat.uniforms.uDir.value.set((1 + k) / BW, 0);
      renderer.setRenderTarget(rtB2); renderer.render(blurScene, orthoCam);
      blurMat.uniforms.uTex.value = rtB2.texture; blurMat.uniforms.uDir.value.set(0, (1 + k) / BH);
      renderer.setRenderTarget(rtB1); renderer.render(blurScene, orthoCam);
    }
    // 4) a pantalla, codificado a sRGB
    renderer.setRenderTarget(null);
    copyMat.uniforms.uTex.value = rtMain.texture;
    renderer.render(copyScene, orthoCam);
  }

  return {
    THREE, renderer, camera, scene, overlay, body, organs, stack, planes, loose, looseTex, looseKey, looseOpacity, looseR, shadow,
    platform, platU, dust, dustU, bloom: bloomU,
    volMat, size, half, dims: { NX, NY, NZ }, raw, project, render,
  };
}
