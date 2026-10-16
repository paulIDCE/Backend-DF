# Reunión con quien genera los JSON — hallazgos y pedidos

> **Guía interna** para la reunión con quien nos entregó el `.sav` y los JSON y los genera con sus propios scripts.
> No es para la persona del ETL `.sav` → SQL: lo que le toca a ella está al final (§8), para no mezclar.
> Datos al 2026‑10‑16 · BD `idce_bco_coop` (2024‑01 → 2026‑08) · JSON de oct‑2026.

## 0. En una frase

Ya pasamos a la BD **7 507 filas** de los cuadros: todo lo que se podía deducir de los saldos y validar contra sus JSON. Quedan **3 037 filas** que no podemos reproducir sin saber **cómo las calcula** o **de qué dato salen**.

**Los tres pedidos de la reunión**, en orden:
1. **Sus scripts** (R / SPSS / lo que use) con que arma los JSON. Resuelven de golpe casi todo lo de §2, §3 y parte de §4.
2. Si no los puede compartir: **una tabla de definiciones**, código del JSON → fórmula o variable del `.sav`.
3. **Las fuentes** de lo que no está en el `.sav` (§4) y **respuestas a las inconsistencias** (§5).

---

## 1. Cuadro resumen

Prioridad: 🔴 alta (indicadores muy usados o decisión que cambia cifras publicadas) · 🟡 media · ⚪ baja.

