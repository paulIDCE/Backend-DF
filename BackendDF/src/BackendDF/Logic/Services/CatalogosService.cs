using BackendDF.Data.Interfaces;
using BackendDF.Logic.Dominio;
using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;
using BackendDF.Models.Entities;

namespace BackendDF.Logic.Services
{
    /// <summary>
    /// Catálogos para los filtros del frontend y el listado de entidades (contrato §5).
    /// Sin try/catch: los errores de la fuente burbujean hasta el middleware.
    /// </summary>
    public class CatalogosService : ICatalogosService
    {
        private readonly IFuenteDatos _fuente;

        public CatalogosService(IFuenteDatos fuente)
        {
            _fuente = fuente;
        }

        public async Task<CatalogosDTO> ObtenerCatalogosAsync(CancellationToken ct)
        {
            var entidades = await _fuente.ObtenerEntidadesAsync(ct);
            return new CatalogosDTO(
                Catalogo.Sectores,
                Catalogo.Analisis,
                Catalogo.Creditos,
                Texto.Distintos(entidades.Select(e => e.Tamano)),
                Texto.Distintos(entidades.Select(e => e.Rango)),
                Texto.Distintos(entidades.Select(e => e.Provincia)));
        }

        public async Task<IReadOnlyList<EntidadDTO>> ListarEntidadesAsync(string? q, string? tamano, string? rango, string? provincia, CancellationToken ct)
        {
            var entidades = await _fuente.ObtenerEntidadesAsync(ct);
            var buscado = string.IsNullOrWhiteSpace(q) ? null : Texto.Normalizar(q);

            // La fuente ya las entrega en orden alfabético por nombre.
            return entidades
                .Where(e => buscado is null || Texto.Normalizar(e.Nombre).Contains(buscado, StringComparison.Ordinal))
                .Where(e => Coincide(e.Tamano, tamano) && Coincide(e.Rango, rango) && Coincide(e.Provincia, provincia))
                .Select(ADto)
                .ToList();
        }

        public async Task<EntidadDTO> ObtenerEntidadAsync(string id, CancellationToken ct) =>
            ADto(await _fuente.ResolverEntidadAsync(id, ct));

        /// <summary>Filtro exacto; sin valor no filtra.</summary>
        private static bool Coincide(string? valor, string? filtro) =>
            string.IsNullOrWhiteSpace(filtro) || string.Equals(valor, filtro.Trim(), StringComparison.Ordinal);

        internal static EntidadDTO ADto(EntidadCatalogo e) => new(
            e.Id,
            e.Nombre,
            e.Tipo,
            e.Tamano,
            e.Rango,
            e.Provincia,
            e.TieneBalance,
            RangoPeriodosDTO.De(e.PeriodoDesde, e.PeriodoHasta));
    }
}
