using BackendDF.Data.Interfaces;
using Microsoft.Net.Http.Headers;

namespace BackendDF.Common.Utils
{
    /// <summary>
    /// Caché HTTP de las respuestas de datos (contrato §3.4): <c>ETag</c> débil derivado de la
    /// "versión de datos" y <c>Cache-Control: private, max-age=3600</c>. Si el cliente envía un
    /// <c>If-None-Match</c> vigente se responde 304 sin ejecutar el endpoint.
    ///
    /// Va después de UseAuthorization: un 304 solo se entrega a un usuario autenticado.
    /// </summary>
    public class CacheHttpMiddleware
    {
        private readonly RequestDelegate _next;

        public CacheHttpMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, IFuenteDatos fuente)
        {
            if (!HttpMethods.IsGet(context.Request.Method)
                || !context.Request.Path.StartsWithSegments("/api")
                || context.Request.Path.StartsWithSegments("/api/health")
                || context.GetEndpoint() is null)
            {
                await _next(context);
                return;
            }

            var version = await fuente.ObtenerVersionDatosAsync(context.RequestAborted);
            var etag = new EntityTagHeaderValue($"\"{version.Ticks:x}\"", isWeak: true);

            var ifNoneMatch = context.Request.GetTypedHeaders().IfNoneMatch;
            if (ifNoneMatch.Any(e => e.Equals(EntityTagHeaderValue.Any) || e.Compare(etag, useStrongComparison: false)))
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                EscribirCabeceras(context.Response, etag);
                return;
            }

            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode == StatusCodes.Status200OK)
                    EscribirCabeceras(context.Response, etag);
                return Task.CompletedTask;
            });

            await _next(context);
        }

        private static void EscribirCabeceras(HttpResponse response, EntityTagHeaderValue etag)
        {
            response.Headers.ETag = etag.ToString();
            response.Headers.CacheControl = "private, max-age=3600";
        }
    }
}
