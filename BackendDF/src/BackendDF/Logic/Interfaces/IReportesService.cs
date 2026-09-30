using BackendDF.Models.DTOs;

namespace BackendDF.Logic.Interfaces
{
    /// <summary>Reporte por entidad y comparativo entre entidades (contrato §7 y §9.1).</summary>
    public interface IReportesService
    {
        Task<ConFaltantes<ReporteDTO>> ObtenerReporteAsync(string entidadId, string? codigos, string? desde, string? hasta, CancellationToken ct);
        Task<ConFaltantes<SeriesEntidadesDTO>> ObtenerSeriesAsync(string? ids, string? codigos, string? desde, string? hasta, CancellationToken ct);
    }
}
