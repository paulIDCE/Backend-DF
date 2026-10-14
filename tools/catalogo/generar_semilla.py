"""
Genera la semilla del catálogo de cuadros (Fase 2) a partir de los JSON publicados.

    python tools/catalogo/generar_semilla.py [--datos <carpeta Database>] [--salida db/fase2]

Produce:
  - <salida>/01_semilla_catalogo.sql : INSERT de dbo.Cuadro, dbo.CuadroFila, dbo.CuadroFilaTermino
  - <salida>/semilla_reporte.md      : variantes, plantillas atípicas, fórmulas aceptadas/rechazadas

Reglas (docs/informe-sav-a-sql.md, Fase 2):
  - Entran todos los cuadros de sistema/entidad + REP01 (no los macro).
  - Una plantilla por (cuadro, variante). Variante de entidad = tipo (BP/COOP/MU); de sector =
    tipos que suma el sector ('BP', 'COOP', 'MU', 'BP,COOP,MU'). '*' si es igual para todos.
  - Convención C2: no se incluyen las cuentas de los grupos 6 y 7.
  - Fila '@dígitos' (en balances, solo el análisis "Saldo") → SUMA de esa cuenta × 1e-6.
  - Indicadores de IndicadorFin en cuadros de entidad → SUMA del indicador con su escala.
  - Fórmulas conocidas (pasipat, márgenes del PyG) → SUMA de cuentas, solo si cuadran con el
    propio JSON en todas las plantillas y meses (tolerancia 1e-3). El resto → JSON (pendiente).
Solo usa la biblioteca estándar. Es reproducible: misma entrada → mismo SQL.
"""
from __future__ import annotations

import argparse
import collections
import json
import math
import os
import re
import sys
from dataclasses import dataclass, field

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATOS_DEF = os.path.join(RAIZ, "BackendDF", "src", "BackendDF", "Database")
SALIDA_DEF = os.path.join(RAIZ, "db", "fase2")

ESCALA_SALDO = 1e-6
GRUPOS_OMITIDOS = ("6", "7")
ID_SALDO = "Saldo Millones USD"
PERIODO = re.compile(r"^\d{4}(-(\d{2}|T\d))?$")

# Indicadores cargados en dbo.IndicadorFin y su escala respecto a lo publicado.
INDICADORES = {"ACT_PON_RIESGO": ESCALA_SALDO, "P_T_PRIMARIO": ESCALA_SALDO,
               "P_T_SECUNDARIO": ESCALA_SALDO, "PTC": ESCALA_SALDO, "SOLVENCIA": 100.0}

# Fórmulas candidatas (estructura del Estado de Resultados / Situación de la SB), en cuentas.
_MNI = {51: 1, 41: -1}
_MBF = {**_MNI, 52: 1, 54: 1, 42: -1, 53: 1, 43: -1}
_MNF = {**_MBF, 44: -1}
_MI = {**_MNF, 45: -1}
_MO = {**_MI, 55: 1, 46: -1}
_GAI = {**_MO, 56: 1, 47: -1}
FORMULAS = {
    "pasipat": {2: 1, 3: 1},
    "Marg_Ne": _MNI,
    "Mar_Br_Fi": _MBF,
    "Mar_Ne_Fina": _MNF,
    "Mar_Inte": _MI,
    "Mar_Oper": _MO,
    "Gan_Pe_Imp": _GAI,
    "Gan_Eje": {**_GAI, 48: -1},
}

