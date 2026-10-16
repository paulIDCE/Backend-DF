# Guía: agregar o cambiar la fórmula de una fila de un cuadro

Para pasar una fila de `Calculo = 'JSON'` a un valor que calcule la BD. **No se toca código del backend ni los SP**: basta con editar dos tablas del catálogo. El backend toma la fórmula nueva en la siguiente consulta.

Requisitos: fases 0 y 2 instaladas y, si la fórmula usa operaciones en el tiempo, `db/fase3/03_operaciones_tiempo.sql`.

---

## 1. Cómo funciona (resumen)

```
dbo.Cuadro (1) ──< dbo.CuadroFila (N) ──< dbo.CuadroFilaTermino (N)
  "CAR01"            "IF011"               (N, CUENTA 14,   +1)
                                           (N, CUENTA 1499, −1)
```

| Tabla | Qué se edita |
|---|---|
| `dbo.CuadroFila` | `Calculo`, `Escala`, `OperacionN`, `OperacionD` de la fila |
| `dbo.CuadroFilaTermino` | los términos: `Rol`, `Fuente`, `CuentaID` o `IndicadorCodigo`, `Factor` |

Para cada fila, mes a mes:

```
SN = Σ (Factor × valor)   términos con Rol = 'N'
SD = Σ (Factor × valor)   términos con Rol = 'D'      (solo RATIO)

N  = OperacionN(SN)       NULL o 'NIVEL' = sin transformar
D  = OperacionD(SD)

SUMA  → Valor = N × Escala
RATIO → Valor = N / D × Escala          D = 0 → sin valor
```

Lo calculan `api.ObtenerValoresCuadroEntidades` (lee `dbo.B11` y `dbo.IndicadorData`) y `api.ObtenerValoresCuadroSector` (lee `api.vSaldoAgregado`). Los dos llaman a `dbo.AplicarOperacionesTiempo` cuando hay operaciones.

### Valores permitidos

| Columna | Valores | Notas |
|---|---|---|
| `CuadroFila.Calculo` | `SUMA`, `RATIO`, `JSON` | `JSON` = el backend usa el valor del archivo JSON |
| `CuadroFila.Escala` | número | `1e-6` → millones · `100` → porcentaje · `1` → sin cambio |
| `CuadroFila.OperacionN` / `OperacionD` | ver tabla abajo | `OperacionD` solo tiene sentido en `RATIO`; en `SUMA` dejarla `NULL` |
| `CuadroFilaTermino.Rol` | `N`, `D` | `D` solo en `RATIO` |
| `CuadroFilaTermino.Fuente` | `CUENTA`, `INDICADOR` | `CUENTA` → `CuentaID` (código de cuenta: 14, 1499, 1101…) · `INDICADOR` → `IndicadorCodigo` (`dbo.IndicadorFin.Codigo`). Nunca los dos a la vez |
| `CuadroFilaTermino.Factor` | número | `1` suma, `-1` resta, `0.09` pondera |

### Operaciones en el tiempo

| Operación | Valor en el mes t | Ejemplo de uso |
|---|---|---|
| `NIVEL` (o `NULL`) | S_t | casi todo |
| `ANUALIZADO` | S_t × 12 / mes(t) | PyG anualizado, numerador de ROA y ROE |
| `VARIACION_1` | S_t − S_{t−1} | variación mensual (EFI04) |
| `VARIACION_12` | S_t − S_{t−12} | numerador de una tasa de crecimiento anual |
| `RETARDO_1` / `RETARDO_12` | S_{t−1} / S_{t−12} | denominador de una tasa de crecimiento |
| `PROMEDIO_DIC` | promedio desde el diciembre anterior hasta t | saldos promedio |
| `PROMEDIO_DIC_ROA` | igual que el anterior, pero en diciembre usa S_t | denominador del ROA |
| `BASE_ROE_DIC` | igual, pero en diciembre usa S_t − ΣN_t | denominador del ROE |

Si falta un mes necesario (p. ej. t−12 antes de 2025‑01, porque `B11` empieza en 2024‑01), ese mes **no se devuelve**. No es un error.

