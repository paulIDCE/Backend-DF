using System.Net;
using System.Text.Json;

namespace BackendDF.Tests
{
    /// <summary>Criterios de aceptación del contrato (docs/backend/CONTRATO_API.md §13).</summary>
    [Collection(ColeccionApi.Nombre)]
    public class CriteriosAceptacionTests
    {
        private readonly ApiFactory _factory;
        private readonly HttpClient _cliente;

        public CriteriosAceptacionTests(ApiFactory factory)
        {
            _factory = factory;
            _cliente = factory.ClienteAutenticado();
        }

        private async Task<(HttpStatusCode Status, JsonElement Json, HttpResponseMessage Respuesta)> Get(string url, HttpClient? cliente = null)
        {
            var respuesta = await (cliente ?? _cliente).GetAsync(url);
            var texto = await respuesta.Content.ReadAsStringAsync();
            var json = texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone();
            return (respuesta.StatusCode, json, respuesta);
        }

        private static JsonElement[] Filas(JsonElement cuadro) => cuadro.GetProperty("filas").EnumerateArray().ToArray();

        private static void EsError(JsonElement json, int status, string errorCode)
        {
            Assert.False(json.GetProperty("success").GetBoolean());
            Assert.Equal(status, json.GetProperty("status").GetInt32());
            Assert.Equal(errorCode, json.GetProperty("errorCode").GetString());
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("message").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
            Assert.Equal(JsonValueKind.Null, json.GetProperty("data").ValueKind);
        }

        [Fact(DisplayName = "#1 IEA111A: 13 filas, 26 períodos y valor 2025-12")]
        public async Task C01_MacroAnual()
        {
            var (status, json, _) = await Get("/api/cuadros/IEA111A");
            Assert.Equal(HttpStatusCode.OK, status);
            var periodos = json.GetProperty("periodos").EnumerateArray().Select(p => p.GetString()).ToArray();
            Assert.Equal(26, periodos.Length);
            Assert.Equal("2000-12", periodos[0]);
            Assert.Equal("2025-12", periodos[^1]);
            Assert.Equal("OFERTA MONETARIA (M1) Y LIQUIDEZ TOTAL (M2)", json.GetProperty("titulo").GetString());
            Assert.Equal("anual", json.GetProperty("frecuencia").GetString());

            var filas = Filas(json);
            Assert.Equal(13, filas.Length);
            Assert.Equal("Especies Monetarias en Circulación (1)", filas[0].GetProperty("variable").GetString());
            var valores = filas[0].GetProperty("valores").EnumerateArray().ToArray();
            Assert.Equal(21549.2, Math.Round(valores[^1].GetDouble(), 1));
            Assert.NotEmpty(json.GetProperty("notas").EnumerateArray());
        }

        /// <remarks>
        /// El contrato indica 70 períodos (2021-05 … 2027-02), pero ese es el rango del archivo
        /// completo: en base_estru_sistema solo TPE01 llega a 2027-02. Las filas de SFN01 llegan a
        /// 2027-01, y la unión de sus períodos (misma lógica que el frontend) da 69.
        /// </remarks>
        [Fact(DisplayName = "#2 SFN01 nacional: 212 filas")]
        public async Task C02_Sistema()
        {
            var (status, json, _) = await Get("/api/cuadros/SFN01?sector=nacional");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(212, Filas(json).Length);
            var periodos = json.GetProperty("periodos").EnumerateArray().Select(p => p.GetString()).ToArray();
            Assert.Equal(69, periodos.Length);
            Assert.Equal("2021-05", periodos[0]);
            Assert.Equal("2027-01", periodos[^1]);
            Assert.Equal("SFN01|nacional|@1", Filas(json)[0].GetProperty("clave").GetString());
        }

        [Fact(DisplayName = "#3 SFN06 saldo: árbol con 7 raíces y padre por prefijo")]
        public async Task C03_BalancesArbol()
        {
            var (status, json, _) = await Get("/api/cuadros/SFN06?sector=nacional&analisis=saldo");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("arbol", json.GetProperty("vista").GetString());
            var filas = Filas(json);
            Assert.Equal(1517, filas.Length);

            var raices = filas.Where(f => f.GetProperty("nivel").GetInt32() == 1).ToArray();
            Assert.Equal(7, raices.Length);
            Assert.All(raices, r => Assert.Equal(JsonValueKind.Null, r.GetProperty("padre").ValueKind));
            Assert.Equal("1.    ACTIVO", raices[0].GetProperty("variable").GetString());
            Assert.Equal("7.    CUENTAS DE ORDEN", raices[^1].GetProperty("variable").GetString());

            var fondos = filas.Single(f => f.GetProperty("variable").GetString() == "11.    FONDOS DISPONIBLES");
            var padre = filas[fondos.GetProperty("padre").GetInt32()];
            Assert.Equal("1.    ACTIVO", padre.GetProperty("variable").GetString());
            Assert.Empty(json.GetProperty("notas").EnumerateArray());
        }

