/* =====================================================================================
   Fase 0 · 99 — Revertir todo lo creado por 00–03 (no toca datos ni el ETL)
   ===================================================================================== */
USE idce_bco_coop;
GO

DROP PROCEDURE IF EXISTS api.ObtenerIndicadoresEntidad;
DROP PROCEDURE IF EXISTS api.ObtenerSaldosAgregado;
DROP PROCEDURE IF EXISTS api.ObtenerSaldosEntidades;
DROP PROCEDURE IF EXISTS api.ObtenerSaldosEntidad;
DROP PROCEDURE IF EXISTS api.ListarCuentas;
DROP PROCEDURE IF EXISTS api.ListarIfi;
DROP PROCEDURE IF EXISTS api.ObtenerVentana;
GO

DROP VIEW IF EXISTS api.vVentana;
DROP VIEW IF EXISTS api.vSaldoAgregado;   -- borra también su índice clustered
GO

DROP INDEX IF EXISTS IX_B11_IFIID_Cuenta_Fecha ON dbo.B11;
DROP INDEX IF EXISTS IX_IndicadorData_IFIID    ON dbo.IndicadorData;
DROP INDEX IF EXISTS UX_Ifi_Ruc                ON dbo.Ifi;
GO

-- Rol y esquema (el rol debe quedar sin miembros; el esquema, vacío).
IF DATABASE_PRINCIPAL_ID(N'api_lectura') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id = DATABASE_PRINCIPAL_ID(N'api_lectura'))
        PRINT N'api_lectura tiene miembros: quitarlos (ALTER ROLE api_lectura DROP MEMBER …) y volver a ejecutar.';
    ELSE
        DROP ROLE api_lectura;
END
GO

IF SCHEMA_ID(N'api') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(N'api'))
    DROP SCHEMA api;
GO

PRINT N'99_rollback: OK';
GO
