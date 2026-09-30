namespace BackendDF.Models.Entities
{
    /// <summary>Estado de carga de la fuente de datos (alimenta <c>/api/health</c>).</summary>
    public enum EstadoFuente
    {
        /// <summary>Cargando los archivos base.</summary>
        Cargando,

        /// <summary>Base lista; el índice de reportes se sigue construyendo.</summary>
        IndexandoReportes,

        /// <summary>Todo cargado.</summary>
        Lista,

        /// <summary>No se pudo cargar (p. ej. falta RutaBase).</summary>
        Error
    }

    /// <summary>Una fuente (archivo o carpeta) con su cobertura temporal.</summary>
    public sealed class InfoFuente
    {
        public string Fuente { get; init; } = string.Empty;
        public string? Archivo { get; init; }
        public int? Archivos { get; init; }
        public string? Desde { get; init; }
        public string? Hasta { get; init; }
        public DateTime? Modificado { get; init; }
    }

    /// <summary>Metadatos de los datos cargados (contrato §10).</summary>
    public sealed class InfoFuentes
    {
        public DateTime VersionDatos { get; init; }
        public IReadOnlyList<InfoFuente> Fuentes { get; init; } = [];
        public string? UltimoCorteComun { get; init; }
        public IReadOnlyList<string> Advertencias { get; init; } = [];
    }
}
