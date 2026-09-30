using BackendDF.Common.Utils;
using BackendDF.Data.FuenteJson;
using BackendDF.Data.Interfaces;
using BackendDF.Logic.Interfaces;
using BackendDF.Logic.Services;

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
            // Para pasar a SQL (etapa 3) basta registrar otra implementación de IFuenteDatos.
            services.AddSingleton<FuenteDatosJson>();
            services.AddSingleton<IFuenteDatos>(sp => sp.GetRequiredService<FuenteDatosJson>());
            services.AddHostedService<CargaDatosHostedService>();
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
