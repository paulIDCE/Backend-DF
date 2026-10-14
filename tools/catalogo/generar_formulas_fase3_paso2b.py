"""
Fase 3 · paso 2b — fórmulas que no necesitan la nota técnica de la SB.

    python tools/catalogo/generar_formulas_fase3_paso2b.py [--datos <Database>] [--hasta 2026-08] [--cache <pkl>]

Candidatos (validados como en el paso 2, generar_formulas_fase3_paso2.py):
  1. Cartera por estado (EFI07, CAR01, REP01), por tipo de crédito y total. La composición sale del
     orden de las filas de EFI07 (p. ej. IF007_1 va seguido de @1401 @1409 @1417 @149105 @149405):
       IF007_k  por vencer      original + refinanciada + reestructurada + 1491xx + 1494xx
       IF008_k  no devenga      original + refinanciada + reestructurada + 1492xx + 1495xx
       IF009_k  vencida         original + refinanciada + reestructurada + 1493xx + 1496xx
       IF010_k  improductiva    IF008_k + IF009_k
       IF011_k  bruta           IF007_k + IF010_k
     y los totales (Por_vencer, Refi_vencer, Rees_vencer, Por_vencida, …, Impro_refi, Impro_rees).
  2. Márgenes anualizados de EFI03/SFN03/REP01 (Marg_Ne_anual…): ANUALIZADO de la SUMA de EFI02.
  3. EFI05 PTR_9 = 9 % de los activos ponderados por riesgo (IndicadorData).
  4. tasa_X de los X que ganan fórmula aquí: 100 × VARIACION_12(X) / RETARDO_12(X).

Validación desde --desde-validacion (2022-05 por defecto): los primeros 12 meses de los JSON
(2021-05 → 2022-04) salen de otra serie (el análisis horizontal publica 0 ahí) y la cartera por estado
no cuadra en ninguna entidad en esos meses; se informan aparte. La BD empieza en 2024-01.

Salida:
  db/fase3/07_formulas_sin_nota.sql        UPDATE idempotente (requiere 03_operaciones_tiempo.sql)
  db/fase3/formulas_sin_nota_reporte.md    aceptadas, rechazadas y por qué
"""
from __future__ import annotations

import argparse
import collections
import os
import pickle
import re
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from descubrir_formulas import ultimo_mes_bd  # noqa: E402
import generar_formulas_fase3_paso2 as p2  # noqa: E402
from generar_formulas_fase3 import ANOMALAS, DATOS_DEF, SALIDA, SECTOR_DE  # noqa: E402

DESDE_VALIDACION = "2022-05"
CUADROS_CARTERA = ("EFI07", "CAR01", "REP01")
RE_CARTERA = re.compile(r"^(IF0(07|08|09|10|11)(_\d)?|Por_vencer|Refi_vencer|Rees_vencer|Por_vencida|Refi_vencida|"
                        r"Rees_vencida|Impro_refi|Impro_rees)$")
RE_TASA = re.compile(r"^tasa_(.+)$")
RE_ANUAL = re.compile(r"^(.+)_anual$")
# Margen anualizado → SUMA de EFI02 (catálogo).
MARGEN_DE = {"Marg_Ne": "Marg_Ne", "Mar_Br_Fi": "Mar_Br_Fi", "Mar_Ne_F": "Mar_Ne_Fina", "Mar_Inte": "Mar_Inte",
             "Mar_Oper": "Mar_Oper", "Gan_Pe_Imp": "Gan_Pe_Imp", "Gan_Eje": "Gan_Eje"}
# Tipo de crédito → (desplazamiento sobre 1401…, sufijo de 1491…1496); None = educativo (cuentas propias).
TIPOS = {1: (0, "05"), 2: (1, "20"), 3: (2, "30"), 4: (3, "40"), 5: (7, "35"), 6: (None, "50")}
IDSUB_TIPO = {"Productivo": 1, "Consumo": 2, "Inmobiliario": 3, "Microcrédito": 4,
              "Vivienda interés Público y Social": 5, "Educativo": 6}


