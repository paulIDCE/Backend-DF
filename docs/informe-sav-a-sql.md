# Informe: de `df_sistema.sav` a SQL Server con stored procedures

> v3 · 2026-10-02 · Alcance: `BackendDF/src/BackendDF/Database/*.json` (~2,4 GB) vs `df_sistema/df_sistema.sav` (196 MB) vs la base **`idce_bco_coop`** (SQL Server 2022)
> Método: el `.sav` se decodificó con un lector propio en Python (sin dependencias) y cada familia de JSON se cruzó contra él, valor por valor. La base `idce_bco_coop` se inspeccionó en solo lectura y se cruzó contra los mismos JSON.
>
> **v3 incorpora las convenciones acordadas por el equipo (§0).** Anexo: [`anexo-segmentos-coop.csv`](anexo-segmentos-coop.csv), con las cooperativas cuyo segmento en BD no cuadra con el de los JSON.

---

## 0. Convenciones acordadas

| # | Convención | Consecuencia práctica | Estado |
|---|---|---|---|
| C1 | **Solo se migran datos desde 2024‑01.** Todo filtro de período respeta las fechas que existen en la BD (`MIN/MAX(FechaID)` de `B11`, hoy 2024‑01 → 2026‑08) | Ya no es una brecha: la historia 2021‑05 → 2023‑12 de los JSON **deja de mostrarse**, aunque el cuadro se sirva desde JSON. Los meses 2026‑09 → 2027‑01 de los JSON (sin origen) **también se recortan** | ✅ Decidido |
| C2 | **Se omiten las cuentas de los grupos 6 (contingentes) y 7 (de orden)** | EFI01/SFN01 pierden 10 filas al final del cuadro. EFI06/SFN06 pierden 208 de 1 098 cuentas (~20 % de las filas del árbol). Recomendación en §0.1 | ✅ Decidido (revisable) |
| C3 | **El segmento de la BD (`Ifi.Segmento`) es la fuente de verdad** | Los agregados *Coop. Segmento 1/2/3* se calculan con `Ifi.Segmento` y **no van a coincidir** con los JSON. Las diferencias quedan documentadas en §0.2 y en el anexo | ✅ Decidido (ver advertencia) |

> **Pendiente de confirmar:** C1 se aplica a los cuadros del sistema financiero (sistema, entidad, reportes, rankings). Para los cuadros **macro BCE** (IEA/IEM, desde 2000), que no vienen del `.sav`, se asume que conservan su propio rango.

### 0.1 Opinión sobre las cuentas 6 y 7

Dónde se usan en los JSON:

| Lugar | Filas 6/7 | Comentario |
|---|---|---|
| EFI01 / SFN01 (Estado de situación) | 10 por entidad o filtro: `@6, @61, @64, @6401‑6404, @7, @71, @74` | Bloques finales *Cuentas contingentes* y *Cuentas de orden* |
| EFI06 / SFN06 (Balance detallado) | 208 cuentas (×3: saldo, AH, AV) = 7 923 de 39 207 filas en SFN06 | Ramas 6 y 7 del árbol |
| REP01, rankings, cartera, indicadores (EFI11‑13) | **0** | No los usan |
| Código del backend | **0** referencias | — |

**Recomendación:** la omisión es razonable, porque nada crítico (reportes, rankings, indicadores) depende de estas cuentas. Aun así, conviene **cargar solo las 10 cuentas resumen** (`6, 61, 64, 6401, 6402, 6403, 6404, 7, 71, 74`):
- **Costo:** unas 10 × 327 entidades × 32 meses ≈ **105 mil filas** (+0,7 % de `B11`).
- **Ganancia:** el Estado de situación queda completo. Las *contingentes acreedoras* (avales, fianzas, cartas de crédito, créditos aprobados no desembolsados) son información que un analista sí mira y que suele entrar en indicadores de liquidez y exposición.
- **Las 198 subcuentas restantes** del árbol pueden quedar fuera. Mientras tanto, el backend oculta las ramas 6/7 en EFI06/SFN06, para no mezclar fuentes.
- **Cambio en el ETL:** el `delete … like '6%' or like '7%'` se reemplaza por `… and cuenta not in ('6','61','64','6401','6402','6403','6404','7','71','74')`.

### 0.2 Segmento de cooperativas: dónde no cuadra (C3)

**Origen de cada valor:**
- `sp_etl_bce` arma `Ifi.Segmento` a partir de la columna **`Tamaño`** del `.sav` (Segmento 1…5 / Sin Segmento → 1…5 / 0).
- Lo fija con `max()` la primera vez que ve el RUC, así que no tiene historia.
- Los agregados de los JSON salen de la otra columna del `.sav`, **`segmento`** (S1…S5), que varía por mes.
- `B11Temp` recibe esa columna pero el SP la **sobrescribe** con la de `Tamaño`.

**Agregados `@1` (activo, millones USD)**:

| Período | JSON S1 | **BD S1** | `.sav` `segmento` S1 | JSON S2 | **BD S2** | JSON S3 | **BD S3** | **BD seg. 0** |
|---|---|---|---|---|---|---|---|---|
| 2024‑01 | 21 391,6 | **2 036,8** | 21 391,6 | 2 566,6 | **3 014,0** | 1 107,8 | **3 164,9** | **16 807,9** |
| 2024‑12 | 22 246,0 | **2 055,1** | 22 246,0 | 3 038,1 | **3 334,4** | 1 194,4 | **3 579,0** | **17 415,6** |
| 2025‑06 | 23 789,6 | **2 117,1** | 23 789,6 | 3 379,6 | **3 698,7** | 1 331,5 | **3 985,3** | **18 551,4** |
| 2025‑12 | 24 417,8 | **2 143,1** | 24 417,8 | 3 702,2 | **3 825,3** | 1 427,9 | **4 076,6** | **19 338,1** |
| 2026‑07 | 24 791,2 | **2 137,1** | 24 791,2 | 3 927,0 | **3 991,4** | 1 587,5 | **4 459,4** | **19 512,3** |

Con la columna `segmento` del `.sav` se reproducen los JSON **exacto en 31/31 meses para S1, 30/31 para S2 y 28/31 para S3** (2024‑01 → 2026‑07). Con `Ifi.Segmento` no coincide **ningún** mes.

**Entidades afectadas** (2024‑01 → 2026‑08, 348 cooperativas; detalle en el anexo):