### Limitaciones a tener en cuenta

- **Cuadros de sector (SFN…, CAR02…):** solo se usan términos `CUENTA`. Un término `INDICADOR` se ignora, porque un indicador no se puede sumar entre entidades.
- **Variantes:** un mismo `Codigo` puede existir en varias plantillas (`*`, `BP`, `COOP`, `MU`, `BP:2`…). La fórmula vale para todas las entidades que comparten esa plantilla, así que hay que validarla en todas.
- **Las filas no se crean ni se borran con esta guía:** solo se cambia cómo se calculan. La fila tiene que existir en `dbo.CuadroFila`.

---

## 2. Paso a paso

### Paso 1 — Encontrar la fila

```sql
USE idce_bco_coop;

SELECT f.CuadroFilaID, f.CuadroID, f.Variante, f.IdSub, f.Orden, f.Codigo, f.CuentaID,
       f.Etiqueta, f.Calculo, f.Escala, f.OperacionN, f.OperacionD
FROM dbo.CuadroFila AS f
WHERE f.CuadroID = 'REP01'            -- cuadro
  AND f.Codigo   = 'SB005'            -- código publicado de la fila
  -- AND f.IdSub = N''                -- sub-vista, si el cuadro tiene varias
  -- AND f.Etiqueta LIKE N'%texto%'   -- para filas sin código
ORDER BY f.Variante, f.IdSub, f.Orden;
```

Anotar las variantes que aparecen y el `IdSub`.

Para ver la fórmula actual de una fila:

```sql
SELECT t.*
FROM dbo.CuadroFilaTermino AS t
WHERE t.CuadroFilaID = 12345;         -- CuadroFilaID de la consulta anterior
```

### Paso 2 — Verificar que existen las cuentas e indicadores

```sql
-- Cuentas: deben existir y tener saldos en B11
SELECT c.CuentaID, c.Nombre,
       (SELECT COUNT(*) FROM dbo.B11 AS b WHERE b.CuentaID = c.CuentaID) AS FilasB11
FROM dbo.Cuenta AS c
WHERE c.CuentaID IN (1101, 1102, 1104, 1105);

-- Indicadores
SELECT ind.IndicadorFinID, ind.Codigo
FROM dbo.IndicadorFin AS ind
WHERE ind.Codigo IN ('ACT_PON_RIESGO');
```

### Paso 3 — Aplicar la fórmula (plantilla)

Copiar este script, completar las dos tablas `@f` y `@t` y ejecutarlo. Es **idempotente**: borra y vuelve a crear los términos de las filas que toca, así que se puede correr más de una vez. Una fila puede llevar varios términos; se agrega una fila en `@t` por cada uno.

