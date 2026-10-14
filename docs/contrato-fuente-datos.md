# Contrato de la API — fuente de los datos y meses sin historia

> Para el equipo de frontend. Versión 2026‑10‑14.
> **Todos los cambios son aditivos:** no se quitó ni renombró ningún campo; un cliente que los ignore sigue funcionando igual.

## 1. Qué cambia y por qué

El backend ya no sirve todo desde los archivos JSON. Hoy combina dos fuentes:

| Fuente | Qué es |
|---|---|
| **`bd`** | La base de datos `idce_bco_coop`: saldos de cuentas, indicadores y fórmulas del catálogo (márgenes, análisis vertical y horizontal, ROA, ROE, tasas a 12 meses…). |
| **`json`** | Los archivos JSON de siempre. Cubren lo que todavía no tiene fórmula en la base (p. ej. morosidad, CAMELS, PERLAS, tasas de interés) y los cuadros macro. |

Un mismo cuadro puede mezclar las dos fuentes. Por ejemplo, en EFI11 el ROA y el ROE salen de la BD y la morosidad del JSON. El frontend recibe ahora:

1. **`fuenteDatos`** en cada fila, en cada cuenta del reporte y en el cuadro completo.
2. **`filasPorFuente`**: cuántas filas salen de cada fuente.
3. **`cuadrosPorFuente`** en `/api/meta`: el resumen de todo el sistema.
4. **`sinHistoriaHasta`** en cada fila: para distinguir un vacío por falta de meses anteriores de un dato faltante.

> ⚠ **No confundir** `fuenteDatos` con el campo `origen` que ya existe en `GET /api/cuadros` (`"macro"` | `"sistema"` | `"entidad"`). Son cosas distintas: `origen` es el tipo de cuadro y `fuenteDatos` dice de dónde salen sus números.

---

## 2. `GET /api/cuadros/{id}` — detalle de un cuadro

Parámetros sin cambios: `sector`, `entidad`, `analisis`, `credito`, `desde`, `hasta`, `notas`.

### Campos nuevos en el cuadro

| Campo | Tipo | Significado |
|---|---|---|
| `fuenteDatos` | `"bd"` \| `"json"` \| `"mixto"` \| `null` | `"bd"` = todas las filas con dato salen de la BD; `"json"` = todas del JSON; `"mixto"` = hay de las dos. `null` solo si ninguna fila tiene fuente. |
| `filasPorFuente` | `{ bd: number, json: number, sinFuente: number }` | Conteo sobre las filas devueltas. `sinFuente` = filas sin dato en ninguna fuente (ver §5). |

### Campos nuevos en cada fila (`filas[]`)

| Campo | Tipo | Significado |
|---|---|---|
| `fuenteDatos` | `"bd"` \| `"json"` \| `null` | De dónde salen **todos** los valores de la fila. Una fila nunca mezcla fuentes. `null` = fila sin fuente: todos sus valores son `null`. |
| `sinHistoriaHasta` | `"YYYY-MM"` \| `null` | Ver §4. |

### Ejemplo real (EFI11, BP. Pichincha, 2024‑11 → 2025‑01, dos filas)

```json
{
  "id": "EFI11",
  "titulo": "INDICADORES FINANCIEROS",
  "periodos": ["2024-11", "2024-12", "2025-01"],
  "filas": [
    {
      "indice": 21,
      "clave": "EFI11|BP__PICHINCHA|IF012",
      "grupo": "INDICES DE MOROSIDAD",
      "variable": "MOROSIDAD DE LA CARTERA TOTAL",
      "cuc": "IF012",
      "valores": [3.961, 3.5694, 3.6052],
      "sinHistoriaHasta": null,
      "fuenteDatos": "json"
    },
    {
      "indice": 34,
      "clave": "EFI11|BP__PICHINCHA|IF002",
      "grupo": "RENTABILIDAD",
      "variable": "RESULTADOS DEL EJERCICIO / ACTIVO PROMEDIO",
      "cuc": "IF002",
      "valores": [null, 0.7709682773263407, 1.1063798739220885],
      "sinHistoriaHasta": "2024-12",
      "fuenteDatos": "bd"
    }
  ],
  "notas": [
    "…",
    "Los valores vacíos hasta 2024-12 en tasas y variaciones a 12 meses, variaciones mensuales y promedios del año se deben a que la base de datos no tiene los meses anteriores necesarios para calcularlos; no son un error de cálculo."
  ],
  "fuenteDatos": "mixto",
  "filasPorFuente": { "bd": 2, "json": 39, "sinFuente": 0 }
}
```

