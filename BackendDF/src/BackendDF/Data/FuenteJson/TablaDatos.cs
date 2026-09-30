using BackendDF.Models.Entities;

namespace BackendDF.Data.FuenteJson
{
    /// <summary>
    /// Un archivo de datos ya parseado e indexado por <c>Cuadro</c>, <c>Filtro</c> e <c>ID</c>.
    /// Los índices conservan el orden del archivo (R4).
    /// </summary>
    internal sealed class TablaDatos
    {
        private static readonly IReadOnlyList<FilaDatos> Vacio = [];

        private readonly Dictionary<(string Cuadro, string? Filtro, string? Id), List<FilaDatos>> _porClave = new();
        private readonly Dictionary<string, List<FilaDatos>> _porFiltro = new(StringComparer.Ordinal);

        /// <param name="indexarFiltro">false en los archivos por entidad, donde Filtro es el nombre de la entidad.</param>
        public TablaDatos(string archivo, List<FilaDatos> filas, bool indexarFiltro, DateTime modificado)
        {
            Archivo = archivo;
            Filas = filas;
            Modificado = modificado;
            Periodos = Logic.Dominio.Periodos.Union(filas.Select(f => f.Periodos));

            foreach (var fila in filas)
            {
                Agregar(PorCuadro, fila.Cuadro, fila);
                Agregar(_porClave, (fila.Cuadro, indexarFiltro ? fila.Filtro : null, fila.Id), fila);
                if (indexarFiltro && fila.Filtro is not null)
                    Agregar(_porFiltro, fila.Filtro, fila);
            }
        }

        public string Archivo { get; }
        public IReadOnlyList<FilaDatos> Filas { get; }
        public DateTime Modificado { get; }

        /// <summary>Unión ordenada de los períodos del archivo.</summary>
        public string[] Periodos { get; }

        public Dictionary<string, List<FilaDatos>> PorCuadro { get; } = new(StringComparer.Ordinal);

        public IReadOnlyList<FilaDatos> Buscar(string cuadro, string? filtro, string? id) =>
            _porClave.TryGetValue((cuadro, filtro, id), out var filas) ? filas : Vacio;

        public IReadOnlyList<FilaDatos> BuscarPorFiltro(string filtro) =>
            _porFiltro.TryGetValue(filtro, out var filas) ? filas : Vacio;

        private static void Agregar<TClave>(Dictionary<TClave, List<FilaDatos>> indice, TClave clave, FilaDatos fila)
            where TClave : notnull
        {
            if (!indice.TryGetValue(clave, out var lista))
                indice[clave] = lista = [];
            lista.Add(fila);
        }
    }
}
