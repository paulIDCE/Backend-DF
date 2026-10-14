# Plan — Fase 3 · Paso 2: operaciones en el tiempo en el catálogo de cuadros

> Documento para que otro agente ejecute el paso 2 sin el historial de la conversación.
> Repo: `C:\Users\IDCE_\Documents\Proyectos\BackendDF` · Idioma de trabajo, código y documentos: **español**.

## Estado (2026‑10‑13)

**Decisiones del usuario:**
- Ventana: **opción A** (se aceptan los vacíos de 2024). A cambio, el backend publica `sinHistoriaHasta` por fila (y una nota) para que el frontend distinga esos vacíos de un error.
- ROE: se intentó `BASE_ROE_DIC` y **cuadra**.

**Hecho:**
- [x] 3.1/3.2 `03_operaciones_tiempo.sql`: columnas, plantilla, `dbo.AplicarOperacionesTiempo` y SPs. Probado en LocalDB, dos veces; 30/30 casos.
- [x] 3.3 Generador `tools/catalogo/generar_formulas_fase3_paso2.py` → `04_formulas_tiempo.sql` y `formulas_tiempo_reporte.md`.
  - Análisis horizontal: 6 991 filas. Además 430 con código (`@cuentaA` de EFI03/SFN03/REP01, `tasa_X`, ROA y ROE) y 46 de EFI04/SFN04.
  - Una diferencia con el JSON solo se excluye si el propio JSON la explica: 0 publicado, historia reprocesada o ROA que contradice al ROE. Todas se informan en el reporte.
- [x] 4.2 `06_pruebas_tiempo.sql`.
- [x] Backend: `sinHistoriaHasta` en `FilaCuadroDTO` y `CuentaReporteDTO`, nota en `notas` (cuadros que no son árbol), lectura opcional de `OperacionN`/`OperacionD`.
  - 101/101 pruebas en verde.
  - 4 pruebas `FactSqlFase3` contra el servidor se omiten hasta ejecutar 03 y 04.
- [x] Rendimiento medido en el servidor con una consulta suelta: igual que el SP actual (≈ 23 s vs ≈ 24 s, 327 entidades, insertando en una tabla temporal).
- [x] Documentación: `db/fase3/README.md` e informe §8.7.

**Paso 2b (2026‑10‑14), sin nota técnica:**
- [x] `07_formulas_sin_nota.sql`: 40 fórmulas (márgenes anualizados, cartera por vencer y vencida, VIS, sus tasas y `PTR_9`).
- [x] Pruebas 12‑15 en `06`; LocalDB 36/36.
- [x] El usuario ejecutó `07` y `06` (2026‑10‑14): 15/15 OK; quedan 3 037 filas `JSON`. `dotnet test` 105/105.
- Hallazgo para la reunión: la cartera por estado de los **bancos** no sale del balance (E6). Ver `README.md`, paso 2b.

**Pendiente:**
- [x] **El usuario ejecutó** `03`, `04` y `06` en el servidor (2026‑10‑14): 11/11 OK y 3 077 filas `JSON`.
- [x] `dotnet test`: 105/105, incluidas las 4 `FactSqlFase3`.

---

## 1. Contexto (lo mínimo que hay que saber)

### 1.1 Cómo está el sistema hoy

- **Backend .NET 8** (`BackendDF/src/BackendDF`): sirve cuadros financieros.
  - Fuente híbrida `Data/FuenteHibrida/FuenteDatosHibrida.cs`: SQL Server (`idce_bco_coop`) y, como respaldo, JSON (`BackendDF/src/BackendDF/Database/`).
- **BD `idce_bco_coop`**, scripts en `db/`:
  - **Fase 0**: `api.*`, vistas `api.vSaldoAgregado` y `api.vVentana`.
  - **Fase 2**: catálogo de cuadros.
  - **Fase 3, paso 1**: fórmulas `SUMA`/`RATIO`.
  - Leer primero `db/fase0/README.md`, `db/fase2/README.md`, `db/fase3/README.md` y `docs/informe-sav-a-sql.md` (§0 convenciones, §7‑8).
- **Datos base**: `dbo.B11(FechaID, IFIID, CuentaID, Saldo money)`, saldos en USD por entidad, cuenta y mes.
  - `FechaID` = días desde 1950‑01‑01 al **último día del mes**.
  - Hay meses **2024‑01 → 2026‑08** (convención C1: solo desde 2024).
  - Otras tablas: `dbo.Ifi`, `dbo.Cuenta`, `dbo.IndicadorFin`/`IndicadorData`.
