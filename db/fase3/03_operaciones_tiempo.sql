/* =====================================================================================
   Fase 3 · 03 — Operaciones en el tiempo en el catálogo de cuadros

   Cada fila calculada aplica una operación a la suma de sus términos (S) de cada rol, mes
   a mes, antes de SUMA (N × Escala) o RATIO (N / D × Escala):

     dbo.CuadroFila.OperacionN   se aplica a ΣN      NULL = NIVEL (compatible con lo existente)
     dbo.CuadroFila.OperacionD   se aplica a ΣD      (solo RATIO)

     NIVEL             S_t
     ANUALIZADO        S_t × 12 / mes(t)                          PyG anualizado (EFI03, @…A)
     VARIACION_1       S_t − S_{t−1}                              fuentes y usos (EFI04)
     VARIACION_12      S_t − S_{t−12}                             análisis horizontal, tasa_X
     RETARDO_1 / _12   S_{t−1} / S_{t−12}
     PROMEDIO_DIC      media de S desde diciembre del año anterior hasta t (mes(t)+1 puntos;
                       si falta alguno, sin valor)
     PROMEDIO_DIC_ROA  igual que PROMEDIO_DIC, salvo en diciembre: S_t
     BASE_ROE_DIC      igual que PROMEDIO_DIC, salvo en diciembre: S_t − ΣN_t
                       (patrimonio − ganancia del ejercicio)

   Un mes que necesita meses que no están en dbo.B11 (p. ej. VARIACION_12 en 2024 si B11
   empieza en 2024‑01) no tiene valor y no se devuelve; el backend marca esas filas con
   "sinHistoriaHasta" para que el frontend lo distinga de un error.

   Cambia:
     dbo.CuadroFila                 + OperacionN, OperacionD (con CHECK)
     api.fnFilasCuadro              + OperacionN, OperacionD al final
     api.ObtenerPlantilla           + OperacionN, OperacionD al final (el backend lee por nombre)
     api.ObtenerValoresCuadroEntidades / api.ObtenerValoresCuadroSector
                                    misma firma y mismas columnas; ahora aplican las operaciones
     dbo.AplicarOperacionesTiempo   (nuevo, interno) aplica las operaciones sobre #base del llamador
   Requiere fase 0 y fase 2. Idempotente.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. Columnas ---------- */
IF COL_LENGTH('dbo.CuadroFila', 'OperacionN') IS NULL
BEGIN
    ALTER TABLE dbo.CuadroFila ADD OperacionN varchar(16) NULL, OperacionD varchar(16) NULL;
    PRINT N'Agregadas dbo.CuadroFila.OperacionN / OperacionD';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CuadroFila_Operacion')
    ALTER TABLE dbo.CuadroFila ADD CONSTRAINT CK_CuadroFila_Operacion CHECK
    (    (OperacionN IS NULL OR OperacionN IN ('NIVEL', 'ANUALIZADO', 'VARIACION_1', 'VARIACION_12', 'RETARDO_1', 'RETARDO_12',
                                               'PROMEDIO_DIC', 'PROMEDIO_DIC_ROA', 'BASE_ROE_DIC'))
     AND (OperacionD IS NULL OR OperacionD IN ('NIVEL', 'ANUALIZADO', 'VARIACION_1', 'VARIACION_12', 'RETARDO_1', 'RETARDO_12',
                                               'PROMEDIO_DIC', 'PROMEDIO_DIC_ROA', 'BASE_ROE_DIC')));
GO

