using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using BackendDF.Common.Utils;
using BackendDF.Configuration;
using BackendDF.Data.Interfaces;
using BackendDF.Logic.Dominio;
using BackendDF.Models.Entities;
using Microsoft.Extensions.Options;

namespace BackendDF.Data.FuenteJson
{
    /// <summary>
    /// Implementación de <see cref="IFuenteDatos"/> sobre los JSON de prueba-data, con la misma
    /// estructura de carpetas (contrato §3). Estrategia de carga (§3.4):
    ///
    /// - <b>Base</b> (<c>base_*.json</c>, catálogo, notas ≈ 115 MB): al iniciar, en memoria,
    ///   indexada por Cuadro / Filtro / ID. Los requests que llegan antes esperan a que termine.
    /// - <b>Índice de reportes</b> (<c>reportes/*</c>): en segundo plano después de la base.
    ///   Mientras se construye, un reporte suelto se lee directo de su archivo.
    /// - <b>Por entidad</b> (<c>entidades/*</c>, <c>balances/*</c>): bajo demanda con caché LRU
    ///   de <c>MaxEntidadesEnCache</c> archivos por carpeta.
    ///
    /// Singleton: se registra una sola instancia y la carga la dispara
    /// <see cref="CargaDatosHostedService"/> al arrancar.
    /// </summary>
    public sealed partial class FuenteDatosJson : IFuenteDatos
    {
        private const string CarpetaEntidades = "entidades";
        private const string CarpetaBalances = "balances";
        private const string CarpetaReportes = "reportes";
        private const string ArchivoCatalogo = "entidades_lista.json";
        private const string ArchivoNotas = "base_notas.json";
        private const string MensajeNoDisponible = "Los datos no están disponibles en este momento. Intente más tarde.";
        private const string MensajeArchivoCorrupto = "No se pudieron leer los datos solicitados.";

        /// <summary>El <c>archivo</c> del catálogo solo puede ser un nombre plano (sin rutas).</summary>
        [GeneratedRegex("^[A-Za-z0-9_]+$")]
        private static partial Regex ReArchivoSeguro();

        private readonly ILogger<FuenteDatosJson> _logger;
        private readonly string _ruta;
        private readonly PoolTextos _pool = new();
        private readonly CacheLru<TablaDatos> _cacheEntidades;
        private readonly CacheLru<TablaDatos> _cacheBalances;
        private readonly ConcurrentDictionary<string, ReporteEntidad> _reportes = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<string> _advertenciasIndice = new();
        private readonly Lazy<Task<DatosBase>> _base;
        private readonly TaskCompletionSource _indice = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _iniciado;

        public FuenteDatosJson(IOptions<DatosSettings> opciones, IHostEnvironment env, ILogger<FuenteDatosJson> logger)
        {
            _logger = logger;
            var datos = opciones.Value;
            _ruta = Path.GetFullPath(Path.Combine(env.ContentRootPath, datos.RutaBase));
            _cacheEntidades = new CacheLru<TablaDatos>(datos.MaxEntidadesEnCache);
            _cacheBalances = new CacheLru<TablaDatos>(datos.MaxEntidadesEnCache);
            _base = new Lazy<Task<DatosBase>>(() => Task.Run(CargarBaseAsync));
        }

        // =====================================================================================
        // Ciclo de carga
        // =====================================================================================

        /// <summary>Dispara la carga de la base y, a continuación, el índice de reportes. Idempotente.</summary>
        public void IniciarCarga()
        {
            if (Interlocked.Exchange(ref _iniciado, 1) == 1)
                return;

            _logger.LogInformation("Cargando datos JSON desde {Ruta}", _ruta);
            _ = Task.Run(async () =>
            {
                try
                {
                    await _base.Value;
                    await ConstruirIndiceAsync();
                    _indice.TrySetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo cargar la fuente de datos JSON desde {Ruta}", _ruta);
                    _indice.TrySetException(ex);
                }
            });
        }

