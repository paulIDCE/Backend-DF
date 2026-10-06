using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BackendDF.Tests
{
    /// <summary>
    /// Levanta la API en memoria sobre los datos reales de <c>src/BackendDF/Database</c>, con la
    /// autenticación HABILITADA y un secreto HS256 de pruebas para emitir tokens.
    /// Se comparte entre todas las clases de la colección para cargar los datos una sola vez.
    /// </summary>
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public const string Issuer = "https://pruebas.local/auth/v1";
        public const string Audience = "authenticated";
        private const string Secreto = "secreto-de-pruebas-hs256-con-al-menos-32-bytes";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Datos:RutaBase"] = RutaDatos(),
                // Las pruebas de contrato validan la fuente JSON pura; la híbrida tiene las suyas
                // (FuenteHibridaTests con dobles y SqlIntegracionTests contra la BD).
                ["Datos:Sql:Habilitado"] = "false",
                ["Auth:Habilitada"] = "true",
                ["Auth:Issuer"] = Issuer,
                ["Auth:Audience"] = Audience,
                ["Auth:JwksUrl"] = "",
                ["Auth:Secreto"] = Secreto
            }));
        }

        /// <summary>Cliente con un token válido.</summary>
        public HttpClient ClienteAutenticado()
        {
            var cliente = CreateClient();
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
            return cliente;
        }

        public static string Token(string secreto = Secreto, DateTime? expira = null) =>
            new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = Audience,
                Subject = new ClaimsIdentity([new Claim("sub", "usuario-de-pruebas")]),
                NotBefore = (expira ?? DateTime.UtcNow.AddHours(1)).AddHours(-2),
                Expires = expira ?? DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secreto)), SecurityAlgorithms.HmacSha256)
            });

        /// <summary>Busca <c>src/BackendDF/Database</c> subiendo desde la carpeta de salida de los tests.</summary>
        public static string RutaDatos()
        {
            // Permite compilar los tests fuera del árbol del repo (p. ej. --artifacts-path).
            if (Environment.GetEnvironmentVariable("BACKENDDF_RUTA_DATOS") is { Length: > 0 } ruta && Directory.Exists(ruta))
                return ruta;

            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                var candidato = Path.Combine(dir.FullName, "src", "BackendDF", "Database");
                if (Directory.Exists(candidato))
                    return candidato;
            }
            throw new DirectoryNotFoundException("No se encontró src/BackendDF/Database.");
        }
    }

    [CollectionDefinition(Nombre)]
    public sealed class ColeccionApi : ICollectionFixture<ApiFactory>
    {
        public const string Nombre = "API";
    }
}
