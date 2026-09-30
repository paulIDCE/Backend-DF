namespace BackendDF.Models.Entities
{
    /// <summary>
    /// Una fila de cualquier archivo de datos (tipos A–F del contrato §3): metadatos + valores
    /// por período. Los valores están alineados con <see cref="Periodos"/>, que viene ordenado y
    /// es un arreglo compartido entre las filas que tienen las mismas claves de período.
    /// <see cref="double.NaN"/> representa "sin valor" (null, "", "-" o texto no numérico).
    /// </summary>
    public sealed class FilaDatos
    {
        public string Cuadro { get; init; } = string.Empty;
        public string? Filtro { get; init; }
        public string? Cuc { get; init; }

        /// <summary>Columna <c>ID</c>: tipo de análisis (balances) o tipo de crédito (cartera).</summary>
        public string? Id { get; init; }

        public string? Titulo { get; init; }
        public string? Unidad { get; init; }
        public string? Grupo { get; init; }
        public string? Variable { get; init; }

        /// <summary>Columna <c>Nivel</c> del origen (balances, 1–4).</summary>
        public int? Nivel { get; init; }

        /// <summary>Índice del último <c>Nivel1..Nivel7</c> con texto; 0 si ninguno.</summary>
        public int NivelJerarquia { get; init; }

        /// <summary>Columna <c>Codigo_Base</c> (solo balances).</summary>
        public string? CodigoBase { get; init; }

        public string? SegmentoCredito { get; init; }

        /// <summary>Metadatos de entidad (solo reportes, R6).</summary>
        public string? Tamano { get; init; }
        public string? RangoActivos { get; init; }
        public string? DpaPr { get; init; }

        public string[] Periodos { get; init; } = [];
        public double[] Valores { get; init; } = [];
    }
}
