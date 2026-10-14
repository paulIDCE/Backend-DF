# Propuesta: servir los cuadros geográficos en la API

> 2026-10-13 · Para revisar con frontend (contrato de la API) y con quien genera los JSON (calidad de datos).
> Contexto: los JSON de oct-2026 trajeron 11 cuadros nuevos. Comparación completa en [`json/comparacion.md`](json/comparacion.md).
> Contrato vigente: [`BackendDF/docs/GUIA_CONSUMO_API_FRONTEND.md`](../BackendDF/docs/GUIA_CONSUMO_API_FRONTEND.md) §5.

## 1. Qué llegó

| Ámbito | Cuadro | Contenido | Sub-vistas (`ID` en el JSON) | Medidas (`Variable`) | Períodos |
|---|---|---|---|---|---|
| Sector | **GEO01** | Colocaciones por provincia | Total, Productivo, Consumo, Inmobiliario, Viv. Int. Pub. Soc., Educativo, Microcrédito | Saldo, Vencer, Vencida, Devengada | mensual 2021-05 → 2026-08 |
| Sector | **GEO02** | Colocaciones por cantón | Igual que GEO01 | Igual que GEO01 | Igual que GEO01 |
| Sector | **GEO03** | Captaciones por provincia | Total, Dep. Ahorro, Dep. Cuenta Básica, Dep. Cuenta Corriente, Dep. Plazo, Otros Depósitos | Saldo, Clientes, Cuentas | mensual 2021-05 → 2026-08 |
| Sector | **GEO04** | Captaciones por cantón | Igual que GEO03 | Igual que GEO03 | Igual que GEO03 |
| Sector | **PDA01** | Puntos de atención por provincia | Total, Cajero Automático, Corresponsales Solidarios, Corresponsales no bancarios, Oficinas | (una por provincia) | **trimestral** 2019-03 → 2026-06 (30 cortes) |
| Sector | **PDA02** | Puntos de atención por cantón | Igual que PDA01 | (una por cantón) | Igual que PDA01 |
| Entidad | **GEE01** | Colocaciones de la entidad por provincia | Igual que GEO01 | Igual que GEO01 | Igual que GEO01 |
| Entidad | **GEE03** | Captaciones de la entidad por provincia | Igual que GEO03 | Igual que GEO03 | Igual que GEO03 |
| Entidad | **GEE04** | Captaciones de la entidad por cantón (⚠ mezclado, ver §4) | 12 sub-vistas | — | Igual que GEO03 |
| Entidad | **PDE01** | Puntos de atención de la entidad por provincia | Igual que PDA01 | — | Igual que PDA01 |
| Entidad | **PDE02** | Puntos de atención de la entidad por cantón | Igual que PDA01 | — | Igual que PDA01 |

- Los cuadros de sector traen los mismos 10 sectores de siempre.
- GEE01, GEE03 y GEE04 existen para las 229 entidades; PDE01 y PDE02 para 228.
- **No salen del `.sav`**: se sirven desde JSON y ya conservan sus fechas propias (paso 2, hecho).

## 2. Por qué hoy no se pueden consultar

- El backend clasifica los cuadros por el archivo del que vienen. GEO y PDA quedan como tipo `sistema`, que solo acepta `sector`.
- Sus filas vienen separadas por sub-vista (`ID`). Una consulta sin sub-vista no encuentra filas y devuelve el cuadro vacío.
- GEE y PDE quedan como `carteraEntidad` (`entidad` + `credito`). Solo responden los créditos cuyo texto coincide con el catálogo actual: productivo, consumo, inmobiliario, educativo y microcrédito. No funcionan `total`, `vip`, ningún tipo de depósito ni ningún tipo de punto de atención.

## 3. Propuesta de contrato

### 3.1 Dos tipos nuevos y parámetros por cuadro

| `tipo` | Ids | Parámetros requeridos | `vista` |
|---|---|---|---|
| `geografico` | GEO01, GEO02 | `sector`, `credito` | `grupos` |
| `geografico` | GEO03, GEO04 | `sector`, `deposito` | `grupos` |
| `geografico` | PDA01, PDA02 | `sector`, `punto` | `grupos` |
| `geograficoEntidad` | GEE01, GEE02* | `entidad`, `credito` | `grupos` |
| `geograficoEntidad` | GEE03, GEE04 | `entidad`, `deposito` | `grupos` |
| `geograficoEntidad` | PDE01, PDE02 | `entidad`, `punto` | `grupos` |

\* GEE02 cuando llegue (§4).

