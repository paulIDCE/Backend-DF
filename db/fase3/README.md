# Fase 3 — Fórmulas para las filas que todavía toman su valor del JSON

Objetivo: pasar filas del catálogo (`dbo.CuadroFila`) de `Calculo = 'JSON'` a una fórmula calculada por el SP, para depender cada vez menos de los JSON. Requiere las fases [0](../fase0/README.md) y [2](../fase2/README.md).

## Paso 1 — `SUMA` y `RATIO` (no cambia el modelo)

| # | Script | Qué hace |
|---|---|---|
| 1 | `01_formulas_suma_ratio.sql` | **Generado.** Análisis vertical de EFI06/SFN06 como `RATIO` (6 991 filas) y 28 fórmulas descubiertas y validadas. Idempotente: rehace los términos de cada fila tocada, no crea ni borra filas |
| 2 | `02_pruebas.sql` | 8 pruebas con valores del JSON |

```bash
sqlcmd -S 192.168.1.5 -d idce_bco_coop -U <usuario> -P <clave> -b -f 65001 -i db/fase3/01_formulas_suma_ratio.sql -i db/fase3/02_pruebas.sql
```

**Cómo se generó** (detalle en [`formulas_reporte.md`](formulas_reporte.md)):
1. `tools/catalogo/descubrir_formulas.py` buscó candidatos sobre una muestra por tipo de entidad. Resultado guardado en `tools/catalogo/candidatos_descubiertos.json`.
2. `tools/catalogo/generar_formulas_fase3.py` valida cada candidato contra **las 229 entidades** y los 10 sectores, con meses reales hasta el último mes de la BD (`api.vVentana.Hasta`, hoy 2026‑08).
   - Las proyecciones del JSON (2026‑09 en adelante) nunca se usan.
   - Se acepta solo si no falla en ninguna entidad: una fila del catálogo aplica a todas las que comparten plantilla.
   - La tolerancia considera el redondeo de los saldos publicados (4 decimales en millones) y del valor publicado. El análisis vertical se publica con 2 decimales.
   - COAC Orden y Seguridad (OYS) se valida aparte: su JSON publica 0 donde su balance tiene valores (anomalía de datos ya conocida).

**Qué entra:**

| Fórmula | Filas |
|---|---|
| Análisis vertical = 100 × cuenta / cuenta raíz del grupo (1, 2, 3, 4, 5) | EFI06 (bancos 890, cooperativas 962, mutualistas 924) y SFN06 (1 109 / 1 197 / 962 / 947 según sector). 0 fallas en 16,6 M valores |
| Cartera bruta `IF011` = 14 − 1499 | EFI07, CAR01, REP01 |
| VIS por vencer / que no devenga / bruta (`IF007_5`, `IF008_5`, `IF011_5`) | EFI07, CAR01, REP01 |
| PERLAS E1, E3, E4, E6 = 100 × (cartera neta, 1901, 18, 26) / activo | EFI13, REP01 |
| Activos líquidos improductivos `SB005`, provisiones `SB008`, márgenes del PyG | REP01 |

**Para regenerar** (por ejemplo, con JSON nuevos):

```bash
python tools/catalogo/descubrir_formulas.py --tipo BP     # y COOP, MU
python tools/catalogo/generar_formulas_fase3.py
```

## Paso 2 — operaciones en el tiempo

Detalle del diseño y las decisiones en [`PLAN_PASO2.md`](PLAN_PASO2.md). Ventana: **opción A** (no se cargó 2023 en `B11`).

| # | Script | Qué hace |
|---|---|---|
| 3 | `03_operaciones_tiempo.sql` | `dbo.CuadroFila` + `OperacionN`/`OperacionD` (con CHECK); `api.fnFilasCuadro` y `api.ObtenerPlantilla` las exponen al final; nuevo `dbo.AplicarOperacionesTiempo` (interno); `api.ObtenerValoresCuadroEntidades`/`Sector` las aplican (misma firma y columnas). Idempotente |
| 4 | `04_formulas_tiempo.sql` | **Generado.** Análisis horizontal de EFI06/SFN06 (6 991 filas), 430 filas con código (`@cuentaA`, `tasa_X`, ROA, ROE) y 46 de EFI04/SFN04 "VARIACIÓN MENSUAL". Requiere `03` |
| 6 | `06_pruebas_tiempo.sql` | 11 pruebas con valores del JSON y conteo de filas que siguen en `JSON`, por cuadro |

