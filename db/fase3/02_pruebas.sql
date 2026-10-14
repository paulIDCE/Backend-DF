/* =====================================================================================
   Fase 3 · 02 — Pruebas de las fórmulas SUMA/RATIO (solo lectura)
   Valores esperados = JSON publicados (análisis vertical publicado con 2 decimales).
   ===================================================================================== */
USE idce_bco_coop;
GO
SET NOCOUNT ON;

DECLARE @fallas int = 0, @n int;
DECLARE @AV nvarchar(80) = N'Análisis Vertical (%)';

/* ---------- 1. Catálogo ---------- */
SELECT @n = COUNT(*) FROM dbo.CuadroFila
WHERE CuadroID IN ('EFI06', 'SFN06') AND IdSub = @AV AND Calculo = 'RATIO';
IF @n = 6991 PRINT N'OK    1 Análisis vertical: 6 991 filas de EFI06/SFN06 pasan a RATIO';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 1 Análisis vertical: ', @n, N' filas RATIO (se esperaban 6 991)'); END

SELECT @n = COUNT(*) FROM dbo.CuadroFila f
WHERE f.CuadroID IN ('EFI06', 'SFN06') AND f.IdSub = @AV
  AND (SELECT COUNT(*) FROM dbo.CuadroFilaTermino t WHERE t.CuadroFilaID = f.CuadroFilaID) <> 2;
IF @n = 0 PRINT N'OK    2 Cada fila del análisis vertical tiene un numerador y un denominador';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 2 Filas del análisis vertical con términos incorrectos: ', @n); END

SELECT @n = COUNT(*) FROM dbo.CuadroFila
WHERE (CuadroID = 'EFI07' AND Codigo IN ('IF011', 'IF007_5', 'IF008_5', 'IF011_5'))
   OR (CuadroID = 'EFI13' AND Codigo IN ('E1_Cartera', 'E3_Inversiones', 'E4_INNF', 'E6_Cre_Exte'));
IF @n > 0 AND NOT EXISTS (SELECT 1 FROM dbo.CuadroFila
                          WHERE ((CuadroID = 'EFI07' AND Codigo IN ('IF011', 'IF007_5', 'IF008_5', 'IF011_5'))
                              OR (CuadroID = 'EFI13' AND Codigo IN ('E1_Cartera', 'E3_Inversiones', 'E4_INNF', 'E6_Cre_Exte')))
                            AND Calculo = 'JSON')
    PRINT N'OK    3 Fórmulas de EFI07 y EFI13 cargadas (ninguna queda como JSON)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 3 Fórmulas de EFI07/EFI13'; END

/* ---------- 2. Valores de entidad (BP. PICHINCHA = 260) ---------- */
DECLARE @ve TABLE (IFIID int, CuadroFilaID int, Periodo char(7), Valor float);

INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI06', @IFIIDs = '260', @IdSub = @AV;
IF EXISTS (SELECT 1 FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.CuentaID = 11 AND v.Periodo = '2026-06' AND ABS(v.Valor - 7.19) < 0.005)
AND EXISTS (SELECT 1 FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.CuentaID = 1 AND v.Periodo = '2026-06' AND ABS(v.Valor - 100) < 1e-9)
    PRINT N'OK    4 EFI06 Pichincha, análisis vertical: fondos disponibles 7,19 % y activo 100 % (= JSON)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 4 EFI06 análisis vertical Pichincha'; END

DELETE FROM @ve;
INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI07', @IFIIDs = '260', @IdSub = N'Cartera Total';
IF EXISTS (SELECT 1 FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.Codigo = 'IF011' AND v.Periodo = '2026-06' AND ABS(v.Valor - 14774.5639) < 1e-3)
    PRINT N'OK    5 EFI07 Pichincha: cartera bruta 14 774,5639 (= JSON)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 5 EFI07 cartera bruta Pichincha'; END

DELETE FROM @ve;
INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI13', @IFIIDs = '260';
IF EXISTS (SELECT 1 FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.Codigo = 'E1_Cartera' AND v.Periodo = '2026-06' AND ABS(v.Valor - 65.5406) < 1e-3)
    PRINT N'OK    6 EFI13 Pichincha: E1 cartera neta 65,5406 % (= JSON)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 6 EFI13 E1 Pichincha'; END

/* ---------- 3. Valores de sector ---------- */
DECLARE @vs TABLE (CuadroFilaID int, Periodo char(7), Valor float);
INSERT INTO @vs EXEC api.ObtenerValoresCuadroSector @CuadroID = 'SFN06', @TiposEntidad = 'BP', @Segmentos = '1', @IdSub = @AV;
IF EXISTS (SELECT 1 FROM @vs v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.CuentaID = 11 AND v.Periodo = '2026-06' AND ABS(v.Valor - 10.03) < 0.005)
    PRINT N'OK    7 SFN06 Bancos Grandes, análisis vertical: fondos disponibles 10,03 % (= JSON)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 7 SFN06 análisis vertical Bancos Grandes'; END

DELETE FROM @vs;
INSERT INTO @vs EXEC api.ObtenerValoresCuadroSector @CuadroID = 'CAR01', @TiposEntidad = 'BP', @Segmentos = '1', @IdSub = N'Cartera Total';
IF EXISTS (SELECT 1 FROM @vs v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.Codigo = 'IF011' AND v.Periodo = '2026-06' AND ABS(v.Valor - 35743.2167) < 1e-3)
    PRINT N'OK    8 CAR01 Bancos Grandes: cartera bruta 35 743,2167 (= JSON)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 8 CAR01 cartera bruta Bancos Grandes'; END

/* ---------- Resumen ---------- */
IF @fallas = 0
    PRINT N'==> Todas las pruebas pasaron.';
ELSE
    THROW 50099, N'Fallaron pruebas de la Fase 3 (ver mensajes).', 1;
GO
