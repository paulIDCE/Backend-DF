/* =====================================================================================
   Fase 0 · 04 — Pruebas de humo con valores ya verificados contra el .sav y los JSON
   Solo lectura. Imprime OK / FALLA por prueba y al final un resumen (lanza error si falla
   alguna). Los valores esperados corresponden a la carga actual (2024-01 → 2026-08);
   si el ETL recarga meses, la prueba de ventana puede cambiar legítimamente.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET NOCOUNT ON;

DECLARE @fallas int = 0;

/* ---------- 1. Ventana --------------------------------------------------------------- */
DECLARE @v TABLE (Desde char(7), Hasta char(7), DesdeFechaID int, HastaFechaID int, Meses int,
                  MesesConDatos int, FilasB11 bigint, Firma varchar(100));
INSERT INTO @v EXEC api.ObtenerVentana;

IF EXISTS (SELECT 1 FROM @v WHERE Desde = '2024-01' AND Hasta = '2026-08' AND Meses = 32 AND MesesConDatos = 32)
    PRINT N'OK    1 Ventana 2024-01 → 2026-08 (32 meses)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 1 Ventana'; END

IF EXISTS (SELECT 1 FROM @v WHERE FilasB11 = (SELECT COUNT_BIG(*) FROM dbo.B11))
    PRINT N'OK    2 FilasB11 de la vista = COUNT(*) de B11 (vista indexada consistente)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 2 FilasB11'; END

/* ---------- 2. Entidades --------------------------------------------------------------- */
DECLARE @ifi TABLE (IFIID int, Ruc varchar(15), Nombre varchar(256), TipoEntidad varchar(8), Segmento smallint);
INSERT INTO @ifi EXEC api.ListarIfi;

IF EXISTS (SELECT 1 FROM @ifi WHERE IFIID = 260 AND Ruc = '1790010937001' AND Nombre = 'BP. PICHINCHA')
    PRINT N'OK    3 ListarIfi: 260 = BP. PICHINCHA (1790010937001)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 3 ListarIfi'; END

/* ---------- 3. Saldos por entidad (BP. PICHINCHA, cuenta 1 = ACTIVO) ------------------- */
DECLARE @se TABLE (Cuenta varchar(12), Periodo char(7), Saldo money);
INSERT INTO @se EXEC api.ObtenerSaldosEntidad @IFIID = 260, @Cuentas = '@1,14';

IF  EXISTS (SELECT 1 FROM @se WHERE Cuenta = '1'  AND Periodo = '2024-01' AND Saldo = 16791284118.21)
AND EXISTS (SELECT 1 FROM @se WHERE Cuenta = '1'  AND Periodo = '2026-07' AND Saldo = 22806742733.00)
AND EXISTS (SELECT 1 FROM @se WHERE Cuenta = '1'  AND Periodo = '2026-08' AND Saldo = 23202785318.99)
AND EXISTS (SELECT 1 FROM @se WHERE Cuenta = '14' AND Periodo = '2026-08' AND Saldo = 13358976836.95)
AND (SELECT COUNT(*) FROM @se) = 64
    PRINT N'OK    4 ObtenerSaldosEntidad (= JSON EFI01 22 806,7427 en 2026-07)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 4 ObtenerSaldosEntidad'; END

DELETE FROM @se;
INSERT INTO @se EXEC api.ObtenerSaldosEntidad @IFIID = 260;
IF (SELECT COUNT(*) FROM @se) = (SELECT COUNT(*) FROM dbo.B11 WHERE IFIID = 260)
    PRINT N'OK    5 ObtenerSaldosEntidad sin filtro devuelve todas las cuentas';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 5 ObtenerSaldosEntidad sin filtro'; END

/* ---------- 4. Carga masiva --------------------------------------------------------------- */
DECLARE @sm TABLE (IFIID int, Cuenta varchar(12), Periodo char(7), Saldo money);
INSERT INTO @sm EXEC api.ObtenerSaldosEntidades @IFIIDs = '260,184,999999', @Cuentas = '1';
IF (SELECT COUNT(*) FROM @sm) = 64
   AND EXISTS (SELECT 1 FROM @sm WHERE IFIID = 260 AND Periodo = '2026-07' AND Saldo = 22806742733.00)
    PRINT N'OK    6 ObtenerSaldosEntidades (IFIID inexistente se ignora)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 6 ObtenerSaldosEntidades'; END

