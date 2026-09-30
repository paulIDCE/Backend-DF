using BackendDF.Models.Entities;

namespace BackendDF.Data.Interfaces
{
    /// <summary>
    /// Contrato de acceso a los datos. Hoy lo implementa <c>FuenteJson</c> (los JSON de
    /// prueba-data); en la etapa 3 se reemplaza por una implementación SQL sin tocar los
    /// services ni los endpoints (contrato §14).
    ///
    /// Las filas se devuelven en el orden del origen (R4). Los ids de entidad que recibe son
    /// siempre ids del catálogo: la fuente nunca arma rutas con texto del request.
    /// </summary>
    public interface IFuenteDatos
    {
        /// <summary>Estado de carga, para <c>/api/health</c>.</summary>
        EstadoFuente Estado { get; }

        /// <summary>Motivo del estado <see cref="EstadoFuente.Error"/>.</summary>
        string? DetalleEstado { get; }

        /// <summary>Fecha de modificación más reciente de los archivos base (ETag y <c>/meta</c>).</summary>
        Task<DateTime> ObtenerVersionDatosAsync(CancellationToken ct);

        /// <summary>Catálogo de entidades, ordenado por nombre.</summary>
        Task<IReadOnlyList<EntidadCatalogo>> ObtenerEntidadesAsync(CancellationToken ct);

        /// <summary>Cuadros disponibles con sus metadatos.</summary>
        Task<IReadOnlyList<CuadroFuente>> ObtenerCuadrosAsync(CancellationToken ct);

        /// <summary>Filas de un cuadro con sus filtros (exactos contra el texto del origen).</summary>
        Task<IReadOnlyList<FilaDatos>> ObtenerFilasAsync(ConsultaFilas consulta, CancellationToken ct);

        /// <summary>Todas las filas de Sistema (<c>base_estru_sistema</c>) de un sector.</summary>
        Task<IReadOnlyList<FilaDatos>> ObtenerFilasSistemaAsync(string filtro, CancellationToken ct);

        /// <summary>Párrafos de notas del cuadro.</summary>
        Task<IReadOnlyList<string>> ObtenerNotasAsync(string cuadro, CancellationToken ct);

        /// <summary>Reporte REP01 de una entidad; null si la entidad no tiene reporte.</summary>
        Task<ReporteEntidad?> ObtenerReporteAsync(string entidadId, CancellationToken ct);

        /// <summary>Reportes de todas las entidades (espera a que el índice esté completo).</summary>
        Task<IReadOnlyDictionary<string, ReporteEntidad>> ObtenerReportesAsync(CancellationToken ct);

        /// <summary>Metadatos de las fuentes y advertencias de calidad de datos.</summary>
        Task<InfoFuentes> ObtenerInfoAsync(CancellationToken ct);
    }
}
