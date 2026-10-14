"""
Fase 3 · paso 2 — operaciones en el tiempo para filas del catálogo que hoy toman su valor del JSON.

    python tools/catalogo/generar_formulas_fase3_paso2.py [--datos <Database>] [--hasta 2026-08]

Cada fila calculada tiene una operación por rol (dbo.CuadroFila.OperacionN / OperacionD,
db/fase3/03_operaciones_tiempo.sql) que se aplica a la suma de sus términos (S) mes a mes:

    NIVEL            S_t
    ANUALIZADO       S_t × 12 / mes(t)
    VARIACION_1/12   S_t − S_{t−1} / S_t − S_{t−12}
    RETARDO_1/12     S_{t−1} / S_{t−12}
    PROMEDIO_DIC     media de S desde diciembre del año anterior hasta t (exige todos los meses)
    PROMEDIO_DIC_ROA igual, salvo en diciembre: S_t
    BASE_ROE_DIC     igual, salvo en diciembre: S_t − N_t (N sin operación: patrimonio − ganancia)

Grupos candidatos (detalle en db/fase3/PLAN_PASO2.md §1.2):
  1. Análisis horizontal EFI06/SFN06: RATIO 100 × VARIACION_12(cuenta) / RETARDO_12(cuenta).
  2. Filas @<cuenta>A de EFI03/SFN03/REP01: SUMA ANUALIZADO(cuenta).
  3. EFI04/SFN04 grupo "VARIACIÓN MENSUAL": SUMA VARIACION_1(cuenta), emparejada por texto con EFI01.
  4. tasa_X (EFI07, CAR01, REP01, EFI08, EFI10): RATIO 100 × VARIACION_12(X) / RETARDO_12(X), si X es una
     cuenta o ya tiene fórmula SUMA en el catálogo.
  5. ROA / ROE (EFI11, EFI12, EFI13, REP01, CAR05).

Validación como en el paso 1 (generar_formulas_fase3.py): todas las entidades y, si aplica, los 10
sectores, con meses reales; OYS aparte. Salida:
  db/fase3/04_formulas_tiempo.sql          UPDATE idempotente (requiere 03_operaciones_tiempo.sql)
  db/fase3/formulas_tiempo_reporte.md      aceptadas, rechazadas y por qué
Requiere numpy.
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import re
import subprocess
import sys
import unicodedata

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from descubrir_formulas import ultimo_mes_bd  # noqa: E402
from generar_formulas_fase3 import (  # noqa: E402
    ANOMALAS, DATOS_DEF, ERR_SALDO, RAIZ, SALIDA, SECTOR_DE, Datos, leer)

ID_HORIZONTAL = "Análisis Horizontal (%)"
FORMULAS_BD = os.path.join(RAIZ, "tools", "catalogo", "formulas_suma_bd.json")
GAN_EJE = {41: -1.0, 42: -1.0, 43: -1.0, 44: -1.0, 45: -1.0, 46: -1.0, 47: -1.0, 48: -1.0,
           51: 1.0, 52: 1.0, 53: 1.0, 54: 1.0, 55: 1.0, 56: 1.0}
ROA_ROE = {"EFI11": ("IF002", "IF004"), "EFI12": ("E1_ROA", "E4_ROE"), "EFI13": ("R12_ROA", "R13_ROE"),
           "CAR05": ("IF002", "IF004")}
REP01_ROA_ROE = (("IF002", "E1_ROA", "R12_ROA"), ("IF004", "E4_ROE", "R13_ROE"))
CUADROS_TASA = ("EFI07", "CAR01", "REP01", "EFI08", "EFI10", "CAR04")
GRUPO_VARIACION = "VARIACION MENSUAL"
RE_ANUALIZADO = re.compile(r"^@(\d+)A$")
RE_TASA = re.compile(r"^tasa_(.+)$")
RE_BASE_TASA = re.compile(r"^(@\d+|IF\w+)$")
# ROA → ROE publicado en el mismo cuadro (para comprobar la consistencia del propio JSON).
ROE_DE = {"IF002": "IF004", "E1_ROA": "E4_ROE", "R12_ROA": "R13_ROE"}
# Equivalencias de texto EFI04 → EFI01 cuando no coinciden literalmente.
SINONIMOS_EFI04 = {"PASIVOS": "PASIVO", "OPE. INTERBANCARIAS": "OPERACIONES INTERBANCARIAS"}


def normal(texto):
    t = unicodedata.normalize("NFKD", str(texto or "")).encode("ascii", "ignore").decode()
    return re.sub(r"\s+", " ", t).strip().upper()


# =============================================================================================
# Datos: amplía la carga del paso 1 con el análisis horizontal y las filas sin código de EFI04
# =============================================================================================

class Objetivos:
    """Qué filas del JSON se cargan como objetivo (clave = (cuadro, idsub, código))."""

    def __contains__(self, clave):
        cuadro, _, cod = clave
        if not cod:
            return False
        if cuadro in ("EFI03", "SFN03", "REP01") and RE_ANUALIZADO.match(cod):
            return True
        if cuadro in CUADROS_TASA and (RE_TASA.match(cod) or RE_BASE_TASA.match(cod)):
            return True   # la tasa y su fila base (para comprobar la consistencia del propio JSON)
        return cod in ROA_ROE.get(cuadro, ()) or (cuadro == "REP01" and any(cod in g for g in REP01_ROA_ROE))


class DatosTiempo(Datos):
    def __init__(self, datos, meses, objetivos=None):
        self.horizontal = {}
        self.textos_efi01 = collections.defaultdict(set)   # texto normalizado → cuentas (EFI01, por CUC)
        super().__init__(datos, meses, objetivos or Objetivos())
        self.mes = np.array([int(m[5:7]) for m in meses])

    def _balance(self, unidad, filas):
        super()._balance(unidad, filas)
        self.horizontal[unidad] = {int(str(x["Codigo_Base"]).strip()): self._serie(x) for x in filas
                                   if x.get("ID") == ID_HORIZONTAL and str(x.get("Codigo_Base", "")).strip().isdigit()}

    def _objetivos(self, unidad, filas, codigos):
        super()._objetivos(unidad, filas, codigos)
        for x in filas:
            cuadro = x.get("Cuadro")
            if cuadro in ("EFI04", "SFN04") and not x.get("CUC") and normal(x.get("Grupo")) == GRUPO_VARIACION:
                self.objetivos[(cuadro, "", "|" + x.get("Variable", ""))].setdefault(unidad, self._serie(x))
            elif cuadro == "EFI01" and str(x.get("CUC", "")).startswith("@") and x["CUC"][1:].isdigit():
                self.textos_efi01[normal(x.get("Variable"))].add(int(x["CUC"][1:]))


# =============================================================================================
# Operaciones (espejo de api.ObtenerValoresCuadro* en 03_operaciones_tiempo.sql)
# =============================================================================================

def suma_rol(terminos, saldos, n):
    """S del rol: Σ factor·saldo; NaN en los meses sin ningún término con dato (como el SP)."""
    total, hay = np.zeros(n), np.zeros(n, dtype=bool)
    for c, k in terminos.items():
        s = saldos.get(int(c))
        if s is None:
            continue
        ok = np.isfinite(s)
        total = total + k * np.where(ok, s, 0.0)
        hay |= ok
    return np.where(hay, total, np.nan)


def retardo(s, k):
    r = np.full_like(s, np.nan)
    r[k:] = s[:-k]
    return r


def promedio_dic(s, mes):
    """Media de s[t−mes .. t] (diciembre anterior → t); NaN si falta algún mes o la ventana no alcanza."""
    out = np.full_like(s, np.nan)
    for t in range(len(s)):
        i = t - int(mes[t])
        if i >= 0:
            v = s[i:t + 1]
            if np.all(np.isfinite(v)):
                out[t] = v.mean()
    return out


def operar(op, S, mes, N_bruto=None):
    if op == "NIVEL":
        return S
    if op == "ANUALIZADO":
        return S * 12.0 / mes
    if op == "VARIACION_1":
        return S - retardo(S, 1)
    if op == "VARIACION_12":
        return S - retardo(S, 12)
    if op == "RETARDO_1":
        return retardo(S, 1)
    if op == "RETARDO_12":
        return retardo(S, 12)
    p = promedio_dic(S, mes)
    if op == "PROMEDIO_DIC":
        return p
    if op == "PROMEDIO_DIC_ROA":
        return np.where(mes == 12, S, p)
    if op == "BASE_ROE_DIC":
        return np.where(mes == 12, S - N_bruto, p)
    raise ValueError(op)


def error_rol(op, n_terminos, mes, n_terminos_n=0):
    """Error máximo por redondeo de los saldos publicados (4 decimales en millones) tras la operación."""
    e = n_terminos * ERR_SALDO
    if op == "ANUALIZADO":
        return e * 12.0 / mes
    if op.startswith("VARIACION"):
        return np.full(len(mes), 2 * e)
    if op == "BASE_ROE_DIC":
        return np.where(mes == 12, e + n_terminos_n * ERR_SALDO, e)
    return np.full(len(mes), e)


def evaluar(formula, saldos, mes):
    """→ (predicción, tolerancia sin el redondeo del objetivo)."""
    n = len(mes)
    opN, opD = formula.get("opN", "NIVEL"), formula.get("opD", "NIVEL")
    SN = suma_rol(formula["N"], saldos, n)
    N = operar(opN, SN, mes)
    eN = error_rol(opN, len(formula["N"]), mes)
    if formula["tipo"] == "SUMA":
        return N, 1.05 * eN
    SD = suma_rol(formula["D"], saldos, n)
    D = operar(opD, SD, mes, SN)
    eD = error_rol(opD, len(formula["D"]), mes, len(formula["N"]))
    with np.errstate(divide="ignore", invalid="ignore"):
        p = np.where(D != 0, 100 * N / D, np.nan)
        # Cota exacta de |N'/D' − N/D| con |N'−N| ≤ eN, |D'−D| ≤ eD (las bases chicas, p. ej. 0,0005
        # millones, tienen un error relativo grande que la aproximación lineal subestima).
        tol = np.where(np.abs(D) > eD, 1.05 * 100 * ((np.abs(N) + eN) / (np.abs(D) - eD) - np.abs(N) / np.abs(D)), np.inf)
    return p, tol


def decimales(y):
    v = y[np.isfinite(y)]
    return 2 if len(v) and np.all(np.abs(v * 100 - np.round(v * 100)) < 1e-6) else 4


class Validacion:
    """Resultado de validar una fórmula: fallas reales y fallas explicadas por el propio JSON."""

    def __init__(self):
        self.comparados, self.fallas, self.anomalas = 0, [], []
        self.explicadas = collections.Counter()
        self.ejemplos = {}

    @property
    def ok(self):
        return self.comparados > 0 and not self.fallas

    def texto(self):
        t = f"{self.comparados} comparados, {len(self.fallas)} fallas"
        if self.fallas:
            t += f" (p. ej. {self.fallas[0]})"
        if self.explicadas:
            t += "; excluidas por el propio JSON: " + ", ".join(
                f"{n} {CATEGORIAS[c]} (p. ej. {self.ejemplos[c]})" for c, n in sorted(self.explicadas.items()))
        if self.anomalas:
            t += f" (OYS: {len(self.anomalas)} fallas de datos)"
        return t


CATEGORIAS = {
    "cero": "con 0 publicado (valor faltante en el JSON)",
    "base": "en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce",
    "hermano": "en que el ROA publicado contradice el ROE publicado, que la fórmula sí reproduce",
}


def validar(formula, unidades, objetivo_por_unidad, datos, dec=None, explicar=None, desde=None):
    """
    Compara en las unidades con dato y balance ese mes. Una diferencia se descarta (y se informa
    aparte) solo si el propio JSON la explica: 0 publicado en un cociente, o explicar(u, i, y, p)
    devuelve una categoría (inconsistencia del JSON consigo mismo, comprobada con otra fila publicada).
    desde: primer mes que se compara (None = todos).
    """
    r = Validacion()
    for u in unidades:
        y = objetivo_por_unidad.get(u)
        sal = datos.saldos.get(u)
        if y is None or not sal or 1 not in sal:
            continue
        p, tol = evaluar(formula, sal, datos.mes)
        d = dec if dec is not None else decimales(y)
        tol = tol + 0.5 * 10 ** -d + 1e-6 * np.abs(y) + 1e-9
        activo = sal[1]
        m = np.isfinite(y) & np.isfinite(p) & np.isfinite(activo) & (activo != 0)
        if desde is not None:
            m &= np.array([x >= desde for x in datos.meses])
        malos = np.flatnonzero(m & ~(np.abs(p - y) <= tol))
        r.comparados += int(m.sum())
        reales = []
        for i in malos:
            cat = "cero" if formula["tipo"] == "RATIO" and y[i] == 0 else (explicar(u, i, y[i], p[i]) if explicar else None)
            if cat:
                r.explicadas[cat] += 1
                r.ejemplos.setdefault(cat, (u, datos.meses[i], float(y[i]), round(float(p[i]), 4)))
            else:
                reales.append(i)
        destino = r.anomalas if u in ANOMALAS else r.fallas
        for i in reales[:3]:
            destino.append((u, datos.meses[i], float(y[i]), round(float(p[i]), 6)))
    return r


def explicar_tasa(formula, base_por_unidad, datos):
    """La tasa publicada contradice la que sale de su propia fila base X del JSON, y la fórmula sí la reproduce."""
    def f(u, i, y, p):
        b = base_por_unidad.get(u)
        if b is None or i < 12 or not np.isfinite(b[i]) or not np.isfinite(b[i - 12]) or abs(b[i - 12]) <= ERR_SALDO:
            return None
        propia = 100 * (b[i] / b[i - 12] - 1)
        tol = 100 * ((abs(b[i]) + ERR_SALDO) / (abs(b[i - 12]) - ERR_SALDO) - abs(b[i]) / abs(b[i - 12])) + 1e-3
        n_terminos = len(formula["N"])
        tol_p = tol * max(1, n_terminos)
        return "base" if abs(propia - y) > tol and abs(propia - p) <= tol_p else None
    return f


def explicar_roa(datos, roe_por_unidad):
    """El ROA publicado contradice el ROE publicado: ROE_json × base_ROE / base_ROA reproduce la predicción."""
    n = len(datos.meses)

    def f(u, i, y, p):
        roe = roe_por_unidad.get(u)
        sal = datos.saldos.get(u)
        if roe is None or not np.isfinite(roe[i]):
            return None
        SN = suma_rol(GAN_EJE, sal, n)
        d_roa = operar("PROMEDIO_DIC_ROA", suma_rol({1: 1.0}, sal, n), datos.mes)[i]
        d_roe = operar("BASE_ROE_DIC", suma_rol({3: 1.0}, sal, n), datos.mes, SN)[i]
        if not d_roa or not np.isfinite(d_roe):
            return None
        implicito = roe[i] * d_roe / d_roa
        return "hermano" if abs(implicito - p) <= 1e-3 * abs(p) + 0.01 else None
    return f


# =============================================================================================
# Fórmulas SUMA del catálogo (para tasa_X): de la BD; si no hay conexión, del último volcado
# =============================================================================================

def formulas_suma_catalogo():
    consulta = ("SET NOCOUNT ON; SELECT f.Codigo + '|' + STRING_AGG(CONCAT(t.CuentaID, ':', t.Factor), ',') "
                "FROM dbo.CuadroFila f JOIN dbo.CuadroFilaTermino t ON t.CuadroFilaID = f.CuadroFilaID "
                "WHERE f.Calculo = 'SUMA' AND f.Codigo IS NOT NULL AND f.Codigo NOT LIKE '@%' AND t.Fuente = 'CUENTA' "
                "GROUP BY f.CuadroFilaID, f.Codigo")
    try:
        cfg = open(os.path.join(RAIZ, "BackendDF", "src", "BackendDF", "appsettings.json"), encoding="utf-8").read()
        cadena = re.search(r'"DefaultConnection"\s*:\s*"([^"]+)"', cfg).group(1)
        kv = {k.strip().lower(): v for k, v in (p.split("=", 1) for p in cadena.split(";") if "=" in p)}
        args = ["sqlcmd", "-S", kv["server"], "-d", kv["database"], "-C", "-l", "5", "-h", "-1", "-W", "-I", "-Q", consulta]
        args += ["-U", kv["user id"], "-P", kv.get("pwd") or kv.get("password", "")] if "user id" in kv else ["-E"]
        salida = subprocess.run(args, capture_output=True, text=True, timeout=30)
        formulas = {}
        for linea in salida.stdout.splitlines():
            if "|" not in linea:
                continue
            cod, terminos = linea.strip().split("|", 1)
            formulas.setdefault(cod, {int(c): float(k) for c, k in (x.split(":") for x in terminos.split(","))})
        if salida.returncode == 0 and formulas:
            with open(FORMULAS_BD, "w", encoding="utf-8") as out:
                json.dump({k: {str(c): v for c, v in f.items()} for k, f in sorted(formulas.items())}, out, indent=1)
            return formulas
    except Exception:
        pass
    if os.path.exists(FORMULAS_BD):
        return {k: {int(c): v for c, v in f.items()} for k, f in leer(FORMULAS_BD).items()}
    return {}


def base_tasa(x, sumas):
    """Términos de X en tasa_X: una cuenta, o una SUMA del catálogo (también IF0115 → IF011_5)."""
    if x.isdigit():
        return {int(x): 1.0}, f"cuenta {x}"
    for alias in (x, x[:-1] + "_" + x[-1] if re.match(r"^IF\d{4}$", x) else None):
        if alias and alias in sumas:
            return sumas[alias], f"SUMA {alias}"
    return None, None


# =============================================================================================
# Principal
# =============================================================================================

def q(texto):
    return texto.replace("'", "''")


def texto_formula(f):
    def lado(t):
        return " ".join(f"{'+' if k > 0 else '−'}{c}" for c, k in sorted(t.items()))
    if f["tipo"] == "SUMA":
        return f"{f['opN']}({lado(f['N'])})"
    return f"100·{f['opN']}({lado(f['N'])}) / {f['opD']}({lado(f['D'])})"


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--datos", default=DATOS_DEF)
    p.add_argument("--hasta", default=None)
    p.add_argument("--cache", default=None, help="pickle con los datos cargados (solo para iterar más rápido)")
    a = p.parse_args()

    ultimo = ultimo_mes_bd()
    hasta = min(a.hasta, ultimo) if a.hasta else ultimo
    meses = [m for m in sorted({f"{y}-{mm:02d}" for y in range(2021, 2028) for mm in range(1, 13)}) if "2021-05" <= m <= hasta]
    print(f"Cargando datos (meses {meses[0]}..{meses[-1]}, último mes BD {ultimo})…", file=sys.stderr, flush=True)
    if a.cache and os.path.exists(a.cache):
        import pickle
        with open(a.cache, "rb") as f:
            datos = pickle.load(f)
    else:
        datos = DatosTiempo(a.datos, meses)
        if a.cache:
            import pickle
            with open(a.cache, "wb") as f:
                pickle.dump(datos, f)
    entidades = [u for u, t in datos.tipo.items() if t != "SECTOR"]
    sectores = [u for u, t in datos.tipo.items() if t == "SECTOR"]
    sumas = formulas_suma_catalogo()

    reporte = ["# Fase 3 · paso 2 — operaciones en el tiempo\n",
               f"Validación con meses reales {meses[0]} → {meses[-1]} (último mes de la BD: {ultimo}); "
               f"{len(entidades)} entidades y {len(sectores)} sectores. Los meses sin historia suficiente "
               "(p. ej. el análisis horizontal antes de 2022‑05) no se comparan.\n",
               "Tolerancia: redondeo de los saldos publicados (4 decimales en millones) propagado por la operación "
               "y el cociente, más el redondeo del valor publicado (2 o 4 decimales, según la serie).\n"]

    # ---- 1. Análisis horizontal ----
    reporte.append("## 1. Análisis horizontal (EFI06 / SFN06)\n")
    reporte.append("Regla: 100 × VARIACION_12(cuenta) / RETARDO_12(cuenta) = 100 × (saldo_t / saldo_{t−12} − 1). "
                   "Publicado con 2 decimales.\n")
    reporte.append("| Ámbito | Tipo | Valores comparados | Fallas | Excluidas: 0 publicado | Excluidas: historia reprocesada "
                   "| Ejemplos de falla |\n|---|---|---|---|---|---|---|")
    fallas_ah, reprocesos = {}, []
    for ambito, unidades in (("Entidad", entidades), ("Sector", sectores)):
        por_tipo = collections.defaultdict(lambda: [0, [], 0, 0])   # comparados, fallas, 0 publicado, reproceso
        for u in unidades:
            sal = datos.saldos.get(u, {})
            malos_u = collections.defaultdict(list)   # mes → [(cuenta, y, p)]
            e = por_tipo["OYS (anómala)" if u in ANOMALAS else datos.tipo[u]]
            for c, y in datos.horizontal.get(u, {}).items():
                if c not in sal:
                    continue
                f = {"tipo": "RATIO", "N": {c: 1.0}, "D": {c: 1.0}, "opN": "VARIACION_12", "opD": "RETARDO_12"}
                p_, tol = evaluar(f, sal, datos.mes)
                tol = tol + 0.5e-2 + 1e-9
                m = np.isfinite(y) & np.isfinite(p_)
                e[0] += int(m.sum())
                for i in np.flatnonzero(m & ~(np.abs(p_ - y) <= tol)):
                    if y[i] == 0:
                        e[2] += 1
                    else:
                        malos_u[i].append((c, float(y[i]), round(float(p_[i]), 4)))
            # Reproceso: en ese mes falla alguna cuenta agregada (1 o 2 dígitos), o sea, el JSON publicó la
            # tasa con un balance de t−12 distinto del que publica hoy (historia corregida después).
            for i, lista in sorted(malos_u.items()):
                if any(c < 100 for c, _, _ in lista):
                    e[3] += len(lista)
                    reprocesos.append((u, meses[i], len(lista)))
                else:
                    e[1] += [(u, c, meses[i], y_, p2) for c, y_, p2 in lista]
        for t, (n, f, c0, rp) in sorted(por_tipo.items()):
            fallas_ah[(ambito, t)] = len(f)
            reporte.append(f"| {ambito} | {t} | {n:,} | {len(f)} | {c0:,} | {rp:,} | {'; '.join(map(str, f[:3]))} |")
    acepta_ah = sum(n for (amb, t), n in fallas_ah.items() if t != "OYS (anómala)") == 0
    reporte.append(f"\n**Resultado:** {'✅ se carga como RATIO en EFI06 y SFN06' if acepta_ah else '❌ no se carga (hay fallas)'}.\n")
    por_entidad = collections.Counter(u for u, _, _ in reprocesos)
    reporte.append(f"**Historia reprocesada:** {len(reprocesos)} entidad‑meses de {len(por_entidad)} entidades en que el JSON "
                   "publicó el análisis horizontal con un balance del año anterior distinto del que publica hoy: en ese mes "
                   "falla también una cuenta agregada (1 o 2 dígitos; casi siempre el ACTIVO). La BD tiene el balance "
                   "corregido, así que el SP da la tasa coherente con los saldos publicados. Entidades con más meses: "
                   + ", ".join(f"{u} ({n})" for u, n in por_entidad.most_common(8)) + ".\n")
    reporte.append("**0 publicado:** el JSON pone 0 cuando no puede calcular la tasa; esos valores no se comparan.\n")

    # ---- 2..5. Filas con código (y EFI04 por texto) ----
    candidatos = collections.defaultdict(list)   # (cuadro, idsub, código) → [(fórmula, origen)]
    claves = sorted(datos.objetivos)
    for (cuadro, idsub, cod) in claves:
        if cuadro in ("SFN03", "SFN04", "CAR01", "CAR04", "CAR05"):
            continue   # se validan como sector del cuadro de entidad
        if (m := RE_ANUALIZADO.match(cod or "")) and cuadro in ("EFI03", "REP01"):
            candidatos[(cuadro, idsub, cod)].append(
                ({"tipo": "SUMA", "N": {int(m.group(1)): 1.0}, "opN": "ANUALIZADO"}, f"cuenta {m.group(1)}"))
        elif cuadro == "EFI04" and cod.startswith("|"):
            texto = normal(cod[1:])
            cuentas = sorted(datos.textos_efi01.get(SINONIMOS_EFI04.get(texto, texto), ()), key=lambda c: len(str(c)))
            for c in cuentas:
                candidatos[(cuadro, idsub, cod)].append(
                    ({"tipo": "SUMA", "N": {c: 1.0}, "opN": "VARIACION_1"}, f"EFI01 @{c}"))
        elif (m := RE_TASA.match(cod or "")) and cuadro in CUADROS_TASA:
            terminos, origen = base_tasa(m.group(1), sumas)
            if terminos:
                candidatos[(cuadro, idsub, cod)].append(({"tipo": "RATIO", "N": terminos, "D": terminos,
                                                          "opN": "VARIACION_12", "opD": "RETARDO_12"}, origen))
            else:
                candidatos[(cuadro, idsub, cod)]   # sin base: se informa
        elif cod in ("IF002", "E1_ROA", "R12_ROA"):
            for opD in ("PROMEDIO_DIC_ROA", "PROMEDIO_DIC"):
                candidatos[(cuadro, idsub, cod)].append(({"tipo": "RATIO", "N": GAN_EJE, "D": {1: 1.0},
                                                          "opN": "ANUALIZADO", "opD": opD}, "Gan_Eje / activo"))
        elif cod in ("IF004", "E4_ROE", "R13_ROE"):
            for opD in ("BASE_ROE_DIC", "PROMEDIO_DIC_ROA", "PROMEDIO_DIC"):
                candidatos[(cuadro, idsub, cod)].append(({"tipo": "RATIO", "N": GAN_EJE, "D": {3: 1.0},
                                                          "opN": "ANUALIZADO", "opD": opD}, "Gan_Eje / patrimonio"))

    grupo_de = lambda cuadro, cod: ("2. Anualizado (@cuentaA)" if RE_ANUALIZADO.match(cod) else
                                    "3. Variación mensual (EFI04)" if cod.startswith("|") else
                                    "4. Tasas a 12 meses (tasa_X)" if RE_TASA.match(cod) else "5. ROA / ROE")
    def explicador(cuadro, idsub, cod, f):
        if (m := RE_TASA.match(cod)):
            x = m.group(1)
            return explicar_tasa(f, datos.objetivos.get((cuadro, idsub, "@" + x if x.isdigit() else x), {}), datos)
        if cod in ROE_DE:
            return explicar_roa(datos, datos.objetivos.get((cuadro, idsub, ROE_DE[cod]), {}))
        return None

    aceptadas, filas_rep, excluidas = {}, collections.defaultdict(list), collections.Counter()
    for clave in sorted(candidatos, key=lambda k: (grupo_de(k[0], k[2]), k)):
        cuadro, idsub, cod = clave
        elegida, detalle, origen = None, "sin base con fórmula en el catálogo" if not candidatos[clave] else "", ""
        for f, org in candidatos[clave]:
            r = validar(f, entidades, datos.objetivos.get(clave, {}), datos, explicar=explicador(cuadro, idsub, cod, f))
            if not r.ok:
                motivo = "sin datos para validar (la cuenta no aparece en los balances del JSON)" if r.comparados == 0 else r.texto()
                detalle += f"{texto_formula(f)}: {motivo}. "
                continue
            elegida, origen, detalle = f, org, r.texto()
            excluidas.update(r.explicadas)
            break
        sector_txt = "—"
        if elegida:
            aceptadas[clave] = elegida
            if cuadro in SECTOR_DE:
                clave_s = (SECTOR_DE[cuadro], idsub, cod)
                rs = validar(elegida, sectores, datos.objetivos.get(clave_s, {}), datos,
                             explicar=explicador(SECTOR_DE[cuadro], idsub, cod, elegida))
                if rs.ok:
                    aceptadas[clave_s] = elegida
                    excluidas.update(rs.explicadas)
                    sector_txt = f"✅ {SECTOR_DE[cuadro]}: {rs.texto()}"
                else:
                    sector_txt = f"❌ {SECTOR_DE[cuadro]}: {rs.texto() if rs.comparados else 'sin datos'}"
        filas_rep[grupo_de(cuadro, cod)].append(
            f"| {cuadro} | {idsub or '—'} | `{cod.lstrip('|')}` | {(texto_formula(elegida) + ' · ' + origen) if elegida else '—'} | "
            f"{detalle.strip()} | {sector_txt} | {'✅' if elegida else '❌ queda JSON'} |")

    for g in sorted(filas_rep):
        n_ok = sum(1 for x in filas_rep[g] if x.endswith("✅ |"))
        reporte.append(f"## {g}\n\n{n_ok} de {len(filas_rep[g])} aceptadas.\n")
        reporte.append("| Cuadro | Sub-vista | Código / texto | Fórmula | Entidades | Sector | Resultado |\n|---|---|---|---|---|---|---|")
        reporte += filas_rep[g]
        reporte.append("")
    reporte.append("## Diferencias excluidas por el propio JSON (filas aceptadas)\n")
    reporte.append("Una diferencia solo se excluye si el propio JSON la explica; se cuentan aquí para que quede a la vista.\n")
    reporte += [f"- {n:,} valores {CATEGORIAS[c]}." for c, n in sorted(excluidas.items())] or ["- Ninguna."]
    reporte.append("")
    reporte.append("## Fuera de este paso\n")
    reporte.append("- EFI04/SFN04 grupos FUENTES, USOS y RESULTADOS: dependen del signo de la variación (no lineales); quedan JSON.")
    reporte.append("- tasa_X cuya X no es una cuenta ni tiene SUMA en el catálogo: se resuelven cuando X tenga fórmula (paso 3).")

    # ---- SQL ----
    os.makedirs(SALIDA, exist_ok=True)
    codigos, terminos, efi04, efi04_t = [], [], [], []
    for (cuadro, idsub, cod), f in sorted(aceptadas.items()):
        escala = 1e-6 if f["tipo"] == "SUMA" else 100.0
        opD = f"'{f['opD']}'" if f["tipo"] == "RATIO" else "NULL"
        if cod.startswith("|"):
            efi04.append(f"('{cuadro}', N'{q(cod[1:])}', '{f['tipo']}', {escala!r}, '{f['opN']}', {opD})")
            dest, key = efi04_t, f"('{cuadro}', N'{q(cod[1:])}'"
        else:
            codigos.append(f"('{cuadro}', N'{q(idsub)}', '{cod}', '{f['tipo']}', {escala!r}, '{f['opN']}', {opD})")
            dest, key = terminos, f"('{cuadro}', N'{q(idsub)}', '{cod}'"
        for rol in ("N", "D"):
            for c, k in sorted(f.get(rol, {}).items()):
                dest.append(f"{key}, '{rol}', {c}, {float(k)!r})")
    vacio7 = "('NINGUNO', N'', 'NINGUNO', 'SUMA', 1.0, 'NIVEL', NULL)"
    with open(os.path.join(SALIDA, "04_formulas_tiempo.sql"), "w", encoding="utf-8") as out:
        out.write(PLANTILLA_SQL.format(
            hasta=meses[-1], n_codigos=len(codigos), n_efi04=len(efi04), ah=1 if acepta_ah else 0,
            codigos=",\n".join(codigos) or vacio7,
            terminos=",\n".join(terminos) or "('NINGUNO', N'', 'NINGUNO', 'N', 0, 0.0)",
            efi04=",\n".join(efi04) or "('NINGUNO', N'', 'SUMA', 1.0, 'NIVEL', NULL)",
            efi04_t=",\n".join(efi04_t) or "('NINGUNO', N'', 'N', 0, 0.0)"))
    reporte.append(f"\n**Aceptadas:** {len(aceptadas)} fórmulas (entidad + sector) · análisis horizontal: {'sí' if acepta_ah else 'no'}.\n")
    with open(os.path.join(SALIDA, "formulas_tiempo_reporte.md"), "w", encoding="utf-8") as out:
        out.write("\n".join(reporte) + "\n")
    print(f"Aceptadas {len(aceptadas)} fórmulas; análisis horizontal {'OK' if acepta_ah else 'con fallas'}.")
    return 0


PLANTILLA_SQL = """/* =====================================================================================
   Fase 3 · 04 — Fórmulas con operaciones en el tiempo (GENERADO)
   Origen: tools/catalogo/generar_formulas_fase3_paso2.py · validado con meses reales hasta {hasta}.
   Requiere 03_operaciones_tiempo.sql (columnas OperacionN / OperacionD y SPs).
   - Análisis horizontal de EFI06/SFN06 (todas las variantes): 100 × VARIACION_12 / RETARDO_12 de la cuenta.
   - {n_codigos} filas con código (anualizados @cuentaA, tasa_X, ROA, ROE).
   - {n_efi04} filas de EFI04/SFN04 "VARIACIÓN MENSUAL" (sin código: se identifican por su texto).
   Idempotente: rehace los términos de cada fila tocada. No crea ni borra filas.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.CuadroFila', 'OperacionN') IS NULL
    THROW 50030, N'Falta ejecutar db/fase3/03_operaciones_tiempo.sql.', 1;
GO

BEGIN TRANSACTION;

/* ---------- 1. Análisis horizontal ---------- */
IF {ah} = 1
BEGIN
    DECLARE @ah TABLE (CuadroFilaID int PRIMARY KEY, CuentaID int NOT NULL);
    INSERT INTO @ah (CuadroFilaID, CuentaID)
    SELECT f.CuadroFilaID, f.CuentaID
    FROM dbo.CuadroFila AS f
    WHERE f.CuadroID IN ('EFI06', 'SFN06') AND f.IdSub = N'Análisis Horizontal (%)' AND f.CuentaID IS NOT NULL;

    DELETE t FROM dbo.CuadroFilaTermino AS t INNER JOIN @ah AS a ON a.CuadroFilaID = t.CuadroFilaID;
    INSERT INTO dbo.CuadroFilaTermino (CuadroFilaID, Rol, Fuente, CuentaID, Factor)
    SELECT CuadroFilaID, 'N', 'CUENTA', CuentaID, 1 FROM @ah
    UNION ALL
    SELECT CuadroFilaID, 'D', 'CUENTA', CuentaID, 1 FROM @ah;
    UPDATE f SET Calculo = 'RATIO', Escala = 100, OperacionN = 'VARIACION_12', OperacionD = 'RETARDO_12'
    FROM dbo.CuadroFila AS f INNER JOIN @ah AS a ON a.CuadroFilaID = f.CuadroFilaID;
    DECLARE @nah int = (SELECT COUNT(*) FROM @ah);
    PRINT CONCAT(N'Análisis horizontal: ', @nah, N' filas → RATIO VARIACION_12 / RETARDO_12');
