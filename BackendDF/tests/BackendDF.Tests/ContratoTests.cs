using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BackendDF.Tests
{
    /// <summary>Reglas del contrato no cubiertas por §13: períodos, catálogos, series, caché HTTP.</summary>
    [Collection(ColeccionApi.Nombre)]
    public class ContratoTests
    {
        private readonly HttpClient _cliente;

        public ContratoTests(ApiFactory factory)
        {
            _cliente = factory.ClienteAutenticado();
        }

        private async Task<(HttpStatusCode Status, JsonElement Json, HttpResponseMessage Respuesta)> Get(string url)
        {
            var respuesta = await _cliente.GetAsync(url);
            var texto = await respuesta.Content.ReadAsStringAsync();
            return (respuesta.StatusCode, texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone(), respuesta);
        }

        [Fact(DisplayName = "Anual acepta YYYY y filtra de forma inclusiva")]
        public async Task PeriodosAnualesInclusivos()
        {
            var (_, json, _) = await Get("/api/cuadros/IEA111A?desde=2015&hasta=2025-12");
            var periodos = json.GetProperty("periodos").EnumerateArray().Select(p => p.GetString()).ToArray();
            Assert.Equal(11, periodos.Length);
            Assert.Equal("2015-12", periodos[0]);
            Assert.Equal("2025-12", periodos[^1]);
            Assert.All(json.GetProperty("filas").EnumerateArray(), f => Assert.Equal(11, f.GetProperty("valores").GetArrayLength()));
            Assert.Equal("2000-12", json.GetProperty("periodosDisponibles").GetProperty("desde").GetString());
        }

        [Fact(DisplayName = "Formato de período incorrecto para la frecuencia: 400")]
        public async Task FormatoPeriodoInvalido()
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Get("/api/cuadros/SFN01?sector=nacional&desde=2024")).Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await Get("/api/cuadros/SFN01?sector=nacional&desde=2024-13")).Status);
        }

        [Fact(DisplayName = "Sector inválido: 400; cuadro inexistente: 404")]
        public async Task ValoresInvalidos()
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Get("/api/cuadros/SFN01?sector=marte")).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/cuadros/XYZ99")).Status);
        }

        [Fact(DisplayName = "Macro: las claves repetidas llevan #n y son únicas")]
        public async Task ClavesUnicas()
        {
            var (_, lista, _) = await Get("/api/cuadros?origen=macro");
            Assert.Equal(195, lista.GetArrayLength());

            foreach (var id in new[] { "IEM111", "IEA132A" })
            {
                var (_, json, _) = await Get($"/api/cuadros/{id}");
                var claves = json.GetProperty("filas").EnumerateArray().Select(f => f.GetProperty("clave").GetString()).ToArray();
                Assert.Equal(claves.Length, claves.Distinct().Count());
            }
        }

        [Fact(DisplayName = "Listado de cuadros con tipo, vista y parámetros")]
        public async Task ListadoCuadros()
        {
            var (_, json, _) = await Get("/api/cuadros?q=balance");
            var sfn06 = json.EnumerateArray().Single(c => c.GetProperty("id").GetString() == "SFN06");
            Assert.Equal("balances", sfn06.GetProperty("tipo").GetString());
            Assert.Equal("arbol", sfn06.GetProperty("vista").GetString());
            Assert.Equal(["sector", "analisis"], sfn06.GetProperty("parametros").EnumerateArray().Select(p => p.GetString()));
        }

        [Fact(DisplayName = "Catálogos: 10 sectores y valores distintos de las entidades")]
        public async Task Catalogos()
        {
            var (_, json, _) = await Get("/api/catalogos");
            Assert.Equal(10, json.GetProperty("sectores").GetArrayLength());
            Assert.Equal(7, json.GetProperty("creditos").GetArrayLength());
            Assert.Contains("Pichincha", json.GetProperty("provincias").EnumerateArray().Select(p => p.GetString()));
        }

        [Fact(DisplayName = "Búsqueda de entidades sin tildes ni mayúsculas")]
        public async Task BusquedaEntidades()
        {
            var (_, json, _) = await Get("/api/entidades?q=pichincha");
            Assert.Contains("BP__PICHINCHA", json.EnumerateArray().Select(e => e.GetProperty("id").GetString()));
            var (status, uno, _) = await Get("/api/entidades/bp__pichincha");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("BP__PICHINCHA", uno.GetProperty("id").GetString());
            Assert.Equal("banco", uno.GetProperty("tipo").GetString());
        }

        [Fact(DisplayName = "Series entre entidades: máximo 4")]
        public async Task SeriesEntidades()
        {
            var (status, json, respuesta) = await Get("/api/entidades/series?ids=BP__AMAZONAS,BP__PICHINCHA&codigos=@1,NOPE");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(2, json.GetProperty("series").GetArrayLength());
            Assert.Equal("NOPE", respuesta.Headers.GetValues("X-Codigos-No-Encontrados").Single());

            var cinco = "BP__AMAZONAS,BP__PICHINCHA,BP__AUSTRO,BP__BOLIVARIANO,BP__ATLANTIDA";
            Assert.Equal(HttpStatusCode.BadRequest, (await Get($"/api/entidades/series?ids={cinco}&codigos=@1")).Status);
        }

        [Fact(DisplayName = "Series del sistema por sector (hoja 2)")]
        public async Task SeriesSistema()
        {
            var (_, json, _) = await Get("/api/sistema/series?codigos=@1,@2,@3,Gan_Eje&cuadros=SFN01,SFN02");
            var series = json.GetProperty("series").EnumerateArray().ToArray();
            Assert.Equal(40, series.Length);
            var activo = series.First(s => s.GetProperty("sector").GetString() == "nacional" && s.GetProperty("codigo").GetString() == "@1");
            Assert.Equal(66925.4, Math.Round(activo.GetProperty("valores")[0].GetDouble(), 1));
        }

        [Fact(DisplayName = "Meta: versión, último corte común y advertencias")]
        public async Task Meta()
        {
            var (_, json, _) = await Get("/api/meta");
            Assert.Equal("2026-07", json.GetProperty("ultimoCorteComun").GetString());
            Assert.Contains(json.GetProperty("advertencias").EnumerateArray(), a => a.GetString()!.Contains("balances/: 101 archivos"));
        }

        [Fact(DisplayName = "ETag + Cache-Control y 304 con If-None-Match")]
        public async Task CacheHttp()
        {
            var (_, _, respuesta) = await Get("/api/catalogos");
            var etag = respuesta.Headers.ETag;
            Assert.NotNull(etag);
            Assert.True(respuesta.Headers.CacheControl?.Private);
            Assert.Equal(TimeSpan.FromHours(1), respuesta.Headers.CacheControl?.MaxAge);

            using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/catalogos");
            pedido.Headers.IfNoneMatch.Add(etag!);
            var segunda = await _cliente.SendAsync(pedido);
            Assert.Equal(HttpStatusCode.NotModified, segunda.StatusCode);
        }

        [Fact(DisplayName = "Compresión Brotli")]
        public async Task Compresion()
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/entidades/BP__PICHINCHA/reporte");
            pedido.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
            var respuesta = await _cliente.SendAsync(pedido);
            Assert.Contains("br", respuesta.Content.Headers.ContentEncoding);
        }

        [Fact(DisplayName = "Ruta inexistente bajo /api: 404 con envelope")]
        public async Task RutaInexistente()
        {
            var (status, json, _) = await Get("/api/no-existe");
            Assert.Equal(HttpStatusCode.NotFound, status);
            Assert.Equal("NOT_FOUND", json.GetProperty("errorCode").GetString());
        }
    }
}
