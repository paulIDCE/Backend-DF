using BackendDF.Common.Utils;
using BackendDF.Data.Interfaces;
using BackendDF.Logic.Dominio;
using BackendDF.Logic.Interfaces;
using BackendDF.Models.DTOs;
using BackendDF.Models.Entities;

namespace BackendDF.Logic.Services
{
    /// <summary>
    /// Resuelve el tipo de cuadro, valida sus parámetros, filtra las filas y arma la respuesta
    /// con claves estables, niveles y jerarquía (contrato §6).
    /// </summary>
    public class CuadrosService : ICuadrosService
    {
        private static readonly string[] Origenes = ["macro", "sistema", "entidad"];

        private readonly IFuenteDatos _fuente;

        public CuadrosService(IFuenteDatos fuente)
        {
            _fuente = fuente;
        }

        public async Task<IReadOnlyList<CuadroResumenDTO>> ListarAsync(string? origen, string? q, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(origen) && !Origenes.Contains(origen.Trim(), StringComparer.OrdinalIgnoreCase))
                throw CustomException.Validacion($"El valor '{origen}' no es válido para 'origen'. Valores permitidos: {string.Join(", ", Origenes)}.");

            var buscado = string.IsNullOrWhiteSpace(q) ? null : Texto.Normalizar(q);
            var cuadros = await _fuente.ObtenerCuadrosAsync(ct);
            return cuadros
                .Where(c => string.IsNullOrWhiteSpace(origen) || string.Equals(c.Origen, origen.Trim(), StringComparison.OrdinalIgnoreCase))
                .Where(c => buscado is null
                            || Texto.Normalizar(c.Id).Contains(buscado, StringComparison.Ordinal)
                            || Texto.Normalizar(c.Titulo).Contains(buscado, StringComparison.Ordinal))
                .Select(c => new CuadroResumenDTO(
                    c.Id,
                    c.Titulo,
                    c.Origen,
                    TiposCuadro.Nombre(c.Tipo),
                    c.Frecuencia,
                    TiposCuadro.Vista(c.Tipo),
                    TiposCuadro.Parametros(c.Tipo),
                    RangoPeriodosDTO.De(c.PeriodoDesde, c.PeriodoHasta)))
                .ToList();
        }

        public async Task<CuadroDTO> ObtenerAsync(string id, ParametrosCuadro p, CancellationToken ct)
        {
            var cuadros = await _fuente.ObtenerCuadrosAsync(ct);
            var cuadro = cuadros.FirstOrDefault(c => string.Equals(c.Id, id.Trim(), StringComparison.OrdinalIgnoreCase))
                         ?? throw CustomException.NoEncontrado($"No existe el cuadro '{id}'.");

            ValidarParametros(cuadro, p);

            var sector = p.Sector is null ? null : Catalogo.Resolver(Catalogo.Sectores, p.Sector, TiposCuadro.Sector);
            var analisis = p.Analisis is null ? null : Catalogo.Resolver(Catalogo.Analisis, p.Analisis, TiposCuadro.Analisis);
            var credito = p.Credito is null ? null : Catalogo.Resolver(Catalogo.Creditos, p.Credito, TiposCuadro.Credito);
            var entidad = p.Entidad is null ? null : await _fuente.ResolverEntidadAsync(p.Entidad, ct);
            var (desde, hasta) = Periodos.Rango(p.Desde, p.Hasta, cuadro.Frecuencia);

            var filas = await _fuente.ObtenerFilasAsync(
                new ConsultaFilas(cuadro.Tipo, cuadro.Id, sector?.Nombre, entidad?.Id, analisis?.Nombre ?? credito?.Nombre), ct);

            var disponibles = Periodos.Union(filas.Select(f => f.Periodos));
            var periodos = Periodos.Filtrar(disponibles, desde, hasta);
            var esArbol = TiposCuadro.EsArbol(cuadro.Tipo);

            // {contexto} de la clave: sector o entidad + análisis o crédito, unidos por "-".
            var contextoClave = string.Join("-", new[] { sector?.Id ?? entidad?.Id, analisis?.Id ?? credito?.Id }.OfType<string>());
            var filasDto = ArmarFilas(cuadro.Id, contextoClave, filas, periodos, esArbol);

            var notas = p.Notas && !esArbol ? await _fuente.ObtenerNotasAsync(cuadro.Id, ct) : [];
            var primera = filas.Count > 0 ? filas[0] : null;

            return new CuadroDTO(
                cuadro.Id,
                primera?.Titulo ?? cuadro.Titulo,
                primera is not null ? primera.Unidad : cuadro.Unidad,
                TiposCuadro.Nombre(cuadro.Tipo),
                cuadro.Frecuencia,
                TiposCuadro.Vista(cuadro.Tipo),
                new ContextoCuadroDTO(
                    sector?.Id, sector?.Nombre,
                    entidad?.Id, entidad?.Nombre,
                    analisis?.Id, analisis?.Nombre,
                    credito?.Id, credito?.Nombre),
                RangoPeriodosDTO.De(disponibles),
                periodos,
                filasDto,
                notas);
        }

