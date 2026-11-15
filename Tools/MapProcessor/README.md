# Procesador del mapa de Marte

`generate_mars_chunks.py` prepara el terreno de las 6 zonas de aterrizaje y lo deja en
`Assets/StreamingAssets/MarsMap/<zona>/`. Al pulsar Play, Unity (`MarsTerrainGenerator`) lo
detecta y lo usa. Si una zona no tiene datos, el juego sigue usando el terreno procedural.

## 1. Instalar Python (una vez)

1. Descarga **Python 3.12** de https://www.python.org/downloads/windows/ (instalador de 64 bits).
2. En el instalador marca **"Add python.exe to PATH"** y pulsa *Install Now*.
3. Abre una terminal nueva y comprueba: `python --version`.

> Si `python` abre la Microsoft Store: Configuración ▸ Aplicaciones ▸ Configuración avanzada de
> aplicaciones ▸ Alias de ejecución de aplicaciones ▸ desactiva los dos de "python".

## 2. Instalar dependencias

Desde la carpeta `Tools\MapProcessor`:

```
python -m pip install -r requirements.txt
```

| Paquete | Para qué | ¿Obligatorio? |
|---|---|---|
| numpy, Pillow | cálculo y PNG | sí |
| requests | Mars Trek (CTX) y API ODE (HiRISE, CRISM) | solo para datos reales |
| rasterio | leer MOLA y DTM HiRISE por ventanas (trae GDAL) | solo para MOLA/HiRISE |

## 3. Ejecutar

Cierra Unity antes (el script usa unos 300–500 MB de RAM).

```
# Sin internet: las 6 zonas con simulación geológica fractal (≈1 min)
python generate_mars_chunks.py --mode synthetic

# Con datos reales donde se pueda (y simulación donde falle), una zona
python generate_mars_chunks.py --zones jezero --mode auto

# Todas las zonas con datos reales
python generate_mars_chunks.py --mode auto
```

Opciones útiles: `--chunks 8` (chunks por lado), `--meters-per-sample 4` (4 m → zona de 8,16 km),
`--no-hirise`, `--no-crism`, `--timeout 60`, `--max-tiles 300`.

Después abre Unity y pulsa Play en `Prueba`: el panel dirá `Mapa: real: MOLA…` o
`Mapa: synthetic…`, y mostrará minerales y radiación bajo el robot.

## Qué sale de cada fuente

| Capa | Fuente real | Si falla |
|---|---|---|
| Elevación | MOLA 463 m (USGS), + detalle fractal por debajo de 463 m | geología fractal por zona |
| Textura | CTX 5 m vía NASA Mars Trek (WMTS) | sombreado del relieve |
| Micro-relieve y rocas | DTM HiRISE localizado con ODE (solo si cubre la zona) | ruido fractal |
| Minerales | ODE dice dónde hay observaciones CRISM; las concentraciones son índices de juego según la geología publicada | igual, sin CRISM |
| Clima y radiación | perfiles medios (NASA Ames MCD, Curiosity RAD 0,21 mGy/día en Gale) ajustados por altitud | igual |

Limitaciones honestas:

- Con 8 km por zona, MOLA (463 m/píxel) solo aporta la forma grande (unos 18×18 píxeles). El
  realismo de cerca viene de CTX, de HiRISE si hay DTM y del detalle fractal.
- Los DTM de HiRISE cubren franjas pequeñas: la mayoría de zonas no tendrá uno exacto.
- Las concentraciones de minerales NO son medidas: calcularlas exige procesar cubos espectrales
  CRISM de varios GB por escena.
- Los catálogos de NASA Ames y PDS-PPI solo se comprueban; no se descargan (son netCDF/tablas grandes).
- Las teselas descargadas se guardan en `cache/` para no volver a pedirlas.
