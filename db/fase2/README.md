# Fase 2 — Catálogo de cuadros en `idce_bco_coop`

El catálogo reemplaza al JSON como **plantilla** de cada cuadro. Define:
- qué filas lleva el cuadro y en qué orden;
- el encabezado, el nivel y el texto de cada fila;
- **cómo se calcula** el valor de cada fila: una cuenta, un indicador, sumas y restas de cuentas, o ratios.

Los cálculos los hace el SP. Contexto: [`docs/informe-sav-a-sql.md`](../../docs/informe-sav-a-sql.md). Requiere la [Fase 0](../fase0/README.md).

## Orden de ejecución

| # | Script | Qué hace |
|---|---|---|
| 1 | `00_catalogo_tablas.sql` | Tablas `dbo.Cuadro`, `dbo.CuadroFila`, `dbo.CuadroFilaTermino` |
| 2 | `01_semilla_catalogo.sql` | **Generado.** 29 cuadros · 25 688 filas · 8 305 términos. Reemplaza todo el catálogo en una transacción. Pesa ~4,6 MB: conviene ejecutarlo con `sqlcmd` |
| 3 | `02_procedimientos_cuadros.sql` | Funciones `api.fnResolverVariante` y `api.fnFilasCuadro`, y 7 procedimientos |
| 4 | `03_pruebas.sql` | 16 pruebas con valores verificados contra los JSON y el `.sav` |
| — | `99_rollback.sql` | Borra los procedimientos, las funciones y las tablas del catálogo |

```bash
sqlcmd -S 192.168.1.5 -d idce_bco_coop -U <usuario> -P <clave> -b -f 65001 -i db/fase2/00_catalogo_tablas.sql -i db/fase2/01_semilla_catalogo.sql -i db/fase2/02_procedimientos_cuadros.sql -i db/fase2/03_pruebas.sql
```

`-f 65001` hace que los acentos se lean como UTF‑8. El rol `api_lectura` de la Fase 0 ya cubre los objetos nuevos del esquema `api`.

## Modelo

| Tabla | Una fila es… | Columnas clave |
|---|---|---|
| `dbo.Cuadro` | un cuadro | `Ambito` (ENTIDAD/SECTOR), `Tipo`, `FechasJson` (1 = no sale del `.sav`, conserva sus fechas), `UsaNombreCuenta` |
| `dbo.CuadroFila` | una fila de una plantilla | `Variante`, `IdSub` (análisis o tipo de crédito), `Orden`, `Codigo`, `CuentaID` (para el texto), `Grupo`, `Etiqueta`, `Nivel`, `Calculo`, `Escala` |
| `dbo.CuadroFilaTermino` | un término de la fórmula | `Rol` (N/D), `Fuente` (CUENTA/INDICADOR), `CuentaID` o `IndicadorCodigo`, `Factor` |

**`Calculo`:**
- `SUMA` = Σ (factor × valor) × escala.
- `RATIO` = ΣN / ΣD × escala.
- `JSON` = fórmula todavía no definida. El orden y el texto ya salen del catálogo; el backend completa el valor con el JSON mientras tanto.

Ejemplo, `pasipat` (EFI01/SFN01):

```text
CuadroFila:          Codigo = 'pasipat', Etiqueta = 'TOTAL PASIVO Y PATRIMONIO', Calculo = 'SUMA', Escala = 1e-6
CuadroFilaTermino:   (N, CUENTA, 2, +1)   (N, CUENTA, 3, +1)
```

Para **definir una fórmula nueva**:
1. Cambiar `Calculo` de `JSON` a `SUMA` o `RATIO`.
2. Insertar sus términos.

El backend la toma sola, sin cambiar código.

**Texto de las filas de cuenta:**
- Sale de `dbo.Cuenta.Nombre`. En los balances lleva el formato `"1101.    Caja"`.
- Si el nombre está vacío o es genérico (`cta …`), se usa `Etiqueta`.
- REP01 tiene `UsaNombreCuenta = 0`: ahí el texto es clave de búsqueda del frontend y se conserva el publicado.

**Variantes:** algunas plantillas cambian según a quién se aplica el cuadro.
- Cuadros de entidad: el tipo (`BP`, `COOP`, `MU`). Por ejemplo, EFI06 tiene 890, 962 y 924 cuentas según el tipo.
- Cuadros de sector: `tipos[:segmentos]`, p. ej. `BP:2` o `COOP:3` (CAR02, TEA01).
- `*` si la plantilla es igual para todos.
- Los SP buscan la variante exacta, luego la de solo tipos y luego `*`.

## Procedimientos

| Procedimiento | Parámetros | Resultado |
|---|---|---|
| `api.ListarCuadros` | — | Metadatos y variantes de cada cuadro activo |
| `api.ObtenerPlantilla` | `@CuadroID, @Variante = NULL, @IdSub = NULL` | Filas en orden: `CuadroFilaID, Variante, IdSub, Orden, Codigo, CodigoBase, Grupo, Variable, Nivel, SegmentoCredito, Unidad, Calculo` |
| `api.ObtenerValoresCuadroEntidades` | `@CuadroID, @IFIIDs, @IdSub = NULL` | `IFIID, CuadroFilaID, Periodo, Valor` (ya escalado). Sirve para carga masiva (REP01/rankings) |
| `api.ObtenerValoresCuadroSector` | `@CuadroID, @TiposEntidad, @Segmentos = NULL, @IdSub = NULL` | `CuadroFilaID, Periodo, Valor`, sobre `api.vSaldoAgregado` |
| `api.ObtenerCuadroEntidad` | `@CuadroID, @IFIID, @IdSub = NULL` | **Dos resultsets:** plantilla (según el tipo de la entidad) y valores |
| `api.ObtenerCuadroSector` | `@CuadroID, @TiposEntidad, @Segmentos = NULL, @IdSub = NULL` | **Dos resultsets:** plantilla y valores |

## Regenerar la semilla

```bash
python tools/catalogo/generar_semilla.py
```

Lee los JSON y vuelve a escribir `01_semilla_catalogo.sql` y [`semilla_reporte.md`](semilla_reporte.md). El reporte incluye las variantes, las plantillas atípicas y las fórmulas validadas.

**Ojo:** una vez que el equipo empiece a editar el catálogo en la BD, regenerar la semilla **pisa esos cambios**. A partir de ahí la fuente de verdad es la BD, no el generador.
