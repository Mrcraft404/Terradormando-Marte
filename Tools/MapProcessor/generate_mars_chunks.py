#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
generate_mars_chunks.py
=======================

Pipeline OFFLINE que prepara el terreno de las 6 zonas de aterrizaje para Unity a partir de datos
de Marte (MOLA, CTX, HiRISE, CRISM) o, si no hay red o una fuente falla, de una simulación
geológica fractal. Cada zona se escribe en:

    Assets/StreamingAssets/MarsMap/<zona>/
        manifest.json                 descripción de la zona, fuentes usadas y clima
        chunk_X_Y.heightmap           alturas uint16 little-endian, 256×256, fila 0 = SUR
        chunk_X_Y.micro               micro-relieve HiRISE int8, 256×256, fila 0 = SUR
        chunk_X_Y.rocks               densidad de rocas/cráteres uint8, 128×128, fila 0 = SUR
        chunk_X_Y_albedo.png          textura CTX en gris, 256×256 (fila superior = NORTE)
        chunk_X_Y_minerals.json       minerales (CRISM), entorno, vetas extraíbles

X crece hacia el ESTE y Y hacia el NORTE. Los chunks vecinos comparten la fila/columna del
borde (256 vértices = 255 intervalos), así no hay grietas al unirlos.

FUENTES (todas opcionales; cada capa cae a simulación por separado si falla):
  • Elevación  : MOLA MEGDR 463 m (GeoTIFF de USGS Astrogeology), lectura por ventana con
                 rasterio /vsicurl/ (no descarga los 2 GB enteros). Se le añade detalle fractal
                 por debajo de la resolución de MOLA.
  • Textura    : mosaico CTX 5 m (Murray Lab / Caltech) servido por NASA Mars Trek (WMTS).
  • Micro      : DTM de HiRISE (hirise-pds.lpl.arizona.edu/PDS) localizado con la API ODE.
                 Solo si un DTM cubre la zona; si no, micro-relieve fractal.
  • Minerales  : la API ODE de CRISM indica DÓNDE hay observaciones; las concentraciones son
                 índices de juego basados en la geología publicada de cada zona (sacar
                 abundancias reales exigiría procesar los cubos espectrales, gigabytes por escena).
  • Clima      : perfiles medios por zona basados en el Mars Climate Database (NASA Ames MCMC)
                 y en la dosis medida por Curiosity RAD, ajustados por altitud. Los catálogos
                 de NASA Ames y PDS-PPI solo se comprueban (son netCDF/tablas muy pesadas).

Uso rápido (ver README.md):
    python generate_mars_chunks.py --mode synthetic            # sin internet, 6 zonas
    python generate_mars_chunks.py --zones jezero --mode auto  # intenta datos reales