        /// <summary>
        /// Cada tipo exige exactamente sus parámetros: uno que falta o uno que no aplica
        /// (p. ej. <c>sector</c> en un cuadro EFI) es 400, para evitar ambigüedad.
        /// </summary>
        private static void ValidarParametros(CuadroFuente cuadro, ParametrosCuadro p)
        {
            var requeridos = TiposCuadro.Parametros(cuadro.Tipo);
            (string Nombre, string? Valor)[] recibidos =
            [
                (TiposCuadro.Sector, p.Sector),
                (TiposCuadro.Entidad, p.Entidad),
                (TiposCuadro.Analisis, p.Analisis),
                (TiposCuadro.Credito, p.Credito)
            ];

            foreach (var (nombre, valor) in recibidos)
                if (!string.IsNullOrWhiteSpace(valor) && !requeridos.Contains(nombre))
                    throw CustomException.Validacion($"El parámetro '{nombre}' no aplica al cuadro {cuadro.Id}.");

            foreach (var (nombre, valor) in recibidos)
                if (string.IsNullOrWhiteSpace(valor) && requeridos.Contains(nombre))
                    throw CustomException.Validacion($"El cuadro {cuadro.Id} requiere el parámetro '{nombre}'.");
        }

        private static List<FilaCuadroDTO> ArmarFilas(string cuadroId, string contexto, IReadOnlyList<FilaDatos> filas, string[] periodos, bool esArbol)
        {
            var alineador = new Alineador(periodos);
            var padres = esArbol ? CalcularPadres(filas) : null;
            var repeticiones = new Dictionary<string, int>(StringComparer.Ordinal);
            var salida = new List<FilaCuadroDTO>(filas.Count);

            for (var i = 0; i < filas.Count; i++)
            {
                var f = filas[i];

                // Clave estable: {cuadro}|{contexto}|{cuc}, o {cuadro}|{contexto}|{grupo}|{variable} sin CUC;
                // si se repite se agrega #n.
                var clave = f.Cuc is not null
                    ? $"{cuadroId}|{contexto}|{f.Cuc}"
                    : $"{cuadroId}|{contexto}|{f.Grupo}|{f.Variable}";
                var n = repeticiones[clave] = repeticiones.GetValueOrDefault(clave) + 1;
                if (n > 1)
                    clave += $"#{n}";

                salida.Add(new FilaCuadroDTO(
                    i,
                    clave,
                    f.Grupo,
                    f.Variable,
                    f.Cuc,
                    esArbol ? f.CodigoBase : null,
                    esArbol ? f.Nivel ?? 0 : f.NivelJerarquia,
                    padres?[i],
                    alineador.Alinear(f.Periodos, f.Valores)));
            }
            return salida;
        }

        /// <summary>
        /// Algoritmo de pila del frontend (<c>TablaBalances.tsx</c>): el padre es la última fila
        /// anterior de nivel menor cuyo <c>codigoBase</c> es prefijo del de la fila y distinto de él.
        /// </summary>
        internal static int?[] CalcularPadres(IReadOnlyList<FilaDatos> filas)
        {
            var padres = new int?[filas.Count];
            var pila = new List<int>();
            for (var i = 0; i < filas.Count; i++)
            {
                var codigo = filas[i].CodigoBase ?? string.Empty;
                var nivel = filas[i].Nivel ?? 0;
                while (pila.Count > 0)
                {
                    var tope = filas[pila[^1]];
                    var codigoTope = tope.CodigoBase ?? string.Empty;
                    if ((tope.Nivel ?? 0) < nivel && codigo.StartsWith(codigoTope, StringComparison.Ordinal) && codigo != codigoTope)
                        break;
                    pila.RemoveAt(pila.Count - 1);
                }
                padres[i] = pila.Count > 0 ? pila[^1] : null;
                pila.Add(i);
            }
            return padres;
        }
    }
}
