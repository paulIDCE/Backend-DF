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
    /// <see cref="IFuenteDatos"/> híbrida (informe §8): la plantilla de cada cuadro (orden,
    /// títulos, jerarquía) sale siempre de la fuente JSON; los valores de las filas de cuenta y de
    /// los indicadores de <c>IndicadorData</c> salen de <c>idce_bco_coop</c> vía
    /// <see cref="IBcoCoopRepositorio"/>; el resto, del JSON.
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

            return ctx.Cuadros ??= cuadros.Select(c => !_reglas.UsaVentana(c.Tipo, c.Id) ? c : new CuadroFuente
            {
                Id = c.Id,
                Titulo = c.Titulo,
                Unidad = c.Unidad,
                Origen = c.Origen,
                Tipo = c.Tipo,
                Frecuencia = c.Frecuencia,
                PeriodoDesde = ctx.Ventana.Desde,
                PeriodoHasta = ctx.Ventana.Hasta
            }).ToList();
        }

        public async Task<IReadOnlyList<FilaDatos>> ObtenerFilasAsync(ConsultaFilas consulta, CancellationToken ct)
        {
            var filas = await _json.ObtenerFilasAsync(consulta, ct);
            if (!_reglas.UsaVentana(consulta.Tipo, consulta.Cuadro))
                return filas;

            var ctx = await ObtenerContextoAsync(ct);
            if (ctx is null)
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

            var saldos = filas.Any(f => _reglas.UsaSql(f.Cuadro)) ? await SaldosSectorAsync(ctx, filtro) : null;
            return Combinar(filas, ctx.Eje, saldos, null);
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
                fuentes.Add(new InfoFuente
                {
                    Fuente = NombreFuenteSql,
                    Archivo = "idce_bco_coop",
                    Archivos = ctx.Ids.Count,
                    Desde = ctx.Ventana.Desde,
                    Hasta = ctx.Ventana.Hasta,
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
                Advertencias = advertencias
            };
        }

        // =====================================================================================
        // Ventana, cachés y circuito
        // =====================================================================================

        /// <summary>Estado de la BD para la carga vigente: ventana, eje de meses, mapa de ids y cachés.</summary>
        private sealed class Contexto
        {
            public Contexto(VentanaBd ventana, IReadOnlyDictionary<string, int> ids, IReadOnlyList<string> advertenciasMapa, DateTime version, int capacidad)
            {
                Ventana = ventana;
                Eje = Periodos.Meses(ventana.Desde, ventana.Hasta);
                Ids = ids;
                AdvertenciasMapa = advertenciasMapa;
                Version = version;
                Saldos = new CacheLru<SeriesBd>(capacidad);
                Indicadores = new CacheLru<SeriesBd>(capacidad);
                Sectores = new CacheLru<SeriesBd>(16);
            }

            public VentanaBd Ventana { get; }

            /// <summary>Eje único de la ventana: todas las filas recortadas comparten esta instancia.</summary>
            public string[] Eje { get; }

            public IReadOnlyDictionary<string, int> Ids { get; }
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
                        var (ids, advertencias) = _mapa.Resolver(await _repo.ListarIfiAsync(ct));
                        ctx = new Contexto(ventana, ids, advertencias, Ahora, _capacidadCache);
                        _contexto = ctx;
                        _logger.LogInformation(
                            "Fuente SQL: ventana {Desde}..{Hasta}, {Entidades} entidades mapeadas, firma {Firma}",
                            ventana.Desde, ventana.Hasta, ids.Count, ventana.Firma);
                        foreach (var advertencia in advertencias)
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
                if (cuenta is not null && saldos is not null && ReglasCuadro.EsFilaSaldo(fila))
                    valores = proyector.Serie(saldos, cuenta, ReglasCuadro.EscalaSaldo);
                else if (deSql && indicadores is not null && ReglasCuadro.TryEscalaIndicador(fila.Cuc, out var escala) && indicadores.TryObtener(fila.Cuc!, out _))
                    valores = proyector.Serie(indicadores, fila.Cuc!, escala);
                else
                    valores = proyector.Json(fila.Periodos, fila.Valores);

                salida.Add(Copiar(fila, eje, valores));
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
                var valores = cuenta is not null && saldos is not null
                    ? proyector.Serie(saldos, cuenta, ReglasCuadro.EscalaSaldo)
                    : proyector.Json(reporte.Periodos, c.Valores);
                cuentas.Add(new CuentaReporte(c.Cuc, c.Variable, valores));
            }
            return new ReporteEntidad(reporte.EntidadId, eje, cuentas);
        }

        private static FilaDatos Copiar(FilaDatos f, string[] periodos, double[] valores) => new()
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
            Valores = valores
        };

        /// <summary>Proyecta series (ordenadas por período) sobre el eje; el mapeo se calcula una vez por eje de origen.</summary>
        private sealed class Proyector
        {
            private readonly string[] _eje;
            private readonly Dictionary<string[], int[]> _mapas = new(ReferenceEqualityComparer.Instance);

            public Proyector(string[] eje)
            {
                _eje = eje;
            }

            public double[] Json(string[] periodos, double[] valores) => Aplicar(periodos, valores, 1);

            /// <summary>Serie de la BD escalada; si el código no tiene filas, todo NaN (la BD manda).</summary>
            public double[] Serie(SeriesBd series, string codigo, double escala)
            {
                if (!series.TryObtener(codigo, out var valores))
                {
                    var vacio = new double[_eje.Length];
                    Array.Fill(vacio, double.NaN);
                    return vacio;
                }
                return Aplicar(series.Periodos, valores, escala);
            }

            private double[] Aplicar(string[] origen, double[] valores, double escala)
            {
                if (!_mapas.TryGetValue(origen, out var mapa))
                {
                    mapa = new int[_eje.Length];
                    for (var i = 0; i < _eje.Length; i++)
                        mapa[i] = Array.BinarySearch(origen, _eje[i], StringComparer.Ordinal);
                    _mapas[origen] = mapa;
                }
                var salida = new double[mapa.Length];
                for (var i = 0; i < mapa.Length; i++)
                    salida[i] = mapa[i] >= 0 ? valores[mapa[i]] * escala : double.NaN;
                return salida;
            }
        }
    }
}
