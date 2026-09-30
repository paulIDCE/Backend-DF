using BackendDF.Common.Utils;
using BackendDF.Models.DTOs;

namespace BackendDF.Logic.Dominio
{
    /// <summary>
    /// Mapeo id ↔ texto del origen de sectores, análisis y tipos de crédito (contrato §4.4).
    /// La coincidencia con el texto del origen (<c>Filtro</c> / <c>ID</c>) es exacta.
    /// </summary>
    public static class Catalogo
    {
        public static readonly IReadOnlyList<ItemCatalogoDTO> Sectores =
        [
            new("nacional", "Sistema Financiero Nacional (privado y eps)"),
            new("privado", "Sector Financiero Privado"),
            new("popular", "Sector Financiero Popular y Solidario"),
            new("grande", "Bancos Privados Grandes"),
            new("medianos", "Bancos Privados Medianos"),
            new("peque", "Bancos Privados Pequeños"),
            new("seg1", "Coop. Segmento 1"),
            new("seg2", "Coop. Segmento 2"),
            new("seg3", "Coop. Segmento 3"),
            new("mut", "Asociación Mutualistas de Ahorro y Crédito para la Vivienda")
        ];

        public static readonly IReadOnlyList<ItemCatalogoDTO> Analisis =
        [
            new("saldo", "Saldo Millones USD"),
            new("horizontal", "Análisis Horizontal (%)"),
            new("vertical", "Análisis Vertical (%)")
        ];

        public static readonly IReadOnlyList<ItemCatalogoDTO> Creditos =
        [
            new("total", "Cartera Total"),
            new("productivo", "Productivo"),
            new("consumo", "Consumo"),
            new("inmobiliario", "Inmobiliario"),
            new("vip", "Vivienda interés Público y Social"),
            new("educativo", "Educativo"),
            new("microcredito", "Microcrédito")
        ];

        /// <summary>Busca un id (sin distinguir mayúsculas) o lanza 400 con los valores permitidos.</summary>
        public static ItemCatalogoDTO Resolver(IReadOnlyList<ItemCatalogoDTO> lista, string valor, string parametro) =>
            lista.FirstOrDefault(i => string.Equals(i.Id, valor.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw CustomException.Validacion(
                $"El valor '{valor}' no es válido para '{parametro}'. Valores permitidos: {string.Join(", ", lista.Select(i => i.Id))}.");
    }
}
