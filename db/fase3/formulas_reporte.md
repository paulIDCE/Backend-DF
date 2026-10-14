# Fase 3 · paso 1 — fórmulas SUMA/RATIO

Validación con meses reales 2021-05 → 2026-08 (último mes de la BD: 2026-08); 229 entidades y 10 sectores.

## 1. Análisis vertical (EFI06 / SFN06)

Regla: 100 × cuenta / cuenta raíz del grupo (primer dígito). Publicado con 2 decimales.

| Ámbito | Tipo | Valores comparados | Fallas | Ejemplos de falla |
|---|---|---|---|---|
| Entidad | BP | 1,609,530 | 0 |  |
| Entidad | COOP | 13,858,911 | 0 |  |
| Entidad | MU | 287,067 | 0 |  |
| Entidad | OYS (anómala) | 10,431 | 0 |  |
| Sector | SECTOR | 836,416 | 0 |  |

**Resultado:** ✅ se carga como RATIO en EFI06 y SFN06.

## 2. Candidatos del descubrimiento

| Cuadro | Sub-vista | Código | Fórmula | Entidades | Sectores | Resultado |
|---|---|---|---|---|---|---|
| EFI03 | — | `@5201A` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| EFI03 | — | `@5404A` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| EFI04 | — | `|FUENTES|OPE. INTERBANCARIAS` | — | 0 comparados, 0 fallas (p. ej. -) | — | ❌ queda JSON |
| EFI04 | — | `|RESULTADOS|GASTOS` | — | 0 comparados, 0 fallas (p. ej. -) | — | ❌ queda JSON |
| EFI04 | — | `|RESULTADOS|INGRESOS` | — | 0 comparados, 0 fallas (p. ej. -) | — | ❌ queda JSON |
| EFI04 | — | `|RESULTADOS|INGRESOS - GASTOS` | — | 0 comparados, 0 fallas (p. ej. -) | — | ❌ queda JSON |
| EFI07 | Cartera Total | `IF011` | Σ +14 −1499 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ CAR01 (640) | ✅ SUMA |
| EFI07 | Cartera Total | `Impro_rees` | — | 13635 comparados, 97 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 0.2859, 0.2236)) | — | ❌ queda JSON |
| EFI07 | Cartera Total | `Por_vencida` | — | 13635 comparados, 44 fallas (p. ej. ('BP__BOLIVARIANO', '2021-05', 15.754, 15.7207)) | — | ❌ queda JSON |
| EFI07 | Cartera Total | `Rees_vencer` | — | 13635 comparados, 111 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 2.3106, 1.419501)) | — | ❌ queda JSON |
| EFI07 | Cartera Total | `Rees_vencida` | — | 13635 comparados, 131 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 0.031, 0.0245)) | — | ❌ queda JSON |
| EFI07 | Cartera Total | `Refi_vencer` | — | 13635 comparados, 71 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 5.0393, 3.8525)) | — | ❌ queda JSON |
| EFI07 | Cartera Total | `Refi_vencida` | — | 13635 comparados, 105 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 0.0193, 0.0093)) | — | ❌ queda JSON |
| EFI07 | Consumo | `IF007_2` | — | 13635 comparados, 60 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 59.2372, 57.4952)) | — | ❌ queda JSON |
| EFI07 | Consumo | `IF008_2` | — | 13635 comparados, 60 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 1.8835, 1.6739)) | — | ❌ queda JSON |
| EFI07 | Consumo | `IF009_2` | — | 13635 comparados, 60 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 0.614, 0.6012)) | — | ❌ queda JSON |
| EFI07 | Educativo | `IF007_6` | — | 13635 comparados, 27 fallas (p. ej. ('BP__BOLIVARIANO', '2021-05', 3.7204, 3.711)) | — | ❌ queda JSON |
| EFI07 | Educativo | `IF008_6` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| EFI07 | Educativo | `IF009_6` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| EFI07 | Educativo | `IF010_6` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| EFI07 | Educativo | `IF011_6` | — | 13635 comparados, 37 fallas (p. ej. ('BP__BOLIVARIANO', '2021-05', 3.8094, 3.711)) | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `IF007_3` | — | 13635 comparados, 33 fallas (p. ej. ('BP__AUSTRO', '2021-05', 47.524, 47.208)) | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `IF008_3` | — | 13635 comparados, 27 fallas (p. ej. ('BP__AUSTRO', '2024-09', 2.377, 2.3699)) | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `IF009_3` | — | 13635 comparados, 93 fallas (p. ej. ('BP__AUSTRO', '2022-08', 0.1029, 0.1026)) | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `IF010_3` | — | 13635 comparados, 82 fallas (p. ej. ('BP__AUSTRO', '2022-09', 1.5844, 1.5839)) | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `IF011_3` | — | 13635 comparados, 83 fallas (p. ej. ('BP__AUSTRO', '2021-05', 48.187, 47.871)) | — | ❌ queda JSON |
| EFI07 | Microcrédito | `IF007_4` | — | 13635 comparados, 51 fallas (p. ej. ('BP__AUSTRO', '2021-05', 5.4141, 5.3266)) | — | ❌ queda JSON |
| EFI07 | Microcrédito | `IF008_4` | — | 13635 comparados, 51 fallas (p. ej. ('BP__AUSTRO', '2024-03', 1.604, 1.5603)) | — | ❌ queda JSON |
| EFI07 | Microcrédito | `IF009_4` | — | 13635 comparados, 51 fallas (p. ej. ('BP__AUSTRO', '2024-04', 0.421, 0.4206)) | — | ❌ queda JSON |
| EFI07 | Microcrédito | `IF010_4` | — | 13635 comparados, 51 fallas (p. ej. ('BP__AUSTRO', '2024-03', 1.9966, 1.953)) | — | ❌ queda JSON |
| EFI07 | Productivo | `IF007_1` | — | 13635 comparados, 103 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 50.8233, 50.4876)) | — | ❌ queda JSON |
| EFI07 | Productivo | `IF008_1` | — | 13635 comparados, 103 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 0.1042, 0.0896)) | — | ❌ queda JSON |
| EFI07 | Productivo | `IF009_1` | — | 13635 comparados, 102 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 0.4136, 0.4099)) | — | ❌ queda JSON |
| EFI07 | Productivo | `IF010_1` | — | 13635 comparados, 106 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 0.5178, 0.4995)) | — | ❌ queda JSON |
| EFI07 | Productivo | `IF011_1` | — | 13635 comparados, 117 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 51.3412, 50.9871)) | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `IF007_5` | Σ +1408 +1416 +1424 | 13635 comparados, 0 fallas | ✅ CAR01 (640) | ✅ SUMA |
| EFI07 | Vivienda interés Público y Social | `IF008_5` | Σ +1432 +1440 +1448 | 13635 comparados, 0 fallas | ✅ CAR01 (640) | ✅ SUMA |
| EFI07 | Vivienda interés Público y Social | `IF009_5` | — | 13635 comparados, 6 fallas (p. ej. ('BP__PACIFICO', '2021-05', 2.4218, 2.3653)) | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `IF010_5` | — | 13635 comparados, 6 fallas (p. ej. ('BP__PACIFICO', '2021-05', 6.3294, 6.0542)) | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `IF011_5` | Σ +1408 +1416 +1424 +1432 +1440 +1448 +1456 +1464 +1472 | 13635 comparados, 0 fallas | ✅ CAR01 (640) | ✅ SUMA |
| EFI10 | — | `SB011` | — | 13635 comparados, 55 fallas (p. ej. ('COAC___LA_DOLOROSA_LTDA', '2023-07', 0.7399, 7.0342)) | — | ❌ queda JSON |
| EFI11 | — | `Cart_Depo` | — | 13635 comparados, 55 fallas (p. ej. ('COAC___LA_DOLOROSA_LTDA', '2023-07', 1644.6458, 172.999062)) | — | ❌ queda JSON |
| EFI11 | — | `IF012_3` | — | 8290 comparados, 65 fallas (p. ej. ('BP__AUSTRO', '2021-05', 1.3759, 1.384972)) | — | ❌ queda JSON |
| EFI11 | — | `IF012_5` | — | 567 comparados, 6 fallas (p. ej. ('BP__PACIFICO', '2021-05', 14.1324, 13.518033)) | — | ❌ queda JSON |
| EFI13 | — | `E1_Cartera` | 100·N/D +14 −1499 / +1 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ RATIO |
| EFI13 | — | `E3_Inversiones` | 100·N/D +1901 / +1 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ RATIO |
| EFI13 | — | `E4_INNF` | 100·N/D +18 / +1 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ RATIO |
| EFI13 | — | `E6_Cre_Exte` | 100·N/D +26 / +1 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ RATIO |
| EFI13 | — | `E7_Aporte` | — | 13635 comparados, 69 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 8.4402, 0.0)) | — | ❌ queda JSON |
| REP01 | — | `@5201A` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| REP01 | — | `@5404A` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| REP01 | — | `Cart_Depo` | — | 13407 comparados, 55 fallas (p. ej. ('COAC___LA_DOLOROSA_LTDA', '2023-07', 1644.6458, 172.999062)) | — | ❌ queda JSON |
| REP01 | — | `E1_Cartera` | 100·N/D +14 −1499 / +1 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ RATIO |
| REP01 | — | `E3_Inversiones` | 100·N/D +1901 / +1 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ RATIO |
| REP01 | — | `E4_INNF` | 100·N/D +18 / +1 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ RATIO |
| REP01 | — | `E6_Cre_Exte` | 100·N/D +26 / +1 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ RATIO |
| REP01 | — | `E7_Aporte` | — | 13407 comparados, 69 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 8.4402, 0.0)) | — | ❌ queda JSON |
| REP01 | — | `Gan_Eje` | Σ −41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ SUMA |
| REP01 | — | `Gan_Pe_Imp` | Σ −41 −42 −43 −44 −45 −46 −47 +51 +52 +53 +54 +55 +56 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ SUMA |
| REP01 | — | `IF007_1` | — | 13407 comparados, 103 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 50.8233, 50.4876)) | — | ❌ queda JSON |
| REP01 | — | `IF007_2` | — | 13407 comparados, 60 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 59.2372, 57.4952)) | — | ❌ queda JSON |
| REP01 | — | `IF007_3` | — | 13407 comparados, 33 fallas (p. ej. ('BP__AUSTRO', '2021-05', 47.524, 47.208)) | — | ❌ queda JSON |
| REP01 | — | `IF007_4` | — | 13407 comparados, 51 fallas (p. ej. ('BP__AUSTRO', '2021-05', 5.4141, 5.3266)) | — | ❌ queda JSON |
| REP01 | — | `IF007_5` | Σ +1408 +1416 +1424 | 13407 comparados, 0 fallas | — | ✅ SUMA |
| REP01 | — | `IF007_6` | — | 13407 comparados, 26 fallas (p. ej. ('BP__BOLIVARIANO', '2021-05', 3.7204, 3.711)) | — | ❌ queda JSON |
| REP01 | — | `IF008_1` | — | 13407 comparados, 102 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 0.1042, 0.0896)) | — | ❌ queda JSON |
| REP01 | — | `IF008_2` | — | 13407 comparados, 60 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 1.8835, 1.6739)) | — | ❌ queda JSON |
| REP01 | — | `IF008_3` | — | 13407 comparados, 27 fallas (p. ej. ('BP__AUSTRO', '2024-09', 2.377, 2.3699)) | — | ❌ queda JSON |
| REP01 | — | `IF008_4` | — | 13407 comparados, 51 fallas (p. ej. ('BP__AUSTRO', '2024-03', 1.604, 1.5603)) | — | ❌ queda JSON |
| REP01 | — | `IF008_5` | Σ +1432 +1440 +1448 | 13407 comparados, 0 fallas | — | ✅ SUMA |
| REP01 | — | `IF008_6` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| REP01 | — | `IF009_1` | — | 13407 comparados, 101 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 0.4136, 0.4099)) | — | ❌ queda JSON |
| REP01 | — | `IF009_2` | — | 13407 comparados, 60 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 0.614, 0.6012)) | — | ❌ queda JSON |
| REP01 | — | `IF009_3` | — | 13407 comparados, 93 fallas (p. ej. ('BP__AUSTRO', '2022-08', 0.1029, 0.1026)) | — | ❌ queda JSON |
| REP01 | — | `IF009_4` | — | 13407 comparados, 51 fallas (p. ej. ('BP__AUSTRO', '2024-04', 0.421, 0.4206)) | — | ❌ queda JSON |
| REP01 | — | `IF009_5` | — | 13407 comparados, 6 fallas (p. ej. ('BP__PACIFICO', '2021-05', 2.4218, 2.3653)) | — | ❌ queda JSON |
| REP01 | — | `IF009_6` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| REP01 | — | `IF010_1` | — | 13407 comparados, 105 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 0.5178, 0.4995)) | — | ❌ queda JSON |
| REP01 | — | `IF010_3` | — | 13407 comparados, 82 fallas (p. ej. ('BP__AUSTRO', '2022-09', 1.5844, 1.5839)) | — | ❌ queda JSON |
| REP01 | — | `IF010_4` | — | 13407 comparados, 51 fallas (p. ej. ('BP__AUSTRO', '2024-03', 1.9966, 1.953)) | — | ❌ queda JSON |
| REP01 | — | `IF010_5` | — | 13407 comparados, 6 fallas (p. ej. ('BP__PACIFICO', '2021-05', 6.3294, 6.0542)) | — | ❌ queda JSON |
| REP01 | — | `IF010_6` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| REP01 | — | `IF011` | Σ +14 −1499 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ SUMA |
| REP01 | — | `IF011_1` | — | 13407 comparados, 116 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 51.3412, 50.9871)) | — | ❌ queda JSON |
| REP01 | — | `IF011_3` | — | 13407 comparados, 83 fallas (p. ej. ('BP__AUSTRO', '2021-05', 48.187, 47.871)) | — | ❌ queda JSON |
| REP01 | — | `IF011_5` | — | 13407 comparados, 9 fallas (p. ej. ('BP__PACIFICO', '2021-05', 44.7862, 44.4464)) | — | ❌ queda JSON |
| REP01 | — | `IF011_6` | — | 13407 comparados, 36 fallas (p. ej. ('BP__BOLIVARIANO', '2021-05', 3.8094, 3.711)) | — | ❌ queda JSON |
| REP01 | — | `IF012_3` | — | 8155 comparados, 65 fallas (p. ej. ('BP__AUSTRO', '2021-05', 1.3759, 1.384972)) | — | ❌ queda JSON |
| REP01 | — | `IF012_5` | — | 558 comparados, 6 fallas (p. ej. ('BP__PACIFICO', '2021-05', 14.1324, 13.518033)) | — | ❌ queda JSON |
| REP01 | — | `Indic_vulne_finan` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| REP01 | — | `Indic_vulne_finan_ANT12` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| REP01 | — | `Mar_Br_Fi` | Σ −41 −42 −43 +51 +52 +53 +54 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ SUMA |
| REP01 | — | `Mar_Inte` | Σ −41 −42 −43 −44 −45 +51 +52 +53 +54 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ SUMA |
| REP01 | — | `Mar_Ne_Fina` | Σ −41 −42 −43 −44 +51 +52 +53 +54 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ SUMA |
| REP01 | — | `Mar_Oper` | Σ −41 −42 −43 −44 −45 −46 +51 +52 +53 +54 +55 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ SUMA |
| REP01 | — | `Marg_Ne` | Σ −41 +51 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ SUMA |
| REP01 | — | `Multi_CAMELS` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| REP01 | — | `Multi_solve` | — | fila siempre en cero en el JSON: no es una fórmula | — | ❌ queda JSON |
| REP01 | — | `SB002` | — | 13407 comparados, 169 fallas (p. ej. ('BP__AMAZONAS', '2022-09', 278.8228, 280.7454)) | — | ❌ queda JSON |
| REP01 | — | `SB005` | Σ +1101 +1102 +1104 +1105 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ SUMA |
| REP01 | — | `SB007` | — | 13407 comparados, 250 fallas (p. ej. ('BP__AMAZONAS', '2021-05', 204.9065, 217.5403)) | — | ❌ queda JSON |
| REP01 | — | `SB008` | Σ +1499 +1699 +1799 +1999 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ SUMA |

**Aceptadas:** 28 fórmulas (entidad + sector) · análisis vertical: sí.