```sql
/* =====================================================================================
   Fórmula: <qué calcula y por qué>  ·  Autor: <nombre>  ·  Fecha: <yyyy-mm-dd>
   Validada contra: <JSON / nota técnica / entidades probadas>
   ===================================================================================== */
USE idce_bco_coop;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;      -- ante cualquier error deshace todo
BEGIN TRANSACTION;

/* ---------- 1. Filas y cómo se calculan ----------
   Variante = NULL → aplica a TODAS las variantes de esa fila (lo habitual).
   OperacionN / OperacionD = NULL → NIVEL.                                          */
DECLARE @f TABLE (CuadroID varchar(16), Variante varchar(20) NULL, IdSub nvarchar(80), Codigo varchar(64),
                  Calculo varchar(8), Escala float, OperacionN varchar(16) NULL, OperacionD varchar(16) NULL);
INSERT INTO @f VALUES
-- CuadroID  Variante  IdSub   Codigo    Calculo  Escala  OpN   OpD
('REP01',    NULL,     N'',    'SB005',  'SUMA',  1e-6,   NULL, NULL);

/* ---------- 2. Términos ----------
   Fuente 'CUENTA'    → CuentaID con valor, IndicadorCodigo NULL
   Fuente 'INDICADOR' → IndicadorCodigo con valor, CuentaID NULL                    */
DECLARE @t TABLE (CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64),
                  Rol char(1), Fuente varchar(10), CuentaID int NULL, IndicadorCodigo varchar(32) NULL, Factor float);
INSERT INTO @t VALUES
-- CuadroID  IdSub  Codigo   Rol  Fuente     CuentaID  Indicador  Factor
('REP01',    N'',   'SB005', 'N', 'CUENTA',  1101,     NULL,      1),
('REP01',    N'',   'SB005', 'N', 'CUENTA',  1102,     NULL,      1),
('REP01',    N'',   'SB005', 'N', 'CUENTA',  1104,     NULL,      1),
('REP01',    N'',   'SB005', 'N', 'CUENTA',  1105,     NULL,      1);

/* ---------- 3. Resolver las filas del catálogo ---------- */
DECLARE @filas TABLE (CuadroFilaID int PRIMARY KEY, CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64));
INSERT INTO @filas
SELECT cf.CuadroFilaID, cf.CuadroID, cf.IdSub, cf.Codigo
FROM dbo.CuadroFila AS cf
INNER JOIN @f AS f ON f.CuadroID = cf.CuadroID AND f.IdSub = cf.IdSub AND f.Codigo = cf.Codigo
                  AND (f.Variante IS NULL OR f.Variante = cf.Variante);

-- Controles: aborta si algo no cuadra
IF EXISTS (SELECT 1 FROM @f AS f WHERE NOT EXISTS
           (SELECT 1 FROM @filas AS x WHERE x.CuadroID = f.CuadroID AND x.IdSub = f.IdSub AND x.Codigo = f.Codigo))
    THROW 50100, N'Alguna fila de @f no existe en dbo.CuadroFila (revisar CuadroID / IdSub / Codigo / Variante).', 1;
IF EXISTS (SELECT 1 FROM @f AS f WHERE NOT EXISTS
           (SELECT 1 FROM @t AS t WHERE t.CuadroID = f.CuadroID AND t.IdSub = f.IdSub AND t.Codigo = f.Codigo AND t.Rol = 'N'))
    THROW 50101, N'Alguna fila no tiene términos de numerador (Rol N).', 1;
IF EXISTS (SELECT 1 FROM @f AS f WHERE f.Calculo = 'RATIO' AND NOT EXISTS
           (SELECT 1 FROM @t AS t WHERE t.CuadroID = f.CuadroID AND t.IdSub = f.IdSub AND t.Codigo = f.Codigo AND t.Rol = 'D'))
    THROW 50102, N'Una fila RATIO no tiene términos de denominador (Rol D).', 1;
IF EXISTS (SELECT 1 FROM @t AS t WHERE t.Fuente = 'CUENTA' AND NOT EXISTS (SELECT 1 FROM dbo.Cuenta AS c WHERE c.CuentaID = t.CuentaID))
    THROW 50103, N'Alguna cuenta de @t no existe en dbo.Cuenta.', 1;
IF EXISTS (SELECT 1 FROM @t AS t WHERE t.Fuente = 'INDICADOR' AND NOT EXISTS (SELECT 1 FROM dbo.IndicadorFin AS i WHERE i.Codigo = t.IndicadorCodigo))
    THROW 50104, N'Algún indicador de @t no existe en dbo.IndicadorFin.', 1;

/* ---------- 4. Reemplazar términos y actualizar la fila ---------- */
DELETE t FROM dbo.CuadroFilaTermino AS t INNER JOIN @filas AS x ON x.CuadroFilaID = t.CuadroFilaID;

INSERT INTO dbo.CuadroFilaTermino (CuadroFilaID, Rol, Fuente, CuentaID, IndicadorCodigo, Factor)
SELECT x.CuadroFilaID, t.Rol, t.Fuente, t.CuentaID, t.IndicadorCodigo, t.Factor
FROM @filas AS x
INNER JOIN @t AS t ON t.CuadroID = x.CuadroID AND t.IdSub = x.IdSub AND t.Codigo = x.Codigo;

UPDATE cf SET Calculo = f.Calculo, Escala = f.Escala, OperacionN = f.OperacionN, OperacionD = f.OperacionD
FROM dbo.CuadroFila AS cf
INNER JOIN @filas AS x ON x.CuadroFilaID = cf.CuadroFilaID
INNER JOIN @f AS f ON f.CuadroID = x.CuadroID AND f.IdSub = x.IdSub AND f.Codigo = x.Codigo;

DECLARE @n int = (SELECT COUNT(*) FROM @filas);
PRINT CONCAT(N'Filas actualizadas: ', @n);

COMMIT TRANSACTION;
GO
```

