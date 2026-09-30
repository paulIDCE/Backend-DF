using System.Diagnostics;

namespace BackendDF.Common.Utils
{
    /// <summary>
    /// Envelope de error que ya interpreta el kit del frontend (<c>src/utils/apiError.ts</c>,
    /// contrato §4.5). Las respuestas exitosas NO lo usan: devuelven el recurso directamente.
    /// </summary>
    public class ResponseHandler
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>Texto técnico opcional (solo Development, para consola).</summary>
        public string? Detalle { get; set; }

        public string? ErrorCode { get; set; }
        public string? TraceId { get; set; }
        public int Status { get; set; }
        public object? Data { get; set; }

        public static ResponseHandler Error(int status, string errorCode, string message, string? traceId, string? detalle = null) =>
            new()
            {
                Success = false,
                Status = status,
                ErrorCode = errorCode,
                Message = message,
                TraceId = traceId,
                Detalle = detalle
            };

        /// <summary>Mismo traceId que queda en los logs estructurados.</summary>
        public static string TraceIdDe(HttpContext context) => Activity.Current?.Id ?? context.TraceIdentifier;

        /// <summary>Escribe el envelope como respuesta (middleware, 401 de JwtBearer, fallback 404, model state).</summary>
        public static Task EscribirAsync(HttpContext context, int status, string errorCode, string message, string? detalle = null)
        {
            context.Response.StatusCode = status;
            return context.Response.WriteAsJsonAsync(Error(status, errorCode, message, TraceIdDe(context), detalle));
        }
    }
}