```bash
sqlcmd -S 192.168.1.5 -d idce_bco_coop -U <usuario> -P <clave> -b -I -f 65001 -i db/fase3/03_operaciones_tiempo.sql -i db/fase3/04_formulas_tiempo.sql -i db/fase3/06_pruebas_tiempo.sql
```

| Operación | Valor en el mes t (S = suma de los términos del rol) | Se usa en |
|---|---|---|
| `ANUALIZADO` | S_t × 12 / mes(t) | EFI03/SFN03, `@cuentaA` de REP01, numerador de ROA/ROE |
| `VARIACION_1` | S_t − S_{t−1} | EFI04/SFN04 "VARIACIÓN MENSUAL" |
| `VARIACION_12` / `RETARDO_12` | S_t − S_{t−12} / S_{t−12} | análisis horizontal, `tasa_X` (RATIO × 100) |
| `PROMEDIO_DIC_ROA` | media dic. anterior → t; en diciembre, S_t | denominador del ROA (activo) |
| `BASE_ROE_DIC` | media dic. anterior → t; en diciembre, S_t − ganancia | denominador del ROE (patrimonio) |

**Meses sin historia.** Con `B11` desde 2024‑01 quedan sin valor:
- las tasas a 12 meses de 2024;
- ROA y ROE de 2024‑01 a 2024‑11 (diciembre sí tiene valor, por su regla propia);
- la variación mensual de 2024‑01.

El backend publica en cada fila `sinHistoriaHasta` para que el frontend distinga esos vacíos de un error (ver `docs/informe-sav-a-sql.md` §8.7).

**Cómo se generó** (detalle en [`formulas_tiempo_reporte.md`](formulas_tiempo_reporte.md)):
- `tools/catalogo/generar_formulas_fase3_paso2.py` reusa la carga y las reglas del paso 1. Implementa las operaciones en numpy (espejo del SP) y valida con la historia de los JSON (desde 2021‑05) en las 229 entidades y los 10 sectores.
- La tolerancia usa la cota exacta del cociente con saldos redondeados: las bases chicas, como 0,0005 millones, tienen mucho error relativo.
- Una diferencia solo se excluye si el propio JSON la explica. Todas se informan:
  - **0 publicado:** el JSON pone 0 cuando no pudo calcular.
  - **Historia reprocesada:** en 436 entidad‑meses de 42 entidades el JSON calculó la tasa con un balance del año anterior que después se corrigió. Ahí falla también una cuenta agregada, casi siempre el ACTIVO, y la `tasa_X` publicada contradice su propia fila base. La BD tiene el balance corregido.
  - **ROA que contradice al ROE publicado** (COAC 2023‑05/07 y algunos diciembres): el ROE publicado se reproduce con la fórmula.
- Quedan `JSON`:
  - filas cuya cuenta no aparece en ningún balance (no hay con qué validar);
  - `tasa_X` cuya X no es cuenta ni tiene `SUMA` en el catálogo;
  - EFI04/SFN04 FUENTES, USOS y RESULTADOS (dependen del signo de la variación).

**Prueba previa en LocalDB** (nunca contra el servidor), con datos sintéticos de valores calculables a mano:

```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -I -b -f 65001 -i db/fase3/localdb/00_base_minima.sql
# luego db/fase0/00-03, db/fase2/00-02, db/fase3/01, 03 y 04 (dos veces, para probar idempotencia)
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -I -b -f 65001 -i db/fase3/localdb/01_pruebas_tiempo.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -I -i db/fase3/localdb/99_borrar.sql
```

`01_pruebas_tiempo.sql` cubre 30 casos: cada operación, diciembre, un mes faltante, sectores, la regresión de `NIVEL` y la plantilla.

**Rendimiento** (consulta suelta en el servidor, REP01 de 327 entidades, insertando en una tabla temporal): ≈ 23 s con las operaciones, frente a ≈ 24 s del SP actual medido igual. Se calculan 35 % más valores; las operaciones toman ≈ 4 s.

**Para regenerar:**

```bash
python tools/catalogo/generar_formulas_fase3_paso2.py    # --cache <archivo.pkl> para iterar sin recargar los JSON
```

