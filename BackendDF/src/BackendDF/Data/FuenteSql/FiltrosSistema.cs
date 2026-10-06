namespace BackendDF.Data.FuenteSql
{
    /// <summary>Qué entidades de la BD suman un sector: tipos de <c>Ifi.TipoEntidad</c> y, opcionalmente, <c>Ifi.Segmento</c>.</summary>
    public sealed record ReglaSector(string[] TiposEntidad, int[]? Segmentos);

    /// <summary>
    /// Traducción del sector del frontend (texto exacto de <c>Filtro</c>, ver
    /// <c>Logic/Dominio/Catalogo.Sectores</c>) a la regla de agregación de la BD. Vive en el
    /// backend: la BD no conoce los nombres de los sectores (informe §7). Los segmentos de
    /// cooperativas son los de <c>Ifi.Segmento</c> (convención C3; difieren de los JSON, ver
    /// <c>docs/anexo-segmentos-coop.csv</c>).
    /// </summary>
    public static class FiltrosSistema
    {
        private static readonly Dictionary<string, ReglaSector> Reglas = new(StringComparer.Ordinal)
        {
            ["Sistema Financiero Nacional (privado y eps)"] = new(["BP", "COOP", "MU"], null),
            ["Sector Financiero Privado"] = new(["BP"], null),
            ["Sector Financiero Popular y Solidario"] = new(["COOP"], null),
            ["Bancos Privados Grandes"] = new(["BP"], [1]),
            ["Bancos Privados Medianos"] = new(["BP"], [2]),
            ["Bancos Privados Pequeños"] = new(["BP"], [3]),
            ["Coop. Segmento 1"] = new(["COOP"], [1]),
            ["Coop. Segmento 2"] = new(["COOP"], [2]),
            ["Coop. Segmento 3"] = new(["COOP"], [3]),
            ["Asociación Mutualistas de Ahorro y Crédito para la Vivienda"] = new(["MU"], null)
        };

        public static bool TryObtener(string? filtro, out ReglaSector regla)
        {
            if (filtro is not null && Reglas.TryGetValue(filtro, out var r))
            {
                regla = r;
                return true;
            }
            regla = null!;
            return false;
        }

        public static IEnumerable<string> Sectores => Reglas.Keys;
    }
}
