using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace BackendDF.Common.Security
{
    /// <summary>
    /// Proveedor de claves de firma para JwtBearer a partir de un JWKS plano (Supabase publica
    /// <c>/auth/v1/.well-known/jwks.json</c>). Cachea las claves 1 hora; si llega un token con un
    /// <c>kid</c> desconocido, JwtBearer pide <see cref="RequestRefresh"/> y se vuelven a
    /// descargar (como mucho una vez por minuto). Si una recarga falla se conservan las claves
    /// anteriores.
    /// </summary>
    public sealed class JwksConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly TimeSpan Vigencia = TimeSpan.FromHours(1);
        private static readonly TimeSpan EsperaReintento = TimeSpan.FromMinutes(1);

        private readonly string _jwksUrl;
        private readonly string? _issuer;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private OpenIdConnectConfiguration? _actual;
        private DateTimeOffset _vence = DateTimeOffset.MinValue;
        private DateTimeOffset _ultimoRefresco = DateTimeOffset.MinValue;

        public JwksConfigurationManager(string jwksUrl, string? issuer)
        {
            _jwksUrl = jwksUrl;
            _issuer = issuer;
        }

        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
        {
            if (_actual is not null && DateTimeOffset.UtcNow < _vence)
                return _actual;

            await _lock.WaitAsync(cancel);
            try
            {
                if (_actual is not null && DateTimeOffset.UtcNow < _vence)
                    return _actual;

                try
                {
                    var json = await Http.GetStringAsync(_jwksUrl, cancel);
                    var jwks = new JsonWebKeySet(json);
                    var config = new OpenIdConnectConfiguration { Issuer = _issuer, JsonWebKeySet = jwks };
                    foreach (var clave in jwks.GetSigningKeys())
                        config.SigningKeys.Add(clave);

                    _actual = config;
                    _vence = DateTimeOffset.UtcNow + Vigencia;
                }
                catch (Exception) when (_actual is not null)
                {
                    _vence = DateTimeOffset.UtcNow + EsperaReintento;
                }

                return _actual!;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void RequestRefresh()
        {
            var ahora = DateTimeOffset.UtcNow;
            if (ahora - _ultimoRefresco < EsperaReintento)
                return;
            _ultimoRefresco = ahora;
            _vence = DateTimeOffset.MinValue;
        }
    }
}
