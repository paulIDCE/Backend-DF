using BackendDF.Common.Utils;
using BackendDF.Data.Interfaces;
using BackendDF.Logic.Dominio;
using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;
using BackendDF.Models.Entities;

namespace BackendDF.Logic.Services
{
    /// <summary>
    /// Reporte REP01 de una entidad (revista de Análisis, contrato §7) y series de varias
    /// entidades para el comparativo (hoja 31, §9.1). Un código que no existe no es error: se
    /// informa en <see cref="ConFaltantes{T}.NoEncontrados"/>.
    /// </summary>
    public class ReportesService : IReportesService
    {
        private const int MaxEntidadesSeries = 4;
        private const int MaxCodigosSeries = 50;

        private readonly IFuenteDatos _fuente;

        public ReportesService(IFuenteDatos fuente)
        {
            _fuente = fuente;
        }

        public async Task<ConFaltantes<ReporteDTO>> ObtenerReporteAsync(string entidadId, string? codigos, string? desde, string? hasta, CancellationToken ct)
        {
            var entidad = await _fuente.ResolverEntidadAsync(entidadId, ct);
            var (d, h) = Periodos.Rango(desde, hasta, Periodos.Mensual);
            var reporte = await _fuente.ReporteObligatorioAsync(entidad, ct);

            var pedidos = Texto.Separar(codigos);
            var noEncontrados = new List<string>();
            IEnumerable<CuentaReporte> cuentas = reporte.Cuentas;
            if (pedidos.Count > 0)
            {
                var seleccion = new List<CuentaReporte>();
                var vistas = new HashSet<CuentaReporte>(ReferenceEqualityComparer.Instance);
                foreach (var codigo in pedidos)
                {
                    var cuenta = reporte.BuscarCuenta(codigo);
                    if (cuenta is null)
                        noEncontrados.Add(codigo);
                    else if (vistas.Add(cuenta))
                        seleccion.Add(cuenta);
                }
                cuentas = seleccion;
            }

            var periodos = Periodos.Filtrar(reporte.Periodos, d, h);
            var alineador = new Alineador(periodos);
            var dto = new ReporteDTO(
                new EntidadReporteDTO(entidad.Id, entidad.Nombre, entidad.Tamano, entidad.Rango, entidad.Provincia),
                periodos,
                cuentas.Select(c => new CuentaReporteDTO(c.Cuc, c.Variable, alineador.Alinear(reporte.Periodos, c.Valores))).ToList());

            return new ConFaltantes<ReporteDTO>(dto, noEncontrados);
        }

        public async Task<ConFaltantes<SeriesEntidadesDTO>> ObtenerSeriesAsync(string? ids, string? codigos, string? desde, string? hasta, CancellationToken ct)
        {
            var listaIds = Texto.Separar(ids);
            if (listaIds.Count == 0)
                throw CustomException.Validacion("Indique al menos una entidad en el parámetro 'ids'.");
            if (listaIds.Count > MaxEntidadesSeries)
                throw CustomException.Validacion($"Se pueden comparar hasta {MaxEntidadesSeries} entidades (se recibieron {listaIds.Count}).");

            var listaCodigos = Texto.Separar(codigos);
            if (listaCodigos.Count == 0)
                throw CustomException.Validacion("Indique al menos una cuenta en el parámetro 'codigos'.");
            if (listaCodigos.Count > MaxCodigosSeries)
                throw CustomException.Validacion($"Se pueden pedir hasta {MaxCodigosSeries} cuentas (se recibieron {listaCodigos.Count}).");

            var (d, h) = Periodos.Rango(desde, hasta, Periodos.Mensual);

            var reportes = new List<(EntidadCatalogo Entidad, ReporteEntidad Reporte)>();
            foreach (var id in listaIds)
            {
                var entidad = await _fuente.ResolverEntidadAsync(id, ct);
                reportes.Add((entidad, await _fuente.ReporteObligatorioAsync(entidad, ct)));
            }

            // Unión de los períodos de las entidades: donde a una le falta un período va null.
            var periodos = Periodos.Filtrar(Periodos.Union(reportes.Select(r => r.Reporte.Periodos)), d, h);
            var alineador = new Alineador(periodos);
            var encontrados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var series = new List<SerieEntidadDTO>();

            foreach (var (entidad, reporte) in reportes)
                foreach (var codigo in listaCodigos)
                {
                    var cuenta = reporte.BuscarCuenta(codigo);
                    if (cuenta is null)
                        continue;
                    encontrados.Add(codigo);
                    series.Add(new SerieEntidadDTO(entidad.Id, codigo, cuenta.Variable, alineador.Alinear(reporte.Periodos, cuenta.Valores)));
                }

            return new ConFaltantes<SeriesEntidadesDTO>(
                new SeriesEntidadesDTO(periodos, series),
                listaCodigos.Where(c => !encontrados.Contains(c)).ToList());
        }
    }
}
