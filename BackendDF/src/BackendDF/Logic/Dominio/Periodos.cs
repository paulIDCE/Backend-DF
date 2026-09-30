using System.Globalization;
using System.Text.RegularExpressions;
using BackendDF.Common.Utils;

namespace BackendDF.Logic.Dominio
{
    /// <summary>
    /// Parseo y rango de períodos (contrato §4.3): <c>YYYY-MM</c> (mensual), <c>YYYY-Tn</c>
    /// (trimestral) y <c>YYYY-12</c> (anual, formato del origen). Dentro de una misma frecuencia
    /// el orden ordinal del texto es el orden cronológico.
    /// </summary>
    public static partial class Periodos
    {
        public const string Mensual = "mensual";
        public const string Trimestral = "trimestral";
        public const string Anual = "anual";

        [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
        private static partial Regex ReMensual();

        [GeneratedRegex(@"^\d{4}-T[1-4]$")]
        private static partial Regex ReTrimestral();

        [GeneratedRegex(@"^\d{4}(-12)?$")]
        private static partial Regex ReAnual();

        /// <summary>R1: la clave es un período si cumple <c>^\d{4}(-(\d{2}|T\d))?$</c>.</summary>
        public static bool EsClave(string clave)
        {
            if (clave.Length != 4 && clave.Length != 7)
                return false;
            for (var i = 0; i < 4; i++)
                if (!char.IsAsciiDigit(clave[i]))
                    return false;
            if (clave.Length == 4)
                return true;
            return clave[4] == '-' && (char.IsAsciiDigit(clave[5]) || clave[5] == 'T') && char.IsAsciiDigit(clave[6]);
        }

        public static bool EsMensual(string periodo) => ReMensual().IsMatch(periodo);

        /// <summary>Frecuencia según las claves: trimestral si hay "-T", anual si el archivo es anual.</summary>
        public static string FrecuenciaDe(IEnumerable<string> periodos, bool archivoAnual)
        {
            if (periodos.Any(p => p.Contains('T')))
                return Trimestral;
            return archivoAnual ? Anual : Mensual;
        }

        /// <summary>
        /// Valida y normaliza <c>desde</c>/<c>hasta</c> para la frecuencia (anual acepta <c>YYYY</c>)
        /// y comprueba que <c>desde ≤ hasta</c>. Ambos son opcionales.
        /// </summary>
        public static (string? Desde, string? Hasta) Rango(string? desde, string? hasta, string frecuencia)
        {
            var d = Normalizar(desde, frecuencia, "desde");
            var h = Normalizar(hasta, frecuencia, "hasta");
            if (d is not null && h is not null && string.CompareOrdinal(d, h) > 0)
                throw CustomException.Validacion($"El período 'desde' ({desde}) no puede ser posterior a 'hasta' ({hasta}).");
            return (d, h);
        }

        private static string? Normalizar(string? valor, string frecuencia, string parametro)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null;

            var v = valor.Trim().ToUpperInvariant();
            switch (frecuencia)
            {
                case Mensual when ReMensual().IsMatch(v):
                case Trimestral when ReTrimestral().IsMatch(v):
                    return v;
                case Anual when ReAnual().IsMatch(v):
                    return v.Length == 4 ? v + "-12" : v;
            }

            var formato = frecuencia switch
            {
                Trimestral => "YYYY-Tn",
                Anual => "YYYY o YYYY-12",
                _ => "YYYY-MM"
            };
            throw CustomException.Validacion($"El parámetro '{parametro}' debe tener el formato {formato} (recibido: '{valor}').");
        }

        /// <summary>Períodos dentro de [desde, hasta] (inclusivos, opcionales).</summary>
        public static string[] Filtrar(string[] periodos, string? desde, string? hasta)
        {
            if (desde is null && hasta is null)
                return periodos;
            return periodos
                .Where(p => (desde is null || string.CompareOrdinal(p, desde) >= 0)
                            && (hasta is null || string.CompareOrdinal(p, hasta) <= 0))
                .ToArray();
        }

        /// <summary>Unión ordenada de varios ejes de períodos (los arreglos repetidos se miran una vez).</summary>
        public static string[] Union(IEnumerable<string[]> ejes)
        {
            var vistos = new HashSet<string[]>(ReferenceEqualityComparer.Instance);
            var union = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var eje in ejes)
                if (vistos.Add(eje))
                    union.UnionWith(eje);
            return [.. union];
        }

        /// <summary><c>YYYY-MM</c> menos N meses.</summary>
        public static string RestarMeses(string mensual, int meses)
        {
            var fecha = DateTime.ParseExact(mensual + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).AddMonths(-meses);
            return fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        /// <summary>El período en curso según la frecuencia de <paramref name="periodo"/>.</summary>
        public static string ActualComo(string periodo, DateTime hoy)
        {
            if (periodo.Contains('T'))
                return $"{hoy.Year:D4}-T{(hoy.Month - 1) / 3 + 1}";
            return hoy.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }
    }
}
