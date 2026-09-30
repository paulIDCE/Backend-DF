using BackendDF.Common.Utils;
using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BackendDF.Controllers
{
    /// <summary>Entidades del catálogo, su reporte REP01 y el comparativo entre entidades (contrato §5, §7, §9.1).</summary>
    [ApiController]
    [Route("api/entidades")]
    [Produces("application/json")]
    public class EntidadesController : ControllerBase
    {
        private readonly ICatalogosService _catalogos;
        private readonly IReportesService _reportes;

        public EntidadesController(ICatalogosService catalogos, IReportesService reportes)
        {
            _catalogos = catalogos;
            _reportes = reportes;
        }

        /// <summary>Las 229 entidades en orden alfabético, con filtros opcionales.</summary>
        /// <param name="q">Busca en el nombre, sin distinguir mayúsculas ni tildes.</param>
        /// <param name="tamano">Filtro exacto por Tamaño.</param>
        /// <param name="rango">Filtro exacto por Rango_Activos.</param>
        /// <param name="provincia">Filtro exacto por provincia (DPA_PR).</param>
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<EntidadDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar(
            [FromQuery] string? q,
            [FromQuery] string? tamano,
            [FromQuery] string? rango,
            [FromQuery] string? provincia,
            CancellationToken ct) =>
            Ok(await _catalogos.ListarEntidadesAsync(q, tamano, rango, provincia, ct));

        /// <summary>Series de hasta 4 entidades para comparar (hoja 31).</summary>
        /// <param name="ids">Ids de entidad separados por coma (máx. 4).</param>
        /// <param name="codigos">CUC o Variable separados por coma (máx. 50).</param>
        /// <param name="desde">YYYY-MM (opcional).</param>
        /// <param name="hasta">YYYY-MM (opcional).</param>
        [HttpGet("series")]
        [ProducesResponseType(typeof(SeriesEntidadesDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHandler), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHandler), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Series(
            [FromQuery] string? ids,
            [FromQuery] string? codigos,
            [FromQuery] string? desde,
            [FromQuery] string? hasta,
            CancellationToken ct)
        {
            var resultado = await _reportes.ObtenerSeriesAsync(ids, codigos, desde, hasta, ct);
            CabecerasApi.AgregarNoEncontrados(Response, resultado.NoEncontrados);
            return Ok(resultado.Resultado);
        }

        /// <summary>Una entidad del catálogo.</summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(EntidadDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHandler), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Obtener(string id, CancellationToken ct) =>
            Ok(await _catalogos.ObtenerEntidadAsync(id, ct));

        /// <summary>Reporte REP01 de la entidad (revista de Análisis Financiero).</summary>
        /// <param name="id">Id de la entidad (p. ej. BP__PICHINCHA).</param>
        /// <param name="codigos">CUC o Variable separados por coma. Sin él se devuelven las 776 cuentas.</param>
        /// <param name="desde">YYYY-MM (opcional).</param>
        /// <param name="hasta">YYYY-MM (opcional).</param>
        [HttpGet("{id}/reporte")]
        [ProducesResponseType(typeof(ReporteDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHandler), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Reporte(
            string id,
            [FromQuery] string? codigos,
            [FromQuery] string? desde,
            [FromQuery] string? hasta,
            CancellationToken ct)
        {
            var resultado = await _reportes.ObtenerReporteAsync(id, codigos, desde, hasta, ct);
            CabecerasApi.AgregarNoEncontrados(Response, resultado.NoEncontrados);
            return Ok(resultado.Resultado);
        }
    }
}
