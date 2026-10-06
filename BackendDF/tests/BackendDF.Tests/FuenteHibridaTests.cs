using BackendDF.Configuration;
using BackendDF.Data.FuenteHibrida;
using BackendDF.Data.FuenteSql;
using BackendDF.Data.Interfaces;
using BackendDF.Data.Repositorios;
using BackendDF.Logic.Dominio;
using BackendDF.Models.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace BackendDF.Tests
{
    /// <summary>
    /// Reglas de la fuente híbrida (informe §8.3) con dobles en memoria: sin BD ni archivos JSON.
    /// Ventana de prueba: 2024-01 → 2024-03; entidad BP__X = IFIID 260.
    /// </summary>
    public class FuenteHibridaTests
    {
        private static readonly string[] EjeJson = ["2023-12", "2024-01", "2024-02"];

        // ---------------------------------------------------------------------------------
        // Overlay por fila
        // ---------------------------------------------------------------------------------

        [Fact(DisplayName = "Híbrida: cuenta desde SQL en millones, otras filas del JSON recortadas a la ventana, 6/7 ocultas")]
        public async Task EntidadCombinaSqlYJson()
        {
            var json = new FuenteFalsa
            {
                Filas =
                [
                    Fila("EFI01", cuc: "@1", valores: [9, 9, 9]),
                    Fila("EFI01", cuc: "@6", valores: [5, 5, 5]),
                    Fila("EFI01", cuc: "pasipat", valores: [1, 2, 3])
                ]
            };
            var fuente = Crear(json, new RepositorioFalso());

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI01", null, "BP__X", null), default);

            Assert.Equal(["@1", "pasipat"], filas.Select(f => f.Cuc));
            Assert.All(filas, f => Assert.Equal(["2024-01", "2024-02", "2024-03"], f.Periodos));
            Assert.Equal([1.0, 2.0, 3.0], filas[0].Valores);                  // B11 en USD / 1e6
            Assert.Equal(2.0, filas[1].Valores[0]);                          // JSON 2024-01
            Assert.Equal(3.0, filas[1].Valores[1]);                          // JSON 2024-02
            Assert.True(double.IsNaN(filas[1].Valores[2]));                  // JSON sin 2024-03 → sin valor
            Assert.Same(filas[0].Periodos, filas[1].Periodos);               // eje compartido
            Assert.Equal(EjeJson, json.Filas[0].Periodos);                   // la caché JSON no se modifica
        }

        [Fact(DisplayName = "Híbrida: el mes sin fila en la BD queda sin valor (NaN ≠ 0)")]
        public async Task HuecoEnBdEsNaN()
        {
            var repo = new RepositorioFalso();
            repo.SaldosEntidad = Series(("1", "2024-01", 1e6), ("1", "2024-03", 3e6));
            var fuente = Crear(new FuenteFalsa { Filas = [Fila("EFI01", cuc: "@1", valores: [9, 9, 9])] }, repo);

            var fila = (await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI01", null, "BP__X", null), default)).Single();

            Assert.Equal(1.0, fila.Valores[0]);
            Assert.True(double.IsNaN(fila.Valores[1]));
            Assert.Equal(3.0, fila.Valores[2]);
        }

        [Theory(DisplayName = "Híbrida: macro y cuadros externos conservan las fechas del JSON")]
        [InlineData(TipoCuadro.Macro, "IEM111")]
        [InlineData(TipoCuadro.CarteraEntidad, "EFI09")]
        public async Task SinVentana(TipoCuadro tipo, string cuadro)
        {
            var json = new FuenteFalsa { Filas = [Fila(cuadro, cuc: "@1", valores: [9, 9, 9])] };
            var repo = new RepositorioFalso();
            var fuente = Crear(json, repo);

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(tipo, cuadro, null, "BP__X", null), default);

            Assert.Same(json.Filas, filas);
            Assert.Equal(0, repo.LlamadasSaldosEntidad);
        }

        [Fact(DisplayName = "Híbrida: en balances solo la fila de saldo viene de SQL; ramas 6/7 ocultas en todas")]
        public async Task Balances()
        {
            var json = new FuenteFalsa
            {
                Filas =
                [
                    Fila("EFI06", codigoBase: "1", id: ReglasCuadro.IdSaldo, valores: [9, 9, 9]),
                    Fila("EFI06", codigoBase: "1", id: "Análisis Horizontal (%)", valores: [4, 5, 6]),
                    Fila("EFI06", codigoBase: "6", id: ReglasCuadro.IdSaldo, valores: [9, 9, 9]),
                    Fila("EFI06", codigoBase: "7", id: "Análisis Horizontal (%)", valores: [9, 9, 9])
                ]
            };
            var fuente = Crear(json, new RepositorioFalso());

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.BalancesEntidad, "EFI06", null, "BP__X", null), default);

            Assert.Equal(2, filas.Count);
            Assert.Equal([1.0, 2.0, 3.0], filas[0].Valores);
            Assert.Equal(5.0, filas[1].Valores[0]);
        }

        [Fact(DisplayName = "Híbrida: indicadores de EFI05 desde IndicadorData con su escala (SOLVENCIA ×100)")]
        public async Task Indicadores()
        {
            var json = new FuenteFalsa
            {
                Filas =
                [
                    Fila("EFI05", cuc: "SOLVENCIA", valores: [9, 9, 9]),
                    Fila("EFI05", cuc: "PTC", valores: [9, 9, 9]),
                    Fila("EFI05", cuc: "PTR_9", valores: [7, 7, 7])
                ]
            };
            var fuente = Crear(json, new RepositorioFalso());

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI05", null, "BP__X", null), default);

            Assert.Equal(13.5, filas[0].Valores[0], 9);       // 0,135 × 100
            Assert.Equal(2529.159469, filas[1].Valores[0], 9); // USD / 1e6
            Assert.Equal(7.0, filas[2].Valores[0]);           // no está en la BD → JSON
        }

        [Theory(DisplayName = "Híbrida: el sector se traduce a tipos y segmentos de Ifi (C3)")]
        [InlineData("Bancos Privados Grandes", "BP", "1")]
        [InlineData("Sistema Financiero Nacional (privado y eps)", "BP,COOP,MU", "")]
        [InlineData("Coop. Segmento 2", "COOP", "2")]
        public async Task Sectores(string filtro, string tipos, string segmentos)
        {
            var repo = new RepositorioFalso();
            var fuente = Crear(new FuenteFalsa { Filas = [Fila("SFN01", cuc: "@1", valores: [9, 9, 9], filtro: filtro)] }, repo);

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Sistema, "SFN01", filtro, null, null), default);

            Assert.Equal(tipos, string.Join(',', repo.UltimosTipos!));
            Assert.Equal(segmentos, string.Join(',', repo.UltimosSegmentos ?? []));
            Assert.Equal([10.0, 20.0, 30.0], filas[0].Valores);
        }

        // ---------------------------------------------------------------------------------
        // Reportes
        // ---------------------------------------------------------------------------------

        [Fact(DisplayName = "Híbrida: REP01 toma las cuentas de SQL y los indicadores del JSON, en la ventana")]
        public async Task Reportes()
        {
            var reporte = new ReporteEntidad("BP__X", EjeJson,
                [new CuentaReporte("@1", "ACTIVO", [9, 9, 9]), new CuentaReporte("SOLVENCIA", null, [11, 12, 13])]);
            var json = new FuenteFalsa { Reportes = new Dictionary<string, ReporteEntidad> { ["BP__X"] = reporte } };
            var repo = new RepositorioFalso();
            var fuente = Crear(json, repo);

            var todos = await fuente.ObtenerReportesAsync(default);
            var uno = await fuente.ObtenerReporteAsync("BP__X", default);

            Assert.Equal(1, repo.LlamadasSaldosEntidades);
            Assert.Same(todos["BP__X"], uno);                    // reutiliza el índice ya armado
            Assert.Equal(["2024-01", "2024-02", "2024-03"], uno!.Periodos);
            Assert.Equal(2.0, uno.Valor(uno.BuscarCuenta("@1")!, "2024-02"));
            Assert.Equal(12.0, uno.Valor(uno.BuscarCuenta("SOLVENCIA")!, "2024-01"));
        }

        // ---------------------------------------------------------------------------------
        // Circuito, versión y mapa
        // ---------------------------------------------------------------------------------

        [Fact(DisplayName = "Híbrida: con SQL caído responde con el JSON, lo advierte y no reintenta hasta cerrar el circuito")]
        public async Task Fallback()
        {
            var json = new FuenteFalsa { Filas = [Fila("EFI01", cuc: "@1", valores: [9, 9, 9])] };
            var repo = new RepositorioFalso { Falla = true };
            var reloj = new RelojFalso();
            var fuente = Crear(json, repo, reloj);

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI01", null, "BP__X", null), default);
            Assert.Same(json.Filas, filas);
            Assert.True(fuente.SqlNoDisponible);

            await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI01", null, "BP__X", null), default);
            Assert.Equal(1, repo.LlamadasVentana);                // circuito abierto: no reintenta

            var info = await fuente.ObtenerInfoAsync(default);
            Assert.Contains(info.Advertencias, a => a.Contains("SQL no está disponible", StringComparison.Ordinal));

            repo.Falla = false;
            reloj.Avanzar(TimeSpan.FromSeconds(61));
            filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI01", null, "BP__X", null), default);
            Assert.False(fuente.SqlNoDisponible);
            Assert.Equal([1.0, 2.0, 3.0], filas[0].Valores);
        }

        [Fact(DisplayName = "Híbrida: si cambia la firma de la carga, cambia la versión y se vacían las cachés")]
        public async Task CambioDeFirma()
        {
            var repo = new RepositorioFalso();
            var reloj = new RelojFalso();
            var fuente = Crear(new FuenteFalsa { Filas = [Fila("EFI01", cuc: "@1", valores: [9, 9, 9])] }, repo, reloj);
            var consulta = new ConsultaFilas(TipoCuadro.Entidad, "EFI01", null, "BP__X", null);

            await fuente.ObtenerFilasAsync(consulta, default);
            await fuente.ObtenerFilasAsync(consulta, default);
            var v1 = await fuente.ObtenerVersionDatosAsync(default);
            Assert.Equal(1, repo.LlamadasSaldosEntidad);           // caché

            repo.Ventana = repo.Ventana with { Firma = "f2" };
            reloj.Avanzar(TimeSpan.FromMinutes(11));
            await fuente.ObtenerFilasAsync(consulta, default);
            var v2 = await fuente.ObtenerVersionDatosAsync(default);

            Assert.Equal(2, repo.LlamadasSaldosEntidad);
            Assert.True(v2 > v1);
        }

        [Fact(DisplayName = "Mapa de entidades: IFIID desalineado se resuelve por RUC; RUC ausente queda fuera")]
        public void MapaPorRuc()
        {
            var mapa = new MapaEntidades(
            [
                new("BP__A", 1, "R-A", null),
                new("BP__B", 2, "R-B", null),
                new("BP__C", 3, "R-C", null)
            ]);
            var (ids, advertencias) = mapa.Resolver([new IfiBd(1, "R-A", null, "BP", 1), new IfiBd(9, "R-B", null, "BP", 2)]);

            Assert.Equal(1, ids["bp__a"]);
            Assert.Equal(9, ids["BP__B"]);
            Assert.False(ids.ContainsKey("BP__C"));
            Assert.Equal(2, advertencias.Count);
        }

        [Fact(DisplayName = "Reglas: cuentas numéricas, grupos omitidos y meses de la ventana")]
        public void Reglas()
        {
            Assert.Equal("1101", ReglasCuadro.Cuenta("@1101"));
            Assert.Null(ReglasCuadro.Cuenta("@4101A"));
            Assert.Null(ReglasCuadro.Cuenta("SOLVENCIA"));
            Assert.Equal("110105", ReglasCuadro.Cuenta(new FilaDatos { CodigoBase = " 110105 ", Cuc = "x" }));
            var reglas = new ReglasCuadro(new SqlSettings());
            Assert.True(reglas.Omitida("6401"));
            Assert.False(reglas.Omitida("1601"));
            Assert.Equal(["2025-11", "2025-12", "2026-01"], Periodos.Meses("2025-11", "2026-01"));
        }

        // ---------------------------------------------------------------------------------
        // Dobles
        // ---------------------------------------------------------------------------------

        private static FuenteDatosHibrida Crear(FuenteFalsa json, RepositorioFalso repo, TimeProvider? reloj = null) => new(
            json,
            repo,
            new MapaEntidades([new EntradaMapaEntidad("BP__X", 260, "R260", "BP. X")]),
            new DatosSettings { Sql = new SqlSettings { Habilitado = true } },
            NullLogger<FuenteDatosHibrida>.Instance,
            reloj ?? new RelojFalso());

        private static FilaDatos Fila(string cuadro, double[] valores, string? cuc = null, string? codigoBase = null, string? id = null, string? filtro = null) => new()
        {
            Cuadro = cuadro,
            Cuc = cuc,
            CodigoBase = codigoBase,
            Id = id,
            Filtro = filtro,
            Periodos = EjeJson,
            Valores = valores
        };

        private static SeriesBd Series(params (string Codigo, string Periodo, double Valor)[] filas) => SeriesBd.Construir(filas);

        private sealed class RelojFalso : TimeProvider
        {
            private DateTimeOffset _ahora = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => _ahora;
            public void Avanzar(TimeSpan t) => _ahora += t;
        }

        private sealed class RepositorioFalso : IBcoCoopRepositorio
        {
            public bool Falla { get; set; }
            public VentanaBd Ventana { get; set; } = new("2024-01", "2024-03", 3, 3, 100, "f1");
            public SeriesBd SaldosEntidad { get; set; } = Series(
                ("1", "2024-01", 1e6), ("1", "2024-02", 2e6), ("1", "2024-03", 3e6),
                ("6", "2024-01", 8e6));
            public int LlamadasVentana { get; private set; }
            public int LlamadasSaldosEntidad { get; private set; }
            public int LlamadasSaldosEntidades { get; private set; }
            public string[]? UltimosTipos { get; private set; }
            public int[]? UltimosSegmentos { get; private set; }

            public Task<VentanaBd> ObtenerVentanaAsync(CancellationToken ct)
            {
                LlamadasVentana++;
                return Falla ? throw new InvalidOperationException("sin conexión (prueba)") : Task.FromResult(Ventana);
            }

            public Task<IReadOnlyList<IfiBd>> ListarIfiAsync(CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<IfiBd>>([new IfiBd(260, "R260", "BP. X", "BP", 1)]);

            public Task<SeriesBd> ObtenerSaldosEntidadAsync(int ifiId, IEnumerable<string>? cuentas, CancellationToken ct)
            {
                LlamadasSaldosEntidad++;
                return Task.FromResult(SaldosEntidad);
            }

            public Task<IReadOnlyDictionary<int, SeriesBd>> ObtenerSaldosEntidadesAsync(IEnumerable<int> ifiIds, IEnumerable<string>? cuentas, CancellationToken ct)
            {
                LlamadasSaldosEntidades++;
                return Task.FromResult<IReadOnlyDictionary<int, SeriesBd>>(ifiIds.ToDictionary(i => i, _ => SaldosEntidad));
            }

            public Task<SeriesBd> ObtenerSaldosAgregadoAsync(IEnumerable<string> tiposEntidad, IEnumerable<int>? segmentos, IEnumerable<string>? cuentas, CancellationToken ct)
            {
                UltimosTipos = tiposEntidad.ToArray();
                UltimosSegmentos = segmentos?.ToArray();
                return Task.FromResult(Series(("1", "2024-01", 10e6), ("1", "2024-02", 20e6), ("1", "2024-03", 30e6)));
            }

            public Task<SeriesBd> ObtenerIndicadoresEntidadAsync(int ifiId, IEnumerable<string>? codigos, CancellationToken ct) =>
                Task.FromResult(Series(
                    ("SOLVENCIA", "2024-01", 0.135), ("SOLVENCIA", "2024-02", 0.14),
                    ("PTC", "2024-01", 2529159469)));
        }

        private sealed class FuenteFalsa : IFuenteDatos
        {
            public List<FilaDatos> Filas { get; init; } = [];
            public Dictionary<string, ReporteEntidad> Reportes { get; init; } = new(StringComparer.Ordinal);

            public EstadoFuente Estado => EstadoFuente.Lista;
            public string? DetalleEstado => null;
            public Task<DateTime> ObtenerVersionDatosAsync(CancellationToken ct) => Task.FromResult(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            public Task<IReadOnlyList<EntidadCatalogo>> ObtenerEntidadesAsync(CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<EntidadCatalogo>>([new EntidadCatalogo { Id = "BP__X", Nombre = "BP. X" }]);
            public Task<IReadOnlyList<CuadroFuente>> ObtenerCuadrosAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CuadroFuente>>([]);
            public Task<IReadOnlyList<FilaDatos>> ObtenerFilasAsync(ConsultaFilas consulta, CancellationToken ct) => Task.FromResult<IReadOnlyList<FilaDatos>>(Filas);
            public Task<IReadOnlyList<FilaDatos>> ObtenerFilasSistemaAsync(string filtro, CancellationToken ct) => Task.FromResult<IReadOnlyList<FilaDatos>>(Filas);
            public Task<IReadOnlyList<string>> ObtenerNotasAsync(string cuadro, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
            public Task<ReporteEntidad?> ObtenerReporteAsync(string entidadId, CancellationToken ct) => Task.FromResult(Reportes.GetValueOrDefault(entidadId));
            public Task<IReadOnlyDictionary<string, ReporteEntidad>> ObtenerReportesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyDictionary<string, ReporteEntidad>>(Reportes);
            public Task<InfoFuentes> ObtenerInfoAsync(CancellationToken ct) => Task.FromResult(new InfoFuentes());
        }
    }
}
