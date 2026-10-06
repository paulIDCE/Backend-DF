using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace BackendDF.Tests
{
    /// <summary>
    /// API con la fuente híbrida contra <c>idce_bco_coop</c> real (cadena "DefaultConnection" de
    /// src/BackendDF/appsettings*.json o user-secrets). Si la BD no responde, las pruebas se
    /// omiten. Valores esperados verificados contra el .sav y los JSON (informe §3bis).
    /// </summary>
    public sealed class SqlApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Datos:RutaBase"] = ApiFactory.RutaDatos(),
                ["Datos:Sql:Habilitado"] = "true",
                ["Auth:Habilitada"] = "false"
            }));
        }

        /// <summary>null si la BD responde; si no, el motivo para omitir las pruebas.</summary>
        public static readonly Lazy<string?> MotivoOmision = new(() =>
        {
            try
            {
                var raiz = Path.GetDirectoryName(ApiFactory.RutaDatos())!;
                var config = new ConfigurationBuilder()
                    .AddJsonFile(Path.Combine(raiz, "appsettings.json"), optional: true)
                    .AddJsonFile(Path.Combine(raiz, "appsettings.Development.json"), optional: true)
                    .AddUserSecrets<Program>(optional: true)
                    .Build();
                var cadena = config.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(cadena))
                    return "Sin ConnectionStrings:DefaultConnection.";

                using var conexion = new SqlConnection(new SqlConnectionStringBuilder(cadena) { ConnectTimeout = 5 }.ConnectionString);
                conexion.Open();
                return null;
            }
            catch (Exception ex)
            {
                return $"BD no disponible: {ex.GetBaseException().Message}";
            }
        });
    }

    /// <summary>Se omite (Skip) si <see cref="SqlApiFactory.MotivoOmision"/> no es null.</summary>
    public sealed class FactSqlAttribute : FactAttribute
    {
        public FactSqlAttribute()
        {
            if (SqlApiFactory.MotivoOmision.Value is { } motivo)
                Skip = motivo;
        }
    }

    [CollectionDefinition(Nombre)]
    public sealed class ColeccionSql : ICollectionFixture<SqlApiFactory>
    {
        public const string Nombre = "SQL";
    }

    [Collection(ColeccionSql.Nombre)]
    public class SqlIntegracionTests
    {
        private readonly SqlApiFactory _factory;

        public SqlIntegracionTests(SqlApiFactory factory)
        {
            _factory = factory;
        }

        private async Task<JsonElement> Get(string url)
        {
            var respuesta = await _factory.CreateClient().GetAsync(url);
            var texto = await respuesta.Content.ReadAsStringAsync();
            Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"{url} → {(int)respuesta.StatusCode}: {texto}");
            return JsonDocument.Parse(texto).RootElement.Clone();
        }

        private static double Valor(JsonElement cuadro, string cuc, string periodo)
        {
            var periodos = cuadro.GetProperty("periodos").EnumerateArray().Select(p => p.GetString()).ToList();
            var fila = cuadro.GetProperty("filas").EnumerateArray().First(f => f.GetProperty("cuc").GetString() == cuc);
            return fila.GetProperty("valores")[periodos.IndexOf(periodo)].GetDouble();
        }

        [FactSql(DisplayName = "SQL: EFI01 de BP. PICHINCHA desde B11, en la ventana de la BD y sin cuentas 6/7")]
        public async Task Efi01Entidad()
        {
            var cuadro = await Get("/api/cuadros/EFI01?entidad=BP__PICHINCHA");
            var periodos = cuadro.GetProperty("periodos").EnumerateArray().Select(p => p.GetString()!).ToList();

            Assert.Equal("2024-01", periodos[0]);
            Assert.True(string.CompareOrdinal(periodos[^1], "2026-08") >= 0);
            Assert.Equal(22806.742733, Valor(cuadro, "@1", "2026-07"), 6);
            Assert.Equal(23202.78531899, Valor(cuadro, "@1", "2026-08"), 6);   // cierre corregido (≠ JSON)
            Assert.DoesNotContain(cuadro.GetProperty("filas").EnumerateArray(),
                f => f.GetProperty("cuc").GetString() is { } c && (c.StartsWith("@6") || c.StartsWith("@7")));
        }

        [FactSql(DisplayName = "SQL: SFN01 Bancos Privados Grandes desde la vista indexada = JSON")]
        public async Task Sfn01Sector()
        {
            var cuadro = await Get("/api/cuadros/SFN01?sector=grande");
            Assert.Equal(38275.9688, Valor(cuadro, "@1", "2024-01"), 3);
            Assert.Equal(53946.6732, Valor(cuadro, "@1", "2026-07"), 3);
        }

        [FactSql(DisplayName = "SQL: EFI05 SOLVENCIA desde IndicadorData con escala ×100")]
        public async Task Efi05Indicadores()
        {
            var cuadro = await Get("/api/cuadros/EFI05?entidad=BP__PICHINCHA");
            Assert.Equal(13.6285, Valor(cuadro, "SOLVENCIA", "2026-07"), 3);
            Assert.Equal(2529.159469, Valor(cuadro, "PTC", "2026-07"), 6);
        }

        [FactSql(DisplayName = "SQL: los cuadros macro conservan las fechas del JSON")]
        public async Task MacroIntacto()
        {
            var cuadro = await Get("/api/cuadros/IEM111");
            Assert.Equal("2000-01", cuadro.GetProperty("periodosDisponibles").GetProperty("desde").GetString());
        }

        [FactSql(DisplayName = "SQL: ranking de activos (índice de reportes con una sola carga masiva desde B11)")]
        public async Task Ranking()
        {
            var ranking = await Get("/api/rankings?cuenta=@1&fecha=2026-07&agrupacion=todas");
            var pichincha = ranking.GetProperty("filas").EnumerateArray().First(f => f.GetProperty("entidadId").GetString() == "BP__PICHINCHA");
            Assert.Equal(1, pichincha.GetProperty("posicion").GetInt32());
            Assert.Equal(22806.742733, pichincha.GetProperty("actual").GetDouble(), 6);
        }

        [FactSql(DisplayName = "SQL: reporte REP01 con la ventana de la BD y /api/meta con la fuente SQL")]
        public async Task ReporteYMeta()
        {
            var reporte = await Get("/api/entidades/BP__PICHINCHA/reporte?codigos=@1");
            Assert.Equal("2024-01", reporte.GetProperty("periodos")[0].GetString());

            var meta = await Get("/api/meta");
            Assert.Contains("sql-bco_coop", meta.GetRawText(), StringComparison.Ordinal);
        }
    }
}
