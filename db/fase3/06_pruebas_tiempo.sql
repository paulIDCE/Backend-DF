/* =====================================================================================
   Fase 3 · 06 — Pruebas de las operaciones en el tiempo (solo lectura)
   Requiere 03_operaciones_tiempo.sql y 04_formulas_tiempo.sql. Valores esperados = JSON
   publicados (análisis horizontal y EFI04 con 2 decimales; el resto con 4).
   Entidades: BP. PICHINCHA = 260, BP. AMAZONAS = 184.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET NOCOUNT ON;

DECLARE @fallas int = 0, @n int, @x float, @y float;
DECLARE @AH nvarchar(80) = N'Análisis Horizontal (%)';
DECLARE @ve TABLE (IFIID int, CuadroFilaID int, Periodo char(7), Valor float);
DECLARE @vs TABLE (CuadroFilaID int, Periodo char(7), Valor float);

/* ---------- 1. Catálogo ---------- */
SELECT @n = COUNT(*) FROM dbo.CuadroFila
WHERE CuadroID IN ('EFI06', 'SFN06') AND IdSub = @AH AND Calculo = 'RATIO'
  AND OperacionN = 'VARIACION_12' AND OperacionD = 'RETARDO_12';
IF @n = 6991 PRINT N'OK    1 Análisis horizontal: 6 991 filas (2 776 EFI06 + 4 215 SFN06) pasan a RATIO en el tiempo';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 1 Análisis horizontal: ', @n, N' filas (se esperaban 6 991)'); END

SELECT @n = COUNT(*) FROM dbo.CuadroFila WHERE CuadroID IN ('EFI06', 'SFN06') AND IdSub = @AH AND Calculo = 'JSON';
IF @n = 0 PRINT N'OK    2 Ninguna fila del análisis horizontal queda como JSON';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 2 Filas del análisis horizontal que siguen en JSON: ', @n); END

SELECT @n = COUNT(*) FROM dbo.CuadroFila
WHERE Calculo IN ('SUMA', 'RATIO') AND NOT EXISTS (SELECT 1 FROM dbo.CuadroFilaTermino t WHERE t.CuadroFilaID = CuadroFila.CuadroFilaID);
IF @n = 0 PRINT N'OK    3 Toda fila calculada tiene términos';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 3 Filas calculadas sin términos: ', @n); END

/* ---------- 2. Valores de entidad ---------- */
INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI06', @IFIIDs = '260', @IdSub = @AH;
SELECT @x = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.CuentaID = 1 AND v.Periodo = '2026-06';
SELECT @n = COUNT(*) FROM @ve WHERE Periodo < '2025-01';
IF ABS(@x - 7.22) < 0.005 AND @n = 0
    PRINT CONCAT(N'OK    4 Horizontal Pichincha cuenta 1, 2026-06 = ', ROUND(@x, 4), N' (JSON 7,22); 2024 sin valor');
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 4 Horizontal Pichincha: ', @x, N'; valores de 2024: ', @n); END

DELETE FROM @ve;
INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI03', @IFIIDs = '184';
SELECT @x = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = '@5A' AND v.Periodo = '2026-07';
SELECT @y = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = '@5A' AND v.Periodo = '2025-01';
IF ABS(@x - 50.1663) < 0.00005 AND ABS(@y - 44.5582) < 0.00005
    PRINT N'OK    5 EFI03 @5A BP Amazonas: 2026-07 = 50,1663 y 2025-01 = 44,5582 (anualizado)';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 5 EFI03 @5A: ', @x, N' / ', @y); END

DELETE FROM @ve;
INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI04', @IFIIDs = '260';
SELECT @x = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
WHERE f.Etiqueta = N'ACTIVO' AND REPLACE(UPPER(f.Grupo), N'Ó', N'O') = N'VARIACION MENSUAL' AND v.Periodo = '2026-06';
SELECT @n = COUNT(*) FROM @ve WHERE Periodo = '2024-01';
IF ABS(@x - (-341.04)) < 0.005 AND @n = 0
    PRINT N'OK    6 EFI04 variación mensual ACTIVO Pichincha 2026-06 = −341,04; 2024-01 sin valor';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 6 EFI04 ACTIVO: ', @x, N'; valores de 2024-01: ', @n); END

DELETE FROM @ve;
INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI07', @IFIIDs = '260', @IdSub = N'Cartera Total';
SELECT @x = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = 'tasa_IF011' AND v.Periodo = '2026-06';
IF ABS(@x - 7.2768) < 0.00005
    PRINT N'OK    7 EFI07 tasa_IF011 Pichincha 2026-06 = 7,2768';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 7 tasa_IF011: ', @x); END

DELETE FROM @ve;
INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI11', @IFIIDs = '260';
SELECT @x = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = 'IF002' AND v.Periodo = '2026-06';
SELECT @y = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = 'IF002' AND v.Periodo = '2025-12';
IF ABS(@x - 1.146) < 0.0005 AND ABS(@y - 1.1164) < 0.00005
    PRINT N'OK    8 ROA Pichincha: 2026-06 = 1,146 y 2025-12 = 1,1164 (diciembre con el activo del mes)';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 8 ROA: ', @x, N' / ', @y); END