- **Catálogo** (fase 2): `dbo.Cuadro`, `dbo.CuadroFila` y `dbo.CuadroFilaTermino`.
  - Cada fila tiene `Calculo` ∈ `SUMA | RATIO | JSON`, `Escala` y términos `(Rol N/D, Fuente CUENTA/INDICADOR, CuentaID | IndicadorCodigo, Factor)`.
  - El SP calcula `SUMA = Σ(factor·valor)·Escala` y `RATIO = ΣN/ΣD·Escala`.
  - `JSON` = valor pendiente: el backend lo toma del JSON mientras tanto.
- **SPs que calculan**, en `db/fase2/02_procedimientos_cuadros.sql`:
  - `api.ObtenerValoresCuadroEntidades(@CuadroID, @IFIIDs, @IdSub)`: usa tablas temporales `#filas`/`#valores`. Ojo: con CTEs era cuadrático, por eso se reescribió así.
  - `api.ObtenerValoresCuadroSector(@CuadroID, @TiposEntidad, @Segmentos, @IdSub)`: sobre `api.vSaldoAgregado`.
  - Los envoltorios `api.ObtenerCuadroEntidad` y `api.ObtenerCuadroSector` devuelven plantilla y valores.
- **Estado actual del catálogo**: 8 145 `SUMA` · 6 999 `RATIO` · **10 544 `JSON`** (pendientes).

### 1.2 Qué resuelve el paso 2

La mayoría de las filas `JSON` son **operaciones en el tiempo**, que `SUMA`/`RATIO` no expresan porque `Escala` y `Factor` son constantes:

| Grupo | Filas `JSON` aprox. | Fórmula (verificada contra los JSON) |
|---|---|---|
| Análisis horizontal EFI06 / SFN06 (`IdSub = 'Análisis Horizontal (%)'`) | 2 776 / 4 215 | `100 × (saldo_t / saldo_{t−12} − 1)`. Publicado con **2 decimales**. Ej. Pichincha cuenta 1, 2026‑06 = **7,22** |
| EFI03 / SFN03 (PyG anualizado, códigos `@xxxxA`) y filas `@…A` de REP01 | 117 + 117 + ~90 | `saldo_t × 12 / mes(t)`. Ej. BP Amazonas (IFIID 184) cuenta 5: 2026‑07 = 29,2637 → **50,1663**; 2025‑01 = 3,7132 → 44,5582 |
| EFI04 / SFN04 (fuentes y usos, grupo "VARIACIÓN MENSUAL") | 87 + 87 | `saldo_t − saldo_{t−1}`. Las filas **no tienen código**: emparejar por texto (`Variable`) con la cuenta. Ej. Pichincha ACTIVO 2026‑06 = **−341,04** (cuenta 1: 22 542,6244 − 22 883,6617) |
| `tasa_X` de EFI07 / CAR01 / REP01 | ~70 por cuadro | `100 × (X_t / X_{t−12} − 1)`, con X = la fila base (`tasa_IF011` → `IF011` = 14 − 1499) |
| ROA (`IF002`, `E1_ROA`, `R12_ROA`) y ROE (`IF004`, `E4_ROE`, `R13_ROE`) | pocas, muy usadas | ROA = `Gan_Eje_anualizado / PROMEDIO(activo)`; ROE igual con patrimonio (3). **PROMEDIO = media de los saldos desde diciembre del año anterior hasta t (mes+1 puntos)**. Verificado en marzo, junio y septiembre. **Diciembre es especial**: ROA usa el saldo de diciembre (no el promedio); ROE usa `Gan_Eje / (patrimonio_dic − Gan_Eje)` |

`Gan_Eje` está definido en el catálogo como SUMA:

```text
Gan_Eje = −41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56
```

### 1.3 Restricción importante: la ventana

Con `B11` desde 2024‑01, los cálculos de 2024 que necesitan meses anteriores quedan **sin valor**:

| Operación | Meses de 2024 sin valor |
|---|---|
| `RETARDO_12` / `VARIACION_12` | todo 2024 |
| `PROMEDIO` | todo 2024 (falta 2023‑12) |
| `VARIACION_1` | 2024‑01 |

`ANUALIZADO` no tiene ese problema.

**Decisión pendiente del usuario (preguntar antes de implementar 3.4):**
- **(A)** Aceptar los vacíos de 2024: no hay que tocar el ETL.
- **(B)** Cargar 2023 en `B11` solo como base de cálculo, sin publicarlo.
  - Requiere que la ventana publicada deje de ser `MIN(FechaID)`. Por ejemplo, una tabla `dbo.Parametro('VentanaDesde','2024-01')` que use `api.vVentana`.
  - Además, `sp_etl_bce` (script R embebido) debe cargar 2023.
  - **Recomendado (B)**. Si el usuario no decide, implementar (A) dejando el diseño listo para (B).

