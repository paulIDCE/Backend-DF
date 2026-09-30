namespace BackendDF.Models.Entities
{
    /// <summary>
    /// Entidad financiera del catálogo (<c>entidades_lista.json</c>, R5) con los metadatos de la
    /// primera fila de su reporte (R6).
    /// </summary>
    public sealed class EntidadCatalogo
    {
        /// <summary>Identidad: el <c>archivo</c> del catálogo (p. ej. <c>BP__PICHINCHA</c>).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Nombre para mostrar (<c>BP. PICHINCHA</c>).</summary>
        public string Nombre { get; init; } = string.Empty;

        /// <summary>"banco" | "cooperativa" | "mutualista" (por prefijo BP./COAC./MUT.).</summary>
        public string Tipo { get; init; } = string.Empty;

        public string? Tamano { get; init; }
        public string? Rango { get; init; }
        public string? Provincia { get; init; }
        public bool TieneBalance { get; init; }
        public string? PeriodoDesde { get; init; }
        public string? PeriodoHasta { get; init; }
    }
}