SELECT @x = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = 'IF004' AND v.Periodo = '2026-06';
SELECT @y = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = 'IF004' AND v.Periodo = '2025-12';
IF ABS(@x - 11.7935) < 0.00005 AND ABS(@y - 12.2043) < 0.00005
    PRINT N'OK    9 ROE Pichincha: 2026-06 = 11,7935 y 2025-12 = 12,2043 (diciembre: patrimonio − ganancia)';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 9 ROE: ', @x, N' / ', @y); END

SELECT @n = COUNT(*) FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = 'IF002' AND v.Periodo BETWEEN '2024-01' AND '2024-11';
IF @n = 0 PRINT N'OK   10 ROA 2024-01 a 2024-11 sin valor (falta diciembre de 2023 en la BD)';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 10 ROA con valor en 2024: ', @n); END

/* ---------- 3. Sector ---------- */
INSERT INTO @vs EXEC api.ObtenerValoresCuadroSector @CuadroID = 'CAR05', @TiposEntidad = 'BP,COOP,MU';
SELECT @n = COUNT(*) FROM @vs v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo IN ('IF002', 'IF004') AND v.Periodo = '2026-06';
IF @n = 2 PRINT N'OK   11 CAR05 sistema: ROA y ROE con valor en 2026-06';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 11 CAR05 ROA/ROE: ', @n, N' valores'); END

/* ---------- 4. Fórmulas sin nota técnica (07_formulas_sin_nota.sql) ---------- */
IF EXISTS (SELECT 1 FROM dbo.CuadroFila WHERE CuadroID = 'EFI03' AND Codigo = 'Gan_Eje_anual' AND Calculo = 'SUMA')
BEGIN
    DELETE FROM @ve;
    INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI03', @IFIIDs = '260';
    SELECT @x = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = 'Gan_Eje_anual' AND v.Periodo = '2026-07';
    SELECT @y = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = 'Mar_Br_Fi_anual' AND v.Periodo = '2026-07';
    IF ABS(@x - 250.8412) < 0.001 AND ABS(@y - 1864.1852) < 0.001
        PRINT N'OK   12 EFI03 Pichincha 2026-07: Gan_Eje_anual = 250,8412 y Mar_Br_Fi_anual = 1 864,1852';
    ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 12 Márgenes anualizados: ', @x, N' / ', @y); END

    DELETE FROM @ve;
    INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI07', @IFIIDs = '260', @IdSub = N'Cartera Total';
    SELECT @x = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = 'Por_vencer' AND v.Periodo = '2026-06';
    SELECT @y = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = 'tasa_Por_vencer' AND v.Periodo = '2026-06';
    IF ABS(@x - 13677.0137) < 0.001 AND ABS(@y - 8.2709) < 0.00005
        PRINT N'OK   13 EFI07 Pichincha 2026-06: Por_vencer = 13 677,0137 y tasa_Por_vencer = 8,2709';
    ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 13 Cartera por vencer: ', @x, N' / ', @y); END

    DELETE FROM @ve;
    INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI05', @IFIIDs = '260';
    SELECT @x = v.Valor FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo = 'PTR_9' AND v.Periodo = '2026-06';
    IF ABS(@x - 1652.433) < 0.001
        PRINT N'OK   14 EFI05 PTR_9 Pichincha 2026-06 = 1 652,433 (9 % de ACT_PON_RIESGO)';
    ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 14 PTR_9: ', @x); END

    DELETE FROM @vs;
    INSERT INTO @vs EXEC api.ObtenerValoresCuadroSector @CuadroID = 'SFN03', @TiposEntidad = 'BP,COOP,MU';
    SELECT @n = COUNT(*) FROM @vs v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID WHERE f.Codigo LIKE '%[_]anual' AND v.Periodo = '2026-06';
    IF @n = 7 PRINT N'OK   15 SFN03 sistema: los 7 márgenes anualizados con valor en 2026-06';
    ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 15 SFN03 márgenes anualizados: ', @n, N' valores'); END
END
ELSE
    PRINT N'AVISO 12‑15 omitidas: falta ejecutar 07_formulas_sin_nota.sql';

/* ---------- Resumen: filas que siguen en JSON por cuadro ---------- */
PRINT N'';
PRINT N'Filas que siguen tomando su valor del JSON:';
DECLARE @c varchar(16), @k int;
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT CuadroID, COUNT(*) FROM dbo.CuadroFila WHERE Calculo = 'JSON' GROUP BY CuadroID ORDER BY CuadroID;
OPEN cur;
FETCH NEXT FROM cur INTO @c, @k;
WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT CONCAT(N'  ', @c, N': ', @k);
    FETCH NEXT FROM cur INTO @c, @k;
END
CLOSE cur; DEALLOCATE cur;

IF @fallas = 0 PRINT N'==> 06_pruebas_tiempo: TODAS OK';
ELSE THROW 50098, N'06_pruebas_tiempo: hay fallas', 1;
GO