| # | Cuadro / tema | Qué muestra | Filas aún en JSON | Qué nos falta | Cómo se solventa | Prio. | Pregunta clave |
|---|---|---|---|---|---|---|---|
| A1 | **EFI11 / CAR05** | Indicadores financieros: morosidad, cobertura, rendimiento, eficiencia, liquidez, vulnerabilidad | 39 + 39 | Fórmula de 37 indicadores | §2 Reglas | 🔴 | ¿Qué cuentas y qué promedios usa cada `SBxxx`, `IF012_*`, `IF006`…? |
| A2 | **EFI12** | CAMELS e IVF | 19 | Fórmulas, ponderaciones y escalas del puntaje | §2 Reglas | 🔴 | ¿Cómo se calcula `Indic_CAMELS_1` e `IVF_Cuantitativo`? |
| A3 | **EFI13** + puntajes de REP01 | PERLAS y su puntaje acumulado | 34 + ~17 | Fórmulas; regla del puntaje (`S_*_acum`, `perlas_acumulado`) | §2 Reglas | 🔴 | ¿Qué metas y pesos usa el puntaje PERLAS? |
| A4 | **REP01**: copia de los indicadores | Mismos códigos de A1–A3 | ~108 | Sale sola cuando estén A1–A3 | §2 Reglas | 🔴 | — |
| A5 | **EFI04 / SFN04** | Fuentes y usos (grupos FUENTES, USOS, RESULTADOS) | 64 + 64 | Regla de clasificación | §2 Reglas | 🟡 | ¿Cómo pasa de la variación mensual a fuente/uso? |
| A6 | **EFI05 / SFN05** | `Ex_Patri_Reque`; patrimonio técnico del sector | 1 + 7 | Regla del excedente; cómo agrega el sector | §2 Reglas | 🟡 | ¿Por qué el excedente sale 0? |
| A7 | **REP01**: estadísticas entre entidades | Percentiles, desviaciones (`@1SD_*`…), participaciones, `var_anual_*`, `turbulencia` | ~51 | Universo y fórmula | §2 Reglas | ⚪ | ¿Contra qué grupo de entidades se calculan? |
| B1 | **EFI07 / CAR01** + REP01 | Cartera por estado (por vencer, no devenga, vencida, improductiva, refinanciada, reestructurada) | 106 + 106 + 34 | **Bancos:** no cuadra con el balance | §3 Variables del `.sav` | 🔴 | ¿La cartera de bancos sale de `Cart_*` del `.sav` o de otro reporte? |
| B2 | **EFI08 / CAR02** + REP01 | Montos y número de operaciones por segmento de crédito | 102 + 726 + ~60 | Qué variable alimenta cada fila | §3 Variables del `.sav` | 🟡 | ¿`pro_*`, `num_*`, `moa_*` son variables del `.sav`? ¿En qué unidad? |
| B3 | **TEA02 / TEA01** + REP01 | Tasas activas: efectiva, máxima, referencial, equilibrio, costo de fondeo, etc. | 75 + 596 + 71 | Variable o fórmula de cada tasa | §3 Variables del `.sav` (+ §2) | 🟡 | ¿`t_*`, `max_*`, `ref_*` vienen del `.sav`? ¿Y `CF_`, `GO_`, `RC_`, `CK_`, `TAEB`, `TEN`? |
| C1 | **EFI10 / CAR04** + REP01 | Depósitos por plazo (montos `Plazo*`, número `OPTPE*`, promedios `pro_op*`) | 54 + 70 + ~14 | Dato por plazo de vencimiento | §4 Fuente externa | 🟡 | ¿De qué reporte sale el desglose por plazo? |
| C2 | **TPE01 / TPE02** + REP01 | Tasas pasivas efectivas por plazo | 14 + 7 + 7 | Fuente | §4 Fuente externa | 🟡 | ¿BCE? ¿Con qué periodicidad? |
| C3 | **EFI09 / CAR03** | Cartera por rango de días de vencimiento (R1–R5) | 252 + 252 | Dato por operación o por rango | §4 Fuente externa | 🟡 | ¿De qué base sale? ¿La podemos recibir? |
| C4 | **Macro** (IEA / IEM, 195 cuadros) | Indicadores BCE desde 2000 | todos | Fuente actualizable | §4 Fuente externa | ⚪ | ¿Los actualiza él? ¿Desde dónde? |
| D1 | Historia reprocesada | Tasas publicadas con balances del año anterior ya corregidos | 436 entidad‑meses | Explicación y política | §5 Inconsistencias | 🔴 | ¿Re‑procesa la historia? ¿Cuál vale? |
| D2 | Ceros que significan "sin dato" | ROA, ROE y análisis horizontal publican 0 | ~5 500 valores | Convención | §5 | 🟡 | ¿0 = no se pudo calcular? |
| D3 | ROA que contradice al ROE | COAC 2023‑05/07 y algunos diciembres | 70 por código | Revisión de esos meses | §5 | 🟡 | ¿Hubo cargas con error en esos meses? |
| D4 | Primeros 12 meses de los JSON | 2021‑05 → 2022‑04 vienen de otra serie | todo | Explicación | §5 | ⚪ | ¿Cambió la fuente o el catálogo? |
| D5 | Meses futuros | Los JSON traen 2026‑09 → 2027‑02 | — | Qué son | §5 | ⚪ | ¿Proyecciones? ¿Hay que publicarlas? |
| D6 | Entidad anómala | COAC Orden y Seguridad publica 0 con balance distinto de 0 | 1 entidad | Revisión | §5 | ⚪ | ¿Plantilla distinta? |
| D7 | `@4204A` con valores sin cuenta | El JSON publica 44 valores ≠ 0; en el `.sav`/BD la cuenta 4204 es 0 siempre | 44 valores | Revisión | §5 | ⚪ | ¿De dónde sale? |
| E1 | **Segmento de cooperativas** | Los agregados Coop S1/S2/S3 | 3 sectores | Definición oficial | §6 Definiciones | 🔴 | ¿Vale `segmento` (por mes) o `Tamaño`? |
| E2 | Catálogo de entidades | 229 publicadas de 379 | — | Criterio | §6 | 🟡 | ¿Por qué esas 229? |
| E3 | Provincia, tamaño, rango de activos | Metadatos de entidad | — | Fuente oficial | §6 | 🟡 | ¿`DPA_PR` del `.sav` es la oficial? ¿Cambia en el tiempo? |
| E4 | Nombres de cuentas | 544 cuentas sin nombre (`cta NNNN`) | — | Catálogo de cuentas | §6 | ⚪ | ¿Nos pasa el catálogo único de cuentas de la SB/SEPS? |
| E5 | Historia 2023 del `.sav` | Cubrir los vacíos de 2024 | 12 meses | Confiabilidad | §6 | 🟡 | ¿2023 en el `.sav` es definitivo? |
| F1‑F7 | **Geográficos** (GEO, PDA, GEE, PDE) | Captaciones y colocaciones por provincia y cantón | todos | Correcciones de datos | §7 | ⚪ | Duplicados, GEE02 faltante, unidades, nombres DPA |

