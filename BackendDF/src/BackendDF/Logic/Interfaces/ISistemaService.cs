using BackendDF.Models.DTOs;

namespace BackendDF.Logic.Interfaces
{
    /// <summary>Series de sectores del sistema (contrato §9.2).</summary>
    public interface ISistemaService
    {
        Task<ConFaltantes<SeriesSistemaDTO>> ObtenerSeriesAsync(string? codigos, string? sectores, string? cuadros, string? desde, string? hasta, CancellationToken ct);
    }
}
