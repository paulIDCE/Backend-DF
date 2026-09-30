using BackendDF.Data.Interfaces;
using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;

namespace BackendDF.Logic.Services
{
    /// <summary>Versión de los datos, cobertura por fuente y advertencias (contrato §10).</summary>
    public class MetaService : IMetaService
    {
        private readonly IFuenteDatos _fuente;

        public MetaService(IFuenteDatos fuente)
        {
            _fuente = fuente;
        }

        public async Task<MetaDTO> ObtenerAsync(CancellationToken ct)
        {
            var info = await _fuente.ObtenerInfoAsync(ct);
            return new MetaDTO(
                info.VersionDatos,
                info.Fuentes.Select(f => new FuenteDTO(f.Fuente, f.Archivo, f.Archivos, f.Desde, f.Hasta, f.Modificado)).ToList(),
                info.UltimoCorteComun,
                info.Advertencias);
        }
    }
}
