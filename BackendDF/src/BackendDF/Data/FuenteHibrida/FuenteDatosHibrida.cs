using System.ComponentModel;
using System.Data.Common;
using System.Net.Sockets;
using BackendDF.Common.Utils;
using BackendDF.Configuration;
using BackendDF.Data.FuenteSql;
using BackendDF.Data.Interfaces;
using BackendDF.Data.Repositorios;
using BackendDF.Logic.Dominio;
using BackendDF.Models.Entities;

namespace BackendDF.Data.FuenteHibrida
{
    /// <summary>
    /// <see cref="IFuenteDatos"/> híbrida (informe §8) sobre <c>idce_bco_coop</c> vía
    /// <see cref="IBcoCoopRepositorio"/>:
    /// <list type="bullet">
    /// <item><b>Fase 2</b> (cuadros del catálogo <c>dbo.Cuadro</c>): orden, texto, jerarquía y
    /// cálculos salen de la BD (<see cref="ArmadorCatalogo"/>); el JSON solo aporta el valor de las
    /// filas aún pendientes (Calculo = JSON).</item>
    /// <item><b>Fase 1</b> (sin catálogo: macro, catálogo no instalado o SQL caído al armarlo): la
    /// plantilla sale del JSON y las filas de cuenta e indicadores toman su valor de SQL.</item>
    /// </list>
    ///
    /// Convenciones: C1 recorta lo que deriva del .sav a la ventana de la BD (los macro y lo
    /// externo conservan sus fechas); C2 oculta las cuentas de los grupos 6/7; C3 agrega los
    /// sectores con <c>Ifi.Segmento</c>.
    ///
    /// Si SQL falla se abre un "circuito" y durante <see cref="SqlSettings.SegundosCircuitoAbierto"/>
    /// se sirve solo JSON (con la última ventana conocida); nunca se responde 500 por SQL caído.
    /// Cada vez que cambia la firma de la carga (<c>api.ObtenerVentana</c>) se vacían las cachés
    /// y cambia la versión de datos (ETag).
    /// </summary>
    public sealed class FuenteDatosHibrida : IFuenteDatos
    {
        private const string NombreFuenteSql = "sql-bco_coop";
        private const string NombreFuenteCatalogo = "catalogo-cuadros";

        private static readonly string[] AdvertenciasConvenciones =
        [
            "C1: los datos del sistema financiero se muestran solo en la ventana de la BD; los cuadros macro y externos conservan las fechas de sus archivos.",
            "C2: las cuentas contingentes (6) y de orden (7) no están en la BD y se omiten.",
            "C3: los segmentos de cooperativas son los de la BD (Tamaño); difieren de la publicación anterior (ver docs/anexo-segmentos-coop.csv)."
        ];

        private readonly IFuenteDatos _json;
        private readonly IBcoCoopRepositorio _repo;
        private readonly MapaEntidades _mapa;
        private readonly ReglasCuadro _reglas;
        private readonly SqlSettings _settings;
        private readonly int _capacidadCache;
        private readonly TimeProvider _reloj;
        private readonly ILogger<FuenteDatosHibrida> _logger;
        private readonly SemaphoreSlim _refresco = new(1, 1);

        private volatile Contexto? _contexto;
        private DateTime _revisadoEn = DateTime.MinValue;
        private long _circuitoHastaTicks;
        private volatile string? _ultimoErrorSql;

        public FuenteDatosHibrida(
            IFuenteDatos json,
            IBcoCoopRepositorio repo,
            MapaEntidades mapa,
            DatosSettings settings,
            ILogger<FuenteDatosHibrida> logger,
            TimeProvider? reloj = null)
        {
            _json = json;
            _repo = repo;
            _mapa = mapa;
            _settings = settings.Sql;
            _reglas = new ReglasCuadro(settings.Sql);
            _capacidadCache = Math.Max(1, settings.MaxEntidadesEnCache);
            _logger = logger;
            _reloj = reloj ?? TimeProvider.System;
        }

        /// <summary>true mientras el circuito está abierto (SQL no disponible: se sirve solo JSON).</summary>
        public bool SqlNoDisponible => Ahora.Ticks < Interlocked.Read(ref _circuitoHastaTicks);

        public string? UltimoErrorSql => _ultimoErrorSql;