> Si `03_operaciones_tiempo.sql` no está instalado, las columnas `OperacionN`/`OperacionD` no existen: hay que quitarlas del `UPDATE`.

Ejecutarlo con `sqlcmd` (`-f 65001` para que los acentos se lean bien):

```bash
sqlcmd -S 192.168.1.5 -d idce_bco_coop -U <usuario> -P <clave> -b -I -f 65001 -i mi_formula.sql
```

### Paso 4 — Probar el resultado

```sql
-- Una entidad: RS1 plantilla, RS2 valores (comparar con el JSON publicado)
EXEC api.ObtenerCuadroEntidad @CuadroID = 'REP01', @IFIID = 123;

-- Solo la fila nueva, para varias entidades de distinto tipo
DECLARE @r TABLE (IFIID int, CuadroFilaID int, Periodo char(7), Valor float);
INSERT INTO @r EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'REP01', @IFIIDs = '123,456,789';
SELECT r.*, f.Codigo
FROM @r AS r INNER JOIN dbo.CuadroFila AS f ON f.CuadroFilaID = r.CuadroFilaID
WHERE f.Codigo = 'SB005'
ORDER BY r.IFIID, r.Periodo;

-- Sector (si el cuadro es de sector)
EXEC api.ObtenerCuadroSector @CuadroID = 'SFN01', @TiposEntidad = 'COOP', @Segmentos = '1';
```

Checklist antes de dar la fórmula por buena:
- [ ] Coincide con el JSON publicado en meses reales (hasta el último mes de la BD, `SELECT * FROM api.vVentana`). Las proyecciones del JSON no sirven para comparar.
- [ ] Se probó en entidades de **cada tipo** (`BP`, `COOP`, `MU`) que usan la plantilla.
- [ ] Las diferencias son solo de redondeo (los saldos publicados tienen 4 decimales en millones).
- [ ] Revisar `GET /api/meta`: el cuadro debería tener una fila menos en JSON.

### Paso 5 — Deshacer (volver al JSON)

```sql
USE idce_bco_coop;
BEGIN TRANSACTION;

DECLARE @ids TABLE (CuadroFilaID int PRIMARY KEY);
INSERT INTO @ids
SELECT CuadroFilaID FROM dbo.CuadroFila
WHERE CuadroID = 'REP01' AND IdSub = N'' AND Codigo = 'SB005';

DELETE t FROM dbo.CuadroFilaTermino AS t INNER JOIN @ids AS i ON i.CuadroFilaID = t.CuadroFilaID;
UPDATE f SET Calculo = 'JSON', OperacionN = NULL, OperacionD = NULL
FROM dbo.CuadroFila AS f INNER JOIN @ids AS i ON i.CuadroFilaID = f.CuadroFilaID;

COMMIT TRANSACTION;
```

---

## 3. Ejemplos listos para copiar (solo `@f` y `@t`)

**Resta de cuentas: cartera bruta = 14 − 1499 (millones)**

```sql
INSERT INTO @f VALUES ('CAR01', NULL, N'Cartera Total', 'IF011', 'SUMA', 1e-6, NULL, NULL);
INSERT INTO @t VALUES
('CAR01', N'Cartera Total', 'IF011', 'N', 'CUENTA', 14,   NULL,  1),
('CAR01', N'Cartera Total', 'IF011', 'N', 'CUENTA', 1499, NULL, -1);
```

**Ratio en porcentaje: PERLAS E1 = 100 × cartera neta / activo**

