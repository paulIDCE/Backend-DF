using System.Text;
using BackendDF.Common.Security;
using BackendDF.Common.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BackendDF.Configuration
{
    /// <summary>
    /// Autenticación por JWT configurable (contrato §4.2). Todo se arma desde la sección "Auth"
    /// vía <see cref="IOptions{AuthSettings}"/>, así que pasar de Supabase al SSO propio es un
    /// cambio de configuración. No depende del SSO corporativo de las apps satélite.
    /// </summary>
    public static class AuthenticationExtensions
    {
        private const string MensajeNoAutorizado = "Tu sesión no es válida o expiró. Inicia sesión nuevamente.";

        public static IServiceCollection AddAutenticacionApi(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AuthSettings>(configuration.GetSection("Auth"));

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<AuthSettings>>((options, auth) => ConfigurarJwt(options, auth.Value));

            // Política global: con la auth habilitada TODO endpoint exige usuario autenticado,
            // salvo los marcados [AllowAnonymous] (/api/health).
            services.AddOptions<AuthorizationOptions>()
                .Configure<IOptions<AuthSettings>>((options, auth) =>
                {
                    if (auth.Value.Habilitada)
                        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                });
            services.AddAuthorization();

            return services;
        }

        /// <summary>
        /// Falla el arranque si la configuración de auth no es válida: desactivarla fuera de
        /// Development o habilitarla sin ninguna clave de firma.
        /// </summary>
        public static void ValidarConfiguracionAuth(this WebApplication app)
        {
            var auth = app.Services.GetRequiredService<IOptions<AuthSettings>>().Value;
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Auth");

            if (!auth.Habilitada)
            {
                if (!app.Environment.IsDevelopment())
                    throw new InvalidOperationException("Auth:Habilitada=false solo está permitido en Development.");
                logger.LogWarning("Autenticación DESACTIVADA (Auth:Habilitada=false, solo Development).");
                return;
            }

            if (string.IsNullOrWhiteSpace(auth.JwksUrl) && string.IsNullOrWhiteSpace(auth.Secreto))
                throw new InvalidOperationException("Auth habilitada sin claves de firma: configure Auth:JwksUrl o Auth:Secreto.");

            logger.LogInformation("Autenticación JWT habilitada (modo {Modo}, issuer {Issuer}).", auth.Modo, auth.Issuer);
        }

        private static void ConfigurarJwt(JwtBearerOptions options, AuthSettings auth)
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrWhiteSpace(auth.Issuer),
                ValidIssuer = auth.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(auth.Audience),
                ValidAudience = auth.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidateIssuerSigningKey = true,
                // HS256 (proyectos Supabase antiguos o SSO con secreto compartido).
                IssuerSigningKey = string.IsNullOrWhiteSpace(auth.Secreto)
                    ? null
                    : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.Secreto))
            };

            // Claves públicas (RS256/ES256) publicadas en el JWKS.
            if (!string.IsNullOrWhiteSpace(auth.JwksUrl))
                options.ConfigurationManager = new JwksConfigurationManager(auth.JwksUrl, auth.Issuer);

            options.Events = new JwtBearerEvents
            {
                // 401 con el envelope de error: el frontend redirige a /login.
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    await ResponseHandler.EscribirAsync(
                        context.HttpContext,
                        StatusCodes.Status401Unauthorized,
                        ApiErrorCodes.Unauthorized,
                        MensajeNoAutorizado,
                        context.AuthenticateFailure?.Message ?? context.ErrorDescription);
                }
            };
        }
    }
}
