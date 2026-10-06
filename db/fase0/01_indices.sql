/* =====================================================================================
   Fase 0 · 01 — Índices de lectura (no agregan información, solo optimizan)

   B11 tiene PK clustered (FechaID, IFIID, CuentaID): sirve para leer un mes completo,
   pero leer UNA entidad en todos sus meses obliga a recorrer los 15,8 M de filas.
   El backend lee casi siempre por entidad → índice que empieza por IFIID.

   Tiempo estimado de creación: 1–3 min (B11 ≈ 15,8 M filas). PAGE compression reduce
   bastante el tamaño porque ~83 % de los saldos son 0.
   Idempotente.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET NOCOUNT ON;
GO

-- 1) Saldos por entidad: api.ObtenerSaldosEntidad / api.ObtenerSaldosEntidades
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.B11') AND name = N'IX_B11_IFIID_Cuenta_Fecha')
BEGIN
    CREATE NONCLUSTERED INDEX IX_B11_IFIID_Cuenta_Fecha
        ON dbo.B11 (IFIID, CuentaID, FechaID)
        INCLUDE (Saldo)
        WITH (DATA_COMPRESSION = PAGE, SORT_IN_TEMPDB = ON);
        -- En una edición Enterprise/Developer en producción se puede agregar ONLINE = ON.
    PRINT N'Creado IX_B11_IFIID_Cuenta_Fecha';
END
GO

-- 2) Indicadores por entidad: api.ObtenerIndicadoresEntidad
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.IndicadorData') AND name = N'IX_IndicadorData_IFIID')
BEGIN
    CREATE NONCLUSTERED INDEX IX_IndicadorData_IFIID
        ON dbo.IndicadorData (IFIID, IndicadorFinID, FechaID)
        INCLUDE (Valor);
    PRINT N'Creado IX_IndicadorData_IFIID';
END
GO

-- 3) RUC único en Ifi: el backend valida su mapa (slug → IFIID) contra el RUC.
--    El ETL ya inserta solo RUC nuevos; esto lo garantiza. Si hubiera duplicados, se aborta.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.Ifi') AND name = N'UX_Ifi_Ruc')
BEGIN
    IF EXISTS (SELECT Ruc FROM dbo.Ifi GROUP BY Ruc HAVING COUNT(*) > 1)
        THROW 50001, N'dbo.Ifi tiene RUC duplicados: revisar antes de crear UX_Ifi_Ruc.', 1;

    CREATE UNIQUE NONCLUSTERED INDEX UX_Ifi_Ruc ON dbo.Ifi (Ruc);
    PRINT N'Creado UX_Ifi_Ruc';
END
GO

PRINT N'01_indices: OK';
GO
