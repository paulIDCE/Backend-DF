using BackendDF.Models.DTOs;

namespace BackendDF.Logic.Interfaces
{
    /// <summary>Cuadros de Macro, Sistema y Tasas (contrato §6).</summary>
    public interface ICuadrosService
    {
        Task<IReadOnlyList<CuadroResumenDTO>> ListarAsync(string? origen, string? q, CancellationToken ct);
        Task<CuadroDTO> ObtenerAsync(string id, ParametrosCuadro parametros, CancellationToken ct);
    }
}