*(Se omitieron campos que no cambian: `contexto`, `periodosDisponibles`, `nivel`, `padre`, etc.)*

---

## 3. `GET /api/entidades/{id}/reporte` — reporte REP01

Parámetros sin cambios: `codigos`, `desde`, `hasta`.

| Dónde | Campo | Igual que en el cuadro |
|---|---|---|
| Reporte | `fuenteDatos`, `filasPorFuente` | Se calcula sobre las **cuentas devueltas**. Si se filtra con `codigos`, cuenta solo esas. |
| `cuentas[]` | `fuenteDatos`, `sinHistoriaHasta` | Igual que en las filas del cuadro. |

```json
{
  "entidad": { "id": "BP__PICHINCHA", "nombre": "BP. PICHINCHA", "...": "..." },
  "periodos": ["2024-11", "2024-12", "2025-01"],
  "cuentas": [
    { "cuc": "@1",     "variable": "1.    ACTIVO", "valores": [19083.75, 19493.10, 19687.13], "sinHistoriaHasta": null,      "fuenteDatos": "bd" },
    { "cuc": "E1_ROA", "variable": "E1_ROA",       "valores": [null, 0.7710, 1.1064],         "sinHistoriaHasta": "2024-12", "fuenteDatos": "bd" }
  ],
  "fuenteDatos": "bd",
  "filasPorFuente": { "bd": 2, "json": 0, "sinFuente": 0 }
}
```

---

## 4. `sinHistoriaHasta` — vacíos por falta de meses anteriores

La base de datos tiene meses **desde 2024‑01**. Algunas filas calculan su valor con meses anteriores, así que al principio de la serie no pueden calcularse:

| Tipo de fila | Necesita | Primeros meses sin valor |
|---|---|---|
| Tasas y variaciones a 12 meses (análisis horizontal, `tasa_*`) | el mismo mes del año anterior | 2024‑01 → 2024‑12 |
| Promedios del año (ROA, ROE) | diciembre del año anterior | 2024‑01 → 2024‑11 (dic‑2024 sí tiene valor) |
| Variación mensual (EFI04) | el mes anterior | 2024‑01 |

**Regla para el frontend.** Si una fila trae `sinHistoriaHasta = "2024-12"`:
- un `null` en `valores` en **2024‑12 o antes** significa *"no hay historia suficiente"*. No es un error ni un dato faltante; conviene mostrarlo distinto, p. ej. «—» con un tooltip;
- un `null` **después** de esa fecha es un dato faltante normal;
- un valor no nulo antes de esa fecha es válido (p. ej. el ROA de diciembre).

`sinHistoriaHasta` es `null` cuando la fila no usa meses anteriores, o cuando la fecha queda antes del primer período pedido (`desde`). Así, solo aparece si explica algún vacío visible.

Los cuadros que no son árbol agregan además una frase en `notas` (ver el ejemplo de §2). Los cuadros en árbol (balances EFI06/SFN06) no traen notas, como siempre; ahí basta el campo por fila.

---

## 5. Regla de `fuenteDatos` por fila

| Situación | `fuenteDatos` |
|---|---|
| Saldo de cuenta, indicador o fila con fórmula en el catálogo de la BD | `"bd"` |
| Fila que todavía toma su valor del JSON | `"json"` |
| Fila del catálogo sin fórmula y sin fila equivalente en el JSON (todos sus valores son `null`) | `null` |
| Cuadros macro (`IEA*`, `IEM*`…) | `"json"` |
| La BD no está disponible (la API sigue funcionando solo con JSON) | `"json"` en todo |

Una fila `"bd"` puede tener `null` en algunos meses (sin dato ese mes, o sin historia según §4); sigue siendo `"bd"`.