END

/* ---------- 2. Filas con código ---------- */
DECLARE @f TABLE (CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64), Calculo varchar(8), Escala float,
                  OperacionN varchar(16), OperacionD varchar(16));
INSERT INTO @f VALUES
{codigos};

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
UPDATE cf SET Calculo = f.Calculo, Escala = f.Escala, OperacionN = f.OperacionN, OperacionD = f.OperacionD
FROM dbo.CuadroFila AS cf
INNER JOIN @filas AS x ON x.CuadroFilaID = cf.CuadroFilaID
INNER JOIN @f AS f ON f.CuadroID = x.CuadroID AND f.IdSub = x.IdSub AND f.Codigo = x.Codigo;
DECLARE @nf int = (SELECT COUNT(*) FROM @filas);
PRINT CONCAT(N'Filas con código: ', @nf, N' actualizadas');

/* ---------- 3. EFI04 / SFN04 "VARIACIÓN MENSUAL" (por texto) ---------- */
DECLARE @v TABLE (CuadroID varchar(16), Etiqueta nvarchar(400), Calculo varchar(8), Escala float,
                  OperacionN varchar(16), OperacionD varchar(16));
INSERT INTO @v VALUES
{efi04};

DECLARE @vt TABLE (CuadroID varchar(16), Etiqueta nvarchar(400), Rol char(1), CuentaID int, Factor float);
INSERT INTO @vt VALUES
{efi04_t};

