namespace BackendDF.Common.Utils
{
    /// <summary>Cabeceras propias de la API (se exponen por CORS para que el frontend las lea).</summary>
    public static class CabecerasApi
    {
        /// <summary>Códigos pedidos que no existen (contrato §7), separados por coma.</summary>
        public const string CodigosNoEncontrados = "X-Codigos-No-Encontrados";

        public static void AgregarNoEncontrados(HttpResponse response, IReadOnlyList<string> codigos)
        {
            if (codigos.Count == 0)
                return;
            // Una cabecera HTTP solo admite ASCII: los códigos con tildes van percent-encoded.
            response.Headers[CodigosNoEncontrados] = string.Join(",",
                codigos.Select(c => c.All(ch => ch is >= ' ' and <= '~') ? c : Uri.EscapeDataString(c)));
        }
    }
}
