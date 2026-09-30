using BackendDF.Models.DTOs;

namespace BackendDF.Logic.Interfaces
{
    /// <summary>Metadatos de los datos (contrato §10).</summary>
    public interface IMetaService
    {
        Task<MetaDTO> ObtenerAsync(CancellationToken ct);
    }
}