DECLARE @vf TABLE (CuadroFilaID int PRIMARY KEY, CuadroID varchar(16), Etiqueta nvarchar(400));
INSERT INTO @vf
SELECT cf.CuadroFilaID, cf.CuadroID, cf.Etiqueta
FROM dbo.CuadroFila AS cf
INNER JOIN @v AS v ON v.CuadroID = cf.CuadroID AND v.Etiqueta = cf.Etiqueta
WHERE cf.Codigo IS NULL
  AND REPLACE(UPPER(cf.Grupo), N'Ó', N'O') = N'VARIACION MENSUAL';

DELETE t FROM dbo.CuadroFilaTermino AS t INNER JOIN @vf AS x ON x.CuadroFilaID = t.CuadroFilaID;
INSERT INTO dbo.CuadroFilaTermino (CuadroFilaID, Rol, Fuente, CuentaID, Factor)
SELECT x.CuadroFilaID, t.Rol, 'CUENTA', t.CuentaID, t.Factor
FROM @vf AS x
INNER JOIN @vt AS t ON t.CuadroID = x.CuadroID AND t.Etiqueta = x.Etiqueta;
UPDATE cf SET Calculo = v.Calculo, Escala = v.Escala, OperacionN = v.OperacionN, OperacionD = v.OperacionD
FROM dbo.CuadroFila AS cf
INNER JOIN @vf AS x ON x.CuadroFilaID = cf.CuadroFilaID
INNER JOIN @v AS v ON v.CuadroID = x.CuadroID AND v.Etiqueta = x.Etiqueta;
DECLARE @nv int = (SELECT COUNT(*) FROM @vf);
PRINT CONCAT(N'Variación mensual (EFI04/SFN04): ', @nv, N' filas actualizadas');

COMMIT TRANSACTION;
PRINT N'04_formulas_tiempo: OK';
GO
"""

if __name__ == "__main__":
    sys.exit(main())
