/* =====================================================================================
   Fase 2 · 03 — Pruebas del catálogo de cuadros (solo lectura)
   Valores esperados verificados contra los JSON publicados y el .sav (ventana 2024-01 → 2026-08).
   Imprime OK / FALLA / AVISO por prueba; lanza error al final si alguna falla.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET NOCOUNT ON;

DECLARE @fallas int = 0, @n int;

/* ---------- 1. Contenido de la semilla --------------------------------------------------- */
IF (SELECT COUNT(*) FROM dbo.Cuadro) = 29
   AND (SELECT COUNT(*) FROM dbo.CuadroFila) = 25688
   AND (SELECT COUNT(*) FROM dbo.CuadroFilaTermino) = 8305
    PRINT N'OK    1 Semilla: 29 cuadros, 25 688 filas, 8 305 términos';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 1 Conteos de la semilla'; END

/* ---------- 2. Integridad de las fórmulas -------------------------------------------------- */
SELECT @n = COUNT(*) FROM dbo.CuadroFila AS f
WHERE f.Calculo IN ('SUMA', 'RATIO')
  AND NOT EXISTS (SELECT 1 FROM dbo.CuadroFilaTermino t WHERE t.CuadroFilaID = f.CuadroFilaID AND t.Rol = 'N');
IF @n = 0 PRINT N'OK    2 Toda fila SUMA/RATIO tiene numerador';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 2 Filas SUMA/RATIO sin numerador: ', @n); END

SELECT @n = COUNT(*) FROM dbo.CuadroFila AS f
WHERE f.Calculo = 'RATIO'
  AND NOT EXISTS (SELECT 1 FROM dbo.CuadroFilaTermino t WHERE t.CuadroFilaID = f.CuadroFilaID AND t.Rol = 'D');
IF @n = 0 PRINT N'OK    3 Toda fila RATIO tiene denominador';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 3 Filas RATIO sin denominador: ', @n); END

SELECT @n = COUNT(*) FROM dbo.CuadroFilaTermino t
INNER JOIN dbo.CuadroFila f ON f.CuadroFilaID = t.CuadroFilaID
INNER JOIN dbo.Cuadro c ON c.CuadroID = f.CuadroID
WHERE c.Ambito = 'SECTOR' AND t.Fuente = 'INDICADOR';
IF @n = 0 PRINT N'OK    4 Los cuadros de sector no usan indicadores (no se pueden sumar)';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 4 Términos INDICADOR en cuadros de sector: ', @n); END

SELECT @n = COUNT(*) FROM dbo.CuadroFilaTermino t
WHERE t.Fuente = 'INDICADOR' AND NOT EXISTS (SELECT 1 FROM dbo.IndicadorFin i WHERE i.Codigo = t.IndicadorCodigo);
IF @n = 0 PRINT N'OK    5 Todos los indicadores existen en dbo.IndicadorFin';
ELSE BEGIN SET @fallas += 1; PRINT CONCAT(N'FALLA 5 Indicadores inexistentes: ', @n); END

-- Informativo: cuentas del catálogo que no existen en B11 (sus filas saldrán sin valor).
SELECT @n = COUNT(DISTINCT t.CuentaID) FROM dbo.CuadroFilaTermino t
WHERE t.Fuente = 'CUENTA' AND NOT EXISTS (SELECT 1 FROM dbo.Cuenta c WHERE c.CuentaID = t.CuentaID);
IF @n = 0 PRINT N'OK    6 Todas las cuentas de las fórmulas existen en dbo.Cuenta';
ELSE PRINT CONCAT(N'AVISO 6 Cuentas de las fórmulas que no están en dbo.Cuenta: ', @n, N' (revisar; sus filas no tendrán valor)');