---

## 2. Reglas de trabajo (aprendidas en esta sesión)

1. **La BD del servidor no se modifica desde el agente.**
   - Se escriben scripts `.sql` en `db/fase3/` y el **usuario los ejecuta** con `sqlcmd`. Después el agente verifica.
   - Las consultas de solo lectura se pueden hacer con `sqlcmd` usando la cadena `ConnectionStrings:DefaultConnection` de `BackendDF/src/BackendDF/appsettings.json`.
     - Sale el usuario `sa`, autorizado por el usuario **solo en desarrollo**. No copiar la contraseña a ningún archivo ni mensaje. El MCP `sqlserver-bco_coop` puede no estar conectado.
2. **Probar todo SQL antes en LocalDB**:
   - Crear `idce_bco_coop` en `(localdb)\MSSQLLocalDB` con las 5 tablas mínimas y algo de datos.
   - Ejecutar `db/fase0/*`, `db/fase2/00‑02`, `db/fase3/01` y luego lo nuevo, **dos veces** (idempotencia).
   - **Borrar la base al terminar.** Ver cómo se hizo en el historial de `db/fase3/README.md`.
3. **SQL Server no admite subconsultas dentro de `PRINT CONCAT(...)`.** Usar variables.
4. **Último mes real = `api.vVentana.Hasta` (hoy 2026‑08).**
   - Las columnas del JSON posteriores (2026‑09 → 2027‑02) son **proyecciones**: nunca validar contra ellas.
   - `tools/catalogo/descubrir_formulas.py` tiene `ultimo_mes_bd()` para leerlo.
5. **Validación contra el JSON (oráculo):**
   - Aceptar una fórmula solo si no falla en **ninguna** de las 229 entidades del catálogo (y en los 10 sectores, si el cuadro tiene equivalente de sector), con meses reales.
   - La tolerancia debe considerar el redondeo de los saldos publicados (4 decimales en millones), propagado a cocientes, más el redondeo del valor publicado (4 decimales; **2 en el análisis horizontal y vertical**). Ver `tolerancia_ratio()` en `tools/catalogo/generar_formulas_fase3.py`.
   - La entidad `COAC___ORDEN_Y_SEGURIDAD__OYS_` tiene datos anómalos: validarla aparte y no rechazar por ella (`ANOMALAS` en ese script).
   - En el JSON, el análisis horizontal de 2021‑05 → 2022‑04 vale 0 (no había año anterior): excluir esos meses de la validación.
6. **No regenerar `db/fase2/01_semilla_catalogo.sql`**: pisaría el catálogo. Todo cambio al catálogo va como `UPDATE` idempotente en scripts de `db/fase3/`.
7. **Herramientas locales:**
   - En el Bash de esta máquina, los heredocs con comillas a veces fallan: escribir los scripts Python en la carpeta scratchpad con la herramienta de escritura y ejecutarlos.
   - Para numpy: `OPENBLAS_NUM_THREADS=1`.
8. **Backend:**
   - Visual Studio suele tener bloqueado `bin/`. Compilar y probar así:

     ```bash
     cd BackendDF
     export BACKENDDF_RUTA_DATOS="$(cygpath -w "$PWD/src/BackendDF/Database")" ASPNETCORE_TEST_CONTENTROOT_BACKENDDF="$(cygpath -w "$PWD/src/BackendDF")"
     dotnet test tests/BackendDF.Tests/BackendDF.Tests.csproj --artifacts-path "<scratchpad>/build/"
     ```

   - Hoy pasan **89/89** pruebas; deben seguir pasando.

---

## 3. Diseño

### 3.1 Modelo: una operación por rol

En `dbo.CuadroFila` se agregan dos columnas (NULL = `NIVEL`, compatible con todo lo existente):

```sql
ALTER TABLE dbo.CuadroFila ADD
    OperacionN varchar(16) NULL,   -- se aplica a ΣN (por entidad/sector, por mes)
    OperacionD varchar(16) NULL;   -- se aplica a ΣD (solo RATIO)
-- CHECK: OperacionN/OperacionD IN ('NIVEL','ANUALIZADO','VARIACION_1','VARIACION_12','RETARDO_1','RETARDO_12','PROMEDIO_DIC','PROMEDIO_DIC_ROA','BASE_ROE_DIC')
```