        public EstadoFuente Estado
        {
            get
            {
                if (!_base.IsValueCreated)
                    return EstadoFuente.Cargando;
                var baseTask = _base.Value;
                if (baseTask.IsFaulted || _indice.Task.IsFaulted)
                    return EstadoFuente.Error;
                if (!baseTask.IsCompletedSuccessfully)
                    return EstadoFuente.Cargando;
                return _indice.Task.IsCompletedSuccessfully ? EstadoFuente.Lista : EstadoFuente.IndexandoReportes;
            }
        }

        public string? DetalleEstado =>
            _base.IsValueCreated && _base.Value.IsFaulted
                ? _base.Value.Exception?.GetBaseException().Message
                : _indice.Task.Exception?.GetBaseException().Message;

        private async Task<DatosBase> BaseAsync(CancellationToken ct)
        {
            IniciarCarga();
            try
            {
                return await _base.Value.WaitAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw CustomException.Interno($"Fuente JSON no disponible: {ex.GetBaseException().Message}", MensajeNoDisponible);
            }
        }

        // =====================================================================================
        // IFuenteDatos
        // =====================================================================================

        public async Task<DateTime> ObtenerVersionDatosAsync(CancellationToken ct) =>
            (await BaseAsync(ct)).Info.VersionDatos;

        public async Task<IReadOnlyList<EntidadCatalogo>> ObtenerEntidadesAsync(CancellationToken ct) =>
            (await BaseAsync(ct)).Entidades;

        public async Task<IReadOnlyList<CuadroFuente>> ObtenerCuadrosAsync(CancellationToken ct) =>
            (await BaseAsync(ct)).Cuadros;

        public async Task<IReadOnlyList<FilaDatos>> ObtenerFilasAsync(ConsultaFilas consulta, CancellationToken ct)
        {
            var b = await BaseAsync(ct);
            switch (consulta.Tipo)
            {
                case TipoCuadro.Macro:
                    return b.Macro.TryGetValue(consulta.Cuadro, out var filas) ? filas : [];
                case TipoCuadro.Sistema:
                    return b.Sistema.Buscar(consulta.Cuadro, consulta.Filtro, null);
                case TipoCuadro.Balances:
                    return b.Balances.Buscar(consulta.Cuadro, consulta.Filtro, consulta.Id);
                case TipoCuadro.Cartera:
                    return b.Cartera.Buscar(consulta.Cuadro, consulta.Filtro, consulta.Id);
                case TipoCuadro.Entidad:
                case TipoCuadro.CarteraEntidad:
                    return (await TablaEntidadAsync(b, CarpetaEntidades, consulta.EntidadId, ct)).Buscar(consulta.Cuadro, null, consulta.Id);
                case TipoCuadro.BalancesEntidad:
                    return (await TablaEntidadAsync(b, CarpetaBalances, consulta.EntidadId, ct)).Buscar(consulta.Cuadro, null, consulta.Id);
                default:
                    throw new ArgumentOutOfRangeException(nameof(consulta), consulta.Tipo, "Tipo de cuadro no soportado.");
            }
        }

        public async Task<IReadOnlyList<FilaDatos>> ObtenerFilasSistemaAsync(string filtro, CancellationToken ct) =>
            (await BaseAsync(ct)).Sistema.BuscarPorFiltro(filtro);

        public async Task<IReadOnlyList<string>> ObtenerNotasAsync(string cuadro, CancellationToken ct) =>
            (await BaseAsync(ct)).Notas.TryGetValue(cuadro, out var notas) ? notas : [];

        public async Task<ReporteEntidad?> ObtenerReporteAsync(string entidadId, CancellationToken ct)
        {
            var b = await BaseAsync(ct);
            if (!b.EntidadesPorId.TryGetValue(entidadId, out var entidad))
                return null;
            if (_reportes.TryGetValue(entidad.Id, out var reporte))
                return reporte;

            // El índice aún no llega a esta entidad: se lee su archivo y se deja en el índice.
            reporte = await CargarReporteAsync(entidad, ct);
            return reporte is null ? null : _reportes.GetOrAdd(entidad.Id, reporte);
        }

        public async Task<IReadOnlyDictionary<string, ReporteEntidad>> ObtenerReportesAsync(CancellationToken ct)
        {
            await BaseAsync(ct);
            try
            {
                await _indice.Task.WaitAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw CustomException.Interno($"Índice de reportes no disponible: {ex.GetBaseException().Message}", MensajeNoDisponible);
            }
            return _reportes;
        }