**El valor puede cambiar entre llamadas:** si la base se cae, el backend pasa a JSON y todo vuelve como `"json"`. Conviene leerlo en cada respuesta y no guardarlo en caché por cuadro.

---

## 6. `GET /api/meta` — resumen global

Campo nuevo:

| Campo | Tipo | Significado |
|---|---|---|
| `cuadrosPorFuente` | `{ bd: number, json: number, mixto: number }` \| `null` | Cuántos cuadros salen solo de la BD, solo del JSON o de ambas, según el catálogo vigente. Con la BD caída: todos en `json`. `null` solo si la BD no informa el catálogo (en ese caso hay una entrada en `advertencias`). |

Valor actual:

```json
"cuadrosPorFuente": { "bd": 6, "json": 216, "mixto": 13 }
```

Ese valor se reparte así:
- **`bd`:** EFI01, EFI02, EFI06, SFN01, SFN02 y SFN06;
- **`mixto`:** EFI03, EFI04, EFI05, EFI07, EFI10, EFI11, EFI12, EFI13, REP01, CAR01, CAR05, SFN03 y SFN04;
- **`json`:** los cuadros macro, más CAR02, CAR03, CAR04, EFI08, EFI09, TEA01, TEA02, TPE01, TPE02 y SFN05.

Los campos que ya existían (`versionDatos`, `fuentes`, `ultimoCorteComun`, `advertencias`) no cambian.

---

## 7. Tipos TypeScript

```ts
export type FuenteDatos = "bd" | "json";
export type FuenteDatosCuadro = FuenteDatos | "mixto";

export interface FilasPorFuente { bd: number; json: number; sinFuente: number; }

export interface FilaCuadro {
  indice: number;
  clave: string;
  grupo: string | null;
  variable: string | null;
  cuc: string | null;
  codigoBase: string | null;
  nivel: number;
  padre: number | null;
  valores: (number | null)[];
  sinHistoriaHasta: string | null;       // NUEVO — "YYYY-MM"
  fuenteDatos: FuenteDatos | null;       // NUEVO
}

export interface Cuadro {
  // … campos existentes (id, titulo, unidad, tipo, frecuencia, vista, contexto, periodosDisponibles, periodos) …
  filas: FilaCuadro[];
  notas: string[];
  fuenteDatos: FuenteDatosCuadro | null; // NUEVO
  filasPorFuente: FilasPorFuente;        // NUEVO
}

export interface CuentaReporte {
  cuc: string;
  variable: string | null;
  valores: (number | null)[];
  sinHistoriaHasta: string | null;       // NUEVO
  fuenteDatos: FuenteDatos | null;       // NUEVO
}

export interface Reporte {
  entidad: { id: string; nombre: string; tamano: string | null; rango: string | null; provincia: string | null };
  periodos: string[];
  cuentas: CuentaReporte[];
  fuenteDatos: FuenteDatosCuadro | null; // NUEVO
  filasPorFuente: FilasPorFuente;        // NUEVO
}

export interface Meta {
  // … versionDatos, fuentes, ultimoCorteComun, advertencias …
  cuadrosPorFuente: { bd: number; json: number; mixto: number } | null; // NUEVO
}

/** true si el null de valores[i] se debe a falta de historia previa en la BD. */
export const esSinHistoria = (fila: { sinHistoriaHasta: string | null }, periodo: string) =>
  fila.sinHistoriaHasta !== null && periodo <= fila.sinHistoriaHasta;
```

---

## 8. Sugerencias de presentación (opcionales)

- **Cuadro:** una insignia junto al título: «Base de datos», «Archivos JSON» o «Mixto». En un cuadro mixto, el tooltip puede mostrar `filasPorFuente`.
- **Fila:** un marcador discreto solo en cuadros `"mixto"`, para no recargar los que tienen una sola fuente.
- **Celda vacía:** «—» con el tooltip «Sin historia previa en la base de datos» cuando `esSinHistoria(...)`; vacío normal en el resto.
- **Panel de estado:** `cuadrosPorFuente` de `/api/meta`, junto a las `advertencias` que ya se muestran.
