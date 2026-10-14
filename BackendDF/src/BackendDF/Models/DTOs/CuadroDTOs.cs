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
    /// <param name="FuenteDatos">De dónde salen sus valores: "bd", "json" o null (sin fuente). No es el <c>origen</c> de la lista de cuadros (macro / sistema / entidad).</param>
    /// <param name="SinHistoriaHasta">
    /// Período (inclusive) hasta el que un <c>null</c> en <c>valores</c> significa "falta la historia
    /// previa en la BD" (tasas a 12 meses, variación mensual, promedios del año) y no un error de
    /// cálculo; los <c>null</c> posteriores son "sin dato". null si no aplica o queda antes de <c>periodos</c>.
    /// </param>
    public sealed record FilaCuadroDTO(
        int Indice,
        string Clave,
        string? Grupo,
        string? Variable,
        string? Cuc,
        string? CodigoBase,
        int Nivel,
        int? Padre,
        double?[] Valores,
        string? SinHistoriaHasta = null,
        string? FuenteDatos = null);

    /// <summary>Cuántas filas salen de cada fuente (<c>sinFuente</c> = pendiente del catálogo sin fila en el JSON).</summary>
    public sealed record FilasPorFuenteDTO(int Bd, int Json, int SinFuente);

    /// <summary><c>GET /api/cuadros/{id}</c> (contrato §6.3).</summary>
    /// <param name="FuenteDatos">"bd" | "json" | "mixto" según sus filas; null si ninguna tiene fuente.</param>
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
        IReadOnlyList<string> Notas,
        string? FuenteDatos = null,
        FilasPorFuenteDTO? FilasPorFuente = null);
}
