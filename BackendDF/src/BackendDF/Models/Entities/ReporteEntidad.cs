namespace BackendDF.Models.Entities
{
    /// <summary>Una cuenta del reporte REP01: valores alineados con <see cref="ReporteEntidad.Periodos"/>.</summary>
    public sealed record CuentaReporte(string Cuc, string? Variable, double[] Valores);

    /// <summary>
    /// Reporte <c>REP01</c> de una entidad (tipo F) en forma compacta: la unión ordenada de sus
    /// períodos y las 776 cuentas en el orden del archivo, con búsqueda por CUC o Variable (R7).
    /// Alimenta el reporte, los rankings y las series entre entidades.
    /// </summary>
    public sealed class ReporteEntidad
    {
        private readonly Dictionary<string, CuentaReporte> _porCuc = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CuentaReporte> _porVariable = new(StringComparer.OrdinalIgnoreCase);

        public ReporteEntidad(string entidadId, string[] periodos, IReadOnlyList<CuentaReporte> cuentas)
        {
            EntidadId = entidadId;
            Periodos = periodos;
            Cuentas = cuentas;
            foreach (var cuenta in cuentas)
            {
                _porCuc.TryAdd(cuenta.Cuc, cuenta);
                if (!string.IsNullOrWhiteSpace(cuenta.Variable))
                    _porVariable.TryAdd(cuenta.Variable.Trim(), cuenta);
            }
        }

        public string EntidadId { get; }
        public string[] Periodos { get; }
        public IReadOnlyList<CuentaReporte> Cuentas { get; }

        /// <summary>R7: busca por CUC y, si no aparece, por Variable, sin distinguir mayúsculas.</summary>
        public CuentaReporte? BuscarCuenta(string codigo)
        {
            var c = codigo.Trim();
            return _porCuc.TryGetValue(c, out var cuenta) || _porVariable.TryGetValue(c, out cuenta) ? cuenta : null;
        }

        /// <summary>Valor de una cuenta en un período; NaN si el período no existe o no hay dato.</summary>
        public double Valor(CuentaReporte cuenta, string periodo)
        {
            var i = Array.BinarySearch(Periodos, periodo, StringComparer.Ordinal);
            return i >= 0 ? cuenta.Valores[i] : double.NaN;
        }
    }
}
