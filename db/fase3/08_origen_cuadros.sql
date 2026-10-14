/* =====================================================================================
   Fase 3 · 08 — De dónde salen los datos de cada cuadro (para GET /api/meta)

   api.ListarCuadros devuelve dos columnas más, al final (el backend lee por nombre):
     FilasBd    filas con fórmula (Calculo SUMA / RATIO): las calcula la BD
     FilasJson  filas pendientes (Calculo JSON): toman su valor de los archivos JSON
   Se cuentan sobre todas las variantes de plantilla. El backend clasifica cada cuadro:
   FilasJson = 0 → "bd"; FilasBd = 0 → "json"; si no, "mixto".
   Requiere fase 2. Idempotente.
   ===================================================================================== */
USE idce_bco_coop;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE api.ListarCuadros
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.CuadroID, c.Titulo, c.Unidad, c.Ambito, c.Tipo, c.Frecuencia, c.FechasJson, c.Orden,
           (SELECT STRING_AGG(v.Variante, '|') WITHIN GROUP (ORDER BY v.Variante)
            FROM (SELECT DISTINCT Variante FROM dbo.CuadroFila WHERE CuadroID = c.CuadroID) AS v) AS Variantes,
           (SELECT COUNT(*) FROM dbo.CuadroFila AS f WHERE f.CuadroID = c.CuadroID AND f.Calculo IN ('SUMA', 'RATIO')) AS FilasBd,
           (SELECT COUNT(*) FROM dbo.CuadroFila AS f WHERE f.CuadroID = c.CuadroID AND f.Calculo = 'JSON') AS FilasJson
    FROM dbo.Cuadro AS c
    WHERE c.Activo = 1
    ORDER BY c.Orden;
END
GO

PRINT N'08_origen_cuadros: OK';
GO
