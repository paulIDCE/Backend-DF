/* =====================================================================================
   Fase 3 · 01 — Fórmulas SUMA/RATIO para filas que hoy toman su valor del JSON (GENERADO)
   Origen: tools/catalogo/generar_formulas_fase3.py · validado con meses reales hasta 2026-08.
   - Análisis vertical de EFI06/SFN06 (todas las variantes): 100 × cuenta / cuenta raíz del grupo.
   - 28 fórmulas del descubrimiento, validadas en todas las entidades (y sectores).
   Idempotente: rehace los términos de cada fila tocada. No crea ni borra filas.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

/* ---------- 1. Análisis vertical ---------- */
IF 1 = 1
BEGIN
    DECLARE @av TABLE (CuadroFilaID int PRIMARY KEY, CuentaID int NOT NULL, Raiz int NOT NULL);
    INSERT INTO @av (CuadroFilaID, CuentaID, Raiz)
    SELECT f.CuadroFilaID, f.CuentaID, CAST(LEFT(CAST(f.CuentaID AS varchar(12)), 1) AS int)
    FROM dbo.CuadroFila AS f
    WHERE f.CuadroID IN ('EFI06', 'SFN06') AND f.IdSub = N'Análisis Vertical (%)' AND f.CuentaID IS NOT NULL;

    DELETE t FROM dbo.CuadroFilaTermino AS t INNER JOIN @av AS a ON a.CuadroFilaID = t.CuadroFilaID;
    INSERT INTO dbo.CuadroFilaTermino (CuadroFilaID, Rol, Fuente, CuentaID, Factor)
    SELECT CuadroFilaID, 'N', 'CUENTA', CuentaID, 1 FROM @av
    UNION ALL
    SELECT CuadroFilaID, 'D', 'CUENTA', Raiz, 1 FROM @av;
    UPDATE f SET Calculo = 'RATIO', Escala = 100
    FROM dbo.CuadroFila AS f INNER JOIN @av AS a ON a.CuadroFilaID = f.CuadroFilaID;
    DECLARE @nav int = (SELECT COUNT(*) FROM @av);
    PRINT CONCAT(N'Análisis vertical: ', @nav, N' filas → RATIO');
END

/* ---------- 2. Fórmulas descubiertas ---------- */
DECLARE @f TABLE (CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64), Calculo varchar(8), Escala float);
INSERT INTO @f VALUES
('CAR01', N'Cartera Total', 'IF011', 'SUMA', 1e-06),
('CAR01', N'Vivienda interés Público y Social', 'IF007_5', 'SUMA', 1e-06),
('CAR01', N'Vivienda interés Público y Social', 'IF008_5', 'SUMA', 1e-06),
('CAR01', N'Vivienda interés Público y Social', 'IF011_5', 'SUMA', 1e-06),
('EFI07', N'Cartera Total', 'IF011', 'SUMA', 1e-06),
('EFI07', N'Vivienda interés Público y Social', 'IF007_5', 'SUMA', 1e-06),
('EFI07', N'Vivienda interés Público y Social', 'IF008_5', 'SUMA', 1e-06),
('EFI07', N'Vivienda interés Público y Social', 'IF011_5', 'SUMA', 1e-06),
('EFI13', N'', 'E1_Cartera', 'RATIO', 100.0),
('EFI13', N'', 'E3_Inversiones', 'RATIO', 100.0),
('EFI13', N'', 'E4_INNF', 'RATIO', 100.0),
('EFI13', N'', 'E6_Cre_Exte', 'RATIO', 100.0),
('REP01', N'', 'E1_Cartera', 'RATIO', 100.0),
('REP01', N'', 'E3_Inversiones', 'RATIO', 100.0),
('REP01', N'', 'E4_INNF', 'RATIO', 100.0),
('REP01', N'', 'E6_Cre_Exte', 'RATIO', 100.0),
('REP01', N'', 'Gan_Eje', 'SUMA', 1e-06),
('REP01', N'', 'Gan_Pe_Imp', 'SUMA', 1e-06),
('REP01', N'', 'IF007_5', 'SUMA', 1e-06),
('REP01', N'', 'IF008_5', 'SUMA', 1e-06),
('REP01', N'', 'IF011', 'SUMA', 1e-06),
('REP01', N'', 'Mar_Br_Fi', 'SUMA', 1e-06),
('REP01', N'', 'Mar_Inte', 'SUMA', 1e-06),
('REP01', N'', 'Mar_Ne_Fina', 'SUMA', 1e-06),
('REP01', N'', 'Mar_Oper', 'SUMA', 1e-06),
('REP01', N'', 'Marg_Ne', 'SUMA', 1e-06),
('REP01', N'', 'SB005', 'SUMA', 1e-06),
('REP01', N'', 'SB008', 'SUMA', 1e-06);