# Sector (texto de Filtro) → variante = "tipos[:segmentos]" que suma el sector (ver
# Data/FuenteSql/FiltrosSistema.cs). El SP busca la variante exacta, luego solo los tipos y luego '*'.
SECTOR_VARIANTE = {
    "Sistema Financiero Nacional (privado y eps)": "BP,COOP,MU",
    "Sector Financiero Privado": "BP",
    "Sector Financiero Popular y Solidario": "COOP",
    "Bancos Privados Grandes": "BP:1",
    "Bancos Privados Medianos": "BP:2",
    "Bancos Privados Pequeños": "BP:3",
    "Coop. Segmento 1": "COOP:1",
    "Coop. Segmento 2": "COOP:2",
    "Coop. Segmento 3": "COOP:3",
    "Asociación Mutualistas de Ahorro y Crédito para la Vivienda": "MU",
}
PREFIJO_TIPO = (("BP__", "BP"), ("COAC___", "COOP"), ("MUT__", "MU"))

# No salen del .sav: conservan las fechas del JSON (convención C1).
FECHAS_JSON = {"EFI09", "TPE02", "CAR03", "TPE01",
               # Geográficos (oct-2026): colocaciones/captaciones por provincia y cantón, puntos de atención.
               "GEO01", "GEO02", "GEO03", "GEO04", "PDA01", "PDA02",
               "GEE01", "GEE02", "GEE03", "GEE04", "PDE01", "PDE02"}
# En REP01 la "Variable" funciona como clave de búsqueda (R7): se conserva el texto publicado.
SIN_NOMBRE_CUENTA = {"REP01"}


# =============================================================================================
# Lectura
# =============================================================================================

def leer(ruta: str) -> list[dict]:
    with open(ruta, encoding="utf-8") as f:
        return [x for x in json.load(f) if isinstance(x, dict)]


def texto(v) -> str | None:
    if v is None:
        return None
    s = str(v).strip()
    return None if s in ("", "None") else s


def numero(v) -> float:
    if v is None:
        return math.nan
    try:
        x = float(str(v).strip())
        return x if math.isfinite(x) else math.nan
    except ValueError:
        return math.nan


def nivel_jerarquia(x: dict) -> int:
    n = 0
    for i in range(1, 8):
        if texto(x.get(f"Nivel{i}")):
            n = i
    return n


def cuenta_de(codigo: str | None) -> int | None:
    """'@1101' → 1101; cualquier otro código → None."""
    if codigo and codigo.startswith("@") and codigo[1:].isdigit():
        return int(codigo[1:])
    return None


def tipo_por_archivo(nombre: str) -> str | None:
    for prefijo, tipo in PREFIJO_TIPO:
        if nombre.startswith(prefijo):
            return tipo
    return None


# =============================================================================================
# Modelo
# =============================================================================================

@dataclass
class Fila:
    id_sub: str
    codigo: str | None
    codigo_base: str | None
    grupo: str | None
    etiqueta: str | None
    nivel: int
    segmento_credito: str | None
    unidad: str | None
    cuenta_id: int | None = None          # cuenta que la fila representa (texto desde dbo.Cuenta)
    calculo: str = "JSON"
    escala: float = 1.0
    terminos: list = field(default_factory=list)   # (rol, fuente, cuenta_id, indicador, factor)

    def firma(self):
        return (self.id_sub, self.codigo, self.codigo_base, self.grupo, self.etiqueta)


@dataclass
class Cuadro:
    id: str
    titulo: str
    unidad: str | None
    ambito: str
    tipo: str
    frecuencia: str = "mensual"
    orden: int = 0
    variantes: dict = field(default_factory=dict)   # variante → list[Fila]


def fila_desde(x: dict, es_arbol: bool) -> Fila:
    return Fila(
        id_sub=texto(x.get("ID")) or "",
        codigo=texto(x.get("CUC")),
        codigo_base=texto(x.get("Codigo_Base")),
        grupo=texto(x.get("Grupo")),
        etiqueta=texto(x.get("Variable")),
        nivel=int(numero(x.get("Nivel"))) if es_arbol and not math.isnan(numero(x.get("Nivel"))) else nivel_jerarquia(x),
        segmento_credito=texto(x.get("segmento_credito")),
        unidad=texto(x.get("Unidad")),
    )


