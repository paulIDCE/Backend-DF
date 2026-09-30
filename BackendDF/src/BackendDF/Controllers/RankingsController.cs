using BackendDF.Common.Utils;
using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BackendDF.Controllers
{
    /// <summary>Rankings de entidades de las hojas 5, 9, 24, 25 y 26 (contrato §8).</summary>
    [ApiController]
    [Route("api/rankings")]
    [Produces("application/json")]
    public class RankingsController : ControllerBase
    {
        private readonly IRankingService _service;

        public RankingsController(IRankingService service)
        {
            _service = service;
        }

        /// <summary>Participación de cada entidad del grupo en una cuenta, a una fecha y un año antes.</summary>
        /// <param name="cuenta">CUC a rankear (@1, @2, @14, monto_total, mop…).</param>
        /// <param name="fecha">Corte YYYY-MM.</param>
        /// <param name="agrupacion">sector | activos | provincia | todas.</param>
        /// <param name="entidad">Entidad de referencia (obligatoria salvo con todas).</param>
        [HttpGet]
        [ProducesResponseType(typeof(RankingDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHandler), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHandler), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Obtener(
            [FromQuery] string? cuenta,
            [FromQuery] string? fecha,
            [FromQuery] string? agrupacion,
            [FromQuery] string? entidad,
            CancellationToken ct) =>
            Ok(await _service.ObtenerAsync(cuenta, fecha, agrupacion, entidad, ct));
    }
}