| Operación | Valor en el mes t (S = suma de los términos del rol) |
|---|---|
| `NIVEL` (o NULL) | S_t |
| `ANUALIZADO` | S_t × 12 / mes(t) |
| `VARIACION_1` | S_t − S_{t−1} |
| `VARIACION_12` | S_t − S_{t−12} |
| `RETARDO_1` / `RETARDO_12` | S_{t−1} / S_{t−12} |
| `PROMEDIO_DIC` | media de S desde diciembre del año anterior hasta t (exige todos los meses; si falta uno, NULL) |
| `PROMEDIO_DIC_ROA` | igual que `PROMEDIO_DIC`, salvo en diciembre: S_t |
| `BASE_ROE_DIC` | igual que `PROMEDIO_DIC`, salvo en diciembre: patrimonio_dic − Gan_Eje_dic. Validar con el oráculo antes de dejarla; si no cuadra en todas las entidades, ROE queda `JSON` y se documenta |

Combinaciones resultantes:

| Fila | Calculo | N (op) | D (op) | Escala |
|---|---|---|---|---|
| Análisis horizontal | `RATIO` | cuenta (`VARIACION_12`) | cuenta (`RETARDO_12`) | 100 |
| `@xxxxA` | `SUMA` | cuenta xxxx (`ANUALIZADO`) | — | 1e‑6 |
| EFI04 | `SUMA` | cuenta (`VARIACION_1`) | — | 1e‑6 |
| `tasa_X` | `RATIO` | términos de X (`VARIACION_12`) | términos de X (`RETARDO_12`) | 100 |
| ROA | `RATIO` | términos de `Gan_Eje` (`ANUALIZADO`) | cuenta 1 (`PROMEDIO_DIC_ROA`) | 100 |
| ROE | `RATIO` | términos de `Gan_Eje` (`ANUALIZADO`) | cuenta 3 (`BASE_ROE_DIC`) | 100 |

> ⚠ `BASE_ROE_DIC` necesita `Gan_Eje` dentro del denominador. Implementarlo como caso especial en el SP (en diciembre, D = S_D − N_sin_anualizar), o descartarlo y dejar ROE en `JSON`. Decidir según cuadre con el oráculo.

### 3.2 SPs (`db/fase3/03_operaciones_tiempo.sql`)

Hay que reescribir `api.ObtenerValoresCuadroEntidades` y `api.ObtenerValoresCuadroSector` (`CREATE OR ALTER`, sin cambiar firmas ni columnas de salida):

1. **Calendario.** `#meses(FechaID, Anio, Mes, FechaID_1, FechaID_12, FechaID_DicAnterior)` a partir de los `FechaID` distintos de `B11`. Calcular el mes anterior con `DATEDIFF(day,'19500101', EOMONTH(DATEADD(day, FechaID,'19500101'), -k))`.
2. **Base.** `#base(IFIID, CuadroFilaID, Rol, FechaID, S)` con la suma de términos por rol y mes. Es lo que hoy es `#valores` agregado; mantener tablas temporales con PK, nunca CTEs encadenados.
3. **Operación por rol.** Aplicar `OperacionN`/`OperacionD` con joins a `#meses` y a `#base` desplazado (para el promedio, `AVG` sobre `FechaID BETWEEN FechaID_DicAnterior AND FechaID` exigiendo `COUNT = Mes + 1`).
4. **Resultado.** Igual que hoy: `SUMA → N·Escala`; `RATIO → N/D·Escala` si `D ≠ 0`. Un mes sin valor no se devuelve (NaN ≠ 0).
5. **Rendimiento.** La carga masiva de REP01 para 229 entidades hoy tarda ≈ 7 s y debe seguir < 20 s. Medir en el servidor con una consulta suelta, sin crear objetos (como se hizo en la fase 2).

No hace falta cambiar `api.fnFilasCuadro` ni `api.ObtenerPlantilla`. El **backend no cambia**: `ArmadorCatalogo` trata como calculada toda fila `SUMA`/`RATIO`, y las operaciones son internas al SP.

### 3.3 Generador y validación (`tools/catalogo/generar_formulas_fase3_paso2.py`)

Reutilizar `Datos`, `tolerancia_ratio` y `ANOMALAS` de `tools/catalogo/generar_formulas_fase3.py` (importar, no copiar):

1. **Implementar las operaciones de 3.1 en numpy**, sobre las series mensuales que ya carga `Datos`.
   - Los JSON traen historia desde 2021‑05, así que la validación offline usa todos los meses reales, aunque `B11` empiece en 2024.