**Totales en JSON por cuadro:** CAR01 106 · CAR02 726 · CAR03 252 · CAR04 70 · CAR05 39 · EFI03 17 · EFI04 64 · EFI05 1 · EFI07 106 · EFI08 102 · EFI09 252 · EFI10 54 · EFI11 39 · EFI12 19 · EFI13 34 · REP01 376 · SFN03 17 · SFN04 64 · SFN05 7 · TEA01 596 · TEA02 75 · TPE01 14 · TPE02 7 → **3 037**. CAR02 y TEA01 suman todas sus variantes de sector.

> EFI03/SFN03 (17 + 17): son cuentas anualizadas (`@5201A`, `@4301A`…) que **sí** están en la BD, pero no en los balances del JSON, así que no pudimos validarlas. Es tarea **nuestra** (validar contra `B11`), salvo `@4204A` (D7).

---

## 2. Necesitamos sus reglas de cálculo (A1–A7)

Son indicadores que se calculan con datos que **ya tenemos** (saldos de cuentas e indicadores de patrimonio técnico). Solo nos falta la fórmula exacta. Si comparte su script, se resuelven todos a la vez.

### A1 · EFI11 / CAR05 — indicadores financieros (39)

| Grupo | Códigos |
|---|---|
| Morosidad | `IF012` total, `IF012_1`…`_6` por tipo de crédito |
| Cobertura de provisiones | `SB028` (refinanciada), `SB029`…`SB034` por tipo |
| Rendimiento de la cartera | `SB036` total, `SB037`…`SB042`, `SB044`, `SB045` |
| Calidad de activos | `SB018A`, `ac_prod_neto_acti`, `ac_prod_pco` |
| Eficiencia | `marg_int_ac`, `marg_int_pas`, `gast_oper_ac`, `gast_operacion`, `gasto_personal` |
| Liquidez / intermediación | `IF006`, `Cart_Depo` |
| Patrimonio | `SB025`, `SBP001`, `SBP002`, `SBP003` (FK), `SB018B` (FI), `SBP005` (FK/FI) |

**Ya resueltos (para darle confianza):** ROA (`IF002`) y ROE (`IF004`) calcados en 229 entidades y 10 sectores, incluida la regla de diciembre:
- **ROA:** ganancia anualizada / activo promedio desde diciembre; en diciembre, el activo del mes.
- **ROE:** igual sobre el patrimonio; en diciembre, patrimonio − ganancia.
- **Ejemplo:** Pichincha 2026‑06 ROA = 1,146 y ROE = 11,7935, idénticos al JSON.

**Preguntar:**
- ¿Los "promedios" son siempre desde diciembre del año anterior, como en ROA/ROE, o en algún indicador son móviles de 12 meses?
- "Margen de intermediación **estimado**", "gastos de operación **estimados**": ¿qué significa *estimado*? ¿Anualizado?
- Morosidad por tipo: ¿usa la cartera del balance o la de `Cart_*` (ver B1)? Para bancos importa.
- ¿La cobertura usa provisiones constituidas (1499) o requeridas?

**Pedir:** su script; o una tabla `código → numerador / denominador / operación`.

**Con la respuesta:** cada fórmula se carga en el catálogo, se valida contra sus JSON en todas las entidades y solo entra si cuadra en todas. El mecanismo ya existe, incluidos promedios y anualización.

### A2 · EFI12 — CAMELS e IVF (19)

`Indic_CAMELS_1`, `IVF_Cuantitativo`, C1–C3, A2–A4, `Porce_activ_impro`, M1, `Efici_opera`, `M3_des_estr`, `Efici_negoc`, `E6_Margen_Spread`, `Indic_liqui`, L2–L4 y `Tasa_inter_impli`.

**Preguntar:**
- Fórmula de cada componente y **cómo se agregan** en el puntaje CAMELS (pesos, escalas, bandas).
- ¿`C3` (cartera improductiva neta / capital regulatorio) usa un dato que no está en el balance, como el capital regulatorio de patrimonio técnico?
- ¿`IVF_Cuantitativo` es un índice propio o de la SB?

### A3 · EFI13 — PERLAS y puntaje (34 + 17 en REP01)