def periodos_de(x: dict) -> dict[str, float]:
    return {k: numero(v) for k, v in x.items() if PERIODO.match(k)}


# =============================================================================================
# Plantillas y variantes
# =============================================================================================

class Plantillas:
    """Acumula las plantillas observadas por (cuadro, variante) y elige la mayoritaria."""

    def __init__(self):
        self.vistas = collections.defaultdict(lambda: collections.defaultdict(list))  # (cuadro, var) → firma → [origen]
        self.filas = {}          # (cuadro, firma) → list[Fila]
        self.valores = collections.defaultdict(list)  # (cuadro, firma) → list[list[dict periodo→valor]] por fila

    def agregar(self, cuadro: str, variante: str, origen: str, filas: list[Fila], valores: list[dict]):
        firma = tuple(f.firma() for f in filas)
        self.vistas[(cuadro, variante)][firma].append(origen)
        self.filas.setdefault((cuadro, firma), filas)
        if len(self.valores[(cuadro, firma)]) < 12:   # muestra para validar fórmulas
            self.valores[(cuadro, firma)].append(valores)

    def resolver(self, reporte: list[str]) -> dict[str, dict[str, tuple]]:
        """cuadro → {variante: firma}; colapsa a '*' si todas las variantes coinciden."""
        salida = collections.defaultdict(dict)
        for (cuadro, variante), firmas in sorted(self.vistas.items()):
            ordenadas = sorted(firmas.items(), key=lambda kv: -len(kv[1]))
            elegida, origenes = ordenadas[0]
            for firma, otros in ordenadas[1:]:
                reporte.append(f"- `{cuadro}` variante `{variante}`: plantilla atípica en {len(otros)} archivo(s) "
                               f"({', '.join(sorted(otros)[:5])}); se usa la mayoritaria ({len(origenes)}).")
            salida[cuadro][variante] = elegida
        for cuadro, por_variante in salida.items():
            if len(set(por_variante.values())) == 1:
                salida[cuadro] = {"*": next(iter(por_variante.values()))}
                continue
            # "BP:2" igual a "BP" sobra: el SP cae de la variante exacta a la de solo tipos.
            for variante in [v for v in por_variante if ":" in v]:
                base = variante.split(":")[0]
                if por_variante.get(base) == por_variante[variante]:
                    del por_variante[variante]
        return salida


# =============================================================================================
# Clasificación de filas
# =============================================================================================

def es_omitida(cuenta: int | None) -> bool:
    return cuenta is not None and str(cuenta).startswith(GRUPOS_OMITIDOS)


def clasificar(cuadro: Cuadro, filas: list[Fila], muestras: list[list[dict]], reporte_formulas: dict) -> list[Fila]:
    """Asigna cuenta, cálculo y términos; quita las cuentas de los grupos omitidos (C2)."""
    es_arbol = cuadro.tipo in ("Balances", "BalancesEntidad")
    salida = []
    for i, f in enumerate(filas):
        cuenta = int(f.codigo_base) if es_arbol and f.codigo_base and f.codigo_base.isdigit() else cuenta_de(f.codigo)
        if es_omitida(cuenta):
            continue
        g = Fila(**{k: v for k, v in f.__dict__.items() if k != "terminos"}, terminos=[])
        g.cuenta_id = cuenta

        if cuenta is not None and (not es_arbol or f.id_sub == ID_SALDO):
            g.calculo, g.escala = "SUMA", ESCALA_SALDO
            g.terminos = [("N", "CUENTA", cuenta, None, 1.0)]
        elif cuadro.ambito == "ENTIDAD" and f.codigo in INDICADORES:
            g.calculo, g.escala = "SUMA", INDICADORES[f.codigo]
            g.terminos = [("N", "INDICADOR", None, f.codigo, 1.0)]
        elif f.codigo in FORMULAS:
            ok, detalle = validar_formula(filas, i, FORMULAS[f.codigo], muestras)
            clave = (cuadro.id, f.codigo)
            reporte_formulas[clave] = (ok, detalle)
            if ok:
                g.calculo, g.escala = "SUMA", ESCALA_SALDO
                g.terminos = [("N", "CUENTA", c, None, float(k)) for c, k in sorted(FORMULAS[f.codigo].items())]
        salida.append(g)
    return salida


