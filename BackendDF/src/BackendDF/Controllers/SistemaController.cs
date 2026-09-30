using BackendDF.Common.Utils;
using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BackendDF.Controllers
{
    /// <summary>Series del sistema financiero por sector (contrato §9.2).</summary>
    [ApiController]
    [Route("api/sistema")]
    [Produces("application/json")]
    public class SistemaController : ControllerBase
    {
        private readonly ISistemaService _service;

        public SistemaController(ISistemaService service)
        {
            _service = service;
        }

        /// <summary>Series de los sectores como referencia (hoja 2).</summary>
        /// <param name="codigos">CUC de base_estru_sistema separados por coma (p. ej. @1,@2,@3,Gan_Eje).</param>
        /// <param name="sectores">Ids de sector (opcional; sin él, los 10).</param>
        /// <param name="cuadros">Restringe Cuadro (opcional; p. ej. SFN01,SFN02).</param>
        /// <param name="desde">YYYY-MM (opcional).</param>
        /// <param name="hasta">YYYY-MM (opcional).</param>
        [HttpGet("series")]
        [ProducesResponseType(typeof(SeriesSistemaDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHandler), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Series(
            [FromQuery] string? codigos,
            [FromQuery] string? sectores,
            [FromQuery] string? cuadros,
            [FromQuery] string? desde,
            [FromQuery] string? hasta,
            CancellationToken ct)
        {
            var resultado = await _service.ObtenerSeriesAsync(codigos, sectores, cuadros, desde, hasta, ct);
            CabecerasApi.AgregarNoEncontrados(Response, resultado.NoEncontrados);
            return Ok(resultado.Resultado);
        }
    }
}