        [Fact(DisplayName = "#4 CAR01 nacional total: 42 filas")]
        public async Task C04_Cartera()
        {
            var (_, json, _) = await Get("/api/cuadros/CAR01?sector=nacional&credito=total");
            Assert.Equal(42, Filas(json).Length);
        }

        [Fact(DisplayName = "#5 TEA01 seg1 productivo: 21 filas en Porcentajes")]
        public async Task C05_Tasas()
        {
            var (_, json, _) = await Get("/api/cuadros/TEA01?sector=seg1&credito=productivo");
            Assert.Equal(21, Filas(json).Length);
            Assert.Equal("Porcentajes", json.GetProperty("unidad").GetString());
        }

        [Fact(DisplayName = "#6 EFI06 PICHINCHA vertical: 1.098 filas")]
        public async Task C06_BalanceEntidad()
        {
            var (_, json, _) = await Get("/api/cuadros/EFI06?entidad=BP__PICHINCHA&analisis=vertical");
            Assert.Equal(1098, Filas(json).Length);
            Assert.Equal("BP. PICHINCHA", json.GetProperty("contexto").GetProperty("entidadNombre").GetString());
        }

        [Fact(DisplayName = "#7 EFI08 PICHINCHA productivo: 24 filas")]
        public async Task C07_CarteraEntidad()
        {
            var (_, json, _) = await Get("/api/cuadros/EFI08?entidad=BP__PICHINCHA&credito=productivo");
            Assert.Equal(24, Filas(json).Length);
        }

        [Fact(DisplayName = "#8 TPE02 PICHINCHA: 7 filas")]
        public async Task C08_Entidad()
        {
            var (_, json, _) = await Get("/api/cuadros/TPE02?entidad=BP__PICHINCHA");
            Assert.Equal(7, Filas(json).Length);
        }

        [Fact(DisplayName = "#9 SFN06 sin analisis: 400")]
        public async Task C09_FaltaParametro()
        {
            var (status, json, _) = await Get("/api/cuadros/SFN06?sector=nacional");
            Assert.Equal(HttpStatusCode.BadRequest, status);
            EsError(json, 400, "VALIDATION_ERROR");
            Assert.Contains("analisis", json.GetProperty("message").GetString());
        }

        [Fact(DisplayName = "#10 Entidad inexistente: 404")]
        public async Task C10_EntidadInexistente()
        {
            var (status, json, _) = await Get("/api/cuadros/EFI01?entidad=NO_EXISTE");
            Assert.Equal(HttpStatusCode.NotFound, status);
            EsError(json, 404, "NOT_FOUND");
        }

        [Fact(DisplayName = "#11 Parámetro que no aplica: 400")]
        public async Task C11_ParametroNoAplica()
        {
            var (status, json, _) = await Get("/api/cuadros/EFI01?entidad=BP__PICHINCHA&sector=nacional");
            Assert.Equal(HttpStatusCode.BadRequest, status);
            EsError(json, 400, "VALIDATION_ERROR");
        }

        [Fact(DisplayName = "#12 Ranking de activos por sector")]
        public async Task C12_Ranking()
        {
            var (status, json, _) = await Get("/api/rankings?cuenta=@1&fecha=2026-07&agrupacion=sector&entidad=BP__AMAZONAS");
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("2025-07", json.GetProperty("fechaComparacion").GetString());
            Assert.Equal("Banco Privado Pequeño", json.GetProperty("agrupacion").GetProperty("valor").GetString());
            Assert.Equal(2546.5, Math.Round(json.GetProperty("total").GetProperty("actual").GetDouble(), 1));
            Assert.Equal(2250.9, Math.Round(json.GetProperty("total").GetProperty("anterior").GetDouble(), 1));
            Assert.Equal(2, json.GetProperty("posicionEntidad").GetInt32());

            var filas = Filas(json);
            Assert.Equal(10, filas.Length);
            (string, double)[] esperado = [("BP. PROCREDIT", 28.19), ("BP. AMAZONAS", 20.51), ("BP. BANCODESARROLLO", 12.19)];
            for (var i = 0; i < esperado.Length; i++)
            {
                Assert.Equal(i + 1, filas[i].GetProperty("posicion").GetInt32());
                Assert.Equal(esperado[i].Item1, filas[i].GetProperty("nombre").GetString());
                Assert.Equal(esperado[i].Item2, Math.Round(filas[i].GetProperty("participacionActual").GetDouble(), 2));
            }
        }