def validar_formula(filas: list[Fila], indice: int, formula: dict, muestras: list[list[dict]]) -> tuple[bool, str]:
    """La fila calculada del JSON debe ser igual a Σ factor × cuenta (filas de la misma plantilla)."""
    por_cuenta = {}
    for j, f in enumerate(filas):
        c = cuenta_de(f.codigo)
        if c is not None and (f.id_sub, c) not in por_cuenta:
            por_cuenta[(f.id_sub, c)] = j
    id_sub = filas[indice].id_sub
    faltan = [c for c in formula if (id_sub, c) not in por_cuenta]
    if faltan:
        return False, f"faltan cuentas en la plantilla: {faltan}"
    comparados, peor = 0, 0.0
    for valores in muestras:
        objetivo = valores[indice]
        for periodo, v in objetivo.items():
            if math.isnan(v):
                continue
            partes = [valores[por_cuenta[(id_sub, c)]].get(periodo, math.nan) * k for c, k in formula.items()]
            if any(math.isnan(p) for p in partes):
                continue
            peor = max(peor, abs(sum(partes) - v))
            comparados += 1
    if comparados == 0:
        return False, "sin valores para comparar"
    return peor <= 1e-3, f"{comparados} valores comparados, diferencia máxima {peor:.6f}"


# =============================================================================================
# Recorrido de los JSON
# =============================================================================================

def construir(datos: str, reporte: list[str]):
    plantillas = Plantillas()
    meta = {}   # cuadro → (titulo, unidad, ambito, tipo)

    def registrar(cuadro, titulo, unidad, ambito, tipo):
        if cuadro not in meta:
            meta[cuadro] = [titulo, unidad, ambito, tipo]
        else:
            m = meta[cuadro]
            m[0] = m[0] or titulo
            m[1] = m[1] or unidad
            if tipo == "CarteraEntidad":
                m[3] = tipo

    def acumular(filas_json, ambito, tipo_base, es_arbol, variante, origen):
        por_cuadro = collections.OrderedDict()
        for x in filas_json:
            c = texto(x.get("Cuadro"))
            if c:
                por_cuadro.setdefault(c, []).append(x)
        for c, xs in por_cuadro.items():
            tipo = tipo_base
            if tipo_base == "Entidad" and any(texto(x.get("ID")) for x in xs):
                tipo = "CarteraEntidad"
            titulo = next((texto(x.get("Titulo_Cuadro")) for x in xs if texto(x.get("Titulo_Cuadro"))), c)
            unidad = next((texto(x.get("Unidad")) for x in xs if texto(x.get("Unidad"))), None)
            registrar(c, titulo, unidad, ambito, tipo)
            filas = [fila_desde(x, es_arbol) for x in xs]
            plantillas.agregar(c, variante, origen, filas, [periodos_de(x) for x in xs])

    catalogo = {e["archivo"] for e in leer(os.path.join(datos, "entidades_lista.json"))}

    # ---- Sector ----
    for archivo, tipo, arbol in (("base_estru_sistema.json", "Sistema", False),
                                 ("base_balances.json", "Balances", True),
                                 ("base_cartera.json", "Cartera", False)):
        filas = leer(os.path.join(datos, archivo))
        por_filtro = collections.OrderedDict()
        for x in filas:
            por_filtro.setdefault(texto(x.get("Filtro")), []).append(x)
        for filtro, xs in por_filtro.items():
            variante = SECTOR_VARIANTE.get(filtro)
            if variante is None:
                reporte.append(f"- `{archivo}`: filtro desconocido `{filtro}` (ignorado).")
                continue
            acumular(xs, "SECTOR", tipo, arbol, variante, filtro)

    # ---- Entidad ----
    for carpeta, tipo, arbol in (("entidades", "Entidad", False), ("balances", "BalancesEntidad", True),
                                 ("reportes", "Reporte", False)):
        for nombre in sorted(os.listdir(os.path.join(datos, carpeta))):
            if not nombre.endswith(".json"):
                continue
            slug = nombre[:-5]
            variante = tipo_por_archivo(slug)
            if variante is None or (carpeta == "reportes" and slug not in catalogo):
                continue
            acumular(leer(os.path.join(datos, carpeta, nombre)), "ENTIDAD", tipo, arbol, variante, slug)

    elegidas = plantillas.resolver(reporte)

    cuadros = []
    reporte_formulas = {}
    for orden, cid in enumerate(sorted(meta, key=lambda c: (0 if meta[c][2] == "SECTOR" else 1, c)), start=1):
        titulo, unidad, ambito, tipo = meta[cid]
        cuadro = Cuadro(cid, titulo, unidad, ambito, tipo, orden=orden * 10)
        for variante, firma in sorted(elegidas[cid].items()):
            filas = plantillas.filas[(cid, firma)]
            muestras = plantillas.valores[(cid, firma)]
            cuadro.variantes[variante] = clasificar(cuadro, filas, muestras, reporte_formulas)
        cuadros.append(cuadro)
    return cuadros, reporte_formulas


