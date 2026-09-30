using System.IO.Compression;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using BackendDF.Common.Utils;
using BackendDF.Configuration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// JSON: camelCase (default de ASP.NET), null explícito cuando no hay valor y tildes sin escapar.
// ---------------------------------------------------------------------------
var encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Encoder = encoder;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });
// Mismas opciones para los envelopes que se escriben fuera de MVC (middleware, 401, 404).
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Encoder = encoder);

// Errores de binding (p. ej. notas=abc) con el envelope de error, no con ProblemDetails.
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = context =>
{
    var detalle = string.Join(" ", context.ModelState
        .Where(e => e.Value?.Errors.Count > 0)
        .Select(e => $"{e.Key}: {string.Join(", ", e.Value!.Errors.Select(x => x.ErrorMessage))}"));
    var status = StatusCodes.Status400BadRequest;
    return new ObjectResult(ResponseHandler.Error(status, ApiErrorCodes.ValidationError,
        "Hay parámetros con un valor no válido.", ResponseHandler.TraceIdDe(context.HttpContext), detalle))
    { StatusCode = status };
});

// Fuente de datos JSON, services y health check.
builder.Services.AddApplicationServices(builder.Configuration);

// Autenticación JWT (Supabase hoy, SSO propio después) + política global.
builder.Services.AddAutenticacionApi(builder.Configuration);

// ---------------------------------------------------------------------------
// Compresión: las respuestas son series numéricas y comprimen mucho.
// ---------------------------------------------------------------------------
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// ---------------------------------------------------------------------------
// Swagger con soporte de Bearer (botón "Authorize" para probar endpoints protegidos).
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AnalisisFinanciero API",
        Version = "v1",
        Description = "Consultas de solo lectura sobre los datos de DATA FINANCIERO (contrato docs/backend/CONTRATO_API.md)."
    });

    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml))
        c.IncludeXmlComments(xml);

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Pega el access token (sin el prefijo 'Bearer').",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { securityScheme, Array.Empty<string>() } });
});

// ---------------------------------------------------------------------------
// CORS — solo los orígenes de Cors:Origenes. La API es de solo lectura (GET).
// ---------------------------------------------------------------------------
var corsSettings = builder.Configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();
builder.Services.AddCors(options =>
    options.AddPolicy("CorsPolicy", policy =>
        policy.WithOrigins(corsSettings.Origenes)
              .WithMethods(HttpMethods.Get)
              .AllowAnyHeader()
              .WithExposedHeaders(CabecerasApi.CodigosNoEncontrados, "ETag")));

var app = builder.Build();

app.ValidarConfiguracionAuth();

// Manejo de errores primero: envuelve todo el pipeline posterior.
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("CorsPolicy");
app.UseAuthentication();   // 1º: valida el JWT y puebla HttpContext.User.
app.UseAuthorization();    // 2º: aplica el FallbackPolicy.
app.UseMiddleware<CacheHttpMiddleware>();   // ETag / 304 solo para usuarios ya autorizados.

app.MapControllers();

app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        detalle = report.Entries.Values.FirstOrDefault().Description
    })
}).AllowAnonymous();

// Cualquier otra ruta bajo /api: 404 con el envelope.
app.MapFallback("/api/{**ruta}", context => ResponseHandler.EscribirAsync(
    context, StatusCodes.Status404NotFound, ApiErrorCodes.NotFound, "El recurso solicitado no existe."));

app.Run();

/// <summary>Visible para los tests de integración (WebApplicationFactory).</summary>
public partial class Program;
