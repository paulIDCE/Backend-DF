using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BackendDF.Controllers
{
    /// <summary>Metadatos de los datos: versión, cobertura y advertencias (contrato §10).</summary>
    [ApiController]
    [Route("api/meta")]
    [Produces("application/json")]
    public class MetaController : ControllerBase
    {
        private readonly IMetaService _service;

        public MetaController(IMetaService service)
        {
            _service = service;
        }

        /// <summary>Reemplaza el "Datos actualizados" del hub con la fecha real de los archivos.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(MetaDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> Obtener(CancellationToken ct) =>
            Ok(await _service.ObtenerAsync(ct));
    }
}