        public async Task<InfoFuentes> ObtenerInfoAsync(CancellationToken ct)
        {
            var info = (await BaseAsync(ct)).Info;
            if (_advertenciasIndice.IsEmpty)
                return info;
            return new InfoFuentes
            {
                VersionDatos = info.VersionDatos,
                Fuentes = info.Fuentes,
                UltimoCorteComun = info.UltimoCorteComun,
                Advertencias = [.. info.Advertencias, .. _advertenciasIndice]
            };
        }

        // =====================================================================================
        // Archivos por entidad y reportes
        // =====================================================================================

        private async Task<TablaDatos> TablaEntidadAsync(DatosBase b, string carpeta, string? entidadId, CancellationToken ct)
        {
            // R5 / seguridad: la ruta sale SOLO del catálogo, nunca del texto del request.
            if (entidadId is null || !b.EntidadesPorId.TryGetValue(entidadId, out var entidad))
                throw CustomException.NoEncontrado($"No existe la entidad '{entidadId}'.");

            var ruta = Path.Combine(_ruta, carpeta, entidad.Id + ".json");
            if (!File.Exists(ruta))
                throw CustomException.NoEncontrado(carpeta == CarpetaBalances
                    ? $"La entidad {entidad.Nombre} no tiene balance detallado."
                    : $"No hay datos de la entidad {entidad.Nombre}.");

            return await CargarTablaEntidadAsync(carpeta, entidad.Id, ruta).WaitAsync(ct);
        }

        /// <summary>Carga compartida: no se cancela si el request que la inició se cancela.</summary>
        private Task<TablaDatos> CargarTablaEntidadAsync(string carpeta, string entidadId, string ruta)
        {
            var cache = carpeta == CarpetaBalances ? _cacheBalances : _cacheEntidades;
            return cache.ObtenerAsync(entidadId, () => LeerTablaAsync(ruta, $"{carpeta}/{entidadId}.json", indexarFiltro: false, CancellationToken.None));
        }

        private async Task<TablaDatos> LeerTablaAsync(string ruta, string nombre, bool indexarFiltro, CancellationToken ct)
        {
            try
            {
                var filas = await LectorJson.LeerFilasAsync(ruta, _pool, ct);
                return new TablaDatos(nombre, filas, indexarFiltro, File.GetLastWriteTimeUtc(ruta));
            }
            catch (JsonException ex)
            {
                throw CustomException.Interno($"Archivo corrupto {nombre}: {ex.Message}", MensajeArchivoCorrupto);
            }
        }

        private async Task<ReporteEntidad?> CargarReporteAsync(EntidadCatalogo entidad, CancellationToken ct)
        {
            var ruta = Path.Combine(_ruta, CarpetaReportes, entidad.Id + ".json");
            if (!File.Exists(ruta))
                return null;

            var tabla = await LeerTablaAsync(ruta, $"{CarpetaReportes}/{entidad.Id}.json", indexarFiltro: false, ct);
            var eje = tabla.Periodos;
            var cuentas = new List<CuentaReporte>(tabla.Filas.Count);
            foreach (var fila in tabla.Filas)
            {
                if (fila.Cuc is null)
                    continue;
                // Si la fila ya cubre todo el eje de la entidad se reutiliza su arreglo tal cual.
                var valores = fila.Periodos.Length == eje.Length ? fila.Valores : Expandir(fila, eje);
                cuentas.Add(new CuentaReporte(fila.Cuc, fila.Variable, valores));
            }
            return new ReporteEntidad(entidad.Id, eje, cuentas);
        }

        private static double[] Expandir(FilaDatos fila, string[] eje)
        {
            var salida = new double[eje.Length];
            for (var i = 0; i < eje.Length; i++)
            {
                var j = Array.BinarySearch(fila.Periodos, eje[i], StringComparer.Ordinal);
                salida[i] = j >= 0 ? fila.Valores[j] : double.NaN;
            }
            return salida;
        }

        private async Task ConstruirIndiceAsync()
        {
            var b = await _base.Value;
            var cronometro = Stopwatch.StartNew();
            var opciones = new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 6) };

