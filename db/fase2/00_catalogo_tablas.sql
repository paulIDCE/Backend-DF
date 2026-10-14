/* =====================================================================================
   Fase 2 · 00 — Tablas del catálogo de cuadros (configuración del reporte)

   El catálogo define cada cuadro: qué filas lleva, en qué orden, con qué encabezado,
   nivel y texto, y cómo se calcula el valor de cada fila. Reemplaza al JSON como
   "plantilla". Se siembra una vez con 01_semilla_catalogo.sql (generado) y desde ahí lo
   mantiene el equipo.

   dbo.Cuadro             un cuadro (EFI01, SFN06, REP01…)
   dbo.CuadroFila         una fila de un cuadro, por variante de plantilla y sub-vista (IdSub)
   dbo.CuadroFilaTermino  los términos con que se calcula la fila (cuenta o indicador × factor)

   Calculo de la fila:
     SUMA   Σ(factor × valor de N) × Escala           una cuenta, un indicador o sumas/restas
     RATIO  Σ(factor × N) / Σ(factor × D) × Escala     indicadores; D = 0 → sin valor
     JSON   fórmula aún no definida: en la transición el backend toma el valor del JSON
            (el orden y el texto igual salen de aquí)

   Variante (plantillas distintas según a quién se aplica el cuadro):
     '*'                       igual para todos
     'BP' | 'COOP' | 'MU'      cuadros de entidad: Ifi.TipoEntidad
     'BP:2', 'COOP:3', 'BP,COOP,MU' …  cuadros de sector: tipos[:segmentos] que suma el sector
   Los SP buscan la variante exacta, luego solo los tipos (antes de ':') y luego '*'.

   Idempotente: no borra datos si las tablas ya existen.
   ===================================================================================== */
USE idce_bco_coop;
GO

IF OBJECT_ID(N'dbo.Cuadro', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cuadro
    (
        CuadroID        varchar(16)    NOT NULL CONSTRAINT PK_Cuadro PRIMARY KEY,
        Titulo          nvarchar(200)  NOT NULL,
        Unidad          nvarchar(100)  NULL,
        Ambito          varchar(10)    NOT NULL CONSTRAINT CK_Cuadro_Ambito CHECK (Ambito IN ('ENTIDAD', 'SECTOR')),
        Tipo            varchar(20)    NOT NULL CONSTRAINT CK_Cuadro_Tipo CHECK
                        (Tipo IN ('Entidad', 'CarteraEntidad', 'BalancesEntidad', 'Reporte', 'Sistema', 'Cartera', 'Balances')),
        Frecuencia      varchar(12)    NOT NULL CONSTRAINT DF_Cuadro_Frecuencia DEFAULT ('mensual'),
        -- 1 = no sale del .sav: conserva las fechas de su fuente (convención C1).
        FechasJson      bit            NOT NULL CONSTRAINT DF_Cuadro_FechasJson DEFAULT (0),
        -- 1 = el texto de las filas de cuenta sale de dbo.Cuenta; 0 = se usa CuadroFila.Etiqueta.
        UsaNombreCuenta bit            NOT NULL CONSTRAINT DF_Cuadro_UsaNombreCuenta DEFAULT (1),
        Orden           int            NOT NULL,
        Activo          bit            NOT NULL CONSTRAINT DF_Cuadro_Activo DEFAULT (1)
    );
    PRINT N'Creada dbo.Cuadro';
END
GO

IF OBJECT_ID(N'dbo.CuadroFila', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CuadroFila
    (
        CuadroFilaID    int            NOT NULL CONSTRAINT PK_CuadroFila PRIMARY KEY,
        CuadroID        varchar(16)    NOT NULL CONSTRAINT FK_CuadroFila_Cuadro REFERENCES dbo.Cuadro (CuadroID),
        Variante        varchar(20)    NOT NULL CONSTRAINT DF_CuadroFila_Variante DEFAULT ('*'),
        -- Sub-vista: tipo de análisis ('Saldo Millones USD'…) o de crédito ('Productivo'…); '' si no aplica.
        IdSub           nvarchar(80)   NOT NULL CONSTRAINT DF_CuadroFila_IdSub DEFAULT (N''),
        Orden           int            NOT NULL,
        Codigo          varchar(64)    NULL,      -- código publicado: '@1101', 'pasipat', 'SOLVENCIA'…
        CodigoBase      varchar(16)    NULL,      -- código de cuenta en los balances (árbol)
        CuentaID        int            NULL,      -- cuenta que representa la fila (para su texto)
        Grupo           nvarchar(300)  NULL,      -- encabezado del bloque
        Etiqueta        nvarchar(400)  NULL,      -- texto propio (filas no-cuenta) y respaldo del de dbo.Cuenta
        Nivel           tinyint        NOT NULL CONSTRAINT DF_CuadroFila_Nivel DEFAULT (0),
        SegmentoCredito nvarchar(80)   NULL,
        Unidad          nvarchar(100)  NULL,      -- NULL = la del cuadro
        Calculo         varchar(8)     NOT NULL CONSTRAINT CK_CuadroFila_Calculo CHECK (Calculo IN ('SUMA', 'RATIO', 'JSON')),
        Escala          float          NOT NULL CONSTRAINT DF_CuadroFila_Escala DEFAULT (1),
        CONSTRAINT UX_CuadroFila UNIQUE (CuadroID, Variante, IdSub, Orden)
    );
    PRINT N'Creada dbo.CuadroFila';
END
GO

IF OBJECT_ID(N'dbo.CuadroFilaTermino', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CuadroFilaTermino
    (
        CuadroFilaTerminoID int        NOT NULL IDENTITY (1, 1) CONSTRAINT PK_CuadroFilaTermino PRIMARY KEY,
        CuadroFilaID    int            NOT NULL CONSTRAINT FK_CuadroFilaTermino_Fila
                                       REFERENCES dbo.CuadroFila (CuadroFilaID) ON DELETE CASCADE,
        Rol             char(1)        NOT NULL CONSTRAINT DF_CuadroFilaTermino_Rol DEFAULT ('N')
                                       CONSTRAINT CK_CuadroFilaTermino_Rol CHECK (Rol IN ('N', 'D')),
        Fuente          varchar(10)    NOT NULL CONSTRAINT CK_CuadroFilaTermino_Fuente CHECK (Fuente IN ('CUENTA', 'INDICADOR')),
        CuentaID        int            NULL,      -- dbo.B11.CuentaID
        IndicadorCodigo varchar(32)    NULL,      -- dbo.IndicadorFin.Codigo
        Factor          float          NOT NULL CONSTRAINT DF_CuadroFilaTermino_Factor DEFAULT (1),
        CONSTRAINT CK_CuadroFilaTermino_Origen CHECK
            ((Fuente = 'CUENTA' AND CuentaID IS NOT NULL AND IndicadorCodigo IS NULL)
          OR (Fuente = 'INDICADOR' AND IndicadorCodigo IS NOT NULL AND CuentaID IS NULL))
    );
    PRINT N'Creada dbo.CuadroFilaTermino';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CuadroFilaTermino') AND name = N'IX_CuadroFilaTermino_Fila')
    CREATE NONCLUSTERED INDEX IX_CuadroFilaTermino_Fila
        ON dbo.CuadroFilaTermino (CuadroFilaID)
        INCLUDE (Rol, Fuente, CuentaID, IndicadorCodigo, Factor);
GO

PRINT N'00_catalogo_tablas: OK';
GO