P1, P2, P4, P6, E2, E5, E7–E9, A1–A3, R1, R2, R5, R6, R8–R11, L1–L3, S1–S9, el global `efic_perlas_acum` y, en REP01, `S_*_acum` y `perlas_acumulado`.

**Ya resueltos:** E1, E3, E4 y E6 (proporciones sobre el activo).

**Preguntar:**
- **P4** "promedios móviles de 12 meses": ¿12 meses incluido el actual?
- **S1–S9** (crecimientos): ¿son anuales, contra el mismo mes del año anterior, o contra diciembre?
- **Puntaje** (`S_*_acum`, `perlas_acumulado`): ¿qué meta y qué peso tiene cada indicador?
- **P1/P2** (provisiones > 12 y < 12 meses): ¿de qué dato sale la morosidad por antigüedad? Puede depender de C3 (rangos).

### A5 · EFI04 / SFN04 — fuentes y usos (64 + 64)

**Ya resuelto:** el grupo *VARIACIÓN MENSUAL* (saldo_t − saldo_{t‑1}).

**No resuelto:** *FUENTES*, *USOS* y *RESULTADOS*.

**Evidencia:** Pichincha 2026‑06, variación del ACTIVO = −341,04, pero FUENTES·ACTIVO = 270,49. No es la variación ni su valor absoluto: probablemente suma las disminuciones de las subcuentas del activo.

**Preguntar:**
- ¿Cómo clasifica cada variación como fuente o uso? ¿A qué nivel del árbol de cuentas?
- ¿Qué entra en *RESULTADOS* (`INGRESOS − GASTOS`, `RESULTADOS + FUENTES − USOS`)?

### A6 · EFI05 / SFN05 — patrimonio técnico

**Ya resuelto:** `PTR_9` = 9 % de los activos ponderados por riesgo (Pichincha 2026‑06: 1 652,433, exacto).

**Pendiente:**
- **`Ex_Patri_Reque`** (excedente o deficiencia): el JSON publica **0** aunque PTC − PTR_9 = 2 517,65 − 1 652,43 = **865,22**. ¿Solo muestra deficiencias? ¿Hay un tope?
- **SFN05** (sector): ¿suma PTC y activos ponderados de las entidades y recalcula la solvencia, o promedia?

### A7 · REP01 — estadísticas entre entidades (~51)

- Percentiles (`perc50`, `perc75`), `turbulencia`.
- Desviaciones `@1SD_*`, `@2SD_*`, `@1mSD_*`, `@2mSD_*` por segmento de crédito.
- Participaciones (`part_*`, `partmon_*`), `var_anual_*` y `Multi*`.

**Preguntar:**
- ¿Sobre qué universo se calculan? ¿Todas las entidades, las del mismo tipo, segmento o tamaño?
- ¿Con qué variable? ¿Qué es `turbulencia`?

---

## 3. Variables del `.sav` que hay que identificar (B1–B3)

El `.sav` trae unas **145 variables derivadas** además de las cuentas: cartera por segmento (`Cart_bru_*`, `Cart_vencer_*`, `Cart_impr_*`, `Prov_cons_*`), tasas (`t_*`, `tea_global`, `ref_*`, `max_*`) y montos/número (`num_*`, `proc`, `cons`…). Hoy la BD solo carga **5** de ellas (patrimonio técnico y solvencia).

Con que nos diga **qué variable del `.sav` alimenta cada código del JSON** (y la unidad), el ETL las carga y nosotros las publicamos. No hace falta otra fuente.

### B1 · EFI07 / CAR01 — cartera por estado de los **bancos** 🔴

**Hallazgo:**
- **Cooperativas y mutualistas:** la cartera por vencer, no devenga, vencida e improductiva, por tipo de crédito, **cuadra exacto** con el balance (subcuentas 1401–1489 y 1491–1496).
- **Bancos:** **no cuadra en ningún mes**, y ninguna cuenta 14xx explica la diferencia.
- **Ejemplo:** Pichincha 2026‑06, `IF007` (cartera por vencer) JSON − balance = **+41,18 M**.

**Ya cargado** (cuadra en todos los tipos de entidad): `Por_vencer`, `Por_vencida` y la vencida/improductiva VIS.

