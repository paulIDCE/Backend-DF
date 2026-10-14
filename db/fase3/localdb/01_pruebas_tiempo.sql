/* =====================================================================================
   Fase 3 · LocalDB · 01 — Pruebas de las operaciones en el tiempo con datos sintéticos
   (00_base_minima.sql). Los valores esperados están calculados a mano a partir de las
   fórmulas de los datos: k = 0 en 2024‑01, activo = 100 + k, patrimonio = 20 + k,
   IF011 = 49 + k, Gan_Eje = mes (acumulado) → anualizado 12; todo en millones.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET NOCOUNT ON;

DECLARE @fallas int = 0, @n int, @x float;
DECLARE @AH nvarchar(80) = N'Análisis Horizontal (%)', @AV nvarchar(80) = N'Análisis Vertical (%)';
DECLARE @ve TABLE (IFIID int, CuadroFilaID int, Periodo char(7), Valor float);
DECLARE @vs TABLE (CuadroFilaID int, Periodo char(7), Valor float);
DECLARE @esp TABLE (Prueba varchar(60), Cuadro varchar(16), IdSub nvarchar(80), Codigo varchar(64), CuentaID int,
                    Etiqueta nvarchar(400), IFIID int, Periodo char(7), Esperado float NULL);   -- NULL = no debe venir

-- Entidad 1001 (BP)
INSERT INTO @esp VALUES
 ('NIVEL      EFI01 @1',                'EFI01', N'',  '@1',   NULL, NULL, 1001, '2025-06', 117),
 ('NIVEL      EFI06 vertical 14',       'EFI06', @AV,  NULL,   14,   NULL, 1001, '2025-06', 100e0 * 67 / 117),
 ('VAR_12     EFI06 horizontal 1',      'EFI06', @AH,  NULL,   1,    NULL, 1001, '2025-06', 100e0 * 12 / 105),
 ('VAR_12     sin historia 2024-12',    'EFI06', @AH,  NULL,   1,    NULL, 1001, '2024-12', NULL),
 ('VAR_12     primer mes 2025-01',      'EFI06', @AH,  NULL,   1,    NULL, 1001, '2025-01', 100e0 * 12 / 100),
 ('ANUAL      EFI03 @5A 2024-03',       'EFI03', N'',  '@5A',  NULL, NULL, 1001, '2024-03', 24),
 ('ANUAL      EFI03 @5A 2025-11',       'EFI03', N'',  '@5A',  NULL, NULL, 1001, '2025-11', 24),
 ('VAR_1      EFI04 ACTIVO 2025-06',    'EFI04', N'',  NULL,   NULL, N'ACTIVO', 1001, '2025-06', 1),
 ('VAR_1      EFI04 sin historia',      'EFI04', N'',  NULL,   NULL, N'ACTIVO', 1001, '2024-01', NULL),
 ('TASA_12    EFI07 tasa_IF011',        'EFI07', N'Cartera Total', 'tasa_IF011', NULL, NULL, 1001, '2025-06', 100e0 * 12 / 54),
 ('ROA        EFI11 IF002 2025-06',     'EFI11', N'',  'IF002', NULL, NULL, 1001, '2025-06', 100e0 * 12 / 114),
 ('ROA        diciembre 2025-12',       'EFI11', N'',  'IF002', NULL, NULL, 1001, '2025-12', 100e0 * 12 / 123),
 ('ROA        diciembre 2024 (sin hist.)', 'EFI11', N'', 'IF002', NULL, NULL, 1001, '2024-12', 100e0 * 12 / 111),
 ('ROA        sin historia 2024-06',    'EFI11', N'',  'IF002', NULL, NULL, 1001, '2024-06', NULL),
 ('ROE        EFI11 IF004 2025-06',     'EFI11', N'',  'IF004', NULL, NULL, 1001, '2025-06', 100e0 * 12 / 34),
 ('ROE        diciembre 2025-12',       'EFI11', N'',  'IF004', NULL, NULL, 1001, '2025-12', 100e0 * 12 / 31),
 ('ROA        REP01 E1_ROA',            'REP01', N'',  'E1_ROA', NULL, NULL, 1001, '2025-06', 100e0 * 12 / 114),
 -- Fase 3 · 07 (sin nota técnica)
 ('07 ANUAL   EFI03 Gan_Eje_anual',     'EFI03', N'',  'Gan_Eje_anual', NULL, NULL, 1001, '2025-03', 12),
 ('07 ANUAL   REP01 Mar_Br_Fi_anual',   'REP01', N'',  'Mar_Br_Fi_anual', NULL, NULL, 1001, '2025-03', 12),
 ('07 NIVEL   EFI07 Por_vencer',        'EFI07', N'Cartera Total', 'Por_vencer', NULL, NULL, 1001, '2025-06', 47),
 ('07 NIVEL   EFI07 Por_vencida',       'EFI07', N'Cartera Total', 'Por_vencida', NULL, NULL, 1001, '2025-06', 1),
 ('07 TASA_12 EFI07 tasa_Por_vencer',   'EFI07', N'Cartera Total', 'tasa_Por_vencer', NULL, NULL, 1001, '2025-06', 100e0 * 12 / 35),
 ('07 INDIC.  EFI05 PTR_9',             'EFI05', N'',  'PTR_9', NULL, NULL, 1001, '2025-06', 90),
-- Entidad 1002 (COOP, montos × 2): los cocientes no cambian, los niveles se duplican
 ('ANUAL      COOP @5A',                'EFI03', N'',  '@5A',  NULL, NULL, 1002, '2025-11', 48),
 ('ROA        COOP',                    'EFI11', N'',  'IF002', NULL, NULL, 1002, '2025-06', 100e0 * 12 / 114),
 ('VAR_12     COOP horizontal',         'EFI06', @AH,  NULL,   1,    NULL, 1002, '2025-06', 100e0 * 12 / 105),
-- Entidad 1003 (MU, sin 2025‑03): los meses que necesitan 2025‑03 quedan sin valor
 ('HUECO      VAR_1 2025-04',           'EFI04', N'',  NULL,   NULL, N'ACTIVO', 1003, '2025-04', NULL),
 ('HUECO      VAR_1 2025-05',           'EFI04', N'',  NULL,   NULL, N'ACTIVO', 1003, '2025-05', 1),
 ('HUECO      PROMEDIO 2025-06',        'EFI11', N'',  'IF002', NULL, NULL, 1003, '2025-06', NULL),
 ('HUECO      PROMEDIO 2026-01',        'EFI11', N'',  'IF002', NULL, NULL, 1003, '2026-01', 100e0 * 12 / 123.5),
 ('HUECO      VAR_12 en el hueco',      'EFI06', @AH,  NULL,   1,    NULL, 1003, '2025-03', NULL),
 ('HUECO      VAR_12 2026-02',          'EFI06', @AH,  NULL,   1,    NULL, 1003, '2026-02', 100e0 * 12 / 113);

-- Valores calculados para cada (cuadro, entidad).
DECLARE @c varchar(16), @i int;
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT Cuadro, IFIID FROM @esp;
OPEN cur;
FETCH NEXT FROM cur INTO @c, @i;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @ids varchar(12) = CAST(@i AS varchar(12));
    INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = @c, @IFIIDs = @ids;
    FETCH NEXT FROM cur INTO @c, @i;
END
CLOSE cur; DEALLOCATE cur;

DECLARE @r TABLE (Prueba varchar(60), Esperado float NULL, Obtenido float NULL, Filas int);
INSERT INTO @r
SELECT e.Prueba, e.Esperado, MAX(v.Valor), COUNT(v.Valor)
FROM @esp AS e
INNER JOIN dbo.Ifi AS i ON i.IFIID = e.IFIID
INNER JOIN dbo.CuadroFila AS f ON f.CuadroID = e.Cuadro AND f.Variante = api.fnResolverVariante(e.Cuadro, i.TipoEntidad)
                              AND f.IdSub = e.IdSub
                              AND (e.Codigo IS NULL OR f.Codigo = e.Codigo)
                              AND (e.CuentaID IS NULL OR f.CuentaID = e.CuentaID)
                              AND (e.Etiqueta IS NULL OR (f.Etiqueta = e.Etiqueta AND REPLACE(UPPER(f.Grupo), N'Ó', N'O') = N'VARIACION MENSUAL'))
LEFT JOIN @ve AS v ON v.IFIID = e.IFIID AND v.CuadroFilaID = f.CuadroFilaID AND v.Periodo = e.Periodo
GROUP BY e.Prueba, e.Esperado;

DECLARE @prueba varchar(60), @e float, @o float, @filas int;
DECLARE cr CURSOR LOCAL FAST_FORWARD FOR SELECT Prueba, Esperado, Obtenido, Filas FROM @r ORDER BY Prueba;
OPEN cr;
FETCH NEXT FROM cr INTO @prueba, @e, @o, @filas;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF (@e IS NULL AND @filas = 0) OR (@e IS NOT NULL AND @filas = 1 AND ABS(@o - @e) < 1e-9)
        PRINT CONCAT(N'OK    ', @prueba, N' = ', ISNULL(CAST(@o AS varchar(30)), N'(sin valor)'));
    ELSE
    BEGIN
        SET @fallas += 1;
        PRINT CONCAT(N'FALLA ', @prueba, N': esperado ', ISNULL(CAST(@e AS varchar(30)), N'(sin valor)'),
                     N', obtenido ', ISNULL(CAST(@o AS varchar(30)), N'(sin valor)'), N' en ', @filas, N' filas');
    END
    FETCH NEXT FROM cr INTO @prueba, @e, @o, @filas;
END
CLOSE cr; DEALLOCATE cr;

SELECT @n = COUNT(*) FROM @esp;
SELECT @n = @n - COUNT(*) FROM @r;
IF @n <> 0
BEGIN
    SET @fallas += 1;
    PRINT CONCAT(N'FALLA filas del catálogo no encontradas: ', @n);
END

/* ---------- Sector (BP segmento 1 = solo la entidad 1001) ---------- */
INSERT INTO @vs EXEC api.ObtenerValoresCuadroSector @CuadroID = 'SFN06', @TiposEntidad = 'BP', @Segmentos = '1', @IdSub = @AH;
SELECT @x = v.Valor FROM @vs v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
WHERE f.CuentaID = 1 AND v.Periodo = '2025-06';
IF ABS(@x - 100e0 * 12 / 105) < 1e-9
   AND NOT EXISTS (SELECT 1 FROM @vs WHERE Periodo < '2025-01')
    PRINT N'OK    SECTOR     SFN06 horizontal BP:1 = entidad 1001; 2024 sin valor';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA SECTOR SFN06 horizontal: ', @x); END

