using BackendDF.Common.Utils;
using BackendDF.Data.Interfaces;
using BackendDF.Models.Entities;

namespace BackendDF.Logic.Dominio
{
    public static class FuenteDatosExtensions
    {
        /// <summary>Entidad del catálogo por id (sin distinguir mayúsculas) o 404 NOT_FOUND.</summary>
        public static async Task<EntidadCatalogo> ResolverEntidadAsync(this IFuenteDatos fuente, string id, CancellationToken ct)
        {
            var buscado = id.Trim();
            var entidades = await fuente.ObtenerEntidadesAsync(ct);
            return entidades.FirstOrDefault(e => string.Equals(e.Id, buscado, StringComparison.OrdinalIgnoreCase))
                   ?? throw CustomException.NoEncontrado($"No existe la entidad '{id}'.");
        }

        /// <summary>Reporte de la entidad o 404 si no tiene.</summary>
        public static async Task<ReporteEntidad> ReporteObligatorioAsync(this IFuenteDatos fuente, EntidadCatalogo entidad, CancellationToken ct) =>
            await fuente.ObtenerReporteAsync(entidad.Id, ct)
            ?? throw CustomException.NoEncontrado($"La entidad {entidad.Nombre} no tiene reporte.");
    }
}
