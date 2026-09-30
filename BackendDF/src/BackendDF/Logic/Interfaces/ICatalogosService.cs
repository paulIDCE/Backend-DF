using BackendDF.Models.DTOs;

namespace BackendDF.Logic.Interfaces
{
    /// <summary>Catálogos y entidades (contrato §5).</summary>
    public interface ICatalogosService
    {
        Task<CatalogosDTO> ObtenerCatalogosAsync(CancellationToken ct);
        Task<IReadOnlyList<EntidadDTO>> ListarEntidadesAsync(string? q, string? tamano, string? rango, string? provincia, CancellationToken ct);
        Task<EntidadDTO> ObtenerEntidadAsync(string id, CancellationToken ct);
    }
}
