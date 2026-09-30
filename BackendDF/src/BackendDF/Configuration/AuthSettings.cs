namespace BackendDF.Configuration
{
    /// <summary>
    /// Bindea la sección "Auth" de appsettings.json (contrato §4.2). La validación del token se
    /// arma solo con estos valores, así que pasar de Supabase al SSO propio es un cambio de
    /// configuración y no de código.
    /// </summary>
    public class AuthSettings
    {
        /// <summary>false desactiva la autenticación. Solo se admite en Development.</summary>
        public bool Habilitada { get; set; } = true;

        /// <summary>Informativo ("Supabase" hoy). Se registra en el log al arrancar.</summary>
        public string Modo { get; set; } = "Supabase";

        /// <summary>Valor esperado del claim <c>iss</c>.</summary>
        public string? Issuer { get; set; }

        /// <summary>URL del JWKS con las claves públicas de firma (RS256/ES256).</summary>
        public string? JwksUrl { get; set; }

        /// <summary>Valor esperado del claim <c>aud</c> (Supabase: "authenticated").</summary>
        public string? Audience { get; set; }

        /// <summary>Secreto HS256 (proyectos Supabase antiguos). Cargar por user-secrets.</summary>
        public string? Secreto { get; set; }
    }
}
