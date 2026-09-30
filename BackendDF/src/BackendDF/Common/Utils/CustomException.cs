namespace BackendDF.Common.Utils
{
    /// <summary>
    /// Excepción de negocio controlada. Lleva un <see cref="StatusCode"/> HTTP, un
    /// <see cref="ErrorCode"/> de <see cref="ApiErrorCodes"/> y un <see cref="UserMessage"/> apto
    /// para mostrar al usuario final. El middleware la traduce al envelope de error; el
    /// <see cref="Exception.Message"/> técnico solo viaja en <c>detalle</c> en Development.
    /// </summary>
    public class CustomException : Exception
    {
        public int StatusCode { get; }
        public string UserMessage { get; }
        public string ErrorCode { get; }

        public CustomException(string message, string userMessage, int statusCode = 400, string? errorCode = null)
            : base(message)
        {
            StatusCode = statusCode;
            UserMessage = userMessage;
            ErrorCode = errorCode ?? ApiErrorCodes.PorEstado(statusCode);
        }

        /// <summary>400 VALIDATION_ERROR con el mismo texto para usuario y consola.</summary>
        public static CustomException Validacion(string mensaje) =>
            new(mensaje, mensaje, 400, ApiErrorCodes.ValidationError);

        /// <summary>404 NOT_FOUND.</summary>
        public static CustomException NoEncontrado(string mensaje) =>
            new(mensaje, mensaje, 404, ApiErrorCodes.NotFound);

        /// <summary>500 INTERNAL_ERROR con detalle técnico separado del mensaje de usuario.</summary>
        public static CustomException Interno(string detalle, string mensaje) =>
            new(detalle, mensaje, 500, ApiErrorCodes.InternalError);
    }
}
