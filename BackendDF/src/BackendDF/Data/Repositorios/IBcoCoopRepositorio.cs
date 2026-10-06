namespace BackendDF.Data.Repositorios
{
    /// <summary>
    /// Acceso a <c>idce_bco_coop</c> a través de los procedimientos del esquema <c>api</c>
    /// (<c>db/fase0/03_procedimientos.sql</c>): un método por procedimiento, sin lógica de negocio.
    /// Las cuentas se pasan como texto ("1101"); los montos vuelven en USD.
    /// </summary>
    public interface IBcoCoopRepositorio
    {
        /// <summary><c>api.ObtenerVentana</c></summary>
        Task<VentanaBd> ObtenerVentanaAsync(CancellationToken ct);

        /// <summary><c>api.ListarIfi</c></summary>
        Task<IReadOnlyList<IfiBd>> ListarIfiAsync(CancellationToken ct);

        /// <summary><c>api.ObtenerSaldosEntidad</c>. <paramref name="cuentas"/> null = todas.</summary>
        Task<SeriesBd> ObtenerSaldosEntidadAsync(int ifiId, IEnumerable<string>? cuentas, CancellationToken ct);

        /// <summary><c>api.ObtenerSaldosEntidades</c>: series por IFIID (las entidades sin filas no aparecen).</summary>
        Task<IReadOnlyDictionary<int, SeriesBd>> ObtenerSaldosEntidadesAsync(IEnumerable<int> ifiIds, IEnumerable<string>? cuentas, CancellationToken ct);

        /// <summary><c>api.ObtenerSaldosAgregado</c>. <paramref name="segmentos"/> null = todos.</summary>
        Task<SeriesBd> ObtenerSaldosAgregadoAsync(IEnumerable<string> tiposEntidad, IEnumerable<int>? segmentos, IEnumerable<string>? cuentas, CancellationToken ct);

        /// <summary><c>api.ObtenerIndicadoresEntidad</c>. <paramref name="codigos"/> null = todos.</summary>
        Task<SeriesBd> ObtenerIndicadoresEntidadAsync(int ifiId, IEnumerable<string>? codigos, CancellationToken ct);
    }
}