        private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

        // =====================================================================================
        // IFuenteDatos
        // =====================================================================================

        public EstadoFuente Estado => _json.Estado;

        public string? DetalleEstado => _json.DetalleEstado;

        public async Task<DateTime> ObtenerVersionDatosAsync(CancellationToken ct)
        {
            var version = await _json.ObtenerVersionDatosAsync(ct);
            var ctx = await ObtenerContextoAsync(ct);
            return ctx is not null && ctx.Version > version ? ctx.Version : version;
        }

        public async Task<IReadOnlyList<EntidadCatalogo>> ObtenerEntidadesAsync(CancellationToken ct)
        {
            var entidades = await _json.ObtenerEntidadesAsync(ct);
            var ctx = await ObtenerContextoAsync(ct);
            if (ctx is null || !_reglas.UsaVentana(TipoCuadro.Entidad, "REP01"))
                return entidades;

            // Carrera inofensiva: dos requests pueden armar la misma lista.
            return ctx.Entidades ??= entidades.Select(e => new EntidadCatalogo
            {
                Id = e.Id,
                Nombre = e.Nombre,
                Tipo = e.Tipo,
                Tamano = e.Tamano,
                Rango = e.Rango,
                Provincia = e.Provincia,
                TieneBalance = e.TieneBalance,
                PeriodoDesde = ctx.Ventana.Desde,
                PeriodoHasta = ctx.Ventana.Hasta
            }).ToList();
        }

        public async Task<IReadOnlyList<CuadroFuente>> ObtenerCuadrosAsync(CancellationToken ct)
        {
            var cuadros = await _json.ObtenerCuadrosAsync(ct);
            var ctx = await ObtenerContextoAsync(ct);
            if (ctx is null)
                return cuadros;

            // Título, unidad y fechas de los cuadros del catálogo salen de la BD; el resto (macro),
            // del JSON con la regla de ventana de Fase 1. Tipo, origen y frecuencia no cambian.
            return ctx.Cuadros ??= cuadros.Select(c =>
            {
                var enCatalogo = ctx.Catalogo.TryGetValue(c.Id, out var def);
                var conVentana = enCatalogo ? !def!.FechasJson : _reglas.UsaVentana(c.Tipo, c.Id);
                if (!enCatalogo && !conVentana)
                    return c;
                return new CuadroFuente
                {
                    Id = c.Id,
                    Titulo = def?.Titulo ?? c.Titulo,
                    Unidad = enCatalogo ? def!.Unidad : c.Unidad,
                    Origen = c.Origen,
                    Tipo = c.Tipo,
                    Frecuencia = c.Frecuencia,
                    PeriodoDesde = conVentana ? ctx.Ventana.Desde : c.PeriodoDesde,
                    PeriodoHasta = conVentana ? ctx.Ventana.Hasta : c.PeriodoHasta
                };
            }).ToList();
        }

        public async Task<IReadOnlyList<FilaDatos>> ObtenerFilasAsync(ConsultaFilas consulta, CancellationToken ct)
        {
            var filas = await _json.ObtenerFilasAsync(consulta, ct);
            var ctx = await ObtenerContextoAsync(ct);

            // Fase 2: el cuadro está en el catálogo de la BD → plantilla y cálculos desde SQL.
            if (ctx is not null && ctx.Catalogo.TryGetValue(consulta.Cuadro, out var cuadro)
                && await CuadroCalculadoAsync(ctx, cuadro, consulta.EntidadId, consulta.Filtro, consulta.Id) is { } calculado)
                return ArmadorCatalogo.Filas(cuadro, calculado, filas, cuadro.FechasJson ? null : ctx.Eje, consulta.Filtro);

            // Fase 1: plantilla JSON con valores de cuenta desde SQL.
            if (!_reglas.UsaVentana(consulta.Tipo, consulta.Cuadro) || ctx is null)
                return filas;

            SeriesBd? saldos = null, indicadores = null;
            if (_reglas.UsaSql(consulta.Cuadro))
            {
                switch (consulta.Tipo)
                {
                    case TipoCuadro.Entidad or TipoCuadro.CarteraEntidad or TipoCuadro.BalancesEntidad:
                        if (consulta.EntidadId is not null && ctx.Ids.TryGetValue(consulta.EntidadId, out var ifiId))
                        {
                            saldos = await SaldosEntidadAsync(ctx, ifiId);
                            if (filas.Any(f => ReglasCuadro.EsIndicador(f.Cuc)))
                                indicadores = await IndicadoresEntidadAsync(ctx, ifiId);
                        }
                        break;
                    case TipoCuadro.Sistema or TipoCuadro.Balances:
                        saldos = await SaldosSectorAsync(ctx, consulta.Filtro);
                        break;
                }
            }
            return Combinar(filas, ctx.Eje, saldos, indicadores);
        }

