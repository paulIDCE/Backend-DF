"""
Fase 3 · paso 1 — fórmulas SUMA/RATIO para filas del catálogo que hoy toman su valor del JSON.

    python tools/catalogo/generar_formulas_fase3.py [--datos <Database>] [--hasta 2026-08]

Dos fuentes de fórmulas:
  1. Análisis vertical de los balances (EFI06 y SFN06, sub-vista "Análisis Vertical (%)"):
     100 × cuenta / cuenta raíz de su grupo (1 activo, 2 pasivo, 3 patrimonio, 4 gastos, 5 ingresos).
  2. Candidatos SUMA/RATIO hallados por descubrir_formulas.py (tools/catalogo/candidatos_descubiertos.json).

Cada fórmula se valida contra TODAS las entidades del catálogo (y, si el cuadro tiene equivalente de
sector, contra los 10 sectores), solo con meses reales (≤ último mes de la BD). Como una fila del
catálogo aplica a todas las entidades que comparten plantilla, se acepta solo si no falla en ninguna.

Salida:
  db/fase3/01_formulas_suma_ratio.sql   UPDATE de dbo.CuadroFila + términos (idempotente)
  db/fase3/formulas_reporte.md          aceptadas, rechazadas y por qué
Requiere numpy.
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import re
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from descubrir_formulas import ultimo_mes_bd  # noqa: E402

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATOS_DEF = os.path.join(RAIZ, "BackendDF", "src", "BackendDF", "Database")
CANDIDATOS = os.path.join(RAIZ, "tools", "catalogo", "candidatos_descubiertos.json")
SALIDA = os.path.join(RAIZ, "db", "fase3")
ID_SALDO = "Saldo Millones USD"
ID_VERTICAL = "Análisis Vertical (%)"
# Cuadro de entidad → cuadro de sector con la misma plantilla de códigos.
SECTOR_DE = {"EFI07": "CAR01", "EFI11": "CAR05", "EFI05": "SFN05", "EFI10": "CAR04", "EFI03": "SFN03", "EFI04": "SFN04"}
PREFIJO_TIPO = (("BP__", "BP"), ("COAC___", "COOP"), ("MUT__", "MU"))


def leer(ruta):
    with open(ruta, encoding="utf-8") as f:
        return json.load(f)


def num(v):
    if v is None:
        return np.nan
    try:
        return float(str(v).strip())
    except ValueError:
        return np.nan


def tipo_de(archivo):
    return next((t for p, t in PREFIJO_TIPO if archivo.startswith(p)), None)


# =============================================================================================
# Carga
# =============================================================================================

class Datos:
    def __init__(self, datos, meses, codigos_objetivo):
        self.meses = meses
        self.idx = {m: i for i, m in enumerate(meses)}
        self.saldos = {}       # unidad (entidad o sector) → cuenta → serie (millones)
        self.vertical = {}     # unidad → cuenta → serie (% publicado)
        self.objetivos = collections.defaultdict(dict)   # (cuadro, idsub, código) → unidad → serie
        self.tipo = {}         # unidad → BP | COOP | MU | SECTOR

        catalogo = [e["archivo"] for e in leer(os.path.join(datos, "entidades_lista.json"))]
        for n, a in enumerate(catalogo, 1):
            self.tipo[a] = tipo_de(a)
            ruta = os.path.join(datos, "balances", a + ".json")
            if os.path.exists(ruta):
                self._balance(a, leer(ruta))
            for carpeta in ("entidades", "reportes"):
                ruta = os.path.join(datos, carpeta, a + ".json")
                if os.path.exists(ruta):
                    self._objetivos(a, leer(ruta), codigos_objetivo)
            if n % 25 == 0:
                print(f"  cargadas {n}/{len(catalogo)} entidades", file=sys.stderr, flush=True)

        # Sectores: balances agregados y cuadros de sector.
        por_filtro = collections.defaultdict(list)
        for x in leer(os.path.join(datos, "base_balances.json")):
            por_filtro[x.get("Filtro")].append(x)
        for f, xs in por_filtro.items():
            self.tipo["S:" + f] = "SECTOR"
            self._balance("S:" + f, xs)
        for archivo in ("base_estru_sistema.json", "base_cartera.json"):
            por_filtro = collections.defaultdict(list)
            for x in leer(os.path.join(datos, archivo)):
                por_filtro[x.get("Filtro")].append(x)
            for f, xs in por_filtro.items():
                self._objetivos("S:" + f, xs, codigos_objetivo)

    def _serie(self, x):
        s = np.full(len(self.meses), np.nan)
        for k, v in x.items():
            i = self.idx.get(k)
            if i is not None:
                s[i] = num(v)
        return s

    def _balance(self, unidad, filas):
        sal, ver = {}, {}
        for x in filas:
            cb = str(x.get("Codigo_Base", "")).strip()
            if not cb.isdigit():
                continue
            if x.get("ID") == ID_SALDO:
                sal[int(cb)] = self._serie(x)
            elif x.get("ID") == ID_VERTICAL:
                ver[int(cb)] = self._serie(x)
        self.saldos[unidad], self.vertical[unidad] = sal, ver

    def _objetivos(self, unidad, filas, codigos):
        for x in filas:
            clave = (x.get("Cuadro"), x.get("ID") or "", x.get("CUC"))
            if clave in codigos:
                self.objetivos[clave].setdefault(unidad, self._serie(x))


# =============================================================================================
# Evaluación de fórmulas
# =============================================================================================

def evaluar(formula, saldos, n):
    """formula = {"tipo": SUMA|RATIO, "N": {cuenta: factor}, "D": {...}} → serie (millones o %)."""
    def suma(terminos):
        total = np.zeros(n)
        for c, k in terminos.items():
            total = total + k * np.nan_to_num(saldos.get(int(c), np.zeros(n)))
        return total
    N = suma(formula["N"])
    if formula["tipo"] == "SUMA":
        return N
    D = suma(formula["D"])
    with np.errstate(divide="ignore", invalid="ignore"):
        return np.where(D != 0, 100 * N / D, np.nan)


# Entidades con datos anómalos en los JSON (plantilla atípica; publican 0 donde su balance tiene
# valores). Se validan aparte: sus fallas se informan pero no rechazan la fórmula.
ANOMALAS = {"COAC___ORDEN_Y_SEGURIDAD__OYS_"}
ERR_SALDO = 0.5e-4   # redondeo de los saldos publicados (millones, 4 decimales)


def tolerancia_ratio(N, D, n_terminos_n, n_terminos_d, decimales):
    """Error máximo de 100·N/D cuando N y D se arman con saldos redondeados, más el redondeo del objetivo."""
    with np.errstate(divide="ignore", invalid="ignore"):
        prop = 100 * (n_terminos_n * ERR_SALDO / np.abs(D) + np.abs(N) * n_terminos_d * ERR_SALDO / D ** 2)
    return 0.5 * 10 ** -decimales + 1.05 * prop + 1e-9


def evaluar_partes(formula, saldos, n):
    def suma(terminos):
        total = np.zeros(n)
        for c, k in terminos.items():
            total = total + k * np.nan_to_num(saldos.get(int(c), np.zeros(n)))
        return total
    return suma(formula["N"]), (suma(formula["D"]) if formula["tipo"] == "RATIO" else None)


def validar(formula, unidades, objetivo_por_unidad, datos, decimales=4):
    """Devuelve (comparados, fallas, fallas_anomalas) en las unidades con dato y balance ese mes."""
    nN, nD = len(formula["N"]), len(formula.get("D", {}))
    fallas, anomalas, comparados = [], [], 0
    for u in unidades:
        y = objetivo_por_unidad.get(u)
        sal = datos.saldos.get(u)
        if y is None or not sal or 1 not in sal:
            continue
        N, D = evaluar_partes(formula, sal, len(datos.meses))
        activo = sal[1]
        m = np.isfinite(y) & np.isfinite(activo) & (activo != 0)
        if D is None:
            p = N
            tol = 0.5 * 10 ** -decimales + ERR_SALDO * nN + 1e-6 * np.abs(y)
        else:
            with np.errstate(divide="ignore", invalid="ignore"):
                p = np.where(D != 0, 100 * N / D, np.nan)
            m &= np.isfinite(p)
            tol = tolerancia_ratio(N, D, nN, nD, decimales)
        malos = np.flatnonzero(m & ~(np.abs(p - y) <= tol))
        comparados += int(m.sum())
        destino = anomalas if u in ANOMALAS else fallas
        for i in malos[:3]:
            destino.append((u, datos.meses[i], float(y[i]), round(float(p[i]), 6)))
    return comparados, fallas, anomalas


# =============================================================================================
# Candidatos
# =============================================================================================

def candidatos_descubiertos():
    """(cuadro, idsub, código) → lista de fórmulas canónicas distintas halladas en algún tipo."""
    d = leer(CANDIDATOS)
    sumas_por_codigo = {}
    for t in d:
        for k, v in d[t]["resultados"].items():
            if v["tipo"] == "SUMA":
                sumas_por_codigo.setdefault(k.split("|", 2)[2], {int(c): f for c, f in v["terminos"].items()})

    def expandir(ref):
        tipo, valor = ref
        if tipo == "CUENTA":
            return {int(valor): 1.0}
        return sumas_por_codigo.get(valor)

    salida = collections.defaultdict(list)
    for t in d:
        for k, v in d[t]["resultados"].items():
            cuadro, idsub, cod = k.split("|", 2)
            if v["tipo"] == "SUMA":
                f = {"tipo": "SUMA", "N": {int(c): k2 for c, k2 in v["terminos"].items()}}
            elif v["tipo"] == "RATIO":
                n, dd = expandir(v["N"]), expandir(v["D"])
                if not n or not dd:
                    continue
                f = {"tipo": "RATIO", "N": n, "D": dd}
            else:
                continue
            if f not in salida[(cuadro, idsub, cod)]:
                salida[(cuadro, idsub, cod)].append(f)
    return salida


# =============================================================================================
# Principal
# =============================================================================================

def sql_formulas(aceptadas):
    """Script idempotente: por (cuadro, idsub, código) en todas las variantes."""
    filas, terminos = [], []
    for (cuadro, idsub, cod), f in sorted(aceptadas.items()):
        escala = 1e-6 if f["tipo"] == "SUMA" else 100.0
        filas.append(f"('{cuadro}', N'{idsub}', '{cod}', '{f['tipo']}', {escala!r})")
        for rol in ("N", "D"):
            for c, k in sorted(f.get(rol, {}).items()):
                terminos.append(f"('{cuadro}', N'{idsub}', '{cod}', '{rol}', {c}, {float(k)!r})")
    return filas, terminos


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--datos", default=DATOS_DEF)
    p.add_argument("--hasta", default=None)
    a = p.parse_args()

    ultimo = ultimo_mes_bd()
    hasta = min(a.hasta, ultimo) if a.hasta else ultimo
    meses = [m for m in sorted({f"{y}-{mm:02d}" for y in range(2021, 2028) for mm in range(1, 13)}) if "2021-05" <= m <= hasta]

    candidatos = candidatos_descubiertos()
    codigos = set(candidatos) | {(SECTOR_DE[c], i, k) for (c, i, k) in candidatos if c in SECTOR_DE}
    print(f"Cargando datos (meses {meses[0]}..{meses[-1]}, último mes BD {ultimo})…", file=sys.stderr, flush=True)
    datos = Datos(a.datos, meses, codigos)
    entidades = [u for u, t in datos.tipo.items() if t != "SECTOR"]
    sectores = [u for u, t in datos.tipo.items() if t == "SECTOR"]

    reporte = ["# Fase 3 · paso 1 — fórmulas SUMA/RATIO\n",
               f"Validación con meses reales {meses[0]} → {meses[-1]} (último mes de la BD: {ultimo}); "
               f"{len(entidades)} entidades y {len(sectores)} sectores.\n"]

    # ---- 1. Análisis vertical ----
    reporte.append("## 1. Análisis vertical (EFI06 / SFN06)\n")
    reporte.append("Regla: 100 × cuenta / cuenta raíz del grupo (primer dígito). Publicado con 2 decimales.\n")
    reporte.append("| Ámbito | Tipo | Valores comparados | Fallas | Ejemplos de falla |\n|---|---|---|---|---|")
    fallas_av = {}
    for ambito, unidades in (("Entidad", entidades), ("Sector", sectores)):
        por_tipo = collections.defaultdict(lambda: [0, []])
        for u in unidades:
            sal, ver = datos.saldos.get(u, {}), datos.vertical.get(u, {})
            for c, y in ver.items():
                raiz = int(str(c)[0])
                if c not in sal or raiz not in sal:
                    continue
                with np.errstate(divide="ignore", invalid="ignore"):
                    p_ = np.where(sal[raiz] != 0, 100 * sal[c] / sal[raiz], np.nan)
                tol = tolerancia_ratio(sal[c], sal[raiz], 1, 1, decimales=2)
                m = np.isfinite(y) & np.isfinite(p_)
                malos = np.flatnonzero(m & ~(np.abs(p_ - y) <= tol))
                e = por_tipo["OYS (anómala)" if u in ANOMALAS else datos.tipo[u]]
                e[0] += int(m.sum())
                e[1] += [(u, c, meses[i], float(y[i]), round(float(p_[i]), 4)) for i in malos[:2]]
        for t, (n, f) in sorted(por_tipo.items()):
            fallas_av[(ambito, t)] = len(f)
            reporte.append(f"| {ambito} | {t} | {n:,} | {len(f)} | {'; '.join(map(str, f[:3]))} |")
    acepta_av = sum(n for (amb, t), n in fallas_av.items() if t != "OYS (anómala)") == 0
    reporte.append(f"\n**Resultado:** {'✅ se carga como RATIO en EFI06 y SFN06' if acepta_av else '❌ no se carga (hay fallas)'}.\n")

    # ---- 2. Candidatos descubiertos ----
    reporte.append("## 2. Candidatos del descubrimiento\n")
    reporte.append("| Cuadro | Sub-vista | Código | Fórmula | Entidades | Sectores | Resultado |\n|---|---|---|---|---|---|---|")
    aceptadas = {}
    for (cuadro, idsub, cod), formulas in sorted(candidatos.items()):
        elegida, detalle = None, ""
        for f in formulas:
            if not f["N"]:
                detalle = "fila siempre en cero en el JSON: no es una fórmula"
                continue
            n_e, f_e, a_e = validar(f, entidades, datos.objetivos.get((cuadro, idsub, cod), {}), datos)
            if f_e or n_e == 0:
                detalle = f"{n_e} comparados, {len(f_e)} fallas (p. ej. {f_e[0] if f_e else '-'})"
                continue
            elegida, detalle = f, f"{n_e} comparados, 0 fallas" + (f" (OYS: {len(a_e)} fallas de datos)" if a_e else "")
            break
        texto = ("Σ " if elegida and elegida["tipo"] == "SUMA" else "100·N/D ") if elegida else ""
        if elegida:
            texto += " ".join(f"{'+' if k > 0 else '−'}{c}" for c, k in sorted(elegida["N"].items()))
            if elegida["tipo"] == "RATIO":
                texto += " / " + " ".join(f"{'+' if k > 0 else '−'}{c}" for c, k in sorted(elegida["D"].items()))
        sector_txt = "—"
        if elegida:
            aceptadas[(cuadro, idsub, cod)] = elegida
            if cuadro in SECTOR_DE:
                clave_s = (SECTOR_DE[cuadro], idsub, cod)
                n_s, f_s, _ = validar(elegida, sectores, datos.objetivos.get(clave_s, {}), datos)
                if n_s and not f_s:
                    aceptadas[clave_s] = elegida
                    sector_txt = f"✅ {SECTOR_DE[cuadro]} ({n_s})"
                else:
                    sector_txt = f"❌ {SECTOR_DE[cuadro]} ({n_s} comp., {len(f_s)} fallas)"
        reporte.append(f"| {cuadro} | {idsub or '—'} | `{cod}` | {texto or '—'} | {detalle} | {sector_txt} | "
                       f"{'✅ ' + elegida['tipo'] if elegida else '❌ queda JSON'} |")

    # ---- SQL ----
    os.makedirs(SALIDA, exist_ok=True)
    filas, terminos = sql_formulas(aceptadas)
    with open(os.path.join(SALIDA, "01_formulas_suma_ratio.sql"), "w", encoding="utf-8") as out:
        out.write(PLANTILLA_SQL.format(
            hasta=meses[-1], n_formulas=len(aceptadas), av=1 if acepta_av else 0,
            filas=",\n".join(filas) if filas else "('NINGUNO', N'', 'NINGUNO', 'SUMA', 1.0)",
            terminos=",\n".join(terminos) if terminos else "('NINGUNO', N'', 'NINGUNO', 'N', 0, 0.0)"))
    reporte.append(f"\n**Aceptadas:** {len(aceptadas)} fórmulas (entidad + sector) · análisis vertical: {'sí' if acepta_av else 'no'}.\n")
    with open(os.path.join(SALIDA, "formulas_reporte.md"), "w", encoding="utf-8") as out:
        out.write("\n".join(reporte) + "\n")
    print(f"Aceptadas {len(aceptadas)} fórmulas; análisis vertical {'OK' if acepta_av else 'con fallas'}.")
    return 0


PLANTILLA_SQL = """/* =====================================================================================
   Fase 3 · 01 — Fórmulas SUMA/RATIO para filas que hoy toman su valor del JSON (GENERADO)
   Origen: tools/catalogo/generar_formulas_fase3.py · validado con meses reales hasta {hasta}.
   - Análisis vertical de EFI06/SFN06 (todas las variantes): 100 × cuenta / cuenta raíz del grupo.
   - {n_formulas} fórmulas del descubrimiento, validadas en todas las entidades (y sectores).
   Idempotente: rehace los términos de cada fila tocada. No crea ni borra filas.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

/* ---------- 1. Análisis vertical ---------- */
IF {av} = 1
BEGIN
    DECLARE @av TABLE (CuadroFilaID int PRIMARY KEY, CuentaID int NOT NULL, Raiz int NOT NULL);
    INSERT INTO @av (CuadroFilaID, CuentaID, Raiz)
    SELECT f.CuadroFilaID, f.CuentaID, CAST(LEFT(CAST(f.CuentaID AS varchar(12)), 1) AS int)
    FROM dbo.CuadroFila AS f
    WHERE f.CuadroID IN ('EFI06', 'SFN06') AND f.IdSub = N'Análisis Vertical (%)' AND f.CuentaID IS NOT NULL;

    DELETE t FROM dbo.CuadroFilaTermino AS t INNER JOIN @av AS a ON a.CuadroFilaID = t.CuadroFilaID;
    INSERT INTO dbo.CuadroFilaTermino (CuadroFilaID, Rol, Fuente, CuentaID, Factor)
    SELECT CuadroFilaID, 'N', 'CUENTA', CuentaID, 1 FROM @av
    UNION ALL
    SELECT CuadroFilaID, 'D', 'CUENTA', Raiz, 1 FROM @av;
    UPDATE f SET Calculo = 'RATIO', Escala = 100
    FROM dbo.CuadroFila AS f INNER JOIN @av AS a ON a.CuadroFilaID = f.CuadroFilaID;
    DECLARE @nav int = (SELECT COUNT(*) FROM @av);
    PRINT CONCAT(N'Análisis vertical: ', @nav, N' filas → RATIO');
