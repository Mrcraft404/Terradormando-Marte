"""
Aplica la traducción al inglés del juego.

1. Lee lista_numerada.txt (textos en español, numerados por extraer_textos.py) y los
   ficheros ingles_*.txt ("número|traducción").
2. Genera Assets/Scripts/Localization/Translations.cs con la tabla español → inglés.
3. En los scripts, envuelve cada texto traducido: "Hola" → Tr.T("Hola"). Tr.T devuelve el
   inglés si en Ajustes está elegido English; si no, el texto en español tal cual.

Los espacios y saltos de línea (\\n) del principio y del final se copian del original, para que
las frases que se pegan con números ("Faltan " + n + " kg") sigan quedando bien.
Se puede ejecutar varias veces: no vuelve a envolver lo ya envuelto.

Uso: python Tools/Traduccion/aplicar_traduccion.py
"""
import glob
import io
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extraer_textos import CADENA, EXCLUIR_LINEA, SALTAR_ARCHIVOS, SCRIPTS  # noqa: E402

AQUI = os.path.dirname(os.path.abspath(__file__))
SALIDA_CS = os.path.join(SCRIPTS, "Localization", "Translations.cs")
BORDE = re.compile(r"^((?:\s|\\n)*)(.*?)((?:\s|\\n)*)$", re.S)


def cargar():
    lista = {}
    for n, linea in enumerate(io.open(os.path.join(AQUI, "lista_numerada.txt"), encoding="utf-8"), 1):
        linea = linea.rstrip("\n")
        if n == 1 or " | " not in linea:
            continue
        lista[n] = linea.split(" | ", 1)[1]
    tabla = {}
    for f in sorted(glob.glob(os.path.join(AQUI, "ingles_*.txt"))):
        for linea in io.open(f, encoding="utf-8"):
            linea = linea.rstrip("\n")
            if "|" not in linea:
                continue
            num, en = linea.split("|", 1)
            es = lista.get(int(num))
            if es is None:
                print("aviso: número desconocido", num, "en", os.path.basename(f))
                continue
            a, _, b = BORDE.match(es).groups()
            en = a + BORDE.match(en).group(2) + b
            if en != es:
                tabla[es] = en
    # Textos añadidos a mano (ya envueltos en el código): "español ⇒ inglés".
    extra = os.path.join(AQUI, "extra.txt")
    if os.path.exists(extra):
        for linea in io.open(extra, encoding="utf-8"):
            if " ⇒ " in linea:
                es, en = linea.strip().split(" ⇒ ", 1)
                tabla[es] = en
    return tabla


def generar_cs(tabla):
    lineas = [
        "// GENERADO por Tools/Traduccion/aplicar_traduccion.py: no editar a mano.",
        "// Tabla español → inglés de los textos del juego.",
        "using System.Collections.Generic;",
        "",
        "/// <summary>Traducción de textos: Tr.T(\"texto en español\") devuelve el inglés si así está en Ajustes.</summary>",
        "public static class Tr",
        "{",
        "    /// <summary>Texto en el idioma elegido en Ajustes (Opciones → Idioma).</summary>",
        "    public static string T(string es)",
        "    {",
        "        if (MarsRobot.Core.GameSettings.Language != 0 || es == null) return es;",
        "        string en;",
        "        return Table.TryGetValue(es, out en) ? en : es;",
        "    }",
        "",
        "    private static readonly Dictionary<string, string> Table = new Dictionary<string, string>",
        "    {",
    ]
    for es in sorted(tabla):
        lineas.append('        { "%s", "%s" },' % (es, tabla[es].replace('"', '\\"') if '\\"' not in tabla[es] else tabla[es]))
    lineas += ["    };", "}", ""]
    io.open(SALIDA_CS, "w", encoding="utf-8", newline="\n").write("\n".join(lineas))


def envolver(tabla):
    cambios = 0
    archivos = 0
    for raiz, _, nombres in os.walk(SCRIPTS):
        for f in nombres:
            if not f.endswith(".cs"):
                continue
            ruta = os.path.join(raiz, f)
            rel = os.path.relpath(ruta, SCRIPTS)
            if rel.startswith(SALTAR_ARCHIVOS):
                continue
            texto = io.open(ruta, encoding="utf-8").read()
            salida = []
            tocado = False
            en_atributo = False
            for linea in texto.split("\n"):
                cod = linea
                # Atributos de varias líneas ([Tooltip("..." +\n "...")]): no se pueden traducir.
                if re.match(r"\s*\[\w+\(", cod) and not cod.rstrip().endswith("]"):
                    en_atributo = True
                    salida.append(linea)
                    continue
                if en_atributo:
                    if cod.rstrip().endswith("]"):
                        en_atributo = False
                    salida.append(linea)
                    continue
                if cod.strip().startswith(("///", "//", "*")) or EXCLUIR_LINEA.search(cod):
                    salida.append(linea)
                    continue

                def cambiar(m):
                    nonlocal cambios, tocado
                    s = m.group(1)
                    if s not in tabla:
                        return m.group(0)
                    # Ya envuelto (ejecución repetida).
                    inicio = m.start()
                    if linea[max(0, inicio - 5):inicio] == "Tr.T(":
                        return m.group(0)
                    cambios += 1
                    tocado = True
                    return 'Tr.T("' + s + '")'

                # No tocar lo que va después de un comentario de línea.
                partes = re.split(r'(?<!:)//', linea, maxsplit=1) if '//' in linea and not re.search(r'"[^"]*//[^"]*"', linea) else [linea]
                partes[0] = CADENA.sub(cambiar, partes[0])
                salida.append("//".join(partes))
            if tocado:
                io.open(ruta, "w", encoding="utf-8", newline="").write("\n".join(salida))
                archivos += 1
    return cambios, archivos


def main():
    tabla = cargar()
    generar_cs(tabla)
    cambios, archivos = envolver(tabla)
    print("textos con traducción:", len(tabla), "· envueltos:", cambios, "en", archivos, "archivos")


if __name__ == "__main__":
    main()
