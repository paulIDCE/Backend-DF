using BackendDF.Common.Utils;
using BackendDF.Data.FuenteHibrida;
using BackendDF.Data.FuenteJson;
using BackendDF.Data.FuenteSql;
using BackendDF.Data.Interfaces;
using BackendDF.Data.Repositorios;
using BackendDF.Logic.Interfaces;
using BackendDF.Logic.Services;
using Microsoft.Extensions.Options;

namespace BackendDF.Configuration
{
    /// <summary>
    /// Registro centralizado de servicios de la aplicación para mantener Program.cs limpio.
    /// Añade aquí cada par (I*Service/*Service) de tus módulos.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<DatosSettings>(configuration.GetSection("Datos"));

            // ---- Fuente de datos ----
            // Singleton: la base y los índices viven en memoria durante toda la vida de la app.
            // La fuente JSON siempre se carga (plantillas, catálogo, macro, lo que aún no está en SQL).
            services.AddSingleton<FuenteDatosJson>();
            services.AddHostedService<CargaDatosHostedService>();

            // Fuente híbrida (informe §8): SQL (idce_bco_coop) + JSON. Se decide en tiempo de
            // resolución con Datos:Sql:Habilitado; con false la API es exactamente la de antes.
            services.AddSingleton<IBcoCoopRepositorio>(sp =>
            {
                var sql = sp.GetRequiredService<IOptions<DatosSettings>>().Value.Sql;
                var cadena = configuration.GetConnectionString(sql.ConnectionStringName) ?? string.Empty;
                return new BcoCoopRepositorioSql(cadena, sql.TimeoutSegundos);
            });
            services.AddSingleton(sp =>
            {
                var sql = sp.GetRequiredService<IOptions<DatosSettings>>().Value.Sql;
                var raiz = sp.GetRequiredService<IHostEnvironment>().ContentRootPath;
                return MapaEntidades.Cargar(Path.GetFullPath(Path.Combine(raiz, sql.RutaMapaEntidades)));
            });
            services.AddSingleton(sp => new FuenteDatosHibrida(
                sp.GetRequiredService<FuenteDatosJson>(),
                sp.GetRequiredService<IBcoCoopRepositorio>(),
                sp.GetRequiredService<MapaEntidades>(),
                sp.GetRequiredService<IOptions<DatosSettings>>().Value,
                sp.GetRequiredService<ILogger<FuenteDatosHibrida>>()));
            services.AddSingleton<IFuenteDatos>(sp =>
                sp.GetRequiredService<IOptions<DatosSettings>>().Value.Sql.Habilitado
                    ? sp.GetRequiredService<FuenteDatosHibrida>()
                    : sp.GetRequiredService<FuenteDatosJson>());
            services.AddHostedService<PrecargaHibridaHostedService>();

            services.AddHealthChecks().AddCheck<FuenteDatosHealthCheck>("datos");

            // ---- Módulos de negocio ----
            services.AddScoped<ICatalogosService, CatalogosService>();
            services.AddScoped<ICuadrosService, CuadrosService>();
            services.AddScoped<IReportesService, ReportesService>();
            services.AddScoped<IRankingService, RankingService>();
            services.AddScoped<ISistemaService, SistemaService>();
            services.AddScoped<IMetaService, MetaService>();

            return services;
        }
    }
}