| Tipo de diferencia | Cooperativas | Ejemplos (activo 2026‑08, MUSD) |
|---|---|---|
| BD = 0 (*Sin Segmento*), pero en el `.sav` son S1/S2 | 36 | JEP (3 626), Jardín Azuayo (2 527), Alianza del Valle (1 563), 29 de Octubre (1 086), Cooprogreso, San Francisco, Andalucía, Mushuc Runa, Oscus… |
| Segmento fijo distinto al del `.sav` | 69 | Fernando Daquilema, Ambato, Chibuleo, Kullki Wasi, Erco (BD 2 / `.sav` S1); Yuyay, Imbaburapak, Provida (BD 3 / `.sav` S2)… |
| El `.sav` cambia de segmento en el período y la BD es fija | 140 | Mayormente S3↔S4↔S5, más algunas S1↔S2 (Lucha Campesina, Luz del Valle, Guaranda) |
| Coinciden todos los meses | 103 | — |

**Impacto:**
- 245 de 348 cooperativas tienen al menos un mes con segmento distinto. Representan **26 759 de 30 892 MUSD (87 %) del activo cooperativo** en 2026‑08.
- 122 de esas 245 están en el catálogo del frontend.
- El agregado *Sector Financiero Popular y Solidario* no depende del segmento. Con la convención se calcula como *todas las COOP de la BD*: 30 752,2 vs 30 100,2 en el JSON (2026‑07), +2,2 %.

**Advertencia y recomendación:** usar `Ifi.Segmento` como verdad hace que los cuadros *Coop. Segmento 1/2/3* cambien de forma **radical** respecto a lo publicado: el Segmento 1 pasa de ~24 800 a ~2 100 MUSD. Si la intención es la segmentación oficial vigente de la SEPS, la columna correcta probablemente es `segmento` del `.sav`, que ya llega a `B11Temp`. El arreglo es pequeño:
1. **No sobrescribir** `B11Temp.segmento`.
2. Guardarla **por mes** en `IfiPeriodo` (E3).
3. Opcional: mantener `Ifi.Segmento` como "segmento actual" = último `segmento` del `.sav`.

Mientras no se cambie, el backend aplica C3 y `/api/meta` lo advierte: *"Segmentos de cooperativas según BD (Tamaño); difieren de la publicación anterior; ver anexo"*.

---

## 1. Resumen ejecutivo

- El `.sav` **sí es el origen de los saldos contables**. EFI01 de BP. PICHINCHA coincide exacto (`@CUC / 1 000 000`) en 211 de 212 filas. Los agregados de sistema por tipo de entidad y por tamaño de banco también coinciden exacto.
- Lo que **no está en el `.sav`** y debía estar en el código R perdido:
  1. **Reglas de filtrado y segmentación** (qué cooperativas entran en cada agregado y en qué segmento). Desde 2024 la columna `segmento` del `.sav` las explica casi por completo (§0.2).
  2. **Fórmulas** de indicadores (CAMELS, PERLAS, IVF, márgenes, anualizados, fuentes y usos, AH/AV, crecimientos S1‑S9…).
  3. La **"plantilla" de cada cuadro**: orden de filas, títulos, `Grupo`, `Variable`, `Nivel1..7`.
- Algunas cosas **no salen del `.sav` de ninguna forma**: los cuadros macro del BCE (IEA/IEM), la cartera por rango, los depósitos y tasas pasivas por plazo. Los meses 2026‑09 → 2027‑01 de los JSON quedan fuera por C1.
- **Ya en SQL (`idce_bco_coop`)**:
  - Saldos contables (`B11`, 15,8 M filas, 2024‑01 → 2026‑08, 379 entidades).
  - Catálogo de cuentas con nombres (`Cuenta`).
  - 5 indicadores de solvencia (`IndicadorData`).

  Cuadran exacto contra los JSON: EFI01/EFI02, el saldo de EFI06, y SFN01/SFN06 para Sector Privado, Bancos por tamaño y Mutualistas.
- **Con las convenciones C1‑C3, todo lo que sea saldo contable (grupos 1‑5) por entidad o agregado se puede servir desde SQL desde ya**, incluidos los agregados de cooperativas (con la diferencia documentada en §0.2).
- **Brechas del ETL que siguen abiertas**:
  - No guarda provincia ni estado (E4). El slug de archivo **no** va en la BD: se resuelve con un mapa en el backend (E5, §7).
  - Solo 5 derivadas en `IndicadorData` (E6).
  - **Contraseña de `sa` en texto plano** en el SP (E7).
- **Recomendación:**
  - **Ya**: una fuente **híbrida** en el backend (§8). SQL para saldos e indicadores cargados; JSON para la plantilla de filas y lo no migrado (fórmulas, macro, fuentes externas). Todo se recorta a la ventana de la BD.
  - **En paralelo**: el equipo cierra E4‑E7 y migra fórmulas cuadro por cuadro.

---

## 2. Anatomía del `.sav`

| Atributo | Valor |
|---|---|
| Producto | IBM SPSS Statistics 25, compresión bytecode, UTF‑8 |
| Guardado | 29‑sep‑2026 |
| Filas | **37 346** (1 fila = entidad × mes, formato ANCHO) |
| Variables | **2 156** (2 214 segmentos) |
| Entidades | 446 (`RUC`/`Nom_Efi`), 327 activas en 2026‑08 |
| Períodos | **2007‑01 → 2026‑08** (236 meses) |
| Tipos (`Tipoentidad`) | BP 5 686 · Coop 29 283 · MU 959 · SF 1 212 · TC 191 · PU 15 |
| Value labels | ninguno |

**Grupos de columnas**

| Grupo | # | Ejemplos | Nota |
|---|---|---|---|
| Identificación | ~20 | `Nom_Efi, IFI, RUC, Tipoentidad, segmento (S1..S5), Tamaño, Estado, DPA_PR, año, mes, fecha, cod_ifi` | `Tamaño` ≠ segmento usado en los JSON |
| Cuentas contables | **1 989** | `@1, @11, @1101, @110105 …` | En **USD** (los JSON van en millones). La etiqueta es solo el código, **sin nombre** |
| Derivadas de cartera | ~60 | `Cart_bru_*, Cart_vencer_*, Cart_impr_*, Prov_cons_*` | Por segmento de crédito |
| Tasas / montos | ~70 | `t_*, tea_global, ref_*, max_*, num_*, proc, cons…` | Parte de TEA02 / EFI08 |
| Solvencia | ~15 | `PTC, SOLVENCIA, APR_*, P_T_PRIMARIO…` | EFI05 |

291 de las 1 989 cuentas valen cero en todo el período 2021+.

---

## 3. Inventario de los JSON