2. **Candidatos:**
   - **Análisis horizontal:** todas las filas de EFI06/SFN06 con `IdSub = 'Análisis Horizontal (%)'` (por cuenta).
   - **`@xxxxA`:** todas las filas de EFI03, SFN03 y REP01 cuyo código es `@dígitosA`. Usar `candidatos_descubiertos.json`: tipo `TIEMPO:ANUALIZADO`, `base`. Probar también las `NO_RESUELTA` con esa forma.
   - **EFI04/SFN04:** emparejar `Variable` con el texto de la fila de EFI01 de la misma entidad y tomar su CUC (`@1`, `@11`…). Las filas de otros grupos de EFI04, si existen, se informan y quedan `JSON`.
   - **`tasa_X`:** solo si X ya tiene fórmula `SUMA` en el catálogo. Leer la BD o reconstruir desde `db/fase2/01_semilla_catalogo.sql` + `db/fase3/01_formulas_suma_ratio.sql`.
   - **ROA / ROE** en EFI11, EFI12, EFI13, REP01 y CAR05 (sector) con sus códigos de §1.2.
3. **Validar** como en el paso 1 (entidades + sectores, tolerancias, OYS aparte). Generar:
   - `db/fase3/04_formulas_tiempo.sql`: `UPDATE` idempotente de `Calculo`, `Escala`, `OperacionN`, `OperacionD` y términos. Para el análisis horizontal, de forma masiva como el vertical en `01`.
   - `db/fase3/formulas_tiempo_reporte.md`: aceptadas, rechazadas y motivo.

### 3.4 Si se elige la opción (B) de la ventana

- `db/fase3/05_ventana_base_calculo.sql`:
  - Tabla `dbo.Parametro(Clave, Valor)` con `VentanaDesde = '2024-01'`.
  - `api.vVentana` toma `Desde` de ahí, no de `MIN(FechaID)`.
  - Los SPs de valores no devuelven meses anteriores a `VentanaDesde`, pero sí los usan para calcular.
- Documentar para quien mantiene `dbo.sp_etl_bce`:
  - Ejecutar el script R con rango 2023‑01 → 2023‑12. El SP es idempotente por mes.
  - Con eso `B11` crece un ~37 % y la vista indexada se mantiene sola.

---

## 4. Pruebas y verificación

1. **LocalDB** (§2.2) con datos sintéticos que ejerciten cada operación:
   - 3 entidades × 26 meses, incluido un diciembre y un hueco de un mes.
   - Valores esperados calculados a mano en el propio script de prueba.
2. **`db/fase3/06_pruebas_tiempo.sql`** (contra el servidor, valores del JSON):
   - Análisis horizontal Pichincha (IFIID 260), cuenta 1, 2026‑06 ≈ 7,22 (±0,005).
   - `@5A` BP Amazonas (IFIID 184) 2026‑07 ≈ 50,1663; 2025‑01 ≈ 44,5582 (este último solo con la opción B; con A, 2025‑01 sí existe porque el anualizado no necesita historia).
   - EFI04 Pichincha ACTIVO 2026‑06 ≈ −341,04 (±0,005).
   - `tasa_IF011` Pichincha 2026‑06 ≈ 7,2768.
   - ROA Pichincha 2026‑06 = 1,146 y 2025‑12 = 1,1164 (diciembre).
   - Conteos: filas del análisis horizontal ya no `JSON` = 2 776 (EFI06) + 4 215 (SFN06).
3. **El usuario ejecuta** `03`, `04` (y `05` si aplica) y `06`. Después, el agente:
   - Corre `dotnet test` completo (debe seguir en verde).
   - Agrega a `BackendDF/tests/BackendDF.Tests/SqlIntegracionTests.cs` pruebas por API: `/api/cuadros/EFI06?entidad=BP__PICHINCHA&analisis=horizontal`, `/api/cuadros/EFI03?entidad=BP__AMAZONAS`, `/api/cuadros/EFI04?entidad=BP__PICHINCHA`.
   - Cuenta en la BD cuántas filas quedan `JSON`, por cuadro.
4. **Actualizar** `db/fase3/README.md` (pasos), `docs/informe-sav-a-sql.md` §8.7 (estado) y este plan (marcar lo hecho).

---

## 5. Fuera de alcance (no hacer en este paso)

- **Indicadores EFI11‑13 y REP01 restantes** (morosidades, coberturas, liquidez, CAMELS, PERLAS global, IVF): paso 3, con la nota técnica de la SB.
- **Cuadros geográficos** (GEO/PDA/GEE/PDE): pendiente documentado en `docs/propuesta-cuadros-geograficos.md`.
- **Limitar la validación de `tools/catalogo/generar_semilla.py` al último mes real**: pendiente aparte.
- **Seguridad**: la contraseña de `sa` está en `appsettings.json` y en `sp_etl_bce`. El usuario lo resolverá al desplegar; no tocarlo.
