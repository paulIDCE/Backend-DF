using BackendDF.Configuration;
using Microsoft.Extensions.Options;

namespace BackendDF.Data.FuenteHibrida
{
    /// <summary>
    /// Con la fuente híbrida habilitada, arma en segundo plano el índice de reportes (JSON + una
    /// carga masiva desde B11) apenas arranca la API, para que el primer ranking no espere.
    /// No bloquea el arranque; si SQL falla, la fuente ya cae a JSON por sí sola.
    /// </summary>
    public sealed class PrecargaHibridaHostedService : BackgroundService
    {
        private readonly IServiceProvider _servicios;
        private readonly IOptions<DatosSettings> _opciones;
        private readonly ILogger<PrecargaHibridaHostedService> _logger;

        public PrecargaHibridaHostedService(IServiceProvider servicios, IOptions<DatosSettings> opciones, ILogger<PrecargaHibridaHostedService> logger)
        {
            _servicios = servicios;
            _opciones = opciones;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_opciones.Value.Sql.Habilitado)
                return;

            try
            {
                var fuente = _servicios.GetRequiredService<FuenteDatosHibrida>();
                var inicio = DateTime.UtcNow;
                var reportes = await fuente.ObtenerReportesAsync(stoppingToken);
                _logger.LogInformation("Precarga híbrida: {Reportes} reportes listos en {Segundos:F1} s",
                    reportes.Count, (DateTime.UtcNow - inicio).TotalSeconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Precarga híbrida no completada; se hará en el primer pedido.");
            }
        }
    }
}
