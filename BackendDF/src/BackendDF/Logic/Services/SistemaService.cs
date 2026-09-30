using BackendDF.Common.Utils;
using BackendDF.Data.Interfaces;
using BackendDF.Logic.Dominio;
using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;
using BackendDF.Models.Entities;

namespace BackendDF.Logic.Services
{
    /// <summary>
    /// Series de <c>base_estru_sistema</c> por sector, usadas como referencia en la hoja 2 de la
    /// revista (contrato §9.2). Por cada sector y código se toma la primera fila (orden del
    /// archivo) cuyo CUC coincide, opcionalmente restringida a ciertos cuadros.
    /// </summary>
    public class SistemaService : ISistemaService
    {
        private const int MaxCodigos = 50;

        private readonly IFuenteDatos _fuente;

        public SistemaService(IFuenteDatos fuente)
        {
            _fuente = fuente;
        }

        public async Task<ConFaltantes<SeriesSistemaDTO>> ObtenerSeriesAsync(string? codigos, string? sectores, string? cuadros, string? desde, string? hasta, CancellationToken ct)
        {
            var listaCodigos = Texto.Separar(codigos);
            if (listaCodigos.Count == 0)
                throw CustomException.Validacion("Indique al menos un código en el parámetro 'codigos'.");
            if (listaCodigos.Count > MaxCodigos)
                throw CustomException.Validacion($"Se pueden pedir hasta {MaxCodigos} códigos (se recibieron {listaCodigos.Count}).");

            var listaSectores = Texto.Separar(sectores) is { Count: > 0 } ids
                ? ids.Select(s => Catalogo.Resolver(Catalogo.Sectores, s, "sectores")).ToList()
                : Catalogo.Sectores.ToList();
            var filtroCuadros = Texto.Separar(cuadros) is { Count: > 0 } lista
                ? lista.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : null;
            var (d, h) = Periodos.Rango(desde, hasta, Periodos.Mensual);

            var coincidencias = new List<(ItemCatalogoDTO Sector, string Codigo, FilaDatos Fila)>();
            var encontrados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sector in listaSectores)
            {
                var filas = await _fuente.ObtenerFilasSistemaAsync(sector.Nombre, ct);
                foreach (var codigo in listaCodigos)
                {
                    var fila = filas.FirstOrDefault(f => string.Equals(f.Cuc, codigo, StringComparison.OrdinalIgnoreCase)
                                                         && (filtroCuadros is null || filtroCuadros.Contains(f.Cuadro)));
                    if (fila is null)
                        continue;
                    encontrados.Add(codigo);
                    coincidencias.Add((sector, codigo, fila));
                }
            }

            var periodos = Periodos.Filtrar(Periodos.Union(coincidencias.Select(c => c.Fila.Periodos)), d, h);
            var alineador = new Alineador(periodos);
            var series = coincidencias
                .Select(c => new SerieSistemaDTO(c.Sector.Id, c.Sector.Nombre, c.Codigo, c.Fila.Cuadro, c.Fila.Variable,
                    alineador.Alinear(c.Fila.Periodos, c.Fila.Valores)))
                .ToList();

            return new ConFaltantes<SeriesSistemaDTO>(
                new SeriesSistemaDTO(periodos, series),
                listaCodigos.Where(c => !encontrados.Contains(c)).ToList());
        }
    }
}
