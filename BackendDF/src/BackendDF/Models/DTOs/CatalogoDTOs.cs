namespace BackendDF.Models.DTOs
{
    /// <summary>Par id ↔ texto del origen (sector, análisis, crédito).</summary>
    public sealed record ItemCatalogoDTO(string Id, string Nombre);

    /// <summary>Desde/hasta de una cobertura temporal.</summary>
    public sealed record RangoPeriodosDTO(string Desde, string Hasta)
    {
        public static RangoPeriodosDTO? De(string? desde, string? hasta) =>
            desde is null || hasta is null ? null : new RangoPeriodosDTO(desde, hasta);

        public static RangoPeriodosDTO? De(IReadOnlyList<string> periodos) =>
            periodos.Count == 0 ? null : new RangoPeriodosDTO(periodos[0], periodos[^1]);
    }

    /// <summary><c>GET /api/catalogos</c> (contrato §5).</summary>
    public sealed record CatalogosDTO(
        IReadOnlyList<ItemCatalogoDTO> Sectores,
        IReadOnlyList<ItemCatalogoDTO> Analisis,
        IReadOnlyList<ItemCatalogoDTO> Creditos,
        IReadOnlyList<string> Tamanos,
        IReadOnlyList<string> RangosActivos,
        IReadOnlyList<string> Provincias);

    /// <summary>Elemento de <c>GET /api/entidades</c>.</summary>
    public sealed record EntidadDTO(
        string Id,
        string Nombre,
        string Tipo,
        string? Tamano,
        string? Rango,
        string? Provincia,
        bool TieneBalance,
        RangoPeriodosDTO? Periodos);
}
