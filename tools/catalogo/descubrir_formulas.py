"""
Descubre fórmulas para las filas del catálogo que todavía toman su valor del JSON (Calculo = JSON),
usando los JSON publicados como oráculo.

    python tools/catalogo/descubrir_formulas.py [--datos <Database>] [--salida tools/catalogo/formulas_descubiertas.json]

Para cada fila pendiente de los cuadros de entidad (EFI*, REP01) intenta, en este orden:
  0. Operaciones en el tiempo, primero porque son baratas (solo se DETECTAN: el catálogo aún no
     las soporta): códigos "…A" → anualizado (x × 12 / mes); "tasa_X" → tasa anual sobre X;
     también variación mensual (x_t − x_t-1) y tasa mensual.
  1. SUMA   : Σ ±cuenta (regresión por nivel de cuenta; si no, búsqueda voraz con coeficientes ±1).
  2. RATIO  : 100 × A / B, con A y B cuentas o filas ya resueltas como SUMA.
Solo se usan meses reales: hasta el último mes de la BD (api.vVentana.Hasta, por defecto 2026-08).
Las columnas posteriores del JSON son proyecciones y nunca se usan para validar.
Una fórmula se acepta solo si reproduce el JSON en TODAS las observaciones de la muestra
(entidades × meses) con la tolerancia del redondeo publicado (4 decimales).
Las fórmulas aceptadas se validan además contra los cuadros de sector (SFN/CAR) con el mismo código.

Salida: JSON con, por (cuadro, sub-vista, código): tipo, términos, escala, error máximo y n.
Requiere numpy.
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import random
import re
import sys

import numpy as np

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATOS_DEF = os.path.join(RAIZ, "BackendDF", "src", "BackendDF", "Database")
SALIDA_DEF = os.path.join(RAIZ, "tools", "catalogo", "formulas_descubiertas.json")
MENSUAL = re.compile(r"^\d{4}-\d{2}$")
ID_SALDO = "Saldo Millones USD"
CUADROS = ("EFI03", "EFI04", "EFI05", "EFI06", "EFI07", "EFI10", "EFI11", "EFI12", "EFI13", "REP01")
# Equivalencias entidad → sector (misma plantilla de códigos).
SECTOR_DE = {"EFI03": "SFN03", "EFI04": "SFN04", "EFI05": "SFN05", "EFI07": "CAR01", "EFI10": "CAR04", "EFI11": "CAR05"}


HASTA_POR_DEFECTO = "2026-08"


def ultimo_mes_bd(raiz=RAIZ):
    """
    Último mes con datos reales (api.vVentana.Hasta, db/fase0/02_vistas.sql) usando sqlcmd y la
    cadena DefaultConnection del backend. Si no hay conexión, HASTA_POR_DEFECTO.
    """
    import subprocess
    try:
        cfg = open(os.path.join(raiz, "BackendDF", "src", "BackendDF", "appsettings.json"), encoding="utf-8").read()
        cadena = re.search(r'"DefaultConnection"\s*:\s*"([^"]+)"', cfg).group(1)
        kv = {k.strip().lower(): v for k, v in (p.split("=", 1) for p in cadena.split(";") if "=" in p)}
        args = ["sqlcmd", "-S", kv["server"], "-d", kv["database"], "-C", "-l", "5", "-h", "-1", "-W",
                "-Q", "SET NOCOUNT ON; SELECT Hasta FROM api.vVentana"]
        if "user id" in kv:
            args += ["-U", kv["user id"], "-P", kv.get("pwd") or kv.get("password", "")]
        else:
            args += ["-E"]
        salida = subprocess.run(args, capture_output=True, text=True, timeout=20).stdout.strip()
        if re.match(r"^\d{4}-\d{2}$", salida):
            return salida
    except Exception:
        pass
    return HASTA_POR_DEFECTO


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


def cuenta(cuc):
    return int(cuc[1:]) if cuc and cuc.startswith("@") and cuc[1:].isdigit() else None


# =============================================================================================
# Carga de la muestra
# =============================================================================================

def cargar_muestra(datos, meses, n_coop, tipo="TODOS", semilla=7):
    catalogo = [e["archivo"] for e in leer(os.path.join(datos, "entidades_lista.json"))]
    bp = [a for a in catalogo if a.startswith("BP__")]
    mu = [a for a in catalogo if a.startswith("MUT__")]
    coop = [a for a in catalogo if a.startswith("COAC___")]
    random.Random(semilla).shuffle(coop)
    muestra = {"BP": bp, "MU": mu, "COOP": coop[:n_coop]}.get(tipo, bp + mu + coop[:n_coop])

    cuentas = collections.defaultdict(dict)    # entidad → cuenta → serie
    objetivos = collections.defaultdict(dict)  # (cuadro, idsub, codigo) → entidad → serie
    etiquetas = {}
    indice = {m: i for i, m in enumerate(meses)}

    def serie(x):
        s = np.full(len(meses), np.nan)
        for k, v in x.items():
            if k in indice:
                s[indice[k]] = num(v)
        return s

    for a in muestra:
        bal = os.path.join(datos, "balances", a + ".json")
        if os.path.exists(bal):
            for x in leer(bal):
                if x.get("ID") == ID_SALDO and str(x.get("Codigo_Base", "")).strip().isdigit():
                    cuentas[a][int(str(x["Codigo_Base"]).strip())] = serie(x)
        for carpeta in ("entidades", "reportes"):
            ruta = os.path.join(datos, carpeta, a + ".json")
            if not os.path.exists(ruta):
                continue
            for x in leer(ruta):
                c = x.get("Cuadro")
                if c not in CUADROS:
                    continue
                cod = x.get("CUC") or (("#" + str(x.get("Codigo_Base")).strip()) if x.get("Codigo_Base") else None)
                if cod is None:
                    cod = "|" + str(x.get("Grupo")) + "|" + str(x.get("Variable"))
                clave = (c, x.get("ID") or "", cod)
                objetivos[clave].setdefault(a, serie(x))
                etiquetas[clave] = x.get("Variable")
    return muestra, cuentas, objetivos, etiquetas


# =============================================================================================
# Búsquedas
# =============================================================================================

class Matriz:
    """Observaciones (entidad × mes) en filas; cuentas en columnas (millones; ausente = 0)."""

    def __init__(self, muestra, cuentas, meses):
        self.muestra = muestra
        self.ids = sorted({c for e in cuentas.values() for c in e})
        self.col = {c: i for i, c in enumerate(self.ids)}
        n = len(muestra) * len(meses)
        self.A = np.zeros((n, len(self.ids)))
        for i, e in enumerate(muestra):
            for c, s in cuentas.get(e, {}).items():
                self.A[i * len(meses):(i + 1) * len(meses), self.col[c]] = np.nan_to_num(s)
        self.meses = meses
        # observación válida: la entidad tiene balance ese mes (ACTIVO informado)
        activo = self.A[:, self.col[1]] if 1 in self.col else np.ones(n)
        self.valida = activo != 0

    def vector(self, por_entidad):
        v = np.full(len(self.muestra) * len(self.meses), np.nan)
        for i, e in enumerate(self.muestra):
            if e in por_entidad:
                v[i * len(self.meses):(i + 1) * len(self.meses)] = por_entidad[e]
        return v


def tolerancia(y, k=1):
    return 1e-3 + 2e-4 * k + 1e-6 * np.abs(y)


def buscar_suma(M: Matriz, y, mascara, max_terminos=30, max_obs=250):
    """
    Voraz: agrega ±cuenta que más reduce el residuo hasta reproducir y exacto. Busca sobre una
    submuestra de observaciones (rápido), corta si no converge y valida sobre todas al final.
    """
    idx = np.flatnonzero(mascara)
    sub = idx if len(idx) <= max_obs else np.random.default_rng(3).choice(idx, max_obs, replace=False)
    A = M.A[sub]
    r = y[sub].copy()
    inicial = np.abs(r).sum()
    terminos = {}
    for paso in range(max_terminos):
        if np.all(np.abs(r) <= tolerancia(y[sub], len(terminos))):
            break
        if paso == 4 and np.abs(r).sum() > 0.5 * inicial:
            return None                                   # no se parece a una suma de cuentas
        mejor = None
        for s in (1.0, -1.0):
            res = np.abs(r[:, None] - s * A).sum(axis=0)
            j = int(np.argmin(res))
            if mejor is None or res[j] < mejor[0]:
                mejor = (res[j], j, s)
        if np.abs(r).sum() - mejor[0] < 1e-9:
            return None
        _, j, s = mejor
        c = M.ids[j]
        terminos[c] = terminos.get(c, 0) + s
        if terminos[c] == 0:
            del terminos[c]
        r = r - s * A[:, j]
    else:
        return None
    # Validación completa
    total = sum(k * M.A[idx, M.col[c]] for c, k in terminos.items()) if terminos else np.zeros(len(idx))
    return terminos if np.all(np.abs(total - y[idx]) <= tolerancia(y[idx], len(terminos))) else None


_CACHE_PINV = {}


def buscar_suma_ols(M: Matriz, y, mascara, max_obs=1500):
    """
    Regresión por nivel de cuenta: dentro de un mismo nivel (2, 4 o 6 dígitos) las cuentas no se
    contienen entre sí, así que una suma exacta aparece con coeficientes ±1 (o 0). Se prueban
    también nivel 2 + cuentas de provisiones (xx99) para casos como bruta = 14 − 1499.
    La pseudo-inversa de cada conjunto se calcula una vez por submuestra y se reutiliza entre filas.
    """
    idx = np.flatnonzero(mascara)
    sub = idx if len(idx) <= max_obs else np.random.default_rng(5).choice(idx, max_obs, replace=False)
    clave_sub = hash(sub.tobytes())
    largo = lambda c: len(str(c))
    conjuntos = (
        ("L2", lambda c: largo(c) == 2),
        ("L4", lambda c: largo(c) == 4),
        ("L6", lambda c: largo(c) == 6),
        ("L2+99", lambda c: largo(c) == 2 or (largo(c) == 4 and str(c).endswith("99"))),
    )
    for nombre, filtro in conjuntos:
        clave = (id(M), nombre, clave_sub)
        if clave not in _CACHE_PINV:
            cols = [c for c in M.ids if filtro(c) and np.any(M.A[sub, M.col[c]] != 0)]
            pinv = np.linalg.pinv(M.A[np.ix_(sub, [M.col[c] for c in cols])]) if cols else None
            _CACHE_PINV[clave] = (cols, pinv)
        cols, pinv = _CACHE_PINV[clave]
        if not cols:
            continue
        coef = pinv @ y[sub]
        redondo = np.round(coef)
        if np.any(np.abs(coef - redondo) > 0.02) or np.any(np.abs(redondo) > 1):
            continue
        terminos = {c: float(k) for c, k in zip(cols, redondo) if k != 0}
        if not terminos:
            continue
        total = sum(k * M.A[idx, M.col[c]] for c, k in terminos.items())
        if np.all(np.abs(total - y[idx]) <= tolerancia(y[idx], len(terminos))):
            return terminos
    return None


def buscar_ratio(y, mascara, pool, nombres, escala=100.0, max_obs=600):
    """y = escala × N / D con N, D del pool (columnas). Busca en una submuestra y valida en todo."""
    idx = np.flatnonzero(mascara)
    if len(idx) == 0:
        return None
    sub = idx if len(idx) <= max_obs else np.random.default_rng(1).choice(idx, max_obs, replace=False)
    P = pool[sub]
    t = y[sub]
    for d in range(P.shape[1]):
        D = P[:, d]
        if np.any(D == 0):
            continue
        R = escala * P / D[:, None]                      # todas las N para este D
        ok = np.all(np.abs(R - t[:, None]) <= 5e-3 + 1e-5 * np.abs(t[:, None]), axis=0)
        for n in np.flatnonzero(ok):
            if n == d:
                continue
            Nf, Df = pool[idx, n], pool[idx, d]
            with np.errstate(divide="ignore", invalid="ignore"):
                full = escala * Nf / Df
            if np.all(np.abs(full - y[idx]) <= 5e-3 + 1e-5 * np.abs(y[idx])):
                return nombres[n], nombres[d], float(np.max(np.abs(full - y[idx])))
    return None


def detectar_tiempo(y, mascara, base, meses):
    """Prueba operaciones en el tiempo contra una serie base (misma longitud, por entidad × mes)."""
    T = len(meses)
    n = len(y) // T
    Y, B = y.reshape(n, T), base.reshape(n, T)
    mes = np.array([int(m[5:]) for m in meses])
    cand = {}
    with np.errstate(divide="ignore", invalid="ignore"):
        dif = np.full_like(B, np.nan); dif[:, 1:] = B[:, 1:] - B[:, :-1]
        cand["VARIACION_1"] = dif
        tasa = np.full_like(B, np.nan); tasa[:, 12:] = 100 * (B[:, 12:] / B[:, :-12] - 1)
        cand["TASA_12"] = tasa
        cand["ANUALIZADO"] = B * 12 / mes[None, :]
        ah = np.full_like(B, np.nan); ah[:, 1:] = 100 * (B[:, 1:] / B[:, :-1] - 1)
        cand["TASA_1"] = ah
    for nombre, C in cand.items():
        m = mascara.reshape(n, T) & np.isfinite(C) & np.isfinite(Y)
        if m.sum() >= max(20, 0.5 * mascara.sum()) and np.all(np.abs(C[m] - Y[m]) <= 2e-3 + 1e-5 * np.abs(Y[m])):
            return nombre
    return None


# =============================================================================================
# Principal
# =============================================================================================

def base_temporal(M, objetivos, resueltas, cuadro, idsub, cod):
    """Serie base de una operación en el tiempo: "@xxxA" → cuenta xxx; "tasa_X" → fila o cuenta X."""
    if cod and cod.endswith("A") and cuenta(cod[:-1]) is not None:
        c = cuenta(cod[:-1])
        return M.A[:, M.col[c]] if c in M.col else None
    m = re.match(r"^tasa_(.+)$", cod or "")
    if not m:
        return None
    b = m.group(1)
    if b in resueltas:
        return resueltas[b]
    if b.isdigit() and int(b) in M.col:
        return M.A[:, M.col[int(b)]]
    for alt in ("@" + b, b):
        if (cuadro, idsub, alt) in objetivos:
            return M.vector(objetivos[(cuadro, idsub, alt)])
    return None


def base_de(cod):
    return cod[5:] if cod.startswith("tasa_") else cod[:-1]


def main():
    import time
    p = argparse.ArgumentParser()
    p.add_argument("--datos", default=DATOS_DEF)
    p.add_argument("--salida", default=SALIDA_DEF)
    p.add_argument("--coops", type=int, default=40)
    p.add_argument("--desde", default="2021-05")
    p.add_argument("--hasta", default=None, help="por defecto, el último mes de la BD (api.vVentana)")
    p.add_argument("--tipo", default="TODOS", choices=["TODOS", "BP", "COOP", "MU"])
    p.add_argument("--cuadros", default=None, help="lista separada por comas (p. ej. EFI03,EFI07) para probar un subconjunto")
    p.add_argument("--limite", type=int, default=None, help="máximo de filas pendientes a procesar (prueba)")
    a = p.parse_args()

    # Nunca validar contra meses posteriores al último mes real de la BD (lo demás son proyecciones).
    ultimo = ultimo_mes_bd()
    hasta = min(a.hasta, ultimo) if a.hasta else ultimo
    meses = [m for m in sorted({f"{y}-{mm:02d}" for y in range(2021, 2028) for mm in range(1, 13)}) if a.desde <= m <= hasta]
    inicio = time.time()
    muestra, cuentas, objetivos, etiquetas = cargar_muestra(a.datos, meses, a.coops, a.tipo)
    M = Matriz(muestra, cuentas, meses)
    print(f"[{a.tipo}] Muestra: {len(muestra)} entidades × {len(meses)} meses ({meses[0]}..{meses[-1]}, "
          f"último mes BD {ultimo}); {len(M.ids)} cuentas; carga {time.time() - inicio:.0f} s", file=sys.stderr, flush=True)

    filtro_cuadros = set(a.cuadros.split(",")) if a.cuadros else None
    pendientes = sorted(k for k in objetivos
                        if cuenta(k[2]) is None and k[0] != "EFI06"
                        and (filtro_cuadros is None or k[0] in filtro_cuadros))
    if a.limite:
        pendientes = pendientes[:a.limite]
    print(f"[{a.tipo}] {len(pendientes)} filas pendientes", file=sys.stderr, flush=True)

    resultados = {}
    resueltas = {}   # código → vector (para ratios y tiempo)

    def progreso(pasada, i, clave):
        if i % 50 == 0:
            print(f"[{a.tipo}] pasada {pasada} · {i}/{len(pendientes)} · {clave[0]} · {time.time() - inicio:.0f} s",
                  file=sys.stderr, flush=True)

    # Pasada 1: operaciones en el tiempo (baratas) y luego sumas.
    for i, clave in enumerate(pendientes, 1):
        progreso(1, i, clave)
        cuadro, idsub, cod = clave
        k = f"{cuadro}|{idsub}|{cod}"
        y = M.vector(objetivos[clave])
        mascara = np.isfinite(y) & M.valida
        if mascara.sum() < 30 or np.all(y[mascara] == 0):
            continue
        base = base_temporal(M, objetivos, resueltas, cuadro, idsub, cod)
        if base is not None:
            op = detectar_tiempo(y, mascara, base, meses)
            if op:
                resultados[k] = {"tipo": f"TIEMPO:{op}", "base": base_de(cod),
                                 "etiqueta": etiquetas[clave], "n": int(mascara.sum())}
                continue
        t = buscar_suma_ols(M, y, mascara) or buscar_suma(M, y, mascara)
        if t is not None:
            resultados[k] = {"tipo": "SUMA", "terminos": {str(c): v for c, v in sorted(t.items())},
                             "escala": 1e-6, "n": int(mascara.sum()), "etiqueta": etiquetas[clave]}
            resueltas.setdefault(cod, y)

    # Pool para ratios: cuentas de hasta 4 dígitos + filas resueltas como SUMA.
    base_cols = [c for c in M.ids if c < 10000]
    nombres = [("CUENTA", c) for c in base_cols] + [("FILA", c) for c in resueltas]
    pool = np.column_stack([M.A[:, M.col[c]] for c in base_cols] + [np.nan_to_num(v) for v in resueltas.values()])

    # Pasada 2: "tasa_X" cuya base se resolvió en la pasada 1 y luego ratios.
    for i, clave in enumerate(pendientes, 1):
        progreso(2, i, clave)
        cuadro, idsub, cod = clave
        k = f"{cuadro}|{idsub}|{cod}"
        if k in resultados:
            continue
        y = M.vector(objetivos[clave])
        mascara = np.isfinite(y) & M.valida & (y != 0)
        if mascara.sum() < 30:
            resultados[k] = {"tipo": "SIN_DATOS", "etiqueta": etiquetas[clave]}
            continue
        base = base_temporal(M, objetivos, resueltas, cuadro, idsub, cod)
        op = detectar_tiempo(y, mascara, base, meses) if base is not None else None
        if op:
            resultados[k] = {"tipo": f"TIEMPO:{op}", "base": base_de(cod),
                             "etiqueta": etiquetas[clave], "n": int(mascara.sum())}
            continue
        r = buscar_ratio(y, mascara, pool, nombres)
        if r:
            resultados[k] = {"tipo": "RATIO", "N": list(r[0]), "D": list(r[1]), "escala": 100.0,
                             "error": r[2], "n": int(mascara.sum()), "etiqueta": etiquetas[clave]}
            continue
        resultados[k] = {"tipo": "NO_RESUELTA", "etiqueta": etiquetas[clave], "n": int(mascara.sum())}

    os.makedirs(os.path.dirname(a.salida), exist_ok=True)
    with open(a.salida, "w", encoding="utf-8") as f:
        json.dump({"tipo": a.tipo, "meses": [meses[0], meses[-1]], "resultados": resultados}, f, ensure_ascii=False, indent=1)
    cuenta_tipos = collections.Counter(v["tipo"] for v in resultados.values())
    print(f"[{a.tipo}] terminado en {time.time() - inicio:.0f} s", file=sys.stderr, flush=True)
    print(json.dumps(cuenta_tipos, ensure_ascii=False, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
