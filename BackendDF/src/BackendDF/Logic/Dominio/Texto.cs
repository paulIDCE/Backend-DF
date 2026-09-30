using System.Globalization;
using System.Text;

namespace BackendDF.Logic.Dominio
{
    /// <summary>Comparación y búsqueda de textos del dominio.</summary>
    public static class Texto
    {
        /// <summary>Orden alfabético en español, sin distinguir mayúsculas (como <c>localeCompare</c>).</summary>
        public static readonly StringComparer ComparadorNombres = StringComparer.Create(CultureInfo.GetCultureInfo("es"), ignoreCase: true);

        /// <summary>Mayúsculas y sin tildes, para búsquedas que no distinguen ninguna de las dos.</summary>
        public static string Normalizar(string texto)
        {
            var descompuesto = texto.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (var c in descompuesto)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }

        /// <summary>Lista separada por comas → elementos recortados, sin vacíos ni repetidos (conserva el orden).</summary>
        public static List<string> Separar(string? lista)
        {
            if (string.IsNullOrWhiteSpace(lista))
                return [];
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return lista.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(vistos.Add)
                .ToList();
        }

        /// <summary>Distintos no vacíos, en orden alfabético.</summary>
        public static List<string> Distintos(IEnumerable<string?> valores) =>
            valores.OfType<string>()
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(v => v, ComparadorNombres)
                .ToList();
    }
}
