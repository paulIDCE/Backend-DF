/* =====================================================================================
   Fase 0 · 03 — Procedimientos de lectura para la capa repository del backend

   Convenciones comunes:
   - Solo lectura. Sin prefijo sp_ (reservado para procedimientos del sistema).
   - Periodo se devuelve como CHAR(7) 'yyyy-MM' (formato de los JSON y del backend).
   - Montos en USD tal como están en B11 (money). La escala a millones la hace el backend.
   - Listas como texto separado por comas ('1,11,1101'). Se aceptan cuentas con o sin '@'
     ('@1101' = '1101'). Valores no numéricos se ignoran. NULL = sin filtro.
   - Una entidad/cuenta inexistente no es error: devuelve 0 filas.
   - Ausencia de fila ≠ saldo 0 (regla R2/R3 del backend): no se rellenan huecos.
   Idempotente (CREATE OR ALTER).
   ===================================================================================== */
USE idce_bco_coop;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -------------------------------------------------------------------------------------
   api.ObtenerVentana — ventana de datos (C1) y firma de la carga actual.
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ObtenerVentana
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Desde, Hasta, DesdeFechaID, HastaFechaID, Meses, MesesConDatos, FilasB11, Firma
    FROM api.vVentana;
END
GO

/* -------------------------------------------------------------------------------------
   api.ListarIfi — entidades de la BD, para validar el mapa slug → IFIID del backend.
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ListarIfi
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IFIID, Ruc, Nombre, TipoEntidad, Segmento
    FROM dbo.Ifi
    ORDER BY IFIID;
END
GO

/* -------------------------------------------------------------------------------------
   api.ListarCuentas — catálogo de cuentas con nombre (diagnóstico / uso futuro).
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ListarCuentas
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CuentaID AS int) AS CuentaID, Nombre
    FROM dbo.Cuenta
    ORDER BY CAST(CuentaID AS varchar(12));   -- orden del plan de cuentas: 1, 11, 1101, 110105…
END
GO

/* -------------------------------------------------------------------------------------
   api.ObtenerSaldosEntidad — saldos de UNA entidad en toda la ventana.
   @Cuentas NULL = todas (~1 532 cuentas × meses ≈ 49 mil filas).
   Usa IX_B11_IFIID_Cuenta_Fecha.
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ObtenerSaldosEntidad
    @IFIID   int,
    @Cuentas varchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @IFIID IS NULL
        THROW 50010, N'@IFIID es obligatorio.', 1;

    IF @Cuentas IS NULL
    BEGIN
        SELECT CAST(b.CuentaID AS varchar(12))                            AS Cuenta,
               CONVERT(char(7), DATEADD(day, b.FechaID, '19500101'), 126)  AS Periodo,
               b.Saldo
        FROM dbo.B11 AS b
        WHERE b.IFIID = @IFIID
        ORDER BY b.CuentaID, b.FechaID;
        RETURN;
    END

    DECLARE @c TABLE (CuentaID int PRIMARY KEY);
    INSERT INTO @c (CuentaID)
    SELECT DISTINCT TRY_CONVERT(int, REPLACE(LTRIM(RTRIM(value)), '@', ''))
    FROM STRING_SPLIT(@Cuentas, ',')
    WHERE TRY_CONVERT(int, REPLACE(LTRIM(RTRIM(value)), '@', '')) IS NOT NULL;

    SELECT CAST(b.CuentaID AS varchar(12))                            AS Cuenta,
           CONVERT(char(7), DATEADD(day, b.FechaID, '19500101'), 126)  AS Periodo,
           b.Saldo
    FROM @c AS c
    INNER JOIN dbo.B11 AS b ON b.IFIID = @IFIID AND b.CuentaID = c.CuentaID
    ORDER BY b.CuentaID, b.FechaID;
END
GO

/* -------------------------------------------------------------------------------------
   api.ObtenerSaldosEntidades — carga masiva para el índice de reportes y los rankings.
   @IFIIDs obligatorio. @Cuentas recomendado (NULL = todas: ≈ 49 mil filas por entidad).
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ObtenerSaldosEntidades
    @IFIIDs  varchar(max),
    @Cuentas varchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @IFIIDs IS NULL OR LTRIM(RTRIM(@IFIIDs)) = ''
        THROW 50011, N'@IFIIDs es obligatorio.', 1;

    DECLARE @i TABLE (IFIID int PRIMARY KEY);
    INSERT INTO @i (IFIID)
    SELECT DISTINCT TRY_CONVERT(int, LTRIM(RTRIM(value)))
    FROM STRING_SPLIT(@IFIIDs, ',')
    WHERE TRY_CONVERT(int, LTRIM(RTRIM(value))) IS NOT NULL;

    IF @Cuentas IS NULL
    BEGIN
        SELECT b.IFIID,
               CAST(b.CuentaID AS varchar(12))                            AS Cuenta,
               CONVERT(char(7), DATEADD(day, b.FechaID, '19500101'), 126)  AS Periodo,
               b.Saldo
        FROM @i AS i
        INNER JOIN dbo.B11 AS b ON b.IFIID = i.IFIID
        ORDER BY b.IFIID, b.CuentaID, b.FechaID;
        RETURN;
    END

    DECLARE @c TABLE (CuentaID int PRIMARY KEY);
    INSERT INTO @c (CuentaID)
    SELECT DISTINCT TRY_CONVERT(int, REPLACE(LTRIM(RTRIM(value)), '@', ''))
    FROM STRING_SPLIT(@Cuentas, ',')
    WHERE TRY_CONVERT(int, REPLACE(LTRIM(RTRIM(value)), '@', '')) IS NOT NULL;

    SELECT b.IFIID,
           CAST(b.CuentaID AS varchar(12))                            AS Cuenta,
           CONVERT(char(7), DATEADD(day, b.FechaID, '19500101'), 126)  AS Periodo,
           b.Saldo
    FROM @i AS i
    CROSS JOIN @c AS c
    INNER JOIN dbo.B11 AS b ON b.IFIID = i.IFIID AND b.CuentaID = c.CuentaID
    ORDER BY b.IFIID, b.CuentaID, b.FechaID
    OPTION (RECOMPILE);   -- cardinalidad real de las variables de tabla
END
GO

/* -------------------------------------------------------------------------------------
   api.ObtenerSaldosAgregado — saldos sumados de un sector, desde la vista indexada.
   @TiposEntidad: 'BP', 'COOP', 'MU', 'PU' (uno o varios, separados por coma).
   @Segmentos:    NULL = todos; si no, p. ej. '1' o '1,2,3' (Ifi.Segmento, convención C3).
   Entidades = número de entidades que aportan a esa cuenta y mes.
   El backend traduce el sector del frontend a tipos/segmentos (la BD no conoce esos nombres).
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ObtenerSaldosAgregado
    @TiposEntidad varchar(100),
    @Segmentos    varchar(100) = NULL,
    @Cuentas      varchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @TiposEntidad IS NULL OR LTRIM(RTRIM(@TiposEntidad)) = ''
        THROW 50012, N'@TiposEntidad es obligatorio.', 1;

    DECLARE @t TABLE (TipoEntidad varchar(8) PRIMARY KEY);
    INSERT INTO @t (TipoEntidad)
    SELECT DISTINCT UPPER(LTRIM(RTRIM(value)))
    FROM STRING_SPLIT(@TiposEntidad, ',')
    WHERE LTRIM(RTRIM(value)) <> '';

    DECLARE @s TABLE (Segmento smallint PRIMARY KEY);
    INSERT INTO @s (Segmento)
    SELECT DISTINCT TRY_CONVERT(smallint, LTRIM(RTRIM(value)))
    FROM STRING_SPLIT(@Segmentos, ',')               -- STRING_SPLIT(NULL) no devuelve filas
    WHERE TRY_CONVERT(smallint, LTRIM(RTRIM(value))) IS NOT NULL;

    DECLARE @c TABLE (CuentaID int PRIMARY KEY);
    INSERT INTO @c (CuentaID)
    SELECT DISTINCT TRY_CONVERT(int, REPLACE(LTRIM(RTRIM(value)), '@', ''))
    FROM STRING_SPLIT(@Cuentas, ',')
    WHERE TRY_CONVERT(int, REPLACE(LTRIM(RTRIM(value)), '@', '')) IS NOT NULL;

    DECLARE @todosSegmentos bit = CASE WHEN @Segmentos IS NULL THEN 1 ELSE 0 END;
    DECLARE @todasCuentas   bit = CASE WHEN @Cuentas   IS NULL THEN 1 ELSE 0 END;

    SELECT CAST(v.CuentaID AS varchar(12))                            AS Cuenta,
           CONVERT(char(7), DATEADD(day, v.FechaID, '19500101'), 126)  AS Periodo,
           SUM(v.Saldo)                                               AS Saldo,
           SUM(v.Filas)                                               AS Entidades
    FROM api.vSaldoAgregado AS v WITH (NOEXPAND)
    INNER JOIN @t AS t ON t.TipoEntidad = v.TipoEntidad
    WHERE (@todosSegmentos = 1 OR v.Segmento IN (SELECT Segmento FROM @s))
      AND (@todasCuentas   = 1 OR v.CuentaID IN (SELECT CuentaID FROM @c))
    GROUP BY v.CuentaID, v.FechaID
    ORDER BY v.CuentaID, v.FechaID
    OPTION (RECOMPILE);
END
GO

/* -------------------------------------------------------------------------------------
   api.ObtenerIndicadoresEntidad — IndicadorData de una entidad (PTC, SOLVENCIA, …).
   Valores tal como están en la BD (SOLVENCIA es fracción; el backend aplica la escala).
   ------------------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE api.ObtenerIndicadoresEntidad
    @IFIID   int,
    @Codigos varchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @IFIID IS NULL
        THROW 50013, N'@IFIID es obligatorio.', 1;

    DECLARE @k TABLE (Codigo varchar(32) PRIMARY KEY);
    INSERT INTO @k (Codigo)
    SELECT DISTINCT LTRIM(RTRIM(value))
    FROM STRING_SPLIT(@Codigos, ',')
    WHERE LTRIM(RTRIM(value)) <> '';

    SELECT f.Codigo,
           CONVERT(char(7), DATEADD(day, d.FechaID, '19500101'), 126)  AS Periodo,
           d.Valor
    FROM dbo.IndicadorData AS d
    INNER JOIN dbo.IndicadorFin AS f ON f.IndicadorFinID = d.IndicadorFinID
    WHERE d.IFIID = @IFIID
      AND (@Codigos IS NULL OR f.Codigo IN (SELECT Codigo FROM @k))
    ORDER BY f.Codigo, d.FechaID;
END
GO

PRINT N'03_procedimientos: OK';
GO
