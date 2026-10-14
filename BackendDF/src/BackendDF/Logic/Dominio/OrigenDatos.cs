namespace BackendDF.Logic.Dominio
{
    /// <summary>
    /// De dónde salen los valores de una fila o un cuadro: la BD (<c>idce_bco_coop</c>: cuentas de
    /// <c>B11</c>, indicadores de <c>IndicadorData</c> o fórmulas del catálogo) o los archivos JSON.
    /// Una fila sale entera de una sola fuente; un cuadro puede mezclar ("mixto").
    /// </summary>
    public static class OrigenDatos
    {
        public const string Bd = "bd";
        public const string Json = "json";
        public const string Mixto = "mixto";

        /// <summary>
        /// Origen del conjunto: "bd" o "json" si todas las filas con fuente coinciden, "mixto" si no,
        /// null si ninguna tiene fuente. Cuenta las filas por origen (null = sin fuente).
        /// </summary>
        public static (string? Origen, int Bd, int Json, int SinFuente) Agregar(IEnumerable<string?> origenes)
        {
            int bd = 0, json = 0, sinFuente = 0;
            foreach (var o in origenes)
            {
                switch (o)
                {
                    case Bd: bd++; break;
                    case Json: json++; break;
                    default: sinFuente++; break;
                }
            }
            var origen = bd > 0 && json > 0 ? Mixto : bd > 0 ? Bd : json > 0 ? Json : null;
            return (origen, bd, json, sinFuente);
        }
    }
}