DECLARE @t TABLE (CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64), Rol char(1), CuentaID int, Factor float);
INSERT INTO @t VALUES
('CAR01', N'Cartera Total', 'IF011', 'N', 14, 1.0),
('CAR01', N'Cartera Total', 'IF011', 'N', 1499, -1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF007_5', 'N', 1408, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF007_5', 'N', 1416, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF007_5', 'N', 1424, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF008_5', 'N', 1432, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF008_5', 'N', 1440, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF008_5', 'N', 1448, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1408, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1416, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1424, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1432, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1440, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1448, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1456, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1464, 1.0),
('CAR01', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1472, 1.0),
('EFI07', N'Cartera Total', 'IF011', 'N', 14, 1.0),
('EFI07', N'Cartera Total', 'IF011', 'N', 1499, -1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF007_5', 'N', 1408, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF007_5', 'N', 1416, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF007_5', 'N', 1424, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF008_5', 'N', 1432, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF008_5', 'N', 1440, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF008_5', 'N', 1448, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1408, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1416, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1424, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1432, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1440, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1448, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1456, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1464, 1.0),
('EFI07', N'Vivienda interés Público y Social', 'IF011_5', 'N', 1472, 1.0),
('EFI13', N'', 'E1_Cartera', 'N', 14, 1.0),
('EFI13', N'', 'E1_Cartera', 'N', 1499, -1.0),
('EFI13', N'', 'E1_Cartera', 'D', 1, 1.0),
('EFI13', N'', 'E3_Inversiones', 'N', 1901, 1.0),
('EFI13', N'', 'E3_Inversiones', 'D', 1, 1.0),
('EFI13', N'', 'E4_INNF', 'N', 18, 1.0),
('EFI13', N'', 'E4_INNF', 'D', 1, 1.0),
('EFI13', N'', 'E6_Cre_Exte', 'N', 26, 1.0),
('EFI13', N'', 'E6_Cre_Exte', 'D', 1, 1.0),
('REP01', N'', 'E1_Cartera', 'N', 14, 1.0),
('REP01', N'', 'E1_Cartera', 'N', 1499, -1.0),
('REP01', N'', 'E1_Cartera', 'D', 1, 1.0),
('REP01', N'', 'E3_Inversiones', 'N', 1901, 1.0),
('REP01', N'', 'E3_Inversiones', 'D', 1, 1.0),
('REP01', N'', 'E4_INNF', 'N', 18, 1.0),
('REP01', N'', 'E4_INNF', 'D', 1, 1.0),
('REP01', N'', 'E6_Cre_Exte', 'N', 26, 1.0),
('REP01', N'', 'E6_Cre_Exte', 'D', 1, 1.0),
('REP01', N'', 'Gan_Eje', 'N', 41, -1.0),
('REP01', N'', 'Gan_Eje', 'N', 42, -1.0),
('REP01', N'', 'Gan_Eje', 'N', 43, -1.0),
('REP01', N'', 'Gan_Eje', 'N', 44, -1.0),
('REP01', N'', 'Gan_Eje', 'N', 45, -1.0),
('REP01', N'', 'Gan_Eje', 'N', 46, -1.0),
('REP01', N'', 'Gan_Eje', 'N', 47, -1.0),
('REP01', N'', 'Gan_Eje', 'N', 48, -1.0),
('REP01', N'', 'Gan_Eje', 'N', 51, 1.0),
('REP01', N'', 'Gan_Eje', 'N', 52, 1.0),
('REP01', N'', 'Gan_Eje', 'N', 53, 1.0),
('REP01', N'', 'Gan_Eje', 'N', 54, 1.0),
('REP01', N'', 'Gan_Eje', 'N', 55, 1.0),
('REP01', N'', 'Gan_Eje', 'N', 56, 1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 41, -1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 42, -1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 43, -1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 44, -1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 45, -1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 46, -1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 47, -1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 51, 1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 52, 1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 53, 1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 54, 1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 55, 1.0),
('REP01', N'', 'Gan_Pe_Imp', 'N', 56, 1.0),
('REP01', N'', 'IF007_5', 'N', 1408, 1.0),
('REP01', N'', 'IF007_5', 'N', 1416, 1.0),
('REP01', N'', 'IF007_5', 'N', 1424, 1.0),
('REP01', N'', 'IF008_5', 'N', 1432, 1.0),
('REP01', N'', 'IF008_5', 'N', 1440, 1.0),
('REP01', N'', 'IF008_5', 'N', 1448, 1.0),
('REP01', N'', 'IF011', 'N', 14, 1.0),
('REP01', N'', 'IF011', 'N', 1499, -1.0),
('REP01', N'', 'Mar_Br_Fi', 'N', 41, -1.0),
('REP01', N'', 'Mar_Br_Fi', 'N', 42, -1.0),
('REP01', N'', 'Mar_Br_Fi', 'N', 43, -1.0),
('REP01', N'', 'Mar_Br_Fi', 'N', 51, 1.0),
('REP01', N'', 'Mar_Br_Fi', 'N', 52, 1.0),
('REP01', N'', 'Mar_Br_Fi', 'N', 53, 1.0),
('REP01', N'', 'Mar_Br_Fi', 'N', 54, 1.0),
('REP01', N'', 'Mar_Inte', 'N', 41, -1.0),
('REP01', N'', 'Mar_Inte', 'N', 42, -1.0),
('REP01', N'', 'Mar_Inte', 'N', 43, -1.0),
('REP01', N'', 'Mar_Inte', 'N', 44, -1.0),
('REP01', N'', 'Mar_Inte', 'N', 45, -1.0),
('REP01', N'', 'Mar_Inte', 'N', 51, 1.0),
('REP01', N'', 'Mar_Inte', 'N', 52, 1.0),
('REP01', N'', 'Mar_Inte', 'N', 53, 1.0),
('REP01', N'', 'Mar_Inte', 'N', 54, 1.0),
('REP01', N'', 'Mar_Ne_Fina', 'N', 41, -1.0),
('REP01', N'', 'Mar_Ne_Fina', 'N', 42, -1.0),
('REP01', N'', 'Mar_Ne_Fina', 'N', 43, -1.0),
('REP01', N'', 'Mar_Ne_Fina', 'N', 44, -1.0),
('REP01', N'', 'Mar_Ne_Fina', 'N', 51, 1.0),
('REP01', N'', 'Mar_Ne_Fina', 'N', 52, 1.0),
('REP01', N'', 'Mar_Ne_Fina', 'N', 53, 1.0),
('REP01', N'', 'Mar_Ne_Fina', 'N', 54, 1.0),
('REP01', N'', 'Mar_Oper', 'N', 41, -1.0),
('REP01', N'', 'Mar_Oper', 'N', 42, -1.0),
('REP01', N'', 'Mar_Oper', 'N', 43, -1.0),
('REP01', N'', 'Mar_Oper', 'N', 44, -1.0),
('REP01', N'', 'Mar_Oper', 'N', 45, -1.0),
('REP01', N'', 'Mar_Oper', 'N', 46, -1.0),
('REP01', N'', 'Mar_Oper', 'N', 51, 1.0),
('REP01', N'', 'Mar_Oper', 'N', 52, 1.0),
('REP01', N'', 'Mar_Oper', 'N', 53, 1.0),
('REP01', N'', 'Mar_Oper', 'N', 54, 1.0),
('REP01', N'', 'Mar_Oper', 'N', 55, 1.0),
('REP01', N'', 'Marg_Ne', 'N', 41, -1.0),
('REP01', N'', 'Marg_Ne', 'N', 51, 1.0),
('REP01', N'', 'SB005', 'N', 1101, 1.0),
('REP01', N'', 'SB005', 'N', 1102, 1.0),
('REP01', N'', 'SB005', 'N', 1104, 1.0),
('REP01', N'', 'SB005', 'N', 1105, 1.0),
('REP01', N'', 'SB008', 'N', 1499, 1.0),
('REP01', N'', 'SB008', 'N', 1699, 1.0),
('REP01', N'', 'SB008', 'N', 1799, 1.0),
('REP01', N'', 'SB008', 'N', 1999, 1.0);

