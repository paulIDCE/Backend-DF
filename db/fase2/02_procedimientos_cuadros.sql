/* =====================================================================================
   Fase 2 · 02 — Procedimientos de lectura del catálogo de cuadros

   Plantilla (filas en orden, con su texto) y valores calculados (SUMA / RATIO, ya
   escalados a las unidades publicadas). Las filas Calculo = 'JSON' vienen en la plantilla
   pero sin valores: en la transición el backend las completa con el JSON.

   Para el backend (dos resultsets: plantilla y valores):
     api.ObtenerCuadroEntidad   @CuadroID, @IFIID, @IdSub = NULL
     api.ObtenerCuadroSector    @CuadroID, @TiposEntidad, @Segmentos = NULL, @IdSub = NULL
   Piezas (un resultset; también usadas por las pruebas):
     api.ListarCuadros
     api.ObtenerPlantilla              @CuadroID, @Variante = NULL, @IdSub = NULL
     api.ObtenerValoresCuadroEntidades @CuadroID, @IFIIDs, @IdSub = NULL   (carga masiva: REP01/rankings)
     api.ObtenerValoresCuadroSector    @CuadroID, @TiposEntidad, @Segmentos = NULL, @IdSub = NULL

   Convenciones de Fase 0: Periodo 'yyyy-MM'; listas separadas por comas; un mes sin datos
   de origen no se devuelve (sin valor ≠ 0). Idempotente.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -------------------------------------------------------------------------------------
   api.fnResolverVariante — variante de plantilla a usar: exacta, solo tipos, '*'.
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER FUNCTION api.fnResolverVariante (@CuadroID varchar(16), @Clave varchar(20))
RETURNS varchar(20)
AS
BEGIN
    IF @Clave IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.CuadroFila WHERE CuadroID = @CuadroID AND Variante = @Clave)
        RETURN @Clave;

    DECLARE @base varchar(20) = LEFT(@Clave, CHARINDEX(':', @Clave + ':') - 1);
    IF @base IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.CuadroFila WHERE CuadroID = @CuadroID AND Variante = @base)
        RETURN @base;

    RETURN '*';
END
GO

/* -------------------------------------------------------------------------------------
   api.fnFilasCuadro — filas de una plantilla con su texto resuelto:
   filas de cuenta → dbo.Cuenta.Nombre (en balances "1101.    Nombre"), salvo nombre vacío
   o genérico ('cta …') o cuadro con UsaNombreCuenta = 0 → CuadroFila.Etiqueta.
   ------------------------------------------------------------------------------------- */
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
        f.Escala
    FROM dbo.CuadroFila AS f
    INNER JOIN dbo.Cuadro AS c ON c.CuadroID = f.CuadroID
    OUTER APPLY (SELECT NULLIF(LTRIM(RTRIM(cu.Nombre)), '') AS Nombre
                 FROM dbo.Cuenta AS cu
                 WHERE cu.CuentaID = f.CuentaID) AS n
    WHERE f.CuadroID = @CuadroID
      AND f.Variante = @Variante;
GO

/* -------------------------------------------------------------------------------------
   api.ListarCuadros — metadatos de los cuadros activos y sus variantes de plantilla.
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ListarCuadros
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.CuadroID, c.Titulo, c.Unidad, c.Ambito, c.Tipo, c.Frecuencia, c.FechasJson, c.Orden,
           (SELECT STRING_AGG(v.Variante, '|') WITHIN GROUP (ORDER BY v.Variante)
            FROM (SELECT DISTINCT Variante FROM dbo.CuadroFila WHERE CuadroID = c.CuadroID) AS v) AS Variantes
    FROM dbo.Cuadro AS c
    WHERE c.Activo = 1
    ORDER BY c.Orden;
END
GO

/* -------------------------------------------------------------------------------------
   api.ObtenerPlantilla — filas de un cuadro (sin valores), en orden.
   @Variante NULL = '*'. Admite 'BP', 'COOP:2'… y resuelve con api.fnResolverVariante.
   ------------------------------------------------------------------------------------- */
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

    SELECT CuadroFilaID, Variante, IdSub, Orden, Codigo, CodigoBase, Grupo, Variable, Nivel, SegmentoCredito, Unidad, Calculo
    FROM api.fnFilasCuadro(@CuadroID, @v)
    WHERE @IdSub IS NULL OR IdSub = @IdSub
    ORDER BY Orden;