        [Fact(DisplayName = "#13 Entidades por tamaño")]
        public async Task C13_EntidadesPorTamano()
        {
            var (_, json, _) = await Get("/api/entidades?tamano=Banco%20Privado%20Peque%C3%B1o");
            Assert.Equal(10, json.GetArrayLength());
        }

        [Fact(DisplayName = "#14 Catálogo: 229 entidades, sin huérfanos")]
        public async Task C14_Entidades()
        {
            var (_, json, _) = await Get("/api/entidades");
            var ids = json.EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToArray();
            Assert.Equal(229, ids.Length);
            Assert.DoesNotContain("Bancos_Privados_Pequenos", ids);
            Assert.DoesNotContain("COAC___SOLIDARIA_LTDA", ids);

            var nombres = json.EnumerateArray().Select(e => e.GetProperty("nombre").GetString()!).ToArray();
            Assert.Equal(nombres.OrderBy(n => n, Logic.Dominio.Texto.ComparadorNombres), nombres);
        }

        [Fact(DisplayName = "#15 Reporte completo de PICHINCHA")]
        public async Task C15_Reporte()
        {
            var (_, json, _) = await Get("/api/entidades/BP__PICHINCHA/reporte");
            Assert.Equal(776, json.GetProperty("cuentas").GetArrayLength());
            var periodos = json.GetProperty("periodos").EnumerateArray().Select(p => p.GetString()).ToArray();
            Assert.Equal(63, periodos.Length);
            Assert.Equal("2021-05", periodos[0]);
            Assert.Equal("2026-07", periodos[^1]);
            var entidad = json.GetProperty("entidad");
            Assert.Equal("Banco Privado Grande", entidad.GetProperty("tamano").GetString());
            Assert.Equal("Pichincha", entidad.GetProperty("provincia").GetString());
        }

        [Fact(DisplayName = "#16 Reporte con códigos (CUC, Variable e inexistente)")]
        public async Task C16_ReporteCodigos()
        {
            var (status, json, respuesta) = await Get("/api/entidades/BP__PICHINCHA/reporte?codigos=@1,Percentil%2075,NOPE");
            Assert.Equal(HttpStatusCode.OK, status);
            var cuentas = json.GetProperty("cuentas").EnumerateArray().Select(c => c.GetProperty("cuc").GetString()!).ToArray();
            Assert.Equal(["@1", "perc75_turb"], cuentas);
            Assert.Equal("NOPE", respuesta.Headers.GetValues("X-Codigos-No-Encontrados").Single());
        }

        [Fact(DisplayName = "#17 Sin token o con token inválido: 401 con envelope")]
        public async Task C17_SinToken()
        {
            var anonimo = _factory.CreateClient();
            var (status, json, _) = await Get("/api/catalogos", anonimo);
            Assert.Equal(HttpStatusCode.Unauthorized, status);
            EsError(json, 401, "UNAUTHORIZED");

            var otraFirma = _factory.CreateClient();
            otraFirma.DefaultRequestHeaders.Authorization = new("Bearer", ApiFactory.Token("otro-secreto-distinto-de-al-menos-32-bytes"));
            Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/api/cuadros/IEA111A", otraFirma)).Status);

            var vencido = _factory.CreateClient();
            vencido.DefaultRequestHeaders.Authorization = new("Bearer", ApiFactory.Token(expira: DateTime.UtcNow.AddMinutes(-5)));
            Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/api/cuadros/IEA111A", vencido)).Status);

            // /api/health es la única ruta sin auth.
            Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/health")).StatusCode);
        }

        [Fact(DisplayName = "#18 desde > hasta: 400")]
        public async Task C18_RangoInvertido()
        {
            var (status, json, _) = await Get("/api/cuadros/IEA111A?desde=2026&hasta=2020");
            Assert.Equal(HttpStatusCode.BadRequest, status);
            EsError(json, 400, "VALIDATION_ERROR");
        }

        [Fact(DisplayName = "#19 Número guardado como texto en notación científica")]
        public async Task C19_NumeroComoTexto()
        {
            // En base_anual, IEA122A "c.2 Crédito a los gobiernos provinciales..." 2025-12 = "5.7262000000000003E-4".
            var (_, json, _) = await Get("/api/cuadros/IEA122A?desde=2025&hasta=2025");
            var fila = Filas(json).First(f => f.GetProperty("variable").GetString()!.StartsWith("c.2 Crédito a los gobiernos provinciales"));
            var valor = fila.GetProperty("valores")[0];
            Assert.Equal(JsonValueKind.Number, valor.ValueKind);
            Assert.Equal(0.00057262, valor.GetDouble(), 12);
        }
    }
}