| Archivo / carpeta | Filas o archivos | Cuadros | Períodos |
|---|---|---|---|
| `base_anual.json` | 2 292 filas | 68 IEA | 2000‑12 → 2025‑12 (+ 222 columnas basura `...109`‑`...330`) |
| `base_mensual.json` | 2 041 | 65 IEM | 2000‑01 → 2026‑08 |
| `base_trimestral.json` | 1 094 | 62 | 2000‑T1 → 2026‑T1 |
| `base_estru_sistema.json` | 6 650 | SFN01‑08 × 10 filtros | 2021‑05 → **2027‑02** |
| `base_balances.json` | 39 207 | SFN06 (Saldo / AH / AV) | 2021‑05 → 2026‑07 |
| `base_cartera.json` | 7 051 | CAR01‑03, TEA01 | 2021‑05 → 2027‑01 |
| `base_notas.json` | — | notas por cuadro | — |
| `entidades_lista.json` | 229 | catálogo | — |
| `entidades/*.json` | 229 archivos | EFI01‑05, 07‑13, TEA02, TPE02 (~1 430 filas c/u) | 2021‑05 → 2027‑01 |
| `balances/*.json` | 330 archivos (101 fuera del catálogo) | EFI06 (1 098 cuentas × 3) | 2021‑05 → 2026‑07 |
| `reportes/*.json` | 231 archivos (229 + 2 agregados) | REP01 (776 filas) | 2021‑05 → 2026‑07 |

---

## 3bis. Lo que ya existe en SQL Server (`idce_bco_coop`)

### 3bis.1 Objetos

| Objeto | Filas | Contenido | Clave |
|---|---|---|---|
| `B11` | 15 854 668 | Saldos (`money`, USD): `FechaID, IFIID, CuentaID, Saldo` | PK clustered `(FechaID, IFIID, CuentaID)` |
| `Ifi` | 379 | `IFIID, Ruc, Nombre, TipoEntidad (BP/COOP/MU/PU), Segmento (0‑5)` | PK `IFIID` |
| `Cuenta` | 1 799 | `CuentaID, Nombre`: 1 253 con nombre real, 544 con texto genérico `cta NNNN` | PK `CuentaID` |
| `IndicadorFin` / `IndicadorData` | 5 / 51 745 | `ACT_PON_RIESGO, P_T_PRIMARIO, P_T_SECUNDARIO, PTC, SOLVENCIA` | PK `(FechaID, IFIID, IndicadorFinID)` |
| `B11Temp` | 4 006 959 | Staging que escribe el R (último lote: 2026‑01 → 2026‑08) | heap |
| `dbo.sp_etl_bce` | — | Script R comentado (lee el `.sav`, quita columnas derivadas, despivotea `@cuentas`) + T‑SQL que puebla `Ifi`, `B11` e `IndicadorData` | — |
| `dbo.fn_fecha` / `fn_fechaid` | — | `FechaID` = días desde 1950‑01‑01, fin de mes | — |

No hay claves foráneas ni vistas, y no hay SPs de lectura.

### 3bis.2 Cobertura y cruces contra los JSON

| Comprobación | Resultado |
|---|---|
| Meses en `B11` / `IndicadorData` | **2024‑01 → 2026‑08** (32 meses), entre 279 y 327 entidades por mes (2025‑07 solo 279) |
| BP. PICHINCHA `@1` 2026‑07 | `B11` 22 806 742 733 → 22 806,7427 = JSON ✔ |
| BP. PICHINCHA `@1` 2026‑08 | `B11` = `.sav` (23 202,79) ≠ JSON (22 882,61): SQL tiene el cierre corregido |
| Cuentas EFI01/EFI02 (365 códigos numéricos) | **355/355 en `B11`**; las 10 que faltan son de los grupos 6/7 |
| Cuentas EFI06 (1 098 `Codigo_Base`) | **890/890 en `B11` y en `Cuenta`, todas con nombre real**; faltan las 208 de los grupos 6/7 |
| SFN01 `@1` 2024‑01 y 2026‑07: Sector Privado, Bancos Grandes/Medianos/Pequeños, Mutualistas | **Exacto** (p. ej. Bancos Grandes 38 275,9687 vs 38 275,9688) |
| SFN01 Coop. Segmento 1/2/3 y PSyS | ❌ `Ifi.Segmento` sale de `Tamaño` y es fijo. El JSON da S1 2024‑01 = 21 391,6; SQL da 2 036,8 |
| EFI05 Patrimonio técnico | 5 indicadores en `IndicadorData` (`SOLVENCIA` ×100 en el JSON) |

### 3bis.3 Brechas del ETL actual (`sp_etl_bce`)

| # | Brecha | Efecto | Corrección |
|---|---|---|---|
| E1 | La historia empieza en 2024‑01 | **No es brecha (C1)**: la ventana de la BD manda | — |
| E2 | `delete B11Temp where cuenta like '6%' or like '7%'` | **Intencional (C2)**: faltan contingentes y cuentas de orden | Opcional: conservar las 10 cuentas resumen (§0.1) |
| E3 | `Ifi.Segmento` = `max()` derivado de `Tamaño`, sin historia | **Fuente de verdad por convención (C3)**. Difiere de los JSON en 245 coops (§0.2). Un banco que cambie de tamaño queda en el segmento de su primera carga | Documentado. Si se revisa C3: `IfiPeriodo` con el `segmento` del `.sav` por mes |
| E4 | El R descarta `DPA_PR`, `Estado` e `IFI` | El catálogo necesita provincia | Mantener esas columnas en `B11Temp` → `IfiPeriodo` |
| E5 | `Ifi` sin slug de archivo (`BP__PICHINCHA`) | El backend identifica entidades por ese id | **Resuelto en el backend**: `Data/FuenteSql/mapa-entidades.json` (229/229 entidades mapeadas). La BD no se toca |
| E6 | Solo 5 de ~145 derivadas del `.sav` pasan a `IndicadorData` | Tasas, cartera por segmento y montos (EFI07/08, TEA02) siguen solo en JSON | Agregar códigos a `IndicadorFin`; el SP ya los carga por nombre |
| E7 | **Credenciales `sa` en texto plano** en el comentario del SP; servidor `DESKTOP-TOAEF2L` fijo | Riesgo de seguridad | Quitar el script del SP, versionarlo en git con variables de entorno y usar un usuario dedicado de mínimo privilegio. **Rotar la contraseña de `sa`** |
| E8 | `Saldo` es `money` (4 decimales) | Suficiente: el JSON redondea a millones con 4 decimales | — |
| E9 | Cursores + `DBCC SHRINKFILE` en cada carga | Lento; el shrink fragmenta | Inserción por lote de meses; quitar el shrink |

