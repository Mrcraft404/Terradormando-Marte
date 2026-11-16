"""
Genera la voz de Marti (la mascota guía) sin internet, con Piper.

Lee las frases de Marti de Assets/Scripts/Localization/Localizer.cs (claves intro_*, marti_* y tut_*),
las dice con Piper en español y en inglés y las pone más agudas para que suenen entre niño y
niña. Guarda un WAV por frase en Assets/Resources/Mascot/Voice/<es|en>/<clave>.wav.

Cómo se hace la voz "de niño/a" sin cambiar la velocidad:
  1) Piper habla más despacio (length_scale = TONO).
  2) Se reproduce TONO veces más rápido (remuestreo): sube el tono y la velocidad vuelve a la normal.

Uso:
  python Tools/VoiceGen/generar_voz_marti.py "<carpeta de piper>"
La carpeta de Piper debe tener piper/piper.exe y los modelos es_MX-claude-high.onnx y
en_US-amy-medium.onnx (con sus .onnx.json). Volver a ejecutarlo al añadir frases nuevas.
"""
import io
import os
import re
import subprocess
import sys
import tempfile
import wave

import numpy as np

TONO = 1.22          # 1.0 = voz original; 1.2–1.3 = voz infantil
VOCES = {"es": "es_MX-claude-high.onnx", "en": "en_US-amy-medium.onnx"}

AQUI = os.path.dirname(os.path.abspath(__file__))
PROYECTO = os.path.normpath(os.path.join(AQUI, "..", ".."))
LOCALIZER = os.path.join(PROYECTO, "Assets", "Scripts", "Localization", "Localizer.cs")
SALIDA = os.path.join(PROYECTO, "Assets", "Resources", "Mascot", "Voice")


def frases():
    texto = io.open(LOCALIZER, encoding="utf-8").read()
    patron = re.compile(r'\{ "((?:intro_|marti_|tut_)[a-z0-9_]+)", \("(.*?)", "(.*?)"\) \},')
    for clave, en, es in patron.findall(texto):
        if clave.startswith("intro_opt"):
            continue   # son las respuestas del jugador, no las dice Marti
        yield clave, {"en": en, "es": es}


def leer_wav(ruta):
    with wave.open(ruta, "rb") as w:
        rate = w.getframerate()
        datos = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32)
    return datos, rate


def escribir_wav(ruta, datos, rate):
    datos = np.clip(datos, -32767, 32767).astype(np.int16)
    with wave.open(ruta, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(datos.tobytes())


def mas_agudo(datos, factor):
    """Remuestreo lineal: menos muestras a la misma frecuencia = más agudo y más rápido."""
    n = int(len(datos) / factor)
    x = np.linspace(0, len(datos) - 1, n)
    return np.interp(x, np.arange(len(datos)), datos)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    carpeta = sys.argv[1]
    piper = os.path.join(carpeta, "piper", "piper.exe")
    total = 0
    for clave, textos in frases():
        for idioma, texto in textos.items():
            destino = os.path.join(SALIDA, idioma)
            os.makedirs(destino, exist_ok=True)
            with tempfile.TemporaryDirectory() as tmp:
                crudo = os.path.join(tmp, "crudo.wav")
                subprocess.run(
                    [piper, "--model", os.path.join(carpeta, VOCES[idioma]), "--output_file", crudo,
                     "--length_scale", str(TONO), "--sentence_silence", "0.25"],
                    input=texto.encode("utf-8"), check=True, capture_output=True)
                datos, rate = leer_wav(crudo)
            datos = mas_agudo(datos, TONO)
            pico = np.max(np.abs(datos)) or 1.0
            datos = datos * (0.9 * 32767 / pico)          # mismo volumen en todas las frases
            escribir_wav(os.path.join(destino, clave + ".wav"), datos, rate)
            total += 1
            print(idioma, clave, "%.1f s" % (len(datos) / rate))
    print("Listo:", total, "audios en", SALIDA)


if __name__ == "__main__":
    main()