END
GO

/* -------------------------------------------------------------------------------------
   api.ObtenerValoresCuadroEntidades — valores SUMA/RATIO de un cuadro de entidad para
   una o varias entidades (cada una con la plantilla de su tipo). Fuentes: B11 e IndicadorData.
   Usa tablas temporales indexadas: con CTEs el join final se reevaluaba y el costo crecía
   más que linealmente (229 entidades de REP01: > 60 s → ≈ 7 s). Filas sin orden garantizado.
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
        IFIID        int        NOT NULL,
        CuadroFilaID int        NOT NULL,
        Calculo      varchar(8) NOT NULL,
        Escala       float      NOT NULL,
        PRIMARY KEY (IFIID, CuadroFilaID)
    );
    INSERT INTO #filas (IFIID, CuadroFilaID, Calculo, Escala)
    SELECT i.IFIID, f.CuadroFilaID, f.Calculo, f.Escala
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

    -- SUMA = ΣN × escala; RATIO = ΣN / ΣD × escala (sin NULL dentro de los SUM).
    SELECT p.IFIID,
           p.CuadroFilaID,
           CONVERT(char(7), DATEADD(day, p.FechaID, '19500101'), 126) AS Periodo,
           CASE f.Calculo
               WHEN 'SUMA'  THEN p.N * f.Escala
               WHEN 'RATIO' THEN CASE WHEN p.HayD > 0 THEN p.N / NULLIF(p.D, 0) * f.Escala END
           END                                                         AS Valor
    FROM (SELECT IFIID, CuadroFilaID, FechaID,
                 SUM(CASE WHEN Rol = 'N' THEN Valor ELSE 0 END) AS N,
                 SUM(CASE WHEN Rol = 'N' THEN 1 ELSE 0 END)     AS HayN,
                 SUM(CASE WHEN Rol = 'D' THEN Valor ELSE 0 END) AS D,
                 SUM(CASE WHEN Rol = 'D' THEN 1 ELSE 0 END)     AS HayD
          FROM #valores
          GROUP BY IFIID, CuadroFilaID, FechaID) AS p
    INNER JOIN #filas AS f ON f.IFIID = p.IFIID AND f.CuadroFilaID = p.CuadroFilaID
    WHERE p.HayN > 0;
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

    WITH filas AS (
        SELECT f.CuadroFilaID, f.Calculo, f.Escala, f.Orden
        FROM dbo.CuadroFila AS f
        WHERE f.CuadroID = @CuadroID AND f.Variante = @v
          AND f.Calculo IN ('SUMA', 'RATIO')
          AND (@IdSub IS NULL OR f.IdSub = @IdSub)
    ),
    valores AS (
        SELECT t.CuadroFilaID, t.Rol, a.FechaID, CAST(a.Saldo AS float) * t.Factor AS Valor
        FROM filas AS fi
        INNER JOIN dbo.CuadroFilaTermino AS t ON t.CuadroFilaID = fi.CuadroFilaID AND t.Fuente = 'CUENTA'
        INNER JOIN api.vSaldoAgregado AS a WITH (NOEXPAND) ON a.CuentaID = t.CuentaID
        INNER JOIN @t AS tt ON tt.TipoEntidad = a.TipoEntidad
        WHERE @todosSegmentos = 1 OR a.Segmento IN (SELECT Segmento FROM @s)
    ),
    partes AS (
        SELECT CuadroFilaID, FechaID,
               SUM(CASE WHEN Rol = 'N' THEN Valor ELSE 0 END) AS N,
               SUM(CASE WHEN Rol = 'N' THEN 1 ELSE 0 END)     AS HayN,
               SUM(CASE WHEN Rol = 'D' THEN Valor ELSE 0 END) AS D,
               SUM(CASE WHEN Rol = 'D' THEN 1 ELSE 0 END)     AS HayD
        FROM valores
        GROUP BY CuadroFilaID, FechaID
    )
    SELECT p.CuadroFilaID,
           CONVERT(char(7), DATEADD(day, p.FechaID, '19500101'), 126) AS Periodo,
           CASE f.Calculo
               WHEN 'SUMA'  THEN p.N * f.Escala
               WHEN 'RATIO' THEN CASE WHEN p.HayD > 0 THEN p.N / NULLIF(p.D, 0) * f.Escala END
           END                                                         AS Valor
    FROM partes AS p
    INNER JOIN filas AS f ON f.CuadroFilaID = p.CuadroFilaID
    WHERE p.HayN > 0
    ORDER BY f.Orden, p.FechaID
    OPTION (RECOMPILE);
END
GO

/* -------------------------------------------------------------------------------------
   api.ObtenerCuadroEntidad — RS1 plantilla (variante = tipo de la entidad) + RS2 valores.
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ObtenerCuadroEntidad
    @CuadroID varchar(16),
    @IFIID    int,
    @IdSub    nvarchar(80) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @IFIID IS NULL
        THROW 50010, N'@IFIID es obligatorio.', 1;

    DECLARE @tipo varchar(8) = (SELECT TipoEntidad FROM dbo.Ifi WHERE IFIID = @IFIID);
    DECLARE @ids varchar(12) = CAST(@IFIID AS varchar(12));

    EXEC api.ObtenerPlantilla @CuadroID = @CuadroID, @Variante = @tipo, @IdSub = @IdSub;
    EXEC api.ObtenerValoresCuadroEntidades @CuadroID = @CuadroID, @IFIIDs = @ids, @IdSub = @IdSub;
END
GO

/* -------------------------------------------------------------------------------------
   api.ObtenerCuadroSector — RS1 plantilla (variante = tipos[:segmentos]) + RS2 valores.
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ObtenerCuadroSector
    @CuadroID     varchar(16),
    @TiposEntidad varchar(100),
    @Segmentos    varchar(100) = NULL,
    @IdSub        nvarchar(80) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @t TABLE (TipoEntidad varchar(8) PRIMARY KEY);
    INSERT INTO @t SELECT DISTINCT UPPER(LTRIM(RTRIM(value))) FROM STRING_SPLIT(@TiposEntidad, ',') WHERE LTRIM(RTRIM(value)) <> '';
    DECLARE @s TABLE (Segmento smallint PRIMARY KEY);
    INSERT INTO @s SELECT DISTINCT TRY_CONVERT(smallint, LTRIM(RTRIM(value))) FROM STRING_SPLIT(@Segmentos, ',')
    WHERE TRY_CONVERT(smallint, LTRIM(RTRIM(value))) IS NOT NULL;
    DECLARE @clave varchar(20) =
        (SELECT STRING_AGG(TipoEntidad, ',') WITHIN GROUP (ORDER BY TipoEntidad) FROM @t)
        + ISNULL(':' + (SELECT STRING_AGG(CAST(Segmento AS varchar(3)), ',') WITHIN GROUP (ORDER BY Segmento) FROM @s), '');

    EXEC api.ObtenerPlantilla @CuadroID = @CuadroID, @Variante = @clave, @IdSub = @IdSub;
    EXEC api.ObtenerValoresCuadroSector @CuadroID = @CuadroID, @TiposEntidad = @TiposEntidad, @Segmentos = @Segmentos, @IdSub = @IdSub;
END
GO

PRINT N'02_procedimientos_cuadros: OK';
GO