---

## 4. Tabla resumen de reproducibilidad

"En SQL hoy" se refiere a `idce_bco_coop` tal como está (2024‑01 → 2026‑08). "Fuente ya" es lo que el backend leería con la estrategia híbrida de §8 desde el primer día.

| Familia JSON | Contenido | Cobertura desde `.sav` | En SQL hoy | Fuente ya | Qué falta |
|---|---|---|---|---|---|
| `base_anual / mensual / trimestral` (195 cuadros IEA/IEM) | Macro BCE | 0 % | ❌ | JSON | Fuente externa BCE → migrar desde el JSON actual |
| `base_notas` | Notas de cuadros | 0 % | ❌ | JSON | Migrar tal cual |
| `entidades_lista` | Catálogo de 229 entidades | 229/229 casan por nombre normalizado | 🟢 229/229 en `Ifi` vía `mapa-entidades.json` | JSON (catálogo) + SQL (datos) | Provincia (E4) |
| `base_estru_sistema` SFN01‑08: Privado, Bancos G/M/P, MU | Agregados | Saldos: sí | 🟢 cuadra exacto | **SQL** (filas `@CUC`) | — (6/7 fuera por C2) |
| `base_estru_sistema` SFN01‑08: SFN, PSyS, Coop S1/S2/S3 | Agregados | Saldos: sí | 🟡 calculable con `Ifi.Segmento` | **SQL** (C3) | **No cuadra con los JSON por diseño** (§0.2) |
| `base_balances` SFN06 (Saldo) | Balance agregado | Saldos: sí | 🟢 | **SQL** (todos los filtros; coops según C3) | AH/AV |
| `base_balances` SFN06 (AH/AV) | | Calculable | ❌ | JSON | Fórmula AH/AV |
| `base_cartera` CAR01‑03, TEA01 | Cartera, montos, tasas | Parcial | ❌ (E6) | JSON | CAR03 por rango necesita un insumo externo |
| `entidades/*` EFI01 | Estado de situación | **211/212 (99,5 %)** | 🟢 201/212 (sin 6/7) | **SQL** | 1 fila calculada (`pasipat`) |
| EFI02 | PyG mensual | 110/117 (94 %) | 🟢 110/117 | **SQL** + JSON (márgenes) | 7 márgenes calculados (`Mar_*`, `Gan_*`) |
| EFI03 | PyG anualizado | 0 directo (`@4101A`…) | 🟡 base en `B11` | JSON | Fórmula de anualización |
| EFI04 | Fuentes y usos | 0 directo | 🟡 base en `B11` | JSON | Fórmula (variación de saldos) |
| EFI05 | Patrimonio técnico | 7/7 | 🟢 5 en `IndicadorData` | **SQL** (5) + JSON | Escalas (/1e6, ×100) |
| EFI07 | Estructura de cartera | 102/286 (36 %) | 🟡 102 saldos | SQL (saldos) + JSON | `IF007_*`, `IF008…` (probablemente desde `Cart_*`); E6 |
| EFI08 | Monto de operaciones | 20/102 (20 %) | ❌ (E6) | JSON | `moa_*`, `monto_*` |
| EFI09 | Cartera por rango | 0/252 | ❌ | JSON | Datos por operación: no están en el `.sav` |
| EFI10 | Depósitos por plazo | 13/70 (19 %) | 🟡 13 saldos | SQL (saldos) + JSON | `OPTPE*`, `Plazo*` |
| EFI11 / 12 / 13 | Indicadores / CAMELS / PERLAS | 0 directo | ❌ | JSON | Fórmulas exactas |
| TEA02 | Tasas activas | 33/75 (44 %) | ❌ (E6) | JSON | `CF_*`, `CK_*` |
| TPE02 | Tasas pasivas por plazo | 0/7 | ❌ | JSON | Fuente externa |
| `balances/*` EFI06 (Saldo) | Balance detallado | **1 098/1 098 códigos** | 🟢 **890/890** de los grupos 1‑5, con nombre | **SQL** | — (6/7 fuera por C2) |
| `balances/*` EFI06 (AH/AV) | | Calculable | ❌ | JSON | Fórmula AH/AV |
| `reportes/*` REP01 | 776 filas por entidad | 330/776 (43 %) | 🟡 ~330 saldos | SQL (saldos) + JSON | PERLAS, CAMELS, IVF, S1‑S9, `@1SD_*`; texto de `Rango_Activos` |
| **Períodos** | JSON 2021‑05 → 2027‑01 | `.sav` 2007‑01 → 2026‑08 | 2024‑01 → 2026‑08 | **Ventana de la BD para todo** (C1), incluido lo que se sirve desde JSON | — |

**Leyenda**: 🟢 directo · 🟡 reconstruible por fórmula y validable con el oráculo · 🟠 mixto · ❌ requiere otra fuente

---

## 5. Evidencia de los cruces

### 5.1 Saldos por entidad (EFI01, BP. PICHINCHA)
- `@1` 2021‑05: `.sav` = 12 488 761 060,70 USD → JSON = **12 488,7611** (millones) ✔
- Las 211 filas con CUC presente en el `.sav` coinciden en 2023‑06 con `@CUC/1e6`.
- La única diferencia en toda la serie aparece en **2026‑08**: JSON 22 882,61 vs `.sav` 23 202,79. El `.sav` es posterior a la generación de los JSON (probablemente se reprocesó ese cierre).

### 5.2 Agregados (SFN01, `@1`, 2021‑05)

| Filtro JSON | JSON | `.sav` (suma) | Regla |
|---|---|---|---|
| Sistema Financiero Nacional | 66 925,4205 | 66 925,4205 | BP + Coop + MU (excluye SF, TC, PU) |
| Sector Financiero Privado | 48 455,1380 | 48 455,1380 | `Tipoentidad = BP` |
| Sector Financiero Popular y Solidario | 17 331,8230 | 17 331,8230 | `Tipoentidad = Coop` |
| Bancos Privados Grandes | 30 756,3820 | 30 756,3820 | `Tamaño` |
| Bancos Privados Medianos | 16 103,7039 | 16 103,7039 | `Tamaño` |
| Bancos Privados Pequeños | 1 595,0521 | 1 595,0521 | `Tamaño` |
| Coop. Segmento 1 | 14 536,0876 | 14 269,3 (`segmento`) | ❓ |

**Segmentos de cooperativas.** Se probaron dos reglas:
- **`segmento` del mismo mes**: coincide exacto en 2023‑01 y en 2025‑06, pero falla en 2021‑05 y en 2023‑06.
- **Último segmento conocido aplicado a toda la historia**: no coincide.