class ObjetivosSinNota:
    def __contains__(self, clave):
        cuadro, _, cod = clave
        if not cod:
            return False
        if cuadro in CUADROS_CARTERA and (RE_CARTERA.match(cod) or RE_TASA.match(cod) or re.match(r"^@\d+$", cod)):
            return True
        if cuadro in ("EFI03", "SFN03", "REP01") and RE_ANUAL.match(cod):
            return True
        return cuadro == "EFI05" and cod in ("PTR_9", "ACT_PON_RIESGO")


# =============================================================================================
# Composición de la cartera por estado
# =============================================================================================

def cuentas_tipo(k):
    """Cuentas de un tipo de crédito por estado: (original, refinanciada, reestructurada) y subcuentas 149x."""
    o, sfx = TIPOS[k]
    if o is None:
        pv, nd, v = (1473, 1475, 1477), (1479, 1481, 1483), (1485, 1487, 1489)
    else:
        pv, nd, v = (1401 + o, 1409 + o, 1417 + o), (1425 + o, 1433 + o, 1441 + o), (1449 + o, 1457 + o, 1465 + o)
    sub = {b: int(f"{b}{sfx}") for b in (1491, 1492, 1493, 1494, 1495, 1496)}
    return pv, nd, v, sub


def suma(*conjuntos):
    f = {}
    for c in conjuntos:
        for cuenta in c:
            f[cuenta] = f.get(cuenta, 0.0) + 1.0
    return f


def formulas_cartera():
    """código → lista de candidatos {cuenta: factor} (el primero que valida, gana)."""
    f = {}
    for k in TIPOS:
        pv, nd, v, sub = cuentas_tipo(k)
        f7 = suma(pv, (sub[1491], sub[1494]))
        f8 = suma(nd, (sub[1492], sub[1495]))
        f9 = suma(v, (sub[1493], sub[1496]))
        f[f"IF007_{k}"] = [f7]
        f[f"IF008_{k}"] = [f8]
        f[f"IF009_{k}"] = [f9]
        f[f"IF010_{k}"] = [suma(f8, f9)]
        f[f"IF011_{k}"] = [suma(f7, f8, f9)]
    por = {e: [] for e in ("pv", "rpv", "epv", "nd", "rnd", "end", "v", "rv", "ev")}
    for k in TIPOS:
        pv, nd, v, _ = cuentas_tipo(k)
        for nombre, c in zip(("pv", "rpv", "epv"), pv):
            por[nombre].append(c)
        for nombre, c in zip(("nd", "rnd", "end"), nd):
            por[nombre].append(c)
        for nombre, c in zip(("v", "rv", "ev"), v):
            por[nombre].append(c)
    t = {k: suma(c) for k, c in por.items()}
    f["Por_vencer"], f["Refi_vencer"], f["Rees_vencer"] = [t["pv"]], [t["rpv"]], [t["epv"]]
    f["Por_vencida"], f["Refi_vencida"], f["Rees_vencida"] = [t["v"]], [t["rv"]], [t["ev"]]
    f["Impro_refi"] = [t["rnd"], suma(t["rnd"], t["rv"])]
    f["Impro_rees"] = [t["end"], suma(t["end"], t["ev"])]
    f7 = suma(t["pv"], t["rpv"], t["epv"], (1491, 1494))
    f8 = suma(t["nd"], t["rnd"], t["end"], (1492, 1495))
    f9 = suma(t["v"], t["rv"], t["ev"], (1493, 1496))
    f["IF007"], f["IF008"], f["IF009"] = [f7], [f8], [f9]
    f["IF010"] = [suma(f8, f9)]
    f["IF011"] = [suma(f7, f8, f9)]
    return f


def alias(x):
    """Formas en que aparece el código base X de tasa_X: IF0071 ↔ IF007_1."""
    salida = [x]
    if (m := re.match(r"^(IF0\d\d)(\d)$", x)):
        salida.append(f"{m.group(1)}_{m.group(2)}")
    return salida


