namespace BackendDF.Common.Utils
{
    /// <summary>
    /// Punto ÚNICO de manejo de errores. Cualquier excepción que burbujee desde la fuente de
    /// datos → service → controller se captura aquí y se devuelve con el envelope de
    /// <see cref="ResponseHandler"/> (contrato §4.5).
    ///
    /// - <see cref="CustomException"/>: usa su StatusCode, ErrorCode y UserMessage.
    /// - Cualquier otra excepción: 500 INTERNAL_ERROR con mensaje genérico.
    /// - <c>detalle</c> (texto técnico) solo se envía en Development.
    ///
    /// Gracias a esto los controllers NO necesitan try/catch por acción.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private const string MensajeGenerico = "Ocurrió un error inesperado. Por favor, contacte al soporte.";

        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // El cliente cerró la conexión: no hay a quién responder.
            }
            catch (Exception ex)
            {
                var traceId = ResponseHandler.TraceIdDe(context);
                var custom = ex as CustomException;
                var status = custom?.StatusCode ?? StatusCodes.Status500InternalServerError;

                if (status >= 500)
                    _logger.LogError(ex, "Error {Status} en {Ruta} (traceId {TraceId})", status, context.Request.Path, traceId);
                else
                    _logger.LogWarning("Error {Status} en {Ruta} (traceId {TraceId}): {Mensaje}", status, context.Request.Path, traceId, ex.Message);

                if (context.Response.HasStarted)
                    throw;

                await ResponseHandler.EscribirAsync(
                    context,
                    status,
                    custom?.ErrorCode ?? ApiErrorCodes.InternalError,
                    custom?.UserMessage ?? MensajeGenerico,
                    _env.IsDevelopment() ? ex.Message : null);
            }
        }
    }
}