DECLARE @filas TABLE (CuadroFilaID int PRIMARY KEY, CuadroID varchar(16), IdSub nvarchar(80), Codigo varchar(64));
INSERT INTO @filas
SELECT cf.CuadroFilaID, cf.CuadroID, cf.IdSub, cf.Codigo
FROM dbo.CuadroFila AS cf
INNER JOIN @f AS f ON f.CuadroID = cf.CuadroID AND f.IdSub = cf.IdSub AND f.Codigo = cf.Codigo;

DELETE t FROM dbo.CuadroFilaTermino AS t INNER JOIN @filas AS x ON x.CuadroFilaID = t.CuadroFilaID;
INSERT INTO dbo.CuadroFilaTermino (CuadroFilaID, Rol, Fuente, CuentaID, Factor)
SELECT x.CuadroFilaID, t.Rol, 'CUENTA', t.CuentaID, t.Factor
FROM @filas AS x
INNER JOIN @t AS t ON t.CuadroID = x.CuadroID AND t.IdSub = x.IdSub AND t.Codigo = x.Codigo;
UPDATE cf SET Calculo = f.Calculo, Escala = f.Escala
FROM dbo.CuadroFila AS cf
INNER JOIN @filas AS x ON x.CuadroFilaID = cf.CuadroFilaID
INNER JOIN @f AS f ON f.CuadroID = x.CuadroID AND f.IdSub = x.IdSub AND f.Codigo = x.Codigo;
DECLARE @nf int = (SELECT COUNT(*) FROM @filas);
PRINT CONCAT(N'Fórmulas descubiertas: ', @nf, N' filas actualizadas');

COMMIT TRANSACTION;
PRINT N'01_formulas_suma_ratio: OK';
GO