/* ---------- 2. Plantilla: expone las operaciones ---------- */
CREATE OR ALTER FUNCTION api.fnFilasCuadro (@CuadroID varchar(16), @Variante varchar(20))
RETURNS TABLE
AS
RETURN
    SELECT
        f.CuadroFilaID,
        f.Variante,
        f.IdSub,
        f.Orden,
        f.Codigo,
        f.CodigoBase,
        f.CuentaID,
        f.Grupo,
        CASE
            WHEN c.UsaNombreCuenta = 1 AND n.Nombre IS NOT NULL AND n.Nombre NOT LIKE 'cta %'
                THEN CASE WHEN c.Tipo IN ('Balances', 'BalancesEntidad')
                          THEN CONCAT(f.CodigoBase, N'.    ', n.Nombre)
                          ELSE n.Nombre END
            ELSE f.Etiqueta
        END                              AS Variable,
        f.Nivel,
        f.SegmentoCredito,
        COALESCE(f.Unidad, c.Unidad)     AS Unidad,
        f.Calculo,
        f.Escala,
        ISNULL(f.OperacionN, 'NIVEL')    AS OperacionN,
        ISNULL(f.OperacionD, 'NIVEL')    AS OperacionD
    FROM dbo.CuadroFila AS f
    INNER JOIN dbo.Cuadro AS c ON c.CuadroID = f.CuadroID
    OUTER APPLY (SELECT NULLIF(LTRIM(RTRIM(cu.Nombre)), '') AS Nombre
                 FROM dbo.Cuenta AS cu
                 WHERE cu.CuentaID = f.CuentaID) AS n
    WHERE f.CuadroID = @CuadroID
      AND f.Variante = @Variante;
GO

CREATE OR ALTER PROCEDURE api.ObtenerPlantilla
    @CuadroID varchar(16),
    @Variante varchar(20)  = NULL,
    @IdSub    nvarchar(80) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Cuadro WHERE CuadroID = @CuadroID AND Activo = 1)
        THROW 50020, N'El cuadro no existe en el catálogo o no está activo.', 1;

    DECLARE @v varchar(20) = api.fnResolverVariante(@CuadroID, @Variante);

    SELECT CuadroFilaID, Variante, IdSub, Orden, Codigo, CodigoBase, Grupo, Variable, Nivel, SegmentoCredito, Unidad, Calculo,
           OperacionN, OperacionD
    FROM api.fnFilasCuadro(@CuadroID, @v)
    WHERE @IdSub IS NULL OR IdSub = @IdSub
    ORDER BY Orden;
END
GO