        public async Task<IReadOnlyList<FilaDatos>> ObtenerFilasSistemaAsync(string filtro, CancellationToken ct)
        {
            var filas = await _json.ObtenerFilasSistemaAsync(filtro, ct);
            var ctx = await ObtenerContextoAsync(ct);
            if (ctx is null)
                return filas;

            // Cuadro por cuadro, en el orden del archivo: los del catálogo se arman desde la BD y el
            // resto sigue la ruta de Fase 1 (los saldos del sector se piden una sola vez).
            var salida = new List<FilaDatos>(filas.Count);
            SeriesBd? saldos = null;
            var saldosPedidos = false;
            foreach (var grupo in filas.GroupBy(f => f.Cuadro))
            {
                var delCuadro = grupo.ToList();
                if (ctx.Catalogo.TryGetValue(grupo.Key, out var cuadro)
                    && await CuadroCalculadoAsync(ctx, cuadro, null, filtro, null) is { } calculado)
                {
                    salida.AddRange(ArmadorCatalogo.Filas(cuadro, calculado, delCuadro, cuadro.FechasJson ? null : ctx.Eje, filtro));
                    continue;
                }
                if (!_reglas.UsaVentana(TipoCuadro.Sistema, grupo.Key))
                {
                    salida.AddRange(delCuadro);   // no sale del .sav: fechas del JSON (C1)
                    continue;
                }
                if (!saldosPedidos && delCuadro.Any(f => _reglas.UsaSql(f.Cuadro)))
                {
                    saldos = await SaldosSectorAsync(ctx, filtro);
                    saldosPedidos = true;
                }
                salida.AddRange(Combinar(delCuadro, ctx.Eje, saldos, null));
            }
            return salida;
        }

        public Task<IReadOnlyList<string>> ObtenerNotasAsync(string cuadro, CancellationToken ct) =>
            _json.ObtenerNotasAsync(cuadro, ct);

        public async Task<ReporteEntidad?> ObtenerReporteAsync(string entidadId, CancellationToken ct)
        {
            var reporte = await _json.ObtenerReporteAsync(entidadId, ct);
            if (reporte is null || !_reglas.UsaVentana(TipoCuadro.Entidad, "REP01"))
                return reporte;

            var ctx = await ObtenerContextoAsync(ct);
            if (ctx is null)
                return reporte;

            // Si el índice de todas las entidades ya está armado, se reutiliza.
            if (ctx.Reportes is { IsCompletedSuccessfully: true } listos && listos.Result.TryGetValue(reporte.EntidadId, out var armado))
                return armado;

            // Fase 2: REP01 desde el catálogo (orden, texto y cálculos de la BD).
            if (ctx.Catalogo.TryGetValue("REP01", out var rep01)
                && await CuadroCalculadoAsync(ctx, rep01, reporte.EntidadId, null, null) is { } calculado)
                return ArmadorCatalogo.Reporte(reporte.EntidadId, calculado.Filas, calculado.Valores, reporte, ctx.Eje);

            SeriesBd? saldos = null;
            if (_reglas.UsaSql("REP01") && ctx.Ids.TryGetValue(reporte.EntidadId, out var ifiId))
                saldos = await SaldosEntidadAsync(ctx, ifiId);
            return CombinarReporte(reporte, ctx.Eje, saldos);
        }

        public async Task<IReadOnlyDictionary<string, ReporteEntidad>> ObtenerReportesAsync(CancellationToken ct)
        {
            var reportes = await _json.ObtenerReportesAsync(ct);
            if (!_reglas.UsaVentana(TipoCuadro.Entidad, "REP01"))
                return reportes;

            var ctx = await ObtenerContextoAsync(ct);
            if (ctx is null)
                return reportes;

            Task<IReadOnlyDictionary<string, ReporteEntidad>> tarea;
            lock (ctx)
                tarea = ctx.Reportes ??= ConstruirReportesAsync(ctx, reportes);
            return await tarea.WaitAsync(ct);
        }