"""

import argparse
import datetime
import io
import json
import math
import re
import shutil
import sys
import time
import zlib
from pathlib import Path
from xml.etree import ElementTree

import numpy as np
from PIL import Image, ImageOps

try:
    import requests
except ImportError:  # el modo synthetic funciona sin requests
    requests = None

try:
    import rasterio
    from rasterio.crs import CRS
    from rasterio.enums import Resampling
    from rasterio.warp import transform_bounds
    from rasterio.windows import from_bounds
except ImportError:  # sin rasterio: MOLA y HiRISE pasan a simulación
    rasterio = None

# ---------------------------------------------------------------------------
# Constantes físicas y endpoints
# ---------------------------------------------------------------------------

MARS_RADIUS_M = 3396190.0          # radio de referencia (esfera IAU usada por MOLA/USGS)
MARS_GRAVITY = 3.721               # m/s² (0,38 g)
DATUM_PRESSURE_PA = 610.0          # presión media en el datum (punto triple del agua)
PRESSURE_SCALE_HEIGHT_M = 11100.0  # altura de escala atmosférica
RAD_GALE_MGY_PER_DAY = 0.21        # dosis absorbida media medida por Curiosity RAD en Gale
GALE_ELEVATION_M = -4500.0
LAPSE_RATE_K_PER_KM = 2.5          # gradiente térmico medio aproximado

DEFAULT_MOLA_URL = "https://planetarymaps.usgs.gov/mosaic/Mars_MGS_MOLA_DEM_mosaic_global_463m.tif"
DEFAULT_CTX_CAPS = ("https://trek.nasa.gov/tiles/Mars/EQ/CTX_beta01_uncontrolled_5m_Caltech/"
                    "1.0.0/WMTSCapabilities.xml")
ODE_URL = "https://oderest.rsl.wustl.edu/live2/"
CLIMATE_REFERENCES = {
    "nasa_ames_mcmc": "https://data.nas.nasa.gov/mcmcref/",
    "curiosity_rad": "https://pds-ppi.igpp.ucla.edu/data/MSL-M-RAD-3-RDR-V1.0/",
}

VERTICES_PER_CHUNK = 256
MINERALS = ("clays", "sulfates", "ironOxides", "subsurfaceIce")

# ---------------------------------------------------------------------------
# Zonas (los zoneId coinciden con los LandingZoneData de Unity)
#   datum      : cota aproximada del suelo de la zona (m sobre el datum MOLA)
#   detail_m   : desviación típica del detalle fractal por debajo de la resolución MOLA
#   micro_m    : amplitud del micro-relieve simulado (si no hay DTM HiRISE)
#   craters    : cráteres ≥ diámetro mínimo por km² (superficies jóvenes → pocos)
#   rock_base  : densidad base de rocas 0..1
#   minerals   : índices 0..1 basados en la geología publicada (no abundancias medidas)
# ---------------------------------------------------------------------------

ZONES = {
    "elysium_planitia": dict(
        name="Elysium Planitia", lat=4.5, lon=135.6, datum=-2600.0, macro="elysium",
        detail_m=3.0, micro_m=0.3, craters=4.0, rock_base=0.15,
        minerals=dict(clays=0.10, sulfates=0.12, ironOxides=0.45, subsurfaceIce=0.10),
        climate=dict(meanTemperatureC=-60, minTemperatureC=-95, maxTemperatureC=-5,
                     meanWindSpeed=5, gustWindSpeed=20, dustStormChancePerSol=0.01)),
    "acidalia_planitia": dict(
        name="Acidalia Planitia", lat=46.7, lon=337.8, datum=-4000.0, macro="acidalia",
        detail_m=5.0, micro_m=0.4, craters=1.0, rock_base=0.25,
        minerals=dict(clays=0.25, sulfates=0.15, ironOxides=0.40, subsurfaceIce=0.35),
        climate=dict(meanTemperatureC=-65, minTemperatureC=-105, maxTemperatureC=-20,
                     meanWindSpeed=7, gustWindSpeed=26, dustStormChancePerSol=0.08)),
    "jezero": dict(
        name="Cráter Jezero", lat=18.38, lon=77.58, datum=-2600.0, macro="jezero",
        detail_m=10.0, micro_m=0.6, craters=2.0, rock_base=0.35,
        minerals=dict(clays=0.60, sulfates=0.15, ironOxides=0.45, subsurfaceIce=0.05),
        climate=dict(meanTemperatureC=-58, minTemperatureC=-95, maxTemperatureC=-5,
                     meanWindSpeed=5, gustWindSpeed=18, dustStormChancePerSol=0.02)),
    "gale": dict(
        name="Cráter Gale", lat=-5.4, lon=137.8, datum=-4500.0, macro="gale",
        detail_m=10.0, micro_m=0.8, craters=1.5, rock_base=0.50,
        minerals=dict(clays=0.55, sulfates=0.60, ironOxides=0.60, subsurfaceIce=0.05),
        climate=dict(meanTemperatureC=-50, minTemperatureC=-85, maxTemperatureC=5,
                     meanWindSpeed=4, gustWindSpeed=15, dustStormChancePerSol=0.03)),
    "utopia_planitia": dict(
        name="Utopia Planitia", lat=48.0, lon=134.3, datum=-4200.0, macro="utopia",
        detail_m=4.0, micro_m=0.5, craters=1.0, rock_base=0.55,
        minerals=dict(clays=0.20, sulfates=0.10, ironOxides=0.40, subsurfaceIce=0.85),
        climate=dict(meanTemperatureC=-70, minTemperatureC=-110, maxTemperatureC=-30,
                     meanWindSpeed=6, gustWindSpeed=22, dustStormChancePerSol=0.03)),
    "valles_marineris": dict(
        name="Valles Marineris", lat=-13.9, lon=300.8, datum=-3500.0, macro="valles",
        detail_m=20.0, micro_m=1.2, craters=0.5, rock_base=0.75,
        minerals=dict(clays=0.30, sulfates=0.70, ironOxides=0.55, subsurfaceIce=0.05),
        climate=dict(meanTemperatureC=-45, minTemperatureC=-90, maxTemperatureC=5,
                     meanWindSpeed=7, gustWindSpeed=25, dustStormChancePerSol=0.04)),
}


# ---------------------------------------------------------------------------
# Utilidades
# ---------------------------------------------------------------------------

def log(msg):
    print("[mars] " + msg, flush=True)


def stable_seed(text, base):
    """Semilla estable (no depende de PYTHONHASHSEED)."""
    return (zlib.crc32(text.encode("utf-8")) + base) & 0x7FFFFFFF


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def fractal_noise(size, beta, rng):
    """
    Ruido fractal por síntesis espectral (potencia ∝ 1/f^beta). beta≈2,0–2,4 reproduce bien el
    espectro de la topografía marciana; valores menores dan superficies más rugosas.
    Devuelve float32 size×size con media 0 y desviación 1.
    """
    n = 1 << int(math.ceil(math.log2(max(size, 2))))
    ky = np.fft.fftfreq(n)[:, None]
    kx = np.fft.rfftfreq(n)[None, :]
    k = np.sqrt(kx * kx + ky * ky)
    k[0, 0] = 1.0
    amp = k ** (-beta / 2.0)
    amp[0, 0] = 0.0
    phase = rng.uniform(0.0, 2.0 * np.pi, size=amp.shape)
    field = np.fft.irfft2(amp * np.exp(1j * phase), s=(n, n))[:size, :size]
    field = field - field.mean()
    std = field.std()
    if std > 0:
        field = field / std
    return field.astype(np.float32)


def fractal_noise_1d(size, beta, rng):
    """Perfil fractal 1D (media 0, desviación 1). beta≥3 da curvas suaves, sin dientes de sierra."""
    n = 1 << int(math.ceil(math.log2(max(size, 2))))
    k = np.fft.rfftfreq(n)
    k[0] = 1.0
    amp = k ** (-beta / 2.0)
    amp[0] = 0.0
    phase = rng.uniform(0.0, 2.0 * np.pi, size=amp.shape)
    line = np.fft.irfft(amp * np.exp(1j * phase), n=n)[:size]
    line = line - line.mean()
    std = line.std()
    return (line / std if std > 0 else line).astype(np.float32)


def box_blur(a, r):
    """Media en ventana (2r+1)² con imagen integral (rápido y sin SciPy)."""
    r = int(r)
    if r < 1:
        return a.astype(np.float32, copy=True)
    k = 2 * r + 1
    p = np.pad(a.astype(np.float64), r, mode="edge")
    c = np.cumsum(np.cumsum(p, axis=0), axis=1)
    c = np.pad(c, ((1, 0), (1, 0)))
    s = c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]
    return (s / (k * k)).astype(np.float32)


def slope_degrees(elev, mps):
    dz_dy, dz_dx = np.gradient(elev.astype(np.float32), mps)
    return np.degrees(np.arctan(np.hypot(dz_dx, dz_dy))).astype(np.float32)


def zone_bbox(lat, lon_east, size_m):
    """Caja lat/lon (grados) de un cuadrado de size_m metros centrado en (lat, lon)."""
    half_deg = math.degrees((size_m / 2.0) / MARS_RADIUS_M)
    lon_half = half_deg / max(0.05, math.cos(math.radians(lat)))
    return dict(lat_s=lat - half_deg, lat_n=lat + half_deg,
                lon_w=lon_east - lon_half, lon_e=lon_east + lon_half)


def lon_signed(lon_east):
    """0..360 este → −180..180."""
    return lon_east - 360.0 if lon_east > 180.0 else lon_east


def make_session():
    if requests is None:
        return None
    s = requests.Session()
    # Agente genérico: no se envía ningún dato personal.
    s.headers["User-Agent"] = "mars-terrain-preprocessor/1.0 (game asset pipeline)"
    return s


def chunk_rows_cols(n_chunks, cx, cz, v=VERTICES_PER_CHUNK):
    """Filas/columnas (array con fila 0 = NORTE) que ocupa el chunk (cx, cz) con cz=0 al SUR."""
    size = n_chunks * (v - 1) + 1
    south_row0 = cz * (v - 1)
    top = size - 1 - (south_row0 + v - 1)
    col0 = cx * (v - 1)
    return slice(top, top + v), slice(col0, col0 + v)


# ---------------------------------------------------------------------------
# 1) Elevación: MOLA (real) o simulación geológica
# ---------------------------------------------------------------------------

def fetch_mola(bbox, size, args):
    if rasterio is None:
        log("  MOLA: rasterio no está instalado → simulación")
        return None
    url = "/vsicurl/" + args.mola_url
    env = rasterio.Env(GDAL_DISABLE_READDIR_ON_OPEN="EMPTY_DIR", GDAL_CACHEMAX=64,
                       CPL_VSIL_CURL_ALLOWED_EXTENSIONS=".tif",
                       GDAL_HTTP_TIMEOUT=str(args.timeout),
                       GDAL_HTTP_MAX_RETRY="2")
    try:
        with env, rasterio.open(url) as ds:
            # El mosaico usa proyección cilíndrica simple en metros sobre la esfera de 3396190 m.
            x0 = math.radians(lon_signed(bbox["lon_w"])) * MARS_RADIUS_M
            x1 = math.radians(lon_signed(bbox["lon_e"])) * MARS_RADIUS_M
            y0 = math.radians(bbox["lat_s"]) * MARS_RADIUS_M
            y1 = math.radians(bbox["lat_n"]) * MARS_RADIUS_M
            win = from_bounds(min(x0, x1), y0, max(x0, x1), y1, transform=ds.transform)
            data = ds.read(1, window=win, out_shape=(size, size),
                           resampling=Resampling.bilinear).astype(np.float32)
            scale = ds.scales[0] if ds.scales and ds.scales[0] else 1.0
            offset = ds.offsets[0] if ds.offsets and ds.offsets[0] else 0.0
            data = data * scale + offset
            if ds.nodata is not None and np.any(data == ds.nodata):
                log("  MOLA: la ventana tiene huecos → simulación")
                return None
            log("  MOLA: %.0f..%.0f m (resolución nativa 463 m/px)" % (data.min(), data.max()))
            return data
    except Exception as e:  # red, URL cambiada, GDAL sin soporte HTTP...
        log("  MOLA: fallo (%s) → simulación" % e)
        return None


def add_craters(h, mps, rng, per_km2, rim_map):
    """
    Estampa cráteres simples con distribución de tamaños en ley de potencia N(>D) ∝ D^-2.
    Profundidad ≈ 0,2·D y borde ≈ 0,04·D (cráteres frescos), atenuados al azar (erosión).
    Marca en rim_map los bordes/eyecta (zonas con más bloques). Devuelve los cráteres creados.
    """
    size = h.shape[0]
    area_km2 = (size * mps / 1000.0) ** 2
    d_min = max(10.0, 3.0 * mps)
    d_max = min(400.0, size * mps / 6.0)
    count = int(rng.poisson(per_km2 * area_km2))
    placed = []
    ratio = (d_min / d_max) ** 2
    for _ in range(count):
        u = rng.uniform()
        diameter = d_min * (1.0 - u * (1.0 - ratio)) ** -0.5
        radius_px = diameter / 2.0 / mps
        cy, cx = rng.uniform(0, size, 2)
        ext = int(radius_px * 2.2) + 2
        y0, y1 = max(0, int(cy) - ext), min(size, int(cy) + ext + 1)
        x0, x1 = max(0, int(cx) - ext), min(size, int(cx) + ext + 1)
        if y0 >= y1 or x0 >= x1:
            continue
        yy, xx = np.mgrid[y0:y1, x0:x1]
        r = (np.hypot(yy - cy, xx - cx) / radius_px).astype(np.float32)
        fresh = rng.uniform(0.35, 1.0)
        depth = 0.2 * diameter * fresh
        rim = 0.04 * diameter * fresh
        inside = -depth * (1.0 - r * r) + rim
        outside = rim * np.exp(-((r - 1.0) / 0.35) ** 2)
        h[y0:y1, x0:x1] += np.where(r < 1.0, inside, outside).astype(np.float32)
        rim_map[y0:y1, x0:x1] = np.maximum(rim_map[y0:y1, x0:x1],
                                           fresh * np.exp(-((r - 1.1) / 0.3) ** 2))
        placed.append((cy, cx, radius_px))
    return placed


def macro_shape(zone, size, mps, rng):
    """Forma a gran escala de cada zona (metros, fila 0 = norte)."""
    u = np.linspace(0.0, 1.0, size, dtype=np.float32)[None, :]   # oeste → este
    v = np.linspace(0.0, 1.0, size, dtype=np.float32)[:, None]   # norte → sur
    zone_m = (size - 1) * mps
    h = np.zeros((size, size), dtype=np.float32)
    kind = zone["macro"]

    if kind == "jezero":
        # Frente del abanico deltaico al oeste: escarpe de ~90 m sobre el fondo del cráter.
        wiggle = fractal_noise_1d(size, 4.5, rng)[:, None] * 0.05
        front = 0.32 + wiggle
        h += 90.0 * smoothstep(front + 0.06, front - 0.06, u)
        h += 25.0 * smoothstep(0.0, 1.0, 1.0 - u)        # pendiente suave hacia el delta
    elif kind == "gale":
        # Faldas del monte Sharp al sur: capas sedimentarias en terrazas (~450 m de subida).
        t = smoothstep(0.3, 1.0, v) * 8.0
        steps = (np.floor(t) + smoothstep(0.55, 1.0, t - np.floor(t))) / 8.0
        h += 450.0 * steps
    elif kind == "elysium":
        h += 0.002 * u * zone_m                          # llanura casi perfecta
    elif kind == "utopia":
        # Suelo poligonal por contracción térmica del hielo: red de surcos de ~2 m.
        f = fractal_noise(size, 2.6, rng)
        h += -2.0 * np.exp(-(f / 0.08) ** 2)
        # Depresiones festoneadas (pérdida de hielo por sublimación).
        for _ in range(15):
            cy, cx = rng.uniform(0, size, 2)
            ry, rx = rng.uniform(15, 40, 2) / mps * 4.0
            yy = (np.arange(size, dtype=np.float32)[:, None] - cy) / ry
            xx = (np.arange(size, dtype=np.float32)[None, :] - cx) / rx
            h += -6.0 * np.exp(-(yy * yy + xx * xx))
    elif kind == "valles":
        # Pared del cañón: ~2 km de desnivel repartidos en ~3,3 km (pendiente media ≈ 31°,
        # como las paredes reales), con espolones y un fondo con derrubios de deslizamientos.
        edge = 0.55 + fractal_noise_1d(size, 4.5, rng)[:, None] * 0.05
        wall = smoothstep(edge - 0.2, edge + 0.2, u)
        h += 2000.0 * wall
        spurs = fractal_noise(size, 3.2, rng)
        h += 80.0 * spurs * (wall * (1.0 - wall) * 4.0)
        h += 12.0 * np.abs(fractal_noise(size, 3.0, rng)) * (1.0 - wall)
    elif kind == "acidalia":
        # Pequeños conos (posible vulcanismo de lodo) sobre la llanura.
        for _ in range(30):
            cy, cx = rng.uniform(0, size, 2)
            radius = rng.uniform(30.0, 70.0) / mps
            height = rng.uniform(10.0, 30.0)
            y0, y1 = max(0, int(cy - radius) - 1), min(size, int(cy + radius) + 2)
            x0, x1 = max(0, int(cx - radius) - 1), min(size, int(cx + radius) + 2)
            yy, xx = np.mgrid[y0:y1, x0:x1]
            r = (np.hypot(yy - cy, xx - cx) / radius).astype(np.float32)
            cone = np.where(r < 1.0, height * (1.0 - r) - 0.3 * height * np.exp(-(r / 0.25) ** 2), 0.0)
            h[y0:y1, x0:x1] += cone.astype(np.float32)
    return h


def build_elevation(zone, bbox, size, mps, rng, args, sources):
    rim_map = np.zeros((size, size), dtype=np.float32)
    # beta = 3,2: potencia ∝ 1/f^3,2 en 2D (≈ 1/f^2,2 en perfiles), como la topografía marciana
    # medida; con valores menores la pendiente a escala de metros sale irrealmente alta.
    detail = fractal_noise(size, 3.2, rng) * zone["detail_m"]
    mola = None if args.mode == "synthetic" else fetch_mola(bbox, size, args)
    if mola is not None:
        elev = mola + detail
        sources["elevation"] = "real: MOLA 463 m (USGS) + detalle fractal"
    else:
        if args.mode == "real":
            raise RuntimeError("--mode real: no se pudo leer MOLA")
        elev = zone["datum"] + macro_shape(zone, size, mps, rng) + detail
        sources["elevation"] = "synthetic: geología fractal de la zona"
    craters = add_craters(elev, mps, rng, zone["craters"], rim_map)
    return elev.astype(np.float32), rim_map, craters


# ---------------------------------------------------------------------------
# 2) Micro-relieve HiRISE (DTM localizado con la API ODE) o simulación
# ---------------------------------------------------------------------------

def ode_products(session, params, timeout):
    """Consulta la API REST de ODE y devuelve la lista de productos (puede estar vacía)."""
    # f = archivos, p = producto, m = metadatos (sin "m" no vienen las coordenadas de la huella).
    query = dict(query="product", target="mars", output="JSON", results="fpm")
    query.update(params)
    r = session.get(ODE_URL, params=query, timeout=timeout)
    r.raise_for_status()
    results = r.json().get("ODEResults", {})
    if str(results.get("Status", "")).upper() == "ERROR":
        raise RuntimeError(results.get("Error", "ODE devolvió un error"))
    products = results.get("Products", {})
    plist = products.get("Product", []) if isinstance(products, dict) else []
    return [plist] if isinstance(plist, dict) else list(plist)


def ode_bbox_params(bbox):
    return dict(minlat="%.5f" % bbox["lat_s"], maxlat="%.5f" % bbox["lat_n"],
                westlon="%.5f" % (bbox["lon_w"] % 360.0), eastlon="%.5f" % (bbox["lon_e"] % 360.0))


def product_files(product):
    files = product.get("Product_files", {})
    flist = files.get("Product_file", []) if isinstance(files, dict) else []
    return [flist] if isinstance(flist, dict) else list(flist)


def product_bounds(product):
    """Huella (lat_s, lat_n, lon_w, lon_e en 0..360) de un producto ODE, o None."""
    try:
        return (float(product["Minimum_latitude"]), float(product["Maximum_latitude"]),
                float(product["Westernmost_longitude"]) % 360.0, float(product["Easternmost_longitude"]) % 360.0)
    except (KeyError, TypeError, ValueError):
        return None


def hirise_highpass(data, valid, mps):
    """
    Paso alto del DTM (quita la forma grande, que ya aporta MOLA) usando SOLO datos válidos
    (convolución normalizada): así el borde de la franja HiRISE no deja un escalón.
    Devuelve (micro en metros, peso 0..1 que se desvanece suavemente cerca del borde).
    """
    r = max(1, int(40.0 / mps))
    v = valid.astype(np.float32)
    num = box_blur(np.where(valid, data, 0.0), r)
    den = box_blur(v, r)
    low = np.where(den > 1e-3, num / np.maximum(den, 1e-3), 0.0)
    micro = np.where(valid, data - low, 0.0).astype(np.float32)
    weight = (smoothstep(0.55, 0.95, den) * v).astype(np.float32)
    return micro, weight


def fetch_hirise_micro(bbox, size, mps, session, args, cache_dir=None):
    """Devuelve (micro_m, peso, cobertura 0..1) a partir de un DTM HiRISE, o (None, None, 0)."""
    if args.no_hirise or session is None or rasterio is None:
        return None, None, 0.0
    # La lectura remota del DTM tarda minutos: se guarda el resultado en la caché.
    cache_file = None
    if cache_dir is not None:
        key = "%.5f_%.5f_%.5f_%.5f_%d" % (bbox["lat_s"], bbox["lat_n"], bbox["lon_w"], bbox["lon_e"], size)
        cache_file = Path(cache_dir) / "hirise" / ("%08x.npz" % zlib.crc32(key.encode("ascii")))
        if cache_file.exists():
            cached = np.load(cache_file)
            log("  HiRISE: desde caché (cobertura %.0f %%)" % (float(cached["coverage"]) * 100))
            return cached["micro"], cached["weight"], float(cached["coverage"])
    result = _fetch_hirise_remote(bbox, size, mps, session, args)
    if result[0] is not None and cache_file is not None:
        cache_file.parent.mkdir(parents=True, exist_ok=True)
        np.savez_compressed(cache_file, micro=result[0], weight=result[1], coverage=result[2])
    return result


def _fetch_hirise_remote(bbox, size, mps, session, args):
    """Busca un DTM HiRISE con ODE y lo lee por ventana con rasterio /vsicurl/."""
    try:
        params = dict(ihid="MRO", iid="HIRISE", pt="DTM")
        params.update(ode_bbox_params(bbox))
        products = ode_products(session, params, args.timeout)
    except Exception as e:
        log("  HiRISE: consulta ODE fallida (%s)" % e)
        return None, None, 0.0

    urls = []
    for p in products:
        for f in product_files(p):
            name = str(f.get("FileName", ""))
            url = str(f.get("URL", ""))
            if name.upper().startswith("DTE") and name.upper().endswith(".IMG") and url.startswith("http"):
                urls.append(url)
    if not urls:
        log("  HiRISE: ningún DTM cubre la zona → micro-relieve simulado")
        return None, None, 0.0

    mars_geo = CRS.from_proj4("+proj=longlat +a=3396190 +b=3396190 +no_defs")
    for url in urls[:3]:
        name = url.rsplit("/", 1)[-1]
        for attempt in (1, 2):
            try:
                # Los DTM pesan 400-700 MB: se lee solo la ventana de la zona. Fragmentos de 1 MB
                # (menos peticiones), reintentos HTTP y caché de GDAL limitada (por defecto ~5 % de la RAM).
                with rasterio.Env(GDAL_DISABLE_READDIR_ON_OPEN="EMPTY_DIR", GDAL_CACHEMAX=64,
                                  GDAL_HTTP_TIMEOUT=str(int(args.timeout)), GDAL_HTTP_MAX_RETRY="5",
                                  GDAL_HTTP_RETRY_DELAY="3", CPL_VSIL_CURL_CHUNK_SIZE="1048576"),                         rasterio.open("/vsicurl/" + url) as ds:
                    left, bottom, right, top = transform_bounds(
                        mars_geo, ds.crs, lon_signed(bbox["lon_w"]), bbox["lat_s"],
                        lon_signed(bbox["lon_e"]), bbox["lat_n"])
                    win = from_bounds(left, bottom, right, top, transform=ds.transform)
                    nodata = ds.nodata if ds.nodata is not None else -3.4028226550889045e38
                    data = ds.read(1, window=win, out_shape=(size, size), boundless=True,
                                   fill_value=nodata, resampling=Resampling.bilinear).astype(np.float64)
                valid = (data > -1e30) & np.isfinite(data) & (data != nodata)
                coverage = float(valid.mean())
                if coverage < 0.05:
                    break                                   # este DTM apenas toca la zona: el siguiente
                micro, weight = hirise_highpass(data, valid, mps)
                log("  HiRISE: DTM %s (cobertura %.0f %%)" % (name, coverage * 100))
                return micro, weight, coverage
            except Exception as e:
                log("  HiRISE: intento %d con %s fallido (%s)" % (attempt, name, e))
    return None, None, 0.0


def build_micro(zone, bbox, size, mps, rng, session, args, sources, cache_dir=None):
    # Micro-relieve: rugosidad de escala métrica (beta 2,2), amplitud pequeña.
    synthetic = fractal_noise(size, 2.2, rng) * zone["micro_m"]
    if args.mode == "synthetic":
        sources["microRelief"] = "synthetic: ruido fractal"
        return synthetic, 0.0
    micro, weight, coverage = fetch_hirise_micro(bbox, size, mps, session, args, cache_dir)
    if micro is None:
        sources["microRelief"] = "synthetic: ruido fractal (sin DTM HiRISE en la zona)"
        return synthetic, 0.0
    # Donde el DTM no llega, micro-relieve simulado; en el borde se mezclan con suavidad.
    sources["microRelief"] = "real: DTM HiRISE (%.0f %% de la zona) + fractal" % (coverage * 100)
    return (micro * weight + synthetic * (1.0 - weight)).astype(np.float32), coverage


# ---------------------------------------------------------------------------
# 3) Textura CTX (WMTS de Mars Trek) o simulación
# ---------------------------------------------------------------------------

WMTS_NS = {"wmts": "http://www.opengis.net/wmts/1.0", "ows": "http://www.opengis.net/ows/1.1"}


def parse_wmts(xml_text):
    root = ElementTree.fromstring(xml_text)
    layer = root.find("wmts:Contents/wmts:Layer", WMTS_NS)
    if layer is None:
        raise RuntimeError("WMTS sin capas")
    resource = layer.find("wmts:ResourceURL", WMTS_NS)
    template = resource.get("template")
    style_el = layer.find("wmts:Style/ows:Identifier", WMTS_NS)
    style = style_el.text if style_el is not None else "default"
    tms_id = layer.find("wmts:TileMatrixSetLink/wmts:TileMatrixSet", WMTS_NS).text
    matrices = []
    for tms in root.findall("wmts:Contents/wmts:TileMatrixSet", WMTS_NS):
        if tms.find("ows:Identifier", WMTS_NS).text != tms_id:
            continue
        for tm in tms.findall("wmts:TileMatrix", WMTS_NS):
            a, b = [float(x) for x in tm.find("wmts:TopLeftCorner", WMTS_NS).text.split()]
            # Algunos servidores dan "lat lon" (orden EPSG:4326) y otros "lon lat".
            tl_lon, tl_lat = (b, a) if abs(a) == 90.0 and abs(b) == 180.0 else (a, b)
            matrices.append(dict(
                id=tm.find("ows:Identifier", WMTS_NS).text,
                tl_lon=tl_lon, tl_lat=tl_lat,
                tw=int(float(tm.find("wmts:TileWidth", WMTS_NS).text)),
                th=int(float(tm.find("wmts:TileHeight", WMTS_NS).text)),
                mw=int(float(tm.find("wmts:MatrixWidth", WMTS_NS).text)),
                mh=int(float(tm.find("wmts:MatrixHeight", WMTS_NS).text))))
    if not matrices:
        raise RuntimeError("WMTS sin TileMatrix")
    for m in matrices:
        m["deg_px"] = 360.0 / (m["mw"] * m["tw"])  # capa global en cilíndrica simple
    matrices.sort(key=lambda m: -m["deg_px"])       # de grueso a fino
    return template, style, tms_id, matrices


def fill_template(template, values):
    out = template
    for key, val in values.items():
        out = re.sub(r"\{" + key + r"\}", str(val), out, flags=re.IGNORECASE)
    return out


def ctx_tile_matrices(session, args):
    """
    Niveles de teselas de la capa CTX. Se leen del WMTSCapabilities de Mars Trek y, si las
    teselas las sirve un MapServer de ArcGIS (astro.arcgis.com), de su ?f=pjson, que lista más
    niveles (más detalle) y da la resolución exacta de cada uno.
    """
    r = session.get(args.ctx_caps, timeout=args.timeout)
    r.raise_for_status()
    template, style, tms_id, matrices = parse_wmts(r.text)
    arcgis = re.match(r"(https?://.+/MapServer)/tile/", template)
    if arcgis:
        try:
            info = session.get(arcgis.group(1), params={"f": "pjson"}, timeout=args.timeout).json()["tileInfo"]
            levels = []
            for lod in info["lods"]:
                deg_px = float(lod["resolution"])
                levels.append(dict(id=str(lod["level"]), tl_lon=float(info["origin"]["x"]),
                                   tl_lat=float(info["origin"]["y"]), tw=int(info["cols"]), th=int(info["rows"]),
                                   mw=int(round(360.0 / (int(info["cols"]) * deg_px))), deg_px=deg_px))
            if levels:
                matrices = sorted(levels, key=lambda m: -m["deg_px"])
        except Exception as e:
            log("  CTX: sin lista de niveles de ArcGIS (%s); uso la del WMTS" % e)
    return template, style, tms_id, matrices


def tile_range(m, lon_w, lon_e, lat_s, lat_n):
    span_x, span_y = m["tw"] * m["deg_px"], m["th"] * m["deg_px"]
    c0 = int(math.floor((lon_w - m["tl_lon"]) / span_x))
    c1 = int(math.floor((lon_e - m["tl_lon"]) / span_x))
    r0 = int(math.floor((m["tl_lat"] - lat_n) / span_y))
    r1 = int(math.floor((m["tl_lat"] - lat_s) / span_y))
    return c0, c1, r0, r1


def load_ctx_level(m, template, style, tms_id, ranges, session, args, cache_dir):
    """Descarga y cose las teselas de un nivel. Devuelve (mosaico, faltan, total)."""
    c0, c1, r0, r1 = ranges
    total = (c1 - c0 + 1) * (r1 - r0 + 1)
    mosaic = Image.new("L", ((c1 - c0 + 1) * m["tw"], (r1 - r0 + 1) * m["th"]), 0)
    filled = Image.new("L", mosaic.size, 0)          # 255 donde hay tesela buena
    missing = 0
    tile_cache = cache_dir / "ctx" / str(m["id"])
    tile_cache.mkdir(parents=True, exist_ok=True)
    for row in range(r0, r1 + 1):
        for col in range(c0, c1 + 1):
            wrapped = col % m["mw"]
            path = tile_cache / ("%d_%d.img" % (row, wrapped))
            bad = tile_cache / ("%d_%d.bad" % (row, wrapped))   # tesela que ya falló: no repetir
            if bad.exists():
                missing += 1
                continue
            data = path.read_bytes() if path.exists() else None
            if data is None:
                url = fill_template(template, {"Style": style, "TileMatrixSet": tms_id,
                                               "TileMatrix": m["id"], "TileRow": row, "TileCol": wrapped})
                try:
                    resp = session.get(url, timeout=args.timeout)
                    if resp.status_code == 200 and resp.content:
                        data = resp.content
                    elif 400 <= resp.status_code < 500:
                        bad.touch()                  # no existe en el servidor
                except Exception:
                    data = None                      # error de red: se reintentará otro día
            if data is None:
                missing += 1
                continue
            try:
                # Decodificación estricta: algunos servidores mandan PNG cortados (filas negras al
                # final). Esos se cuentan como que faltan en vez de pegarlos con franjas.
                tile = Image.open(io.BytesIO(data))
                tile.load()
                mosaic.paste(tile.convert("L"), ((col - c0) * m["tw"], (row - r0) * m["th"]))
                filled.paste(255, ((col - c0) * m["tw"], (row - r0) * m["th"],
                                   (col - c0 + 1) * m["tw"], (row - r0 + 1) * m["th"]))
                if not path.exists():
                    path.write_bytes(data)          # solo se guardan en caché las teselas buenas
            except Exception:
                missing += 1
                bad.touch()                          # PNG roto del servidor
    if missing:
        # Huecos de teselas que faltan: gris mediano de la imagen, no negro.
        arr, ok = np.asarray(mosaic), np.asarray(filled) > 0
        if ok.any():
            mosaic = Image.fromarray(np.where(ok, arr, np.uint8(np.median(arr[ok]))).astype(np.uint8), "L")
    return mosaic, missing, total


def fetch_ctx(bbox, out_px, session, args, cache_dir):
    if session is None:
        return None
    try:
        template, style, tms_id, matrices = ctx_tile_matrices(session, args)
    except Exception as e:
        log("  CTX: no se pudo leer WMTSCapabilities (%s) → simulación" % e)
        return None

    lon_w, lon_e = lon_signed(bbox["lon_w"]), lon_signed(bbox["lon_e"])
    lat_s, lat_n = bbox["lat_s"], bbox["lat_n"]
    target = (lat_n - lat_s) / out_px
    # Empezamos por el nivel más grueso que sea al menos tan fino como lo pedido; si ese nivel
    # falla (sin cobertura o teselas rotas), probamos los siguientes más gruesos.
    candidates = [i for i, m in enumerate(matrices) if m["deg_px"] <= target]
    start = candidates[0] if candidates else len(matrices) - 1
    attempts = 0
    for idx in range(start, -1, -1):
        m = matrices[idx]
        ranges = tile_range(m, lon_w, lon_e, lat_s, lat_n)
        c0, c1, r0, r1 = ranges
        count = (c1 - c0 + 1) * (r1 - r0 + 1)
        if count > args.max_tiles:
            continue
        attempts += 1
        if attempts > 3:
            break
        log("  CTX: nivel %s (%.1f m/píxel), %d×%d teselas" %
            (m["id"], math.radians(m["deg_px"]) * MARS_RADIUS_M, c1 - c0 + 1, r1 - r0 + 1))
        mosaic, missing, total = load_ctx_level(m, template, style, tms_id, ranges, session, args, cache_dir)
        if missing > total * 0.1:
            log("  CTX: nivel %s inservible (faltan o vienen rotas %d de %d teselas)" % (m["id"], missing, total))
            continue
        left = (lon_w - m["tl_lon"]) / m["deg_px"] - c0 * m["tw"]
        right = (lon_e - m["tl_lon"]) / m["deg_px"] - c0 * m["tw"]
        top = (m["tl_lat"] - lat_n) / m["deg_px"] - r0 * m["th"]
        bottom = (m["tl_lat"] - lat_s) / m["deg_px"] - r0 * m["th"]
        crop = mosaic.crop((int(left), int(top), int(math.ceil(right)), int(math.ceil(bottom))))
        crop = crop.resize((out_px, out_px), Image.LANCZOS)
        return ImageOps.autocontrast(crop, cutoff=1)   # "textura difusa normalizada"
    log("  CTX: ningún nivel útil → simulación")
    return None


def synthetic_albedo(elev, rocks, mps, out_px, rng):
    """Imagen tipo CTX: sombreado del relieve (sol al NO, 35°) + polvo claro/arena oscura."""
    dz_dy, dz_dx = np.gradient(elev, mps)
    # Fila 0 = norte: dz_dy positivo = baja hacia el sur.
    azimuth, altitude = math.radians(315.0), math.radians(35.0)
    slope = np.arctan(np.hypot(dz_dx, dz_dy))
    aspect = np.arctan2(-dz_dy, dz_dx)
    shade = (math.sin(altitude) * np.cos(slope) +
             math.cos(altitude) * np.sin(slope) * np.cos(azimuth - aspect))
    lows = box_blur(elev, 20) - elev                    # depresiones = arena oscura
    tone = 0.55 + 0.35 * (shade - shade.mean()) / (shade.std() + 1e-6) * 0.5
    tone -= 0.08 * np.clip(lows / (np.abs(lows).max() + 1e-6), 0.0, 1.0)
    tone += 0.05 * fractal_noise(elev.shape[0], 1.8, rng)
    tone -= 0.08 * rocks
    img = Image.fromarray((np.clip(tone, 0.0, 1.0) * 255).astype(np.uint8), "L")
    return ImageOps.autocontrast(img.resize((out_px, out_px), Image.BILINEAR), cutoff=1)


# ---------------------------------------------------------------------------
# 4) Rocas, minerales (CRISM) y entorno
# ---------------------------------------------------------------------------

def build_rocks(zone, elev, micro, rim_map, mps, rng):
    slope = slope_degrees(elev, mps)
    rough = box_blur(np.abs(micro - box_blur(micro, 2)), 3)
    rough = rough / (rough.max() + 1e-6)
    patches = fractal_noise(elev.shape[0], 2.4, rng)
    base = zone["rock_base"] * (0.6 + 0.4 * np.tanh(patches))
    rocks = base + 0.5 * smoothstep(10.0, 35.0, slope) + 0.4 * rough + 0.5 * rim_map
    return np.clip(rocks, 0.0, 1.0).astype(np.float32)


def query_crism(bbox, session, args):
    """Huellas de las observaciones CRISM que tocan la zona, o None si no se pudo consultar."""
    if args.no_crism or session is None or args.mode == "synthetic":
        return None
    for pt in ("MTRDR", "TRDR"):
        try:
            params = dict(ihid="MRO", iid="CRISM", pt=pt)
            params.update(ode_bbox_params(bbox))
            products = ode_products(session, params, args.timeout)
            footprints = [b for b in (product_bounds(p) for p in products) if b is not None]
            log("  CRISM: %d observaciones %s en la zona" % (len(products), pt))
            if products:
                return footprints
        except Exception as e:
            log("  CRISM: consulta %s fallida (%s)" % (pt, e))
    return []


def build_mineral_fields(zone, elev, slope, size, rng):
    """Campos 0..1 por mineral, modulados por la geología (cotas bajas, paredes, crestas)."""
    rel = (elev - elev.min()) / (np.ptp(elev) + 1e-6)
    fields = {}
    for name in MINERALS:
        f = fractal_noise(size, 3.0, rng)
        f = 1.0 / (1.0 + np.exp(-1.5 * f))                   # 0..1
        if name in ("clays", "sulfates"):
            f *= 0.6 + 0.8 * (1.0 - rel) + 0.4 * smoothstep(15.0, 35.0, slope)  # sedimentos y capas expuestas
        elif name == "ironOxides":
            f *= 0.7 + 0.6 * rel                              # crestas y superficies expuestas
        elif name == "subsurfaceIce":
            f *= 1.2 - smoothstep(5.0, 25.0, slope)           # el hielo se conserva en llano
        fields[name] = np.clip(zone["minerals"][name] * f * 1.4, 0.0, 1.0).astype(np.float32)
    return fields


def environment_at(zone, elevation_m):
    pressure = DATUM_PRESSURE_PA * math.exp(-elevation_m / PRESSURE_SCALE_HEIGHT_M)
    gale_p = DATUM_PRESSURE_PA * math.exp(-GALE_ELEVATION_M / PRESSURE_SCALE_HEIGHT_M)
    # Menos atmósfera encima = menos blindaje frente a los rayos cósmicos.
    radiation = RAD_GALE_MGY_PER_DAY * (gale_p / pressure) ** 0.25
    temp = zone["climate"]["meanTemperatureC"] - LAPSE_RATE_K_PER_KM * (elevation_m - zone["datum"]) / 1000.0
    return dict(meanTemperatureC=round(temp, 2), windSpeed=float(zone["climate"]["meanWindSpeed"]),
                pressurePa=round(pressure, 1), radiationMilliGrayPerDay=round(radiation, 4))


def check_climate_references(session, args):
    status = {}
    if session is None or args.mode == "synthetic":
        return status
    for key, url in CLIMATE_REFERENCES.items():
        try:
            status[key] = session.head(url, timeout=min(args.timeout, 15), allow_redirects=True).status_code < 400
        except Exception:
            status[key] = False
    return status


# ---------------------------------------------------------------------------
# 5) Escritura de la zona
# ---------------------------------------------------------------------------

def process_zone(zone_id, args, session, out_root, cache_dir):
    zone = ZONES[zone_id]
    n = args.chunks
    v = VERTICES_PER_CHUNK
    size = n * (v - 1) + 1
    mps = args.meters_per_sample
    zone_m = (size - 1) * mps
    chunk_m = (v - 1) * mps
    bbox = zone_bbox(zone["lat"], zone["lon"], zone_m)
    rng = np.random.default_rng(stable_seed(zone_id, args.seed))
    sources = {}
    t0 = time.time()
    log("%s: %d×%d chunks, %.2f km de lado, %.1f m por muestra" % (zone["name"], n, n, zone_m / 1000.0, mps))

    elev, rim_map, craters = build_elevation(zone, bbox, size, mps, rng, args, sources)
    micro, hirise_cov = build_micro(zone, bbox, size, mps, rng, session, args, sources, cache_dir)
    rocks = build_rocks(zone, elev, micro, rim_map, mps, rng)
    sources["rocks"] = "derivado: pendiente + rugosidad del micro-relieve + bordes de cráter"
    slope = slope_degrees(elev, mps)

    albedo = None if args.mode == "synthetic" else fetch_ctx(bbox, n * args.albedo_size, session, args, cache_dir)
    if albedo is not None:
        sources["albedo"] = "real: CTX 5 m (Murray Lab/Caltech) vía NASA Mars Trek"
    else:
        albedo = synthetic_albedo(elev, rocks, mps, n * args.albedo_size, rng)
        sources["albedo"] = "synthetic: sombreado del relieve"
    albedo_arr = np.asarray(albedo, dtype=np.uint8)

    crism_centers = query_crism(bbox, session, args)
    fields = build_mineral_fields(zone, elev, slope, size, rng)
    sources["minerals"] = ("índices por geología publicada + ruido; CRISM ODE: %s" %
                           ("sin consultar" if crism_centers is None else "%d observaciones" % len(crism_centers)))

    climate_refs = check_climate_references(session, args)
    sources["climate"] = ("perfil medio por zona (Mars Climate Database / NASA Ames, Curiosity RAD), "
                          "ajustado por altitud" + ("; referencias accesibles: %s" % climate_refs if climate_refs else ""))

    # Rango de altura común a toda la zona (sin micro-relieve: Unity lo suma aparte).
    h_min, h_max = float(elev.min()), float(elev.max())
    h_range = max(h_max - h_min, 0.01)
    micro_scale = float(min(max(np.abs(micro).max(), 0.05), 5.0))

    # Se escribe en una carpeta temporal y se cambia al final: si algo falla a medias
    # (red, memoria), la zona anterior queda intacta.
    final_dir = out_root / zone_id
    zone_dir = out_root / ("." + zone_id + ".tmp")   # el punto inicial hace que Unity la ignore
    if zone_dir.exists():
        shutil.rmtree(zone_dir)
    zone_dir.mkdir(parents=True)
    a = args.albedo_size
    rm = args.rock_mask_size

    for cz in range(n):
        for cx in range(n):
            rows, cols = chunk_rows_cols(n, cx, cz, v)
            base = zone_dir / ("chunk_%d_%d" % (cx, cz))

            h_block = elev[rows, cols]
            q = np.clip(np.round((h_block - h_min) / h_range * 65535.0), 0, 65535).astype("<u2")
            np.flipud(q).tofile(str(base) + ".heightmap")          # fila 0 = sur

            m_block = np.clip(np.round(micro[rows, cols] / micro_scale * 127.0), -127, 127).astype(np.int8)
            np.flipud(m_block).tofile(str(base) + ".micro")

            r_img = Image.fromarray((rocks[rows, cols] * 255).astype(np.uint8), "L").resize((rm, rm), Image.BILINEAR)
            np.flipud(np.asarray(r_img, dtype=np.uint8)).tofile(str(base) + ".rocks")

            top = (n - 1 - cz) * a                                   # la imagen va con el norte arriba
            Image.fromarray(albedo_arr[top:top + a, cx * a:(cx + 1) * a], "L").save(
                str(base) + "_albedo.png", optimize=True)

            write_chunk_metadata(base, zone, cx, cz, n, v, size, rows, cols, h_block, rocks, fields,
                                 craters, crism_centers, bbox, chunk_m, hirise_cov, sources, rng)

    manifest = dict(
        formatVersion=1, zoneId=zone_id, displayName=zone["name"],
        centerLat=zone["lat"], centerLonEast=zone["lon"],
        chunksX=n, chunksZ=n, verticesPerChunk=v, metersPerSample=mps,
        chunkSizeMeters=chunk_m, zoneSizeMeters=zone_m,
        heightMinMeters=h_min, heightMaxMeters=h_max, microScaleMeters=micro_scale,
        albedoSize=a, rockMaskSize=rm, gravity=MARS_GRAVITY,
        sources=sources,
        climate=dict(zone["climate"],
                     surfacePressurePa=environment_at(zone, zone["datum"])["pressurePa"],
                     radiationMilliGrayPerDay=environment_at(zone, zone["datum"])["radiationMilliGrayPerDay"]),
        generatedUtc=datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"))
    with open(zone_dir / "manifest.json", "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, ensure_ascii=False, indent=2)
    old_dir = out_root / ("." + zone_id + ".old")
    if old_dir.exists():
        shutil.rmtree(old_dir)
    if final_dir.exists():
        final_dir.rename(old_dir)
    zone_dir.rename(final_dir)
    if old_dir.exists():
        shutil.rmtree(old_dir)
    log("  listo en %.1f s → %s" % (time.time() - t0, final_dir))


def write_chunk_metadata(base, zone, cx, cz, n, v, size, rows, cols, h_block, rocks, fields,
                         craters, crism_centers, bbox, chunk_m, hirise_cov, sources, rng):
    mean_h = float(h_block.mean())
    minerals = {name: round(float(fields[name][rows, cols].mean()), 4) for name in MINERALS}

    # Cráteres cuyo centro cae en el chunk.
    r0, r1, c0, c1 = rows.start, rows.stop, cols.start, cols.stop
    crater_count = sum(1 for (cy, ccx, _r) in craters if r0 <= cy < r1 and c0 <= ccx < c1)

    # Observaciones CRISM cuya huella se solapa con el chunk (el centro suele caer fuera).
    crism_count = 0
    if crism_centers:
        lat_s = bbox["lat_s"] + (bbox["lat_n"] - bbox["lat_s"]) * cz / n
        lat_n = bbox["lat_s"] + (bbox["lat_n"] - bbox["lat_s"]) * (cz + 1) / n
        lon_w = (bbox["lon_w"] + (bbox["lon_e"] - bbox["lon_w"]) * cx / n) % 360.0
        lon_e = (bbox["lon_w"] + (bbox["lon_e"] - bbox["lon_w"]) * (cx + 1) / n) % 360.0
        crism_count = sum(1 for (fs, fn, fw, fe) in crism_centers
                          if fs < lat_n and fn > lat_s and fw < lon_e and fe > lon_w)

    # Vetas: en los puntos más ricos de cada mineral (unas pocas por chunk).
    veins = []
    for name in MINERALS:
        field = fields[name][rows, cols]
        expected = 3.0 * minerals[name]
        count = int(min(6, rng.poisson(expected)))
        if count == 0:
            continue
        cand_y = rng.integers(0, v, 40)
        cand_x = rng.integers(0, v, 40)
        values = field[cand_y, cand_x]
        for idx in np.argsort(-values)[:count]:
            veins.append(dict(mineral=name,
                              u=round(float(cand_x[idx]) / (v - 1), 4),
                              v=round(1.0 - float(cand_y[idx]) / (v - 1), 4),  # v = 0 al sur
                              richness=round(float(values[idx]), 3)))

    meta = dict(
        chunkX=cx, chunkZ=cz,
        meanElevationMeters=round(mean_h, 2),
        minElevationMeters=round(float(h_block.min()), 2),
        maxElevationMeters=round(float(h_block.max()), 2),
        rockDensity=round(float(rocks[rows, cols].mean()), 4),
        craterCount=crater_count,
        crismObservations=crism_count,
        hiriseCoverage=round(float(hirise_cov), 3),
        minerals=minerals,
        environment=environment_at(zone, mean_h),
        veins=veins,
        source="elevación: %s; minerales: %s" % (sources["elevation"], sources["minerals"]))
    with open(str(base) + "_minerals.json", "w", encoding="utf-8") as fh:
        json.dump(meta, fh, ensure_ascii=False, indent=2)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def parse_args(argv=None):
    project_root = Path(__file__).resolve().parents[2]
    p = argparse.ArgumentParser(description="Genera los chunks del mapa de Marte para Unity.")
    p.add_argument("--zones", default="all",
                   help="'all' o lista separada por comas: " + ",".join(ZONES))
    p.add_argument("--mode", choices=("auto", "real", "synthetic"), default="auto",
                   help="auto: intenta datos reales y simula lo que falle; real: falla si no hay MOLA; "
                        "synthetic: todo simulado, sin internet")
    p.add_argument("--chunks", type=int, default=8, help="chunks por lado (zona cuadrada)")
    p.add_argument("--meters-per-sample", type=float, default=4.0,
                   help="separación entre vértices (4 m → zona de 8,16 km con 8 chunks)")
    p.add_argument("--albedo-size", type=int, default=256, help="píxeles por lado de cada textura de chunk")
    p.add_argument("--rock-mask-size", type=int, default=128)
    p.add_argument("--out", default=str(project_root / "Assets" / "StreamingAssets" / "MarsMap"))
    p.add_argument("--cache", default=str(Path(__file__).resolve().parent / "cache"))
    p.add_argument("--timeout", type=float, default=30.0, help="segundos por petición de red")
    p.add_argument("--max-tiles", type=int, default=300, help="máximo de teselas CTX por zona")
    p.add_argument("--no-hirise", action="store_true", help="no buscar DTM de HiRISE")
    p.add_argument("--no-crism", action="store_true", help="no consultar CRISM en ODE")
    p.add_argument("--mola-url", default=DEFAULT_MOLA_URL)
    p.add_argument("--ctx-caps", default=DEFAULT_CTX_CAPS)
    p.add_argument("--seed", type=int, default=1234)
    args = p.parse_args(argv)
    if args.chunks < 1 or args.meters_per_sample <= 0:
        p.error("--chunks y --meters-per-sample deben ser positivos")
    return args


def main(argv=None):
    # La consola de Windows suele usar cp1252: sin esto, caracteres como "→" o "×" rompen el script.
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    args = parse_args(argv)
    zone_ids = list(ZONES) if args.zones == "all" else [z.strip() for z in args.zones.split(",") if z.strip()]
    unknown = [z for z in zone_ids if z not in ZONES]
    if unknown:
        log("zonas desconocidas: %s (válidas: %s)" % (", ".join(unknown), ", ".join(ZONES)))
        return 2
    if args.mode != "synthetic" and requests is None:
        log("falta 'requests' (pip install requests); se usa el modo synthetic")
        args.mode = "synthetic"
    if args.mode != "synthetic" and rasterio is None:
        log("aviso: sin 'rasterio' no se leen MOLA ni HiRISE (se simulan); CTX y CRISM sí funcionan")

    out_root = Path(args.out)
    cache_dir = Path(args.cache)
    out_root.mkdir(parents=True, exist_ok=True)
    cache_dir.mkdir(parents=True, exist_ok=True)
    session = make_session() if args.mode != "synthetic" else None

    failures = 0
    for zone_id in zone_ids:
        try:
            process_zone(zone_id, args, session, out_root, cache_dir)
        except Exception as e:
            failures += 1
            log("ERROR en %s: %s" % (zone_id, e))
    log("terminado: %d zona(s) bien, %d con error" % (len(zone_ids) - failures, failures))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