Conclusión: había una tabla o regla de reclasificación en el R.

**Sector Popular y Solidario.** Desde **2023‑02** el JSON queda por debajo del `.sav`. En esa fecha el número de cooperativas del `.sav` sube de 197 a 228 y luego a 297, con altas y segmentos S4/S5. El R excluía algunas entidades. En 2025‑06, PSyS del JSON (28 352,45) ≠ S1+S2+S3 del `.sav` (28 500,68), así que la exclusión no es simplemente "solo S1‑S3".

**Bancos.** Coinciden exacto hasta 2026‑07. En 2026‑08 el JSON (78 943) es menor que el `.sav` (82 906): ese mes estaba incompleto cuando se generó el JSON.

### 5.3 Balance detallado (EFI06)
Los 1 098 `Codigo_Base` de BP. PICHINCHA existen como `@codigo` en el `.sav`. Las filas AH/AV valen 0 en los primeros meses, lo que sugiere variación contra el mes anterior. Esto se puede confirmar con el oráculo.

### 5.4 REP01
Contiene 776 CUC. De ellos, 330 son saldos `@…` presentes en el `.sav`. El resto son indicadores: `efic_perlas_acum, Indic_CAMELS_1, IVF_Cuantitativo, P1_Provisiones … S1_Crec_14 … S9_Cre_Activos, @1SD_* (saldos “sin diferimiento”?)`.

`Tamaño` y `DPA_PR` vienen del `.sav`. `Rango_Activos` ("Nivel 8: 3269 Millones - 22807 Millones") viene de una tabla de rangos que no está en el `.sav`.

### 5.5 Catálogo
- Las 229 entidades casan con `Nom_Efi` tras normalizar tildes y espacios (`PEQUENA` ↔ `PEQUEÑA`, `RUMINAHUI` ↔ `RUMIÑAHUI`).
- El slug `archivo` se obtiene así: `BP. AMAZONAS` → `BP__AMAZONAS`, es decir, se sustituyen los caracteres no alfanuméricos por `_`.
- `balances/` trae 101 archivos de entidades que no están en el catálogo. El backend ya los ignora.

---

## 6. Brechas y preguntas abiertas (priorizadas)

1. **Script R completo.** En `sp_etl_bce` solo aparece la parte que carga saldos a SQL. Las reglas y fórmulas que generaban los JSON siguen sin aparecer. Hay que preguntar a quien escribió ese SP si existe el resto (repos, correo, máquina del analista); ahorraría la mayor parte de la fase 2.
2. **Segmentación de cooperativas**: resuelta por convención (C3, segmento de la BD). Queda documentado que la columna `segmento` del `.sav` reproduce los JSON y la BD no (§0.2). Decisión de negocio: ¿se mantiene C3 o se adopta `segmento`?
3. **Origen de 2026‑09 → 2027‑01** en los JSON: deja de importar, porque C1 los recorta. Solo se necesita aclararlo si alguien reclama esos meses.
4. **Criterio del catálogo** (por qué 229 entidades si `Ifi` tiene 379).
5. **Fórmulas** de CAMELS, PERLAS, IVF, EFI03/04, AH/AV y S1‑S9.
6. **Insumos externos**: macro BCE, cartera por rango (EFI09/CAR03), depósitos y tasas pasivas por plazo (EFI10/TPE02).
7. **Nombres de cuentas**: resuelto en buena parte con `Cuenta`. Quedan 544 genéricos `cta NNNN`, ninguno usado en EFI06; se completan con `Variable` de EFI06/SFN06.
8. **Seguridad**: hay credenciales de `sa` en texto plano dentro de `sp_etl_bce` (E7).

---

## 7. Arquitectura SQL: evolucionar `idce_bco_coop`, no empezar de cero

Se **conservan** `B11`, `Ifi`, `Cuenta` e `IndicadorData` y se les agrega solo lo que falta. Los nombres siguen la convención actual (PascalCase, `FechaID` en días desde 1950‑01‑01).

**Principio (acordado): la BD solo guarda datos que vienen de las fuentes (`.sav` y, a futuro, fuentes oficiales). No guarda nada derivado de los JSON ni de la presentación del frontend.** Por eso **viven en el backend**, no en la BD:

| Información | Dónde vive | Formato |
|---|---|---|
| Slug de entidad (`BP__PICHINCHA`) ↔ entidad de la BD | `Data/FuenteSql/mapa-entidades.json` (ya generado, §8.5) | `archivo → ruc, ifiId` |
| Catálogo visible (qué 229 entidades se muestran, nombre) | `entidades_lista.json`, como hoy | — |
| Plantilla de cuadros (orden, títulos, `Grupo`, `Variable`, `Nivel1..7`) | Los JSON actuales, usados solo como plantilla (§8) | — |
| Nombre del filtro de sistema ↔ regla (`TipoEntidad`, `Segmento`) | Configuración del backend (§8.4) | `appsettings` o clase estática |
| Escalas (`/1e6`, `×100`) | Backend | — |
| Macro BCE y notas | JSON, hasta tener una fuente oficial que cargar | — |

```
 df_sistema.sav ──► R/Python (sp_etl_bce) ──► B11Temp ──► Ifi / IfiPeriodo / B11 / IndicadorData / B11Agregado
                                                                         │
                                                                         ▼
                                              SPs de lectura api.* ──► FuenteDatosSql ─┐
                                                                                       ├─► FuenteDatosHibrida
 JSON (plantillas, catálogo, macro, notas) + mapa-entidades.json ──► FuenteDatosJson ──┘
```

### 7.1 Cambios al esquema (solo datos de origen)

```sql
-- Atributos por período que hoy el R descarta (provincia, estado) → E4
CREATE TABLE dbo.IfiPeriodo (
  FechaID INT NOT NULL, IFIID INT NOT NULL,
  TipoEntidad VARCHAR(8), Segmento SMALLINT, Tamano VARCHAR(40), Estado VARCHAR(20), Provincia VARCHAR(40),
  CONSTRAINT PK_IfiPeriodo PRIMARY KEY (FechaID, IFIID));

-- Agregados materializados por carga (evita sumar 15 M filas por request).
-- Por (TipoEntidad, Segmento): el backend suma los grupos que forman cada filtro.
CREATE TABLE dbo.B11Agregado (
  FechaID INT, TipoEntidad VARCHAR(8), Segmento SMALLINT, CuentaID NUMERIC(9), Saldo MONEY, Entidades INT,
  CONSTRAINT PK_B11Agregado PRIMARY KEY (FechaID, TipoEntidad, Segmento, CuentaID));

-- Control de cargas (VersionDatos / ETag)
CREATE TABLE dbo.Carga (CargaID INT IDENTITY PRIMARY KEY, ArchivoOrigen NVARCHAR(260), FechaArchivo DATETIME2,
  FechaCarga DATETIME2 DEFAULT SYSUTCDATETIME(), DesdeFechaID INT, HastaFechaID INT, Estado VARCHAR(20));

-- Índice para lectura por entidad (la PK actual empieza por FechaID)
CREATE NONCLUSTERED INDEX IX_B11_Ifi ON dbo.B11(IFIID, CuentaID, FechaID) INCLUDE (Saldo);
-- Alternativa con más volumen: CLUSTERED COLUMNSTORE sobre B11.
```