**Preguntar:**
- ¿La cartera por estado de los bancos sale de `Cart_bru_*` / `Cart_vencer_*` / `Cart_impr_*` del `.sav`, o de otro reporte, como el de cartera por operación de la SB?
- ¿Por qué difiere del balance? ¿Incluye contingentes, cartera vendida o titularizada?

**Con la respuesta:**
- **Si es una variable del `.sav`:** se carga como indicador y se usa para todos.
- **Si es otro reporte:** lo pedimos (pasa a §4).

### B2 · EFI08 / CAR02 — montos y número de operaciones

Códigos: `pro_*` (montos por subsegmento: corporativo, empresarial, PYMES, minorista, VIS…), `num_*` (número), `moa_*`, `monto_total`, `ope_total`, `proc`, `prodem`, `prodpy`… El JSON dice "(USD)", no millones.

**Preguntar:**
- ¿Son variables del `.sav`? ¿Monto **otorgado en el mes** (flujo), no saldo?
- ¿La unidad es USD o miles?
- ¿CAR02 (sector) es la suma de las entidades?

### B3 · TEA02 / TEA01 — tasas activas

- **Tasas:** `t_*` (efectiva), `max_*` (máxima), `ref_*` (referencial).
- **Componentes:** `TAEB_*` / `TEN_*` (equilibrio efectiva / nominal), `CF_*` (costo de fondeo), `GO_*` (gasto operativo), `RC_*` (riesgo de crédito), `CK_*` (costo de capital).

**Preguntar:**
- `t_`, `max_`, `ref_`: ¿son variables del `.sav`, o vienen del BCE (las máximas y referenciales las publica el BCE)?
- Componentes de la tasa de equilibrio (`CF`, `GO`, `RC`, `CK`): ¿los calcula él? ¿Con qué fórmula?
- TEA01 (sector): ¿es el promedio ponderado por monto de las entidades?

---

## 4. Datos que no están en el `.sav` (C1–C4)

Para estos no basta una fórmula: necesitamos **el archivo o la base de origen** y su periodicidad.

| # | Qué | Qué pedir |
|---|---|---|
| C1 | **Depósitos por plazo** (EFI10/CAR04): montos `Plazo30`…`Plazo361`, número `OPTPE*`, promedios `pro_op*`, `SB011` | Fuente del desglose por plazo de vencimiento; si es del `.sav`, la variable |
| C2 | **Tasas pasivas efectivas por plazo** (TPE01/02): `TPE`, `TPE30`…`TPE361` | ¿BCE? URL o archivo y periodicidad |
| C3 | **Cartera por rango de días** (EFI09/CAR03): R1 = 1‑30, R2 = 31‑90, R3 = 91‑180, R4 = 181‑360, R5 = > 360 días, para bruta, improductiva, por vencer, no devenga y vencida | ¿De qué base sale (operaciones, cartera por vencimiento)? ¿Nos la puede entregar con la misma estructura del `.sav` (entidad × mes)? |
| C4 | **Macro BCE** (195 cuadros IEA/IEM) | ¿Los actualiza él? ¿De qué publicación? Hoy se sirven del JSON y no está previsto migrarlos |

**Con la respuesta:** se crea una tabla de carga para cada fuente. Es tarea del ETL; nosotros solo definimos la estructura.

---

## 5. Inconsistencias de los JSON (D1–D7)

Las encontramos al validar. Ninguna impidió migrar, porque las excluimos de la comparación solo cuando **el propio JSON se contradice**. Pero cambian cifras visibles: lo que muestre la app (desde la BD) puede no coincidir con lo que él publicó.

### D1 · Historia reprocesada 🔴

**Qué vimos:** en **436 entidad‑meses de 42 entidades**, el análisis horizontal y las tasas a 12 meses publicadas no coinciden con los saldos que el **mismo JSON** publica para el año anterior. Falla incluso el ACTIVO total.

**Ejemplo:**
- DELBANK 2024‑06, `tasa_14` (crecimiento de cartera) publicada = **15,937 %**.
- Con sus propios saldos publicados (@14: 23,8223 / 20,7062) da **15,05 %**.

Entidades con más meses afectados: COAC 4 Ríos (22), El Molino (22), Grupo Difare (21), Ecuacréditos (20), Pucará (14), Campesina Coopac (12), BP Delbank (11) y 15 de Abril (11).