        public async Task<InfoFuentes> ObtenerInfoAsync(CancellationToken ct)
        {
            var info = await _json.ObtenerInfoAsync(ct);
            var ctx = await ObtenerContextoAsync(ct);

            var advertencias = new List<string>(info.Advertencias);
            var fuentes = new List<InfoFuente>(info.Fuentes);
            if (ctx is not null)
            {
                advertencias.AddRange(AdvertenciasConvenciones);
                advertencias.AddRange(ctx.AdvertenciasMapa);
                if (ctx.AvisoCatalogo is not null)
                    advertencias.Add(ctx.AvisoCatalogo);
                fuentes.Add(new InfoFuente
                {
                    Fuente = NombreFuenteSql,
                    Archivo = "idce_bco_coop",
                    Archivos = ctx.Ids.Count,
                    Desde = ctx.Ventana.Desde,
                    Hasta = ctx.Ventana.Hasta,
                    Modificado = ctx.Version
                });
                if (ctx.Catalogo.Count > 0)
                    fuentes.Add(new InfoFuente
                    {
                        Fuente = NombreFuenteCatalogo,
                        Archivo = "idce_bco_coop.dbo.Cuadro",
                        Archivos = ctx.Catalogo.Count,
                        Modificado = ctx.Version
                    });
            }
            if (SqlNoDisponible || ctx is null)
                advertencias.Add($"La base SQL no está disponible ({_ultimoErrorSql ?? "sin conexión"}); se sirven solo los datos JSON.");

            return new InfoFuentes
            {
                VersionDatos = ctx is not null && ctx.Version > info.VersionDatos ? ctx.Version : info.VersionDatos,
                Fuentes = fuentes,
                UltimoCorteComun = ctx?.Ventana.Hasta ?? info.UltimoCorteComun,
                Advertencias = advertencias,
                CuadrosPorOrigen = await CuadrosPorOrigenAsync(ctx, advertencias, ct)
            };
        }

        /// <summary>
        /// Cuadros por fuente de sus datos: los del catálogo según sus filas (con fórmula / pendientes),
        /// el resto (macro) del JSON. Sin SQL, todo JSON. null si el catálogo no informa los conteos.
        /// </summary>
        private async Task<CuadrosPorOrigen?> CuadrosPorOrigenAsync(Contexto? ctx, List<string> advertencias, CancellationToken ct)
        {
            var cuadros = await _json.ObtenerCuadrosAsync(ct);
            if (ctx is null || SqlNoDisponible)
                return new CuadrosPorOrigen(0, cuadros.Count, 0);
            if (ctx.Catalogo.Count == 0)
                return null;   // catálogo no instalado: ya lo advierte AvisoCatalogo
            if (ctx.Catalogo.Values.Any(c => c.Origen is null))
            {
                advertencias.Add("La BD no informa las filas con fórmula de cada cuadro (falta db/fase3/08_origen_cuadros.sql): " +
                                 "no se puede resumir la fuente de los datos por cuadro.");
                return null;
            }
            var origenes = cuadros.Select(c => ctx.Catalogo.TryGetValue(c.Id, out var def) ? def.Origen! : OrigenDatos.Json)
                .Concat(ctx.Catalogo.Keys.Except(cuadros.Select(c => c.Id), StringComparer.OrdinalIgnoreCase).Select(k => ctx.Catalogo[k].Origen!))
                .ToList();
            return new CuadrosPorOrigen(origenes.Count(o => o == OrigenDatos.Bd), origenes.Count(o => o == OrigenDatos.Json),
                origenes.Count(o => o == OrigenDatos.Mixto));
        }

        // =====================================================================================
        // Ventana, cachés y circuito
        // =====================================================================================

