namespace BackendDF.Common.Utils
{
    /// <summary>
    /// Caché LRU de capacidad fija y segura entre hilos. Guarda la <b>tarea</b> de carga, así
    /// dos requests que piden la misma entidad a la vez comparten una sola lectura del disco.
    /// Una carga fallida se quita de la caché para que el siguiente intento la repita.
    /// </summary>
    public sealed class CacheLru<TValor> where TValor : class
    {
        private readonly int _capacidad;
        private readonly Dictionary<string, LinkedListNode<(string Clave, Lazy<Task<TValor>> Valor)>> _mapa;
        private readonly LinkedList<(string Clave, Lazy<Task<TValor>> Valor)> _orden = new();
        private readonly object _lock = new();

        public CacheLru(int capacidad, IEqualityComparer<string>? comparer = null)
        {
            _capacidad = Math.Max(1, capacidad);
            _mapa = new Dictionary<string, LinkedListNode<(string, Lazy<Task<TValor>>)>>(comparer ?? StringComparer.Ordinal);
        }

        public int Cantidad
        {
            get { lock (_lock) return _mapa.Count; }
        }

        public async Task<TValor> ObtenerAsync(string clave, Func<Task<TValor>> cargar)
        {
            Lazy<Task<TValor>> lazy;
            lock (_lock)
            {
                if (_mapa.TryGetValue(clave, out var nodo))
                {
                    _orden.Remove(nodo);
                    _orden.AddFirst(nodo);
                    lazy = nodo.Value.Valor;
                }
                else
                {
                    lazy = new Lazy<Task<TValor>>(cargar, LazyThreadSafetyMode.ExecutionAndPublication);
                    _mapa[clave] = _orden.AddFirst((clave, lazy));
                    while (_mapa.Count > _capacidad)
                    {
                        var ultimo = _orden.Last!;
                        _orden.RemoveLast();
                        _mapa.Remove(ultimo.Value.Clave);
                    }
                }
            }

            try
            {
                return await lazy.Value;
            }
            catch
            {
                lock (_lock)
                {
                    if (_mapa.TryGetValue(clave, out var nodo) && ReferenceEquals(nodo.Value.Valor, lazy))
                    {
                        _orden.Remove(nodo);
                        _mapa.Remove(clave);
                    }
                }
                throw;
            }
        }
    }
}
