"""
Genera las imágenes (2D, pintadas por capas) de todo lo que se puede construir.
Salen en Assets/Resources/StructureArt/<clave>.png y el juego las usa en el menú de
construcción (B / N) y en la tarjeta al colocar.

Claves: pieza_<id> (BuildPieceCatalog), modulo_<moduleId> (MarsModuleData),
        equipo_<id> (StructureCatalog).

Uso: python Tools/StructureArt/generar_imagenes.py
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pintor import Lienzo, S  # noqa: E402

AQUI = os.path.dirname(os.path.abspath(__file__))
SALIDA = os.path.normpath(os.path.join(AQUI, "..", "..", "Assets", "Resources", "StructureArt"))

REG = (0.64, 0.39, 0.26)          # regolito sinterizado
REG_OSC = (0.5, 0.3, 0.2)
METAL = (0.8, 0.81, 0.82)
TITANIO = (0.62, 0.64, 0.68)
BLANCO = (0.9, 0.9, 0.88)
OSCURO = (0.22, 0.23, 0.26)
NARANJA = (0.92, 0.48, 0.14)
VERDE = (0.25, 0.7, 0.35)
AZUL_CEL = (0.1, 0.18, 0.42)
VIDRIO = (0.5, 0.7, 0.82)


# ------------------------------------------------------------------ utilidades de escena

def nuevo(seed, escala, centro=(0.5, 0.68)):
    c = Lienzo(seed=seed, escala=escala, centro=centro)
    c.fondo()
    return c


def ventana_y(c, x0, x1, z0, z1, y, alfa=0.85):
    """Ventana en la cara izquierda (y máxima) de una caja."""
    pol = c.poligono([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], VIDRIO, "pintura", 0.9, alfa)
    c.linea([pol[3], pol[2], pol[1]], (0.9, 0.92, 0.95), 2, 0.8)
    c.linea([c.p(x0 + (x1 - x0) * 0.2, y, z1 - 0.1), c.p(x0 + (x1 - x0) * 0.45, y, z0 + 0.1)], (1, 1, 1), 3, 0.35)


def ventana_x(c, y0, y1, z0, z1, x, alfa=0.85):
    """Ventana en la cara derecha (x máxima)."""
    pol = c.poligono([(x, y0, z0), (x, y1, z0), (x, y1, z1), (x, y0, z1)], VIDRIO, "pintura", 0.75, alfa)
    c.linea([pol[0], pol[1], pol[2], pol[3], pol[0]], (0.85, 0.87, 0.9), 2, 0.7)


def franjas_y(c, x0, x1, z0, z1, y, n=8):
    """Franjas de peligro amarillas y negras en la cara izquierda."""
    for i in range(n):
        a = x0 + (x1 - x0) * i / n
        b = x0 + (x1 - x0) * (i + 1) / n
        col = (0.95, 0.78, 0.1) if i % 2 == 0 else (0.1, 0.1, 0.1)
        c.poligono([(a, y, z0), (b, y, z0), (b, y, z1), (a, y, z1)], col, "pintura", 0.95)


def anillo(c, x, y, z, r, color, ancho=3.0, alfa=0.9):
    cx, cy = c.p(x, y, z)
    rx, ry = r * c.k, r * c.k * 0.5
    img = Image.new("L", (S, S), 0)
    ImageDraw.Draw(img).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], outline=255, width=int(ancho * S / 512))
    c.mezclar(np.asarray(img, np.float32) / 255, np.asarray(color, np.float32), alfa)


def disco(c, x, y, z, r, color, inclin=0.75):
    """Plato de antena mirando al cielo (elipse con degradado radial)."""
    cx, cy = c.p(x, y, z)
    rx, ry = r * c.k, r * c.k * inclin
    dx, dy = (c.xx - cx) / rx, (c.yy - cy) / ry
    d2 = dx * dx + dy * dy
    m = np.clip((1 - d2) * rx / 2, 0, 1)
    luz = 0.55 + 0.5 * np.clip(0.6 * dx + 0.7 * dy + 0.4, 0, 1)
    c.mezclar(m, np.asarray(color, np.float32) * luz[..., None])
    anillo(c, x, y, z, r, (0.95, 0.95, 0.95), 2, 0.6)


def pilares(c, sx, sy, alto, r=0.12, color=TITANIO):
    for (px, py) in ((-sx / 2, sy / 2), (sx / 2, sy / 2), (sx / 2, -sy / 2), (-sx / 2, -sy / 2)):
        c.cilindro(px, py, 0, r, alto, color)


def rejilla_superior(c, x0, x1, y0, y1, z, n, color, alfa=0.6):
    for i in range(1, n):
        t = i / n
        c.linea([c.p(x0 + (x1 - x0) * t, y0, z), c.p(x0 + (x1 - x0) * t, y1, z)], color, 1.6, alfa)
        c.linea([c.p(x0, y0 + (y1 - y0) * t, z), c.p(x1, y0 + (y1 - y0) * t, z)], color, 1.6, alfa)


def cristal_caja(c, x, y, z, sx, sy, sz, alfa=0.45):
    """Volumen de cristal: caras traslúcidas y marco de titanio."""
    x0, x1, y0, y1, z0, z1 = x - sx / 2, x + sx / 2, y - sy / 2, y + sy / 2, z, z + sz
    c.poligono([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], VIDRIO, "pintura", 1.0, alfa)
    c.poligono([(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], VIDRIO, "pintura", 0.8, alfa)
    c.poligono([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], VIDRIO, "pintura", 1.15, alfa)
    P = c.p
    marco = (0.75, 0.77, 0.8)
    for seg in ([P(x0, y1, z0), P(x1, y1, z0), P(x1, y0, z0)], [P(x0, y1, z1), P(x1, y1, z1), P(x1, y0, z1), P(x0, y0, z1), P(x0, y1, z1)],
                [P(x0, y1, z0), P(x0, y1, z1)], [P(x1, y1, z0), P(x1, y1, z1)], [P(x1, y0, z0), P(x1, y0, z1)]):
        c.linea(seg, marco, 3, 0.95)
    # Reflejo diagonal.
    c.linea([P(x0 + sx * 0.15, y1, z1 - sz * 0.1), P(x0 + sx * 0.45, y1, z0 + sz * 0.15)], (1, 1, 1), 4, 0.3)


def tuberia(c, a, b, color=TITANIO, ancho=6):
    c.linea([c.p(*a), c.p(*b)], np.asarray(color) * 0.6, ancho + 2, 1)
    c.linea([c.p(*a), c.p(*b)], color, ancho, 1)


def luz_verde(c, x, y, z, r=0.12, color=(0.3, 1, 0.45)):
    c.esfera(x, y, z, r, color)


# ------------------------------------------------------------------ piezas de construcción

def pieza_cimiento(c):
    c.sombra(0, 0, 3.3, 1.6, 0.45)
    c.caja(0, 0, 0, 4, 4, 0.55, REG, "regolito", juntas=1)
    rejilla_superior(c, -2, 2, -2, 2, 0.55, 4, (0.35, 0.2, 0.13), 0.45)


def pieza_losa_piso(c):
    c.sombra(0, 0, 3.4, 1.7, 0.4)
    pilares(c, 3.6, 3.6, 2.2)
    c.caja(0, 0, 2.2, 4, 4, 0.3, (0.7, 0.68, 0.64), "metal")
    rejilla_superior(c, -2, 2, -2, 2, 2.5, 2, (0.4, 0.4, 0.42), 0.5)


def pieza_rejilla(c):
    c.sombra(0, 0, 3.2, 1.5, 0.3)
    pilares(c, 3.6, 3.6, 1.4, 0.08)
    c.caja(0, 0, 1.4, 4, 4, 0.12, (0.45, 0.47, 0.5), "metal")
    rejilla_superior(c, -2, 2, -2, 2, 1.52, 14, (0.15, 0.15, 0.17), 0.85)


def pieza_pared_regolito(c):
    c.sombra(0, 0, 2.6, 1.0, 0.45)
    c.caja(0, 0, 0, 4, 0.5, 3, REG, "regolito", juntas=6)


def pieza_pared_ventana(c):
    c.sombra(0, 0, 2.6, 1.0, 0.45)
    c.caja(0, 0, 0, 4, 0.5, 3, REG, "regolito", juntas=6)
    ventana_y(c, -1.0, 1.0, 1.0, 2.3, 0.251)
    c.linea([c.p(-1.0, 0.251, 1.0), c.p(1.0, 0.251, 1.0)], (0.75, 0.77, 0.8), 4, 0.9)


def pieza_panel_presurizado(c):
    c.sombra(0, 0, 2.6, 1.0, 0.4)
    c.caja(0, 0, 0, 4, 0.3, 3, (0.92, 0.86, 0.62), "pintura")
    for x in (-2, 0, 2):
        c.linea([c.p(x, 0.151, 0), c.p(x, 0.151, 3)], TITANIO, 6, 1)
    for z in (0.05, 1.5, 2.95):
        c.linea([c.p(-2, 0.151, z), c.p(2, 0.151, z)], TITANIO, 6, 1)


def pieza_murete(c):
    c.sombra(0, 0, 2.6, 1.0, 0.4)
    c.caja(0, 0, 0, 4, 0.4, 1.0, REG, "regolito", juntas=2)


def pieza_panel_cristal(c):
    c.sombra(0, 0, 2.6, 1.0, 0.25)
    cristal_caja(c, 0, 0, 0, 4, 0.15, 3)
    c.linea([c.p(0, 0.075, 0), c.p(0, 0.075, 3)], (0.75, 0.77, 0.8), 4, 0.95)


def pieza_techo_cristal(c):
    c.sombra(0, 0, 3.3, 1.6, 0.3)
    pilares(c, 3.8, 3.8, 2.6)
    cristal_caja(c, 0, 0, 2.6, 4, 4, 0.15, 0.5)
    rejilla_superior(c, -2, 2, -2, 2, 2.75, 4, (0.8, 0.82, 0.85), 0.8)


def pieza_claraboya(c):
    c.sombra(0, 0, 3.0, 1.5, 0.35)
    c.caja(0, 0, 0, 4, 4, 0.4, (0.7, 0.68, 0.64), "metal")
    c.esfera(0, 0, 0.4, 1.7, VIDRIO, cristal=True, corte=0.05)
    cx, cy = c.p(0, 0, 0.4)
    R = 1.7 * c.k
    for a in np.linspace(0.15, 0.85, 5):   # nervios de la cúpula
        img = Image.new("L", (S, S), 0)
        ImageDraw.Draw(img).arc([cx - R * a, cy - R, cx + R * a, cy + R], 180, 360, fill=255, width=3)
        c.mezclar(np.asarray(img, np.float32) / 255, np.array([0.85, 0.87, 0.9]), 0.7)


def pieza_puerta_presurizada(c):
    c.sombra(0, 0, 2.6, 1.0, 0.45)
    c.caja(0, 0, 0, 4, 0.5, 3, (0.78, 0.79, 0.8), "metal", juntas=0)
    c.poligono([(-0.75, 0.251, 0), (0.75, 0.251, 0), (0.75, 0.251, 2.4), (-0.75, 0.251, 2.4)], (0.35, 0.37, 0.4), "metal", 1.0)
    franjas_y(c, -0.95, 0.95, 2.4, 2.6, 0.252, 10)
    c.linea([c.p(0, 0.253, 0), c.p(0, 0.253, 2.4)], (0.15, 0.15, 0.17), 3, 0.9)
    luz_verde(c, 1.1, 0.3, 1.4, 0.1)


def pieza_porton_rover(c):
    c.sombra(0, 0, 2.8, 1.1, 0.45)
    c.caja(0, 0, 0, 4.4, 0.6, 3.6, (0.72, 0.73, 0.74), "metal")
    x0, x1 = -1.8, 1.8
    for i in range(14):
        z0 = i * 0.22
        col = (0.55, 0.57, 0.6) if i % 2 else (0.48, 0.5, 0.53)
        c.poligono([(x0, 0.301, z0), (x1, 0.301, z0), (x1, 0.301, z0 + 0.22), (x0, 0.301, z0 + 0.22)], col, "metal", 1.0)
    franjas_y(c, -2.2, 2.2, 3.15, 3.45, 0.302, 14)


def pieza_techo_regolito(c):
    c.sombra(0, 0, 3.4, 1.7, 0.5)
    pilares(c, 3.6, 3.6, 2.4)
    c.caja(0, 0, 2.4, 4, 4, 0.7, REG_OSC, "regolito", juntas=2)


def pieza_pilar(c):
    c.sombra(0, 0, 1.0, 0.5, 0.45)
    c.caja(0, 0, 0, 0.9, 0.9, 0.2, OSCURO, "metal")
    c.cilindro(0, 0, 0.2, 0.22, 3.2, TITANIO, bandas=[(2.9, 0.12, (0.45, 0.46, 0.5))])


def pieza_escalera(c):
    c.sombra(0, 0, 2.2, 1.2, 0.45)
    n = 9
    for i in range(n):
        c.caja(-1.6 + i * 0.36, 0, 0, 0.36, 1.4, 0.34 * (n - i), (0.66, 0.67, 0.7) if i % 2 else (0.6, 0.61, 0.64), "metal", brillo=0.6)
    c.linea([c.p(-1.8, 0.75, 4.0), c.p(1.6, 0.75, 1.0)], TITANIO, 4, 1)   # barandilla


# ------------------------------------------------------------------ módulos presurizados

def modulo_habitat_tripulacion(c):
    c.sombra(0, 0, 5.2, 2.6, 0.5)
    c.cilindro(0, 0, 0, 3.6, 2.6, BLANCO, bandas=[(0.25, 0.22, NARANJA)])
    c.esfera(0, 0, 2.6, 3.6, (0.92, 0.92, 0.9), corte=0.02)
    for (x, y) in ((1.6, 3.1), (3.1, 1.6), (-0.6, 3.5)):
        cx, cy = c.p(x, y, 1.6)
        c.mezclar(c.mascara(elipse=[cx - 10, cy - 14, cx + 10, cy + 14]), np.array([0.2, 0.3, 0.38]))
        c.mezclar(c.mascara(elipse=[cx - 6, cy - 10, cx + 2, cy - 2]), np.array([0.7, 0.85, 0.95]), 0.6)
    c.caja(3.6, -1.2, 0, 1.8, 1.6, 2.0, (0.85, 0.85, 0.83), "metal")          # esclusa
    c.poligono([(4.5, -1.6, 0.1), (4.5, -0.8, 0.1), (4.5, -0.8, 1.7), (4.5, -1.6, 1.7)], (0.35, 0.37, 0.4), "metal", 0.8)


def modulo_domo_cultivo(c):
    c.sombra(0, 0, 5.0, 2.5, 0.4)
    c.cilindro(0, 0, 0, 3.6, 0.6, (0.7, 0.68, 0.64), "metal")
    rng = np.random.default_rng(5)
    for _ in range(26):  # plantas dentro
        a, r = rng.uniform(0, 6.28), rng.uniform(0, 2.8)
        c.esfera(np.cos(a) * r, np.sin(a) * r, 0.9, rng.uniform(0.3, 0.55), (0.2 + rng.uniform(0, 0.15), 0.55 + rng.uniform(0, 0.2), 0.2))
    c.esfera(0, 0, 0.6, 3.6, VIDRIO, cristal=True, corte=0.02)
    cx, cy = c.p(0, 0, 0.6)
    R = 3.6 * c.k
    for a in np.linspace(0.1, 0.9, 6):
        img = Image.new("L", (S, S), 0)
        ImageDraw.Draw(img).arc([cx - R * a, cy - R, cx + R * a, cy + R], 180, 360, fill=255, width=3)
        c.mezclar(np.asarray(img, np.float32) / 255, np.array([0.85, 0.87, 0.9]), 0.6)


def modulo_planta_isru(c):
    c.sombra(0, 0, 5.0, 2.4, 0.5)
    c.caja(0, 0, 0, 5, 3.2, 2.4, (0.78, 0.78, 0.76), "metal", juntas=0)
    franjas_y(c, -2.5, 2.5, 0, 0.3, 1.601, 16)
    c.cilindro(-1.4, -0.4, 2.4, 0.7, 1.8, BLANCO, bandas=[(1.2, 0.2, VERDE)])
    c.cilindro(0.4, -0.4, 2.4, 0.7, 1.8, BLANCO, bandas=[(1.2, 0.2, (0.2, 0.45, 0.9))])
    tuberia(c, (-1.4, 0.4, 3.6), (1.8, 0.4, 3.6))
    c.cilindro(2.0, 0.8, 2.4, 0.25, 2.6, OSCURO)
    ventana_x(c, -0.8, 0.8, 0.8, 1.8, 2.501)


def modulo_taller_robotica(c):
    c.sombra(0, 0, 5.4, 2.6, 0.5)
    c.caja(0, 0, 0, 6, 4.4, 3.6, (0.82, 0.82, 0.8), "metal")
    for i in range(12):
        z0 = i * 0.22
        col = (0.55, 0.57, 0.6) if i % 2 else (0.48, 0.5, 0.53)
        c.poligono([(-1.6, 2.201, z0), (1.6, 2.201, z0), (1.6, 2.201, z0 + 0.22), (-1.6, 2.201, z0 + 0.22)], col, "metal", 1.0)
    franjas_y(c, -2.0, 2.0, 2.7, 3.0, 2.202, 14)
    c.cilindro(2.0, -1.2, 3.6, 0.06, 1.6, TITANIO)
    disco(c, 2.0, -1.2, 5.2, 0.7, (0.92, 0.92, 0.92))


# ------------------------------------------------------------------ equipos de la base

def equipo_solar(c):
    c.sombra(0, 0, 2.6, 1.3, 0.4)
    for x in (-0.8, 0.8):
        c.cilindro(x, 0, 0, 0.08, 1.2, METAL)
    top = [(-1.8, -1.1, 0.8), (1.8, -1.1, 0.8), (1.8, 1.1, 1.8), (-1.8, 1.1, 1.8)]
    c.poligono(top, AZUL_CEL, "pintura", 1.0)
    for i in range(1, 7):
        t = i / 7
        c.linea([c.p(-1.8 + 3.6 * t, -1.1, 0.8), c.p(-1.8 + 3.6 * t, 1.1, 1.8)], (0.75, 0.78, 0.86), 1.5, 0.7)
    for i in range(1, 4):
        t = i / 4
        c.linea([c.p(-1.8, -1.1 + 2.2 * t, 0.8 + t), c.p(1.8, -1.1 + 2.2 * t, 0.8 + t)], (0.75, 0.78, 0.86), 1.5, 0.7)
    c.linea([c.p(*top[0]), c.p(*top[1]), c.p(*top[2]), c.p(*top[3]), c.p(*top[0])], (0.85, 0.86, 0.9), 3, 0.9)
    c.linea([c.p(-1.2, 0.6, 1.6), c.p(0.2, -0.6, 1.0)], (1, 1, 1), 6, 0.18)


def equipo_battery(c):
    c.sombra(0, 0, 2.0, 1.0, 0.5)
    c.caja(0, 0, 0, 2.4, 1.4, 1.8, (0.2, 0.5, 0.3), "pintura")
    for i in range(4):
        c.linea([c.p(-1.0 + i * 0.6, 0.701, 0.2), c.p(-1.0 + i * 0.6, 0.701, 1.6)], (0.12, 0.3, 0.18), 3, 0.9)
    c.poligono([(1.201, -0.4, 1.0), (1.201, 0.4, 1.0), (1.201, 0.4, 1.5), (1.201, -0.4, 1.5)], (0.1, 0.12, 0.1), "pintura", 1)
    c.poligono([(1.202, -0.3, 1.1), (1.202, 0.2, 1.1), (1.202, 0.2, 1.4), (1.202, -0.3, 1.4)], (0.3, 1, 0.45), "pintura", 1)
    c.cilindro(-0.5, 0, 1.8, 0.14, 0.2, (0.85, 0.2, 0.15))
    c.cilindro(0.5, 0, 1.8, 0.14, 0.2, OSCURO)


def equipo_reactor(c):
    c.sombra(0, 0, 2.8, 1.4, 0.5)
    for i, a in enumerate(np.linspace(0, 2 * np.pi, 7)[:-1]):    # aletas radiadoras
        x, y = np.cos(a) * 1.5, np.sin(a) * 1.5
        c.caja(x, y, 0.6, 0.12 if abs(np.cos(a)) > 0.5 else 1.0, 1.0 if abs(np.cos(a)) > 0.5 else 0.12, 2.0, (0.55, 0.57, 0.6), "metal")
    c.cilindro(0, 0, 0, 0.95, 3.0, (0.75, 0.76, 0.78), bandas=[(2.2, 0.35, (0.95, 0.78, 0.1))])
    c.esfera(0, 0, 3.0, 0.95, (0.75, 0.76, 0.78), corte=0.0)


def equipo_oxygen(c):
    c.sombra(0, 0, 2.2, 1.1, 0.5)
    c.caja(0, 0, 0, 2.2, 1.6, 1.6, (0.86, 0.86, 0.84), "metal")
    c.cilindro(0.5, -0.2, 1.6, 0.45, 1.4, BLANCO, bandas=[(0.8, 0.2, VERDE)])
    tuberia(c, (-0.6, 0.2, 1.6), (-0.6, 0.2, 2.6))
    tuberia(c, (-0.6, 0.2, 2.6), (0.2, 0.2, 2.6))
    ventana_x(c, -0.5, 0.5, 0.6, 1.2, 1.101)
    luz_verde(c, -0.8, 0.85, 1.3, 0.09)


def equipo_icedrill(c):
    c.sombra(0, 0, 2.0, 1.0, 0.5)
    base = 0.0
    for (x, y) in ((-1.1, 1.1), (1.2, 0.6), (0.2, -1.2)):
        c.linea([c.p(x, y, base), c.p(0, 0, 3.6)], TITANIO, 7, 1)
    c.caja(0, 0, 2.6, 0.7, 0.7, 0.8, NARANJA, "pintura")
    c.cilindro(0, 0, 0, 0.12, 2.6, (0.5, 0.5, 0.55))
    c.cilindro(0, 0, 0, 0.6, 0.15, (0.88, 0.92, 0.98))   # hielo extraído


def equipo_condenser(c):
    c.sombra(0, 0, 2.4, 1.2, 0.5)
    c.caja(0, 0, 0, 2.6, 2.0, 1.4, (0.8, 0.81, 0.8), "metal")
    for (x, y) in ((-0.6, 0), (0.6, 0)):
        cx, cy = c.p(x, y, 1.4)
        R = 0.48 * c.k
        c.mezclar(c.mascara(elipse=[cx - R, cy - R * 0.5, cx + R, cy + R * 0.5]), np.array([0.2, 0.21, 0.23]))
        for a in np.linspace(0, np.pi, 4):
            c.linea([(cx - R * np.cos(a), cy - R * 0.5 * np.sin(a)), (cx + R * np.cos(a), cy + R * 0.5 * np.sin(a))], (0.6, 0.62, 0.65), 2, 0.8)
    for i in range(6):
        c.linea([c.p(-1.3 + i * 0.5, 1.001, 0.25), c.p(-1.3 + i * 0.5, 1.001, 1.15)], (0.45, 0.46, 0.5), 2, 0.8)
    c.cilindro(1.6, 0.4, 0, 0.35, 1.0, (0.6, 0.75, 0.95))


def equipo_o2tank(c):
    c.sombra(0, 0, 1.6, 0.8, 0.5)
    c.cilindro(0, 0, 0, 0.9, 2.8, BLANCO, bandas=[(1.7, 0.35, VERDE), (0.15, 0.1, (0.6, 0.6, 0.62))])
    c.esfera(0, 0, 2.8, 0.9, BLANCO)
    c.cilindro(0, 0, 3.5, 0.12, 0.3, TITANIO)


def equipo_watertank(c):
    c.sombra(0, 0, 1.8, 0.9, 0.5)
    c.cilindro(0, 0, 0, 1.1, 2.4, (0.85, 0.88, 0.9), bandas=[(1.5, 0.35, (0.2, 0.5, 0.95))])
    c.esfera(0, 0, 2.4, 1.1, (0.85, 0.88, 0.9))
    tuberia(c, (1.0, 0.6, 0.4), (2.0, 0.6, 0.4), TITANIO, 5)


def equipo_printer(c):
    c.sombra(0, 0, 3.0, 1.5, 0.45)
    c.caja(0.1, 0.2, 0, 2.6, 0.5, 1.4, REG, "regolito", juntas=5)      # muro a medio imprimir
    for (x, y) in ((-1.8, 1.4), (1.8, 1.4), (1.8, -1.0), (-1.8, -1.0)):
        c.caja(x, y, 0, 0.18, 0.18, 2.8, NARANJA, "pintura")
    c.caja(0, 1.4, 2.8, 3.8, 0.18, 0.18, NARANJA, "pintura")
    c.caja(0, -1.0, 2.8, 3.8, 0.18, 0.18, NARANJA, "pintura")
    c.caja(0.4, 0.2, 2.6, 0.18, 2.6, 0.2, OSCURO, "metal")
    c.caja(0.4, 0.2, 1.6, 0.3, 0.3, 1.0, (0.3, 0.3, 0.33), "metal")      # cabezal
    c.esfera(0.4, 0.2, 1.5, 0.12, (1, 0.6, 0.2))


def equipo_wall(c):
    c.sombra(0, 0, 3.0, 1.0, 0.45)
    c.caja(0, 0, 0, 5, 0.9, 2.0, REG, "regolito", juntas=4)


def equipo_floor(c):
    c.sombra(0, 0, 3.4, 1.7, 0.3)
    c.caja(0, 0, 0, 4.4, 4.4, 0.25, REG, "regolito")
    rejilla_superior(c, -2.2, 2.2, -2.2, 2.2, 0.25, 5, (0.35, 0.2, 0.13), 0.55)


def equipo_shelter(c):
    c.sombra(0, 0, 4.2, 2.1, 0.45)
    c.esfera(0, 0, 0, 3.3, REG, corte=0.0, tipo="regolito")          # cúpula enterrada en regolito
    cx, cy = c.p(0, 0, 0)
    R = 3.3 * c.k
    for k in range(1, 6):                                             # hileras de sacos de regolito
        h = k / 6.0
        w = (1 - h * h) ** 0.5
        img = Image.new("L", (S, S), 0)
        ImageDraw.Draw(img).arc([cx - R * w, cy - R * h - R * 0.5 * w * 0.2, cx + R * w, cy - R * h + R * 0.5 * w * 0.2], 0, 180, fill=255, width=3)
        c.mezclar(np.asarray(img, np.float32) / 255, np.array([0.3, 0.17, 0.11]), 0.5)
    c.caja(2.3, 1.6, 0, 1.6, 1.3, 1.9, (0.78, 0.79, 0.8), "metal")     # entrada
    c.poligono([(3.101, 1.15, 0.05), (3.101, 2.05, 0.05), (3.101, 2.05, 1.6), (3.101, 1.15, 1.6)], (0.32, 0.34, 0.37), "metal", 0.8)
    franjas_y(c, 1.5, 3.1, 1.6, 1.85, 2.251, 8)


def equipo_pad(c):
    c.sombra(0, 0, 4.4, 2.2, 0.25)
    c.cilindro(0, 0, 0, 4.0, 0.25, (0.55, 0.55, 0.56), "metal")
    anillo(c, 0, 0, 0.26, 3.2, (0.95, 0.78, 0.1), 7, 0.95)
    anillo(c, 0, 0, 0.26, 1.2, (0.95, 0.95, 0.95), 5, 0.9)
    for a in np.linspace(0, 2 * np.pi, 7)[:-1]:
        c.esfera(np.cos(a) * 3.7, np.sin(a) * 3.7, 0.4, 0.15, (0.4, 1, 0.5))


def equipo_lab(c):
    c.sombra(0, 0, 3.4, 1.7, 0.5)
    c.caja(0, 0, 0, 4.0, 2.8, 2.4, BLANCO, "metal")
    franjas_y(c, -2, 2, 2.2, 2.4, 1.401, 1)
    c.poligono([(-2, 1.402, 2.2), (2, 1.402, 2.2), (2, 1.402, 2.4), (-2, 1.402, 2.4)], (0.2, 0.45, 0.9), "pintura", 1)
    ventana_y(c, -1.5, -0.1, 0.9, 1.8, 1.403)
    ventana_y(c, 0.3, 1.7, 0.9, 1.8, 1.403)
    ventana_x(c, -0.6, 0.6, 0.9, 1.8, 2.001)
    c.cilindro(1.2, -0.8, 2.4, 0.05, 0.9, TITANIO)
    disco(c, 1.2, -0.8, 3.3, 0.45, (0.95, 0.95, 0.95))


def equipo_antenna(c):
    c.sombra(0, 0, 2.2, 1.1, 0.5)
    c.caja(0, 0, 0, 1.2, 1.2, 0.4, OSCURO, "metal")
    c.cilindro(0, 0, 0.4, 0.15, 2.6, TITANIO)
    disco(c, 0, 0, 3.4, 1.8, (0.95, 0.95, 0.95), 0.62)
    c.linea([c.p(0, 0, 3.4), c.p(0, 0, 4.4)], (0.4, 0.4, 0.44), 4, 1)
    c.esfera(0, 0, 4.45, 0.13, (1, 0.35, 0.25))


def equipo_beacon(c):
    c.sombra(0, 0, 1.2, 0.6, 0.45)
    c.caja(0, 0, 0, 0.8, 0.8, 0.3, OSCURO, "metal")
    c.cilindro(0, 0, 0.3, 0.07, 3.8, TITANIO, bandas=[(1.5, 0.2, NARANJA), (2.6, 0.2, NARANJA)])
    c.poligono([(-0.6, 0.1, 2.0), (0.4, 0.1, 2.0), (0.4, 0.1, 2.6), (-0.6, 0.1, 2.6)], AZUL_CEL, "pintura", 1)
    c.esfera(0, 0, 4.15, 0.22, (0.35, 1, 0.5))
    cx, cy = c.p(0, 0, 4.15)
    dx, dy = c.xx - cx, c.yy - cy
    halo = np.exp(-(dx * dx + dy * dy) / (2 * (0.6 * c.k) ** 2))
    c.rgb = c.rgb + halo[..., None] * np.array([0.2, 0.5, 0.25]) * 0.8


def equipo_refinery(c):
    modulo_planta_isru(c)


# ------------------------------------------------------------------ lista

DIBUJOS = [
    # clave, función, escala (px por metro a 512), semilla
    ("pieza_cimiento", pieza_cimiento, 44, 11), ("pieza_losa_piso", pieza_losa_piso, 40, 12),
    ("pieza_rejilla", pieza_rejilla, 44, 13), ("pieza_pared_regolito", pieza_pared_regolito, 50, 14),
    ("pieza_pared_ventana", pieza_pared_ventana, 50, 15), ("pieza_panel_presurizado", pieza_panel_presurizado, 50, 16),
    ("pieza_murete", pieza_murete, 66, 17), ("pieza_panel_cristal", pieza_panel_cristal, 50, 18),
    ("pieza_techo_cristal", pieza_techo_cristal, 40, 19), ("pieza_claraboya", pieza_claraboya, 46, 20),
    ("pieza_puerta_presurizada", pieza_puerta_presurizada, 50, 21), ("pieza_porton_rover", pieza_porton_rover, 46, 22),
    ("pieza_techo_regolito", pieza_techo_regolito, 40, 23), ("pieza_pilar", pieza_pilar, 62, 24),
    ("pieza_escalera", pieza_escalera, 48, 25),
    ("modulo_habitat_tripulacion", modulo_habitat_tripulacion, 30, 31), ("modulo_domo_cultivo", modulo_domo_cultivo, 34, 32),
    ("modulo_planta_isru", modulo_planta_isru, 34, 33), ("modulo_taller_robotica", modulo_taller_robotica, 30, 34),
    ("equipo_solar", equipo_solar, 56, 41), ("equipo_battery", equipo_battery, 70, 42),
    ("equipo_reactor", equipo_reactor, 52, 43), ("equipo_oxygen", equipo_oxygen, 62, 44),
    ("equipo_icedrill", equipo_icedrill, 66, 45), ("equipo_condenser", equipo_condenser, 60, 46),
    ("equipo_o2tank", equipo_o2tank, 62, 47), ("equipo_watertank", equipo_watertank, 58, 48),
    ("equipo_printer", equipo_printer, 50, 49), ("equipo_wall", equipo_wall, 56, 50),
    ("equipo_floor", equipo_floor, 42, 51), ("equipo_shelter", equipo_shelter, 40, 52),
    ("equipo_pad", equipo_pad, 34, 53), ("equipo_lab", equipo_lab, 46, 54),
    ("equipo_antenna", equipo_antenna, 58, 55), ("equipo_beacon", equipo_beacon, 66, 56),
    ("equipo_refinery", equipo_refinery, 34, 57),
]


def main():
    os.makedirs(SALIDA, exist_ok=True)
    solo = set(sys.argv[1:])
    for clave, fn, escala, seed in DIBUJOS:
        if solo and clave not in solo:
            continue
        c = nuevo(seed, escala)
        fn(c)
        c.guardar(os.path.join(SALIDA, clave + ".png"))
        print(clave)
    print("Listo:", SALIDA)


if __name__ == "__main__":
    main()
