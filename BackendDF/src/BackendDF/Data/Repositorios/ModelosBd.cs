namespace BackendDF.Data.Repositorios
{
    /// <summary>Resultado de <c>api.ObtenerVentana</c>: rango de meses de la BD y firma de la carga.</summary>
    /// <param name="Desde">Primer mes con datos (<c>yyyy-MM</c>).</param>
    /// <param name="Hasta">Último mes con datos (<c>yyyy-MM</c>).</param>
    /// <param name="Firma">Cambia con cada recarga del ETL (mes nuevo, filas o saldos distintos).</param>
    public sealed record VentanaBd(string Desde, string Hasta, int Meses, int MesesConDatos, long FilasB11, string Firma);

    /// <summary>Fila de <c>api.ListarIfi</c>.</summary>
    public sealed record IfiBd(int IfiId, string Ruc, string? Nombre, string? TipoEntidad, int? Segmento);

    /// <summary>
    /// Series por código (cuenta o indicador) sobre un eje ordenado de meses. Los valores van en
    /// las unidades de la BD (USD, fracciones); <see cref="double.NaN"/> = sin fila en la BD.
    /// </summary>
    public sealed class SeriesBd
    {
        public static readonly SeriesBd Vacia = new([], new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase));

        private readonly Dictionary<string, double[]> _valores;

        public SeriesBd(string[] periodos, Dictionary<string, double[]> valores)
        {
            Periodos = periodos;
            _valores = valores;
        }

        /// <summary>Eje de meses ordenado (orden ordinal = cronológico).</summary>
        public string[] Periodos { get; }

        public int Cantidad => _valores.Count;

        public bool TryObtener(string codigo, out double[] valores) => _valores.TryGetValue(codigo, out valores!);

        /// <summary>Arma las series a partir de filas (código, período, valor) en cualquier orden.</summary>
        public static SeriesBd Construir(IReadOnlyCollection<(string Codigo, string Periodo, double Valor)> filas)
        {
            if (filas.Count == 0)
                return Vacia;

            var periodos = filas.Select(f => f.Periodo).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var indice = new Dictionary<string, int>(periodos.Length, StringComparer.Ordinal);
            for (var i = 0; i < periodos.Length; i++)
                indice[periodos[i]] = i;

            var valores = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var (codigo, periodo, valor) in filas)
            {
                if (!valores.TryGetValue(codigo, out var serie))
                {
                    serie = new double[periodos.Length];
                    Array.Fill(serie, double.NaN);
                    valores[codigo] = serie;
                }
                serie[indice[periodo]] = valor;
            }
            return new SeriesBd(periodos, valores);
        }
    }
}
