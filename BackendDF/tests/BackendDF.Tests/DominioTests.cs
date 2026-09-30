using System.Text.Json;
using BackendDF.Common.Utils;
using BackendDF.Data.FuenteJson;
using BackendDF.Logic.Dominio;
using BackendDF.Logic.Services;
using BackendDF.Models.Entities;

namespace BackendDF.Tests
{
    /// <summary>Pruebas unitarias de las reglas de lectura y del dominio (sin levantar la API).</summary>
    public class DominioTests
    {
        [Theory(DisplayName = "R1: claves de período")]
        [InlineData("2021-05", true)]
        [InlineData("2016-T1", true)]
        [InlineData("2025", true)]
        [InlineData("...109", false)]
        [InlineData("Nivel1", false)]
        [InlineData("2021-5", false)]
        public void EsClavePeriodo(string clave, bool esperado) => Assert.Equal(esperado, Periodos.EsClave(clave));

        [Theory(DisplayName = "R2/R3: números y textos numéricos")]
        [InlineData("1.5", 1.5)]
        [InlineData("\"5.7262000000000003E-4\"", 0.00057262)]
        [InlineData("\" 12.25 \"", 12.25)]
        [InlineData("0", 0.0)]
        public void Numero(string json, double esperado) =>
            Assert.Equal(esperado, LectorJson.Numero(JsonDocument.Parse(json).RootElement), 12);

        [Theory(DisplayName = "R2: vacío, guion, texto o null → sin valor")]
        [InlineData("null")]
        [InlineData("\"\"")]
        [InlineData("\"-\"")]
        [InlineData("\"10,40/11,50 (4)\"")]
        [InlineData("\"NaN\"")]
        public void SinValor(string json) =>
            Assert.True(double.IsNaN(LectorJson.Numero(JsonDocument.Parse(json).RootElement)));

        [Fact(DisplayName = "Rango de períodos: normaliza el anual y rechaza desde > hasta")]
        public void Rango()
        {
            Assert.Equal(("2015-12", "2025-12"), Periodos.Rango("2015", "2025", Periodos.Anual));
            Assert.Equal(("2016-T1", (string?)null), Periodos.Rango("2016-t1", null, Periodos.Trimestral));
            Assert.Throws<CustomException>(() => Periodos.Rango("2026", "2020", Periodos.Anual));
            Assert.Equal("2025-07", Periodos.RestarMeses("2026-07", 12));
            Assert.Equal("2025-12", Periodos.RestarMeses("2026-01", 1));
        }

        [Fact(DisplayName = "Jerarquía de balances por prefijo de código")]
        public void Padres()
        {
            FilaDatos Fila(string codigo, int nivel) => new() { CodigoBase = codigo, Nivel = nivel };
            var filas = new[] { Fila("1", 1), Fila("11", 2), Fila("1101", 3), Fila("110105", 4), Fila("1102", 3), Fila("2", 1), Fila("21", 2) };
            Assert.Equal(new int?[] { null, 0, 1, 2, 1, null, 5 }, CuadrosService.CalcularPadres(filas));
        }

        [Fact(DisplayName = "Búsqueda por CUC y luego por Variable, sin mayúsculas (R7)")]
        public void BuscarCuenta()
        {
            var reporte = new ReporteEntidad("X", ["2026-07"],
            [
                new CuentaReporte("@1", "1.    ACTIVO", [10]),
                new CuentaReporte("perc75_turb", "Percentil 75", [0.15])
            ]);
            Assert.Equal("@1", reporte.BuscarCuenta("@1")?.Cuc);
            Assert.Equal("perc75_turb", reporte.BuscarCuenta("percentil 75")?.Cuc);
            Assert.Equal("perc75_turb", reporte.BuscarCuenta("PERC75_TURB")?.Cuc);
            Assert.Null(reporte.BuscarCuenta("NOPE"));
        }

        [Fact(DisplayName = "Caché LRU: expulsa la menos usada")]
        public async Task CacheLru()
        {
            var cache = new CacheLru<string>(2);
            var cargas = 0;
            Task<string> Cargar(string v) { cargas++; return Task.FromResult(v); }

            await cache.ObtenerAsync("a", () => Cargar("a"));
            await cache.ObtenerAsync("b", () => Cargar("b"));
            await cache.ObtenerAsync("a", () => Cargar("a"));   // a pasa a ser la más reciente
            await cache.ObtenerAsync("c", () => Cargar("c"));   // expulsa b
            await cache.ObtenerAsync("a", () => Cargar("a"));
            Assert.Equal(3, cargas);
            await cache.ObtenerAsync("b", () => Cargar("b"));
            Assert.Equal(4, cargas);
            Assert.Equal(2, cache.Cantidad);
        }

        [Fact(DisplayName = "Texto: sin tildes y listas por coma")]
        public void TextoUtil()
        {
            Assert.Equal("ASOCIACION MUTUALISTA", Texto.Normalizar(" Asociación mutualista "));
            Assert.Equal(["@1", "Percentil 75"], Texto.Separar("@1, Percentil 75,,@1"));
        }
    }
}