        /// <summary>Estado de la BD para la carga vigente: ventana, eje de meses, mapa de ids, catálogo y cachés.</summary>
        private sealed class Contexto
        {
            public Contexto(VentanaBd ventana, IReadOnlyDictionary<string, int> ids, IReadOnlyDictionary<int, string?> tipoPorIfi,
                IReadOnlyList<string> advertenciasMapa, IReadOnlyDictionary<string, CuadroBd> catalogo, string? avisoCatalogo,
                DateTime version, int capacidad)
            {
                Ventana = ventana;
                Eje = Periodos.Meses(ventana.Desde, ventana.Hasta);
                Ids = ids;
                TipoPorIfi = tipoPorIfi;
                AdvertenciasMapa = advertenciasMapa;
                Catalogo = catalogo;
                AvisoCatalogo = avisoCatalogo;
                Version = version;
                Saldos = new CacheLru<SeriesBd>(capacidad);
                Indicadores = new CacheLru<SeriesBd>(capacidad);
                Sectores = new CacheLru<SeriesBd>(16);
                Calculados = new CacheLru<CuadroCalculadoBd>(capacidad * 5);
                Plantillas = new CacheLru<PlantillaBd>(32);
            }

            public VentanaBd Ventana { get; }

            /// <summary>Eje único de la ventana: todas las filas recortadas comparten esta instancia.</summary>
            public string[] Eje { get; }

            public IReadOnlyDictionary<string, int> Ids { get; }
            public IReadOnlyDictionary<int, string?> TipoPorIfi { get; }

            /// <summary>Catálogo de cuadros de la BD (Fase 2); vacío si no está instalado: se usa la plantilla JSON.</summary>
            public IReadOnlyDictionary<string, CuadroBd> Catalogo { get; }
            public string? AvisoCatalogo { get; }

            /// <summary>Cuadros armados por (cuadro, entidad o sector, sub-vista).</summary>
            public CacheLru<CuadroCalculadoBd> Calculados { get; }

            /// <summary>Plantillas por (cuadro, variante), para la carga masiva de REP01.</summary>
            public CacheLru<PlantillaBd> Plantillas { get; }

            public IReadOnlyList<string> AdvertenciasMapa { get; }
            public DateTime Version { get; }
            public CacheLru<SeriesBd> Saldos { get; }
            public CacheLru<SeriesBd> Indicadores { get; }
            public CacheLru<SeriesBd> Sectores { get; }
            public IReadOnlyList<EntidadCatalogo>? Entidades { get; set; }
            public IReadOnlyList<CuadroFuente>? Cuadros { get; set; }
            public Task<IReadOnlyDictionary<string, ReporteEntidad>>? Reportes { get; set; }
        }

        /// <summary>
        /// Contexto vigente. Se revisa la ventana cada <see cref="SqlSettings.MinutosRevisionVentana"/>;
        /// si la firma cambió se crea un contexto nuevo (cachés vacías, versión nueva). Con el
        /// circuito abierto devuelve el último contexto conocido (o null si nunca hubo).
        /// </summary>
        private async Task<Contexto?> ObtenerContextoAsync(CancellationToken ct)
        {
            var ctx = _contexto;
            if (ctx is not null && Ahora - _revisadoEn < TimeSpan.FromMinutes(Math.Max(1, _settings.MinutosRevisionVentana)))
                return ctx;
            if (SqlNoDisponible)
                return ctx;

            await _refresco.WaitAsync(ct);
            try
            {
                ctx = _contexto;
                if (ctx is not null && Ahora - _revisadoEn < TimeSpan.FromMinutes(Math.Max(1, _settings.MinutosRevisionVentana)))
                    return ctx;
                if (SqlNoDisponible)
                    return ctx;

                try
                {
                    var ventana = await _repo.ObtenerVentanaAsync(ct);
                    if (ctx is null || ctx.Ventana.Firma != ventana.Firma)
                    {
                        var ifis = await _repo.ListarIfiAsync(ct);
                        var (ids, advertencias) = _mapa.Resolver(ifis);
                        var (catalogo, avisoCatalogo) = await CargarCatalogoAsync(ct);
                        ctx = new Contexto(ventana, ids, ifis.ToDictionary(i => i.IfiId, i => i.TipoEntidad), advertencias,
                            catalogo, avisoCatalogo, Ahora, _capacidadCache);
                        _contexto = ctx;
                        _logger.LogInformation(
                            "Fuente SQL: ventana {Desde}..{Hasta}, {Entidades} entidades mapeadas, {Cuadros} cuadros en el catálogo, firma {Firma}",
                            ventana.Desde, ventana.Hasta, ids.Count, catalogo.Count, ventana.Firma);
                        foreach (var advertencia in advertencias.Append(avisoCatalogo).OfType<string>())
                            _logger.LogWarning("{Advertencia}", advertencia);
                    }
                    _revisadoEn = Ahora;
                    _ultimoErrorSql = null;
                    return ctx;
                }
                catch (Exception ex) when (EsFallaSql(ex))
                {
                    AbrirCircuito(ex);
                    return ctx;
                }
            }
            finally
            {
                _refresco.Release();
            }
        }

