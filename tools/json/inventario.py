"""
Inventario de la "BD JSON" del backend y comparación entre dos versiones.

    # Foto de la carpeta actual
    python tools/json/inventario.py foto [--datos <carpeta Database>] [--salida docs/json/inventario-<fecha>.json]

    # Comparar dos fotos (o una foto contra una carpeta)
    python tools/json/inventario.py comparar <foto_anterior.json> <foto_nueva.json | carpeta> [--salida docs/json/comparacion.md]

La foto guarda, por archivo: tamaño, SHA-256, filas, cuadros, filtros, IDs, códigos (CUC/Codigo_Base),
claves de columnas no-período y rango de períodos. Por cuadro: filas, títulos y firma de la plantilla
(orden de códigos y textos). Así se detecta qué cambió y qué afecta al backend (catálogo de cuadros,
mapa de entidades, pruebas de contrato).
Solo usa la biblioteca estándar.
"""
from __future__ import annotations

import argparse
import collections
import datetime as dt
import hashlib
import json
import os
import re
import sys

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATOS_DEF = os.path.join(RAIZ, "BackendDF", "src", "BackendDF", "Database")
PERIODO = re.compile(r"^\d{4}(-(\d{2}|T\d))?$")


def sha256(ruta: str) -> str:
    h = hashlib.sha256()
    with open(ruta, "rb") as f:
        for bloque in iter(lambda: f.read(1 << 20), b""):
            h.update(bloque)
    return h.hexdigest()


def firma(textos) -> str:
    return hashlib.sha1("\u001e".join(map(str, textos)).encode("utf-8")).hexdigest()[:16]


def leer(ruta):
    with open(ruta, encoding="utf-8") as f:
        return json.load(f)


def resumen_archivo(ruta: str, relativo: str) -> dict:
    datos = leer(ruta)
    info = {"archivo": relativo, "bytes": os.path.getsize(ruta), "sha256": sha256(ruta)}
    if not isinstance(datos, list):
        info["tipo"] = type(datos).__name__
        return info

    filas = [x for x in datos if isinstance(x, dict)]
    periodos, columnas = set(), collections.Counter()
    cuadros = collections.OrderedDict()
    for x in filas:
        for k in x:
            (periodos.add(k) if PERIODO.match(k) else columnas.update([k]))
        c = x.get("Cuadro")
        if c is None:
            continue
        e = cuadros.setdefault(c, {"filas": 0, "titulos": set(), "filtros": collections.Counter(), "ids": set(), "plantilla": []})
        e["filas"] += 1
        if x.get("Titulo_Cuadro") not in (None, "None"):
            e["titulos"].add(str(x["Titulo_Cuadro"]))
        if x.get("Filtro") is not None:
            e["filtros"][str(x["Filtro"])] += 1
        if x.get("ID") is not None:
            e["ids"].add(str(x["ID"]))
        e["plantilla"].append((x.get("Filtro"), x.get("ID"), x.get("CUC") or x.get("Codigo_Base"), x.get("Variable")))

    p = sorted(periodos)
    info.update({
        "filas": len(filas),
        "columnas": dict(sorted(columnas.items())),
        "periodos": {"cantidad": len(p), "desde": p[0] if p else None, "hasta": p[-1] if p else None},
        "cuadros": {
            c: {
                "filas": e["filas"],
                "titulos": sorted(e["titulos"]),
                "filtros": dict(e["filtros"]),
                "ids": sorted(e["ids"]),
                # Plantilla de la primera combinación filtro: orden de códigos y textos.
                "firma_plantilla": firma(t[1:] for t in e["plantilla"] if t[0] == e["plantilla"][0][0]),
                "codigos": len({t[2] for t in e["plantilla"] if t[2]}),
            }
            for c, e in cuadros.items()
        },
    })
    return info


def foto(datos: str) -> dict:
    archivos, carpetas = {}, {}
    for nombre in sorted(os.listdir(datos)):
        ruta = os.path.join(datos, nombre)
        if os.path.isfile(ruta) and nombre.endswith(".json"):
            archivos[nombre] = resumen_archivo(ruta, nombre)

    for carpeta in ("entidades", "balances", "reportes"):
        base = os.path.join(datos, carpeta)
        if not os.path.isdir(base):
            continue
        detalle, firmas, periodos = {}, collections.Counter(), set()
        for nombre in sorted(os.listdir(base)):
            if not nombre.endswith(".json"):
                continue
            r = resumen_archivo(os.path.join(base, nombre), f"{carpeta}/{nombre}")
            detalle[nombre[:-5]] = {
                "bytes": r["bytes"], "sha256": r["sha256"], "filas": r.get("filas"),
                "periodos": r.get("periodos"),
                "cuadros": {c: {"filas": v["filas"], "firma_plantilla": v["firma_plantilla"]} for c, v in r.get("cuadros", {}).items()},
            }
            for c, v in r.get("cuadros", {}).items():
                firmas[(c, v["firma_plantilla"])] += 1
            if r.get("periodos", {}).get("desde"):
                periodos.update([r["periodos"]["desde"], r["periodos"]["hasta"]])
        carpetas[carpeta] = {
            "archivos": len(detalle),
            "bytes": sum(d["bytes"] for d in detalle.values()),
            "periodos": {"desde": min(periodos) if periodos else None, "hasta": max(periodos) if periodos else None},
            "plantillas": [{"cuadro": c, "firma": f, "archivos": n} for (c, f), n in sorted(firmas.items())],
            "detalle": detalle,
        }

    catalogo = leer(os.path.join(datos, "entidades_lista.json")) if os.path.exists(os.path.join(datos, "entidades_lista.json")) else []
    return {
        "generado": dt.datetime.now().isoformat(timespec="seconds"),
        "carpeta": os.path.abspath(datos),
        "archivos": archivos,
        "carpetas": carpetas,
        "catalogo": sorted((e.get("archivo"), e.get("nombre")) for e in catalogo),
    }


