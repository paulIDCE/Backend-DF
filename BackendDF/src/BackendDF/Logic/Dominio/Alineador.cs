namespace BackendDF.Logic.Dominio
{
    /// <summary>
    /// Proyecta los valores de cada fila (alineados con su propio eje de períodos) sobre el eje
    /// de la respuesta: <c>valores</c> queda alineado posición a posición con <c>periodos</c>
    /// (contrato §4.3). Los mapeos se calculan una vez por eje distinto.
    /// </summary>
    public sealed class Alineador
    {
        private readonly string[] _destino;
        private readonly Dictionary<string[], int[]> _mapas = new(ReferenceEqualityComparer.Instance);

        public Alineador(string[] destino)
        {
            _destino = destino;
        }

        public double?[] Alinear(string[] periodosOrigen, double[] valores)
        {
            if (!_mapas.TryGetValue(periodosOrigen, out var mapa))
            {
                mapa = new int[_destino.Length];
                for (var i = 0; i < _destino.Length; i++)
                    mapa[i] = Array.BinarySearch(periodosOrigen, _destino[i], StringComparer.Ordinal);
                _mapas[periodosOrigen] = mapa;
            }

            var salida = new double?[mapa.Length];
            for (var i = 0; i < mapa.Length; i++)
            {
                var j = mapa[i];
                salida[i] = j >= 0 && !double.IsNaN(valores[j]) ? valores[j] : null;
            }
            return salida;
        }
    }
}
