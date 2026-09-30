namespace BackendDF.Configuration
{
    /// <summary>Bindea la sección "Cors" de appsettings.json.</summary>
    public class CorsSettings
    {
        /// <summary>Orígenes permitidos. Solo estos pueden llamar a la API desde el navegador.</summary>
        public string[] Origenes { get; set; } = [];
    }
}