# =============================================================================================
# Reporte de una foto
# =============================================================================================

def reporte_foto(f: dict) -> str:
    l = [f"# Inventario de la BD JSON\n", f"Generado {f['generado']} sobre `{f['carpeta']}`.\n",
         "## Archivos base\n", "| Archivo | MB | Filas | Cuadros | Períodos | SHA-256 (12) |", "|---|---|---|---|---|---|"]
    for n, a in f["archivos"].items():
        p = a.get("periodos") or {}
        l.append(f"| {n} | {a['bytes'] / 1e6:,.1f} | {a.get('filas', '')} | {len(a.get('cuadros', {}))} | "
                 f"{p.get('desde')} → {p.get('hasta')} ({p.get('cantidad')}) | `{a['sha256'][:12]}` |")
    l += ["", "## Carpetas por entidad\n", "| Carpeta | Archivos | MB | Períodos | Plantillas distintas |", "|---|---|---|---|---|"]
    for n, c in f["carpetas"].items():
        l.append(f"| {n}/ | {c['archivos']} | {c['bytes'] / 1e6:,.1f} | {c['periodos']['desde']} → {c['periodos']['hasta']} | "
                 + ", ".join(f"{p['cuadro']}×{p['archivos']}" for p in c["plantillas"]) + " |")
    l += ["", f"## Catálogo (`entidades_lista.json`)\n", f"{len(f['catalogo'])} entidades.", ""]
    l += ["## Cuadros por archivo base\n", "| Archivo | Cuadro | Filas | Filtros | IDs | Códigos | Firma plantilla |", "|---|---|---|---|---|---|---|"]
    for n, a in f["archivos"].items():
        for c, v in a.get("cuadros", {}).items():
            l.append(f"| {n} | {c} | {v['filas']} | {len(v['filtros'])} | {len(v['ids'])} | {v['codigos']} | `{v['firma_plantilla']}` |")
    return "\n".join(l) + "\n"


# =============================================================================================
# Comparación
# =============================================================================================

