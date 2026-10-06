using System.Text.Json;
using BackendDF.Data.Repositorios;

namespace BackendDF.Data.FuenteSql
{
    /// <summary>Una entrada de <c>mapa-entidades.json</c>.</summary>
    public sealed record EntradaMapaEntidad(string Archivo, int IfiId, string Ruc, string? Nombre);

    /// <summary>
    /// Mapa del id del backend (el <c>archivo</c> del catálogo, p. ej. <c>BP__PICHINCHA</c>) a la
    /// entidad de <c>idce_bco_coop.dbo.Ifi</c>. Vive en el backend: la BD no guarda nada derivado
    /// de los JSON (informe §7). La clave estable es el RUC; el IFIID se valida contra la BD.
    /// </summary>
    public sealed class MapaEntidades
    {
        private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

        public MapaEntidades(IReadOnlyList<EntradaMapaEntidad> entradas)
        {
            Entradas = entradas;
        }

        public IReadOnlyList<EntradaMapaEntidad> Entradas { get; }

        public static MapaEntidades Cargar(string ruta)
        {
            if (!File.Exists(ruta))
                throw new FileNotFoundException($"No existe el mapa de entidades (Datos:Sql:RutaMapaEntidades): {ruta}", ruta);

            using var stream = File.OpenRead(ruta);
            var archivo = JsonSerializer.Deserialize<ArchivoMapa>(stream, Opciones);
            return new MapaEntidades(archivo?.Entidades ?? []);
        }

        /// <summary>
        /// Cruza el mapa con las entidades de la BD: si el IFIID existe con el mismo RUC se usa;
        /// si no, se busca por RUC (con advertencia); si el RUC no está en la BD, la entidad queda
        /// sin mapeo y se sirve solo desde JSON.
        /// </summary>
        public (IReadOnlyDictionary<string, int> Ids, IReadOnlyList<string> Advertencias) Resolver(IReadOnlyList<IfiBd> ifis)
        {
            var porId = ifis.ToDictionary(i => i.IfiId);
            var porRuc = ifis.GroupBy(i => i.Ruc, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var advertencias = new List<string>();

            foreach (var e in Entradas)
            {
                if (porId.TryGetValue(e.IfiId, out var ifi) && string.Equals(ifi.Ruc, e.Ruc, StringComparison.Ordinal))
                    ids[e.Archivo] = e.IfiId;
                else if (porRuc.TryGetValue(e.Ruc, out ifi))
                {
                    ids[e.Archivo] = ifi.IfiId;
                    advertencias.Add($"Mapa de entidades: {e.Archivo} apunta a IFIID {e.IfiId}, pero su RUC {e.Ruc} es el IFIID {ifi.IfiId} en la BD (se usa este).");
                }
                else
                    advertencias.Add($"Mapa de entidades: {e.Archivo} (RUC {e.Ruc}) no existe en la BD; se sirve solo desde JSON.");
            }
            return (ids, advertencias);
        }

        private sealed record ArchivoMapa(List<EntradaMapaEntidad>? Entidades);
    }
}
