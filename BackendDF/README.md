# BackendDF — API de AnalisisFinanciero

API .NET de **solo lectura** que expone los datos de DATA FINANCIERO (prueba-data) con consultas
filtradas, en lugar de que el navegador descargue los JSON monolíticos. Implementa el contrato
acordado con el frontend: `AnalisisFinanciero/docs/backend/CONTRATO_API.md` (v1.0).

- **Fuente de datos**: los mismos JSON de prueba-data, con la misma estructura de carpetas, detrás
  de `IFuenteDatos`. En la etapa 3 se reemplaza por SQL sin cambiar el contrato.
- **Autenticación**: JWT configurable (Supabase hoy, SSO propio después). Todo `/api/**` exige
  token salvo `/api/health`. No depende del SSO corporativo de las apps satélite.
- **Errores**: envelope `{ success, message, detalle, errorCode, traceId, status, data }`.

Partió de la Plantilla API IDCE; se conservó su estructura por capas, el middleware de errores y
la política global de autenticación, y se quitaron el módulo de ejemplo, Dapper y el `TenantProvider`.

---

## Ejecutar

1. Copia los datos de prueba-data en `src/BackendDF/Database/` (o apunta `Datos:RutaBase` a otra carpeta):

   ```
   Database/
     base_anual.json  base_mensual.json  base_trimestral.json  base_notas.json
     base_estru_sistema.json  base_balances.json  base_cartera.json  entidades_lista.json
     entidades/*.json  balances/*.json  reportes/*.json
   ```

2. Arranca:

   ```bash
   dotnet run --project src/BackendDF
   ```

   Escucha en **http://localhost:5097** (la URL que ya usa el frontend: `http://localhost:5097/api`).
   Swagger: http://localhost:5097/swagger (solo Development).

La base (≈ 115 MB) se carga en unos 3–4 s y el índice de los 229 reportes en ~2 s más, en segundo
plano. `/api/health` responde `Degraded` hasta que ambos terminan y luego `Healthy`.

En **Development** la autenticación viene desactivada (`appsettings.Development.json`) para probar
sin token. Con `Auth:Habilitada=false` fuera de Development la API no arranca.

---

## Configuración

```jsonc
{
  "Datos": { "RutaBase": "Database", "MaxEntidadesEnCache": 40 },
  "Auth": {
    "Habilitada": true,
    "Modo": "Supabase",
    "Issuer": "https://<proyecto>.supabase.co/auth/v1",
    "JwksUrl": "https://<proyecto>.supabase.co/auth/v1/.well-known/jwks.json",
    "Audience": "authenticated",
    "Secreto": ""          // HS256 (proyectos Supabase antiguos): por user-secrets
  },
  "Cors": { "Origenes": ["http://localhost:3000", "http://localhost:51646"] }
}
```

- Con `JwksUrl` se validan tokens RS256/ES256 con las claves publicadas (cacheadas 1 h).
- Con `Secreto` se validan tokens HS256: `dotnet user-secrets set "Auth:Secreto" "<jwt secret>"`.
- Para el SSO propio basta cambiar `Issuer`, `Audience` y `JwksUrl`/`Secreto`.

---

## Endpoints (todos `GET`)

| Endpoint | Qué devuelve |
|---|---|
| `/api/catalogos` | Sectores, análisis, créditos, tamaños, rangos de activos y provincias |
| `/api/entidades?q=&tamano=&rango=&provincia=` | Las 229 entidades del catálogo |
| `/api/entidades/{id}` | Una entidad |
| `/api/entidades/{id}/reporte?codigos=&desde=&hasta=` | Reporte REP01 (776 cuentas) |
| `/api/entidades/series?ids=&codigos=&desde=&hasta=` | Comparativo de hasta 4 entidades |
| `/api/cuadros?origen=&q=` | Cuadros disponibles con tipo, frecuencia y parámetros |
| `/api/cuadros/{id}?sector=&entidad=&analisis=&credito=&desde=&hasta=&notas=` | Un cuadro filtrado |
| `/api/rankings?cuenta=&fecha=&agrupacion=&entidad=` | Ranking por sector, activos, provincia o todas |
| `/api/sistema/series?codigos=&sectores=&cuadros=&desde=&hasta=` | Series de los sectores |
| `/api/meta` | Versión de datos, cobertura por fuente y advertencias |
| `/api/health` | `Healthy` / `Degraded` / `Unhealthy` (sin auth) |

Las respuestas llevan `ETag` + `Cache-Control: private, max-age=3600` (304 con `If-None-Match`) y
van comprimidas con Brotli/Gzip. Los códigos pedidos que no existen se informan en la cabecera
`X-Codigos-No-Encontrados`.

---

## Estructura

```
src/BackendDF/
├── Program.cs                     # JSON, compresión, Swagger, CORS, pipeline, /api/health
├── Configuration/                 # *Settings, AddApplicationServices(), AddAutenticacionApi()
├── Common/
│   ├── Utils/                     # ApiErrorCodes, CustomException, ResponseHandler (envelope),
│   │                              #   ExceptionHandlingMiddleware, CacheHttpMiddleware, CacheLru, health check
│   └── Security/                  # JwksConfigurationManager (claves del JWKS)
├── Controllers/                   # Catalogos, Entidades, Cuadros, Rankings, Sistema, Meta
├── Logic/
│   ├── Dominio/                   # Periodos, Catalogo (ids ↔ textos), TiposCuadro, Alineador, Texto
│   ├── Interfaces/ Services/      # Un service por área del contrato
├── Data/
│   ├── Interfaces/IFuenteDatos.cs # Contrato de acceso (se reemplazará por SQL)
│   └── FuenteJson/                # Lector JSON (reglas R1–R8), tablas indexadas, carga y caché
├── Models/{Entities,DTOs}         # Modelos de la fuente y respuestas del contrato
└── Database/                      # Los JSON (no se versionan, no se copian a bin ni se sirven)
tests/BackendDF.Tests/             # xUnit + WebApplicationFactory: criterios §13 y reglas del contrato
```

## Tests

```bash
dotnet test tests/BackendDF.Tests
```

Levantan la API en memoria sobre los datos reales de `src/BackendDF/Database` con la
autenticación habilitada (tokens HS256 de prueba).

## Notas sobre el contrato

- **Criterio #2** (`SFN01?sector=nacional`): el contrato espera 70 períodos hasta 2027-02, pero
  en `base_estru_sistema` solo las filas de `TPE01` llegan a 2027-02. Las de SFN01 terminan en
  2027-01, así que la API devuelve **69** (la unión de las filas filtradas, igual que el frontend).
- **Runtime**: el contrato sugiere .NET 10; se mantiene **.NET 8 (LTS)** de la plantilla porque es
  el runtime disponible. El cambio es solo el `TargetFramework`.
- En `/api/cuadros`, el rango de períodos de los cuadros por entidad (`EFI*`, `TEA02`, `TPE02`) es
  el de la primera entidad del catálogo: cada entidad puede cubrir un rango distinto.
- Se usa **Workstation GC**: con Server GC el proceso retenía más de 1,8 GB. Con Workstation GC
  queda en ≈ 450 MB con la caché de entidades llena.
