# API AnalisisFinanciero: contrato y guía de consumo para el frontend

Versión 1.0 implementada · 30/09/2026 · Backend: `BackendDF` (.NET 8)

Este documento explica dos cosas:

1. **El contrato tal como está implementado**: endpoints, parámetros, respuestas y errores.
2. **Qué tiene que cambiar el frontend** (`AnalisisFinanciero`) para dejar de leer `/data/*.json`
   y consumir la API.

El contrato original es `AnalisisFinanciero/docs/backend/CONTRATO_API.md`. La API lo cumple salvo
las diferencias de la [§ 8](#8-diferencias-con-el-contrato-v10).

---

## 1. Resumen para el equipo del frontend

| | |
|---|---|
| URL base | `http://localhost:5097/api` (ya es el `VITE_API_URL` de `public/config/routes.json`) |
| Métodos | Solo `GET` |
| Formato | JSON, campos en español camelCase, `null` explícito cuando no hay valor |
| Autenticación | `Authorization: Bearer <access_token de Supabase>` en todo `/api/**` salvo `/api/health` |
| Errores | Envelope `{ success, message, detalle, errorCode, traceId, status, data }` (ya lo interpreta `src/utils/apiError.ts`) |
| Caché | `ETag` + `Cache-Control: private, max-age=3600`; el navegador revalida solo (304) |
| Compresión | Brotli/Gzip automática |

**Trabajo del frontend, en orden:**

1. Enviar el token de Supabase en el interceptor de axios ([§ 3](#3-autenticación-lo-primero-que-hay-que-cambiar)).
2. Agregar las funciones de la API y los adaptadores en `src/services/` ([§ 6](#6-implementación-en-el-frontend)).
3. Cambiar los cargadores de cada pantalla ([§ 7](#7-cambios-por-pantalla)). Los adaptadores devuelven las
   filas con la misma forma que los JSON de hoy, así que **los componentes de tabla, gráfica,
   colección y carrito no se tocan**.
4. Quitar `public/data` del despliegue.

---

## 2. Levantar la API en local

```bash
cd BackendDF
dotnet run --project src/BackendDF
```

- Escucha en `http://localhost:5097` y Swagger está en `http://localhost:5097/swagger`.
- Los datos se leen de `BackendDF/src/BackendDF/Database/`, con la misma estructura que `prueba-data/data`.
- Carga en unos 5 s. `GET /api/health` responde `Degraded` mientras carga y `Healthy` cuando termina.
- **En Development la autenticación está desactivada**, así que el frontend puede consumir la API sin token.
  Fuera de Development el token es obligatorio.
- CORS permite `http://localhost:3000` y `http://localhost:51646`. Si el frontend corre en otro puerto,
  hay que agregarlo en `Cors:Origenes` del `appsettings.json` del backend.

---

## 3. Autenticación (lo primero que hay que cambiar)

### 3.1 El problema

Supabase sí entrega un token al iniciar sesión: `session.access_token`, un JWT firmado por el proyecto
(ES256). Pero `src/interceptors/authInterceptor.ts` lo busca en `localStorage[AUTH_TOKEN_KEY]`, que es
donde lo guarda el SSO corporativo de las apps satélite. Supabase nunca escribe ahí, así que hoy
**no se envía la cabecera** y, con la autenticación habilitada, la API responde 401.

La API ya está configurada para validar los tokens del proyecto `mxpseuoksbrqsecukqou`:

| Claim | Valor esperado |
|---|---|
| `iss` | `https://mxpseuoksbrqsecukqou.supabase.co/auth/v1` |
| `aud` | `authenticated` |
| firma | Claves de `…/auth/v1/.well-known/jwks.json` |
| `exp` | Vigente (tolerancia de 30 s) |

### 3.2 Cambio en `src/interceptors/authInterceptor.ts`

```ts
import { getSupabase, supabaseDisponible } from "@/auth/supabase";

/**
 * Token de la sesión de Supabase (login propio de la app). getSession()
 * devuelve la sesión guardada y la renueva si el access_token expiró.
 * Se deja el localStorage/cookie como fallback para el SSO propio futuro.
 */
const getAuthToken = async (): Promise<string | undefined> => {
  if (supabaseDisponible()) {
    const { data } = await getSupabase().auth.getSession();
    if (data.session) return data.session.access_token;
  }
  const stored = localStorage.getItem(AUTH_TOKEN_KEY);
  if (stored) return stored;
  return document.cookie
    .split(";")
    .map((c) => c.trim())
    .find((c) => c.startsWith(`${AUTH_TOKEN_KEY}=`))
    ?.replace(`${AUTH_TOKEN_KEY}=`, "");
};

// En createClient(): el interceptor pasa a ser async.
instance.interceptors.request.use(
  async (config) => {
    const token = await getAuthToken();
    if (token) config.headers["Authorization"] = `Bearer ${token}`;
    return config;
  },
  (error) => Promise.reject(error)
);
```

- El manejo actual del 401 (limpiar y redirigir a `/login`) sigue sirviendo. Conviene sumar
  `getSupabase().auth.signOut()` antes de redirigir, para que no quede una sesión vencida.
- `withCredentials: true` no hace falta para esta API (no usa cookies), aunque no molesta.
- **Probar en Swagger con token:** después de iniciar sesión en la app, se obtiene desde la consola
  del navegador con `(await supabase.auth.getSession()).data.session.access_token` y se pega en
  "Authorize".

---

## 4. Convenciones

### 4.1 Períodos

| Frecuencia | Formato | `desde` / `hasta` aceptan |
|---|---|---|
| `mensual` | `YYYY-MM` | `YYYY-MM` |
| `trimestral` | `YYYY-Tn` | `YYYY-Tn` |
| `anual` | `YYYY-12` (formato del origen) | `YYYY` o `YYYY-12` |

- `desde` y `hasta` son **inclusivos** y **opcionales**. Sin ellos se devuelven todos los períodos.
- `desde > hasta` o un formato que no corresponde a la frecuencia → 400.
- **Siempre** viene `periodos: string[]`, y cada serie trae `valores: (number | null)[]` **alineado
  posición a posición** con `periodos`.
- `null` = sin dato: el origen traía null, `""`, `"-"` o texto no numérico. El `0` del origen se
  devuelve como `0`.
- Los números que el origen guardaba como texto (`"5.7262E-4"`) ya llegan como número.
- Hay datos con períodos futuros (hasta 2027-02). No se filtran; se informan en `/api/meta`.

### 4.2 Identificadores de catálogo

| Parámetro | Valores | Nota para el frontend |
|---|---|---|
| `sector` | `nacional`, `privado`, `popular`, `grande`, `medianos`, `peque`, `seg1`, `seg2`, `seg3`, `mut` | **Iguales** a los `value` de `SECTORES` en `Sistema/cargarCuadro.ts` |
| `analisis` | `saldo`, `horizontal`, `vertical` | **Iguales** a `ANALISIS` |
| `credito` | `total`, `productivo`, `consumo`, `inmobiliario`, `vip`, `educativo`, `microcredito` | ⚠️ Hoy `CREDITOS` usa el texto ("Cartera Total"…) como `value`. Hay que usar estos ids o mapearlos ([§ 6.3](#63-mapeo-de-créditos)) |
| `entidad` | `archivo` del catálogo, p. ej. `BP__PICHINCHA` | Hoy la pantalla maneja el nombre (`BP. PICHINCHA`). `archivoEntidad(nombre)` de `datosService.ts` da el id correcto |

`GET /api/catalogos` devuelve estas listas con `{ id, nombre }`, así que el frontend puede dejar de
tenerlas escritas en el código.

### 4.3 Errores

Toda respuesta con error usa este envelope (las exitosas devuelven el recurso directamente):

```json
{
  "success": false,
  "message": "El cuadro SFN06 requiere el parámetro 'analisis'.",
  "detalle": "texto técnico (solo en Development)",
  "errorCode": "VALIDATION_ERROR",
  "traceId": "00-4bf92f35...",
  "status": 400,
  "data": null
}
```

| HTTP | `errorCode` | Cuándo |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Parámetro faltante, inválido o que no aplica al cuadro |
| 401 | `UNAUTHORIZED` | Token faltante, vencido o inválido → redirigir a `/login` |
| 404 | `NOT_FOUND` | Cuadro, entidad, cuenta o ruta inexistente |
| 500 | `INTERNAL_ERROR` | Error no controlado o datos no disponibles |

- `message` está redactado para el usuario final y se puede mostrar tal cual.
- `traceId` sirve para buscar el error en los logs del backend.
- Si los filtros no dan filas, la respuesta es **200 con `filas: []`**, no 404.

### 4.4 Cabeceras útiles

| Cabecera | Uso |
|---|---|
| `X-Codigos-No-Encontrados` | En reporte y series: códigos pedidos que no existen, separados por coma (los que tienen tildes van percent-encoded). No es un error. |
| `ETag` / `Cache-Control` | Las maneja el navegador: con la caché vigente, axios recibe la respuesta desde caché sin ir al servidor. |

Ambas están expuestas por CORS, así que axios puede leerlas en `response.headers`.

---

## 5. Referencia de endpoints

### Tipos TypeScript

Se sugiere ponerlos en `src/types/api.ts`.

```ts
export type Valores = (number | null)[];
export interface Rango { desde: string; hasta: string }
export interface Item { id: string; nombre: string }

export interface Catalogos {
  sectores: Item[];
  analisis: Item[];
  creditos: Item[];
  tamanos: string[];
  rangosActivos: string[];
  provincias: string[];
}

export interface Entidad {
  id: string;                 // "BP__AMAZONAS"
  nombre: string;             // "BP. AMAZONAS"
  tipo: "banco" | "cooperativa" | "mutualista";
  tamano: string | null;
  rango: string | null;
  provincia: string | null;
  tieneBalance: boolean;
  periodos: Rango | null;
}

export type TipoCuadroApi =
  | "macro" | "sistema" | "balances" | "cartera"
  | "entidad" | "balancesEntidad" | "carteraEntidad";

export interface CuadroResumen {
  id: string;
  titulo: string;
  origen: "macro" | "sistema" | "entidad";
  tipo: TipoCuadroApi;
  frecuencia: "mensual" | "trimestral" | "anual";
  vista: "grupos" | "arbol";
  parametros: ("sector" | "entidad" | "analisis" | "credito")[];
  periodos: Rango | null;
}

export interface FilaApi {
  indice: number;
  clave: string;             // estable: "SFN01|nacional|@1"
  grupo: string | null;
  variable: string | null;
  cuc: string | null;
  codigoBase: string | null; // solo balances
  nivel: number;
  padre: number | null;      // indice del padre (solo vista arbol)
  valores: Valores;
}

export interface CuadroApi {
  id: string;
  titulo: string;
  unidad: string | null;
  tipo: TipoCuadroApi;
  frecuencia: "mensual" | "trimestral" | "anual";
  vista: "grupos" | "arbol";
  contexto: {
    sector: string | null; sectorNombre: string | null;
    entidad: string | null; entidadNombre: string | null;
    analisis: string | null; analisisNombre: string | null;
    credito: string | null; creditoNombre: string | null;
  };
  periodosDisponibles: Rango | null;
  periodos: string[];
  filas: FilaApi[];
  notas: string[];
}

export interface ReporteApi {
  entidad: { id: string; nombre: string; tamano: string | null; rango: string | null; provincia: string | null };
  periodos: string[];
  cuentas: { cuc: string; variable: string | null; valores: Valores }[];
}

export interface RankingApi {
  cuenta: string;
  fecha: string;
  fechaComparacion: string;
  agrupacion: { tipo: "sector" | "activos" | "provincia" | "todas"; valor: string | null };
  total: { anterior: number; actual: number };
  posicionEntidad: number | null;
  filas: {
    posicion: number; entidadId: string; nombre: string;
    anterior: number; actual: number;
    participacionAnterior: number; participacionActual: number; // en %, sin redondear
  }[];
}

export interface SeriesEntidadesApi {
  periodos: string[];
  series: { entidadId: string; codigo: string; variable: string | null; valores: Valores }[];
}

export interface SeriesSistemaApi {
  periodos: string[];
  series: { sector: string; sectorNombre: string; codigo: string; cuadro: string; variable: string | null; valores: Valores }[];
}

export interface MetaApi {
  versionDatos: string;       // ISO UTC: fecha real de los datos
  fuentes: { fuente: string; archivo: string | null; archivos: number | null; desde: string | null; hasta: string | null; modificado: string | null }[];
  ultimoCorteComun: string | null;
  advertencias: string[];
}
```

### 5.1 `GET /api/catalogos` → `Catalogos`

Todo lo necesario para los filtros, en una sola llamada. `tamanos`, `rangosActivos` y `provincias`
son los valores distintos de las entidades, en orden alfabético.

### 5.2 `GET /api/entidades` → `Entidad[]`

| Query | Descripción |
|---|---|
| `q` | Busca en el nombre, sin distinguir mayúsculas ni tildes |
| `tamano`, `rango`, `provincia` | Filtro exacto |

Son 229 entidades en orden alfabético, sin paginar. Los 101 balances y los 2 reportes huérfanos no aparecen.

### 5.3 `GET /api/entidades/{id}` → `Entidad`

Devuelve 404 si la entidad no está en el catálogo. El id no distingue mayúsculas.

### 5.4 `GET /api/cuadros` → `CuadroResumen[]`

| Query | Descripción |
|---|---|
| `origen` | `macro` \| `sistema` \| `entidad` (opcional) |
| `q` | Busca en el id y el título (opcional) |

Hay 223 cuadros: 195 de Macro, 13 de Sistema y 15 por entidad. En los cuadros por entidad, `periodos`
es el rango de la primera entidad del catálogo.

### 5.5 `GET /api/cuadros/{id}` → `CuadroApi` (endpoint principal)

| Query | Descripción |
|---|---|
| `sector`, `entidad`, `analisis`, `credito` | Según el tipo (tabla abajo) |
| `desde`, `hasta` | Opcionales ([§ 4.1](#41-períodos)) |
| `notas` | `true` por defecto |

| `tipo` | Ids | Parámetros requeridos | `vista` |
|---|---|---|---|
| `macro` | `IEA*`, `IEM*`, `TOU*`, `VAB*`, `VAA*`, `VAP*` | — | `grupos` |
| `sistema` | `SFN01`–`SFN05`, `CAR04`, `CAR05`, `TPE01` | `sector` | `grupos` |
| `balances` | `SFN06` | `sector`, `analisis` | `arbol` |
| `cartera` | `CAR01`–`CAR03`, `TEA01` | `sector`, `credito` | `grupos` |
| `entidad` | `EFI01`–`EFI05`, `EFI10`–`EFI13`, `TPE02` | `entidad` | `grupos` |
| `balancesEntidad` | `EFI06` | `entidad`, `analisis` | `arbol` |
| `carteraEntidad` | `EFI07`–`EFI09`, `TEA02` | `entidad`, `credito` | `grupos` |

- Un parámetro que **no aplica** al tipo (p. ej. `sector` en un EFI) devuelve 400. **Hay que mandar
  solo los que corresponden**; `controlesDe(tipo)` de `cargarCuadro.ts` ya sabe cuáles son.
- `nivel`: en `grupos` es el último `NivelN` con texto; en `arbol` es la columna `Nivel` (1–4).
- `padre`: en `arbol` es el `indice` del padre, calculado con el mismo algoritmo de pila que
  `TablaBalances.tsx`; en `grupos` es `null`.
- `clave`: es estable entre exportaciones (`{cuadro}|{contexto}|{cuc}`, o `…|{grupo}|{variable}`
  con `#n` si se repite). Sirve de id para la colección y el carrito.
- `notas`: párrafos ya separados. En los balances viene `[]`.

```
GET /api/cuadros/IEA111A?desde=2015&hasta=2025
GET /api/cuadros/SFN01?sector=seg1&desde=2024-01
GET /api/cuadros/SFN06?sector=nacional&analisis=vertical
GET /api/cuadros/TEA01?sector=nacional&credito=productivo
GET /api/cuadros/EFI06?entidad=BP__PICHINCHA&analisis=saldo
GET /api/cuadros/EFI08?entidad=BP__PICHINCHA&credito=productivo
```

### 5.6 `GET /api/entidades/{id}/reporte` → `ReporteApi`

| Query | Descripción |
|---|---|
| `codigos` | CUC o Variable separados por coma. Opcional: sin él vienen las 776 cuentas |
| `desde`, `hasta` | Opcionales (mensual) |

- Cada código se busca por `CUC` y, si no aparece, por `Variable`, sin distinguir mayúsculas.
  Por eso "Percentil 75" encuentra `perc75_turb`.
- Un código inexistente se omite y se lista en `X-Codigos-No-Encontrados`.
- La respuesta completa pesa unos 170 KB comprimida. **Pedirla completa una vez por entidad** y cachearla.

### 5.7 `GET /api/rankings` → `RankingApi`

| Query | Requerido | Descripción |
|---|---|---|
| `cuenta` | ✅ | CUC: `@1`, `@2`, `@14`, `monto_total`, `mop`… (cualquiera del reporte) |
| `fecha` | ✅ | Corte `YYYY-MM` |
| `agrupacion` | ✅ | `sector` (mismo Tamaño) \| `activos` (mismo Rango_Activos) \| `provincia` (mismo DPA_PR) \| `todas` |
| `entidad` | ✅ salvo `todas` | Entidad de referencia |

- Replica `renderRankingGenerico`, con el filtro de provincia ya corregido.
- `fechaComparacion` = `fecha` − 12 meses. Un valor nulo cuenta como 0.
- Las filas vienen ordenadas por `participacionActual` descendente; `posicion` va de 1 a n.
- Tarda unos 60 ms. Hoy, en el navegador, tarda ~10 s y descarga 296 MB.

### 5.8 `GET /api/entidades/series` → `SeriesEntidadesApi` (hoja 31)

| Query | Descripción |
|---|---|
| `ids` | Hasta 4 entidades separadas por coma (principal + 3) |
| `codigos` | CUC/Variable, máximo 50 |
| `desde`, `hasta` | Opcionales |

`periodos` es la unión de los períodos de las entidades pedidas; donde a una le falta un período va `null`.
Los códigos que no existen van en `X-Codigos-No-Encontrados`.

### 5.9 `GET /api/sistema/series` → `SeriesSistemaApi` (hoja 2)

| Query | Descripción |
|---|---|
| `codigos` | CUC de `base_estru_sistema` (p. ej. `@1,@2,@3,Gan_Eje`) |
| `sectores` | Ids de sector (opcional; sin él, los 10) |
| `cuadros` | Restringe `Cuadro` (opcional; p. ej. `SFN01,SFN02`) |
| `desde`, `hasta` | Opcionales |

Por cada sector y código se toma la primera fila, en el orden del archivo, cuyo CUC coincide.

### 5.10 `GET /api/meta` → `MetaApi`

Reemplaza el "Datos actualizados" del hub, que hoy muestra la hora actual:

- `versionDatos` es la fecha de modificación más reciente de los archivos base.
- `ultimoCorteComun` (hoy `2026-07`) es el último mes con datos a la vez en reportes, balances y sistema.
  Es buena candidata a fecha de corte por defecto de la revista.

### 5.11 `GET /api/health` (sin token)

Responde `{ "status": "Healthy" | "Degraded" | "Unhealthy", "detalle": "..." }`.

---

## 6. Implementación en el frontend

La idea: **solo cambian `src/services/` y los cargadores**. Los adaptadores convierten la
respuesta de la API a la forma "ancha" que ya usan los componentes (`FilaCuadro`, `FilaReporte`:
metadatos + una clave por período). Así no se tocan `TablaCuadro`, `TablaBalances`, las gráficas,
las descargas, la colección ni el carrito. El carrito guardado en `localStorage` sigue siendo compatible.

### 6.1 `src/services/apiDatos.ts` (nuevo)

```ts
import api from "@/interceptors/authInterceptor";
import type {
  Catalogos, CuadroApi, CuadroResumen, Entidad, MetaApi, RankingApi,
  ReporteApi, SeriesEntidadesApi, SeriesSistemaApi,
} from "@/types/api";

/** Quita los parámetros vacíos: mandar uno que no aplica al cuadro es 400. */
const limpiar = (p: Record<string, string | number | boolean | undefined | null>) =>
  Object.fromEntries(Object.entries(p).filter(([, v]) => v !== undefined && v !== null && v !== ""));

const get = async <T>(url: string, params?: Record<string, unknown>) =>
  (await api.get<T>(url, { params: params && limpiar(params as never) })).data;

/** Cachea la PROMESA por URL, como `leerJson` hoy (un fallo no queda cacheado). */
const cache = new Map<string, Promise<unknown>>();
const getCache = <T>(url: string, params?: Record<string, unknown>): Promise<T> => {
  const clave = url + JSON.stringify(params ?? {});
  let p = cache.get(clave) as Promise<T> | undefined;
  if (!p) {
    p = get<T>(url, params);
    p.catch(() => cache.delete(clave));
    cache.set(clave, p);
  }
  return p;
};

export const apiCatalogos = () => getCache<Catalogos>("/catalogos");
export const apiEntidades = (f?: { q?: string; tamano?: string; rango?: string; provincia?: string }) =>
  getCache<Entidad[]>("/entidades", f);
export const apiEntidad = (id: string) => getCache<Entidad>(`/entidades/${encodeURIComponent(id)}`);
export const apiCuadros = (origen?: string) => getCache<CuadroResumen[]>("/cuadros", { origen });

export interface FiltrosCuadro {
  sector?: string; entidad?: string; analisis?: string; credito?: string;
  desde?: string; hasta?: string; notas?: boolean;
}
export const apiCuadro = (id: string, f: FiltrosCuadro = {}) =>
  getCache<CuadroApi>(`/cuadros/${encodeURIComponent(id)}`, { ...f });

/** Completo una vez por entidad (≈ 170 KB) y cacheado. */
export const apiReporte = (entidadId: string) =>
  getCache<ReporteApi>(`/entidades/${encodeURIComponent(entidadId)}/reporte`);

export const apiRanking = (p: { cuenta: string; fecha: string; agrupacion: string; entidad?: string }) =>
  getCache<RankingApi>("/rankings", p);

export const apiSeriesEntidades = (ids: string[], codigos: string[], desde?: string, hasta?: string) =>
  get<SeriesEntidadesApi>("/entidades/series", { ids: ids.join(","), codigos: codigos.join(","), desde, hasta });

export const apiSeriesSistema = (codigos: string[], cuadros?: string[], sectores?: string[]) =>
  getCache<SeriesSistemaApi>("/sistema/series", {
    codigos: codigos.join(","), cuadros: cuadros?.join(","), sectores: sectores?.join(","),
  });

export const apiMeta = () => getCache<MetaApi>("/meta");
```

### 6.2 `src/services/adaptadores.ts` (nuevo): de la API a la forma "ancha"

```ts
import type { CuadroApi, ReporteApi, SeriesSistemaApi } from "@/types/api";
import type { CuadroCargado, FilaCuadro } from "@/modulos/Explorador/tipos";
import type { FilaReporte } from "@/modulos/Analisis/datos";

/** { "2025-01": v1, "2025-02": v2, ... } */
const columnas = (periodos: string[], valores: (number | null)[]) =>
  Object.fromEntries(periodos.map((p, i) => [p, valores[i]]));

/** Cuadro de la API -> CuadroCargado que ya pinta el Explorador. */
export const aCuadroCargado = (c: CuadroApi): CuadroCargado => {
  const ctx = c.contexto;
  const partes = [ctx.sectorNombre ?? ctx.entidadNombre, ctx.analisisNombre ?? ctx.creditoNombre]
    .filter(Boolean) as string[];
  return {
    id: c.id,
    titulo: c.titulo,
    // Misma composición que hoy hace cargarCuadroSistema.
    unidad: [c.unidad, ...partes.slice(1), ctx.entidadNombre].filter(Boolean).join(" - "),
    notas: c.notas,
    vista: c.vista,
    sector: partes.join("|") || undefined,
    sectorNombre: partes.join(" · ") || undefined,
    filas: c.filas.map<FilaCuadro>((f) => ({
      Cuadro: c.id,
      Titulo_Cuadro: c.titulo,
      Unidad: c.unidad ?? undefined,
      Grupo: f.grupo ?? undefined,
      Variable: f.variable ?? undefined,
      CUC: f.cuc ?? undefined,
      Codigo_Base: f.codigoBase ?? undefined,
      Nivel: f.nivel,
      _nivel: f.nivel,        // nivelFila() lo usa directamente
      _clave: f.clave,        // id estable (colección / carrito)
      _padre: f.padre,        // jerarquía ya calculada (opcional usarla)
      ...columnas(c.periodos, f.valores),
    })),
  };
};

/** Reporte de la API -> FilaReporte[] (lo que hoy leen la revista y sus hojas). */
export const aFilasReporte = (r: ReporteApi): FilaReporte[] =>
  r.cuentas.map((c, i) => ({
    CUC: c.cuc,
    Variable: c.variable ?? undefined,
    // R6: los metadatos de la entidad iban en la primera fila; se repiten para que infoDe() funcione igual.
    ...(i === 0 ? { Tamaño: r.entidad.tamano ?? undefined, Rango_Activos: r.entidad.rango ?? undefined, DPA_PR: r.entidad.provincia ?? undefined } : {}),
    ...columnas(r.periodos, c.valores),
  }));

/** Series del sistema -> filas como las de base_estru_sistema (hoja 2). */
export const aFilasSistema = (s: SeriesSistemaApi) =>
  s.series.map((x) => ({
    Cuadro: x.cuadro,
    Filtro: x.sectorNombre,
    CUC: x.codigo,
    Variable: x.variable ?? undefined,
    ...columnas(s.periodos, x.valores),
  }));
```

> Los valores `null` llegan como `null`, no como `"-"` ni `""`. `numero()` y `fmtValor()` de
> `Explorador/datos.ts` ya los tratan como "sin dato". En la revista, `num()` los vuelve 0,
> igual que hoy.

### 6.3 Mapeo de créditos

`CREDITOS` en `Sistema/cargarCuadro.ts` usa el texto como `value`. Hay dos opciones:

- **Recomendado:** cambiar los `value` a los ids de la API (`total`, `productivo`, `consumo`,
  `inmobiliario`, `vip`, `educativo`, `microcredito`), o cargarlos de `/api/catalogos`.
- **Mínimo:** traducir al llamar:

  ```ts
  const ID_CREDITO: Record<string, string> = {
    "Cartera Total": "total", Productivo: "productivo", Consumo: "consumo",
    Inmobiliario: "inmobiliario", "Vivienda interés Público y Social": "vip",
    Educativo: "educativo", Microcrédito: "microcredito",
  };
  ```

---

## 7. Cambios por pantalla

| Pantalla | Hoy | Con la API |
|---|---|---|
| Login, Dashboard | Supabase | Sin cambios (solo el interceptor, [§ 3](#3-autenticación-lo-primero-que-hay-que-cambiar)) |
| Macro | 3 archivos base + notas (≈ 19 MB) | `apiCuadro(id)` |
| Sistema / Tasas: filtros | `entidades_lista.json` | `apiEntidades()`, `apiCatalogos()` |
| Sistema / Tasas: cuadros | Archivos base o por entidad (hasta 66 MB) | `apiCuadro(id, { sector \| entidad, analisis, credito })` |
| Análisis: hub | `entidades_lista` + 1 reporte | `apiEntidades()`, `apiEntidad(id)`, `apiMeta()` |
| Análisis: hojas 1, 3, 4, 6–8, 10–23, 27–30 | 1 reporte | `apiReporte(id)` |
| Análisis: hoja 2 | `base_estru_sistema` (13 MB) | `apiSeriesSistema(["@1","@2","@3","Gan_Eje"], ["SFN01","SFN02"])` |
| Análisis: hojas 5, 9, 24–26 | 229 reportes (≈ 296 MB) | `apiRanking({ cuenta, fecha, agrupacion, entidad })` |
| Análisis: hoja 31 | 229 reportes + hasta 3 | `apiEntidades({ tamano, provincia, rango })` + `apiSeriesEntidades(...)` |

### 7.1 Macro: `src/modulos/Macro/Macroeconomico.tsx`

```ts
import { apiCuadro } from "@/services/apiDatos";
import { aCuadroCargado } from "@/services/adaptadores";

const cargarCuadro = async (id: string): Promise<CuadroCargado> => aCuadroCargado(await apiCuadro(id));
```

Se eliminan `ARCHIVOS`, `cargarTodo` y la lectura de `base_notas.json`.

### 7.2 Sistema y Tasas: `src/modulos/Sistema/cargarCuadro.ts`

```ts
import { archivoEntidad } from "@/services/datosService";
import { apiCuadro, apiEntidades } from "@/services/apiDatos";
import { aCuadroCargado } from "@/services/adaptadores";

export const cargarCuadroSistema = async (tipo: TipoCuadro, id: string, f: Filtros): Promise<CuadroCargado> => {
  const c = controlesDe(tipo);
  const cuadro = await apiCuadro(id, {
    sector: c.sector ? f.sector : undefined,
    entidad: c.entidad ? archivoEntidad(f.entidad) : undefined,   // "BP. PICHINCHA" -> "BP__PICHINCHA"
    analisis: c.analisis ? f.analisis : undefined,
    credito: c.credito ? ID_CREDITO[f.credito] ?? f.credito : undefined,
  });
  return aCuadroCargado(cuadro);
};

export const cargarEntidades = async () =>
  (await apiEntidades()).map((e) => ({ value: e.nombre, label: e.nombre }));
```

El filtro por rango de fechas del Explorador puede seguir haciéndose en el cliente
(`rangoPorDefecto`) o pasarse como `desde`/`hasta` para bajar menos datos.

### 7.3 Análisis: `resumenEntidades.ts`, `Revista.tsx`, `Analisis.tsx`

```ts
// resumenEntidades.ts
export const cargarListaEntidades = async (): Promise<EntidadLista[]> =>
  (await apiEntidades()).map((e) => ({ id: e.nombre, nombre: e.nombre, archivo: e.id }));

// Revista.tsx / Analisis.tsx / indicadores.tsx: donde hoy se hace leerJson(rutaReporte(x))
const cargarReporte = async (entidad: string) => aFilasReporte(await apiReporte(archivoEntidad(entidad)));
const cargarInfo = async (entidad: string) => infoDe(await cargarReporte(entidad));
// (o, más liviano para el hub: apiEntidad(archivoEntidad(entidad)) trae tamano/rango/provincia)
```

`Ctx`, `valor()`, `fila()` y todas las hojas siguen igual, porque reciben `FilaReporte[]` con la misma forma.

### 7.4 Rankings (hojas 5, 9, 24–26): `paginas/rankings.tsx`

Se reemplaza el cálculo local sobre `cargarResumenEntidades` por la API:

```ts
const agrupacion = filtro;   // "sector" | "activos" | "provincia"
const { data: r, isLoading } = useService(
  () => apiRanking({ cuenta: c.cuenta, fecha: ctx.fecha, agrupacion, entidad: archivoEntidad(entidad) }),
  [c.cuenta, ctx.fecha, agrupacion, entidad], [], true, "No se pudo cargar el ranking");

const ranking = (r?.filas ?? []).map((f) => ({
  entidad: f.nombre,
  anterior: f.anterior,
  actual: f.actual,
  partAnterior: f.participacionAnterior,
  partActual: f.participacionActual,
  posicion: f.posicion,
}));
const posicion = r?.posicionEntidad ?? 0;
const totales = r?.total ?? { anterior: 0, actual: 0 };
```

- `fechaComparacion` de la API equivale a `ctx.anioAnterior`.
- El treemap y la tabla no cambian.

### 7.5 Comparativo (hoja 31): `paginas/indicadores.tsx`

- Lista de candidatas: `apiEntidades({ tamano, provincia, rango })`, en lugar de filtrar `cargarResumenEntidades()`.
- Datos de la entidad principal + hasta 3 más:

  ```ts
  apiSeriesEntidades([principal, ...extras].map(archivoEntidad), CODIGOS)
  ```

- Si una pantalla necesita la entidad adicional como `FilaReporte[]` (hoy `cargarAdicional`),
  alcanza con `aFilasReporte(await apiReporte(id))`.
- Con esto se puede **borrar `cargarResumenEntidades`**, que era el barrido de los 229 reportes.

### 7.6 Hoja 2: `paginas/balance.tsx`

```ts
const cargarSectores = async () =>
  aFilasSistema(await apiSeriesSistema(["@1", "@2", "@3", "Gan_Eje"], ["SFN01", "SFN02"]));
```

Devuelve filas `{ Cuadro, Filtro, CUC, Variable, "YYYY-MM": valor }`, como `FilaSistema`.
Se descargan unos KB en lugar de 13 MB.

### 7.7 Hub: "Datos actualizados"

```ts
const { data: meta } = useService(apiMeta, [], [], true, "");
// meta.versionDatos: fecha real de los datos (ISO UTC)
// meta.ultimoCorteComun: mes de corte por defecto sugerido (hoy "2026-07")
```

### 7.8 Limpieza al terminar

- Quitar `public/data/` del build y `DATA_BASE_URL` de `routes.json`.
- Quitar `leerJson`, `leerJsonSinCache` y `rutaReporte` de `datosService.ts` cuando ya nadie los use.
  `archivoEntidad` se puede conservar mientras las pantallas manejen nombres.
- Opcional, más adelante: usar `clave` (`_clave`) como id de la colección y el carrito, en lugar
  de `cuadro_sector_variable_indice`.

---

## 8. Diferencias con el contrato v1.0

| Punto | Contrato | Implementado | Motivo |
|---|---|---|---|
| Criterio #2 `SFN01?sector=nacional` | 70 períodos (hasta 2027-02) | **69** (hasta 2027-01) | En `base_estru_sistema` solo `TPE01` llega a 2027-02. La API devuelve la unión de los períodos de las filas pedidas, como el frontend (`unionPeriodos`) |
| Runtime | .NET 10 | .NET 8 LTS | Runtime disponible; se cambia en una línea del `.csproj` |
| `/api/sistema/series` | `{ sector, sectorNombre, codigo, valores }` | Además `cuadro` y `variable` | Campos extra, no rompen nada |
| `/api/cuadros` en cuadros por entidad | `periodos` | Rango de la primera entidad del catálogo | Cada entidad cubre un rango distinto |

Los demás criterios de aceptación (§13 del contrato) se cumplen y están cubiertos por los tests
del backend (`dotnet test tests/BackendDF.Tests`).

---

## 9. Checklist de integración

- [ ] Interceptor envía `Authorization: Bearer <access_token de Supabase>` ([§ 3.2](#32-cambio-en-srcinterceptorsauthinterceptorts))
- [ ] Al recibir 401: `signOut()` y redirección a `/login`
- [ ] `src/types/api.ts`, `src/services/apiDatos.ts`, `src/services/adaptadores.ts`
- [ ] Créditos con ids de la API ([§ 6.3](#63-mapeo-de-créditos)); entidades con `archivoEntidad(nombre)`
- [ ] Macro → `apiCuadro`
- [ ] Sistema / Tasas → `apiCuadro` con solo los parámetros que aplican
- [ ] Revista y hub → `apiReporte` / `apiEntidad`
- [ ] Rankings → `apiRanking`
- [ ] Hoja 2 → `apiSeriesSistema`
- [ ] Hoja 31 → `apiEntidades` + `apiSeriesEntidades`
- [ ] Hub → `apiMeta` para "Datos actualizados"
- [ ] Se retira `public/data` del despliegue
- [ ] Prueba con auth habilitada (fuera de Development) contra el proyecto Supabase real