/* -------------------------------------------------------------------------------------
   dbo.AplicarOperacionesTiempo — INTERNO. Completa #base.V (creada por el llamador) con la
   operación de cada rol. Al llegar, V ya vale S en las filas NIVEL y NULL en las demás; una
   fila que no se puede calcular (falta un mes) queda en NULL y no se devuelve.

     #base (IFIID int, CuadroFilaID int, Rol char(1), FechaID int, Op varchar(16), S float, V float NULL,
            PRIMARY KEY (IFIID, CuadroFilaID, Rol, FechaID))       -- en sector, IFIID = 0
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.AplicarOperacionesTiempo
AS
BEGIN
    SET NOCOUNT ON;

    -- Calendario de los meses que aparecen en filas con operación.
    CREATE TABLE #meses
    (
        FechaID int     NOT NULL PRIMARY KEY,
        Mes     tinyint NOT NULL,
        F1      int     NOT NULL,   -- mes anterior
        F12     int     NOT NULL,   -- mismo mes del año anterior
        FDic    int     NOT NULL    -- diciembre del año anterior
    );
    INSERT INTO #meses (FechaID, Mes, F1, F12, FDic)
    SELECT d.FechaID, MONTH(x.Fecha),
           DATEDIFF(day, '19500101', EOMONTH(x.Fecha, -1)),
           DATEDIFF(day, '19500101', EOMONTH(x.Fecha, -12)),
           DATEDIFF(day, '19500101', EOMONTH(x.Fecha, -MONTH(x.Fecha)))
    FROM (SELECT DISTINCT FechaID FROM #base WHERE Op <> 'NIVEL') AS d
    CROSS APPLY (SELECT DATEADD(day, d.FechaID, CAST('19500101' AS date)) AS Fecha) AS x;

    UPDATE b SET V = b.S * 12.0 / m.Mes
    FROM #base AS b
    INNER JOIN #meses AS m ON m.FechaID = b.FechaID
    WHERE b.Op = 'ANUALIZADO';

    UPDATE b SET V = CASE b.Op WHEN 'VARIACION_1' THEN b.S - p.S ELSE p.S END
    FROM #base AS b
    INNER JOIN #meses AS m ON m.FechaID = b.FechaID
    INNER JOIN #base AS p ON p.IFIID = b.IFIID AND p.CuadroFilaID = b.CuadroFilaID AND p.Rol = b.Rol AND p.FechaID = m.F1
    WHERE b.Op IN ('VARIACION_1', 'RETARDO_1');

    UPDATE b SET V = CASE b.Op WHEN 'VARIACION_12' THEN b.S - p.S ELSE p.S END
    FROM #base AS b
    INNER JOIN #meses AS m ON m.FechaID = b.FechaID
    INNER JOIN #base AS p ON p.IFIID = b.IFIID AND p.CuadroFilaID = b.CuadroFilaID AND p.Rol = b.Rol AND p.FechaID = m.F12
    WHERE b.Op IN ('VARIACION_12', 'RETARDO_12');

    -- Promedio desde diciembre del año anterior (exige los mes+1 puntos).
    UPDATE b SET V = a.Promedio
    FROM #base AS b
    INNER JOIN (SELECT b2.IFIID, b2.CuadroFilaID, b2.Rol, b2.FechaID, AVG(x.S) AS Promedio
                FROM #base AS b2
                INNER JOIN #meses AS m ON m.FechaID = b2.FechaID
                INNER JOIN #base AS x ON x.IFIID = b2.IFIID AND x.CuadroFilaID = b2.CuadroFilaID AND x.Rol = b2.Rol
                                     AND x.FechaID BETWEEN m.FDic AND b2.FechaID
                WHERE b2.Op IN ('PROMEDIO_DIC', 'PROMEDIO_DIC_ROA', 'BASE_ROE_DIC')
                  AND (b2.Op = 'PROMEDIO_DIC' OR m.Mes < 12)
                GROUP BY b2.IFIID, b2.CuadroFilaID, b2.Rol, b2.FechaID, m.Mes
                HAVING COUNT(*) = m.Mes + 1) AS a
        ON a.IFIID = b.IFIID AND a.CuadroFilaID = b.CuadroFilaID AND a.Rol = b.Rol AND a.FechaID = b.FechaID;

    -- Diciembre: ROA usa el saldo del mes; ROE, patrimonio − ganancia del ejercicio (ΣN sin operación).
    UPDATE b SET V = b.S
    FROM #base AS b
    INNER JOIN #meses AS m ON m.FechaID = b.FechaID
    WHERE b.Op = 'PROMEDIO_DIC_ROA' AND m.Mes = 12;

    UPDATE b SET V = b.S - n.S
    FROM #base AS b
    INNER JOIN #meses AS m ON m.FechaID = b.FechaID
    INNER JOIN #base AS n ON n.IFIID = b.IFIID AND n.CuadroFilaID = b.CuadroFilaID AND n.Rol = 'N' AND n.FechaID = b.FechaID
    WHERE b.Op = 'BASE_ROE_DIC' AND m.Mes = 12;
END
GO

/* -------------------------------------------------------------------------------------
   api.ObtenerValoresCuadroEntidades — valores SUMA/RATIO de un cuadro de entidad para
   una o varias entidades (cada una con la plantilla de su tipo). Fuentes: B11 e IndicadorData.
   Tablas temporales indexadas (con CTEs el costo crecía más que linealmente). Filas sin orden.
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ObtenerValoresCuadroEntidades
    @CuadroID varchar(16),
    @IFIIDs   varchar(max),
    @IdSub    nvarchar(80) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Cuadro WHERE CuadroID = @CuadroID AND Ambito = 'ENTIDAD' AND Activo = 1)
        THROW 50021, N'El cuadro no existe en el catálogo como cuadro de entidad.', 1;

    -- Filas calculadas por entidad (plantilla según su tipo).
    CREATE TABLE #filas
    (
        IFIID        int         NOT NULL,
        CuadroFilaID int         NOT NULL,
        Calculo      varchar(8)  NOT NULL,
        Escala       float       NOT NULL,
        OpN          varchar(16) NOT NULL,
        OpD          varchar(16) NOT NULL,
        PRIMARY KEY (IFIID, CuadroFilaID)
    );
    INSERT INTO #filas (IFIID, CuadroFilaID, Calculo, Escala, OpN, OpD)
    SELECT i.IFIID, f.CuadroFilaID, f.Calculo, f.Escala, ISNULL(f.OperacionN, 'NIVEL'), ISNULL(f.OperacionD, 'NIVEL')
    FROM dbo.Ifi AS i
    CROSS APPLY (SELECT api.fnResolverVariante(@CuadroID, i.TipoEntidad) AS Variante) AS v
    INNER JOIN dbo.CuadroFila AS f ON f.CuadroID = @CuadroID AND f.Variante = v.Variante
    WHERE i.IFIID IN (SELECT TRY_CONVERT(int, LTRIM(RTRIM(value))) FROM STRING_SPLIT(@IFIIDs, ','))
      AND f.Calculo IN ('SUMA', 'RATIO')
      AND (@IdSub IS NULL OR f.IdSub = @IdSub);

    -- Valores de origen de cada término (sin NULL).
    CREATE TABLE #valores
    (
        IFIID        int     NOT NULL,
        CuadroFilaID int     NOT NULL,
        Rol          char(1) NOT NULL,
        FechaID      int     NOT NULL,
        Valor        float   NOT NULL
    );
    INSERT INTO #valores (IFIID, CuadroFilaID, Rol, FechaID, Valor)
    SELECT f.IFIID, f.CuadroFilaID, t.Rol, b.FechaID, CAST(b.Saldo AS float) * t.Factor
    FROM #filas AS f
    INNER JOIN dbo.CuadroFilaTermino AS t ON t.CuadroFilaID = f.CuadroFilaID AND t.Fuente = 'CUENTA'
    INNER JOIN dbo.B11 AS b ON b.IFIID = f.IFIID AND b.CuentaID = t.CuentaID
    WHERE b.Saldo IS NOT NULL;

    INSERT INTO #valores (IFIID, CuadroFilaID, Rol, FechaID, Valor)
    SELECT f.IFIID, f.CuadroFilaID, t.Rol, d.FechaID, d.Valor * t.Factor
    FROM #filas AS f
    INNER JOIN dbo.CuadroFilaTermino AS t ON t.CuadroFilaID = f.CuadroFilaID AND t.Fuente = 'INDICADOR'
    INNER JOIN dbo.IndicadorFin AS ind ON ind.Codigo = t.IndicadorCodigo
    INNER JOIN dbo.IndicadorData AS d ON d.IFIID = f.IFIID AND d.IndicadorFinID = ind.IndicadorFinID
    WHERE d.Valor IS NOT NULL;

    -- S por rol y mes, con la operación del rol; V = S donde no hay operación.
    CREATE TABLE #base
    (
        IFIID        int         NOT NULL,
        CuadroFilaID int         NOT NULL,
        Rol          char(1)     NOT NULL,
        FechaID      int         NOT NULL,
        Op           varchar(16) NOT NULL,
        S            float       NOT NULL,
        V            float       NULL,
        PRIMARY KEY (IFIID, CuadroFilaID, Rol, FechaID)
    );
    INSERT INTO #base (IFIID, CuadroFilaID, Rol, FechaID, Op, S, V)
    SELECT v.IFIID, v.CuadroFilaID, v.Rol, v.FechaID, o.Op, SUM(v.Valor),
           CASE WHEN o.Op = 'NIVEL' THEN SUM(v.Valor) END
    FROM #valores AS v
    INNER JOIN #filas AS f ON f.IFIID = v.IFIID AND f.CuadroFilaID = v.CuadroFilaID
    CROSS APPLY (SELECT CASE v.Rol WHEN 'N' THEN f.OpN ELSE f.OpD END AS Op) AS o
    GROUP BY v.IFIID, v.CuadroFilaID, v.Rol, v.FechaID, o.Op;

    IF EXISTS (SELECT 1 FROM #filas WHERE OpN <> 'NIVEL' OR OpD <> 'NIVEL')
        EXEC dbo.AplicarOperacionesTiempo;

    -- SUMA = N × escala; RATIO = N / D × escala. Sin N, o RATIO sin D (falta historia), el mes no se devuelve;
    -- D = 0 → NULL, como antes.
    SELECT n.IFIID,
           n.CuadroFilaID,
           CONVERT(char(7), DATEADD(day, n.FechaID, '19500101'), 126) AS Periodo,
           CASE f.Calculo
               WHEN 'SUMA'  THEN n.V * f.Escala
               WHEN 'RATIO' THEN n.V / NULLIF(d.V, 0) * f.Escala
           END                                                         AS Valor
    FROM #base AS n
    INNER JOIN #filas AS f ON f.IFIID = n.IFIID AND f.CuadroFilaID = n.CuadroFilaID
    LEFT JOIN #base AS d ON d.IFIID = n.IFIID AND d.CuadroFilaID = n.CuadroFilaID AND d.Rol = 'D' AND d.FechaID = n.FechaID
    WHERE n.Rol = 'N' AND n.V IS NOT NULL
      AND (f.Calculo = 'SUMA' OR d.V IS NOT NULL);
END
GO

/* -------------------------------------------------------------------------------------
   api.ObtenerValoresCuadroSector — valores SUMA/RATIO de un cuadro de sector, sobre la
   vista indexada api.vSaldoAgregado (solo términos de cuenta: un indicador no se suma).
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ObtenerValoresCuadroSector
    @CuadroID     varchar(16),
    @TiposEntidad varchar(100),
    @Segmentos    varchar(100) = NULL,
    @IdSub        nvarchar(80) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Cuadro WHERE CuadroID = @CuadroID AND Ambito = 'SECTOR' AND Activo = 1)
        THROW 50022, N'El cuadro no existe en el catálogo como cuadro de sector.', 1;
    IF @TiposEntidad IS NULL OR LTRIM(RTRIM(@TiposEntidad)) = ''
        THROW 50012, N'@TiposEntidad es obligatorio.', 1;

    DECLARE @t TABLE (TipoEntidad varchar(8) PRIMARY KEY);
    INSERT INTO @t SELECT DISTINCT UPPER(LTRIM(RTRIM(value))) FROM STRING_SPLIT(@TiposEntidad, ',') WHERE LTRIM(RTRIM(value)) <> '';

    DECLARE @s TABLE (Segmento smallint PRIMARY KEY);
    INSERT INTO @s SELECT DISTINCT TRY_CONVERT(smallint, LTRIM(RTRIM(value))) FROM STRING_SPLIT(@Segmentos, ',')
    WHERE TRY_CONVERT(smallint, LTRIM(RTRIM(value))) IS NOT NULL;

    DECLARE @todosSegmentos bit = CASE WHEN @Segmentos IS NULL THEN 1 ELSE 0 END;
    DECLARE @clave varchar(20) =
        (SELECT STRING_AGG(TipoEntidad, ',') WITHIN GROUP (ORDER BY TipoEntidad) FROM @t)
        + ISNULL(':' + (SELECT STRING_AGG(CAST(Segmento AS varchar(3)), ',') WITHIN GROUP (ORDER BY Segmento) FROM @s), '');
    DECLARE @v varchar(20) = api.fnResolverVariante(@CuadroID, @clave);

    CREATE TABLE #filas
    (
        CuadroFilaID int         NOT NULL PRIMARY KEY,
        Calculo      varchar(8)  NOT NULL,
        Escala       float       NOT NULL,
        Orden        int         NOT NULL,
        OpN          varchar(16) NOT NULL,
        OpD          varchar(16) NOT NULL
    );
    INSERT INTO #filas (CuadroFilaID, Calculo, Escala, Orden, OpN, OpD)
    SELECT f.CuadroFilaID, f.Calculo, f.Escala, f.Orden, ISNULL(f.OperacionN, 'NIVEL'), ISNULL(f.OperacionD, 'NIVEL')
    FROM dbo.CuadroFila AS f
    WHERE f.CuadroID = @CuadroID AND f.Variante = @v
      AND f.Calculo IN ('SUMA', 'RATIO')
      AND (@IdSub IS NULL OR f.IdSub = @IdSub);

    -- S por rol y mes (IFIID = 0: el sector es una sola unidad).
    CREATE TABLE #base
    (
        IFIID        int         NOT NULL,
        CuadroFilaID int         NOT NULL,
        Rol          char(1)     NOT NULL,
        FechaID      int         NOT NULL,
        Op           varchar(16) NOT NULL,
        S            float       NOT NULL,
        V            float       NULL,
        PRIMARY KEY (IFIID, CuadroFilaID, Rol, FechaID)
    );
    INSERT INTO #base (IFIID, CuadroFilaID, Rol, FechaID, Op, S, V)
    SELECT 0, fi.CuadroFilaID, t.Rol, a.FechaID, o.Op, SUM(CAST(a.Saldo AS float) * t.Factor),
           CASE WHEN o.Op = 'NIVEL' THEN SUM(CAST(a.Saldo AS float) * t.Factor) END
    FROM #filas AS fi
    INNER JOIN dbo.CuadroFilaTermino AS t ON t.CuadroFilaID = fi.CuadroFilaID AND t.Fuente = 'CUENTA'
    INNER JOIN api.vSaldoAgregado AS a WITH (NOEXPAND) ON a.CuentaID = t.CuentaID
    INNER JOIN @t AS tt ON tt.TipoEntidad = a.TipoEntidad
    CROSS APPLY (SELECT CASE t.Rol WHEN 'N' THEN fi.OpN ELSE fi.OpD END AS Op) AS o
    WHERE @todosSegmentos = 1 OR a.Segmento IN (SELECT Segmento FROM @s)
    GROUP BY fi.CuadroFilaID, t.Rol, a.FechaID, o.Op
    OPTION (RECOMPILE);

    IF EXISTS (SELECT 1 FROM #filas WHERE OpN <> 'NIVEL' OR OpD <> 'NIVEL')
        EXEC dbo.AplicarOperacionesTiempo;

    SELECT n.CuadroFilaID,
           CONVERT(char(7), DATEADD(day, n.FechaID, '19500101'), 126) AS Periodo,
           CASE f.Calculo
               WHEN 'SUMA'  THEN n.V * f.Escala
               WHEN 'RATIO' THEN n.V / NULLIF(d.V, 0) * f.Escala
           END                                                         AS Valor
    FROM #base AS n
    INNER JOIN #filas AS f ON f.CuadroFilaID = n.CuadroFilaID
    LEFT JOIN #base AS d ON d.IFIID = 0 AND d.CuadroFilaID = n.CuadroFilaID AND d.Rol = 'D' AND d.FechaID = n.FechaID
    WHERE n.Rol = 'N' AND n.V IS NOT NULL
      AND (f.Calculo = 'SUMA' OR d.V IS NOT NULL)
    ORDER BY f.Orden, n.FechaID;
END
GO

PRINT N'03_operaciones_tiempo: OK';
GO