DELETE FROM @vs;
INSERT INTO @vs EXEC api.ObtenerValoresCuadroSector @CuadroID = 'CAR05', @TiposEntidad = 'BP', @Segmentos = '1';
SELECT @x = v.Valor FROM @vs v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
WHERE f.Codigo = 'IF002' AND v.Periodo = '2025-06';
IF ABS(@x - 100e0 * 12 / 114) < 1e-9
    PRINT N'OK    SECTOR     CAR05 ROA BP:1 = entidad 1001';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA SECTOR CAR05 ROA: ', @x); END

DELETE FROM @vs;
INSERT INTO @vs EXEC api.ObtenerValoresCuadroSector @CuadroID = 'SFN01', @TiposEntidad = 'BP', @Segmentos = '1';
SELECT @x = v.Valor FROM @vs v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
WHERE f.Codigo = '@1' AND v.Periodo = '2025-06';
IF ABS(@x - 117) < 1e-9
    PRINT N'OK    SECTOR     SFN01 @1 (NIVEL) = 117';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA SECTOR SFN01 @1: ', @x); END

/* ---------- Plantilla: expone las operaciones ---------- */
DECLARE @p TABLE (CuadroFilaID int, Variante varchar(20), IdSub nvarchar(80), Orden int, Codigo varchar(64), CodigoBase varchar(16),
                  Grupo nvarchar(300), Variable nvarchar(800), Nivel tinyint, SegmentoCredito nvarchar(80), Unidad nvarchar(100),
                  Calculo varchar(8), OperacionN varchar(16), OperacionD varchar(16));
INSERT INTO @p EXEC api.ObtenerPlantilla @CuadroID = 'EFI11';
IF EXISTS (SELECT 1 FROM @p WHERE Codigo = 'IF002' AND OperacionN = 'ANUALIZADO' AND OperacionD = 'PROMEDIO_DIC_ROA')
   AND NOT EXISTS (SELECT 1 FROM @p WHERE OperacionN IS NULL OR OperacionD IS NULL)
    PRINT N'OK    PLANTILLA  OperacionN/OperacionD (NIVEL en vez de NULL)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA PLANTILLA operaciones'; END

IF @fallas = 0 PRINT N'==> Pruebas LocalDB de operaciones en el tiempo: TODAS OK';
ELSE THROW 50099, N'Pruebas LocalDB de operaciones en el tiempo: hay fallas', 1;
GO
