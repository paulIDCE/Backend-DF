namespace BackendDF.Models.DTOs
{
    /// <summary>Elemento de <c>GET /api/cuadros</c> (contrato §6.1).</summary>
    public sealed record CuadroResumenDTO(
        string Id,
        string Titulo,
        string Origen,
        string Tipo,
        string Frecuencia,
        string Vista,
        IReadOnlyList<string> Parametros,
        RangoPeriodosDTO? Periodos);

    /// <summary>Parámetros de consulta de <c>GET /api/cuadros/{id}</c>.</summary>
    public sealed record ParametrosCuadro(
        string? Sector,
        string? Entidad,
        string? Analisis,
        string? Credito,
        string? Desde,
        string? Hasta,
        bool Notas = true);

    /// <summary>Filtros con que se resolvió el cuadro.</summary>
    public sealed record ContextoCuadroDTO(
        string? Sector,
        string? SectorNombre,
        string? Entidad,
        string? EntidadNombre,
        string? Analisis,
        string? AnalisisNombre,
        string? Credito,
        string? CreditoNombre);

    /// <summary>Una fila del cuadro con sus valores alineados a <c>periodos</c>.</summary>
    public sealed record FilaCuadroDTO(
        int Indice,
        string Clave,
        string? Grupo,
        string? Variable,
        string? Cuc,
        string? CodigoBase,
        int Nivel,
        int? Padre,
        double?[] Valores);

    /// <summary><c>GET /api/cuadros/{id}</c> (contrato §6.3).</summary>
    public sealed record CuadroDTO(
        string Id,
        string Titulo,
        string? Unidad,
        string Tipo,
        string Frecuencia,
        string Vista,
        ContextoCuadroDTO Contexto,
        RangoPeriodosDTO? PeriodosDisponibles,
        IReadOnlyList<string> Periodos,
        IReadOnlyList<FilaCuadroDTO> Filas,
        IReadOnlyList<string> Notas);
}