**Interpretación:** calculó la tasa con el balance de t−12 *de ese momento*; después ese balance se corrigió y el JSON publica el corregido.

**Preguntar:**
- ¿Re‑procesa la historia cuando la SB o la SEPS corrigen un balance? ¿Con qué frecuencia?
- ¿Recalcula también las tasas de los meses siguientes?

**Implicancia:** la BD tiene el balance corregido, así que nuestras tasas son coherentes con los saldos que se muestran y pueden diferir de las publicadas. **Confirmar que eso es lo correcto.**

### D2 · Ceros que significan "sin dato"

ROA y ROE publican **0** en 47 entidad‑meses (sobre todo dic‑2021 y dic‑2023, cooperativas). El análisis horizontal publica 0 en ~5 400 valores.

**Preguntar:** ¿0 = "no se pudo calcular"? Nosotros devolvemos **vacío** (sin valor), que no es lo mismo que 0. ¿Le parece bien?

### D3 · ROA que contradice al ROE

En COAC, 2023‑05 y 2023‑07 (y algunos diciembres), el ROA publicado no es compatible con el ROE publicado, que sí se reproduce.

**Ejemplo:**
- COAC La Dolorosa 2023‑07: ROA = **+6,61**, ROE = **−21,81**. Signos opuestos con la misma ganancia: imposible.
- Su balance de julio repite el activo de junio (18,2989) y el patrimonio cae de 4,62 a 2,21.

**Preguntar:**
- ¿Hubo cargas con error en 2023‑05 y 2023‑07 para cooperativas?
- ¿El ROA de esos meses salió de otra fuente?

### D4 · Los primeros 12 meses (2021‑05 → 2022‑04)

En ese tramo:
- el análisis horizontal publica 0;
- la cartera por estado no cuadra con el balance en **ninguna** entidad.

Desde 2022‑05 cuadra.

**Preguntar:** ¿cambió la fuente, el catálogo de cuentas o la metodología en 2022‑05?

**Implicancia:** ninguna para la BD, que empieza en 2024‑01. Solo para entender.

### D5 · Meses futuros

Los JSON de entidad traen **2026‑09 → 2027‑02** (p. ej. Pichincha activo 2027‑02 = 24 094,36), y los de sistema y cartera, 2026‑11 → 2027‑02.

**Preguntar:** ¿son proyecciones? ¿Se publican? Hoy la app los recorta al último mes real (2026‑08).

### D6 · COAC Orden y Seguridad

Publica 0 en filas donde su balance tiene valores (plantilla atípica). La validamos aparte.

**Preguntar:** ¿se trata distinto a propósito?

### D7 · `@4204A` con valores sin cuenta

EFI03 publica `@4204A` (anualizado de la cuenta 4204) con 44 valores ≠ 0, pero la cuenta 4204 vale 0 en todos los meses del `.sav`/BD.

**Preguntar:** ¿de dónde sale?

---

## 6. Definiciones de negocio y metadatos (E1–E5)

### E1 · Segmento de cooperativas 🔴 (decisión)

El `.sav` tiene **dos** columnas de segmento:

| Columna | Qué hace la BD hoy | ¿Reproduce los JSON? |
|---|---|---|
| `Tamaño` (Segmento 1…5 / Sin Segmento) | La usa como segmento **fijo** por entidad | **No**, en ningún mes |
| `segmento` (S1…S5), **varía por mes** | La descarta | **Sí**: S1 31/31 meses, S2 30/31, S3 28/31 |

**Ejemplo:** activo Coop **Segmento 1**, 2026‑07: JSON = **24 791,2 M**; BD (con `Tamaño`) = **2 137,1 M**. JEP, Jardín Azuayo y Alianza del Valle figuran "Sin Segmento" en `Tamaño`, pero son S1 en `segmento`. En total difieren 245 de 348 cooperativas, el 87 % del activo cooperativo.

**Preguntar:**
- ¿Cuál es la segmentación oficial (SEPS) para publicar?
- ¿`segmento` cambia por mes a propósito (reclasificación anual)?
- ¿Qué significa `Tamaño` y para qué se usa?

