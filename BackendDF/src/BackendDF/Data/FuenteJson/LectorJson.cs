using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using BackendDF.Logic.Dominio;
using BackendDF.Models.Entities;

namespace BackendDF.Data.FuenteJson
{
    /// <summary>
    /// Reutiliza los textos y ejes de períodos repetidos (Titulo, Unidad, Filtro, "2021-05"…)
    /// para que miles de filas compartan la misma instancia en memoria.
    /// </summary>
    internal sealed class PoolTextos
    {
        private readonly ConcurrentDictionary<string, string> _textos = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string[]> _ejes = new(StringComparer.Ordinal);

        public string Texto(string s) => _textos.GetOrAdd(s, s);

        public string[] Eje(string[] periodos) => _ejes.GetOrAdd(string.Join('|', periodos), _ => periodos);
    }

    /// <summary>
    /// Lectura de los JSON "anchos" del origen (array de filas, una clave por período) aplicando
    /// las reglas de lectura del contrato §3.3. Se leen en streaming, fila por fila.
    /// </summary>
    internal static class LectorJson
    {
        private static readonly JsonSerializerOptions OpcionesCatalogo = new() { PropertyNameCaseInsensitive = true };

        private static FileStream Abrir(string ruta) =>
            new(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.Asynchronous | FileOptions.SequentialScan);

        /// <summary>Lee las filas del archivo (todas, o solo las primeras <paramref name="maxFilas"/>).</summary>
        public static async Task<List<FilaDatos>> LeerFilasAsync(string ruta, PoolTextos pool, CancellationToken ct, int maxFilas = int.MaxValue)
        {
            await using var stream = Abrir(ruta);
            var filas = new List<FilaDatos>();
            await foreach (var elemento in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(stream, cancellationToken: ct))
            {
                if (elemento.ValueKind != JsonValueKind.Object)
                    continue;
                filas.Add(LeerFila(elemento, pool));
                if (filas.Count >= maxFilas)
                    break;
            }
            return filas;
        }

        /// <summary>Lee un archivo pequeño de forma tipada (catálogo, notas).</summary>
        public static async Task<List<T>> LeerListaAsync<T>(string ruta, CancellationToken ct)
        {
            await using var stream = Abrir(ruta);
            return await JsonSerializer.DeserializeAsync<List<T>>(stream, OpcionesCatalogo, ct) ?? [];
        }

        private static FilaDatos LeerFila(JsonElement elemento, PoolTextos pool)
        {
            var claves = new List<string>(80);
            var valores = new List<double>(80);
            string? cuadro = null, filtro = null, cuc = null, id = null, titulo = null, unidad = null, grupo = null,
                variable = null, codigoBase = null, segmento = null, tamano = null, rango = null, dpa = null;
            int? nivel = null;
            var nivelJerarquia = 0;

            foreach (var propiedad in elemento.EnumerateObject())
            {
                var nombre = propiedad.Name;
                var valor = propiedad.Value;

                // R1: las claves de período llevan valor; cualquier otra clave desconocida se ignora
                // (p. ej. las columnas basura "...109" de base_anual).
                if (Periodos.EsClave(nombre))
                {
                    claves.Add(pool.Texto(nombre));
                    valores.Add(Numero(valor));
                    continue;
                }

                switch (nombre)
                {
                    case "Cuadro": cuadro = Texto(valor, pool); break;
                    case "Filtro": filtro = Texto(valor, pool); break;
                    case "CUC": cuc = Texto(valor, pool); break;
                    case "ID": id = Texto(valor, pool); break;
                    case "Titulo_Cuadro": titulo = Texto(valor, pool); break;
                    case "Unidad": unidad = Texto(valor, pool); break;
                    case "Grupo": grupo = Texto(valor, pool); break;
                    case "Variable": variable = Texto(valor, pool); break;
                    case "Codigo_Base": codigoBase = Texto(valor, pool)?.Trim(); break;
                    case "segmento_credito": segmento = Texto(valor, pool); break;
                    case "Tamaño": tamano = Texto(valor, pool); break;
                    case "Rango_Activos": rango = Texto(valor, pool); break;
                    case "DPA_PR": dpa = Texto(valor, pool); break;
                    case "Nivel": nivel = Entero(valor); break;
                    default:
                        // Nivel1..Nivel7: el nivel jerárquico es el último con texto no vacío.
                        if (nombre.Length == 6 && nombre.StartsWith("Nivel", StringComparison.Ordinal)
                            && nombre[5] is >= '1' and <= '7' && Texto(valor, null) is not null)
                            nivelJerarquia = Math.Max(nivelJerarquia, nombre[5] - '0');
                        break;
                }
            }

            var ejes = claves.ToArray();
            var datos = valores.ToArray();
            if (!EstaOrdenado(ejes))
                Array.Sort(ejes, datos, StringComparer.Ordinal);

            return new FilaDatos
            {
                Cuadro = cuadro ?? string.Empty,
                Filtro = filtro,
                Cuc = cuc,
                Id = id,
                Titulo = titulo,
                Unidad = unidad,
                Grupo = grupo,
                Variable = variable,
                Nivel = nivel,
                NivelJerarquia = nivelJerarquia,
                CodigoBase = codigoBase,
                SegmentoCredito = segmento,
                Tamano = tamano,
                RangoActivos = rango,
                DpaPr = dpa,
                Periodos = pool.Eje(ejes),
                Valores = datos
            };
        }

        private static bool EstaOrdenado(string[] claves)
        {
            for (var i = 1; i < claves.Length; i++)
                if (string.CompareOrdinal(claves[i - 1], claves[i]) >= 0)
                    return false;
            return true;
        }

        /// <summary>
        /// R2: número o texto numérico (incluida notación científica) con cultura invariante.
        /// null, "", "-" o texto no numérico → NaN (se devuelve como null). R3: el 0 se conserva.
        /// </summary>
        internal static double Numero(JsonElement valor)
        {
            switch (valor.ValueKind)
            {
                case JsonValueKind.Number:
                    return valor.TryGetDouble(out var numero) && double.IsFinite(numero) ? numero : double.NaN;
                case JsonValueKind.String:
                    var texto = valor.GetString()?.Trim();
                    if (string.IsNullOrEmpty(texto) || texto == "-")
                        return double.NaN;
                    return double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out numero) && double.IsFinite(numero)
                        ? numero
                        : double.NaN;
                default:
                    return double.NaN;
            }
        }

        private static string? Texto(JsonElement valor, PoolTextos? pool)
        {
            var texto = valor.ValueKind switch
            {
                JsonValueKind.String => valor.GetString(),
                JsonValueKind.Number => valor.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
            if (string.IsNullOrWhiteSpace(texto))
                return null;
            return pool is null ? texto : pool.Texto(texto);
        }

        private static int? Entero(JsonElement valor)
        {
            var numero = Numero(valor);
            return double.IsNaN(numero) ? null : (int)numero;
        }
    }
}