/* ---------- 5. Agregados (= JSON SFN01 @1) ----------------------------------------------- */
DECLARE @ag TABLE (Cuenta varchar(12), Periodo char(7), Saldo money, Entidades bigint);

INSERT INTO @ag EXEC api.ObtenerSaldosAgregado @TiposEntidad = 'BP', @Segmentos = '1', @Cuentas = '1';
IF EXISTS (SELECT 1 FROM @ag WHERE Periodo = '2024-01' AND Saldo = 38275968790.73 AND Entidades = 4)
AND EXISTS (SELECT 1 FROM @ag WHERE Periodo = '2026-07' AND ABS(Saldo / 1000000 - 53946.6732) < 0.001)
    PRINT N'OK    7 Bancos Privados Grandes (BP/1) = JSON 38 275,9688 y 53 946,6732';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 7 Agregado BP/1'; END

DELETE FROM @ag;
INSERT INTO @ag EXEC api.ObtenerSaldosAgregado @TiposEntidad = 'BP', @Cuentas = '1';
IF EXISTS (SELECT 1 FROM @ag WHERE Periodo = '2024-01' AND ABS(Saldo / 1000000 - 59955.5592) < 0.001)
    PRINT N'OK    8 Sector Financiero Privado (BP) = JSON 59 955,5592';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 8 Agregado BP'; END

DELETE FROM @ag;
INSERT INTO @ag EXEC api.ObtenerSaldosAgregado @TiposEntidad = 'MU', @Cuentas = '1';
IF EXISTS (SELECT 1 FROM @ag WHERE Periodo = '2024-01' AND ABS(Saldo / 1000000 - 1192.6644) < 0.001)
    PRINT N'OK    9 Mutualistas (MU) = JSON 1 192,6644';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 9 Agregado MU'; END

DELETE FROM @ag;
INSERT INTO @ag EXEC api.ObtenerSaldosAgregado @TiposEntidad = 'BP,COOP,MU', @Cuentas = '1';
-- Convención C3: incluye todas las COOP, por eso difiere del JSON (113 437,9352) ≈ +0,6 %.
IF EXISTS (SELECT 1 FROM @ag WHERE Periodo = '2026-07' AND ABS(Saldo / 1000000 - 114089.97) < 0.05)
    PRINT N'OK   10 Sistema Financiero Nacional (BP+COOP+MU) según C3';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 10 Agregado nacional'; END

/* ---------- 6. Indicadores ----------------------------------------------------------------- */
DECLARE @ind TABLE (Codigo varchar(32), Periodo char(7), Valor float);
INSERT INTO @ind EXEC api.ObtenerIndicadoresEntidad @IFIID = 260, @Codigos = 'SOLVENCIA,PTC';
IF EXISTS (SELECT 1 FROM @ind WHERE Codigo = 'PTC' AND Periodo = '2026-07' AND Valor = 2529159469)
AND EXISTS (SELECT 1 FROM @ind WHERE Codigo = 'SOLVENCIA' AND Periodo = '2026-07' AND ABS(Valor - 0.1362845297) < 1e-9)
    PRINT N'OK   11 ObtenerIndicadoresEntidad (PTC, SOLVENCIA)';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 11 ObtenerIndicadoresEntidad'; END

/* ---------- 7. Cuentas ----------------------------------------------------------------- */
DECLARE @cu TABLE (CuentaID int, Nombre varchar(256));
INSERT INTO @cu EXEC api.ListarCuentas;
IF EXISTS (SELECT 1 FROM @cu WHERE CuentaID = 1 AND Nombre = 'ACTIVO')
    PRINT N'OK   12 ListarCuentas';
ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 12 ListarCuentas'; END

/* ---------- 8. Validaciones de parámetros ----------------------------------------------- */
BEGIN TRY
    EXEC api.ObtenerSaldosEntidad @IFIID = NULL;
    SET @fallas += 1; PRINT N'FALLA 13 @IFIID NULL debía lanzar error';
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 50010 PRINT N'OK   13 @IFIID NULL rechazado (50010)';
    ELSE BEGIN SET @fallas += 1; PRINT N'FALLA 13 error inesperado: ' + ERROR_MESSAGE(); END
END CATCH

/* ---------- Resumen ----------------------------------------------------------------------- */
IF @fallas = 0
    PRINT N'==> Todas las pruebas pasaron.';
ELSE
    THROW 50099, N'Fallaron pruebas de la Fase 0 (ver mensajes).', 1;
GO