```sql
INSERT INTO @f VALUES ('EFI13', NULL, N'', 'E1_Cartera', 'RATIO', 100, NULL, NULL);
INSERT INTO @t VALUES
('EFI13', N'', 'E1_Cartera', 'N', 'CUENTA', 14,   NULL,  1),
('EFI13', N'', 'E1_Cartera', 'N', 'CUENTA', 1499, NULL, -1),
('EFI13', N'', 'E1_Cartera', 'D', 'CUENTA', 1,    NULL,  1);
```

**Tasa de crecimiento anual: 100 × (X_t − X_{t−12}) / X_{t−12}**

```sql
INSERT INTO @f VALUES ('CAR01', NULL, N'Cartera Total', 'tasa_14', 'RATIO', 100, 'VARIACION_12', 'RETARDO_12');
INSERT INTO @t VALUES
('CAR01', N'Cartera Total', 'tasa_14', 'N', 'CUENTA', 14, NULL, 1),
('CAR01', N'Cartera Total', 'tasa_14', 'D', 'CUENTA', 14, NULL, 1);
```

**Valor anualizado del PyG (millones)**

```sql
INSERT INTO @f VALUES ('EFI03', NULL, N'', 'Marg_Ne_anual', 'SUMA', 1e-6, 'ANUALIZADO', NULL);
INSERT INTO @t VALUES
('EFI03', N'', 'Marg_Ne_anual', 'N', 'CUENTA', 51, NULL,  1),
('EFI03', N'', 'Marg_Ne_anual', 'N', 'CUENTA', 41, NULL, -1);
```

**ROA = 100 × ganancia anualizada (ingresos 51‑56 − gastos 41‑48) / activo promedio desde diciembre**

```sql
INSERT INTO @f VALUES ('EFI12', NULL, N'', 'E1_ROA', 'RATIO', 100, 'ANUALIZADO', 'PROMEDIO_DIC_ROA');
INSERT INTO @t VALUES
('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 41, NULL, -1), ('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 42, NULL, -1),
('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 43, NULL, -1), ('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 44, NULL, -1),
('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 45, NULL, -1), ('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 46, NULL, -1),
('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 47, NULL, -1), ('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 48, NULL, -1),
('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 51, NULL,  1), ('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 52, NULL,  1),
('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 53, NULL,  1), ('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 54, NULL,  1),
('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 55, NULL,  1), ('EFI12', N'', 'E1_ROA', 'N', 'CUENTA', 56, NULL,  1),
('EFI12', N'', 'E1_ROA', 'D', 'CUENTA', 1,  NULL,  1);
```

**Término de indicador ponderado: PTR_9 = 0,09 × activos ponderados por riesgo** (solo cuadros de entidad)

```sql
INSERT INTO @f VALUES ('EFI05', NULL, N'', 'PTR_9', 'SUMA', 1e-6, NULL, NULL);
INSERT INTO @t VALUES
('EFI05', N'', 'PTR_9', 'N', 'INDICADOR', NULL, 'ACT_PON_RIESGO', 0.09);
```

> Todos estos ejemplos son fórmulas que ya están cargadas (salen de `db/fase3/01_formulas_suma_ratio.sql`, `04_formulas_tiempo.sql` y `07_formulas_sin_nota.sql`). Sirven de referencia; para una fila nueva, cambiar cuadro, código y términos.

---

## 4. Precauciones

- **Regenerar la semilla pisa todo.** `python tools/catalogo/generar_semilla.py` + `db/fase2/01_semilla_catalogo.sql` reemplazan el catálogo completo. Desde que el equipo edita el catálogo a mano, **la fuente de verdad es la BD**: no volver a correr la semilla en el servidor.
- **Los scripts generados de fase 3 rehacen sus filas.** Si se vuelve a correr `01`, `04` o `07`, las filas que esos scripts tocan vuelven a su fórmula generada. Si cambias una de esas filas, avisa al equipo o actualiza el generador correspondiente en `tools/catalogo/`.
- **Probar primero en LocalDB** si la fórmula usa una operación nueva o rara (ver `db/fase3/README.md`, sección "Prueba previa en LocalDB").
- **Guardar el script** en `db/` (p. ej. `db/fase3/09_<descripcion>.sql`) con el encabezado completado, para que quede registro de qué se cambió y por qué.
