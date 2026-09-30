namespace BackendDF.Data.FuenteJson
{
    /// <summary>
    /// Dispara la carga de los JSON al arrancar la API, sin bloquear el arranque: la base se
    /// carga en segundo plano y los requests que llegan antes la esperan; el índice de reportes
    /// se construye después (mientras tanto <c>/api/health</c> responde Degraded).
    /// </summary>
    public sealed class CargaDatosHostedService : IHostedService
    {
        private readonly FuenteDatosJson _fuente;

        public CargaDatosHostedService(FuenteDatosJson fuente)
        {
            _fuente = fuente;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _fuente.IniciarCarga();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
