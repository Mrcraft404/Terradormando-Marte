"""
Pintor por capas de las imágenes de las estructuras (menú de construcción del juego).

Cada imagen se compone capa a capa, de atrás hacia delante, como una ilustración:
  1. cielo de Marte (degradado)   2. suelo con grano y piedras   3. sombra de contacto
  4. el objeto: caras con luz, textura (regolito, metal, cristal…), oclusión en la base y
     brillos en los bordes   5. viñeta suave.
Se pinta al doble de tamaño y se reduce (bordes suaves). Vista isométrica, luz del noroeste.

Uso:  python Tools/StructureArt/generar_imagenes.py
"""
import math

import numpy as np
from PIL import Image, ImageDraw

S = 512           # tamaño de trabajo (se guarda a la mitad: 256 px)
COS30, SIN30 = math.cos(math.radians(30)), math.sin(math.radians(30))


def _noise(shape, scale, seed):
    """Ruido suave: rejilla aleatoria ampliada con interpolación bicúbica."""
    rng = np.random.default_rng(seed)
    h, w = shape
    small = rng.random((max(2, int(h / scale)) + 2, max(2, int(w / scale)) + 2))
    img = Image.fromarray((small * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    return np.asarray(img, dtype=np.float32) / 255.0


def fbm(seed, scales=(64, 24, 8, 3), weights=(0.45, 0.3, 0.17, 0.08)):
    out = np.zeros((S, S), np.float32)
    for i, (sc, wt) in enumerate(zip(scales, weights)):
        out += _noise((S, S), sc, seed * 31 + i) * wt
    return out


class Lienzo:
    def __init__(self, seed=1, escala=60.0, centro=(0.5, 0.66), horizonte=0.42):
        self.rgb = np.zeros((S, S, 3), np.float32)
        self.seed = seed
        self.k = escala * S / 512.0          # píxeles por metro
        self.cx, self.cy = centro[0] * S, centro[1] * S
        self.horizonte = horizonte
        self.ruido = fbm(seed)
        self.grano = _noise((S, S), 1.5, seed + 7)
        yy, xx = np.mgrid[0:S, 0:S]
        self.xx, self.yy = xx.astype(np.float32), yy.astype(np.float32)

    # ---------------- proyección ----------------
    def p(self, x, y, z):
        """Mundo (x este, y norte, z arriba) → pantalla isométrica."""
        sx = self.cx + (x - y) * COS30 * self.k
        sy = self.cy + (x + y) * SIN30 * self.k - z * self.k
        return (sx, sy)

    # ---------------- capas base ----------------
    def fondo(self):
        h = self.horizonte * S
        t = np.clip(self.yy / max(h, 1), 0, 1)[..., None]
        cielo_alto = np.array([0.62, 0.50, 0.40])
        cielo_bajo = np.array([0.90, 0.72, 0.55])
        cielo = cielo_alto * (1 - t) + cielo_bajo * t
        # Suelo: óxido con manchas y grano, más oscuro cerca.
        tg = np.clip((self.yy - h) / (S - h), 0, 1)[..., None]
        suelo_lejos = np.array([0.74, 0.47, 0.32])
        suelo_cerca = np.array([0.52, 0.29, 0.18])
        suelo = suelo_lejos * (1 - tg) + suelo_cerca * tg
        suelo = suelo * (0.82 + 0.3 * self.ruido[..., None]) * (0.92 + 0.12 * self.grano[..., None])
        es_cielo = (self.yy < h)[..., None]
        self.rgb = np.where(es_cielo, cielo, suelo).astype(np.float32)
        # Bruma en el horizonte.
        bruma = np.exp(-((self.yy - h) / (S * 0.05)) ** 2)[..., None]
        self.rgb = self.rgb * (1 - 0.35 * bruma) + np.array([0.93, 0.76, 0.6]) * 0.35 * bruma
        # Piedrecitas.
        rng = np.random.default_rng(self.seed + 3)
        d = ImageDraw.Draw(img := Image.new("L", (S, S), 0))
        for _ in range(55):
            x, y = rng.uniform(0, S), rng.uniform(h + 8, S)
            r = rng.uniform(1.5, 5.5) * (0.4 + (y - h) / (S - h))
            d.ellipse([x - r * 1.4, y - r, x + r * 1.4, y + r], fill=255)
        m = np.asarray(img, np.float32)[..., None] / 255
        self.rgb = self.rgb * (1 - m * 0.45) + np.array([0.3, 0.17, 0.11]) * m * 0.45

    def vineta(self):
        dx, dy = (self.xx - S / 2) / (S / 2), (self.yy - S / 2) / (S / 2)
        v = np.clip(1 - 0.28 * (dx * dx + dy * dy), 0.6, 1)[..., None]
        self.rgb *= v

    # ---------------- utilidades ----------------
    def mascara(self, poligono=None, elipse=None):
        img = Image.new("L", (S, S), 0)
        d = ImageDraw.Draw(img)
        if poligono is not None:
            d.polygon([tuple(map(float, q)) for q in poligono], fill=255)
        if elipse is not None:
            d.ellipse([float(v) for v in elipse], fill=255)
        return np.asarray(img, np.float32) / 255.0

    def mezclar(self, mascara, color, alfa=1.0):
        a = (mascara * alfa)[..., None]
        if np.ndim(color) == 1:
            color = np.asarray(color, np.float32)
        self.rgb = self.rgb * (1 - a) + color * a

    def sombra(self, x, y, rx, ry, fuerza=0.5, dx=0.35, dy=-0.25):
        """Sombra suave en el suelo, desplazada hacia el sureste (luz del noroeste)."""
        cx, cy = self.p(x + dx, y + dy, 0)
        ex = (self.xx - cx) / (rx * self.k)
        ey = (self.yy - cy) / (ry * self.k)
        e = np.clip(1 - (ex * ex + ey * ey), 0, 1) ** 1.5
        self.rgb *= (1 - fuerza * e)[..., None]

    # ---------------- texturas ----------------
    def textura(self, tipo, color):
        c = np.asarray(color, np.float32)
        if tipo == "regolito":
            n = 0.78 + 0.35 * self.ruido + 0.18 * (self.grano - 0.5)
            return c * n[..., None]
        if tipo == "metal":
            vetas = _noise((S, S), 1.0, self.seed + 11)
            vetas = np.asarray(Image.fromarray((vetas * 255).astype(np.uint8)).resize((S, 4)).resize((S, S)), np.float32) / 255
            n = 0.92 + 0.12 * vetas + 0.06 * (self.ruido - 0.5)
            return c * n[..., None]
        if tipo == "pintura":
            n = 0.95 + 0.08 * (self.ruido - 0.5)
            return c * n[..., None]
        return np.broadcast_to(c, (S, S, 3))

    # ---------------- primitivas ----------------
    def caja(self, x, y, z, sx, sy, sz, color, tipo="metal", juntas=0, brillo=0.25, ao=True):
        """Prisma: caras visibles (arriba, oeste→ izquierda, sur → derecha) con su luz."""
        P = self.p
        x0, x1, y0, y1, z0, z1 = x - sx / 2, x + sx / 2, y - sy / 2, y + sy / 2, z, z + sz
        arriba = [P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1)]
        # Caras visibles desde la cámara: la de y máxima (a la izquierda) y la de x máxima (derecha).
        izq = [P(x0, y1, z0), P(x1, y1, z0), P(x1, y1, z1), P(x0, y1, z1)]
        der = [P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1)]
        tex = self.textura(tipo, color)
        for pol, luz in ((izq, 0.95), (der, 0.62), (arriba, 1.18)):
            m = self.mascara(pol)
            col = tex * luz
            if ao and pol is not arriba:
                # Oclusión: más oscuro abajo (donde toca el suelo).
                yb = max(q[1] for q in pol)
                g = np.clip(1 - (yb - self.yy) / (sz * self.k + 1e-3), 0, 1)
                col = col * (1 - 0.35 * g ** 3)[..., None]
            self.mezclar(m, col)
        # Juntas de bloques (regolito) o paneles (metal).
        if juntas > 0:
            linea = np.array([0.18, 0.1, 0.07]) if tipo == "regolito" else np.array([0.25, 0.27, 0.3])
            for i in range(1, juntas):
                t = i / juntas
                zz = z0 + sz * t
                self.linea([P(x0, y1, zz), P(x1, y1, zz)], linea, 1.6, 0.6)
                self.linea([P(x1, y0, zz), P(x1, y1, zz)], linea, 1.6, 0.6)
                # Juntas verticales al tresbolillo (como bloques).
                desfase = 0.5 if i % 2 else 0.0
                n = max(2, int(sx / 0.9))
                for j in range(n):
                    xx_ = x0 + sx * ((j + desfase) / n)
                    if x0 < xx_ < x1:
                        self.linea([P(xx_, y1, zz - sz / juntas), P(xx_, y1, zz)], linea, 1.2, 0.4)
        # Brillo en las aristas superiores.
        self.linea([P(x0, y1, z1), P(x1, y1, z1), P(x1, y0, z1)], (1, 0.97, 0.9), 2.0, brillo)

    def linea(self, puntos, color, ancho, alfa):
        img = Image.new("L", (S, S), 0)
        ImageDraw.Draw(img).line([tuple(map(float, q)) for q in puntos], fill=255, width=max(1, int(ancho * S / 512)))
        self.mezclar(np.asarray(img, np.float32) / 255.0, np.asarray(color, np.float32), alfa)

    def cilindro(self, x, y, z, r, h, color, tipo="metal", bandas=(), tapa=None):
        """Cilindro vertical con sombreado redondo."""
        cx, cyb = self.p(x, y, z)
        _, cyt = self.p(x, y, z + h)
        rx = r * self.k * 1.0
        ry = r * self.k * 0.5
        cuerpo = self.mascara(poligono=[(cx - rx, cyt), (cx + rx, cyt), (cx + rx, cyb), (cx - rx, cyb)])
        cuerpo = np.maximum(cuerpo, self.mascara(elipse=[cx - rx, cyb - ry, cx + rx, cyb + ry]))
        u = np.clip((self.xx - cx) / rx, -1, 1)
        luz = 0.55 + 0.55 * np.clip(np.sqrt(np.clip(1 - u * u, 0, 1)) * 0.8 - u * 0.45, 0, 1.1)
        brillo = np.exp(-((u + 0.45) / 0.12) ** 2) * 0.35
        tex = self.textura(tipo, color) * luz[..., None] + brillo[..., None]
        self.mezclar(cuerpo, tex)
        for (zb, hb, cb) in bandas:
            _, t0 = self.p(x, y, z + zb)
            _, t1 = self.p(x, y, z + zb + hb)
            band = self.mascara(poligono=[(cx - rx, t1), (cx + rx, t1), (cx + rx, t0), (cx - rx, t0)])
            band = np.maximum(band, self.mascara(elipse=[cx - rx, t0 - ry, cx + rx, t0 + ry])) * cuerpo
            self.mezclar(band, np.asarray(cb, np.float32) * luz[..., None])
        top = self.mascara(elipse=[cx - rx, cyt - ry, cx + rx, cyt + ry])
        tcol = np.asarray(tapa if tapa is not None else color, np.float32) * 1.12
        self.mezclar(top, self.textura(tipo, tcol))
        self.linea([(cx - rx * 0.8, cyt - ry * 0.4), (cx, cyt - ry)], (1, 1, 1), 2, 0.25)

    def esfera(self, x, y, z, r, color, cristal=False, corte=None, tipo=None):
        """Cúpula o esfera (corte = fracción inferior que no se ve)."""
        cx, cy = self.p(x, y, z)
        R = r * self.k
        dx, dy = (self.xx - cx) / R, (self.yy - cy) / R
        d2 = dx * dx + dy * dy
        m = np.clip((1 - d2) * R / 2, 0, 1)
        if corte is not None:
            m = m * (dy < corte)   # corte = 0: solo la mitad de arriba (cúpula)
        nz = np.sqrt(np.clip(1 - d2, 0, 1))
        luz = np.clip(-0.55 * dx - 0.6 * dy + 0.6 * nz, 0, 1)
        spec = np.clip(-0.5 * dx - 0.55 * dy + 0.67 * nz, 0, 1) ** 30
        base = self.textura(tipo, color) if tipo else np.asarray(color, np.float32)
        col = base * (0.35 + 0.8 * luz)[..., None] + spec[..., None] * (0.15 if tipo == "regolito" else 0.8)
        if cristal:
            fondo = self.rgb.copy()
            vidrio = fondo * 0.55 + np.array([0.45, 0.62, 0.72]) * 0.45
            col = vidrio * (0.75 + 0.35 * luz)[..., None] + spec[..., None] * 0.9
        self.mezclar(m, col)

    def poligono(self, puntos3d, color, tipo="pintura", luz=1.0, alfa=1.0):
        pol = [self.p(*q) for q in puntos3d]
        self.mezclar(self.mascara(pol), self.textura(tipo, np.asarray(color) * luz), alfa)
        return pol

    def guardar(self, ruta):
        self.vineta()
        img = Image.fromarray((np.clip(self.rgb, 0, 1) * 255).astype(np.uint8), "RGB")
        img.resize((S // 2, S // 2), Image.LANCZOS).save(ruta)