# =============================================================================================
# Salida SQL
# =============================================================================================

def sql_texto(v) -> str:
    if v is None:
        return "NULL"
    return "N'" + str(v).replace("'", "''") + "'"


def sql_num(v) -> str:
    return "NULL" if v is None else repr(v)


def escribir_sql(cuadros: list[Cuadro], ruta: str):
    filas_sql, terminos_sql = [], []
    fila_id = 0
    for c in cuadros:
        for variante, filas in c.variantes.items():
            for orden, f in enumerate(filas, start=1):
                fila_id += 1
                filas_sql.append(
                    f"({fila_id},{sql_texto(c.id)},{sql_texto(variante)},{sql_texto(f.id_sub)},{orden * 10},"
                    f"{sql_texto(f.codigo)},{sql_texto(f.codigo_base)},{sql_num(f.cuenta_id)},{sql_texto(f.grupo)},"
                    f"{sql_texto(f.etiqueta)},{f.nivel},{sql_texto(f.segmento_credito)},{sql_texto(f.unidad)},"
                    f"{sql_texto(f.calculo)},{repr(float(f.escala))})")
                for rol, fuente, cuenta, indicador, factor in f.terminos:
                    terminos_sql.append(f"({fila_id},'{rol}','{fuente}',{sql_num(cuenta)},{sql_texto(indicador)},{repr(float(factor))})")

    def lotes(valores, tam=900):
        for i in range(0, len(valores), tam):
            yield valores[i:i + tam]

    with open(ruta, "w", encoding="utf-8") as out:
        out.write("/* =====================================================================================\n"
                  "   Fase 2 · 01 — Semilla del catálogo de cuadros (GENERADO, no editar a mano)\n"
                  "   Origen: tools/catalogo/generar_semilla.py sobre los JSON publicados.\n"
                  f"   {len(cuadros)} cuadros · {fila_id} filas · {len(terminos_sql)} términos.\n"
                  "   Reemplaza por completo el contenido del catálogo, en una transacción.\n"
                  "   ===================================================================================== */\n"
                  "USE idce_bco_coop;\nGO\nSET NOCOUNT ON;\nSET XACT_ABORT ON;\nBEGIN TRANSACTION;\n\n"
                  "DELETE FROM dbo.CuadroFilaTermino;\nDELETE FROM dbo.CuadroFila;\nDELETE FROM dbo.Cuadro;\n\n")
        out.write("INSERT INTO dbo.Cuadro (CuadroID, Titulo, Unidad, Ambito, Tipo, Frecuencia, FechasJson, UsaNombreCuenta, Orden, Activo) VALUES\n")
        out.write(",\n".join(
            f"({sql_texto(c.id)},{sql_texto(c.titulo)},{sql_texto(c.unidad)},'{c.ambito}','{c.tipo}','{c.frecuencia}',"
            f"{1 if c.id in FECHAS_JSON else 0},{0 if c.id in SIN_NOMBRE_CUENTA else 1},{c.orden},1)" for c in cuadros) + ";\n\n")
        for lote in lotes(filas_sql):
            out.write("INSERT INTO dbo.CuadroFila (CuadroFilaID, CuadroID, Variante, IdSub, Orden, Codigo, CodigoBase, CuentaID, "
                      "Grupo, Etiqueta, Nivel, SegmentoCredito, Unidad, Calculo, Escala) VALUES\n")
            out.write(",\n".join(lote) + ";\n")
        out.write("\n")
        for lote in lotes(terminos_sql):
            out.write("INSERT INTO dbo.CuadroFilaTermino (CuadroFilaID, Rol, Fuente, CuentaID, IndicadorCodigo, Factor) VALUES\n")
            out.write(",\n".join(lote) + ";\n")
        out.write("\nCOMMIT TRANSACTION;\nPRINT N'01_semilla_catalogo: OK';\nGO\n")
    return fila_id, len(terminos_sql)