def comparar(a: dict, b: dict) -> str:
    l = ["# Comparación de la BD JSON\n", f"- Anterior: {a['generado']} · `{a['carpeta']}`", f"- Nueva: {b['generado']} · `{b['carpeta']}`\n"]
    impacto = []

    l += ["## Archivos base\n", "| Archivo | Estado | Filas | Períodos | Cuadros |", "|---|---|---|---|---|"]
    for n in sorted(set(a["archivos"]) | set(b["archivos"])):
        x, y = a["archivos"].get(n), b["archivos"].get(n)
        if x is None or y is None:
            l.append(f"| {n} | {'NUEVO' if x is None else 'ELIMINADO'} | | | |")
            impacto.append(f"`{n}` {'nuevo' if x is None else 'eliminado'}.")
            continue
        if x["sha256"] == y["sha256"]:
            l.append(f"| {n} | igual | {x.get('filas')} | | |")
            continue
        px, py = x.get("periodos") or {}, y.get("periodos") or {}
        cx, cy = set(x.get("cuadros", {})), set(y.get("cuadros", {}))
        cambios_c = []
        if cx != cy:
            cambios_c.append(f"+{sorted(cy - cx)} −{sorted(cx - cy)}")
        for c in sorted(cx & cy):
            if x["cuadros"][c]["firma_plantilla"] != y["cuadros"][c]["firma_plantilla"]:
                cambios_c.append(f"{c}: plantilla cambia ({x['cuadros'][c]['filas']}→{y['cuadros'][c]['filas']} filas)")
                impacto.append(f"`{n}` · {c}: la plantilla (códigos/textos/orden) cambió → regenerar la semilla del catálogo y revisar fórmulas.")
            if x["cuadros"][c]["filtros"].keys() != y["cuadros"][c]["filtros"].keys():
                cambios_c.append(f"{c}: filtros cambian")
                impacto.append(f"`{n}` · {c}: cambian los filtros (sectores) → revisar FiltrosSistema / variantes.")
        l.append(f"| {n} | CAMBIA | {x.get('filas')}→{y.get('filas')} | {px.get('desde')}→{px.get('hasta')} ⇒ {py.get('desde')}→{py.get('hasta')} | "
                 + ("; ".join(cambios_c) or "solo valores") + " |")

    l += ["", "## Carpetas por entidad\n", "| Carpeta | Archivos | Nuevos | Eliminados | Cambiados | Períodos | Plantillas |", "|---|---|---|---|---|---|---|"]
    for n in sorted(set(a["carpetas"]) | set(b["carpetas"])):
        x, y = a["carpetas"].get(n, {"detalle": {}, "plantillas": [], "periodos": {}}), b["carpetas"].get(n, {"detalle": {}, "plantillas": [], "periodos": {}})
        dx, dy = x["detalle"], y["detalle"]
        nuevos, elim = sorted(set(dy) - set(dx)), sorted(set(dx) - set(dy))
        cambiados = [k for k in set(dx) & set(dy) if dx[k]["sha256"] != dy[k]["sha256"]]
        px = {(p["cuadro"], p["firma"]) for p in x["plantillas"]}
        py = {(p["cuadro"], p["firma"]) for p in y["plantillas"]}
        l.append(f"| {n}/ | {len(dx)}→{len(dy)} | {len(nuevos)} | {len(elim)} | {len(cambiados)} | "
                 f"{x['periodos'].get('desde')}→{x['periodos'].get('hasta')} ⇒ {y['periodos'].get('desde')}→{y['periodos'].get('hasta')} | "
                 f"{'iguales' if px == py else f'{len(py - px)} nuevas / {len(px - py)} desaparecen'} |")
        if px != py:
            impacto.append(f"`{n}/`: cambian plantillas de {sorted({c for c, _ in py ^ px})} → regenerar la semilla del catálogo.")
        if nuevos or elim:
            impacto.append(f"`{n}/`: {len(nuevos)} archivos nuevos, {len(elim)} eliminados → revisar `mapa-entidades.json`."
                           + (f" Nuevos: {', '.join(nuevos[:8])}{'…' if len(nuevos) > 8 else ''}." if nuevos else "")
                           + (f" Eliminados: {', '.join(elim[:8])}{'…' if len(elim) > 8 else ''}." if elim else ""))

    ca, cb = {tuple(e) for e in a["catalogo"]}, {tuple(e) for e in b["catalogo"]}
    l += ["", "## Catálogo de entidades\n", f"{len(ca)} → {len(cb)} entidades; {len(cb - ca)} nuevas, {len(ca - cb)} eliminadas o renombradas."]
    for e in sorted(cb - ca)[:30]:
        l.append(f"- + `{e[0]}` {e[1]}")
    for e in sorted(ca - cb)[:30]:
        l.append(f"- − `{e[0]}` {e[1]}")
    if ca != cb:
        impacto.append("Catálogo de entidades cambió → regenerar/actualizar `Data/FuenteSql/mapa-entidades.json` (slug → RUC/IFIID).")

    l += ["", "## Impacto en el backend\n"] + ([f"- {i}" for i in dict.fromkeys(impacto)] or ["- Solo cambian valores: no hay que tocar el catálogo, el mapa ni el código."])
    return "\n".join(l) + "\n"


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = p.add_subparsers(dest="cmd", required=True)
    f = sub.add_parser("foto")
    f.add_argument("--datos", default=DATOS_DEF)
    f.add_argument("--salida", default=None)
    c = sub.add_parser("comparar")
    c.add_argument("anterior")
    c.add_argument("nueva")
    c.add_argument("--salida", default=os.path.join(RAIZ, "docs", "json", "comparacion.md"))
    a = p.parse_args()

    if a.cmd == "foto":
        resultado = foto(a.datos)
        salida = a.salida or os.path.join(RAIZ, "docs", "json", f"inventario-{dt.date.today():%Y%m%d}.json")
        os.makedirs(os.path.dirname(salida), exist_ok=True)
        with open(salida, "w", encoding="utf-8") as out:
            json.dump(resultado, out, ensure_ascii=False, indent=1)
        with open(salida[:-5] + ".md", "w", encoding="utf-8") as out:
            out.write(reporte_foto(resultado))
        print(f"Foto guardada en {salida} (+ .md)")
    else:
        anterior = leer(a.anterior)
        nueva = foto(a.nueva) if os.path.isdir(a.nueva) else leer(a.nueva)
        os.makedirs(os.path.dirname(a.salida), exist_ok=True)
        with open(a.salida, "w", encoding="utf-8") as out:
            out.write(comparar(anterior, nueva))
        print(f"Comparación guardada en {a.salida}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