/* ---------- 3. Variantes ------------------------------------------------------------------- */
IF api.fnResolverVariante('CAR02', 'BP:1') = 'BP'
   AND api.fnResolverVariante('CAR02', 'COOP:2') = 'COOP:2'
   AND api.fnResolverVariante('SFN01', 'BP:1') = '*'
   AND api.fnResolverVariante('EFI06', 'COOP') = 'COOP'
    PRINT N'OK    7 Resolución de variantes (exacta → tipos → *)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 7 Resolución de variantes'; END

/* ---------- 4. Plantilla y textos --------------------------------------------------------- */
DECLARE @p TABLE (CuadroFilaID int, Variante varchar(20), IdSub nvarchar(80), Orden int, Codigo varchar(64), CodigoBase varchar(16),
                  Grupo nvarchar(300), Variable nvarchar(800), Nivel tinyint, SegmentoCredito nvarchar(80), Unidad nvarchar(100), Calculo varchar(8));
-- Con columnas explícitas (api.ObtenerPlantilla suma columnas en db/fase3/03; INSERT … EXEC exige las mismas).
INSERT INTO @p SELECT CuadroFilaID, Variante, IdSub, Orden, Codigo, CodigoBase, Grupo, Variable, Nivel, SegmentoCredito, Unidad, Calculo
               FROM api.fnFilasCuadro('EFI01', api.fnResolverVariante('EFI01', NULL));
IF (SELECT COUNT(*) FROM @p) = 202
   AND (SELECT TOP 1 Codigo FROM @p ORDER BY Orden) = '@1'
   AND EXISTS (SELECT 1 FROM @p WHERE Codigo = '@1103' AND Variable = (SELECT Nombre FROM dbo.Cuenta WHERE CuentaID = 1103))
   AND EXISTS (SELECT 1 FROM @p WHERE Codigo = 'pasipat' AND Calculo = 'SUMA' AND Variable = N'TOTAL PASIVO Y PATRIMONIO')
   AND NOT EXISTS (SELECT 1 FROM @p WHERE Codigo LIKE '@6%' OR Codigo LIKE '@7%')
    PRINT N'OK    8 Plantilla EFI01: 202 filas, orden, texto de dbo.Cuenta, pasipat, sin grupos 6/7';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 8 Plantilla EFI01'; END

DELETE FROM @p;
INSERT INTO @p SELECT CuadroFilaID, Variante, IdSub, Orden, Codigo, CodigoBase, Grupo, Variable, Nivel, SegmentoCredito, Unidad, Calculo
               FROM api.fnFilasCuadro('EFI06', api.fnResolverVariante('EFI06', 'BP')) WHERE IdSub = N'Saldo Millones USD';
IF (SELECT COUNT(*) FROM @p) = 890
   AND EXISTS (SELECT 1 FROM @p WHERE CodigoBase = '1101' AND Variable = CONCAT(N'1101.    ', (SELECT Nombre FROM dbo.Cuenta WHERE CuentaID = 1101)))
    PRINT N'OK    9 Plantilla EFI06 BP (saldo): 890 cuentas, texto "1101.    Nombre"';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 9 Plantilla EFI06 BP'; END

/* ---------- 5. Valores de entidad (BP. PICHINCHA = IFIID 260) ------------------------------ */
DECLARE @ve TABLE (IFIID int, CuadroFilaID int, Periodo char(7), Valor float);
INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI01', @IFIIDs = '260';
IF EXISTS (SELECT 1 FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.Codigo = '@1' AND v.Periodo = '2026-07' AND ABS(v.Valor - 22806.742733) < 1e-6)
AND EXISTS (SELECT 1 FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.Codigo = 'pasipat' AND v.Periodo = '2026-07' AND ABS(v.Valor - 22660.4187) < 1e-3)
    PRINT N'OK   10 EFI01 Pichincha: ACTIVO 22 806,742733 y pasipat 22 660,4187 (= JSON) en 2026-07';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 10 EFI01 Pichincha'; END

