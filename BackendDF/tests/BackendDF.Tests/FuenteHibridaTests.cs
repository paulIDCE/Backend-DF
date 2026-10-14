using BackendDF.Configuration;
using BackendDF.Data.FuenteHibrida;
using BackendDF.Data.FuenteSql;
using BackendDF.Data.Interfaces;
using BackendDF.Data.Repositorios;
using BackendDF.Logic.Dominio;
using BackendDF.Logic.Services;
using BackendDF.Models.DTOs;
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
        [InlineData(TipoCuadro.Sistema, "GEO01")]
        [InlineData(TipoCuadro.CarteraEntidad, "GEE01")]
        [InlineData(TipoCuadro.CarteraEntidad, "PDE02")]
        public async Task SinVentana(TipoCuadro tipo, string cuadro)
        {
            var json = new FuenteFalsa { Filas = [Fila(cuadro, cuc: "@1", valores: [9, 9, 9])] };
            var repo = new RepositorioFalso();
            var fuente = Crear(json, repo);

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(tipo, cuadro, null, "BP__X", null), default);

            Assert.Same(json.Filas, filas);
            Assert.Equal(0, repo.LlamadasSaldosEntidad);
        }

        [Fact(DisplayName = "Híbrida: en las series de sistema los cuadros geográficos conservan las fechas del JSON")]
        public async Task SistemaConGeograficos()
        {
            var json = new FuenteFalsa
            {
                Filas =
                [
                    Fila("SFN01", cuc: "@1", valores: [9, 9, 9], filtro: "Bancos Privados Grandes"),
                    Fila("GEO01", cuc: "01 - Azuay", id: "Total", valores: [4, 5, 6], filtro: "Bancos Privados Grandes")
                ]
            };
            var fuente = Crear(json, new RepositorioFalso());

            var filas = await fuente.ObtenerFilasSistemaAsync("Bancos Privados Grandes", default);

            Assert.Equal(["2024-01", "2024-02", "2024-03"], filas[0].Periodos);   // SFN01: ventana BD
            Assert.Equal([10.0, 20.0, 30.0], filas[0].Valores);
            Assert.Same(json.Filas[1], filas[1]);                                // GEO01: intacto
            Assert.Equal(EjeJson, filas[1].Periodos);
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
        // Fase 2: catálogo de cuadros en la BD
        // ---------------------------------------------------------------------------------

        private static FilaCatalogoBd FilaCat(int id, string? codigo, string variable, string calculo,
            string idSub = "", string? codigoBase = null, int nivel = 1, string? grupo = "G") =>
            new(id, "*", idSub, id * 10, codigo, codigoBase, grupo, variable, nivel, null, "Millones USD", calculo);

        private static CuadroBd CuadroCat(string id, string ambito = "ENTIDAD", string tipo = "Entidad", bool fechasJson = false) =>
            new(id, $"TÍTULO {id}", "Millones USD", ambito, tipo, "mensual", fechasJson, 10, ["*"]);

        [Fact(DisplayName = "Catálogo: orden, texto y cálculos de la BD; filas pendientes con el valor del JSON")]
        public async Task CatalogoEntidad()
        {
            var repo = new RepositorioFalso { Cuadros = [CuadroCat("EFI01")] };
            repo.Calculados["EFI01"] = new CuadroCalculadoBd(
                [
                    FilaCat(1, "pasipat", "TOTAL PASIVO Y PATRIMONIO", "SUMA"),
                    FilaCat(2, "@1", "ACTIVO (texto de dbo.Cuenta)", "SUMA"),
                    FilaCat(3, "IF_X", "Pendiente", "JSON"),
                    FilaCat(4, "SIN_JSON", "Sin fila en el JSON", "JSON")
                ],
                Series(("1", "2024-01", 5), ("1", "2024-02", 6), ("1", "2024-03", 7),
                       ("2", "2024-01", 10), ("2", "2024-03", 30)));
            var json = new FuenteFalsa
            {
                Filas =
                [
                    Fila("EFI01", cuc: "@1", valores: [9, 9, 9]),
                    Fila("EFI01", cuc: "IF_X", valores: [1, 2, 3]),
                    Fila("EFI01", cuc: "SOLO_EN_JSON", valores: [4, 4, 4])
                ]
            };
            var fuente = Crear(json, repo);

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI01", null, "BP__X", null), default);

            Assert.Equal(["pasipat", "@1", "IF_X", "SIN_JSON"], filas.Select(f => f.Cuc));       // orden del catálogo
            Assert.Equal("ACTIVO (texto de dbo.Cuenta)", filas[1].Variable);
            Assert.Equal("TÍTULO EFI01", filas[0].Titulo);
            Assert.All(filas, f => Assert.Equal(["2024-01", "2024-02", "2024-03"], f.Periodos));
            Assert.Equal([5.0, 6.0, 7.0], filas[0].Valores);                                      // calculado por el SP
            Assert.True(double.IsNaN(filas[1].Valores[1]));                                       // mes sin dato en la BD
            Assert.Equal(2.0, filas[2].Valores[0]);                                               // pendiente: JSON 2024-01
            Assert.True(double.IsNaN(filas[2].Valores[2]));
            Assert.All(filas[3].Valores, v => Assert.True(double.IsNaN(v)));                     // pendiente sin JSON
            Assert.Equal(1, filas[0].NivelJerarquia);
            Assert.Equal(0, repo.LlamadasSaldosEntidad);                                          // no usa la ruta de Fase 1
        }

        [Fact(DisplayName = "Catálogo: cuadro con FechasJson conserva el eje del JSON")]
        public async Task CatalogoFechasJson()
        {
            var repo = new RepositorioFalso { Cuadros = [CuadroCat("EFI09", tipo: "CarteraEntidad", fechasJson: true)] };
            repo.Calculados["EFI09"] = new CuadroCalculadoBd([FilaCat(1, "IF007_1R1", "Rango 1", "JSON", idSub: "Productivo")], SeriesBd.Vacia);
            var json = new FuenteFalsa { Filas = [Fila("EFI09", cuc: "IF007_1R1", id: "Productivo", valores: [1, 2, 3])] };
            var fuente = Crear(json, repo);

            var fila = (await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.CarteraEntidad, "EFI09", null, "BP__X", "Productivo"), default)).Single();

            Assert.Equal(EjeJson, fila.Periodos);
            Assert.Equal([1.0, 2.0, 3.0], fila.Valores);
            Assert.Equal("Productivo", repo.UltimoIdSub);
        }

        [Fact(DisplayName = "Catálogo: balance de sector con nivel de árbol y pendiente emparejada por código de cuenta")]
        public async Task CatalogoBalancesSector()
        {
            var repo = new RepositorioFalso { Cuadros = [CuadroCat("SFN06", "SECTOR", "Balances")] };
            repo.Calculados["SFN06"] = new CuadroCalculadoBd(
                [FilaCat(1, null, "1101.    Caja", "JSON", idSub: "Análisis Horizontal (%)", codigoBase: "1101", nivel: 3, grupo: null)],
                SeriesBd.Vacia);
            var json = new FuenteFalsa
            {
                Filas = [Fila("SFN06", codigoBase: "1101", id: "Análisis Horizontal (%)", valores: [7, 8, 9], filtro: "Bancos Privados Grandes")]
            };
            var fuente = Crear(json, repo);

            var fila = (await fuente.ObtenerFilasAsync(
                new ConsultaFilas(TipoCuadro.Balances, "SFN06", "Bancos Privados Grandes", null, "Análisis Horizontal (%)"), default)).Single();

            Assert.Equal("BP", string.Join(',', repo.UltimosTipos!));
            Assert.Equal("1", string.Join(',', repo.UltimosSegmentos!));
            Assert.Equal(3, fila.Nivel);
            Assert.Equal(0, fila.NivelJerarquia);
            Assert.Equal("1101", fila.CodigoBase);
            Assert.Equal(8.0, fila.Valores[0]);
        }

        [Fact(DisplayName = "Catálogo: REP01 de todas las entidades con una sola carga masiva")]
        public async Task CatalogoReportes()
        {
            var repo = new RepositorioFalso { Cuadros = [CuadroCat("REP01", tipo: "Reporte")] };
            repo.Calculados["REP01"] = new CuadroCalculadoBd(
                [FilaCat(1, "@1", "1.    ACTIVO", "SUMA"), FilaCat(2, "SOLVENCIA", "Solvencia", "JSON")],
                Series(("1", "2024-01", 100), ("1", "2024-02", 200)));
            var reporte = new ReporteEntidad("BP__X", EjeJson,
                [new CuentaReporte("SOLVENCIA", null, [11, 12, 13]), new CuentaReporte("@1", "ACTIVO", [9, 9, 9])]);
            var json = new FuenteFalsa { Reportes = new Dictionary<string, ReporteEntidad> { ["BP__X"] = reporte } };
            var fuente = Crear(json, repo);

            var todos = await fuente.ObtenerReportesAsync(default);
            var uno = await fuente.ObtenerReporteAsync("BP__X", default);

            Assert.Equal(1, repo.LlamadasValoresMasivos);
            Assert.Same(todos["BP__X"], uno);
            Assert.Equal(["@1", "SOLVENCIA"], uno!.Cuentas.Select(c => c.Cuc));                  // orden del catálogo
            Assert.Equal(200.0, uno.Valor(uno.BuscarCuenta("@1")!, "2024-02"));
            Assert.Equal(12.0, uno.Valor(uno.BuscarCuenta("SOLVENCIA")!, "2024-01"));
        }

        // Fase 3 · paso 2: la ventana de la BD empieza en 2024-01 (eje del repositorio falso).
        private static FilaCatalogoBd[] FilasConOperaciones() =>
        [
            FilaCat(1, "IF002", "ROA", "RATIO") with { OperacionN = "ANUALIZADO", OperacionD = "PROMEDIO_DIC_ROA" },
            FilaCat(2, "@5A", "Ingresos anualizados", "SUMA") with { OperacionN = "ANUALIZADO" },
            FilaCat(3, "tasa_14", "Crecimiento anual", "RATIO") with { OperacionN = "VARIACION_12", OperacionD = "RETARDO_12" },
            FilaCat(4, "VAR_1", "Variación mensual", "SUMA") with { OperacionN = "VARIACION_1" },
            FilaCat(5, "IF_X", "Pendiente", "JSON")
        ];

        [Fact(DisplayName = "Catálogo: operaciones en el tiempo marcan hasta dónde falta la historia previa")]
        public async Task CatalogoSinHistoria()
        {
            var repo = new RepositorioFalso { Cuadros = [CuadroCat("EFI11")] };
            repo.Calculados["EFI11"] = new CuadroCalculadoBd(FilasConOperaciones(), Series(("2", "2024-01", 12), ("4", "2024-02", 1)));
            var json = new FuenteFalsa
            {
                Cuadros = [new CuadroFuente { Id = "EFI11", Titulo = "T", Origen = "entidad", Tipo = TipoCuadro.Entidad }]
            };
            var fuente = Crear(json, repo);

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI11", null, "BP__X", null), default);
            Assert.Equal(["2024-12", null, "2024-12", "2024-01", null], filas.Select(f => f.SinHistoriaHasta));

            var servicio = new CuadrosService(fuente);
            var dto = await servicio.ObtenerAsync("EFI11", new ParametrosCuadro(null, "BP__X", null, null, null, null), default);
            Assert.Equal(["2024-12", null, "2024-12", "2024-01", null], dto.Filas.Select(f => f.SinHistoriaHasta));
            Assert.Contains(dto.Notas, n => n.Contains("hasta 2024-12", StringComparison.Ordinal));

            // Con el rango desde 2024-02, la variación mensual ya no tiene vacíos por historia a la vista.
            dto = await servicio.ObtenerAsync("EFI11", new ParametrosCuadro(null, "BP__X", null, null, "2024-02", null), default);
            Assert.Null(dto.Filas[3].SinHistoriaHasta);
            Assert.Equal("2024-12", dto.Filas[2].SinHistoriaHasta);

            // Sin notas pedidas no se agrega la nota.
            dto = await servicio.ObtenerAsync("EFI11", new ParametrosCuadro(null, "BP__X", null, null, null, null, Notas: false), default);
            Assert.Empty(dto.Notas);
        }

        [Fact(DisplayName = "Catálogo: REP01 también marca las cuentas sin historia previa")]
        public async Task CatalogoReportesSinHistoria()
        {
            var repo = new RepositorioFalso { Cuadros = [CuadroCat("REP01", tipo: "Reporte")] };
            repo.Calculados["REP01"] = new CuadroCalculadoBd(FilasConOperaciones(), SeriesBd.Vacia);
            var json = new FuenteFalsa
            {
                Reportes = new Dictionary<string, ReporteEntidad>
                {
                    ["BP__X"] = new("BP__X", EjeJson, [new CuentaReporte("IF_X", null, [1, 2, 3])])
                }
            };
            var fuente = Crear(json, repo);

            var reporte = (await fuente.ObtenerReporteAsync("BP__X", default))!;
            Assert.Equal(["2024-12", null, "2024-12", "2024-01", null], reporte.Cuentas.Select(c => c.SinHistoriaHasta));

            var dto = (await new ReportesService(fuente).ObtenerReporteAsync("BP__X", "tasa_14,VAR_1", "2024-02", null, default)).Resultado;
            Assert.Equal(["2024-12", null], dto.Cuentas.Select(c => c.SinHistoriaHasta));
        }

        // ---- Fuente de los datos (bd / json / mixto) ----

        [Fact(DisplayName = "Fuente de datos: catálogo → fila calculada bd, pendiente con JSON json, sin JSON null; cuadro mixto")]
        public async Task FuenteDatosCatalogo()
        {
            var repo = new RepositorioFalso { Cuadros = [CuadroCat("EFI01")] };
            repo.Calculados["EFI01"] = new CuadroCalculadoBd(
                [FilaCat(1, "@1", "ACTIVO", "SUMA"), FilaCat(2, "IF_X", "Pendiente", "JSON"), FilaCat(3, "SIN_JSON", "Sin JSON", "JSON")],
                Series(("1", "2024-01", 5)));
            var json = new FuenteFalsa
            {
                Filas = [Fila("EFI01", cuc: "IF_X", valores: [1, 2, 3])],
                Cuadros = [new CuadroFuente { Id = "EFI01", Titulo = "T", Origen = "entidad", Tipo = TipoCuadro.Entidad }]
            };
            var fuente = Crear(json, repo);

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI01", null, "BP__X", null), default);
            Assert.Equal([OrigenDatos.Bd, OrigenDatos.Json, null], filas.Select(f => f.Origen));

            var dto = await new CuadrosService(fuente).ObtenerAsync("EFI01", new ParametrosCuadro(null, "BP__X", null, null, null, null), default);
            Assert.Equal([OrigenDatos.Bd, OrigenDatos.Json, null], dto.Filas.Select(f => f.FuenteDatos));
            Assert.Equal(OrigenDatos.Mixto, dto.FuenteDatos);
            Assert.Equal(new FilasPorFuenteDTO(1, 1, 1), dto.FilasPorFuente);
        }

        [Fact(DisplayName = "Fuente de datos: ruta de Fase 1 → indicador de IndicadorData bd, fila sin dato en la BD json")]
        public async Task FuenteDatosFase1()
        {
            var json = new FuenteFalsa { Filas = [Fila("EFI05", cuc: "SOLVENCIA", valores: [9, 9, 9]), Fila("EFI05", cuc: "PTR_9", valores: [7, 7, 7])] };
            var fuente = Crear(json, new RepositorioFalso());

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI05", null, "BP__X", null), default);

            Assert.Equal([OrigenDatos.Bd, OrigenDatos.Json], filas.Select(f => f.Origen));
        }

        [Fact(DisplayName = "Fuente de datos: con SQL caído todo es JSON (filas y resumen de /meta)")]
        public async Task FuenteDatosSqlCaido()
        {
            var json = new FuenteFalsa
            {
                Filas = [Fila("EFI01", cuc: "@1", valores: [9, 9, 9])],
                Cuadros = [new CuadroFuente { Id = "EFI01" }, new CuadroFuente { Id = "IEM01" }]
            };
            var fuente = Crear(json, new RepositorioFalso { Falla = true });

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI01", null, "BP__X", null), default);
            var info = await fuente.ObtenerInfoAsync(default);

            Assert.All(filas, f => Assert.Equal(OrigenDatos.Json, f.Origen));
            Assert.Equal(new CuadrosPorOrigen(0, 2, 0), info.CuadrosPorOrigen);
        }

        [Fact(DisplayName = "Fuente de datos: REP01 del catálogo marca cada cuenta y el reporte queda mixto")]
        public async Task FuenteDatosReporte()
        {
            var repo = new RepositorioFalso { Cuadros = [CuadroCat("REP01", tipo: "Reporte")] };
            repo.Calculados["REP01"] = new CuadroCalculadoBd(
                [FilaCat(1, "@1", "1.    ACTIVO", "SUMA"), FilaCat(2, "SOLVENCIA", "Solvencia", "JSON")],
                Series(("1", "2024-01", 100)));
            var reporte = new ReporteEntidad("BP__X", EjeJson, [new CuentaReporte("SOLVENCIA", null, [11, 12, 13])]);
            var fuente = Crear(new FuenteFalsa { Reportes = new Dictionary<string, ReporteEntidad> { ["BP__X"] = reporte } }, repo);

            var dto = (await new ReportesService(fuente).ObtenerReporteAsync("BP__X", null, null, null, default)).Resultado;

            Assert.Equal([OrigenDatos.Bd, OrigenDatos.Json], dto.Cuentas.Select(c => c.FuenteDatos));
            Assert.Equal(OrigenDatos.Mixto, dto.FuenteDatos);
            Assert.Equal(new FilasPorFuenteDTO(1, 1, 0), dto.FilasPorFuente);
        }

        [Fact(DisplayName = "Fuente de datos: /meta cuenta los cuadros bd, json (macro) y mixtos según el catálogo")]
        public async Task FuenteDatosMeta()
        {
            var json = new FuenteFalsa
            {
                Cuadros = [new CuadroFuente { Id = "EFI01" }, new CuadroFuente { Id = "EFI11" }, new CuadroFuente { Id = "EFI09" },
                           new CuadroFuente { Id = "IEM01" }]
            };
            var repo = new RepositorioFalso
            {
                Cuadros =
                [
                    CuadroCat("EFI01") with { FilasBd = 202, FilasJson = 0 },
                    CuadroCat("EFI11") with { FilasBd = 2, FilasJson = 39 },
                    CuadroCat("EFI09", fechasJson: true) with { FilasBd = 0, FilasJson = 252 }
                ]
            };

            var info = await Crear(json, repo).ObtenerInfoAsync(default);
            var meta = await new MetaService(Crear(json, repo)).ObtenerAsync(default);

            Assert.Equal(new CuadrosPorOrigen(1, 2, 1), info.CuadrosPorOrigen);   // EFI01 bd; EFI09 e IEM01 json; EFI11 mixto
            Assert.Equal(new CuadrosPorFuenteDTO(1, 2, 1), meta.CuadrosPorFuente);
        }

        [Fact(DisplayName = "Fuente de datos: sin los conteos del catálogo (falta 08) el resumen es null y se advierte")]
        public async Task FuenteDatosMetaSin08()
        {
            var json = new FuenteFalsa { Cuadros = [new CuadroFuente { Id = "EFI01" }] };
            var info = await Crear(json, new RepositorioFalso { Cuadros = [CuadroCat("EFI01")] }).ObtenerInfoAsync(default);

            Assert.Null(info.CuadrosPorOrigen);
            Assert.Contains(info.Advertencias, a => a.Contains("08_origen_cuadros", StringComparison.Ordinal));
        }

        [Fact(DisplayName = "Catálogo: si el SP del cuadro falla se sirve la ruta de Fase 1 (plantilla JSON)")]
        public async Task CatalogoFallaSql()
        {
            var repo = new RepositorioFalso { Cuadros = [CuadroCat("EFI01")], FallaCuadros = true };
            var json = new FuenteFalsa { Filas = [Fila("EFI01", cuc: "@1", valores: [9, 9, 9]), Fila("EFI01", cuc: "@6", valores: [5, 5, 5])] };
            var fuente = Crear(json, repo);

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI01", null, "BP__X", null), default);

            Assert.True(fuente.SqlNoDisponible);
            Assert.Equal(["@1"], filas.Select(f => f.Cuc));                                      // plantilla JSON sin 6/7
            Assert.Equal(9.0, filas[0].Valores[0]);                                               // valor JSON
        }

        [Fact(DisplayName = "Catálogo no instalado: se usa la plantilla JSON y /meta lo advierte (no es caída de SQL)")]
        public async Task CatalogoNoInstalado()
        {
            var repo = new RepositorioFalso { CatalogoNoInstalado = true };
            var fuente = Crear(new FuenteFalsa { Filas = [Fila("EFI01", cuc: "@1", valores: [9, 9, 9])] }, repo);

            var filas = await fuente.ObtenerFilasAsync(new ConsultaFilas(TipoCuadro.Entidad, "EFI01", null, "BP__X", null), default);
            var info = await fuente.ObtenerInfoAsync(default);

            Assert.False(fuente.SqlNoDisponible);
            Assert.Equal([1.0, 2.0, 3.0], filas[0].Valores);                                      // Fase 1: cuenta desde SQL
            Assert.Contains(info.Advertencias, a => a.Contains("Catálogo de cuadros no disponible", StringComparison.Ordinal));
        }

        [Fact(DisplayName = "Variantes: exacta, luego solo tipos, luego '*'")]
        public void ResolverVariante()
        {
            var cuadro = new CuadroBd("CAR02", "T", null, "SECTOR", "Cartera", "mensual", false, 1, ["BP", "BP:2", "COOP", "*"]);
            Assert.Equal("BP:2", cuadro.ResolverVariante("BP:2"));
            Assert.Equal("BP", cuadro.ResolverVariante("BP:1"));
            Assert.Equal("*", cuadro.ResolverVariante("MU"));
            Assert.Equal("*", cuadro.ResolverVariante(null));
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

            // ---- Catálogo (Fase 2). Vacío por defecto: las pruebas de Fase 1 no lo usan. ----

            public List<CuadroBd> Cuadros { get; set; } = [];
            public Dictionary<string, CuadroCalculadoBd> Calculados { get; } = new(StringComparer.OrdinalIgnoreCase);
            public bool CatalogoNoInstalado { get; set; }
            public bool FallaCuadros { get; set; }
            public int LlamadasCuadro { get; private set; }
            public int LlamadasValoresMasivos { get; private set; }
            public string? UltimoIdSub { get; private set; }

            public Task<IReadOnlyList<CuadroBd>> ListarCuadrosAsync(CancellationToken ct) =>
                CatalogoNoInstalado
                    ? throw new DbFalsa("No se encontró el procedimiento almacenado 'api.ListarCuadros'.")
                    : Task.FromResult<IReadOnlyList<CuadroBd>>(Cuadros);

            public Task<PlantillaBd> ObtenerPlantillaAsync(string cuadro, string? variante, string? idSub, CancellationToken ct) =>
                Task.FromResult(new PlantillaBd(Calculados[cuadro].Filas));

            public Task<CuadroCalculadoBd> ObtenerCuadroEntidadAsync(string cuadro, int ifiId, string? idSub, CancellationToken ct) =>
                Cuadro(cuadro, idSub);

            public Task<CuadroCalculadoBd> ObtenerCuadroSectorAsync(string cuadro, IEnumerable<string> tiposEntidad, IEnumerable<int>? segmentos, string? idSub, CancellationToken ct)
            {
                UltimosTipos = tiposEntidad.ToArray();
                UltimosSegmentos = segmentos?.ToArray();
                return Cuadro(cuadro, idSub);
            }

            public Task<IReadOnlyDictionary<int, SeriesBd>> ObtenerValoresCuadroEntidadesAsync(string cuadro, IEnumerable<int> ifiIds, string? idSub, CancellationToken ct)
            {
                LlamadasValoresMasivos++;
                return Task.FromResult<IReadOnlyDictionary<int, SeriesBd>>(ifiIds.ToDictionary(i => i, _ => Calculados[cuadro].Valores));
            }

            private Task<CuadroCalculadoBd> Cuadro(string cuadro, string? idSub)
            {
                LlamadasCuadro++;
                UltimoIdSub = idSub;
                return FallaCuadros ? throw new InvalidOperationException("sin conexión (prueba)") : Task.FromResult(Calculados[cuadro]);
            }
        }

        private sealed class DbFalsa : System.Data.Common.DbException
        {
            public DbFalsa(string mensaje) : base(mensaje) { }
        }

        private sealed class FuenteFalsa : IFuenteDatos
        {
            public List<FilaDatos> Filas { get; init; } = [];
            public List<CuadroFuente> Cuadros { get; init; } = [];
            public Dictionary<string, ReporteEntidad> Reportes { get; init; } = new(StringComparer.Ordinal);

            public EstadoFuente Estado => EstadoFuente.Lista;
            public string? DetalleEstado => null;
            public Task<DateTime> ObtenerVersionDatosAsync(CancellationToken ct) => Task.FromResult(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            public Task<IReadOnlyList<EntidadCatalogo>> ObtenerEntidadesAsync(CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<EntidadCatalogo>>([new EntidadCatalogo { Id = "BP__X", Nombre = "BP. X" }]);
            public Task<IReadOnlyList<CuadroFuente>> ObtenerCuadrosAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CuadroFuente>>(Cuadros);
            public Task<IReadOnlyList<FilaDatos>> ObtenerFilasAsync(ConsultaFilas consulta, CancellationToken ct) => Task.FromResult<IReadOnlyList<FilaDatos>>(Filas);
            public Task<IReadOnlyList<FilaDatos>> ObtenerFilasSistemaAsync(string filtro, CancellationToken ct) => Task.FromResult<IReadOnlyList<FilaDatos>>(Filas);
            public Task<IReadOnlyList<string>> ObtenerNotasAsync(string cuadro, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
            public Task<ReporteEntidad?> ObtenerReporteAsync(string entidadId, CancellationToken ct) => Task.FromResult(Reportes.GetValueOrDefault(entidadId));
            public Task<IReadOnlyDictionary<string, ReporteEntidad>> ObtenerReportesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyDictionary<string, ReporteEntidad>>(Reportes);
            public Task<InfoFuentes> ObtenerInfoAsync(CancellationToken ct) => Task.FromResult(new InfoFuentes());
        }
    }
}