Con C3 ya no hacen falta tablas de reglas de segmentación (`FiltroRegla`, `IfiOverride` de v2).

**Decisiones**
- **`B11` se queda en USD.** La escala va en el backend, nunca en el dato.
- **`IndicadorFin` crece por configuración**: agregar `Cart_bru_*`, `t_*`, `num_*`… basta para que `sp_etl_bce` los cargue (E6). Son variables del `.sav`, así que cumplen el principio.
- **Indicadores calculados** (PERLAS, CAMELS…) se calculan en el ETL a partir de `B11` y se guardan en `IndicadorData`. Los SP solo leen.

### 7.2 Flujo de carga (sp_etl_bce v2)
1. Abrir `Carga`.
2. R/Python escribe `B11Temp` (desde 2024‑01, C1), conservando `DPA_PR` y `Estado`. Los grupos 6/7 se siguen omitiendo (C2), salvo las 10 cuentas resumen si se acepta §0.1.
3. Upsert de `Ifi` (como hoy) y de `IfiPeriodo`.
4. `B11` e `IndicadorData` por mes, como hoy pero sin cursores.
5. `B11Agregado`: `GROUP BY FechaID, TipoEntidad, Segmento, CuentaID` sobre `B11 ⋈ Ifi`.
6. Validaciones: activo = pasivo + patrimonio, número de entidades por mes, mes nuevo completo.
7. Cerrar `Carga`.

---

## 8. Estrategia híbrida en el backend: SQL donde ya hay datos, JSON para el resto

### 8.1 Idea
- Se crea `FuenteDatosHibrida : IFuenteDatos` que **envuelve** a `FuenteDatosJson` (sin cambios) y a una nueva `FuenteDatosSql`.
- Para cada consulta, la fila sale del JSON tal cual: **la plantilla** (orden, `Titulo`, `Grupo`, `Variable`, `Nivel1..7`, `Codigo_Base`).
- Si la fila tiene una cuenta numérica (`CUC = @NNNN` o `Codigo_Base`) y su cuadro, filtro e `ID` están habilitados para SQL, **los valores se reemplazan por los de `B11`** (`/1e6`).
- **Toda** fila, venga de SQL o de JSON, se recorta a la ventana de la BD (C1).
- La plantilla **nunca** pasa a la BD (principio de §7).
- Services, controllers, DTOs y frontend **no cambian**.

```
Request ─► Service ─► IFuenteDatos = FuenteDatosHibrida
                         ├─ FuenteDatosJson   (plantilla + valores de lo no migrado)
                         └─ FuenteDatosSql    (valores de B11 / B11Agregado / IndicadorData)
```

### 8.2 Qué se enruta a SQL desde el día 1 (configurable)

```jsonc
"Datos": {
  "RutaBase": "Database",
  "Sql": {
    "Habilitado": true,
    "ConnectionStringName": "BcoCoop",
    // Cuadros por entidad cuyas filas @CUC / Codigo_Base toman valores de B11
    "CuadrosEntidad": ["EFI01", "EFI02", "EFI05", "EFI06", "EFI07", "EFI10", "REP01"],
    // Solo la fila de saldo; AH/AV siguen en JSON
    "IdsBalance": ["Saldo Millones USD"],
    // Agregados (SFN01..08, SFN06): todos los filtros; las coops usan Ifi.Segmento (C3)
    "FiltrosSistema": "*",
    // C1: la ventana sale de la BD (MIN/MAX FechaID de B11); no se configura a mano
    "VentanaDesdeBd": true,
    // C2: grupos de cuenta que no existen en la BD → la fila se oculta
    "GruposOmitidos": ["6", "7"]
  }
}
```

Cada cuadro se agrega a la lista **cuando su paridad (§9) llega al 100 %**, salvo las diferencias aceptadas por convención (agregados de coops). Así se migra sin big‑bang y se puede volver atrás editando la configuración.

### 8.3 Reglas de mezcla
- **Ventana (C1)**: el eje de períodos de **todas** las respuestas del sistema financiero es `[MIN(FechaID), MAX(FechaID)]` de `B11`, hoy 2024‑01 → 2026‑08. Se aplica también a los cuadros que siguen en JSON (EFI03/04, EFI11‑13, AH/AV, REP01 indicadores…). Así no hay series que empiecen en 2021 junto a otras que empiezan en 2024, ni meses 2026‑09+. Excepción: macro BCE (pendiente de confirmar, §0).
- **Dentro de la ventana**: para filas migradas manda SQL (incluye el 2026‑08 corregido). Para filas no migradas, el valor del JSON; si el JSON no tiene ese mes, `NaN`.
- **Cuentas 6/7 (C2)**: las filas `@6…`/`@7…` y las ramas 6/7 del árbol EFI06/SFN06 **se ocultan**, para no mezclar en un mismo cuadro valores SQL con valores JSON sin actualizar. Si se cargan las 10 cuentas resumen (§0.1), esas filas vuelven automáticamente.
- **Segmento (C3)**: los agregados por segmento de coops se calculan con `Ifi.Segmento`. Las filas JSON de esos filtros se usan solo como plantilla.
- **NULL ≠ 0** (R2/R3): si `B11` no tiene la fila para esa entidad y mes, el valor queda `NaN`, nunca 0.
- `/api/meta` publica las tres convenciones como advertencias fijas (ventana, cuentas 6/7 omitidas, segmentos según BD).
- `ObtenerInfoAsync` agrega `sql-b11` a `Fuentes` (desde/hasta, fecha de carga) para que `/api/meta` diga de dónde viene cada dato.
- `ObtenerVersionDatosAsync` = máximo entre la fecha de los JSON y `Carga.FechaCarga` (ETag).

### 8.4 SPs que necesita la fase híbrida (solo valores)

