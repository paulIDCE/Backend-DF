/* =====================================================================================
   Fase 3 · LocalDB · 00 — Base mínima para probar los scripts SIN tocar el servidor

   Crea idce_bco_coop en (localdb)\MSSQLLocalDB con las 5 tablas de origen y datos
   sintéticos pensados para calcular a mano los valores esperados (01_pruebas_tiempo.sql).
   Uso (ver db/fase3/README.md): este script, luego fase0 00‑03, fase2 00‑02, fase3 01, 03 y 04
   (dos veces), luego 01_pruebas_tiempo.sql y al final 99_borrar.sql.

   Datos (montos en USD; k = 0 en 2024‑01 … 25 en 2026‑02):
     IFIID 1001 BP seg. 1, 1002 COOP seg. 1 (= 2 × 1001), 1003 MU (= 1001, sin 2025‑03)
     cuenta 1 activo        (100 + k) M        cuenta 3 patrimonio   (20 + k) M
     cuenta 14 cartera      ( 50 + k) M        cuenta 1499 provisión  1 M   → IF011 = 49 + k
     cuentas 4 / 41 gastos  mes(t) M           cuentas 5 / 51 ingresos 2·mes(t) M
       → Gan_Eje = 51 − 41 = mes(t) M (acumulado del año) y anualizado = 12 M en todo mes
     cuenta 1401 cartera productiva por vencer (30 + k) M, 1449 vencida 1 M → Por_vencer = 30 + k
     indicador ACT_PON_RIESGO = 1 000 M (solo 1001) → PTR_9 = 90 M
   ===================================================================================== */
USE master;
GO
IF DB_ID(N'idce_bco_coop') IS NOT NULL
BEGIN
    ALTER DATABASE idce_bco_coop SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE idce_bco_coop;
END
GO
CREATE DATABASE idce_bco_coop;
GO
USE idce_bco_coop;
GO
SET NOCOUNT ON;

CREATE TABLE dbo.Ifi (IFIID int NOT NULL PRIMARY KEY, Ruc varchar(15) NOT NULL, Nombre varchar(256) NULL,
                      TipoEntidad varchar(8) NULL, Segmento smallint NULL);
CREATE TABLE dbo.Cuenta (CuentaID numeric(18, 0) NOT NULL PRIMARY KEY, Nombre varchar(256) NULL);
CREATE TABLE dbo.B11 (FechaID int NOT NULL, IFIID int NOT NULL, CuentaID int NOT NULL, Saldo money NULL,
                      CONSTRAINT PK_B11Seps PRIMARY KEY (FechaID, IFIID, CuentaID));
CREATE TABLE dbo.IndicadorFin (IndicadorFinID int NOT NULL PRIMARY KEY, Codigo varchar(32) NULL, Nombre varchar(256) NULL);
CREATE TABLE dbo.IndicadorData (FechaID int NOT NULL, IFIID int NOT NULL, IndicadorFinID int NOT NULL, Valor float NULL,
                                PRIMARY KEY (FechaID, IFIID, IndicadorFinID));

INSERT INTO dbo.Ifi VALUES (1001, '1001', 'BANCO PRUEBA', 'BP', 1), (1002, '1002', 'COAC PRUEBA', 'COOP', 1),
                           (1003, '1003', 'MUTUALISTA PRUEBA', 'MU', NULL);
INSERT INTO dbo.Cuenta VALUES (1, 'ACTIVO'), (3, 'PATRIMONIO'), (4, 'GASTOS'), (5, 'INGRESOS'), (14, 'CARTERA DE CREDITOS'),
                              (41, 'Intereses causados'), (51, 'Intereses y descuentos ganados'), (1499, 'Provisiones'),
                              (1401, 'Cartera productiva por vencer'), (1449, 'Cartera productiva vencida');
INSERT INTO dbo.IndicadorFin VALUES (1, 'SOLVENCIA', 'Solvencia'), (2, 'ACT_PON_RIESGO', 'ACT_PON_RIESGO');

DECLARE @k int = 0;
WHILE @k <= 25
BEGIN
    DECLARE @fecha date = EOMONTH(DATEADD(month, @k, '20240101'));
    DECLARE @f int = DATEDIFF(day, '19500101', @fecha), @mes int = MONTH(@fecha);
    INSERT INTO dbo.B11 (FechaID, IFIID, CuentaID, Saldo)
    SELECT @f, e.IFIID, c.CuentaID, c.Valor * e.Factor
    FROM (VALUES (1001, 1), (1002, 2), (1003, 1)) AS e (IFIID, Factor)
    CROSS JOIN (VALUES (1, (100 + @k) * 1e6), (3, (20 + @k) * 1e6), (14, (50 + @k) * 1e6), (1499, 1e6),
                       (4, @mes * 1e6), (41, @mes * 1e6), (5, 2 * @mes * 1e6), (51, 2 * @mes * 1e6),
                       (1401, (30 + @k) * 1e6), (1449, 1e6)) AS c (CuentaID, Valor)
    WHERE NOT (e.IFIID = 1003 AND @fecha = '20250331');   -- hueco: la mutualista no reporta 2025‑03
    INSERT INTO dbo.IndicadorData VALUES (@f, 1001, 1, 0.15), (@f, 1001, 2, 1e9);
    SET @k += 1;
END
PRINT N'00_base_minima: OK';
GO