def escribir_reporte(cuadros, formulas, reporte, total_filas, total_terminos, ruta):
    with open(ruta, "w", encoding="utf-8") as out:
        out.write("# Reporte de la semilla del catálogo de cuadros\n\n"
                  "Generado por `tools/catalogo/generar_semilla.py`. No editar a mano.\n\n")
        out.write(f"**{len(cuadros)} cuadros · {total_filas} filas · {total_terminos} términos.**\n\n")
        out.write("## Cuadros\n\n| Cuadro | Ámbito | Tipo | Fechas | Variante | Filas | SUMA | JSON | Título |\n|---|---|---|---|---|---|---|---|---|\n")
        for c in cuadros:
            for variante, filas in c.variantes.items():
                cnt = collections.Counter(f.calculo for f in filas)
                out.write(f"| {c.id} | {c.ambito} | {c.tipo} | {'JSON' if c.id in FECHAS_JSON else 'ventana BD'} | `{variante}` | "
                          f"{len(filas)} | {cnt['SUMA']} | {cnt['JSON']} | {c.titulo} |\n")
        out.write("\n## Fórmulas validadas contra el JSON\n\n| Cuadro | Código | Resultado | Detalle |\n|---|---|---|---|\n")
        for (cid, codigo), (ok, detalle) in sorted(formulas.items()):
            out.write(f"| {cid} | {codigo} | {'✅ SUMA' if ok else '❌ queda JSON'} | {detalle} |\n")
        out.write("\n## Observaciones\n\n" + ("\n".join(reporte) if reporte else "- Ninguna.") + "\n")


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    p.add_argument("--datos", default=DATOS_DEF)
    p.add_argument("--salida", default=SALIDA_DEF)
    a = p.parse_args()

    reporte: list[str] = []
    cuadros, formulas = construir(a.datos, reporte)
    os.makedirs(a.salida, exist_ok=True)
    total_filas, total_terminos = escribir_sql(cuadros, os.path.join(a.salida, "01_semilla_catalogo.sql"))
    escribir_reporte(cuadros, formulas, reporte, total_filas, total_terminos, os.path.join(a.salida, "semilla_reporte.md"))
    rechazadas = [k for k, (ok, _) in formulas.items() if not ok]
    print(f"{len(cuadros)} cuadros, {total_filas} filas, {total_terminos} términos; "
          f"fórmulas aceptadas {len(formulas) - len(rechazadas)}/{len(formulas)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
