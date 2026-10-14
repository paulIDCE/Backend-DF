using BackendDF.Data.Repositorios;
using BackendDF.Logic.Dominio;
using BackendDF.Models.Entities;

namespace BackendDF.Data.FuenteHibrida
{
    /// <summary>
    /// Arma las filas de un cuadro (o el REP01 de una entidad) desde el catálogo de la BD
    /// (Fase 2, <c>db/fase2</c>): el orden, el texto, el encabezado y el nivel salen del catálogo;
    /// el valor de las filas calculadas (SUMA/RATIO) sale del SP; el de las filas pendientes
    /// (Calculo = JSON) sale de la fila equivalente del JSON durante la transición. Cada fila lleva su
    /// <see cref="FilaDatos.Origen"/>: "bd", "json" o null (pendiente sin fila en el JSON).
    /// </summary>
    internal static class ArmadorCatalogo
    {
        /// <param name="ejeVentana">Eje de la ventana de la BD (C1); null = conservar el eje del JSON.</param>
        public static List<FilaDatos> Filas(CuadroBd cuadro, CuadroCalculadoBd calculado, IReadOnlyList<FilaDatos> json,
            string[]? ejeVentana, string? filtro)
        {
            var eje = ejeVentana ?? Periodos.Union(json.Select(f => f.Periodos));
            var desdeBd = ejeVentana is { Length: > 0 } ? ejeVentana[0] : null;
            var arbol = cuadro.Tipo is "Balances" or "BalancesEntidad";
            var proyector = new Proyector(eje);
            var pendientes = IndiceJson(json);
            var salida = new List<FilaDatos>(calculado.Filas.Count);

            foreach (var fila in calculado.Filas)
            {
                double[] valores;
                string? origen;
                if (fila.EsCalculada)
                    (valores, origen) = (proyector.Serie(calculado.Valores, fila.ClaveValor, 1), OrigenDatos.Bd);
                else if (Tomar(pendientes, Clave(fila.IdSub, fila.Codigo, fila.CodigoBase, fila.Grupo, fila.Variable)) is { } j)
                    (valores, origen) = (proyector.Json(j.Periodos, j.Valores), OrigenDatos.Json);
                else
                    (valores, origen) = (proyector.Vacio(), null);

                salida.Add(new FilaDatos
                {
                    Cuadro = cuadro.Id,
                    Filtro = filtro,
                    Cuc = fila.Codigo,
                    Id = fila.IdSub.Length == 0 ? null : fila.IdSub,
                    Titulo = cuadro.Titulo,
                    Unidad = fila.Unidad,
                    Grupo = fila.Grupo,
                    Variable = fila.Variable,
                    Nivel = arbol ? fila.Nivel : null,
                    NivelJerarquia = arbol ? 0 : fila.Nivel,
                    CodigoBase = fila.CodigoBase,
                    SegmentoCredito = fila.SegmentoCredito,
                    Periodos = eje,
                    Valores = valores,
                    SinHistoriaHasta = desdeBd is null ? null : fila.SinHistoriaHasta(desdeBd),
                    Origen = origen
                });
            }
            return salida;
        }

        /// <summary>REP01 de una entidad: cuentas en el orden del catálogo, con su código y texto.</summary>
        public static ReporteEntidad Reporte(string entidadId, IReadOnlyList<FilaCatalogoBd> plantilla, SeriesBd valores,
            ReporteEntidad? json, string[] eje)
        {
            var proyector = new Proyector(eje);
            var porCuc = new Dictionary<string, CuentaReporte>(StringComparer.Ordinal);
            if (json is not null)
                foreach (var c in json.Cuentas)
                    porCuc.TryAdd(c.Cuc, c);

            var cuentas = new List<CuentaReporte>(plantilla.Count);
            foreach (var fila in plantilla)
            {
                if (fila.Codigo is null)
                    continue;
                double[] serie;
                string? origen;
                if (fila.EsCalculada)
                    (serie, origen) = (proyector.Serie(valores, fila.ClaveValor, 1), OrigenDatos.Bd);
                else if (json is not null && porCuc.TryGetValue(fila.Codigo, out var c))
                    (serie, origen) = (proyector.Json(json.Periodos, c.Valores), OrigenDatos.Json);
                else
                    (serie, origen) = (proyector.Vacio(), null);
                cuentas.Add(new CuentaReporte(fila.Codigo, fila.Variable, serie, eje.Length > 0 ? fila.SinHistoriaHasta(eje[0]) : null, origen));
            }
            return new ReporteEntidad(entidadId, eje, cuentas);
        }

        // ---- Emparejamiento de filas pendientes con el JSON ----

        /// <summary>
        /// Clave de emparejamiento: sub-vista + código publicado; en los balances (sin código) el
        /// código de cuenta; si no hay ninguno, encabezado + texto.
        /// </summary>
        private static string Clave(string? idSub, string? codigo, string? codigoBase, string? grupo, string? variable)
        {
            var sub = idSub ?? string.Empty;
            if (codigo is not null)
                return $"{sub}\u001f{codigo}";
            if (codigoBase is not null)
                return $"{sub}\u001f#{codigoBase.Trim()}";
            return $"{sub}\u001f|{grupo}\u001f{variable}";
        }

        /// <summary>Filas JSON por clave; si una clave se repite, se consumen en orden.</summary>
        private static Dictionary<string, Queue<FilaDatos>> IndiceJson(IReadOnlyList<FilaDatos> json)
        {
            var indice = new Dictionary<string, Queue<FilaDatos>>(StringComparer.Ordinal);
            foreach (var f in json)
            {
                var clave = Clave(f.Id, f.Cuc, f.CodigoBase, f.Grupo, f.Variable);
                if (!indice.TryGetValue(clave, out var cola))
                    indice[clave] = cola = new Queue<FilaDatos>();
                cola.Enqueue(f);
            }
            return indice;
        }

        private static FilaDatos? Tomar(Dictionary<string, Queue<FilaDatos>> indice, string clave) =>
            indice.TryGetValue(clave, out var cola) && cola.Count > 0 ? cola.Dequeue() : null;
    }
}