            await Parallel.ForEachAsync(b.Entidades, opciones, async (entidad, ct) =>
            {
                if (_reportes.ContainsKey(entidad.Id))
                    return;
                try
                {
                    var reporte = await CargarReporteAsync(entidad, ct);
                    if (reporte is not null)
                        _reportes.TryAdd(entidad.Id, reporte);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo indexar {Carpeta}/{Entidad}.json", CarpetaReportes, entidad.Id);
                    _advertenciasIndice.Enqueue($"{CarpetaReportes}/{entidad.Id}.json no se pudo leer ({ex.Message}).");
                }
            });

            _logger.LogInformation("Índice de reportes listo: {Cantidad} entidades en {Segundos:F1} s",
                _reportes.Count, cronometro.Elapsed.TotalSeconds);
        }

        // =====================================================================================
        // Carga de la base
        // =====================================================================================

        private sealed record FilaCatalogoJson(string? Nombre, string? Archivo);

        private sealed record FilaNotaJson(string? Cuadro, string? Notas);

        private sealed record CatalogoCargado(
            List<EntidadCatalogo> Entidades,
            List<string> Advertencias,
            Dictionary<string, int> ArchivosPorCarpeta);

        private sealed class DatosBase
        {
            public required Dictionary<string, List<FilaDatos>> Macro { get; init; }
            public required TablaDatos Sistema { get; init; }
            public required TablaDatos Balances { get; init; }
            public required TablaDatos Cartera { get; init; }
            public required Dictionary<string, List<string>> Notas { get; init; }
            public required List<EntidadCatalogo> Entidades { get; init; }
            public required Dictionary<string, EntidadCatalogo> EntidadesPorId { get; init; }
            public required List<CuadroFuente> Cuadros { get; init; }
            public required InfoFuentes Info { get; init; }
        }

        private async Task<DatosBase> CargarBaseAsync()
        {
            if (!Directory.Exists(_ruta))
                throw new DirectoryNotFoundException($"No existe la carpeta de datos (Datos:RutaBase): {_ruta}");

            var cronometro = Stopwatch.StartNew();
            var ct = CancellationToken.None;

            Task<TablaDatos> Tabla(string archivo, bool indexarFiltro) =>
                Task.Run(() => LeerTablaAsync(Path.Combine(_ruta, archivo), archivo, indexarFiltro, ct));

            var tAnual = Tabla("base_anual.json", false);
            var tMensual = Tabla("base_mensual.json", false);
            var tTrimestral = Tabla("base_trimestral.json", false);
            var tSistema = Tabla("base_estru_sistema.json", true);
            var tBalances = Tabla("base_balances.json", true);
            var tCartera = Tabla("base_cartera.json", true);
            var tNotas = CargarNotasAsync(ct);
            var tCatalogo = CargarCatalogoAsync(ct);
            await Task.WhenAll(tAnual, tMensual, tTrimestral, tSistema, tBalances, tCartera, tNotas, tCatalogo);

            var (anual, mensual, trimestral) = (tAnual.Result, tMensual.Result, tTrimestral.Result);
            var (sistema, balances, cartera) = (tSistema.Result, tBalances.Result, tCartera.Result);
            var catalogo = tCatalogo.Result;
            var advertencias = new List<string>();
            var hoy = DateTime.Today;

            void AdvertirFuturos(string nombre, string[] periodos)
            {
                // R8: no se filtran; solo se informan.
                var futuros = periodos.Where(p => string.CompareOrdinal(p, Periodos.ActualComo(p, hoy)) > 0).ToList();
                if (futuros.Count > 0)
                    advertencias.Add($"{nombre} trae períodos posteriores a hoy ({futuros[0]}..{futuros[^1]}).");
            }

            foreach (var tabla in new[] { anual, mensual, trimestral, sistema, balances, cartera })
                AdvertirFuturos(tabla.Archivo, tabla.Periodos);

            // ---- Cuadros ----
            var cuadros = new List<CuadroFuente>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Registrar(string id, IReadOnlyList<FilaDatos> filas, string origen, TipoCuadro tipo, bool anualArchivo)
            {
                if (id.Length == 0)
                    return;
                if (!ids.Add(id))
                {
                    advertencias.Add($"El cuadro {id} aparece en más de un archivo; se usa el primero.");
                    return;
                }
                var eje = Periodos.Union(filas.Select(f => f.Periodos));
                cuadros.Add(new CuadroFuente
                {
                    Id = id,
                    Titulo = filas.Select(f => f.Titulo).FirstOrDefault(t => t is not null) ?? id,
                    Unidad = filas.Select(f => f.Unidad).FirstOrDefault(u => u is not null),
                    Origen = origen,
                    Tipo = tipo,
                    Frecuencia = Periodos.FrecuenciaDe(eje, anualArchivo),
                    PeriodoDesde = eje.FirstOrDefault(),
                    PeriodoHasta = eje.LastOrDefault()
                });
            }

            // Macro: se unen los tres archivos y se filtra por Cuadro (como el frontend).
            var macro = new Dictionary<string, List<FilaDatos>>(StringComparer.Ordinal);
            foreach (var tabla in new[] { anual, mensual, trimestral })
                foreach (var (id, filas) in tabla.PorCuadro)
                {
                    Registrar(id, filas, "macro", TipoCuadro.Macro, ReferenceEquals(tabla, anual));
                    if (macro.TryGetValue(id, out var existentes))
                        existentes.AddRange(filas);
                    else
                        macro[id] = [.. filas];
                }

            foreach (var (id, filas) in sistema.PorCuadro)
                Registrar(id, filas, "sistema", TipoCuadro.Sistema, false);
            foreach (var (id, filas) in balances.PorCuadro)
                Registrar(id, filas, "sistema", TipoCuadro.Balances, false);
            foreach (var (id, filas) in cartera.PorCuadro)
                Registrar(id, filas, "sistema", TipoCuadro.Cartera, false);

            // Cuadros por entidad: se describen con la primera entidad del catálogo que tenga archivo.
            var refEntidad = catalogo.Entidades.FirstOrDefault(e => File.Exists(Path.Combine(_ruta, CarpetaEntidades, e.Id + ".json")));
            TablaDatos? tablaRefEntidad = null;
            if (refEntidad is not null)
            {
                tablaRefEntidad = await CargarTablaEntidadAsync(CarpetaEntidades, refEntidad.Id, Path.Combine(_ruta, CarpetaEntidades, refEntidad.Id + ".json"));
                foreach (var (id, filas) in tablaRefEntidad.PorCuadro)
                    Registrar(id, filas, "entidad", filas.Any(f => f.Id is not null) ? TipoCuadro.CarteraEntidad : TipoCuadro.Entidad, false);
                AdvertirFuturos($"{CarpetaEntidades}/ (referencia {refEntidad.Id}.json)", tablaRefEntidad.Periodos);
            }

            var refBalance = catalogo.Entidades.FirstOrDefault(e => e.TieneBalance);
            TablaDatos? tablaRefBalance = null;
            if (refBalance is not null)
            {
                tablaRefBalance = await CargarTablaEntidadAsync(CarpetaBalances, refBalance.Id, Path.Combine(_ruta, CarpetaBalances, refBalance.Id + ".json"));
                foreach (var (id, filas) in tablaRefBalance.PorCuadro)
                    Registrar(id, filas, "entidad", TipoCuadro.BalancesEntidad, false);
            }

            string[] ordenOrigen = ["macro", "sistema", "entidad"];
            cuadros = [.. cuadros.OrderBy(c => Array.IndexOf(ordenOrigen, c.Origen)).ThenBy(c => c.Id, StringComparer.Ordinal)];

            // ---- Metadatos (/api/meta) ----
            advertencias.AddRange(catalogo.Advertencias);

            DateTime Modificado(string archivo)
            {
                var ruta = Path.Combine(_ruta, archivo);
                return File.Exists(ruta) ? File.GetLastWriteTimeUtc(ruta) : DateTime.MinValue;
            }

            InfoFuente DeTabla(string fuente, TablaDatos t) => new()
            {
                Fuente = fuente,
                Archivo = t.Archivo,
                Desde = t.Periodos.FirstOrDefault(),
                Hasta = t.Periodos.LastOrDefault(),
                Modificado = t.Modificado
            };

            var desdesReportes = catalogo.Entidades.Select(e => e.PeriodoDesde).OfType<string>().ToList();
            var hastasReportes = catalogo.Entidades.Select(e => e.PeriodoHasta).OfType<string>().ToList();
            var fuenteReportes = new InfoFuente
            {
                Fuente = "reportes",
                Archivos = catalogo.ArchivosPorCarpeta[CarpetaReportes],
                Desde = desdesReportes.Min(StringComparer.Ordinal),
                Hasta = hastasReportes.Max(StringComparer.Ordinal)
            };

            var fuentes = new List<InfoFuente>
            {
                DeTabla("macro-anual", anual),
                DeTabla("macro-mensual", mensual),
                DeTabla("macro-trimestral", trimestral),
                DeTabla("sistema", sistema),
                DeTabla("balances", balances),
                DeTabla("cartera", cartera),
                new() { Fuente = "notas", Archivo = ArchivoNotas, Modificado = Modificado(ArchivoNotas) },
                new() { Fuente = "catalogo", Archivo = ArchivoCatalogo, Archivos = catalogo.Entidades.Count, Modificado = Modificado(ArchivoCatalogo) },
                new()
                {
                    Fuente = "entidades",
                    Archivos = catalogo.ArchivosPorCarpeta[CarpetaEntidades],
                    Desde = tablaRefEntidad?.Periodos.FirstOrDefault(),
                    Hasta = tablaRefEntidad?.Periodos.LastOrDefault()
                },
                new()
                {
                    Fuente = "balances-entidad",
                    Archivos = catalogo.ArchivosPorCarpeta[CarpetaBalances],
                    Desde = tablaRefBalance?.Periodos.FirstOrDefault(),
                    Hasta = tablaRefBalance?.Periodos.LastOrDefault()
                },
                fuenteReportes
            };

            var version = new[] { anual, mensual, trimestral, sistema, balances, cartera }
                .Select(t => t.Modificado)
                .Append(Modificado(ArchivoNotas))
                .Append(Modificado(ArchivoCatalogo))
                .Max();

            // Menor de los "hasta" de reportes, balances y sistema.
            var ultimoCorte = new[] { fuenteReportes.Hasta, balances.Periodos.LastOrDefault(), sistema.Periodos.LastOrDefault() }
                .OfType<string>()
                .Min(StringComparer.Ordinal);

            _logger.LogInformation(
                "Datos base cargados en {Segundos:F1} s: {Cuadros} cuadros, {Entidades} entidades, {Advertencias} advertencias",
                cronometro.Elapsed.TotalSeconds, cuadros.Count, catalogo.Entidades.Count, advertencias.Count);
            foreach (var advertencia in advertencias)
                _logger.LogWarning("Datos: {Advertencia}", advertencia);

            return new DatosBase
            {
                Macro = macro,
                Sistema = sistema,
                Balances = balances,
                Cartera = cartera,
                Notas = tNotas.Result,
                Entidades = catalogo.Entidades,
                EntidadesPorId = catalogo.Entidades.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase),
                Cuadros = cuadros,
                Info = new InfoFuentes
                {
                    VersionDatos = version,
                    Fuentes = fuentes,
                    UltimoCorteComun = ultimoCorte,
                    Advertencias = advertencias
                }
            };
        }

        private async Task<Dictionary<string, List<string>>> CargarNotasAsync(CancellationToken ct)
        {
            var notas = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var ruta = Path.Combine(_ruta, ArchivoNotas);
            if (!File.Exists(ruta))
            {
                _logger.LogWarning("No existe {Archivo}: los cuadros se devolverán sin notas", ArchivoNotas);
                return notas;
            }

            foreach (var nota in await LectorJson.LeerListaAsync<FilaNotaJson>(ruta, ct))
            {
                if (string.IsNullOrWhiteSpace(nota.Cuadro) || string.IsNullOrWhiteSpace(nota.Notas))
                    continue;
                var cuadro = nota.Cuadro.Trim();
                if (!notas.TryGetValue(cuadro, out var parrafos))
                    notas[cuadro] = parrafos = [];
                // Párrafos separados por salto de línea, sin líneas vacías.
                parrafos.AddRange(nota.Notas.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
            }
            return notas;
        }

        private async Task<CatalogoCargado> CargarCatalogoAsync(CancellationToken ct)
        {
            var filas = await LectorJson.LeerListaAsync<FilaCatalogoJson>(Path.Combine(_ruta, ArchivoCatalogo), ct);
            var advertencias = new List<string>();

            var archivos = new Dictionary<string, HashSet<string>>
            {
                [CarpetaEntidades] = ListarArchivos(CarpetaEntidades),
                [CarpetaBalances] = ListarArchivos(CarpetaBalances),
                [CarpetaReportes] = ListarArchivos(CarpetaReportes)
            };

            var validas = new List<(string Id, string Nombre)>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fila in filas)
            {
                if (string.IsNullOrWhiteSpace(fila.Archivo) || string.IsNullOrWhiteSpace(fila.Nombre)
                    || !ReArchivoSeguro().IsMatch(fila.Archivo) || !ids.Add(fila.Archivo))
                {
                    advertencias.Add($"{ArchivoCatalogo}: entrada ignorada (nombre '{fila.Nombre}', archivo '{fila.Archivo}').");
                    continue;
                }
                validas.Add((fila.Archivo, fila.Nombre.Trim()));
            }

            // R6: Tamaño, Rango_Activos y DPA_PR salen de la primera fila del reporte.
            var entidades = new EntidadCatalogo[validas.Count];
            await Parallel.ForEachAsync(Enumerable.Range(0, validas.Count), new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (i, token) =>
            {
                var (id, nombre) = validas[i];
                FilaDatos? primera = null;
                if (archivos[CarpetaReportes].Contains(id))
                {
                    try
                    {
                        primera = (await LectorJson.LeerFilasAsync(Path.Combine(_ruta, CarpetaReportes, id + ".json"), _pool, token, maxFilas: 1)).FirstOrDefault();
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "No se pudo leer la cabecera de {Carpeta}/{Entidad}.json", CarpetaReportes, id);
                    }
                }

                entidades[i] = new EntidadCatalogo
                {
                    Id = id,
                    Nombre = nombre,
                    Tipo = TipoEntidad(nombre),
                    Tamano = primera?.Tamano,
                    Rango = primera?.RangoActivos,
                    Provincia = primera?.DpaPr,
                    TieneBalance = archivos[CarpetaBalances].Contains(id),
                    PeriodoDesde = primera?.Periodos.FirstOrDefault(),
                    PeriodoHasta = primera?.Periodos.LastOrDefault()
                };
            });

            // R5: el catálogo manda. Los archivos que no están en él se ignoran con un warning.
            foreach (var (carpeta, nombres) in archivos)
            {
                var huerfanos = nombres.Where(n => !ids.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
                if (huerfanos.Count > 0)
                {
                    advertencias.Add($"{carpeta}/: {huerfanos.Count} archivos sin entidad en el catálogo (ignorados).");
                    _logger.LogWarning("{Carpeta}/: archivos sin entidad en el catálogo (ignorados): {Archivos}", carpeta, string.Join(", ", huerfanos));
                }
                var faltantes = validas.Count(v => !nombres.Contains(v.Id));
                if (faltantes > 0)
                    advertencias.Add($"{carpeta}/: {faltantes} entidades del catálogo no tienen archivo.");
            }

            return new CatalogoCargado(
                [.. entidades.OrderBy(e => e.Nombre, Texto.ComparadorNombres)],
                advertencias,
                archivos.ToDictionary(a => a.Key, a => validas.Count(v => a.Value.Contains(v.Id))));
        }

        private HashSet<string> ListarArchivos(string carpeta)
        {
            var directorio = Path.Combine(_ruta, carpeta);
            if (!Directory.Exists(directorio))
                return new HashSet<string>(StringComparer.Ordinal);
            return Directory.EnumerateFiles(directorio, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
        }

        private static string TipoEntidad(string nombre) =>
            nombre.StartsWith("BP.", StringComparison.OrdinalIgnoreCase) ? "banco"
            : nombre.StartsWith("COAC.", StringComparison.OrdinalIgnoreCase) ? "cooperativa"
            : nombre.StartsWith("MUT.", StringComparison.OrdinalIgnoreCase) ? "mutualista"
            : "otro";
    }
}
