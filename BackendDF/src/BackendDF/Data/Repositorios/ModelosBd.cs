namespace BackendDF.Data.Repositorios
{
    /// <summary>Resultado de <c>api.ObtenerVentana</c>: rango de meses de la BD y firma de la carga.</summary>
    /// <param name="Desde">Primer mes con datos (<c>yyyy-MM</c>).</param>
    /// <param name="Hasta">Último mes con datos (<c>yyyy-MM</c>).</param>
    /// <param name="Firma">Cambia con cada recarga del ETL (mes nuevo, filas o saldos distintos).</param>
    public sealed record VentanaBd(string Desde, string Hasta, int Meses, int MesesConDatos, long FilasB11, string Firma);

    /// <summary>Fila de <c>api.ListarIfi</c>.</summary>
    public sealed record IfiBd(int IfiId, string Ruc, string? Nombre, string? TipoEntidad, int? Segmento);

    /// <summary>Fila de <c>api.ListarCuadros</c>: un cuadro del catálogo (Fase 2).</summary>
    /// <param name="Ambito">"ENTIDAD" | "SECTOR".</param>
    /// <param name="FechasJson">true = no sale del .sav: conserva las fechas del JSON (convención C1).</param>
    /// <param name="Variantes">Variantes de plantilla del cuadro ('*', 'BP', 'COOP:2'…).</param>
    /// <param name="FilasBd">Filas con fórmula (SUMA/RATIO), todas las variantes; null si la BD no lo informa (antes de db/fase3/08).</param>
    /// <param name="FilasJson">Filas pendientes (Calculo = JSON); ídem.</param>
    public sealed record CuadroBd(string Id, string Titulo, string? Unidad, string Ambito, string Tipo, string Frecuencia,
        bool FechasJson, int Orden, string[] Variantes, int? FilasBd = null, int? FilasJson = null)
    {
        public bool EsDeEntidad => string.Equals(Ambito, "ENTIDAD", StringComparison.OrdinalIgnoreCase);

        /// <summary>"bd" | "json" | "mixto" según sus filas; null si la BD no informa los conteos.</summary>
        public string? Origen => (FilasBd, FilasJson) switch
        {
            (null, _) or (_, null) => null,
            (_, 0) => Logic.Dominio.OrigenDatos.Bd,
            (0, _) => Logic.Dominio.OrigenDatos.Json,
            _ => Logic.Dominio.OrigenDatos.Mixto
        };

        /// <summary>Variante a usar: exacta, luego solo tipos (antes de ':'), luego '*' (igual que <c>api.fnResolverVariante</c>).</summary>
        public string ResolverVariante(string? clave)
        {
            if (clave is not null && Variantes.Contains(clave, StringComparer.Ordinal))
                return clave;
            var tipos = clave?.Split(':')[0];
            return tipos is not null && Variantes.Contains(tipos, StringComparer.Ordinal) ? tipos : "*";
        }
    }

    /// <summary>Fila de una plantilla del catálogo (<c>api.ObtenerPlantilla</c>), con su texto ya resuelto.</summary>
    /// <param name="Calculo">"SUMA" | "RATIO" (valor calculado por el SP) | "JSON" (valor pendiente: lo aporta el JSON).</param>
    /// <param name="OperacionN">Operación en el tiempo sobre ΣN (<c>db/fase3/03_operaciones_tiempo.sql</c>); "NIVEL" = ninguna.</param>
    /// <param name="OperacionD">Ídem sobre ΣD (solo RATIO).</param>
    public sealed record FilaCatalogoBd(int FilaId, string Variante, string IdSub, int Orden, string? Codigo, string? CodigoBase,
        string? Grupo, string? Variable, int Nivel, string? SegmentoCredito, string? Unidad, string Calculo,
        string OperacionN = OperacionTiempo.Nivel, string OperacionD = OperacionTiempo.Nivel)
    {
        public bool EsCalculada => Calculo is "SUMA" or "RATIO";

        /// <summary>Clave de los valores en <see cref="CuadroCalculadoBd.Valores"/>.</summary>
        public string ClaveValor => FilaId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Último mes que queda sin valor porque la BD no tiene los meses anteriores que pide la
        /// operación, si la BD empieza en <paramref name="desde"/>; null si la fila no los necesita.
        /// </summary>
        public string? SinHistoriaHasta(string desde)
        {
            if (!EsCalculada)
                return null;
            var n = OperacionTiempo.SinHistoriaHasta(OperacionN, desde);
            var d = Calculo == "RATIO" ? OperacionTiempo.SinHistoriaHasta(OperacionD, desde) : null;
            return string.CompareOrdinal(n, d) >= 0 ? n : d;
        }
    }

    /// <summary>Operaciones en el tiempo del catálogo (columnas <c>OperacionN</c> / <c>OperacionD</c>).</summary>
    public static class OperacionTiempo
    {
        public const string Nivel = "NIVEL";

        /// <summary>
        /// Último mes sin valor por falta de historia si la BD empieza en <paramref name="desde"/> (<c>yyyy-MM</c>):
        /// t−1 → el primer mes; t−12 → los primeros 12; promedio desde diciembre del año anterior → hasta
        /// diciembre del primer año (en ROA, diciembre igual tiene valor: la marca solo explica los vacíos).
        /// </summary>
        public static string? SinHistoriaHasta(string? operacion, string desde) => operacion switch
        {
            "VARIACION_1" or "RETARDO_1" => desde,
            "VARIACION_12" or "RETARDO_12" => Logic.Dominio.Periodos.RestarMeses(desde, -11),
            "PROMEDIO_DIC" or "PROMEDIO_DIC_ROA" or "BASE_ROE_DIC" => desde[..4] + "-12",
            _ => null   // NIVEL, ANUALIZADO: no usan meses anteriores
        };
    }

    /// <summary>
    /// Resultado de <c>api.ObtenerCuadroEntidad</c> / <c>api.ObtenerCuadroSector</c>: la plantilla en
    /// orden y los valores de las filas calculadas (por <see cref="FilaCatalogoBd.ClaveValor"/>), ya
    /// escalados a las unidades publicadas.
    /// </summary>
    public sealed record CuadroCalculadoBd(IReadOnlyList<FilaCatalogoBd> Filas, SeriesBd Valores);

    /// <summary>Plantilla de un cuadro para una variante (para la carga masiva de REP01).</summary>
    public sealed record PlantillaBd(IReadOnlyList<FilaCatalogoBd> Filas);

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
