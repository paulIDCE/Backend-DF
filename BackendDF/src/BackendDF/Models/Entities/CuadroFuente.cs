namespace BackendDF.Models.Entities
{
    /// <summary>Cómo se resuelve un cuadro (contrato §6.2).</summary>
    public enum TipoCuadro
    {
        Macro,
        Sistema,
        Balances,
        Cartera,
        Entidad,
        BalancesEntidad,
        CarteraEntidad
    }

    /// <summary>Metadatos de un cuadro disponible en la fuente.</summary>
    public sealed class CuadroFuente
    {
        public string Id { get; init; } = string.Empty;
        public string Titulo { get; init; } = string.Empty;
        public string? Unidad { get; init; }

        /// <summary>"macro" | "sistema" | "entidad".</summary>
        public string Origen { get; init; } = string.Empty;

        public TipoCuadro Tipo { get; init; }

        /// <summary>"mensual" | "trimestral" | "anual".</summary>
        public string Frecuencia { get; init; } = "mensual";

        /// <summary>
        /// Rango de períodos del cuadro. En los cuadros por entidad es el de la entidad de
        /// referencia (la primera del catálogo): cada entidad puede cubrir un rango distinto.
        /// </summary>
        public string? PeriodoDesde { get; init; }
        public string? PeriodoHasta { get; init; }
    }

    /// <summary>Filtros con que se piden las filas de un cuadro a la fuente.</summary>
    /// <param name="Filtro">Texto exacto de la columna <c>Filtro</c> (nombre del sector).</param>
    /// <param name="EntidadId">Id de catálogo de la entidad.</param>
    /// <param name="Id">Texto exacto de la columna <c>ID</c> (análisis o tipo de crédito).</param>
    public sealed record ConsultaFilas(TipoCuadro Tipo, string Cuadro, string? Filtro, string? EntidadId, string? Id);
}
