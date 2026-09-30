using BackendDF.Common.Utils;
using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BackendDF.Controllers
{
    /// <summary>
    /// Catálogos para armar los filtros del frontend (contrato §5). Como todo /api, exige token
    /// por el FallbackPolicy global; sin try/catch: los errores los responde el middleware.
    /// </summary>
    [ApiController]
    [Route("api/catalogos")]
    [Produces("application/json")]
    public class CatalogosController : ControllerBase
    {
        private readonly ICatalogosService _service;

        public CatalogosController(ICatalogosService service)
        {
            _service = service;
        }

        /// <summary>Sectores, análisis, créditos y los tamaños, rangos y provincias de las entidades.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(CatalogosDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> Obtener(CancellationToken ct) =>
            Ok(await _service.ObtenerCatalogosAsync(ct));
    }
}
