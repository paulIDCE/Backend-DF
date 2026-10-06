# Fase 0 — Objetos de lectura en `idce_bco_coop`

Crea el esquema `api` con vistas y procedimientos que consumirá la capa repository del backend (Fase 1). **No agrega información ni toca el ETL** (`dbo.sp_etl_bce`): solo índices y vistas de optimización. Contexto: [`docs/informe-sav-a-sql.md`](../../docs/informe-sav-a-sql.md).

## Orden de ejecución

| # | Script | Qué hace | Tiempo aprox. |
|---|---|---|---|
| 1 | `00_esquema_seguridad.sql` | Esquema `api` (dueño `dbo`) y rol `api_lectura` (EXECUTE/SELECT solo sobre `api`). Plantilla comentada del usuario del backend | segundos |
| 2 | `01_indices.sql` | `IX_B11_IFIID_Cuenta_Fecha` (lectura por entidad), `IX_IndicadorData_IFIID`, `UX_Ifi_Ruc` | 1–3 min |
| 3 | `02_vistas.sql` | `api.vSaldoAgregado` (**vista indexada**: saldos por mes × tipo × segmento × cuenta) y `api.vVentana` (ventana C1 + firma de carga) | 1–2 min |
| 4 | `03_procedimientos.sql` | Los 7 procedimientos de abajo | segundos |
| 5 | `04_pruebas.sql` | 13 pruebas con valores ya verificados contra el `.sav` y los JSON | segundos |
| — | `99_rollback.sql` | Revierte todo lo anterior | — |

Desde SSMS: abrir y ejecutar cada archivo en orden. Desde consola:

```bash
sqlcmd -S <servidor> -d idce_bco_coop -E -b -i db/fase0/00_esquema_seguridad.sql -i db/fase0/01_indices.sql -i db/fase0/02_vistas.sql -i db/fase0/03_procedimientos.sql -i db/fase0/04_pruebas.sql
```

Después, crear el usuario del backend con la plantilla comentada al final de `00_esquema_seguridad.sql`. La contraseña no va en el repositorio.

## Contrato para la capa repository

Todos devuelven `Periodo` como `yyyy-MM` y montos en **USD** (el backend escala a millones). Las listas van separadas por comas; las cuentas se aceptan con o sin `@`. `NULL` = sin filtro. Si una fila no existe, el SP no la devuelve: **no** la rellena con 0.

| Procedimiento | Parámetros | Columnas |
|---|---|---|
| `api.ObtenerVentana` | — | `Desde, Hasta, DesdeFechaID, HastaFechaID, Meses, MesesConDatos, FilasB11, Firma` |
| `api.ListarIfi` | — | `IFIID, Ruc, Nombre, TipoEntidad, Segmento` |
| `api.ListarCuentas` | — | `CuentaID, Nombre` |
| `api.ObtenerSaldosEntidad` | `@IFIID int`, `@Cuentas varchar(max) = NULL` | `Cuenta, Periodo, Saldo` |
| `api.ObtenerSaldosEntidades` | `@IFIIDs varchar(max)`, `@Cuentas varchar(max) = NULL` | `IFIID, Cuenta, Periodo, Saldo` |
| `api.ObtenerSaldosAgregado` | `@TiposEntidad varchar(100)`, `@Segmentos varchar(100) = NULL`, `@Cuentas varchar(max) = NULL` | `Cuenta, Periodo, Saldo, Entidades` |
| `api.ObtenerIndicadoresEntidad` | `@IFIID int`, `@Codigos varchar(max) = NULL` | `Codigo, Periodo, Valor` |

Sectores del frontend → parámetros de `ObtenerSaldosAgregado` (la traducción vive en el backend):

| Sector | `@TiposEntidad` | `@Segmentos` | ¿Cuadra con el JSON? |
|---|---|---|---|
| nacional | `BP,COOP,MU` | — | ≈ +0,6 % (C3) |
| privado | `BP` | — | exacto |
| grande / medianos / peque | `BP` | `1` / `2` / `3` | exacto |
| popular | `COOP` | — | +2,2 % (C3) |
| seg1 / seg2 / seg3 | `COOP` | `1` / `2` / `3` | difiere por diseño (C3, ver anexo) |
| mut | `MU` | — | exacto |

## Notas operativas

- **Vista indexada:** cualquier sesión que modifique `B11` o `Ifi` necesita `ANSI_NULLS`, `QUOTED_IDENTIFIER`, `ANSI_WARNINGS`, `ANSI_PADDING`, `ARITHABORT` y `CONCAT_NULL_YIELDS_NULL` en ON, y `NUMERIC_ROUNDABORT` en OFF.
  - `sp_etl_bce` ya se creó con esas opciones, y ODBC/SqlClient las traen por defecto.
  - Si una carga falla con *"INSERT failed because the following SET options have incorrect settings"*, esa es la causa.
- **Costo para el ETL:** cada `INSERT`/`DELETE` mensual en `B11` actualiza la vista indexada y el índice nuevo, lo que hace la carga algo más lenta (segundos).
- **Ventana y firma:** `api.vVentana.Firma` cambia con cada recarga (mes nuevo, filas distintas o saldos corregidos). El backend la usa para invalidar sus cachés.