# =============================================================================================
# Principal
# =============================================================================================

def main():
    a = argparse.ArgumentParser()
    a.add_argument("--datos", default=DATOS_DEF)
    a.add_argument("--hasta", default=None)
    a.add_argument("--desde-validacion", default=DESDE_VALIDACION)
    a.add_argument("--cache", default=None)
    a = a.parse_args()

    ultimo = ultimo_mes_bd()
    hasta = min(a.hasta, ultimo) if a.hasta else ultimo
    meses = [m for m in sorted({f"{y}-{mm:02d}" for y in range(2021, 2028) for mm in range(1, 13)}) if "2021-05" <= m <= hasta]
    print(f"Cargando datos (meses {meses[0]}..{meses[-1]})…", file=sys.stderr, flush=True)
    if a.cache and os.path.exists(a.cache):
        with open(a.cache, "rb") as f:
            datos = pickle.load(f)
    else:
        datos = p2.DatosTiempo(a.datos, meses, ObjetivosSinNota())
        if a.cache:
            with open(a.cache, "wb") as f:
                pickle.dump(datos, f)
    entidades = [u for u, t in datos.tipo.items() if t != "SECTOR"]
    sectores = [u for u, t in datos.tipo.items() if t == "SECTOR"]
    catalogo = p2.formulas_suma_catalogo()     # SUMA ya cargadas (no se tocan)
    desde = a.desde_validacion

    def validar(f, unidades, clave, explicar=None):
        r = p2.validar(f, unidades, datos.objetivos.get(clave, {}), datos, explicar=explicar, desde=desde)
        previo = p2.validar(f, unidades, datos.objetivos.get(clave, {}), datos, explicar=explicar)
        # Entidades que solo difieren antes de `desde` (se informan, no rechazan).
        return r, len({x[0] for x in previo.fallas} - {x[0] for x in r.fallas})

    aceptadas, filas_rep = {}, collections.defaultdict(list)
    nuevas_sumas = {}          # código → términos (para tasa_X)
    excluidas = collections.Counter()

    def registrar(grupo, clave, formula, origen, r, previas, sector_txt, extra=""):
        cuadro, idsub, cod = clave
        ok = formula is not None
        detalle = (r.texto() if r.comparados else "sin datos para validar") + (
            f"; {previas} entidades difieren solo antes de {desde}" if previas else "") + extra
        filas_rep[grupo].append(f"| {cuadro} | {idsub or '—'} | `{cod}` | "
                                f"{(p2.texto_formula(formula) + ' · ' + origen) if ok else '—'} | {detalle} | {sector_txt} | "
                                f"{'✅' if ok else '❌ queda JSON'} |")

    def con_sector(clave, f, explicar_sector=None):
        cuadro, idsub, cod = clave
        if cuadro not in SECTOR_DE:
            return "—"
        clave_s = (SECTOR_DE[cuadro], idsub, cod)
        if clave_s not in datos.objetivos:
            return f"— (sin fila en {SECTOR_DE[cuadro]})"
        rs, _ = validar(f, sectores, clave_s, explicar_sector)
        if rs.ok:
            aceptadas[clave_s] = f
            excluidas.update(rs.explicadas)
            return f"✅ {SECTOR_DE[cuadro]}: {rs.texto()}"
        return f"❌ {SECTOR_DE[cuadro]}: {rs.texto() if rs.comparados else 'sin datos'}"

    # ---- 1. Cartera por estado ----
    cartera = formulas_cartera()
    for clave in sorted(k for k in datos.objetivos if k[0] in ("EFI07", "REP01") and RE_CARTERA.match(k[2])):
        cuadro, idsub, cod = clave
        if cod in catalogo:
            continue   # ya tiene SUMA (paso 1)
        tipo = re.search(r"_(\d)$", cod)
        if cuadro == "EFI07" and tipo and IDSUB_TIPO.get(idsub) != int(tipo.group(1)):
            continue
        elegida, r_e, prev_e, motivos = None, None, 0, ""
        for terminos in cartera.get(cod, []):
            f = {"tipo": "SUMA", "N": terminos, "opN": "NIVEL"}
            r, previas = validar(f, entidades, clave)
            if r.ok:
                elegida, r_e, prev_e = f, r, previas
                break
            motivos += f" · {p2.texto_formula(f)}: {r.texto()}"
        if elegida:
            aceptadas[clave] = elegida
            excluidas.update(r_e.explicadas)
            nuevas_sumas.setdefault(cod, elegida["N"])
            registrar("1. Cartera por estado", clave, elegida, "estructura de EFI07", r_e, prev_e, con_sector(clave, elegida))
        else:
            registrar("1. Cartera por estado", clave, None, "", r if cartera.get(cod) else p2.Validacion(), 0, "—", motivos)

    # ---- 2. Márgenes anualizados ----
    for clave in sorted(k for k in datos.objetivos if k[0] in ("EFI03", "REP01") and RE_ANUAL.match(k[2])):
        cod = clave[2]
        base = MARGEN_DE.get(RE_ANUAL.match(cod).group(1))
        if base not in catalogo:
            registrar("2. Márgenes anualizados", clave, None, "", p2.Validacion(), 0, "—", f" (sin SUMA {base} en el catálogo)")
            continue
        f = {"tipo": "SUMA", "N": catalogo[base], "opN": "ANUALIZADO"}
        r, previas = validar(f, entidades, clave)
        if r.ok:
            aceptadas[clave] = f
            excluidas.update(r.explicadas)
            registrar("2. Márgenes anualizados", clave, f, f"ANUALIZADO de {base}", r, previas, con_sector(clave, f))
        else:
            registrar("2. Márgenes anualizados", clave, None, "", r, previas, "—")

    # ---- 3. EFI05 PTR_9 (identidad con el propio JSON: 9 % de ACT_PON_RIESGO) ----
    clave = ("EFI05", "", "PTR_9")
    fallas, comparados, ceros = [], 0, 0
    for u in entidades:
        y = datos.objetivos.get(clave, {}).get(u)
        x = datos.objetivos.get(("EFI05", "", "ACT_PON_RIESGO"), {}).get(u)
        if y is None or x is None:
            continue
        m = np.isfinite(y) & np.isfinite(x) & np.array([mm >= desde for mm in datos.meses])
        ceros += int((m & (y == 0) & (x != 0)).sum())   # 0 publicado = valor faltante en el JSON
        m &= (y != 0) | (x == 0)
        comparados += int(m.sum())
        malos = np.flatnonzero(m & ~(np.abs(0.09 * x - y) <= 0.09 * 0.5e-4 + 0.5e-4 + 1e-9))
        fallas += [(u, datos.meses[i], float(y[i]), round(float(0.09 * x[i]), 4)) for i in malos[:2] if u not in ANOMALAS]
    ptr_ok = comparados > 0 and not fallas
    filas_rep["3. Patrimonio técnico requerido"].append(
        f"| EFI05 | — | `PTR_9` | 0,09 × ACT_PON_RIESGO (IndicadorData) | {comparados} comparados contra el ACT_PON_RIESGO "
        f"publicado, {len(fallas)} fallas{(' (p. ej. ' + str(fallas[0]) + ')') if fallas else ''}; excluidos {ceros} con 0 publicado | — (SFN05: el SP de sector no suma "
        f"indicadores) | {'✅' if ptr_ok else '❌ queda JSON'} |")
    filas_rep["3. Patrimonio técnico requerido"].append(
        "| EFI05 | — | `Ex_Patri_Reque` | — | El JSON publica 0 aunque PTC − PTR_9 ≠ 0 (p. ej. Pichincha 2026-06: "
        "2 517,65 − 1 652,43): la regla no es la diferencia | — | ❌ queda JSON |")

    # ---- 4. tasa_X de los X nuevos ----
    bases = {**{k: v for k, v in catalogo.items()}, **nuevas_sumas}
    for clave in sorted(k for k in datos.objetivos if k[0] in ("EFI07", "REP01") and RE_TASA.match(k[2])):
        cuadro, idsub, cod = clave
        x = RE_TASA.match(cod).group(1)
        nombre = next((n for n in alias(x) if n in nuevas_sumas), None)
        if nombre is None:
            continue   # su base ya estaba (paso 2) o sigue sin fórmula
        terminos = bases[nombre]
        f = {"tipo": "RATIO", "N": terminos, "D": terminos, "opN": "VARIACION_12", "opD": "RETARDO_12"}

        def explicar(u_cuadro, u_idsub):
            fila_base = next((datos.objetivos[(u_cuadro, u_idsub, n)] for n in alias(x)
                              if (u_cuadro, u_idsub, n) in datos.objetivos), {})
            return p2.explicar_tasa(f, fila_base, datos)

        r, previas = validar(f, entidades, clave, explicar(cuadro, idsub))
        if r.ok:
            aceptadas[clave] = f
            excluidas.update(r.explicadas)
            sector = con_sector(clave, f, explicar(SECTOR_DE.get(cuadro, ""), idsub))
            registrar("4. Tasas a 12 meses de las nuevas bases", clave, f, f"base {nombre}", r, previas, sector)
        else:
            registrar("4. Tasas a 12 meses de las nuevas bases", clave, None, "", r, previas, "—")

    # ---- Reporte ----
    reporte = ["# Fase 3 · paso 2b — fórmulas que no necesitan la nota técnica\n",
               f"Validación con meses reales {desde} → {meses[-1]} (último mes de la BD: {ultimo}); {len(entidades)} entidades "
               f"y {len(sectores)} sectores. Los meses 2021‑05 → 2022‑04 se informan aparte (\"antes de {desde}\"): son los "
               "primeros 12 meses de los JSON, salen de otra serie (el análisis horizontal publica 0) y en ellos la cartera por "
               "estado no cuadra en ninguna entidad. La BD empieza en 2024‑01, así que no afectan a lo que calcula el SP.\n",
               "Mismas tolerancias y exclusiones que el paso 2 (ver `formulas_tiempo_reporte.md`).\n"]
    for g in sorted(filas_rep):
        n_ok = sum(1 for x in filas_rep[g] if x.endswith("✅ |"))
        reporte.append(f"## {g}\n\n{n_ok} de {len(filas_rep[g])} aceptadas.\n")
        reporte.append("| Cuadro | Sub-vista | Código | Fórmula | Entidades | Sector | Resultado |\n|---|---|---|---|---|---|---|")
        reporte += filas_rep[g]
        reporte.append("")
    reporte.append("## Diferencias excluidas por el propio JSON (filas aceptadas)\n")
    reporte += [f"- {n:,} valores {p2.CATEGORIAS[c]}." for c, n in sorted(excluidas.items())] or ["- Ninguna."]
    reporte.append("")

    # ---- SQL ----
    filas, terminos = [], []
    for (cuadro, idsub, cod), f in sorted(aceptadas.items()):
        escala = 1e-6 if f["tipo"] == "SUMA" else 100.0
        opD = f"'{f['opD']}'" if f["tipo"] == "RATIO" else "NULL"
        filas.append(f"('{cuadro}', N'{p2.q(idsub)}', '{cod}', '{f['tipo']}', {escala!r}, '{f['opN']}', {opD})")
        for rol in ("N", "D"):
            for c, k in sorted(f.get(rol, {}).items()):
                terminos.append(f"('{cuadro}', N'{p2.q(idsub)}', '{cod}', '{rol}', 'CUENTA', {c}, NULL, {float(k)!r})")
    if ptr_ok:
        filas.append("('EFI05', N'', 'PTR_9', 'SUMA', 1e-06, 'NIVEL', NULL)")
        terminos.append("('EFI05', N'', 'PTR_9', 'N', 'INDICADOR', NULL, 'ACT_PON_RIESGO', 0.09)")
    n_total = len(filas)
    os.makedirs(SALIDA, exist_ok=True)
    with open(os.path.join(SALIDA, "07_formulas_sin_nota.sql"), "w", encoding="utf-8") as out:
        out.write(PLANTILLA_SQL.format(hasta=meses[-1], desde=desde, n=n_total,
                                       filas=",\n".join(filas), terminos=",\n".join(terminos)))
    reporte.append(f"**Aceptadas:** {n_total} fórmulas (entidad + sector).\n")
    with open(os.path.join(SALIDA, "formulas_sin_nota_reporte.md"), "w", encoding="utf-8") as out:
        out.write("\n".join(reporte) + "\n")
    print(f"Aceptadas {n_total} fórmulas.")
    return 0