| SP | Parámetros | Devuelve | Uso |
|---|---|---|---|
| `api.sp_Saldos_Entidad` | `@IFIID`, `@Cuentas` (CSV o TVP) | `CuentaID, FechaID, Saldo` | EFI01/02/06/07/10, REP01 |
| `api.sp_Saldos_Agregado` | `@TiposEntidad` (CSV), `@Segmentos` (CSV o NULL = todos), `@Cuentas` | `CuentaID, FechaID, Saldo` | SFN01‑08, SFN06. El backend traduce el nombre del filtro a tipos y segmentos (tabla abajo); la BD no conoce los nombres de los filtros |
| `api.sp_Indicadores_Entidad` | `@IFIID`, `@Codigos` | `Codigo, FechaID, Valor` | EFI05 |
| `api.sp_Saldos_Todas` | `@Cuentas` | `IFIID, CuentaID, FechaID, Saldo` | Rankings (REP01 de todas); el backend traduce `IFIID → archivo` con el mapa |
| `api.sp_Ifi_Listar` | — | `IFIID, Ruc, Nombre, TipoEntidad, Segmento` | Validar el mapa al iniciar |
| `api.sp_Meta_Ventana` | — | `MIN/MAX(FechaID)` de `B11` (y, cuando exista, la última `Carga`) | Ventana C1, ETag, `/api/meta` |

Esqueleto:

```sql
CREATE OR ALTER PROCEDURE api.sp_Saldos_Entidad
  @IFIID INT, @Cuentas VARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON;
  SELECT b.CuentaID, b.FechaID, b.Saldo
  FROM dbo.B11 b
  JOIN STRING_SPLIT(@Cuentas, ',') c ON b.CuentaID = TRY_CAST(c.value AS NUMERIC(9))
  WHERE b.IFIID = @IFIID;
END
```

Hasta que exista `B11Agregado` (paso 5 de §7.2), `sp_Saldos_Agregado` agrega en vivo con `GROUP BY` sobre `B11 ⋈ Ifi`, y el backend cachea el resultado. Mapeo de filtros, **definido en el backend**:

| Filtro (texto del JSON) | Regla en `Ifi` | ¿Cuadra con el JSON? |
|---|---|---|
| Sistema Financiero Nacional (privado y eps) | `TipoEntidad IN ('BP','COOP','MU')` | ≈ (+0,6 %: incluye todas las COOP) |
| Sector Financiero Privado | `TipoEntidad = 'BP'` | ✅ exacto |
| Bancos Privados Grandes / Medianos / Pequeños | `BP` y `Segmento` 1 / 2 / 3 | ✅ exacto |
| Asociación Mutualistas… | `TipoEntidad = 'MU'` | ✅ exacto |
| Sector Financiero Popular y Solidario | `TipoEntidad = 'COOP'` | ❌ +2,2 % (C3) |
| Coop. Segmento 1 / 2 / 3 | `COOP` y `Segmento` 1 / 2 / 3 | ❌ por diseño (C3, §0.2) |
| (no publicado) | `PU`: BanEcuador, CFN, BEDE | Fuera de todos los filtros, como en los JSON |

### 8.5 Implementación en el backend (lo que se puede empezar ya)

| # | Archivo | Cambio |
|---|---|---|
| B1 | `Configuration/DatosSettings.cs` | Sección `Sql` (§8.2) |
| B2 | `appsettings*.json` | `ConnectionStrings:BcoCoop` (usuario de solo lectura, no `sa`) + `Datos:Sql` |
| B2b | `Data/FuenteSql/mapa-entidades.json` (**ya generado**) | `archivo → { ruc, ifiId, nombre }` para las 229 entidades del catálogo. Al iniciar, el backend lo valida contra `api.sp_Ifi_Listar`: si un `ifiId` no existe o su RUC no coincide, resuelve por RUC y deja una advertencia en `/meta`. Entidad sin mapeo → se sirve desde JSON. Hay que marcarlo `CopyToOutputDirectory` (o recurso embebido) |
| B3 | `Data/FuenteSql/FuenteDatosSql.cs` | Llama a los SP de §8.4 con `Microsoft.Data.SqlClient` (+ Dapper opcional) y devuelve diccionarios `(cuenta → periodo → valor)` |
| B4 | `Data/FuenteHibrida/FuenteDatosHibrida.cs` | Implementa `IFuenteDatos`: delega en JSON y sustituye `Periodos/Valores` según §8.3. Reutiliza el eje `PoolTextos` y `Periodos.Union` |
| B5 | `Configuration/ServiceCollectionExtensions.cs` | Si `Datos:Sql:Habilitado`, registrar `IFuenteDatos` = `FuenteDatosHibrida`; si no, el JSON como hoy |
| B6 | `Common/Utils/FuenteDatosHealthCheck.cs` | Health también de SQL (degradado si SQL cae → servir JSON) |
| B7 | `Logic/Dominio/Periodos.cs` (uso) | Recorte del eje a la ventana de la BD (C1) en un único punto de `FuenteDatosHibrida` |
| B8 | Tests | Paridad por cuadro (§9) en modo integración |

**Fallback**: si SQL no responde, `FuenteDatosHibrida` registra un warning, agrega una advertencia en `/meta` y sirve el JSON. El sistema nunca queda peor que hoy.

### 8.6 Estado final (cuando todo esté en SQL)
Mismos SPs 1:1 con `IFuenteDatos` que proponía v1 (`sp_Entidades_Listar`, `sp_Cuadros_Listar`, `sp_Filas_Obtener`, `sp_Filas_Sistema`, `sp_Notas`, `sp_Reporte_Entidad`, `sp_Reportes_Todos`, `sp_Info_Fuentes`), con la plantilla de cuadros en el backend (un recurso versionado con el código, extraído una vez de los JSON) y `mapa-entidades.json` para los ids. En ese punto los archivos de datos JSON se retiran; quedan solo los recursos de presentación.

### 8.7 Estado de la implementación

**Fase 0 — BD.** Está instalada en `idce_bco_coop`. Scripts en [`db/fase0/`](../db/fase0/README.md):
- Esquema `api` y rol `api_lectura`.
- Índices `IX_B11_IFIID_Cuenta_Fecha`, `IX_IndicadorData_IFIID` y `UX_Ifi_Ruc`.
- Vista indexada `api.vSaldoAgregado` y vista `api.vVentana`.
- 7 procedimientos: `api.ObtenerVentana`, `ListarIfi`, `ListarCuentas`, `ObtenerSaldosEntidad`, `ObtenerSaldosEntidades`, `ObtenerSaldosAgregado`, `ObtenerIndicadoresEntidad`. Los nombres finales no llevan el prefijo `sp_`.

