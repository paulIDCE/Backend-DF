using BackendDF.Common.Utils;
using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BackendDF.Controllers
{
    /// <summary>Cuadros de Macro, Sistema Financiero y Tasas (contrato §6).</summary>
    [ApiController]
    [Route("api/cuadros")]
    [Produces("application/json")]
    public class CuadrosController : ControllerBase
    {
        private readonly ICuadrosService _service;

        public CuadrosController(ICuadrosService service)
        {
            _service = service;
        }

        /// <summary>Cuadros disponibles con sus metadatos.</summary>
        /// <param name="origen">macro | sistema | entidad (opcional).</param>
        /// <param name="q">Busca en el id y el título (opcional).</param>
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<CuadroResumenDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar([FromQuery] string? origen, [FromQuery] string? q, CancellationToken ct) =>
            Ok(await _service.ListarAsync(origen, q, ct));

        /// <summary>Un cuadro filtrado. Los parámetros requeridos dependen del tipo de cuadro.</summary>
        /// <param name="id">Id del cuadro (IEA111A, SFN01, EFI06…).</param>
        /// <param name="sector">nacional, privado, popular, grande, medianos, peque, seg1, seg2, seg3, mut.</param>
        /// <param name="entidad">Id de la entidad (p. ej. BP__PICHINCHA).</param>
        /// <param name="analisis">saldo | horizontal | vertical.</param>
        /// <param name="credito">total, productivo, consumo, inmobiliario, vip, educativo, microcredito.</param>
        /// <param name="desde">Período inicial, inclusivo (opcional).</param>
        /// <param name="hasta">Período final, inclusivo (opcional).</param>
        /// <param name="notas">Incluye las notas del cuadro (por defecto true).</param>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CuadroDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHandler), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHandler), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Obtener(
            string id,
            [FromQuery] string? sector,
            [FromQuery] string? entidad,
            [FromQuery] string? analisis,
            [FromQuery] string? credito,
            [FromQuery] string? desde,
            [FromQuery] string? hasta,
            CancellationToken ct,
            [FromQuery] bool notas = true) =>
            Ok(await _service.ObtenerAsync(id, new ParametrosCuadro(sector, entidad, analisis, credito, desde, hasta, notas), ct));
    }
}