- **El frontend no necesita una regla nueva.** `GET /api/cuadros` ya devuelve `parametros` **por cuadro**, así que solo tiene que leerlo, como hoy con `controlesDe(tipo)`.
- **Se mantiene la regla del 400:** un parámetro que no aplica al cuadro, o uno requerido que falta, responde 400.

### 3.2 Valores de los parámetros

**`credito`:** se reutilizan los **mismos ids** de hoy. El backend traduce cada id al texto de cada familia de cuadros, así que el frontend no ve las diferencias de texto del origen.

| id | Cartera (CAR/EFI/TEA, hoy) | Geográficos (GEO/GEE) |
|---|---|---|
| `total` | Cartera Total | Total |
| `productivo` | Productivo | Productivo |
| `consumo` | Consumo | Consumo |
| `inmobiliario` | Inmobiliario | Inmobiliario |
| `vip` | Vivienda interés Público y Social | Viv. Int. Pub. Soc. |
| `educativo` | Educativo | Educativo |
| `microcredito` | Microcrédito | Microcrédito |

**Nuevos `deposito` y `punto`**, publicados en `GET /api/catalogos`:

| `deposito` | Texto origen | | `punto` | Texto origen |
|---|---|---|---|---|
| `total` | Total | | `total` | Total |
| `ahorro` | Dep. Ahorro | | `cajero` | Cajero Automático |
| `basica` | Dep. Cuenta Básica | | `solidario` | Corresponsales Solidarios |
| `corriente` | Dep. Cuenta Corriente | | `cnb` | Corresponsales no bancarios |
| `plazo` | Dep. Plazo | | `oficina` | Oficinas |
| `otros` | Otros Depósitos | | | |

### 3.3 Cambios en los tipos TypeScript (§5 de la guía)

```ts
export interface Catalogos {
  // …lo de hoy…
  depositos: Item[];        // nuevo
  puntosAtencion: Item[];   // nuevo
}

export type TipoCuadroApi = /* …lo de hoy… */ | "geografico" | "geograficoEntidad";

export interface CuadroResumen {
  // …
  parametros: ("sector" | "entidad" | "analisis" | "credito" | "deposito" | "punto")[];
}

export interface CuadroApi {
  // …
  contexto: {
    // …lo de hoy…
    deposito: string | null; depositoNombre: string | null;   // nuevo
    punto: string | null;    puntoNombre: string | null;      // nuevo
  };
}

// Opcional (ver 3.4): solo en cuadros geográficos
export interface FilaApi {
  // …
  geo?: { nivel: "provincia" | "canton"; codigo: string; nombre: string; provincia: string };
}
```

### 3.4 Filas: cómo vienen y qué agregaríamos para mapas

Así viene cada fila hoy en el JSON:

| Cuadro | `grupo` | `cuc` | `variable` |
|---|---|---|---|
| GEO01 / GEO03 (provincia) | `01 - Azuay` | `01 - Azuay` | la medida: `Saldo`, `Vencer`… |
| GEO02 / GEO04 (cantón) | `01 - Azuay` (GEO04) o el cantón (GEO02) | `0101 - CUENCA` | la medida |
| PDA01 (provincia) | el tipo de punto (`Total`…) | `01 - Azuay` | la provincia |
| PDA02 (cantón) | la provincia | `0101 - Cuenca` | el cantón |

**Propuesta:** el backend agrega un campo opcional `geo` que separa el código DPA del nombre. Ejemplo: `{ nivel: "canton", codigo: "0101", nombre: "Cuenca", provincia: "01" }`.

Así el frontend puede pintar mapas o cruzar con su capa geográfica sin tener que partir el texto `"0101 - CUENCA"`. Además, el backend unificaría el `grupo` de los cuadros cantonales como **provincia** en todos, con lo que la vista `grupos` agrupa por provincia.

### 3.5 Ejemplos

```
GET /api/cuadros/GEO01?sector=nacional&credito=total
GET /api/cuadros/GEO04?sector=seg1&deposito=ahorro
GET /api/cuadros/PDA02?sector=grande&punto=cajero
GET /api/cuadros/GEE01?entidad=BP__PICHINCHA&credito=consumo
GET /api/cuadros/PDE01?entidad=BP__PICHINCHA&punto=oficina
```

## 4. Problemas en los JSON (para quien los genera)