## Paso 2b — fórmulas que no necesitan la nota técnica

| # | Script | Qué hace |
|---|---|---|
| 7 | `07_formulas_sin_nota.sql` | **Generado.** 40 fórmulas; usa las operaciones del paso 2, así que requiere `03`. Idempotente |
| 6 | `06_pruebas_tiempo.sql` | Suma las pruebas 12‑15; se omiten con un aviso si `07` no está |

```bash
sqlcmd -S 192.168.1.5 -d idce_bco_coop -U <usuario> -P <clave> -b -I -f 65001 -i db/fase3/07_formulas_sin_nota.sql -i db/fase3/06_pruebas_tiempo.sql
```

Qué entra (detalle en [`formulas_sin_nota_reporte.md`](formulas_sin_nota_reporte.md)):
- **Márgenes anualizados** (`Marg_Ne_anual`, `Mar_Br_Fi_anual`, `Gan_Eje_anual`…): `ANUALIZADO` de la `SUMA` de EFI02, en EFI03, SFN03 y REP01. Son 7 por cuadro.
- **Cartera por estado** que cuadra en todas las entidades:
  - `Por_vencer` = 1401 + 1402 + 1403 + 1404 + 1408 + 1473;
  - `Por_vencida` = 1449 + 1450 + 1451 + 1452 + 1456 + 1485;
  - la vencida e improductiva de VIS (`IF009_5`, `IF010_5`).

  Están en EFI07 y CAR01; las dos de VIS también en REP01. Se suman sus `tasa_X`.
- **`PTR_9`** de EFI05 = 0,09 × `ACT_PON_RIESGO`, que sale de `IndicadorData`.

**No entra** (queda `JSON`):
- **Cartera por estado de los bancos.** `IF007`…`IF011` por tipo, `Refi_*`, `Rees_*` e `Impro_*` cuadran en cooperativas y mutualistas, pero en los **bancos** difieren todos los meses (p. ej. Pichincha `IF007` 2026‑06: +41 M). Ninguna cuenta 14xx explica la diferencia. Coincide con el informe §4 (E6): la cartera de bancos viene de otra fuente.
  - Como las plantillas de EFI07, CAR01 y REP01 son únicas (`*`) y el sector incluye bancos, no se carga ninguna.
  - Para el viernes: confirmar de dónde sale la cartera de los bancos.
- **`Ex_Patri_Reque`.** El JSON publica 0 aunque PTC − PTR_9 ≠ 0, así que la regla no es la diferencia.
- **SFN05.** El SP de sector no suma indicadores.

**Validación desde 2022‑05.** En 2021‑05 → 2022‑04 la cartera por estado no cuadra en ninguna entidad: son los primeros 12 meses de los JSON, que salen de otra serie, y ahí el análisis horizontal también publica 0. Esos meses se informan aparte y no afectan a la BD, que empieza en 2024‑01.

**Para regenerar:**

```bash
python tools/catalogo/generar_formulas_fase3_paso2b.py
```

En LocalDB se suman 6 casos a `localdb/01_pruebas_tiempo.sql`: 36 en total, todos correctos.

## Fuente de los datos por cuadro — `08_origen_cuadros.sql`

`api.ListarCuadros` agrega dos columnas: `FilasBd` (filas `SUMA`/`RATIO`) y `FilasJson` (filas `JSON`). Con ellas, `GET /api/meta` resume cuántos cuadros salen de la BD, del JSON o de ambas (`cuadrosPorFuente`).

El origen por fila y por cuadro (`fuenteDatos`, `filasPorFuente`) no necesita este script: el backend lo sabe al armar la respuesta. Sin `08`, `/api/meta` devuelve `cuadrosPorFuente = null` y lo advierte.

```bash
sqlcmd -S 192.168.1.5 -d idce_bco_coop -U <usuario> -P <clave> -b -I -f 65001 -i db/fase3/08_origen_cuadros.sql
```

## Pasos siguientes (pendientes)

- **Paso 3 — indicadores EFI11‑13 y REP01, uno por uno**, con la nota técnica de la SB.
- **Opción B de la ventana**: cargar 2023 en `B11` como base de cálculo, sin publicarlo. Cubriría los vacíos de 2024; ver `PLAN_PASO2.md` §3.4.
