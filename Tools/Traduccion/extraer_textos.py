"""
Busca en los scripts del juego los textos en español que ve el jugador y los apunta en
Tools/Traduccion/textos.json ({"texto en español": "traducción al inglés o vacío"}).
Los que ya están traducidos se conservan; los nuevos se añaden vacíos.

Excluye lo que NO es texto para el jugador: nombres de objetos (new GameObject, Find),
rutas de Resources, Shader.Find, Debug.Log, atributos [Tooltip]/[Header], case "...",
const, claves de PlayerPrefs, cadenas interpoladas $"" y las líneas que ya usan Localizer.

Uso: python Tools/Traduccion/extraer_textos.py   (añade --listar para ver dónde está cada uno)
"""
import io
import json
import os
import re
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.normpath(os.path.join(AQUI, "..", "..", "Assets", "Scripts"))
TABLA = os.path.join(AQUI, "textos.json")

# Cadena C# normal (no interpolada ni literal @), con escapes.
CADENA = re.compile(r'(?<![$@\w])"((?:[^"\\\n]|\\.)*)"')
PALABRA = re.compile(r"[A-Za-zÁÉÍÓÚÜÑáéíóúüñ¿¡]{2,}")
ESPANOL = re.compile(r"[áéíóúüñÁÉÍÓÚÑ¿¡]|\b(de|la|el|los|las|del|con|para|por|una|un|en|sin|tu|es|y|o|al|que|no|se)\b", re.I)

EXCLUIR_LINEA = re.compile(
    r"Debug\.(Log|LogWarning|LogError)|new GameObject\(|\.Find\(|FindChild|Shader\.Find|Resources\.Load|"
    r"PlayerPrefs\.|\[Tooltip|\[Header|\[ContextMenu|\[TextArea|\[CreateAssetMenu|\[MenuItem|\[AddComponentMenu|case \"|const string|"
    r"Localizer\.|\bL\(\"|Tr\.T\(|\.name\s*==|CompareTag|LayerMask|Animator|nodeID|itemID|zoneId|moduleId|"
    r"GetComponent\(\"|SendMessage|Invoke\(|StartCoroutine\(\"|\bnameof\(|#region|#if|Application\.|File\.|Path\.")

SALTAR_ARCHIVOS = ("Localization" + os.sep, "Editor" + os.sep)


def es_texto_jugador(s):
    if len(s) < 2 or not PALABRA.search(s):
        return False
    if re.fullmatch(r"[a-z0-9_./:\-]+", s):          # identificadores, rutas, claves
        return False
    if re.fullmatch(r"[A-Za-z0-9_]+", s) and not ESPANOL.search(s) and s[0].islower():
        return False
    if s.startswith(("Assets/", "Hidden/", "Universal Render", "Standard", "_")):
        return False
    if re.fullmatch(r"#?[0-9a-fA-F]{6,8}", s) or re.fullmatch(r"<.*>", s):
        return False
    return True


def candidatos():
    for raiz, _, archivos in os.walk(SCRIPTS):
        for f in archivos:
            if not f.endswith(".cs"):
                continue
            ruta = os.path.join(raiz, f)
            rel = os.path.relpath(ruta, SCRIPTS)
            if rel.startswith(SALTAR_ARCHIVOS):
                continue
            for n, linea in enumerate(io.open(ruta, encoding="utf-8"), 1):
                cod = linea.split("//", 1)[0] if '"' not in linea.split("//", 1)[0][-1:] else linea
                if cod.strip().startswith(("///", "//", "*")) or EXCLUIR_LINEA.search(cod):
                    continue
                for m in CADENA.finditer(cod):
                    s = m.group(1)
                    if es_texto_jugador(s):
                        yield rel, n, s


def main():
    tabla = {}
    if os.path.exists(TABLA):
        tabla = json.load(io.open(TABLA, encoding="utf-8"))
    vistos = {}
    for rel, n, s in candidatos():
        vistos.setdefault(s, []).append("%s:%d" % (rel, n))
    for s in vistos:
        tabla.setdefault(s, "")
    json.dump(tabla, io.open(TABLA, "w", encoding="utf-8"), ensure_ascii=False, indent=1, sort_keys=True)
    sin = [s for s in vistos if not tabla.get(s)]
    print("textos encontrados:", len(vistos), "· sin traducir:", len(sin), "· archivos:", len({v[0].split(':')[0] for v in vistos.values()}))
    if "--listar" in sys.argv:
        for s, donde in sorted(vistos.items(), key=lambda kv: kv[1][0]):
            print(donde[0], "|", s)


if __name__ == "__main__":
    main()
