namespace BackendDF.Configuration
{
    /// <summary>Bindea la sección "Datos" de appsettings.json (fuente JSON, contrato §3).</summary>
    public class DatosSettings
    {
        /// <summary>Carpeta con los JSON. Absoluta o relativa a la raíz del proyecto (ContentRoot).</summary>
        public string RutaBase { get; set; } = "Database";

        /// <summary>Máximo de archivos por entidad (entidades/ y balances/) retenidos en memoria (LRU).</summary>
        public int MaxEntidadesEnCache { get; set; } = 40;

        /// <summary>Fuente SQL (idce_bco_coop) para la implementación híbrida. Ver <c>docs/informe-sav-a-sql.md</c> §8.</summary>
        public SqlSettings Sql { get; set; } = new();
    }

    /// <summary>
    /// Sección "Datos:Sql". Las listas vacías usan los valores por defecto de
    /// <see cref="Data.FuenteSql.ReglasCuadro"/>.
    /// </summary>
    public class SqlSettings
    {
        /// <summary>false: la API funciona solo con los JSON, igual que antes de la Fase 1.</summary>
        public bool Habilitado { get; set; }

        /// <summary>Nombre de la cadena en "ConnectionStrings".</summary>
        public string ConnectionStringName { get; set; } = "DefaultConnection";

        /// <summary>Timeout de cada procedimiento (la carga masiva de reportes es la más pesada).</summary>
        public int TimeoutSegundos { get; set; } = 60;

        /// <summary>Cada cuánto se consulta <c>api.ObtenerVentana</c> para detectar una recarga del ETL.</summary>
        public int MinutosRevisionVentana { get; set; } = 10;

        /// <summary>Tras un fallo de SQL se sirve solo JSON durante este tiempo antes de reintentar.</summary>
        public int SegundosCircuitoAbierto { get; set; } = 60;

        /// <summary>Mapa slug del backend → IFIID/RUC. Relativo a la raíz del proyecto (ContentRoot).</summary>
        public string RutaMapaEntidades { get; set; } = "Data/FuenteSql/mapa-entidades.json";

        /// <summary>Cuadros cuyas filas de cuenta (y los indicadores de EFI05) se leen de SQL.</summary>
        public string[] CuadrosSql { get; set; } = [];

        /// <summary>Cuadros que conservan las fechas del JSON (no salen del .sav). Los macro siempre.</summary>
        public string[] CuadrosFechasJson { get; set; } = [];

        /// <summary>Grupos de cuenta que no existen en la BD (convención C2): sus filas se ocultan.</summary>
        public string[] GruposOmitidos { get; set; } = [];
    }
}
