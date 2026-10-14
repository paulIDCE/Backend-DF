/* =====================================================================================
   Fase 2 · 99 — Revertir el catálogo de cuadros (procedimientos, funciones y tablas)
   No toca B11, Ifi, Cuenta, IndicadorData ni los objetos de la Fase 0.
   ===================================================================================== */
USE idce_bco_coop;
GO

DROP PROCEDURE IF EXISTS api.ObtenerCuadroSector;
DROP PROCEDURE IF EXISTS api.ObtenerCuadroEntidad;
DROP PROCEDURE IF EXISTS api.ObtenerValoresCuadroSector;
DROP PROCEDURE IF EXISTS api.ObtenerValoresCuadroEntidades;
DROP PROCEDURE IF EXISTS api.ObtenerPlantilla;
DROP PROCEDURE IF EXISTS api.ListarCuadros;
DROP FUNCTION  IF EXISTS api.fnFilasCuadro;
DROP FUNCTION  IF EXISTS api.fnResolverVariante;
GO

DROP TABLE IF EXISTS dbo.CuadroFilaTermino;
DROP TABLE IF EXISTS dbo.CuadroFila;
DROP TABLE IF EXISTS dbo.Cuadro;
GO

PRINT N'99_rollback (fase 2): OK';
GO