| # | Problema | Evidencia | Qué hace el backend mientras tanto |
|---|---|---|---|
| D1 | GEO01‑04 y PDA01‑02 **duplicados** en `base_estru_sistema.json` y `base_cartera.json` (65 124 filas idénticas) | La comparación muestra +65 124 filas en ambos archivos | Usa la copia de `base_estru_sistema.json` y avisa en `/api/meta` |
| D2 | **Falta GEE02** (colocaciones por cantón de la entidad) | Ningún archivo `entidades/*.json` lo trae | — |
| D3 | **GEE04 mezcla** captaciones y colocaciones por cantón | 12 sub-vistas. Las 384 filas de colocación traen columnas extra (`name_efi`, `name_cant`); `Total` aparece dos veces (660 filas = 384 + 276) | Sirve solo las filas de captación (las que no traen `name_efi`). Las colocaciones por cantón de la entidad quedan sin publicar hasta que llegue GEE02 |
| D4 | **Unidad errónea:** todos dicen `Número` | GEO01 "Saldo" Azuay = 2 789,58 (millones USD); GEO03 mezcla `Clientes`/`Cuentas` (número) con `Saldo` (millones) | Sin corregir: hace falta la unidad correcta por medida |
| D5 | **Nombres inconsistentes** | `El Oro` con espacio no separable (U+00A0) en GEO/GEE y normal en PDA; `Bolivar`/`Bolívar`; cantones en MAYÚSCULAS (GEO) y en Título (PDA) | El campo `geo` usa el código DPA como clave y normaliza espacios |
| D6 | **Cantones repetidos o con código ambiguo** en PDA02 | `1701 - Distrito Metropolitano De Quito` y `1701 - Quito`; `0920 - San Jacinto De Yaguachi` y `0920 - Yaguachi`; La Joya de los Sachas bajo `1505` y `2203`; `1707 - San Miguel De/de Los Bancos` | Se publican tal cual; conviene una tabla DPA oficial única |
| D7 | Meses futuros (2026‑11 → 2027‑02) en `base_estru_sistema` y `base_cartera` | Ya estaba en la versión anterior | Se informa en `/api/meta` |

## 5. Decisiones que necesitamos de frontend

1. **Parámetros explícitos** (`credito` / `deposito` / `punto`, propuesta de §3) **o uno genérico** (`desglose`, con sus valores válidos devueltos por cuadro en `GET /api/cuadros`).
   - Recomendamos explícitos: siguen el patrón actual y los ids se pueden validar.
2. **Formato de período de PDA/PDE** (trimestral, fin de trimestre):
   - **(a)** dejar `YYYY-MM` (`2019-03`, `2019-06`…), coherente con GEO/GEE y fácil de cruzar en un mapa por fecha;
   - **(b)** convertir a `YYYY-Tn` y `frecuencia: "trimestral"`, coherente con los trimestrales macro.
   - Recomendamos (a) con `frecuencia: "trimestral"`.
3. **¿Agregamos el campo `geo`** (§3.4)? Solo es necesario si habrá mapas o cruces por código DPA.
4. **Ids de `deposito` y `punto`:** ¿sirven los propuestos (§3.2)?

## 6. Implementación en el backend (después del visto bueno)

| Paso | Cambio | Archivos |
|---|---|---|
| 1 | Tipos `Geografico` y `GeograficoEntidad`, clasificados por prefijo de id (GEO/PDA/GEE/PDE) y no por archivo; se ignora la copia duplicada (D1) | `Models/Entities/CuadroFuente.cs`, `Data/FuenteJson/FuenteDatosJson.cs` |
| 2 | Parámetros por cuadro (`credito`/`deposito`/`punto`) y su validación (400) | `Logic/Dominio/TiposCuadro.cs`, `Logic/Services/CuadrosService.cs`, `Controllers/CuadrosController.cs` |
| 3 | Catálogos `depositos` y `puntosAtencion`; traducción id → texto origen por familia | `Logic/Dominio/Catalogo.cs`, `Models/DTOs/CatalogoDTOs.cs`, `CatalogosService.cs` |
| 4 | Contexto con `deposito`/`punto`; campo `geo` opcional (si se aprueba) | `Models/DTOs/CuadroDTOs.cs`, `CuadrosService.ArmarFilas` |
| 5 | GEE04: servir solo captaciones (D3) | `Data/FuenteJson` |
| 6 | Pruebas de contrato de los 11 cuadros; actualizar la guía del frontend (§5) | `tests/`, `GUIA_CONSUMO_API_FRONTEND.md` |

- **No toca la BD ni la fuente híbrida:** estos cuadros no salen del `.sav` y se sirven desde JSON, con sus fechas.
- **Esfuerzo estimado:** 1‑2 días, más el ajuste de la guía.
