/* Fase 3 · LocalDB · 99 — Borra la base de prueba de LocalDB (nunca ejecutar contra el servidor). */
USE master;
GO
IF SERVERPROPERTY('IsLocalDB') = 1 AND DB_ID(N'idce_bco_coop') IS NOT NULL
BEGIN
    ALTER DATABASE idce_bco_coop SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE idce_bco_coop;
    PRINT N'99_borrar: base de LocalDB borrada';
END
ELSE
    PRINT N'99_borrar: no es LocalDB o no hay base; no se hizo nada';
GO
