using BackendDF.Configuration;
using BackendDF.Models.Entities;

namespace BackendDF.Data.FuenteSql
{
    /// <summary>
    /// Reglas de la fuente híbrida (informe §0 y §8.3):
    /// <list type="bullet">
    /// <item>C1: todo lo que deriva del .sav se recorta a la ventana de la BD; los macro y lo
    /// externo (<see cref="SqlSettings.CuadrosFechasJson"/>) conservan las fechas del JSON.</item>
    /// <item>C2: las cuentas de los grupos omitidos (6 y 7) se ocultan en los cuadros servidos por SQL.</item>
    /// <item>Qué fila se lee de SQL: cuenta numérica (<c>@1101</c> o <c>Codigo_Base</c> en la fila de saldo)
    /// o indicador de <c>IndicadorData</c>, con su escala.</item>
    /// </list>
    /// </summary>
    public sealed class ReglasCuadro
    {
        /// <summary>Texto de <c>ID</c> de la fila de saldo en los balances (las de análisis H/V siguen en JSON).</summary>
        public const string IdSaldo = "Saldo Millones USD";

        /// <summary>B11 está en USD; los cuadros, en millones.</summary>
        public const double EscalaSaldo = 1e-6;

        public static readonly string[] CuadrosSqlPorDefecto =
        [
            "EFI01", "EFI02", "EFI05", "EFI06", "EFI07", "EFI10", "REP01",
            "SFN01", "SFN02", "SFN03", "SFN04", "SFN05", "SFN06", "SFN07", "SFN08"
        ];

        public static readonly string[] CuadrosFechasJsonPorDefecto = ["EFI09", "TPE02", "CAR03"];

        public static readonly string[] GruposOmitidosPorDefecto = ["6", "7"];

        /// <summary>Indicadores de <c>IndicadorData</c> y su escala respecto a los JSON.</summary>
        private static readonly Dictionary<string, double> EscalasIndicador = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ACT_PON_RIESGO"] = EscalaSaldo,
            ["P_T_PRIMARIO"] = EscalaSaldo,
            ["P_T_SECUNDARIO"] = EscalaSaldo,
            ["PTC"] = EscalaSaldo,
            ["SOLVENCIA"] = 100
        };

        private readonly HashSet<string> _cuadrosSql;
        private readonly HashSet<string> _cuadrosFechasJson;
        private readonly string[] _gruposOmitidos;

        public ReglasCuadro(SqlSettings settings)
        {
            _cuadrosSql = new(settings.CuadrosSql is { Length: > 0 } a ? a : CuadrosSqlPorDefecto, StringComparer.OrdinalIgnoreCase);
            _cuadrosFechasJson = new(settings.CuadrosFechasJson is { Length: > 0 } b ? b : CuadrosFechasJsonPorDefecto, StringComparer.OrdinalIgnoreCase);
            _gruposOmitidos = settings.GruposOmitidos is { Length: > 0 } c ? c : GruposOmitidosPorDefecto;
        }

        /// <summary>C1: ¿el cuadro se recorta a la ventana de la BD?</summary>
        public bool UsaVentana(TipoCuadro tipo, string cuadro) =>
            tipo != TipoCuadro.Macro && !_cuadrosFechasJson.Contains(cuadro);

        /// <summary>¿Las filas de cuenta/indicador del cuadro se leen de SQL?</summary>
        public bool UsaSql(string cuadro) => _cuadrosSql.Contains(cuadro);

        /// <summary>C2: ¿la cuenta pertenece a un grupo que no existe en la BD?</summary>
        public bool Omitida(string cuenta) =>
            _gruposOmitidos.Any(g => cuenta.StartsWith(g, StringComparison.Ordinal));

        /// <summary>Cuenta de la fila: <c>Codigo_Base</c> en los balances, si no el CUC <c>@dígitos</c>.</summary>
        public static string? Cuenta(FilaDatos fila) =>
            fila.CodigoBase is not null ? Digitos(fila.CodigoBase) : Cuenta(fila.Cuc);

        /// <summary><c>@1101</c> → <c>1101</c>; cualquier otro código (indicadores, <c>@4101A</c>…) → null.</summary>
        public static string? Cuenta(string? cuc) =>
            cuc is { Length: > 1 } && cuc[0] == '@' ? Digitos(cuc[1..]) : null;

        /// <summary>En los balances solo la fila de saldo viene de SQL; en el resto, toda fila de cuenta.</summary>
        public static bool EsFilaSaldo(FilaDatos fila) =>
            fila.CodigoBase is null || string.Equals(fila.Id, IdSaldo, StringComparison.Ordinal);

        public static bool EsIndicador(string? codigo) => codigo is not null && EscalasIndicador.ContainsKey(codigo);

        public static bool TryEscalaIndicador(string? codigo, out double escala)
        {
            if (codigo is not null && EscalasIndicador.TryGetValue(codigo, out escala))
                return true;
            escala = 1;
            return false;
        }

        public static IEnumerable<string> Indicadores => EscalasIndicador.Keys;

        private static string? Digitos(string texto)
        {
            var t = texto.Trim();
            return t.Length > 0 && t.All(char.IsAsciiDigit) ? t : null;
        }
    }
}
