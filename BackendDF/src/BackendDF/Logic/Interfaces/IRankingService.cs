using BackendDF.Models.DTOs;

namespace BackendDF.Logic.Interfaces
{
    /// <summary>Rankings de entidades (contrato §8).</summary>
    public interface IRankingService
    {
        Task<RankingDTO> ObtenerAsync(string? cuenta, string? fecha, string? agrupacion, string? entidad, CancellationToken ct);
    }
}