**Fase 1 — backend.** Se activa con `Datos:Sql:Habilitado` y la cadena `ConnectionStrings:DefaultConnection`.

| Pieza | Archivo |
|---|---|
| Capa repository, un método por SP | `Data/Repositorios/IBcoCoopRepositorio.cs`, `BcoCoopRepositorioSql.cs`, `ModelosBd.cs` |
| Mapa slug → IFIID/RUC, validado contra `api.ListarIfi` | `Data/FuenteSql/MapaEntidades.cs` + `mapa-entidades.json` |
| Sector → tipos/segmentos (C3) | `Data/FuenteSql/FiltrosSistema.cs` |
| Ventana (C1), grupos omitidos (C2), qué fila sale de SQL, escalas | `Data/FuenteSql/ReglasCuadro.cs` |
| Fuente híbrida: overlay, ventana, cachés por firma, circuito y fallback a JSON | `Data/FuenteHibrida/FuenteDatosHibrida.cs` |
| Precarga del índice de reportes al arrancar | `Data/FuenteHibrida/PrecargaHibridaHostedService.cs` |
| Health: Degraded si SQL cae | `Common/Utils/FuenteDatosHealthCheck.cs` |

**Pruebas:**
- `FuenteHibridaTests`: unitarias con dobles.
- `SqlIntegracionTests`: contra la BD real; se omiten si no hay conexión.
- Las de contrato existentes siguen usando solo JSON.
- Resultado: 72/72 en verde.

---

## 9. Validación de paridad (el JSON como oráculo)

Script `tools/paridad.py` (o test de integración .NET):
1. Para cada cuadro, filtro y entidad, compara la salida de la fuente híbrida (o de los SP) con el JSON. Clave: `(fila, período)`, tolerancia `1e-4`.
2. Clasifica cada diferencia: *falta en SQL*, *valor distinto*, *escala*, *mes corregido* (2026‑08).
3. Publica `% cobertura` por cuadro. Esto decide qué entra en `Datos:Sql` (§8.2).
4. Ventana: 2024‑01 → 2026‑07. 2026‑08 se informa aparte como "corregido en SQL"; 2026‑09+ queda fuera por C1.
5. **Diferencias aceptadas por convención** (no bloquean): agregados de coops (C3), con su desviación esperada por mes registrada a partir de §0.2. Filas 6/7 ocultas (C2).

---

## 10. Plan por frentes de trabajo (para repartir)

### Frente A — Backend híbrido (empieza ya, sin depender de nadie)

| Paso | Entregable | Criterio de salida |
|---|---|---|
| A1 | B1‑B7 de §8.5: EFI01, EFI06 (saldo) y SFN01/SFN06 (todos los filtros) vía SQL; ventana C1 y ocultamiento de 6/7 | Paridad 100 % 2024‑01 → 2026‑07 en bancos/MU/entidades; coops con la desviación documentada |
| A2 | Agregar EFI02, EFI05, EFI07/10 (filas de saldo) y REP01 (saldos) | Paridad 100 % en las filas migradas |
| A3 | Health, fallback y `/api/meta` con origen por fuente | SQL caído → la API sigue respondiendo con JSON |

### Frente B — ETL y datos (DBA / quien mantiene `sp_etl_bce`)

| Paso | Entregable | Desbloquea |
|---|---|---|
| B1 | **E7**: sacar credenciales del SP, rotar `sa`, crear usuario de solo lectura para la API | Despliegue seguro |
| B2 | (Opcional, §0.1) conservar las 10 cuentas resumen 6/7 | EFI01/SFN01 completos |
| B4 | **E4**: `IfiPeriodo` (provincia y estado por mes) | Provincia del catálogo desde SQL |
| B5 | **E6**: ampliar `IndicadorFin` con las derivadas del `.sav` (`Cart_*`, `t_*`, `num_*`, `moa_*`…) | EFI07/08, TEA02 |
| B6 | Tablas `Carga` y `B11Agregado` | ETag y agregados rápidos |

### Frente C — Reglas y fórmulas (analista de datos)

| Paso | Entregable | Desbloquea |
|---|---|---|
| C1 | Revisar con negocio la convención C3 usando el anexo `anexo-segmentos-coop.csv`. Si se cambia a `segmento` del `.sav`, es un ajuste de 2 líneas en `sp_etl_bce` + `IfiPeriodo` | Agregados de coops iguales a lo publicado |
| C2 | Fórmulas AH/AV, EFI03 (anualizado), EFI04 (fuentes y usos), márgenes EFI02 | SFN06/EFI06 completos, EFI02‑04 |
| C3 | CAMELS, PERLAS, IVF, S1‑S9 (EFI11‑13, REP01) | Reportes y rankings desde SQL |

### Frente D — Fuentes externas y contenido estático

| Paso | Entregable |
|---|---|
| D1 | Extraer las plantillas de cuadros de los JSON a un recurso del backend (no a la BD). Macro y notas siguen en JSON hasta tener una fuente oficial |
| D2 | Definir el origen de cartera por rango, depósitos y tasas por plazo (EFI09/10, TPE02, CAR03) |
| D3 | Confirmar si la ventana C1 aplica también a los cuadros macro BCE |

**Orden sugerido**:
- A1 y B1 a la vez.
- C1, la decisión sobre segmentos, lo antes posible, porque cambia cifras visibles.
- A2.
- B4‑B6.
- C2‑C3 y D cuando haya capacidad.

---

## 11. Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Credenciales `sa` expuestas en el SP | Acceso total a la base | B1 inmediato: rotar y usar un usuario dedicado |
| Pérdida de historia 2021‑05 → 2023‑12 (C1) | Usuarios que comparaban contra 2021‑2023 ya no lo ven | Decisión tomada; si se necesita, el `.sav` tiene la historia y el ETL es idempotente por mes |
| Segmentos de coops distintos a lo publicado (C3) | S1 pasa de ~24 800 a ~2 100 MUSD; 87 % del activo cooperativo cambia de grupo | Advertencia en `/api/meta`, anexo con el detalle; revisar C3 (C1 de §10) |
| Filas 6/7 ocultas (C2) | EFI01/SFN01 sin contingentes ni orden | Cargar las 10 cuentas resumen (§0.1) |
| `B11` indexada por fecha primero | Lecturas por entidad lentas | Índice `IX_B11_Ifi` o columnstore |
| SQL caído | API sin datos | Fallback a JSON en `FuenteDatosHibrida` |
| Fórmulas PERLAS/CAMELS con matices | Indicadores difieren levemente | Paridad automática; quedan en JSON hasta llegar al 100 % |