        /// <summary>
        /// Catálogo de cuadros (Fase 2). Si sus procedimientos no existen (Fase 2 sin instalar) no
        /// es una caída de SQL: se sigue con la plantilla JSON y se avisa en <c>/api/meta</c>.
        /// </summary>
        private async Task<(IReadOnlyDictionary<string, CuadroBd> Catalogo, string? Aviso)> CargarCatalogoAsync(CancellationToken ct)
        {
            try
            {
                var cuadros = await _repo.ListarCuadrosAsync(ct);
                return (cuadros.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase), null);
            }
            catch (DbException ex)
            {
                return (new Dictionary<string, CuadroBd>(),
                    $"Catálogo de cuadros no disponible ({ex.GetBaseException().Message}); se usa la plantilla de los JSON.");
            }
        }

        /// <summary>
        /// Cuadro del catálogo armado por el SP para la entidad o el sector de la consulta; null si
        /// la entidad no está mapeada, el sector no se reconoce o SQL falla (el llamador usa la ruta JSON).
        /// </summary>
        private Task<CuadroCalculadoBd?> CuadroCalculadoAsync(Contexto ctx, CuadroBd cuadro, string? entidadId, string? filtro, string? idSub)
        {
            if (cuadro.EsDeEntidad)
            {
                if (entidadId is null || !ctx.Ids.TryGetValue(entidadId, out var ifiId))
                    return Task.FromResult<CuadroCalculadoBd?>(null);
                return IntentarAsync(() => ctx.Calculados.ObtenerAsync($"{cuadro.Id}|E{ifiId}|{idSub}",
                    () => _repo.ObtenerCuadroEntidadAsync(cuadro.Id, ifiId, idSub, CancellationToken.None)));
            }

            if (!FiltrosSistema.TryObtener(filtro, out var regla))
                return Task.FromResult<CuadroCalculadoBd?>(null);
            return IntentarAsync(() => ctx.Calculados.ObtenerAsync($"{cuadro.Id}|S{filtro}|{idSub}",
                () => _repo.ObtenerCuadroSectorAsync(cuadro.Id, regla.TiposEntidad, regla.Segmentos, idSub, CancellationToken.None)));
        }

        private Task<SeriesBd?> SaldosEntidadAsync(Contexto ctx, int ifiId) =>
            IntentarAsync(() => ctx.Saldos.ObtenerAsync(ifiId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                () => _repo.ObtenerSaldosEntidadAsync(ifiId, null, CancellationToken.None)));

        private Task<SeriesBd?> IndicadoresEntidadAsync(Contexto ctx, int ifiId) =>
            IntentarAsync(() => ctx.Indicadores.ObtenerAsync(ifiId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                () => _repo.ObtenerIndicadoresEntidadAsync(ifiId, ReglasCuadro.Indicadores, CancellationToken.None)));

        private Task<SeriesBd?> SaldosSectorAsync(Contexto ctx, string? filtro)
        {
            if (!FiltrosSistema.TryObtener(filtro, out var regla))
                return Task.FromResult<SeriesBd?>(null);
            return IntentarAsync(() => ctx.Sectores.ObtenerAsync(filtro!,
                () => _repo.ObtenerSaldosAgregadoAsync(regla.TiposEntidad, regla.Segmentos, null, CancellationToken.None)));
        }

