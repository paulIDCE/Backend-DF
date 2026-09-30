namespace BackendDF.Models.DTOs
{
    /// <summary>Resultado junto con los códigos pedidos que no existen (header <c>X-Codigos-No-Encontrados</c>).</summary>
    public sealed record ConFaltantes<T>(T Resultado, IReadOnlyList<string> NoEncontrados);

    // ---- Reporte por entidad (contrato §7) ----

    public sealed record EntidadReporteDTO(string Id, string Nombre, string? Tamano, string? Rango, string? Provincia);

    public sealed record CuentaReporteDTO(string Cuc, string? Variable, double?[] Valores);

    /// <summary><c>GET /api/entidades/{id}/reporte</c>.</summary>
    public sealed record ReporteDTO(EntidadReporteDTO Entidad, IReadOnlyList<string> Periodos, IReadOnlyList<CuentaReporteDTO> Cuentas);

    // ---- Rankings (contrato §8) ----

    public sealed record AgrupacionRankingDTO(string Tipo, string? Valor);

    public sealed record TotalRankingDTO(double Anterior, double Actual);

    public sealed record FilaRankingDTO(
        int Posicion,
        string EntidadId,
        string Nombre,
        double Anterior,
        double Actual,
        double ParticipacionAnterior,
        double ParticipacionActual);

    /// <summary><c>GET /api/rankings</c>.</summary>
    public sealed record RankingDTO(
        string Cuenta,
        string Fecha,
        string FechaComparacion,
        AgrupacionRankingDTO Agrupacion,
        TotalRankingDTO Total,
        int? PosicionEntidad,
        IReadOnlyList<FilaRankingDTO> Filas);

    // ---- Series para comparar (contrato §9) ----

    public sealed record SerieEntidadDTO(string EntidadId, string Codigo, string? Variable, double?[] Valores);

    /// <summary><c>GET /api/entidades/series</c>.</summary>
    public sealed record SeriesEntidadesDTO(IReadOnlyList<string> Periodos, IReadOnlyList<SerieEntidadDTO> Series);

    public sealed record SerieSistemaDTO(
        string Sector,
        string SectorNombre,
        string Codigo,
        string Cuadro,
        string? Variable,
        double?[] Valores);

    /// <summary><c>GET /api/sistema/series</c>.</summary>
    public sealed record SeriesSistemaDTO(IReadOnlyList<string> Periodos, IReadOnlyList<SerieSistemaDTO> Series);

    // ---- Metadatos (contrato §10) ----

    public sealed record FuenteDTO(
        string Fuente,
        string? Archivo,
        int? Archivos,
        string? Desde,
        string? Hasta,
        DateTime? Modificado);

    /// <summary><c>GET /api/meta</c>.</summary>
    public sealed record MetaDTO(
        DateTime VersionDatos,
        IReadOnlyList<FuenteDTO> Fuentes,
        string? UltimoCorteComun,
        IReadOnlyList<string> Advertencias);
}
