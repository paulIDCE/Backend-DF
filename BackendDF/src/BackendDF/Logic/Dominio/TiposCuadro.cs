using BackendDF.Models.Entities;

namespace BackendDF.Logic.Dominio
{
    /// <summary>Reglas por tipo de cuadro (contrato §6.2): nombre, vista y parámetros requeridos.</summary>
    public static class TiposCuadro
    {
        public const string Sector = "sector";
        public const string Entidad = "entidad";
        public const string Analisis = "analisis";
        public const string Credito = "credito";

        public static string Nombre(TipoCuadro tipo) => tipo switch
        {
            TipoCuadro.Macro => "macro",
            TipoCuadro.Sistema => "sistema",
            TipoCuadro.Balances => "balances",
            TipoCuadro.Cartera => "cartera",
            TipoCuadro.Entidad => "entidad",
            TipoCuadro.BalancesEntidad => "balancesEntidad",
            TipoCuadro.CarteraEntidad => "carteraEntidad",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null)
        };

        public static bool EsArbol(TipoCuadro tipo) => tipo is TipoCuadro.Balances or TipoCuadro.BalancesEntidad;

        public static string Vista(TipoCuadro tipo) => EsArbol(tipo) ? "arbol" : "grupos";

        public static IReadOnlyList<string> Parametros(TipoCuadro tipo) => tipo switch
        {
            TipoCuadro.Macro => [],
            TipoCuadro.Sistema => [Sector],
            TipoCuadro.Balances => [Sector, Analisis],
            TipoCuadro.Cartera => [Sector, Credito],
            TipoCuadro.Entidad => [Entidad],
            TipoCuadro.BalancesEntidad => [Entidad, Analisis],
            TipoCuadro.CarteraEntidad => [Entidad, Credito],
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null)
        };
    }
}
