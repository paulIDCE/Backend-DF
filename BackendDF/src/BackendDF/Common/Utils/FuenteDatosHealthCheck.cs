using BackendDF.Data.Interfaces;
using BackendDF.Models.Entities;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BackendDF.Common.Utils
{
    /// <summary>
    /// <c>/api/health</c> (contrato §10): Healthy con la base y el índice de reportes listos,
    /// Degraded mientras cargan, Unhealthy si no se pudo cargar (p. ej. falta RutaBase).
    /// </summary>
    public sealed class FuenteDatosHealthCheck : IHealthCheck
    {
        private readonly IFuenteDatos _fuente;

        public FuenteDatosHealthCheck(IFuenteDatos fuente)
        {
            _fuente = fuente;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(_fuente.Estado switch
            {
                EstadoFuente.Lista => HealthCheckResult.Healthy("Datos base e índice de reportes cargados."),
                EstadoFuente.IndexandoReportes => HealthCheckResult.Degraded("Datos base cargados; construyendo el índice de reportes."),
                EstadoFuente.Cargando => HealthCheckResult.Degraded("Cargando los datos base."),
                _ => HealthCheckResult.Unhealthy(_fuente.DetalleEstado ?? "No se pudieron cargar los datos.")
            });
    }
}