END

/* ---------- 2. Fórmulas descubiertas ---------- */
DECLARE @f TABLE (CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64), Calculo varchar(8), Escala float);
INSERT INTO @f VALUES
{filas};

DECLARE @t TABLE (CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64), Rol char(1), CuentaID int, Factor float);
INSERT INTO @t VALUES
{terminos};

DECLARE @filas TABLE (CuadroFilaID int PRIMARY KEY, CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64));
INSERT INTO @filas
SELECT cf.CuadroFilaID, cf.CuadroID, cf.IdSub, cf.Codigo
FROM dbo.CuadroFila AS cf
INNER JOIN @f AS f ON f.CuadroID = cf.CuadroID AND f.IdSub = cf.IdSub AND f.Codigo = cf.Codigo;

DELETE t FROM dbo.CuadroFilaTermino AS t INNER JOIN @filas AS x ON x.CuadroFilaID = t.CuadroFilaID;
INSERT INTO dbo.CuadroFilaTermino (CuadroFilaID, Rol, Fuente, CuentaID, Factor)
SELECT x.CuadroFilaID, t.Rol, 'CUENTA', t.CuentaID, t.Factor
FROM @filas AS x
INNER JOIN @t AS t ON t.CuadroID = x.CuadroID AND t.IdSub = x.IdSub AND t.Codigo = x.Codigo;
UPDATE cf SET Calculo = f.Calculo, Escala = f.Escala
FROM dbo.CuadroFila AS cf
INNER JOIN @filas AS x ON x.CuadroFilaID = cf.CuadroFilaID
INNER JOIN @f AS f ON f.CuadroID = x.CuadroID AND f.IdSub = x.IdSub AND f.Codigo = x.Codigo;
DECLARE @nf int = (SELECT COUNT(*) FROM @filas);
PRINT CONCAT(N'Fórmulas descubiertas: ', @nf, N' filas actualizadas');

COMMIT TRANSACTION;
PRINT N'01_formulas_suma_ratio: OK';
GO
"""

if __name__ == "__main__":
    sys.exit(main())
