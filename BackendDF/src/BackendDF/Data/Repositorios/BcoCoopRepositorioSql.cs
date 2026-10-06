using System.Data;
using Microsoft.Data.SqlClient;

namespace BackendDF.Data.Repositorios
{
    /// <summary>
    /// <see cref="IBcoCoopRepositorio"/> con ADO.NET (Microsoft.Data.SqlClient). Cada llamada abre
    /// su conexión (el pool de SqlClient las reutiliza). Las fallas se propagan: la decisión de
    /// caer a los JSON la toma <see cref="FuenteHibrida.FuenteDatosHibrida"/>.
    /// </summary>
    public sealed class BcoCoopRepositorioSql : IBcoCoopRepositorio
    {
        private readonly string _cadena;
        private readonly int _timeoutSegundos;

        public BcoCoopRepositorioSql(string cadenaConexion, int timeoutSegundos)
        {
            _cadena = cadenaConexion;
            _timeoutSegundos = timeoutSegundos;
        }

        public async Task<VentanaBd> ObtenerVentanaAsync(CancellationToken ct)
        {
            VentanaBd? ventana = null;
            await EjecutarAsync("api.ObtenerVentana", null, r => ventana = new VentanaBd(
                r.GetString(r.GetOrdinal("Desde")).Trim(),
                r.GetString(r.GetOrdinal("Hasta")).Trim(),
                Convert.ToInt32(r["Meses"]),
                Convert.ToInt32(r["MesesConDatos"]),
                Convert.ToInt64(r["FilasB11"]),
                Convert.ToString(r["Firma"]) ?? string.Empty), ct);
            return ventana ?? throw new InvalidOperationException("api.ObtenerVentana no devolvió filas (¿B11 vacía?).");
        }

        public async Task<IReadOnlyList<IfiBd>> ListarIfiAsync(CancellationToken ct)
        {
            var lista = new List<IfiBd>();
            await EjecutarAsync("api.ListarIfi", null, r => lista.Add(new IfiBd(
                r.GetInt32(0),
                r.GetString(1).Trim(),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetInt16(4))), ct);
            return lista;
        }

        public async Task<SeriesBd> ObtenerSaldosEntidadAsync(int ifiId, IEnumerable<string>? cuentas, CancellationToken ct)
        {
            var filas = new List<(string, string, double)>();
            await EjecutarAsync("api.ObtenerSaldosEntidad", p =>
            {
                p.Add("@IFIID", SqlDbType.Int).Value = ifiId;
                AgregarLista(p, "@Cuentas", cuentas);
            }, r => filas.Add((r.GetString(0), r.GetString(1), Monto(r, 2))), ct);
            return SeriesBd.Construir(filas);
        }

        public async Task<IReadOnlyDictionary<int, SeriesBd>> ObtenerSaldosEntidadesAsync(IEnumerable<int> ifiIds, IEnumerable<string>? cuentas, CancellationToken ct)
        {
            var ids = ifiIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, SeriesBd>();

            var porEntidad = new Dictionary<int, List<(string, string, double)>>();
            await EjecutarAsync("api.ObtenerSaldosEntidades", p =>
            {
                AgregarLista(p, "@IFIIDs", ids.Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                AgregarLista(p, "@Cuentas", cuentas);
            }, r =>
            {
                var id = r.GetInt32(0);
                if (!porEntidad.TryGetValue(id, out var filas))
                    porEntidad[id] = filas = [];
                filas.Add((r.GetString(1), r.GetString(2), Monto(r, 3)));
            }, ct);

            return porEntidad.ToDictionary(e => e.Key, e => SeriesBd.Construir(e.Value));
        }

        public async Task<SeriesBd> ObtenerSaldosAgregadoAsync(IEnumerable<string> tiposEntidad, IEnumerable<int>? segmentos, IEnumerable<string>? cuentas, CancellationToken ct)
        {
            var filas = new List<(string, string, double)>();
            await EjecutarAsync("api.ObtenerSaldosAgregado", p =>
            {
                AgregarLista(p, "@TiposEntidad", tiposEntidad, 100);
                AgregarLista(p, "@Segmentos", segmentos?.Select(s => s.ToString(System.Globalization.CultureInfo.InvariantCulture)), 100);
                AgregarLista(p, "@Cuentas", cuentas);
            }, r => filas.Add((r.GetString(0), r.GetString(1), Monto(r, 2))), ct);
            return SeriesBd.Construir(filas);
        }

        public async Task<SeriesBd> ObtenerIndicadoresEntidadAsync(int ifiId, IEnumerable<string>? codigos, CancellationToken ct)
        {
            var filas = new List<(string, string, double)>();
            await EjecutarAsync("api.ObtenerIndicadoresEntidad", p =>
            {
                p.Add("@IFIID", SqlDbType.Int).Value = ifiId;
                AgregarLista(p, "@Codigos", codigos);
            }, r => filas.Add((r.GetString(0), r.GetString(1), r.IsDBNull(2) ? double.NaN : r.GetDouble(2))), ct);
            return SeriesBd.Construir(filas);
        }

        private async Task EjecutarAsync(string procedimiento, Action<SqlParameterCollection>? parametros, Action<SqlDataReader> leerFila, CancellationToken ct)
        {
            await using var conexion = new SqlConnection(_cadena);
            await conexion.OpenAsync(ct);
            await using var comando = new SqlCommand(procedimiento, conexion)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _timeoutSegundos
            };
            parametros?.Invoke(comando.Parameters);

            await using var lector = await comando.ExecuteReaderAsync(ct);
            while (await lector.ReadAsync(ct))
                leerFila(lector);
        }

        /// <summary>Lista como texto separado por comas; null (sin filtro) si no se indica.</summary>
        private static void AgregarLista(SqlParameterCollection p, string nombre, IEnumerable<string>? valores, int largo = -1)
        {
            var texto = valores is null ? null : string.Join(',', valores);
            p.Add(nombre, SqlDbType.VarChar, largo).Value = (object?)texto ?? DBNull.Value;
        }

        /// <summary>money → double; NULL → NaN (sin dato, nunca 0).</summary>
        private static double Monto(SqlDataReader r, int columna) =>
            r.IsDBNull(columna) ? double.NaN : (double)r.GetDecimal(columna);
    }
}
