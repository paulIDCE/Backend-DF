using BackendDF.Common.Utils;
using BackendDF.Data.Interfaces;
using BackendDF.Logic.Dominio;
using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;
using BackendDF.Models.Entities;

namespace BackendDF.Logic.Services
{
    /// <summary>
    /// Rankings de las hojas 5, 9, 24, 25 y 26 (contrato §8). Replica
    /// <c>renderRankingGenerico</c> del original con el filtro de provincia corregido
    /// (<c>DPA_PR</c>), sobre el índice compacto de reportes.
    /// </summary>
    public class RankingService : IRankingService
    {
        private static readonly string[] Agrupaciones = ["sector", "activos", "provincia", "todas"];

        private readonly IFuenteDatos _fuente;

        public RankingService(IFuenteDatos fuente)
        {
            _fuente = fuente;
        }

        public async Task<RankingDTO> ObtenerAsync(string? cuenta, string? fecha, string? agrupacion, string? entidad, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cuenta))
                throw CustomException.Validacion("El parámetro 'cuenta' es obligatorio.");
            if (string.IsNullOrWhiteSpace(fecha) || !Periodos.EsMensual(fecha.Trim()))
                throw CustomException.Validacion("El parámetro 'fecha' es obligatorio y debe tener el formato YYYY-MM.");
            var tipo = agrupacion?.Trim().ToLowerInvariant();
            if (tipo is null || !Agrupaciones.Contains(tipo))
                throw CustomException.Validacion($"El parámetro 'agrupacion' es obligatorio. Valores permitidos: {string.Join(", ", Agrupaciones)}.");
            if (tipo != "todas" && string.IsNullOrWhiteSpace(entidad))
                throw CustomException.Validacion("El parámetro 'entidad' es obligatorio salvo con agrupacion=todas.");

            cuenta = cuenta.Trim();
            fecha = fecha.Trim();
            var referencia = string.IsNullOrWhiteSpace(entidad) ? null : await _fuente.ResolverEntidadAsync(entidad, ct);
            var entidades = await _fuente.ObtenerEntidadesAsync(ct);
            var reportes = await _fuente.ObtenerReportesAsync(ct);

            if (!entidades.Any(e => reportes.TryGetValue(e.Id, out var r) && r.BuscarCuenta(cuenta) is not null))
                throw CustomException.NoEncontrado($"No existe la cuenta '{cuenta}' en los reportes de las entidades.");

            // 1. Comparación contra el mismo mes del año anterior.
            var fechaComparacion = Periodos.RestarMeses(fecha, 12);

            // 2. Grupo: entidades con el mismo valor de agrupación que la de referencia (incluida ella).
            Func<EntidadCatalogo, string?>? campo = tipo switch
            {
                "sector" => e => e.Tamano,
                "activos" => e => e.Rango,
                "provincia" => e => e.Provincia,
                _ => null
            };
            var valorGrupo = campo is null ? null : campo(referencia!);
            var grupo = campo is null
                ? entidades
                : entidades.Where(e => string.Equals(campo(e), valorGrupo, StringComparison.Ordinal)).ToList();

            // 3. Valores; un nulo cuenta como 0.
            var valores = grupo.Select(e =>
            {
                reportes.TryGetValue(e.Id, out var reporte);
                var c = reporte?.BuscarCuenta(cuenta);
                return (Entidad: e, Anterior: Valor(reporte, c, fechaComparacion), Actual: Valor(reporte, c, fecha));
            }).ToList();

            var totalAnterior = valores.Sum(v => v.Anterior);
            var totalActual = valores.Sum(v => v.Actual);

            // 4-5. Participaciones y orden (estable: a igualdad queda el orden alfabético).
            var filas = valores
                .Select(v => (v.Entidad, v.Anterior, v.Actual,
                    PartAnterior: Participacion(v.Anterior, totalAnterior),
                    PartActual: Participacion(v.Actual, totalActual)))
                .OrderByDescending(v => v.PartActual)
                .Select((v, i) => new FilaRankingDTO(i + 1, v.Entidad.Id, v.Entidad.Nombre, v.Anterior, v.Actual, v.PartAnterior, v.PartActual))
                .ToList();

            int? posicion = referencia is null ? null : filas.FirstOrDefault(f => f.EntidadId == referencia.Id)?.Posicion;

            return new RankingDTO(
                cuenta,
                fecha,
                fechaComparacion,
                new AgrupacionRankingDTO(tipo, valorGrupo),
                new TotalRankingDTO(totalAnterior, totalActual),
                posicion,
                filas);
        }

        private static double Valor(ReporteEntidad? reporte, CuentaReporte? cuenta, string periodo)
        {
            if (reporte is null || cuenta is null)
                return 0;
            var valor = reporte.Valor(cuenta, periodo);
            return double.IsNaN(valor) ? 0 : valor;
        }

        private static double Participacion(double valor, double total) => total > 0 ? valor / total * 100 : 0;
    }
}
