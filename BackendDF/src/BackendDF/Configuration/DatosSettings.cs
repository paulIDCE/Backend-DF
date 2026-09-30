namespace BackendDF.Configuration
{
    /// <summary>Bindea la sección "Datos" de appsettings.json (fuente JSON, contrato §3).</summary>
    public class DatosSettings
    {
        /// <summary>Carpeta con los JSON. Absoluta o relativa a la raíz del proyecto (ContentRoot).</summary>
        public string RutaBase { get; set; } = "Database";

        /// <summary>Máximo de archivos por entidad (entidades/ y balances/) retenidos en memoria (LRU).</summary>
        public int MaxEntidadesEnCache { get; set; } = 40;
    }
}