        /// <summary>
        /// Una sola consulta para las cuentas numéricas de REP01 de todas las entidades mapeadas.
        /// Si SQL falla devuelve los reportes JSON recortados y no deja el resultado en caché.
        /// </summary>
        private async Task<IReadOnlyDictionary<string, ReporteEntidad>> ConstruirReportesAsync(Contexto ctx, IReadOnlyDictionary<string, ReporteEntidad> reportes)
        {
            // Sale del lock del llamador antes de trabajar: así, si falla, el "ctx.Reportes = null"
            // de abajo ocurre después de que el llamador guardó la tarea y la limpieza no se pierde.
            await Task.Yield();

            if (ctx.Catalogo.TryGetValue("REP01", out var rep01) && await ConstruirReportesCatalogoAsync(ctx, rep01, reportes) is { } desdeCatalogo)
                return desdeCatalogo;

            IReadOnlyDictionary<int, SeriesBd>? masivo = null;
            if (_reglas.UsaSql("REP01"))
            {
                var ids = reportes.Keys.Where(ctx.Ids.ContainsKey).Select(k => ctx.Ids[k]).Distinct().ToList();
                var cuentas = reportes.Values
                    .SelectMany(r => r.Cuentas)
                    .Select(c => ReglasCuadro.Cuenta(c.Cuc))
                    .OfType<string>()
                    .Where(c => !_reglas.Omitida(c))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                masivo = await IntentarAsync(() => _repo.ObtenerSaldosEntidadesAsync(ids, cuentas, CancellationToken.None));
            }

            var salida = new Dictionary<string, ReporteEntidad>(reportes.Count, StringComparer.Ordinal);
            foreach (var (id, reporte) in reportes)
            {
                SeriesBd? saldos = null;
                if (masivo is not null && ctx.Ids.TryGetValue(id, out var ifiId))
                    saldos = masivo.TryGetValue(ifiId, out var s) ? s : SeriesBd.Vacia;
                salida[id] = CombinarReporte(reporte, ctx.Eje, saldos);
            }

            if (masivo is null && _reglas.UsaSql("REP01"))
                lock (ctx)
                    ctx.Reportes = null;   // se reintenta en el próximo pedido
            return salida;
        }

        /// <summary>
        /// REP01 de todas las entidades desde el catálogo: una consulta de valores para todas y una
        /// plantilla por variante. null si SQL falla (el llamador sigue con la ruta de Fase 1).
        /// </summary>
        private async Task<IReadOnlyDictionary<string, ReporteEntidad>?> ConstruirReportesCatalogoAsync(
            Contexto ctx, CuadroBd rep01, IReadOnlyDictionary<string, ReporteEntidad> reportes)
        {
            var ifiIds = reportes.Keys.Where(ctx.Ids.ContainsKey).Select(k => ctx.Ids[k]).Distinct().ToList();
            var valores = await IntentarAsync(() => _repo.ObtenerValoresCuadroEntidadesAsync(rep01.Id, ifiIds, null, CancellationToken.None));
            if (valores is null)
                return null;

            string Variante(int ifiId) => rep01.ResolverVariante(ctx.TipoPorIfi.GetValueOrDefault(ifiId));

            var plantillas = new Dictionary<string, PlantillaBd>(StringComparer.Ordinal);
            foreach (var variante in ifiIds.Select(Variante).Distinct())
            {
                var plantilla = await IntentarAsync(() => ctx.Plantillas.ObtenerAsync($"{rep01.Id}|{variante}",
                    () => _repo.ObtenerPlantillaAsync(rep01.Id, variante, null, CancellationToken.None)));
                if (plantilla is null)
                    return null;
                plantillas[variante] = plantilla;
            }

            var salida = new Dictionary<string, ReporteEntidad>(reportes.Count, StringComparer.Ordinal);
            foreach (var (id, reporte) in reportes)
            {
                salida[id] = ctx.Ids.TryGetValue(id, out var ifiId)
                    ? ArmadorCatalogo.Reporte(id, plantillas[Variante(ifiId)].Filas,
                        valores.TryGetValue(ifiId, out var v) ? v : SeriesBd.Vacia, reporte, ctx.Eje)
                    : CombinarReporte(reporte, ctx.Eje, null);
            }
            return salida;
        }

        private async Task<T?> IntentarAsync<T>(Func<Task<T>> accion) where T : class
        {
            if (SqlNoDisponible)
                return null;
            try
            {
                return await accion();
            }
            catch (Exception ex) when (EsFallaSql(ex))
            {
                AbrirCircuito(ex);
                return null;
            }
        }