PLANTILLA_SQL = """/* =====================================================================================
   Fase 3 · 07 — Fórmulas que no necesitan la nota técnica de la SB (GENERADO)
   Origen: tools/catalogo/generar_formulas_fase3_paso2b.py · validado con meses reales {desde} → {hasta}.
   Cartera por estado (EFI07, CAR01, REP01), márgenes anualizados (EFI03, SFN03, REP01), EFI05 PTR_9
   y las tasa_X de esas bases. {n} fórmulas (filas por cuadro, sub-vista y código; todas las variantes).
   Requiere 03_operaciones_tiempo.sql. Idempotente: rehace los términos de cada fila tocada.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.CuadroFila', 'OperacionN') IS NULL
    THROW 50030, N'Falta ejecutar db/fase3/03_operaciones_tiempo.sql.', 1;
GO

BEGIN TRANSACTION;

DECLARE @f TABLE (CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64), Calculo varchar(8), Escala float,
                  OperacionN varchar(16), OperacionD varchar(16));
INSERT INTO @f VALUES
{filas};

DECLARE @t TABLE (CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64), Rol char(1), Fuente varchar(10),
                  CuentaID int, IndicadorCodigo varchar(32), Factor float);
INSERT INTO @t VALUES
{terminos};

DECLARE @filas TABLE (CuadroFilaID int PRIMARY KEY, CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64));
INSERT INTO @filas
SELECT cf.CuadroFilaID, cf.CuadroID, cf.IdSub, cf.Codigo
FROM dbo.CuadroFila AS cf
INNER JOIN @f AS f ON f.CuadroID = cf.CuadroID AND f.IdSub = cf.IdSub AND f.Codigo = cf.Codigo;

DELETE t FROM dbo.CuadroFilaTermino AS t INNER JOIN @filas AS x ON x.CuadroFilaID = t.CuadroFilaID;
INSERT INTO dbo.CuadroFilaTermino (CuadroFilaID, Rol, Fuente, CuentaID, IndicadorCodigo, Factor)
SELECT x.CuadroFilaID, t.Rol, t.Fuente, t.CuentaID, t.IndicadorCodigo, t.Factor
FROM @filas AS x
INNER JOIN @t AS t ON t.CuadroID = x.CuadroID AND t.IdSub = x.IdSub AND t.Codigo = x.Codigo;
UPDATE cf SET Calculo = f.Calculo, Escala = f.Escala, OperacionN = f.OperacionN, OperacionD = f.OperacionD
FROM dbo.CuadroFila AS cf
INNER JOIN @filas AS x ON x.CuadroFilaID = cf.CuadroFilaID
INNER JOIN @f AS f ON f.CuadroID = x.CuadroID AND f.IdSub = x.IdSub AND f.Codigo = x.Codigo;
DECLARE @nf int = (SELECT COUNT(*) FROM @filas);
PRINT CONCAT(N'Fórmulas sin nota técnica: ', @nf, N' filas actualizadas');

COMMIT TRANSACTION;
PRINT N'07_formulas_sin_nota: OK';
GO
"""

if __name__ == "__main__":
    sys.exit(main())
