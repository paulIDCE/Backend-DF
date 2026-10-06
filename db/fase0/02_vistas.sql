/* =====================================================================================
   Fase 0 · 02 — Vistas de optimización

   api.vSaldoAgregado  (VISTA INDEXADA)
     Saldos sumados por mes × TipoEntidad × Segmento × cuenta. SQL Server la mantiene
     sola con cada INSERT/DELETE del ETL; no se agrega información nueva ni se toca el ETL.
     ≈ 32 meses × 11 grupos × 1 532 cuentas ≈ 0,5 M filas.
     El backend suma los grupos que forman cada sector (p. ej. "Bancos Privados
     Grandes" = BP/1); la BD no conoce los nombres de los sectores.
     Filas = número de entidades que aportan al grupo (B11 tiene 1 fila por
     mes × entidad × cuenta).

   api.vVentana
     Ventana de datos de la BD (convención C1) y una firma que cambia con cada recarga
     (nuevo mes, filas distintas o saldos corregidos). El backend la usa para invalidar
     cachés y el ETag.

   Requisitos de la vista indexada: SCHEMABINDING, nombres de dos partes, mismo dueño
   (dbo) y estas opciones SET en la sesión que la crea y en las que modifican B11/Ifi
   (sp_etl_bce ya fue creado con ANSI_NULLS y QUOTED_IDENTIFIER ON; ODBC/SqlClient
   usan los valores correctos por defecto).

   OJO: CREATE OR ALTER sobre una vista indexada borra su índice; por eso el índice se
   recrea a continuación si no existe. Idempotente.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

CREATE OR ALTER VIEW api.vSaldoAgregado
WITH SCHEMABINDING
AS
SELECT
    b.FechaID,
    i.TipoEntidad,
    i.Segmento,
    b.CuentaID,
    SUM(ISNULL(b.Saldo, 0)) AS Saldo,   -- en una vista indexada SUM no admite expresiones NULL
    COUNT_BIG(*)            AS Filas
FROM dbo.B11 AS b
INNER JOIN dbo.Ifi AS i ON i.IFIID = b.IFIID
GROUP BY b.FechaID, i.TipoEntidad, i.Segmento, b.CuentaID;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'api.vSaldoAgregado') AND name = N'CX_vSaldoAgregado')
BEGIN
    CREATE UNIQUE CLUSTERED INDEX CX_vSaldoAgregado
        ON api.vSaldoAgregado (TipoEntidad, Segmento, CuentaID, FechaID)
        WITH (DATA_COMPRESSION = PAGE, SORT_IN_TEMPDB = ON);
    PRINT N'Creado CX_vSaldoAgregado';
END
GO

CREATE OR ALTER VIEW api.vVentana
AS
WITH rango AS (
    -- MIN/MAX por la PK de B11 (FechaID primero): búsqueda en los extremos, sin recorrer.
    SELECT MIN(FechaID) AS DesdeFechaID, MAX(FechaID) AS HastaFechaID
    FROM dbo.B11
),
contenido AS (
    -- Sobre la vista indexada (≈ 0,5 M filas) en vez de B11 (15,8 M).
    SELECT
        COUNT(DISTINCT v.FechaID)                               AS MesesConDatos,
        SUM(v.Filas)                                            AS FilasB11,
        CHECKSUM_AGG(CHECKSUM(v.FechaID, v.CuentaID, v.Saldo))  AS SumaControl
    FROM api.vSaldoAgregado AS v WITH (NOEXPAND)
)
SELECT
    CONVERT(char(7), DATEADD(day, r.DesdeFechaID, '19500101'), 126)                    AS Desde,
    CONVERT(char(7), DATEADD(day, r.HastaFechaID, '19500101'), 126)                    AS Hasta,
    r.DesdeFechaID,
    r.HastaFechaID,
    DATEDIFF(month, DATEADD(day, r.DesdeFechaID, '19500101'),
                    DATEADD(day, r.HastaFechaID, '19500101')) + 1                      AS Meses,
    c.MesesConDatos,
    c.FilasB11,
    CONCAT(r.HastaFechaID, '-', c.FilasB11, '-', c.SumaControl)                        AS Firma
FROM rango AS r
CROSS JOIN contenido AS c;
GO

PRINT N'02_vistas: OK';
GO