DELETE FROM @ve;
INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI02', @IFIIDs = '260';
IF EXISTS (SELECT 1 FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.Codigo = 'Gan_Eje' AND v.Periodo = '2026-07' AND ABS(v.Valor - 146.324) < 1e-3)
AND EXISTS (SELECT 1 FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.Codigo = 'Marg_Ne' AND v.Periodo = '2026-07' AND ABS(v.Valor - 764.6353) < 1e-3)
    PRINT N'OK   11 EFI02 Pichincha: margen neto de intereses 764,6353 y ganancia 146,324 (= JSON)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 11 EFI02 Pichincha'; END

DELETE FROM @ve;
INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'EFI05', @IFIIDs = '260';
IF EXISTS (SELECT 1 FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.Codigo = 'SOLVENCIA' AND v.Periodo = '2026-07' AND ABS(v.Valor - 13.6285) < 1e-3)
    PRINT N'OK   12 EFI05 Pichincha: SOLVENCIA 13,6285 (indicador × 100)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 12 EFI05 Pichincha'; END

DELETE FROM @ve;
INSERT INTO @ve EXEC api.ObtenerValoresCuadroEntidades @CuadroID = 'REP01', @IFIIDs = '260,184,999999';
IF (SELECT COUNT(DISTINCT IFIID) FROM @ve) = 2
   AND EXISTS (SELECT 1 FROM @ve v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
               WHERE v.IFIID = 260 AND f.Codigo = '@1' AND v.Periodo = '2026-07' AND ABS(v.Valor - 22806.742733) < 1e-6)
    PRINT N'OK   13 REP01 carga masiva (2 entidades; IFIID inexistente se ignora)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 13 REP01 carga masiva'; END

/* ---------- 6. Valores de sector ----------------------------------------------------------- */
DECLARE @vs TABLE (CuadroFilaID int, Periodo char(7), Valor float);
INSERT INTO @vs EXEC api.ObtenerValoresCuadroSector @CuadroID = 'SFN01', @TiposEntidad = 'BP', @Segmentos = '1';
IF EXISTS (SELECT 1 FROM @vs v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.Codigo = '@1' AND v.Periodo = '2024-01' AND ABS(v.Valor - 38275.9688) < 1e-3)
AND EXISTS (SELECT 1 FROM @vs v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.Codigo = 'pasipat' AND v.Periodo = '2024-01' AND ABS(v.Valor - 38242.3059) < 1e-3)
    PRINT N'OK   14 SFN01 Bancos Grandes: ACTIVO 38 275,9688 y pasipat 38 242,3059 (= JSON) en 2024-01';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 14 SFN01 Bancos Grandes'; END

DELETE FROM @vs;
INSERT INTO @vs EXEC api.ObtenerValoresCuadroSector @CuadroID = 'SFN02', @TiposEntidad = 'BP', @Segmentos = '1';
IF EXISTS (SELECT 1 FROM @vs v JOIN dbo.CuadroFila f ON f.CuadroFilaID = v.CuadroFilaID
           WHERE f.Codigo = 'Gan_Eje' AND v.Periodo = '2026-07' AND ABS(v.Valor - 447.7213) < 1e-3)
    PRINT N'OK   15 SFN02 Bancos Grandes: ganancia del ejercicio 447,7213 (= JSON) en 2026-07';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 15 SFN02 Bancos Grandes'; END

/* ---------- 7. Errores ------------------------------------------------------------------- */
BEGIN TRY
    EXEC api.ObtenerPlantilla @CuadroID = 'NOEXISTE';
    SET @fallas += 1; PRINT N'FALLA 16 cuadro inexistente debía lanzar error';
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 50020 PRINT N'OK   16 Cuadro inexistente rechazado (50020)';
    ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 16 error inesperado: ' + ERROR_MESSAGE(); END
END CATCH

/* ---------- Resumen ----------------------------------------------------------------------- */
IF @fallas = 0
    PRINT N'==> Todas las pruebas pasaron.';
ELSE
    THROW 50099, N'Fallaron pruebas de la Fase 2 (ver mensajes).', 1;
GO
