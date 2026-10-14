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

    /// <summary>
    /// Como <see cref="FactSqlAttribute"/>, pero además se omite si la BD aún no tiene las operaciones
    /// en el tiempo (db/fase3/03_operaciones_tiempo.sql y 04_formulas_tiempo.sql).
    /// </summary>
    public sealed class FactSqlFase3Attribute : FactAttribute
    {
        private static readonly Lazy<string?> Motivo = new(() =>
        {
            if (SqlApiFactory.MotivoOmision.Value is { } motivo)
                return motivo;
            try
            {
                var raiz = Path.GetDirectoryName(ApiFactory.RutaDatos())!;
                var cadena = new ConfigurationBuilder()
                    .AddJsonFile(Path.Combine(raiz, "appsettings.json"), optional: true)
                    .AddJsonFile(Path.Combine(raiz, "appsettings.Development.json"), optional: true)
                    .AddUserSecrets<Program>(optional: true)
                    .Build().GetConnectionString("DefaultConnection");
                using var conexion = new SqlConnection(cadena);
                conexion.Open();
                using var comando = new SqlCommand(
                    "SELECT CASE WHEN COL_LENGTH('dbo.CuadroFila', 'OperacionN') IS NULL THEN 0 " +
                    "ELSE (SELECT COUNT(*) FROM dbo.CuadroFila WHERE CuadroID = 'EFI06' AND Calculo = 'RATIO' " +
                    "AND IdSub = N'Análisis Horizontal (%)') END", conexion);
                return (int)comando.ExecuteScalar()! > 0 ? null : "La BD aún no tiene db/fase3/03 y 04 (operaciones en el tiempo).";
            }
            catch (Exception ex)
            {
                return $"BD no disponible: {ex.GetBaseException().Message}";
            }
        });

        public FactSqlFase3Attribute()
        {
            if (Motivo.Value is { } motivo)
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

        [FactSql(DisplayName = "Catálogo: EFI01 en el orden de la BD, texto de dbo.Cuenta y pasipat calculado por el SP")]
        public async Task CatalogoEfi01()
        {
            var cuadro = await Get("/api/cuadros/EFI01?entidad=BP__PICHINCHA");
            var filas = cuadro.GetProperty("filas").EnumerateArray().ToList();

            Assert.Equal(202, filas.Count);
            Assert.Equal("@1", filas[0].GetProperty("cuc").GetString());
            Assert.Equal("Bancos y otras entidades financieras",
                filas.First(f => f.GetProperty("cuc").GetString() == "@1103").GetProperty("variable").GetString());
            Assert.Equal(22660.4187, Valor(cuadro, "pasipat", "2026-07"), 3);
        }

        [FactSql(DisplayName = "Catálogo: márgenes de EFI02 calculados por el SP = JSON")]
        public async Task CatalogoEfi02()
        {
            var cuadro = await Get("/api/cuadros/EFI02?entidad=BP__PICHINCHA");
            Assert.Equal(764.6353, Valor(cuadro, "Marg_Ne", "2026-07"), 3);
            Assert.Equal(1087.4414, Valor(cuadro, "Mar_Br_Fi", "2026-07"), 3);
            Assert.Equal(146.324, Valor(cuadro, "Gan_Eje", "2026-07"), 3);
        }

        [FactSql(DisplayName = "Catálogo: SFN06 de bancos grandes con la plantilla de bancos (1 109 cuentas)")]
        public async Task CatalogoSfn06()
        {
            var cuadro = await Get("/api/cuadros/SFN06?sector=grande&analisis=saldo");
            var filas = cuadro.GetProperty("filas").EnumerateArray().ToList();
            Assert.Equal(1109, filas.Count);
            Assert.StartsWith("1101.", filas.First(f => f.GetProperty("codigoBase").GetString() == "1101").GetProperty("variable").GetString());
        }

        [FactSql(DisplayName = "Fase 3: análisis vertical de EFI06 calculado por el SP (100 × cuenta / raíz del grupo)")]
        public async Task Fase3AnalisisVertical()
        {
            var cuadro = await Get("/api/cuadros/EFI06?entidad=BP__PICHINCHA&analisis=vertical");
            var periodos = cuadro.GetProperty("periodos").EnumerateArray().Select(p => p.GetString()).ToList();
            double ValorCuenta(string codigo) => cuadro.GetProperty("filas").EnumerateArray()
                .First(f => f.GetProperty("codigoBase").GetString() == codigo)
                .GetProperty("valores")[periodos.IndexOf("2026-06")].GetDouble();

            Assert.Equal(7.19, ValorCuenta("11"), 2);      // JSON publica 7,19; el SP da 7,1905
            Assert.Equal(100.0, ValorCuenta("1"), 6);
            Assert.Equal(100.0, ValorCuenta("2"), 6);      // cada grupo sobre su raíz
        }

        // ---- Fase 3 · paso 2: operaciones en el tiempo (valores del JSON publicado) ----

        private static (double? Valor, string? SinHistoriaHasta) Celda(JsonElement cuadro, Func<JsonElement, bool> fila, string periodo)
        {
            var periodos = cuadro.GetProperty("periodos").EnumerateArray().Select(p => p.GetString()).ToList();
            var f = cuadro.GetProperty("filas").EnumerateArray().First(fila);
            var v = f.GetProperty("valores")[periodos.IndexOf(periodo)];
            var marca = f.TryGetProperty("sinHistoriaHasta", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            return (v.ValueKind == JsonValueKind.Null ? null : v.GetDouble(), marca);
        }

        [FactSqlFase3(DisplayName = "Fase 3: análisis horizontal de EFI06 = 100 × (saldo_t / saldo_t−12 − 1); 2024 sin historia")]
        public async Task Fase3AnalisisHorizontal()
        {
            var cuadro = await Get("/api/cuadros/EFI06?entidad=BP__PICHINCHA&analisis=horizontal");
            bool Activo(JsonElement f) => f.GetProperty("codigoBase").GetString() == "1";

            var (valor, marca) = Celda(cuadro, Activo, "2026-06");
            Assert.Equal(7.22, valor!.Value, 2);            // JSON publica 7,22
            Assert.Equal("2024-12", marca);
            Assert.Null(Celda(cuadro, Activo, "2024-06").Valor);   // falta 2023-06 en la BD
        }

        [FactSqlFase3(DisplayName = "Fase 3: EFI03 anualizado (saldo × 12 / mes) de BP. AMAZONAS")]
        public async Task Fase3Anualizado()
        {
            var cuadro = await Get("/api/cuadros/EFI03?entidad=BP__AMAZONAS");
            bool Ingresos(JsonElement f) => f.GetProperty("cuc").GetString() == "@5A";

            Assert.Equal(50.1663, Celda(cuadro, Ingresos, "2026-07").Valor!.Value, 4);
            Assert.Equal(44.5582, Celda(cuadro, Ingresos, "2025-01").Valor!.Value, 4);
            Assert.Null(Celda(cuadro, Ingresos, "2026-07").SinHistoriaHasta);   // no usa meses anteriores
        }

        [FactSqlFase3(DisplayName = "Fase 3: EFI04 variación mensual (saldo_t − saldo_t−1); el primer mes sin historia")]
        public async Task Fase3VariacionMensual()
        {
            var cuadro = await Get("/api/cuadros/EFI04?entidad=BP__PICHINCHA");
            bool Activo(JsonElement f) => f.GetProperty("variable").GetString() == "ACTIVO"
                                          && f.GetProperty("grupo").GetString()!.StartsWith("VARIACI", StringComparison.Ordinal);

            var (valor, marca) = Celda(cuadro, Activo, "2026-06");
            Assert.Equal(-341.04, valor!.Value, 2);
            Assert.Equal("2024-01", marca);
            Assert.Null(Celda(cuadro, Activo, "2024-01").Valor);
        }

        [FactSqlFase3(DisplayName = "Fase 3: ROA y ROE de EFI11 (promedio desde diciembre; diciembre con su regla) y tasa_IF011")]
        public async Task Fase3RoaRoe()
        {
            var cuadro = await Get("/api/cuadros/EFI11?entidad=BP__PICHINCHA");
            bool Cuc(JsonElement f, string c) => f.GetProperty("cuc").GetString() == c;

            Assert.Equal(1.146, Celda(cuadro, f => Cuc(f, "IF002"), "2026-06").Valor!.Value, 3);
            Assert.Equal(1.1164, Celda(cuadro, f => Cuc(f, "IF002"), "2025-12").Valor!.Value, 4);
            Assert.Equal(11.7935, Celda(cuadro, f => Cuc(f, "IF004"), "2026-06").Valor!.Value, 4);
            Assert.Equal(12.2043, Celda(cuadro, f => Cuc(f, "IF004"), "2025-12").Valor!.Value, 4);
            Assert.Equal("2024-12", Celda(cuadro, f => Cuc(f, "IF002"), "2026-06").SinHistoriaHasta);
            Assert.Contains(cuadro.GetProperty("notas").EnumerateArray(), n => n.GetString()!.Contains("hasta 2024-12", StringComparison.Ordinal));

            var cartera = await Get("/api/cuadros/EFI07?entidad=BP__PICHINCHA&credito=total");
            Assert.Equal(7.2768, Celda(cartera, f => Cuc(f, "tasa_IF011"), "2026-06").Valor!.Value, 4);
        }

        // ---- Fuente de los datos (bd / json / mixto) ----

        [FactSql(DisplayName = "Fuente de datos: EFI01 todo de la BD, EFI11 mixto, un cuadro macro todo JSON")]
        public async Task FuenteDatos()
        {
            var efi01 = await Get("/api/cuadros/EFI01?entidad=BP__PICHINCHA");
            Assert.Equal("bd", efi01.GetProperty("fuenteDatos").GetString());
            Assert.All(efi01.GetProperty("filas").EnumerateArray(), f => Assert.Equal("bd", f.GetProperty("fuenteDatos").GetString()));

            var efi11 = await Get("/api/cuadros/EFI11?entidad=BP__PICHINCHA");
            var filas = efi11.GetProperty("filas").EnumerateArray().ToList();
            Assert.Equal("mixto", efi11.GetProperty("fuenteDatos").GetString());
            Assert.Equal("bd", filas.First(f => f.GetProperty("cuc").GetString() == "IF002").GetProperty("fuenteDatos").GetString());
            Assert.Equal("json", filas.First(f => f.GetProperty("cuc").GetString() == "IF012").GetProperty("fuenteDatos").GetString());
            var porFuente = efi11.GetProperty("filasPorFuente");
            Assert.Equal(filas.Count, porFuente.GetProperty("bd").GetInt32() + porFuente.GetProperty("json").GetInt32()
                                      + porFuente.GetProperty("sinFuente").GetInt32());

            var macro = await Get("/api/cuadros/IEA111A");
            Assert.Equal("json", macro.GetProperty("fuenteDatos").GetString());
        }

        [FactSql(DisplayName = "Fuente de datos: /api/meta resume los cuadros por fuente (o advierte si falta el script 08)")]
        public async Task FuenteDatosMeta()
        {
            var meta = await Get("/api/meta");
            var porFuente = meta.GetProperty("cuadrosPorFuente");
            if (porFuente.ValueKind == JsonValueKind.Null)
            {
                Assert.Contains(meta.GetProperty("advertencias").EnumerateArray(), a => a.GetString()!.Contains("08_origen_cuadros"));
                return;
            }
            Assert.True(porFuente.GetProperty("bd").GetInt32() > 0);      // EFI01, EFI02, SFN01…
            Assert.True(porFuente.GetProperty("mixto").GetInt32() > 0);   // EFI11, REP01…
            Assert.True(porFuente.GetProperty("json").GetInt32() > 100);  // macro y cuadros sin fórmulas
        }

        [FactSql(DisplayName = "Catálogo: EFI09 (no sale del .sav) conserva las fechas del JSON")]
        public async Task CatalogoFechasJson()
        {
            var cuadro = await Get("/api/cuadros/EFI09?entidad=BP__PICHINCHA&credito=total");
            Assert.Equal("2021-05", cuadro.GetProperty("periodos")[0].GetString());
            Assert.NotEmpty(cuadro.GetProperty("filas").EnumerateArray());
        }

        [FactSql(DisplayName = "Geográficos: GEE01 (no sale del .sav) conserva las fechas del JSON")]
        public async Task GeograficoFechasJson()
        {
            var cuadro = await Get("/api/cuadros/GEE01?entidad=BP__PICHINCHA&credito=productivo");
            Assert.Equal("2021-05", cuadro.GetProperty("periodos")[0].GetString());
            Assert.NotEmpty(cuadro.GetProperty("filas").EnumerateArray());
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
            Assert.Contains("catalogo-cuadros", meta.GetRawText(), StringComparison.Ordinal);
        }
    }
}
