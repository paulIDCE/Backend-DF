/* =====================================================================================
   Fase 0 · 00 — Esquema `api` y rol de solo lectura para el backend
   Base: idce_bco_coop (SQL Server 2022)

   - El esquema `api` agrupa todo lo que consume el backend (vistas + procedimientos).
     Su dueño es dbo: requisito de la vista indexada (mismo dueño que las tablas) y
     permite el encadenamiento de propiedad, así el usuario de la API NO necesita
     permisos sobre las tablas dbo.
   - Idempotente: se puede ejecutar varias veces.
   ===================================================================================== */
USE idce_bco_coop;
GO

IF SCHEMA_ID(N'api') IS NULL
    EXEC (N'CREATE SCHEMA api AUTHORIZATION dbo;');
GO

IF DATABASE_PRINCIPAL_ID(N'api_lectura') IS NULL
    CREATE ROLE api_lectura AUTHORIZATION dbo;
GO

-- Solo ejecutar procedimientos y leer vistas del esquema api. Nada sobre dbo.
GRANT EXECUTE ON SCHEMA::api TO api_lectura;
GRANT SELECT  ON SCHEMA::api TO api_lectura;
GO

/* -------------------------------------------------------------------------------------
   Usuario de la API (ejecutar UNA vez, a mano, con una contraseña generada por ustedes).
   No dejar contraseñas en este archivo ni en el repositorio: el backend la lee de
   user-secrets / variables de entorno (ConnectionStrings:BcoCoop).

   USE master;
   CREATE LOGIN backend_df WITH PASSWORD = N'<contraseña fuerte>', CHECK_POLICY = ON;
   GO
   USE idce_bco_coop;
   CREATE USER backend_df FOR LOGIN backend_df WITH DEFAULT_SCHEMA = api;
   ALTER ROLE api_lectura ADD MEMBER backend_df;
   GO

   Recordatorio de seguridad (informe §3bis.3, E7): dbo.sp_etl_bce contiene en sus
   comentarios la contraseña de `sa` en texto plano. Rotar esa contraseña y quitarla del SP.
   ------------------------------------------------------------------------------------- */

PRINT N'00_esquema_seguridad: OK';
GO
