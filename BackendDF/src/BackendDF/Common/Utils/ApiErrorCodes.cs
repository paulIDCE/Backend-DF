namespace BackendDF.Common.Utils
{
    /// <summary>Lista cerrada de <c>errorCode</c> del envelope de error (contrato §4.5).</summary>
    public static class ApiErrorCodes
    {
        /// <summary>400: parámetro faltante, inválido o incompatible con el cuadro.</summary>
        public const string ValidationError = "VALIDATION_ERROR";

        /// <summary>401: token faltante o inválido.</summary>
        public const string Unauthorized = "UNAUTHORIZED";

        /// <summary>404: cuadro, entidad o cuenta inexistente.</summary>
        public const string NotFound = "NOT_FOUND";

        /// <summary>500: error no controlado o archivo corrupto.</summary>
        public const string InternalError = "INTERNAL_ERROR";

        public static string PorEstado(int status) => status switch
        {
            400 => ValidationError,
            401 => Unauthorized,
            404 => NotFound,
            _ => InternalError
        };
    }
}
