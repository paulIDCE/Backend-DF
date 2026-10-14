using BackendDF.Data.Repositorios;

namespace BackendDF.Data.FuenteHibrida
{
    /// <summary>
    /// Proyecta series ordenadas por período sobre un eje de destino (la ventana de la BD o el
    /// eje JSON). El mapeo se calcula una vez por eje de origen. Período ausente → NaN.
    /// </summary>
    internal sealed class Proyector
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
                return Vacio();
            return Aplicar(series.Periodos, valores, escala);
        }

        public double[] Vacio()
        {
            var vacio = new double[_eje.Length];
            Array.Fill(vacio, double.NaN);
            return vacio;
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