**Con la respuesta:**
- **Si vale `segmento`:** el ETL debe guardarlo por mes (ver §8) y los agregados Coop S1/S2/S3 volverán a coincidir con lo publicado.
- **Si vale `Tamaño`:** documentamos que las cifras por segmento cambian.

### E2 · Catálogo de entidades

La app publica **229** entidades; la BD tiene 379 (327 activas en 2026‑08).

**Preguntar:** ¿con qué criterio elige esas 229 (activas, tamaño, que reporten todos los meses)? ¿Hay que agregar las nuevas?

### E3 · Provincia, tamaño y rango de activos

El frontend usa provincia (`DPA_PR`), tamaño y rango de activos (texto como "Nivel 8: 3269 Millones - 22807 Millones").

**Preguntar:**
- ¿`DPA_PR` del `.sav` es la provincia oficial (de la matriz)? ¿Cambia en el tiempo?
- ¿Cómo se definen los niveles de rango de activos? ¿Se recalculan cada mes?

### E4 · Nombres de cuentas

El `.sav` trae solo el código de la cuenta, sin nombre. Quedan **544** cuentas con nombre genérico (`cta 5201`, `cta 4301`…).

**Pedir:** el catálogo único de cuentas (código → nombre) que usa para los JSON, o el oficial de SB/SEPS.

### E5 · Historia 2023 del `.sav`

Para no dejar vacíos en 2024 (tasas a 12 meses y promedios de ROA/ROE necesitan 2023), se puede cargar 2023 en la BD solo como base de cálculo. El `.sav` trae desde 2007.

**Preguntar:**
- ¿2023 en el `.sav` es definitivo o se sigue corrigiendo? Se relaciona con D1.
- ¿Hay cambios de catálogo de cuentas en 2023 que debamos saber?

**Con la respuesta:** si es confiable, se lo pedimos al ETL (opción B).

---

## 7. Cuadros geográficos (F1–F7)

Detalle completo en [`propuesta-cuadros-geograficos.md`](propuesta-cuadros-geograficos.md). Todos son de datos de los JSON:

| # | Problema | Pedido |
|---|---|---|
| F1 | GEO01‑04 y PDA01‑02 **duplicados** en `base_estru_sistema.json` y `base_cartera.json` (65 124 filas idénticas) | Dejarlos en un solo archivo |
| F2 | **Falta GEE02** (colocaciones por cantón de la entidad) | Generarlo |
| F3 | **GEE04 mezcla** captaciones y colocaciones (`Total` aparece dos veces) | Separar; las colocaciones van a GEE02 |
| F4 | **Unidad errónea**: todo dice `Número`, pero hay saldos en millones | Unidad correcta por medida |
| F5 | **Nombres inconsistentes**: `El Oro` con espacio especial, `Bolivar`/`Bolívar`, mayúsculas distintas | Normalizar |
| F6 | **Códigos DPA ambiguos**: `1701` Quito / Distrito Metropolitano; La Joya de los Sachas en `1505` y `2203`… | Usar la tabla DPA oficial única |
| F7 | Meses futuros (= D5) | — |

---

## 8. Lo que NO hay que pedirle (es del ETL `.sav` → SQL)

Para llevar aparte a quien mantiene `sp_etl_bce`:
- Guardar `segmento` **por mes**, si E1 lo confirma.
- Cargar más variables derivadas a `IndicadorData` (las de §3).
- Conservar `DPA_PR`, `Estado` e `IFI`.
- Cargar 2023 (E5) y las 10 cuentas resumen 6/7.
- **Quitar las credenciales de `sa` del SP y rotar la contraseña.**

---

## 9. Cierre de la reunión (checklist)

- [ ] ¿Comparte los scripts? → fecha y forma (repo, correo).
- [ ] Si no: ¿quién arma la tabla de definiciones (código → fórmula/variable)? ¿Para cuándo?
- [ ] E1: decisión sobre el segmento.
- [ ] D1: ¿la historia corregida es la correcta?
- [ ] B1: fuente de la cartera de bancos.
- [ ] C1–C3: fuentes externas, con contacto y periodicidad.
- [ ] Nota técnica de la SB (CAMELS / PERLAS / indicadores), si no entrega scripts.
- [ ] Geográficos: ¿quién corrige y para cuándo?
