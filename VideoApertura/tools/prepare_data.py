"""
Prepara los datos reales del proyecto para la animación.

Lee la serie de TAC y las máscaras de segmentación que ya usa el prototipo
(Assets/StreamingAssets/CT) y genera en VideoApertura/assets:

  volume.bin + volume.json   volumen 3D (uint8) para el render volumétrico
  organ_<nombre>.bin         malla de cada órgano (marching cubes sobre su máscara)
  slices/axial_XXXX.jpg      cortes axiales a resolución completa, para los paneles 2D
  slices/mask_XXXX.png       máscara de colores de esos cortes
  slices/coronal.jpg, sagital.jpg
  captures/real_*.jpg        capturas reales del visor, recortadas

Uso (desde la raíz del repositorio):
  python3 VideoApertura/tools/prepare_data.py [carpeta_con_capturas]
"""

import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage
from skimage import measure

ROOT = Path(__file__).resolve().parents[2]
CT = ROOT / "Assets" / "StreamingAssets" / "CT"
OUT = ROOT / "VideoApertura" / "assets"
SERIES = "serie_cuerpo"

# Espaciado real de la serie (MedicalCtViewerLayout.cs, leído de las cabeceras DICOM).
SLICE_MM, PIXEL_MM = 3.27, 0.977
DOWN = 2  # 512 -> 256 en el plano axial para el volumen

# Colores de los órganos, tomados de cómo se ven en el visor.
ORGANS = {
    "higado": "#DE6240",
    "estomago": "#B46BD0",
    "pancreas": "#EE93BC",
    "vesicula": "#53B3E6",
}


def load_gray(path):
    return np.asarray(Image.open(path).convert("L"), dtype=np.uint8)


def build_volume():
    files = sorted((CT / SERIES / "axial").glob("*.jpg"))
    vol = np.stack([load_gray(f) for f in files])  # z, y, x  (z=0 cabeza)
    small = vol.reshape(vol.shape[0], 256, DOWN, 256, DOWN).mean(axis=(2, 4)).astype(np.uint8)

    # Quitar la camilla y el ruido: quedarse con el mayor componente conexo del cuerpo.
    body = small > 35
    body = ndimage.binary_opening(body, iterations=1)
    lab, n = ndimage.label(body)
    sizes = ndimage.sum(body, lab, range(1, n + 1))
    keep = lab == (int(np.argmax(sizes)) + 1)
    keep = ndimage.binary_closing(keep, iterations=2)
    keep = ndimage.binary_fill_holes(keep)
    keep = ndimage.binary_dilation(keep, iterations=2)
    small = np.where(keep, small, 0).astype(np.uint8)

    OUT.mkdir(parents=True, exist_ok=True)
    small.tofile(OUT / "volume.bin")
    meta = {
        "dims": [256, 256, int(small.shape[0])],  # x, y, z
        "spacing_mm": [PIXEL_MM * DOWN, PIXEL_MM * DOWN, SLICE_MM],
        "slices": len(files),
    }
    (OUT / "volume.json").write_text(json.dumps(meta))
    print("volumen", small.shape, f"{small.nbytes / 1e6:.1f} MB")
    return vol, keep


def build_organs(keep_mask):
    info = {}
    for name, color in ORGANS.items():
        files = sorted((CT / "masks" / name / "axial").glob("*.png"))
        m = np.stack([np.asarray(Image.open(f))[..., 3] > 0 for f in files])  # z,y,x 512
        m = m.reshape(m.shape[0], 256, 2, 256, 2).max(axis=(2, 4)).astype(np.float32)
        m = ndimage.gaussian_filter(m, sigma=(0.7, 1.4, 1.4))
        verts, faces, normals, _ = measure.marching_cubes(
            m, level=0.45, spacing=(SLICE_MM, PIXEL_MM * DOWN, PIXEL_MM * DOWN), step_size=1)
        # z,y,x en mm -> x,y,z
        verts = verts[:, ::-1].astype(np.float32)
        normals = -normals[:, ::-1].astype(np.float32)
        faces = faces.astype(np.uint32)
        header = np.array([len(verts), len(faces)], dtype=np.uint32)
        with open(OUT / f"organ_{name}.bin", "wb") as fh:
            fh.write(header.tobytes())
            fh.write(verts.tobytes())
            fh.write(normals.tobytes())
            fh.write(faces.tobytes())
        info[name] = {"color": color, "verts": len(verts), "faces": len(faces)}
        print(name, len(verts), "vértices", len(faces), "caras")
    (OUT / "organs.json").write_text(json.dumps(info))


def export_slices(vol):
    d = OUT / "slices"
    d.mkdir(parents=True, exist_ok=True)
    z0, z1 = 0, vol.shape[0]
    for z in range(z0, z1):
        Image.fromarray(vol[z]).save(d / f"axial_{z:04d}.jpg", quality=88)

    # Máscara combinada en color para los cortes con segmentación.
    rgb = {k: tuple(int(v[i:i + 2], 16) for i in (1, 3, 5)) for k, v in ORGANS.items()}
    masks = {k: sorted((CT / "masks" / k / "axial").glob("*.png")) for k in ORGANS}
    for z in range(vol.shape[0]):
        out = np.zeros((512, 512, 4), np.uint8)
        any_ = False
        for k, files in masks.items():
            a = np.asarray(Image.open(files[z]))[..., 3] > 0
            if a.any():
                any_ = True
                out[a] = (*rgb[k], 255)
        if any_:
            Image.fromarray(out).save(d / f"mask_{z:04d}.png")

    for plane in ("coronal", "sagital"):
        files = sorted((CT / SERIES / plane).glob("*.jpg"))
        Image.open(files[len(files) // 2]).convert("L").save(d / f"{plane}.jpg", quality=92)
        # una pequeña serie para animar el desplazamiento
        for i, f in enumerate(files[96:416:4]):
            Image.open(f).convert("L").save(d / f"{plane}_{i:03d}.jpg", quality=85)


def export_captures(src):
    if src is None:
        return
    d = OUT / "captures"
    d.mkdir(parents=True, exist_ok=True)
    # Capturas de pantalla del reproductor: se recorta solo el área del video.
    crops = {"real_plano.jpg": ("3.webp", (53, 40, 1917, 872)),
             "real_volumen.jpg": ("4.webp", (53, 40, 1917, 872))}
    for out, (name, box) in crops.items():
        p = Path(src) / name
        if p.exists():
            Image.open(p).convert("RGB").crop(box).save(d / out, quality=94)
            print("captura", out)


if __name__ == "__main__":
    vol, keep = build_volume()
    build_organs(keep)
    export_slices(vol)
    export_captures(sys.argv[1] if len(sys.argv) > 1 else None)