        private void AbrirCircuito(Exception ex)
        {
            var hasta = Ahora.AddSeconds(Math.Max(1, _settings.SegundosCircuitoAbierto));
            Interlocked.Exchange(ref _circuitoHastaTicks, hasta.Ticks);
            _ultimoErrorSql = ex.GetBaseException().Message;
            _logger.LogWarning(ex, "Fuente SQL no disponible; se sirve solo JSON hasta {Hasta:O}", hasta);
        }

        private static bool EsFallaSql(Exception ex) =>
            ex is DbException or TimeoutException or InvalidOperationException or IOException or SocketException or Win32Exception;

        // =====================================================================================
        // Combinación de filas
        // =====================================================================================

        /// <summary>
        /// Filas del JSON proyectadas al eje de la ventana: las de cuenta con valores de SQL (si los
        /// hay), los indicadores de <c>IndicadorData</c> y el resto con sus valores JSON. Las cuentas
        /// de grupos omitidos se descartan en los cuadros servidos por SQL. Respeta el orden (R4) y
        /// NaN ≠ 0 (R2/R3). Devuelve copias: no modifica las filas cacheadas del JSON.
        /// </summary>
        internal List<FilaDatos> Combinar(IReadOnlyList<FilaDatos> filas, string[] eje, SeriesBd? saldos, SeriesBd? indicadores)
        {
            var proyector = new Proyector(eje);
            var salida = new List<FilaDatos>(filas.Count);
            foreach (var fila in filas)
            {
                var deSql = _reglas.UsaSql(fila.Cuadro);
                var cuenta = deSql ? ReglasCuadro.Cuenta(fila) : null;
                if (cuenta is not null && _reglas.Omitida(cuenta))
                    continue;

                double[] valores;
                var origen = OrigenDatos.Bd;
                if (cuenta is not null && saldos is not null && ReglasCuadro.EsFilaSaldo(fila))
                    valores = proyector.Serie(saldos, cuenta, ReglasCuadro.EscalaSaldo);
                else if (deSql && indicadores is not null && ReglasCuadro.TryEscalaIndicador(fila.Cuc, out var escala) && indicadores.TryObtener(fila.Cuc!, out _))
                    valores = proyector.Serie(indicadores, fila.Cuc!, escala);
                else
                    (valores, origen) = (proyector.Json(fila.Periodos, fila.Valores), OrigenDatos.Json);

                salida.Add(Copiar(fila, eje, valores, origen));
            }
            return salida;
        }

        internal ReporteEntidad CombinarReporte(ReporteEntidad reporte, string[] eje, SeriesBd? saldos)
        {
            var proyector = new Proyector(eje);
            var cuentas = new List<CuentaReporte>(reporte.Cuentas.Count);
            foreach (var c in reporte.Cuentas)
            {
                var cuenta = ReglasCuadro.Cuenta(c.Cuc);
                if (cuenta is not null && _reglas.Omitida(cuenta))
                    continue;
                var deBd = cuenta is not null && saldos is not null;
                var valores = deBd
                    ? proyector.Serie(saldos!, cuenta!, ReglasCuadro.EscalaSaldo)
                    : proyector.Json(reporte.Periodos, c.Valores);
                cuentas.Add(new CuentaReporte(c.Cuc, c.Variable, valores, c.SinHistoriaHasta, deBd ? OrigenDatos.Bd : OrigenDatos.Json));
            }
            return new ReporteEntidad(reporte.EntidadId, eje, cuentas);
        }

        private static FilaDatos Copiar(FilaDatos f, string[] periodos, double[] valores, string? origen) => new()
        {
            Cuadro = f.Cuadro,
            Filtro = f.Filtro,
            Cuc = f.Cuc,
            Id = f.Id,
            Titulo = f.Titulo,
            Unidad = f.Unidad,
            Grupo = f.Grupo,
            Variable = f.Variable,
            Nivel = f.Nivel,
            NivelJerarquia = f.NivelJerarquia,
            CodigoBase = f.CodigoBase,
            SegmentoCredito = f.SegmentoCredito,
            Tamano = f.Tamano,
            RangoActivos = f.RangoActivos,
            DpaPr = f.DpaPr,
            Periodos = periodos,
            Valores = valores,
            Origen = origen
        };
    }
}
