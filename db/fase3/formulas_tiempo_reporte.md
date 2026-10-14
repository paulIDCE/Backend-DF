# Fase 3 · paso 2 — operaciones en el tiempo

Validación con meses reales 2021-05 → 2026-08 (último mes de la BD: 2026-08); 229 entidades y 10 sectores. Los meses sin historia suficiente (p. ej. el análisis horizontal antes de 2022‑05) no se comparan.

Tolerancia: redondeo de los saldos publicados (4 decimales en millones) propagado por la operación y el cociente, más el redondeo del valor publicado (2 o 4 decimales, según la serie).

## 1. Análisis horizontal (EFI06 / SFN06)

Regla: 100 × VARIACION_12(cuenta) / RETARDO_12(cuenta) = 100 × (saldo_t / saldo_{t−12} − 1). Publicado con 2 decimales.

| Ámbito | Tipo | Valores comparados | Fallas | Excluidas: 0 publicado | Excluidas: historia reprocesada | Ejemplos de falla |
|---|---|---|---|---|---|---|
| Entidad | BP | 589,955 | 0 | 45 | 3,446 |  |
| Entidad | COOP | 2,884,473 | 0 | 5,024 | 73,717 |  |
| Entidad | MU | 89,666 | 0 | 352 | 1,548 |  |
| Entidad | OYS (anómala) | 0 | 0 | 0 | 0 |  |
| Sector | SECTOR | 468,188 | 0 | 0 | 0 |  |

**Resultado:** ✅ se carga como RATIO en EFI06 y SFN06.

**Historia reprocesada:** 436 entidad‑meses de 42 entidades en que el JSON publicó el análisis horizontal con un balance del año anterior distinto del que publica hoy: en ese mes falla también una cuenta agregada (1 o 2 dígitos; casi siempre el ACTIVO). La BD tiene el balance corregido, así que el SP da la tasa coherente con los saldos publicados. Entidades con más meses: COAC___4_RIOS (22), COAC___EL_MOLINO_LIMITADA (22), COAC___GRUPO_DIFARE (21), COAC___ECUACREDITOS_LTDA (20), COAC___PUCARA_LTDA (14), COAC___CAMPESINA_COOPAC (12), BP__DELBANK (11), COAC___15_DE_ABRIL_LTDA (11).

**0 publicado:** el JSON pone 0 cuando no puede calcular la tasa; esos valores no se comparan.

## 2. Anualizado (@cuentaA)

187 de 221 aceptadas.

| Cuadro | Sub-vista | Código / texto | Fórmula | Entidades | Sector | Resultado |
|---|---|---|---|---|---|---|
| EFI03 | — | `@4101A` | ANUALIZADO(+4101) · cuenta 4101 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4102A` | ANUALIZADO(+4102) · cuenta 4102 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4103A` | ANUALIZADO(+4103) · cuenta 4103 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4104A` | ANUALIZADO(+4104) · cuenta 4104 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4105A` | ANUALIZADO(+4105) · cuenta 4105 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@41A` | ANUALIZADO(+41) · cuenta 41 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4201A` | ANUALIZADO(+4201) · cuenta 4201 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4202A` | ANUALIZADO(+4202) · cuenta 4202 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4203A` | ANUALIZADO(+4203) · cuenta 4203 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4204A` | — | ANUALIZADO(+4204): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@4205A` | ANUALIZADO(+4205) · cuenta 4205 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4290A` | ANUALIZADO(+4290) · cuenta 4290 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@42A` | ANUALIZADO(+42) · cuenta 42 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4301A` | — | ANUALIZADO(+4301): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@4302A` | ANUALIZADO(+4302) · cuenta 4302 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4303A` | ANUALIZADO(+4303) · cuenta 4303 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4304A` | ANUALIZADO(+4304) · cuenta 4304 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4305A` | ANUALIZADO(+4305) · cuenta 4305 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4306A` | ANUALIZADO(+4306) · cuenta 4306 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4307A` | ANUALIZADO(+4307) · cuenta 4307 | 12164 comparados, 0 fallas | ✅ SFN03: 384 comparados, 0 fallas | ✅ |
| EFI03 | — | `@43A` | ANUALIZADO(+43) · cuenta 43 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4401A` | ANUALIZADO(+4401) · cuenta 4401 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@440210A` | ANUALIZADO(+440210) · cuenta 440210 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@440220A` | ANUALIZADO(+440220) · cuenta 440220 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@440230A` | ANUALIZADO(+440230) · cuenta 440230 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@440235A` | ANUALIZADO(+440235) · cuenta 440235 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@440240A` | ANUALIZADO(+440240) · cuenta 440240 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@440245A` | ANUALIZADO(+440245) · cuenta 440245 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4402A` | ANUALIZADO(+4402) · cuenta 4402 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4403A` | ANUALIZADO(+4403) · cuenta 4403 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4404A` | ANUALIZADO(+4404) · cuenta 4404 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4405A` | ANUALIZADO(+4405) · cuenta 4405 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4406A` | ANUALIZADO(+4406) · cuenta 4406 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4407A` | — | ANUALIZADO(+4407): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@44A` | ANUALIZADO(+44) · cuenta 44 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4501A` | ANUALIZADO(+4501) · cuenta 4501 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4502A` | ANUALIZADO(+4502) · cuenta 4502 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4503A` | ANUALIZADO(+4503) · cuenta 4503 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4504A` | ANUALIZADO(+4504) · cuenta 4504 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4505A` | ANUALIZADO(+4505) · cuenta 4505 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4506A` | ANUALIZADO(+4506) · cuenta 4506 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4507A` | ANUALIZADO(+4507) · cuenta 4507 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@45A` | ANUALIZADO(+45) · cuenta 45 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4601A` | ANUALIZADO(+4601) · cuenta 4601 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4602A` | — | ANUALIZADO(+4602): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@4690A` | ANUALIZADO(+4690) · cuenta 4690 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@46A` | ANUALIZADO(+46) · cuenta 46 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4701A` | ANUALIZADO(+4701) · cuenta 4701 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4702A` | ANUALIZADO(+4702) · cuenta 4702 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4703A` | ANUALIZADO(+4703) · cuenta 4703 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4790A` | ANUALIZADO(+4790) · cuenta 4790 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@47A` | ANUALIZADO(+47) · cuenta 47 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4810A` | ANUALIZADO(+4810) · cuenta 4810 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4815A` | ANUALIZADO(+4815) · cuenta 4815 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@4890A` | ANUALIZADO(+4890) · cuenta 4890 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@48A` | ANUALIZADO(+48) · cuenta 48 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5101A` | ANUALIZADO(+5101) · cuenta 5101 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5102A` | ANUALIZADO(+5102) · cuenta 5102 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5103A` | ANUALIZADO(+5103) · cuenta 5103 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@510405A` | ANUALIZADO(+510405) · cuenta 510405 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@510410A` | ANUALIZADO(+510410) · cuenta 510410 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@510415A` | ANUALIZADO(+510415) · cuenta 510415 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@510420A` | ANUALIZADO(+510420) · cuenta 510420 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@510427A` | ANUALIZADO(+510427) · cuenta 510427 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@510428A` | ANUALIZADO(+510428) · cuenta 510428 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@510430A` | ANUALIZADO(+510430) · cuenta 510430 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@510435A` | ANUALIZADO(+510435) · cuenta 510435 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@510450A` | ANUALIZADO(+510450) · cuenta 510450 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@510455A` | ANUALIZADO(+510455) · cuenta 510455 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5104A` | ANUALIZADO(+5104) · cuenta 5104 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5190A` | ANUALIZADO(+5190) · cuenta 5190 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@51A` | ANUALIZADO(+51) · cuenta 51 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@520105A` | — | ANUALIZADO(+520105): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@520110A` | — | ANUALIZADO(+520110): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@520115A` | — | ANUALIZADO(+520115): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@520120A` | — | ANUALIZADO(+520120): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@520125A` | — | ANUALIZADO(+520125): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@5201A` | — | ANUALIZADO(+5201): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@5202A` | — | ANUALIZADO(+5202): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@5203A` | ANUALIZADO(+5203) · cuenta 5203 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5204A` | ANUALIZADO(+5204) · cuenta 5204 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5205A` | ANUALIZADO(+5205) · cuenta 5205 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5290A` | ANUALIZADO(+5290) · cuenta 5290 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@52A` | ANUALIZADO(+52) · cuenta 52 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5301A` | — | ANUALIZADO(+5301): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@5302A` | ANUALIZADO(+5302) · cuenta 5302 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5303A` | ANUALIZADO(+5303) · cuenta 5303 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5304A` | ANUALIZADO(+5304) · cuenta 5304 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5305A` | — | ANUALIZADO(+5305): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@5390A` | ANUALIZADO(+5390) · cuenta 5390 | 12164 comparados, 0 fallas | ✅ SFN03: 384 comparados, 0 fallas | ✅ |
| EFI03 | — | `@53A` | ANUALIZADO(+53) · cuenta 53 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5401A` | ANUALIZADO(+5401) · cuenta 5401 | 11909 comparados, 0 fallas | ✅ SFN03: 320 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5404A` | — | ANUALIZADO(+5404): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@5405A` | ANUALIZADO(+5405) · cuenta 5405 | 11909 comparados, 0 fallas | ✅ SFN03: 320 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5406A` | — | ANUALIZADO(+5406): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@5407A` | — | ANUALIZADO(+5407): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@5490A` | ANUALIZADO(+5490) · cuenta 5490 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@54A` | ANUALIZADO(+54) · cuenta 54 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5501A` | ANUALIZADO(+5501) · cuenta 5501 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5502A` | — | ANUALIZADO(+5502): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI03 | — | `@5503A` | ANUALIZADO(+5503) · cuenta 5503 | 12164 comparados, 0 fallas | ✅ SFN03: 384 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5590A` | ANUALIZADO(+5590) · cuenta 5590 | 13635 comparados, 0 fallas (OYS: 1 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@55A` | ANUALIZADO(+55) · cuenta 55 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5601A` | ANUALIZADO(+5601) · cuenta 5601 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5602A` | ANUALIZADO(+5602) · cuenta 5602 | 13635 comparados, 0 fallas | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5603A` | ANUALIZADO(+5603) · cuenta 5603 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5604A` | ANUALIZADO(+5604) · cuenta 5604 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5690A` | ANUALIZADO(+5690) · cuenta 5690 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@56A` | ANUALIZADO(+56) · cuenta 56 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| EFI03 | — | `@5A` | ANUALIZADO(+5) · cuenta 5 | 13635 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN03: 640 comparados, 0 fallas | ✅ |
| REP01 | — | `@4101A` | ANUALIZADO(+4101) · cuenta 4101 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@4102A` | ANUALIZADO(+4102) · cuenta 4102 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4103A` | ANUALIZADO(+4103) · cuenta 4103 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@4104A` | ANUALIZADO(+4104) · cuenta 4104 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4105A` | ANUALIZADO(+4105) · cuenta 4105 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@41A` | ANUALIZADO(+41) · cuenta 41 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@4201A` | ANUALIZADO(+4201) · cuenta 4201 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4202A` | ANUALIZADO(+4202) · cuenta 4202 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4203A` | ANUALIZADO(+4203) · cuenta 4203 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4204A` | — | ANUALIZADO(+4204): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@4205A` | ANUALIZADO(+4205) · cuenta 4205 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4290A` | ANUALIZADO(+4290) · cuenta 4290 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@42A` | ANUALIZADO(+42) · cuenta 42 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4301A` | — | ANUALIZADO(+4301): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@4302A` | ANUALIZADO(+4302) · cuenta 4302 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4303A` | ANUALIZADO(+4303) · cuenta 4303 | 13407 comparados, 0 fallas (OYS: 1 fallas de datos) | — | ✅ |
| REP01 | — | `@4304A` | ANUALIZADO(+4304) · cuenta 4304 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4305A` | ANUALIZADO(+4305) · cuenta 4305 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4306A` | ANUALIZADO(+4306) · cuenta 4306 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4307A` | ANUALIZADO(+4307) · cuenta 4307 | 11959 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@43A` | ANUALIZADO(+43) · cuenta 43 | 13407 comparados, 0 fallas (OYS: 1 fallas de datos) | — | ✅ |
| REP01 | — | `@4401A` | ANUALIZADO(+4401) · cuenta 4401 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@440210A` | ANUALIZADO(+440210) · cuenta 440210 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@440220A` | ANUALIZADO(+440220) · cuenta 440220 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@440230A` | ANUALIZADO(+440230) · cuenta 440230 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@440235A` | ANUALIZADO(+440235) · cuenta 440235 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@440240A` | ANUALIZADO(+440240) · cuenta 440240 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@440245A` | ANUALIZADO(+440245) · cuenta 440245 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4402A` | ANUALIZADO(+4402) · cuenta 4402 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@4403A` | ANUALIZADO(+4403) · cuenta 4403 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@4404A` | ANUALIZADO(+4404) · cuenta 4404 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4405A` | ANUALIZADO(+4405) · cuenta 4405 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4406A` | ANUALIZADO(+4406) · cuenta 4406 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4407A` | — | ANUALIZADO(+4407): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@44A` | ANUALIZADO(+44) · cuenta 44 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@4501A` | ANUALIZADO(+4501) · cuenta 4501 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@4502A` | ANUALIZADO(+4502) · cuenta 4502 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@4503A` | ANUALIZADO(+4503) · cuenta 4503 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@4504A` | ANUALIZADO(+4504) · cuenta 4504 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@4505A` | ANUALIZADO(+4505) · cuenta 4505 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4506A` | ANUALIZADO(+4506) · cuenta 4506 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4507A` | ANUALIZADO(+4507) · cuenta 4507 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@45A` | ANUALIZADO(+45) · cuenta 45 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@4601A` | ANUALIZADO(+4601) · cuenta 4601 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4602A` | — | ANUALIZADO(+4602): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@4690A` | ANUALIZADO(+4690) · cuenta 4690 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@46A` | ANUALIZADO(+46) · cuenta 46 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4701A` | ANUALIZADO(+4701) · cuenta 4701 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4702A` | ANUALIZADO(+4702) · cuenta 4702 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@4703A` | ANUALIZADO(+4703) · cuenta 4703 | 13407 comparados, 0 fallas (OYS: 1 fallas de datos) | — | ✅ |
| REP01 | — | `@4790A` | ANUALIZADO(+4790) · cuenta 4790 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@47A` | ANUALIZADO(+47) · cuenta 47 | 13407 comparados, 0 fallas (OYS: 1 fallas de datos) | — | ✅ |
| REP01 | — | `@4810A` | ANUALIZADO(+4810) · cuenta 4810 | 13407 comparados, 0 fallas (OYS: 1 fallas de datos) | — | ✅ |
| REP01 | — | `@4815A` | ANUALIZADO(+4815) · cuenta 4815 | 13407 comparados, 0 fallas (OYS: 1 fallas de datos) | — | ✅ |
| REP01 | — | `@4890A` | ANUALIZADO(+4890) · cuenta 4890 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@48A` | ANUALIZADO(+48) · cuenta 48 | 13407 comparados, 0 fallas (OYS: 1 fallas de datos) | — | ✅ |
| REP01 | — | `@4A` | ANUALIZADO(+4) · cuenta 4 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@5101A` | ANUALIZADO(+5101) · cuenta 5101 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@5102A` | ANUALIZADO(+5102) · cuenta 5102 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5103A` | ANUALIZADO(+5103) · cuenta 5103 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@510405A` | ANUALIZADO(+510405) · cuenta 510405 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@510410A` | ANUALIZADO(+510410) · cuenta 510410 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@510415A` | ANUALIZADO(+510415) · cuenta 510415 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@510420A` | ANUALIZADO(+510420) · cuenta 510420 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@510427A` | ANUALIZADO(+510427) · cuenta 510427 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@510428A` | ANUALIZADO(+510428) · cuenta 510428 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@510430A` | ANUALIZADO(+510430) · cuenta 510430 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@510435A` | ANUALIZADO(+510435) · cuenta 510435 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@510450A` | ANUALIZADO(+510450) · cuenta 510450 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@510455A` | ANUALIZADO(+510455) · cuenta 510455 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5104A` | ANUALIZADO(+5104) · cuenta 5104 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@5190A` | ANUALIZADO(+5190) · cuenta 5190 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@51A` | ANUALIZADO(+51) · cuenta 51 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@520105A` | — | ANUALIZADO(+520105): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@520110A` | — | ANUALIZADO(+520110): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@520115A` | — | ANUALIZADO(+520115): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@520120A` | — | ANUALIZADO(+520120): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@520125A` | — | ANUALIZADO(+520125): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@5201A` | — | ANUALIZADO(+5201): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@5202A` | — | ANUALIZADO(+5202): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@5203A` | ANUALIZADO(+5203) · cuenta 5203 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5204A` | ANUALIZADO(+5204) · cuenta 5204 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5205A` | ANUALIZADO(+5205) · cuenta 5205 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5290A` | ANUALIZADO(+5290) · cuenta 5290 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@52A` | ANUALIZADO(+52) · cuenta 52 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5301A` | — | ANUALIZADO(+5301): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@5302A` | ANUALIZADO(+5302) · cuenta 5302 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5303A` | ANUALIZADO(+5303) · cuenta 5303 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5304A` | ANUALIZADO(+5304) · cuenta 5304 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5305A` | — | ANUALIZADO(+5305): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@5390A` | ANUALIZADO(+5390) · cuenta 5390 | 11959 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@53A` | ANUALIZADO(+53) · cuenta 53 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5401A` | ANUALIZADO(+5401) · cuenta 5401 | 11708 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5404A` | — | ANUALIZADO(+5404): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@5405A` | ANUALIZADO(+5405) · cuenta 5405 | 11708 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5406A` | — | ANUALIZADO(+5406): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@5407A` | — | ANUALIZADO(+5407): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@5490A` | ANUALIZADO(+5490) · cuenta 5490 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@54A` | ANUALIZADO(+54) · cuenta 54 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@5501A` | ANUALIZADO(+5501) · cuenta 5501 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@5502A` | — | ANUALIZADO(+5502): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| REP01 | — | `@5503A` | ANUALIZADO(+5503) · cuenta 5503 | 11959 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5590A` | ANUALIZADO(+5590) · cuenta 5590 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@55A` | ANUALIZADO(+55) · cuenta 55 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@5601A` | ANUALIZADO(+5601) · cuenta 5601 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5602A` | ANUALIZADO(+5602) · cuenta 5602 | 13407 comparados, 0 fallas | — | ✅ |
| REP01 | — | `@5603A` | ANUALIZADO(+5603) · cuenta 5603 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@5604A` | ANUALIZADO(+5604) · cuenta 5604 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@5690A` | ANUALIZADO(+5690) · cuenta 5690 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@56A` | ANUALIZADO(+56) · cuenta 56 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |
| REP01 | — | `@5A` | ANUALIZADO(+5) · cuenta 5 | 13407 comparados, 0 fallas (OYS: 3 fallas de datos) | — | ✅ |

## 3. Variación mensual (EFI04)

23 de 26 aceptadas.

| Cuadro | Sub-vista | Código / texto | Fórmula | Entidades | Sector | Resultado |
|---|---|---|---|---|---|---|
| EFI04 | — | `ACEPTACIONES EN CIRCULACION` | — | VARIACION_1(+24): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI04 | — | `ACTIVO` | VARIACION_1(+1) · EFI01 @1 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `BIENES REALIZABLES ADJUDICADOS POR PAGO DE ARRENDAMIENTO MERCANTIL Y NO UTILIZADOS POR LA INSTITUCION` | VARIACION_1(+17) · EFI01 @17 | 13348 comparados, 0 fallas | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `CAPITAL SOCIAL` | VARIACION_1(+31) · EFI01 @31 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `CARTERA DE CREDITOS` | VARIACION_1(+14) · EFI01 @14 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `CUENTAS POR COBRAR` | VARIACION_1(+16) · EFI01 @16 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `CUENTAS POR PAGAR` | VARIACION_1(+25) · EFI01 @25 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `DEUDORES POR ACEPTACIONES` | — | VARIACION_1(+15): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI04 | — | `FONDOS DISPONIBLES` | VARIACION_1(+11) · EFI01 @11 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `INVERSIONES` | VARIACION_1(+13) · EFI01 @13 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `OBLIGACIONES CON EL PUBLICO` | VARIACION_1(+21) · EFI01 @21 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `OBLIGACIONES CONVERTIBLES EN ACCIONES Y APORTES PARA FUTURA CAPITALIZACION` | VARIACION_1(+28) · EFI01 @28 | 13348 comparados, 0 fallas | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `OBLIGACIONES FINANCIERAS` | VARIACION_1(+26) · EFI01 @26 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `OBLIGACIONES INMEDIATAS` | VARIACION_1(+23) · EFI01 @23 | 13348 comparados, 0 fallas | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `OPE. INTERBANCARIAS` | VARIACION_1(+22) · EFI01 @22 | 13348 comparados, 0 fallas | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `OPERACIONES INTERBANCARIAS` | VARIACION_1(+12) · EFI01 @12 | 13348 comparados, 0 fallas | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `OTROS ACTIVOS` | VARIACION_1(+19) · EFI01 @19 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `OTROS APORTES PATRIMONIALES` | VARIACION_1(+34) · EFI01 @34 | 13348 comparados, 0 fallas | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `OTROS PASIVOS` | VARIACION_1(+29) · EFI01 @29 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `PATRIMONIO` | VARIACION_1(+3) · EFI01 @3 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `PRIMA O DESCUENTO EN COLOCACION DE ACCIONES Y CERTIFICADOS` | — | VARIACION_1(+32): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI04 | — | `PROPIEDADES Y EQUIPO` | VARIACION_1(+18) · EFI01 @18 | 13348 comparados, 0 fallas | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `RESERVAS` | VARIACION_1(+33) · EFI01 @33 | 13348 comparados, 0 fallas (OYS: 3 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `RESULTADOS` | VARIACION_1(+36) · EFI01 @36 | 13348 comparados, 0 fallas (OYS: 1 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `SUPERAVIT POR VALUACIONES` | VARIACION_1(+35) · EFI01 @35 | 13348 comparados, 0 fallas (OYS: 1 fallas de datos) | ✅ SFN04: 630 comparados, 0 fallas | ✅ |
| EFI04 | — | `VALORES EN CIRCULACION` | VARIACION_1(+27) · EFI01 @27 | 13348 comparados, 0 fallas | ✅ SFN04: 630 comparados, 0 fallas | ✅ |

## 4. Tasas a 12 meses (tasa_X)

70 de 232 aceptadas.

| Cuadro | Sub-vista | Código / texto | Fórmula | Entidades | Sector | Resultado |
|---|---|---|---|---|---|---|
| EFI07 | Cartera Total | `tasa_14` | 100·VARIACION_12(+14) / RETARDO_12(+14) · cuenta 14 | 10846 comparados, 0 fallas; excluidas por el propio JSON: 437 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 15.937, 15.0491)), 13 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___CREDI_FACIL_LTDA', '2024-02', 0.0, 42.2623)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Cartera Total | `tasa_1491` | — | 100·VARIACION_12(+1491) / RETARDO_12(+1491): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_1492` | — | 100·VARIACION_12(+1492) / RETARDO_12(+1492): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_1493` | — | 100·VARIACION_12(+1493) / RETARDO_12(+1493): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_1494` | — | 100·VARIACION_12(+1494) / RETARDO_12(+1494): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_1495` | — | 100·VARIACION_12(+1495) / RETARDO_12(+1495): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_1496` | — | 100·VARIACION_12(+1496) / RETARDO_12(+1496): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_1499` | 100·VARIACION_12(+1499) / RETARDO_12(+1499) · cuenta 1499 | 10846 comparados, 0 fallas; excluidas por el propio JSON: 322 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -2.5635, 3.2294)), 13 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___CREDI_FACIL_LTDA', '2024-02', 0.0, 112.0895)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Cartera Total | `tasa_IF007` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_IF008` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_IF009` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_IF010` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_IF011` | 100·VARIACION_12(+14 −1499) / RETARDO_12(+14 −1499) · SUMA IF011 | 10846 comparados, 0 fallas; excluidas por el propio JSON: 437 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 15.2789, 14.6544)), 13 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___CREDI_FACIL_LTDA', '2024-02', 0.0, 46.9522)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Cartera Total | `tasa_Impro_rees` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_Impro_refi` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_Por_vencer` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_Por_vencida` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_Rees_vencer` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_Rees_vencida` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_Refi_vencer` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Cartera Total | `tasa_Refi_vencida` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Consumo | `tasa_1402` | 100·VARIACION_12(+1402) / RETARDO_12(+1402) · cuenta 1402 | 10518 comparados, 0 fallas; excluidas por el propio JSON: 400 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 11.0708, 4.933)), 11 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___UNIDAD_Y_PROGRESO', '2024-07', 0.0, -82.8814)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Consumo | `tasa_1410` | 100·VARIACION_12(+1410) / RETARDO_12(+1410) · cuenta 1410 | 5024 comparados, 0 fallas; excluidas por el propio JSON: 80 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -1.3371, -15.8202)), 4 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___MARCABELI_LTDA', '2025-03', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Consumo | `tasa_1418` | 100·VARIACION_12(+1418) / RETARDO_12(+1418) · cuenta 1418 | 5475 comparados, 0 fallas; excluidas por el propio JSON: 103 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -53.1075, -49.7863)), 8 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___SAN_CRISTOBAL_LTDA', '2024-10', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Consumo | `tasa_1426` | 100·VARIACION_12(+1426) / RETARDO_12(+1426) · cuenta 1426 | 10060 comparados, 0 fallas; excluidas por el propio JSON: 360 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -14.1915, 16.4875)), 17 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___METROPOLITANA_LTDA', '2026-05', 0.0, -86.1111)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Consumo | `tasa_1434` | 100·VARIACION_12(+1434) / RETARDO_12(+1434) · cuenta 1434 | 3581 comparados, 0 fallas; excluidas por el propio JSON: 28 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 1007.0949, 1054.2254)), 6 con 0 publicado (valor faltante en el JSON) (p. ej. ('MUT___AMBATO', '2022-06', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Consumo | `tasa_1442` | 100·VARIACION_12(+1442) / RETARDO_12(+1442) · cuenta 1442 | 3742 comparados, 0 fallas; excluidas por el propio JSON: 69 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 110.8722, 55.6777)), 5 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___13_DE_ABRIL', '2026-05', 0.0, 13433.3333)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Consumo | `tasa_1450` | 100·VARIACION_12(+1450) / RETARDO_12(+1450) · cuenta 1450 | 10280 comparados, 0 fallas; excluidas por el propio JSON: 349 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -35.2028, 12.5)), 13 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___UNIDAD_Y_PROGRESO', '2024-07', 0.0, -99.9981)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Consumo | `tasa_1458` | 100·VARIACION_12(+1458) / RETARDO_12(+1458) · cuenta 1458 | 3999 comparados, 0 fallas; excluidas por el propio JSON: 31 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 148.9239, 162.069)), 2 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___UNIVERSIDAD_CATOLICA_DEL_ECUADOR', '2026-07', 0.0, 750.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Consumo | `tasa_1466` | 100·VARIACION_12(+1466) / RETARDO_12(+1466) · cuenta 1466 | 4945 comparados, 0 fallas; excluidas por el propio JSON: 58 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 65.5481, 37.037)), 2 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___CACPE_CELICA', '2024-12', 0.0, -99.9615)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Consumo | `tasa_149120` | — | 100·VARIACION_12(+149120) / RETARDO_12(+149120): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Consumo | `tasa_149220` | — | 100·VARIACION_12(+149220) / RETARDO_12(+149220): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Consumo | `tasa_149320` | — | 100·VARIACION_12(+149320) / RETARDO_12(+149320): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Consumo | `tasa_149420` | — | 100·VARIACION_12(+149420) / RETARDO_12(+149420): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Consumo | `tasa_149520` | — | 100·VARIACION_12(+149520) / RETARDO_12(+149520): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Consumo | `tasa_149620` | — | 100·VARIACION_12(+149620) / RETARDO_12(+149620): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Consumo | `tasa_149910` | 100·VARIACION_12(+149910) / RETARDO_12(+149910) · cuenta 149910 | 10493 comparados, 0 fallas; excluidas por el propio JSON: 266 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -23.4098, -12.2685)), 9 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___CREDI_FACIL_LTDA', '2024-02', 0.0, 38.7755)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Consumo | `tasa_IF0072` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Consumo | `tasa_IF0082` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Consumo | `tasa_IF0092` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Consumo | `tasa_IF0102` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Consumo | `tasa_IF0112` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Educativo | `tasa_1473` | 100·VARIACION_12(+1473) / RETARDO_12(+1473) · cuenta 1473 | 803 comparados, 0 fallas; excluidas por el propio JSON: 2 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___UNIVERSIDAD_CATOLICA_DEL_ECUADOR', '2026-07', 40.265, 42.2535)) | ✅ CAR01: 468 comparados, 0 fallas | ✅ |
| EFI07 | Educativo | `tasa_1475` | 100·VARIACION_12(+1475) / RETARDO_12(+1475) · cuenta 1475 | 252 comparados, 0 fallas | ✅ CAR01: 284 comparados, 0 fallas | ✅ |
| EFI07 | Educativo | `tasa_1477` | 100·VARIACION_12(+1477) / RETARDO_12(+1477) · cuenta 1477 | 168 comparados, 0 fallas | ✅ CAR01: 253 comparados, 0 fallas | ✅ |
| EFI07 | Educativo | `tasa_1479` | 100·VARIACION_12(+1479) / RETARDO_12(+1479) · cuenta 1479 | 400 comparados, 0 fallas | ✅ CAR01: 319 comparados, 0 fallas | ✅ |
| EFI07 | Educativo | `tasa_1481` | 100·VARIACION_12(+1481) / RETARDO_12(+1481) · cuenta 1481 | 32 comparados, 0 fallas | ✅ CAR01: 85 comparados, 0 fallas | ✅ |
| EFI07 | Educativo | `tasa_1483` | 100·VARIACION_12(+1483) / RETARDO_12(+1483) · cuenta 1483 | 124 comparados, 0 fallas | ✅ CAR01: 242 comparados, 0 fallas | ✅ |
| EFI07 | Educativo | `tasa_1485` | 100·VARIACION_12(+1485) / RETARDO_12(+1485) · cuenta 1485 | 474 comparados, 0 fallas; excluidas por el propio JSON: 1 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___UNIVERSIDAD_CATOLICA_DEL_ECUADOR', '2026-07', -55.8655, -23.0769)) | ✅ CAR01: 396 comparados, 0 fallas | ✅ |
| EFI07 | Educativo | `tasa_1487` | 100·VARIACION_12(+1487) / RETARDO_12(+1487) · cuenta 1487 | 28 comparados, 0 fallas | ✅ CAR01: 72 comparados, 0 fallas | ✅ |
| EFI07 | Educativo | `tasa_1489` | 100·VARIACION_12(+1489) / RETARDO_12(+1489) · cuenta 1489 | 126 comparados, 0 fallas | ✅ CAR01: 247 comparados, 0 fallas | ✅ |
| EFI07 | Educativo | `tasa_149955` | 100·VARIACION_12(+149955) / RETARDO_12(+149955) · cuenta 149955 | 835 comparados, 0 fallas; excluidas por el propio JSON: 1 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___UNIVERSIDAD_CATOLICA_DEL_ECUADOR', '2026-07', -60.3659, -28.5714)) | ✅ CAR01: 468 comparados, 0 fallas | ✅ |
| EFI07 | Educativo | `tasa_IF0076` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Educativo | `tasa_IF0086` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Educativo | `tasa_IF0096` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Educativo | `tasa_IF0106` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Educativo | `tasa_IF0116` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `tasa_1403` | 100·VARIACION_12(+1403) / RETARDO_12(+1403) · cuenta 1403 | 6232 comparados, 0 fallas; excluidas por el propio JSON: 204 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 74.9331, 44.5754)), 3 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___UNIDAD_Y_PROGRESO', '2024-07', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Inmobiliario | `tasa_1411` | 100·VARIACION_12(+1411) / RETARDO_12(+1411) · cuenta 1411 | 2576 comparados, 0 fallas; excluidas por el propio JSON: 29 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -83.959, -83.5881)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Inmobiliario | `tasa_1419` | 100·VARIACION_12(+1419) / RETARDO_12(+1419) · cuenta 1419 | 2074 comparados, 0 fallas; excluidas por el propio JSON: 15 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2024-08', -14.5093, -14.1693)), 1 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___MAGISTERIO_MANABITA_LIMITADA', '2025-12', 0.0, -19.403)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Inmobiliario | `tasa_1427` | 100·VARIACION_12(+1427) / RETARDO_12(+1427) · cuenta 1427 | 4208 comparados, 0 fallas; excluidas por el propio JSON: 89 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -64.049, -62.2478)), 7 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___MAGISTERIO_MANABITA_LIMITADA', '2025-09', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Inmobiliario | `tasa_1435` | 100·VARIACION_12(+1435) / RETARDO_12(+1435) · cuenta 1435 | 1436 comparados, 0 fallas; excluidas por el propio JSON: 2 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-08', -83.2192, -82.6087)), 2 con 0 publicado (valor faltante en el JSON) (p. ej. ('BP__DELBANK', '2024-07', 0.0, -80.9677)) | ✅ CAR01: 511 comparados, 0 fallas | ✅ |
| EFI07 | Inmobiliario | `tasa_1443` | 100·VARIACION_12(+1443) / RETARDO_12(+1443) · cuenta 1443 | 717 comparados, 0 fallas; excluidas por el propio JSON: 1 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2025-05', 0.0, -100.0)) | ✅ CAR01: 409 comparados, 0 fallas | ✅ |
| EFI07 | Inmobiliario | `tasa_1451` | 100·VARIACION_12(+1451) / RETARDO_12(+1451) · cuenta 1451 | 5248 comparados, 0 fallas; excluidas por el propio JSON: 85 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -48.329, -54.7541)), 4 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___ECUACREDITOS_LTDA', '2024-11', 0.0, 1050.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Inmobiliario | `tasa_1459` | 100·VARIACION_12(+1459) / RETARDO_12(+1459) · cuenta 1459 | 1581 comparados, 0 fallas; excluidas por el propio JSON: 3 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-09', 39.8337, 5.0)), 1 con 0 publicado (valor faltante en el JSON) (p. ej. ('BP__DELBANK', '2024-07', 0.0, -6.6667)) | ✅ CAR01: 512 comparados, 0 fallas | ✅ |
| EFI07 | Inmobiliario | `tasa_1467` | 100·VARIACION_12(+1467) / RETARDO_12(+1467) · cuenta 1467 | 1208 comparados, 0 fallas | ✅ CAR01: 475 comparados, 0 fallas | ✅ |
| EFI07 | Inmobiliario | `tasa_149130` | — | 100·VARIACION_12(+149130) / RETARDO_12(+149130): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `tasa_149230` | — | 100·VARIACION_12(+149230) / RETARDO_12(+149230): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `tasa_149330` | — | 100·VARIACION_12(+149330) / RETARDO_12(+149330): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `tasa_149430` | — | 100·VARIACION_12(+149430) / RETARDO_12(+149430): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `tasa_149530` | — | 100·VARIACION_12(+149530) / RETARDO_12(+149530): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `tasa_149630` | — | 100·VARIACION_12(+149630) / RETARDO_12(+149630): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `tasa_149915` | 100·VARIACION_12(+149915) / RETARDO_12(+149915) · cuenta 149915 | 6512 comparados, 0 fallas; excluidas por el propio JSON: 85 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 204.6095, 118.5185)), 4 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___UNIDAD_Y_PROGRESO', '2024-07', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Inmobiliario | `tasa_IF0073` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `tasa_IF0083` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `tasa_IF0093` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `tasa_IF0103` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Inmobiliario | `tasa_IF0113` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Microcrédito | `tasa_1404` | 100·VARIACION_12(+1404) / RETARDO_12(+1404) · cuenta 1404 | 9902 comparados, 0 fallas; excluidas por el propio JSON: 377 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 2081.2476, 2274.0741)), 13 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___SAN_CRISTOBAL_LTDA', '2024-10', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Microcrédito | `tasa_1412` | 100·VARIACION_12(+1412) / RETARDO_12(+1412) · cuenta 1412 | 5362 comparados, 0 fallas; excluidas por el propio JSON: 91 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2024-08', 180.0716, 172.4224)), 4 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___CACPE_CELICA', '2024-10', 0.0, -60.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Microcrédito | `tasa_1420` | 100·VARIACION_12(+1420) / RETARDO_12(+1420) · cuenta 1420 | 5842 comparados, 0 fallas; excluidas por el propio JSON: 98 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2024-08', 15.6157, 23.7791)), 3 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___UNIDAD_Y_PROGRESO', '2024-08', 0.0, 16.6667)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Microcrédito | `tasa_1428` | 100·VARIACION_12(+1428) / RETARDO_12(+1428) · cuenta 1428 | 9679 comparados, 0 fallas; excluidas por el propio JSON: 361 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2024-08', 19.3171, 19.3656)), 13 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___4_RIOS', '2024-08', 0.0, 11542.8571)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Microcrédito | `tasa_1436` | 100·VARIACION_12(+1436) / RETARDO_12(+1436) · cuenta 1436 | 4396 comparados, 0 fallas; excluidas por el propio JSON: 50 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2024-09', 43.0545, 44.4444)), 10 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___SAN_ANTONIO_LTDA___IMBABURA', '2025-12', 0.0, 118.5328)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Microcrédito | `tasa_1444` | 100·VARIACION_12(+1444) / RETARDO_12(+1444) · cuenta 1444 | 4622 comparados, 0 fallas; excluidas por el propio JSON: 62 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2024-08', -34.5158, -41.5159)), 12 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___SAN_PEDRO_LTDA', '2025-02', 0.0, -100.0)) | ✅ CAR01: 517 comparados, 0 fallas | ✅ |
| EFI07 | Microcrédito | `tasa_1452` | 100·VARIACION_12(+1452) / RETARDO_12(+1452) · cuenta 1452 | 9744 comparados, 0 fallas; excluidas por el propio JSON: 358 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2024-08', 41.7362, 40.3902)), 13 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___4_RIOS', '2024-08', 0.0, 416.6667)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Microcrédito | `tasa_1460` | 100·VARIACION_12(+1460) / RETARDO_12(+1460) · cuenta 1460 | 5034 comparados, 0 fallas; excluidas por el propio JSON: 61 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2024-08', 209.9115, 209.4136)), 4 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___MARCABELI_LTDA', '2025-05', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Microcrédito | `tasa_1468` | 100·VARIACION_12(+1468) / RETARDO_12(+1468) · cuenta 1468 | 5463 comparados, 0 fallas; excluidas por el propio JSON: 50 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2024-08', 4235.4228, 3334.6154)), 1 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___UNIDAD_Y_PROGRESO', '2024-08', 0.0, -66.6667)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Microcrédito | `tasa_149140` | — | 100·VARIACION_12(+149140) / RETARDO_12(+149140): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Microcrédito | `tasa_149240` | — | 100·VARIACION_12(+149240) / RETARDO_12(+149240): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Microcrédito | `tasa_149340` | — | 100·VARIACION_12(+149340) / RETARDO_12(+149340): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Microcrédito | `tasa_149440` | — | 100·VARIACION_12(+149440) / RETARDO_12(+149440): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Microcrédito | `tasa_149540` | — | 100·VARIACION_12(+149540) / RETARDO_12(+149540): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Microcrédito | `tasa_149640` | — | 100·VARIACION_12(+149640) / RETARDO_12(+149640): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Microcrédito | `tasa_149920` | 100·VARIACION_12(+149920) / RETARDO_12(+149920) · cuenta 149920 | 9875 comparados, 0 fallas; excluidas por el propio JSON: 274 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-09', 4301.7562, 2700.0)), 16 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___SAN_CRISTOBAL_LTDA', '2024-10', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Microcrédito | `tasa_IF0074` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Microcrédito | `tasa_IF0084` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Microcrédito | `tasa_IF0094` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Microcrédito | `tasa_IF0104` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Microcrédito | `tasa_IF0114` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Productivo | `tasa_1401` | 100·VARIACION_12(+1401) / RETARDO_12(+1401) · cuenta 1401 | 3772 comparados, 0 fallas; excluidas por el propio JSON: 72 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 19.7685, 22.0571)), 3 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___UNIDAD_Y_PROGRESO', '2024-07', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Productivo | `tasa_1409` | 100·VARIACION_12(+1409) / RETARDO_12(+1409) · cuenta 1409 | 1567 comparados, 0 fallas; excluidas por el propio JSON: 17 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 357.9185, 374.7374)), 1 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2024-12', 0.0, -6.5844)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Productivo | `tasa_1417` | 100·VARIACION_12(+1417) / RETARDO_12(+1417) · cuenta 1417 | 1263 comparados, 0 fallas; excluidas por el propio JSON: 22 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -33.4972, -32.4511)) | ✅ CAR01: 434 comparados, 0 fallas | ✅ |
| EFI07 | Productivo | `tasa_1425` | 100·VARIACION_12(+1425) / RETARDO_12(+1425) · cuenta 1425 | 2322 comparados, 0 fallas; excluidas por el propio JSON: 20 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -62.7462, -62.6425)), 2 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___15_DE_ABRIL_LTDA', '2025-06', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Productivo | `tasa_1433` | 100·VARIACION_12(+1433) / RETARDO_12(+1433) · cuenta 1433 | 1131 comparados, 0 fallas; excluidas por el propio JSON: 11 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 48.969, 34.8958)) | ✅ CAR01: 459 comparados, 0 fallas | ✅ |
| EFI07 | Productivo | `tasa_1441` | 100·VARIACION_12(+1441) / RETARDO_12(+1441) · cuenta 1441 | 921 comparados, 0 fallas; excluidas por el propio JSON: 14 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 12.803, 308.6957)) | ✅ CAR01: 381 comparados, 0 fallas | ✅ |
| EFI07 | Productivo | `tasa_1449` | 100·VARIACION_12(+1449) / RETARDO_12(+1449) · cuenta 1449 | 3185 comparados, 0 fallas; excluidas por el propio JSON: 25 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -74.1233, -62.7057)), 1 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___4_RIOS', '2025-02', 0.0, -100.0)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Productivo | `tasa_1457` | 100·VARIACION_12(+1457) / RETARDO_12(+1457) · cuenta 1457 | 1240 comparados, 0 fallas; excluidas por el propio JSON: 12 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 317.7368, 721.4286)) | ✅ CAR01: 465 comparados, 0 fallas | ✅ |
| EFI07 | Productivo | `tasa_1465` | 100·VARIACION_12(+1465) / RETARDO_12(+1465) · cuenta 1465 | 1399 comparados, 0 fallas; excluidas por el propio JSON: 12 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 43.9357, 1350.0)) | ✅ CAR01: 417 comparados, 0 fallas | ✅ |
| EFI07 | Productivo | `tasa_149105` | — | 100·VARIACION_12(+149105) / RETARDO_12(+149105): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Productivo | `tasa_149205` | — | 100·VARIACION_12(+149205) / RETARDO_12(+149205): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Productivo | `tasa_149305` | — | 100·VARIACION_12(+149305) / RETARDO_12(+149305): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Productivo | `tasa_149405` | — | 100·VARIACION_12(+149405) / RETARDO_12(+149405): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Productivo | `tasa_149505` | — | 100·VARIACION_12(+149505) / RETARDO_12(+149505): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Productivo | `tasa_149605` | — | 100·VARIACION_12(+149605) / RETARDO_12(+149605): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Productivo | `tasa_149905` | 100·VARIACION_12(+149905) / RETARDO_12(+149905) · cuenta 149905 | 4105 comparados, 0 fallas; excluidas por el propio JSON: 33 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 4.6835, 17.1025)), 1 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___4_RIOS', '2025-04', 0.0, 347.0588)) | ✅ CAR01: 520 comparados, 0 fallas | ✅ |
| EFI07 | Productivo | `tasa_IF0071` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Productivo | `tasa_IF0081` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Productivo | `tasa_IF0091` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Productivo | `tasa_IF0101` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Productivo | `tasa_IF0111` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `tasa_1408` | 100·VARIACION_12(+1408) / RETARDO_12(+1408) · cuenta 1408 | 442 comparados, 0 fallas | ✅ CAR01: 412 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_1416` | 100·VARIACION_12(+1416) / RETARDO_12(+1416) · cuenta 1416 | 87 comparados, 0 fallas | ✅ CAR01: 207 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_1424` | 100·VARIACION_12(+1424) / RETARDO_12(+1424) · cuenta 1424 | 52 comparados, 0 fallas | ✅ CAR01: 156 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_1432` | 100·VARIACION_12(+1432) / RETARDO_12(+1432) · cuenta 1432 | 222 comparados, 0 fallas | ✅ CAR01: 350 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_1440` | 100·VARIACION_12(+1440) / RETARDO_12(+1440) · cuenta 1440 | 12 comparados, 0 fallas | ✅ CAR01: 36 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_1448` | 100·VARIACION_12(+1448) / RETARDO_12(+1448) · cuenta 1448 | 49 comparados, 0 fallas | ✅ CAR01: 147 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_1456` | 100·VARIACION_12(+1456) / RETARDO_12(+1456) · cuenta 1456 | 268 comparados, 0 fallas | ✅ CAR01: 351 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_1464` | 100·VARIACION_12(+1464) / RETARDO_12(+1464) · cuenta 1464 | 10 comparados, 0 fallas | ✅ CAR01: 30 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_1472` | 100·VARIACION_12(+1472) / RETARDO_12(+1472) · cuenta 1472 | 52 comparados, 0 fallas | ✅ CAR01: 156 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_149135` | — | 100·VARIACION_12(+149135) / RETARDO_12(+149135): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `tasa_149235` | — | 100·VARIACION_12(+149235) / RETARDO_12(+149235): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `tasa_149335` | — | 100·VARIACION_12(+149335) / RETARDO_12(+149335): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `tasa_149435` | — | 100·VARIACION_12(+149435) / RETARDO_12(+149435): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `tasa_149535` | — | 100·VARIACION_12(+149535) / RETARDO_12(+149535): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `tasa_149635` | — | 100·VARIACION_12(+149635) / RETARDO_12(+149635): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `tasa_149940` | 100·VARIACION_12(+149940) / RETARDO_12(+149940) · cuenta 149940 | 450 comparados, 0 fallas | ✅ CAR01: 413 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_IF0075` | 100·VARIACION_12(+1408 +1416 +1424) / RETARDO_12(+1408 +1416 +1424) · SUMA IF007_5 | 459 comparados, 0 fallas | ✅ CAR01: 413 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_IF0085` | 100·VARIACION_12(+1432 +1440 +1448) / RETARDO_12(+1432 +1440 +1448) · SUMA IF008_5 | 232 comparados, 0 fallas | ✅ CAR01: 352 comparados, 0 fallas | ✅ |
| EFI07 | Vivienda interés Público y Social | `tasa_IF0095` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `tasa_IF0105` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI07 | Vivienda interés Público y Social | `tasa_IF0115` | 100·VARIACION_12(+1408 +1416 +1424 +1432 +1440 +1448 +1456 +1464 +1472) / RETARDO_12(+1408 +1416 +1424 +1432 +1440 +1448 +1456 +1464 +1472) · SUMA IF011_5 | 459 comparados, 0 fallas | ✅ CAR01: 413 comparados, 0 fallas | ✅ |
| EFI08 | Cartera Total | `tasa_monto_pro` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Cartera Total | `tasa_monto_total` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Cartera Total | `tasa_ope_total` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Consumo | `tasa_moa_const` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Consumo | `tasa_num_const` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Consumo | `tasa_pro_const` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Educativo | `tasa_edu` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Educativo | `tasa_eduso` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Educativo | `tasa_moa_educ` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Educativo | `tasa_num_edu` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Educativo | `tasa_num_educ` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Educativo | `tasa_num_eduso` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Educativo | `tasa_pro_edu` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Educativo | `tasa_pro_educ` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Educativo | `tasa_pro_eduso` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Inmobiliario | `tasa_moa_in` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Inmobiliario | `tasa_num_in` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Inmobiliario | `tasa_pro_in` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_maa` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_mas` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_mino` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_moa_mic` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_num_maa` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_num_mas` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_num_mic` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_num_mino` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_pro_maa` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_pro_mas` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_pro_mic` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Microcrédito | `tasa_pro_mino` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_moa_cor` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_num_cor` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_num_proc` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_num_prodem` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_num_prodpy` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_pro_cor` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_pro_proc` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_pro_prodem` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_pro_prodpy` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_proc` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_prodem` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Productivo | `tasa_prodpy` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Vivienda interés Público y Social | `tasa_moa_vi` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Vivienda interés Público y Social | `tasa_num_vi` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Vivienda interés Público y Social | `tasa_num_vip` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Vivienda interés Público y Social | `tasa_num_vis` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Vivienda interés Público y Social | `tasa_pro_vi` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Vivienda interés Público y Social | `tasa_pro_vip` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Vivienda interés Público y Social | `tasa_pro_vis` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Vivienda interés Público y Social | `tasa_vip` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI08 | Vivienda interés Público y Social | `tasa_vis` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_21` | 100·VARIACION_12(+21) / RETARDO_12(+21) · cuenta 21 | 10846 comparados, 0 fallas; excluidas por el propio JSON: 436 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -4.6945, -4.8689)), 13 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___CREDI_FACIL_LTDA', '2024-02', 0.0, 47.5253)) | ❌ CAR04: sin datos | ✅ |
| EFI10 | — | `tasa_210105` | — | 100·VARIACION_12(+210105) / RETARDO_12(+210105): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI10 | — | `tasa_210110` | 100·VARIACION_12(+210110) / RETARDO_12(+210110) · cuenta 210110 | 1124 comparados, 0 fallas; excluidas por el propio JSON: 11 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -29.4532, -21.0795)) | ❌ CAR04: sin datos | ✅ |
| EFI10 | — | `tasa_210115` | — | 100·VARIACION_12(+210115) / RETARDO_12(+210115): sin datos para validar (la cuenta no aparece en los balances del JSON). | — | ❌ queda JSON |
| EFI10 | — | `tasa_210135` | 100·VARIACION_12(+210135) / RETARDO_12(+210135) · cuenta 210135 | 10727 comparados, 0 fallas; excluidas por el propio JSON: 429 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', -9.5284, -11.5774)), 19 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___SAN_CRISTOBAL_LTDA', '2024-09', 0.0, -4.0548)) | ❌ CAR04: sin datos | ✅ |
| EFI10 | — | `tasa_OPTPE` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_OPTPE121` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_OPTPE181` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_OPTPE30` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_OPTPE361` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_OPTPE61` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_OPTPE91` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_Plazo121` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_Plazo181` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_Plazo30` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_Plazo361` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_Plazo61` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_Plazo91` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_SB011` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_SB2101` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_SB2103` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_SB210305` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_SB210310` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_SB210315` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_SB210320` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_SB210325` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_SB210330` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_mop` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_pro_MOP` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_pro_op121` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_pro_op181` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_pro_op30` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_pro_op361` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_pro_op61` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| EFI10 | — | `tasa_pro_op91` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| REP01 | — | `tasa_14` | 100·VARIACION_12(+14) / RETARDO_12(+14) · cuenta 14 | 10619 comparados, 0 fallas; excluidas por el propio JSON: 432 en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce (p. ej. ('BP__DELBANK', '2024-06', 15.937, 15.0491)), 13 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___CREDI_FACIL_LTDA', '2024-02', 0.0, 42.2623)) | — | ✅ |
| REP01 | — | `tasa_IF0071` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| REP01 | — | `tasa_IF0072` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| REP01 | — | `tasa_IF0073` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |
| REP01 | — | `tasa_IF0074` | — | sin base con fórmula en el catálogo | — | ❌ queda JSON |

## 5. ROA / ROE

12 de 12 aceptadas.

| Cuadro | Sub-vista | Código / texto | Fórmula | Entidades | Sector | Resultado |
|---|---|---|---|---|---|---|
| EFI11 | — | `IF002` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / PROMEDIO_DIC_ROA(+1) · Gan_Eje / activo | 11856 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 0.9169)), 70 en que el ROA publicado contradice el ROE publicado, que la fórmula sí reproduce (p. ej. ('COAC___DE_LA_PEQUENA_EMPRESA_BIBLIAN_LIMITADA', '2023-05', 0.418, 3.4051)) | ✅ CAR05: 570 comparados, 0 fallas; excluidas por el propio JSON: 11 en que el ROA publicado contradice el ROE publicado, que la fórmula sí reproduce (p. ej. ('S:Sistema Financiero Nacional (privado y eps)', '2023-05', 1.1428, 1.1974)) | ✅ |
| EFI11 | — | `IF004` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / BASE_ROE_DIC(+3) · Gan_Eje / patrimonio | 11856 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 7.6672)) | ✅ CAR05: 570 comparados, 0 fallas | ✅ |
| EFI12 | — | `E1_ROA` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / PROMEDIO_DIC_ROA(+1) · Gan_Eje / activo | 11856 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 0.9169)), 70 en que el ROA publicado contradice el ROE publicado, que la fórmula sí reproduce (p. ej. ('COAC___DE_LA_PEQUENA_EMPRESA_BIBLIAN_LIMITADA', '2023-05', 0.418, 3.4051)) | — | ✅ |
| EFI12 | — | `E4_ROE` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / BASE_ROE_DIC(+3) · Gan_Eje / patrimonio | 11856 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 7.6672)) | — | ✅ |
| EFI13 | — | `R12_ROA` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / PROMEDIO_DIC_ROA(+1) · Gan_Eje / activo | 11856 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 0.9169)), 70 en que el ROA publicado contradice el ROE publicado, que la fórmula sí reproduce (p. ej. ('COAC___DE_LA_PEQUENA_EMPRESA_BIBLIAN_LIMITADA', '2023-05', 0.418, 3.4051)) | — | ✅ |
| EFI13 | — | `R13_ROE` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / BASE_ROE_DIC(+3) · Gan_Eje / patrimonio | 11856 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 7.6672)) | — | ✅ |
| REP01 | — | `E1_ROA` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / PROMEDIO_DIC_ROA(+1) · Gan_Eje / activo | 11634 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 0.9169)), 70 en que el ROA publicado contradice el ROE publicado, que la fórmula sí reproduce (p. ej. ('COAC___DE_LA_PEQUENA_EMPRESA_BIBLIAN_LIMITADA', '2023-05', 0.418, 3.4051)) | — | ✅ |
| REP01 | — | `E4_ROE` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / BASE_ROE_DIC(+3) · Gan_Eje / patrimonio | 11634 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 7.6672)) | — | ✅ |
| REP01 | — | `IF002` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / PROMEDIO_DIC_ROA(+1) · Gan_Eje / activo | 11634 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 0.9169)), 70 en que el ROA publicado contradice el ROE publicado, que la fórmula sí reproduce (p. ej. ('COAC___DE_LA_PEQUENA_EMPRESA_BIBLIAN_LIMITADA', '2023-05', 0.418, 3.4051)) | — | ✅ |
| REP01 | — | `IF004` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / BASE_ROE_DIC(+3) · Gan_Eje / patrimonio | 11634 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 7.6672)) | — | ✅ |
| REP01 | — | `R12_ROA` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / PROMEDIO_DIC_ROA(+1) · Gan_Eje / activo | 11634 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 0.9169)), 70 en que el ROA publicado contradice el ROE publicado, que la fórmula sí reproduce (p. ej. ('COAC___DE_LA_PEQUENA_EMPRESA_BIBLIAN_LIMITADA', '2023-05', 0.418, 3.4051)) | — | ✅ |
| REP01 | — | `R13_ROE` | 100·ANUALIZADO(−41 −42 −43 −44 −45 −46 −47 −48 +51 +52 +53 +54 +55 +56) / BASE_ROE_DIC(+3) · Gan_Eje / patrimonio | 11634 comparados, 0 fallas; excluidas por el propio JSON: 47 con 0 publicado (valor faltante en el JSON) (p. ej. ('COAC___LAS_NAVES_LTDA', '2021-12', 0.0, 7.6672)) | — | ✅ |

## Diferencias excluidas por el propio JSON (filas aceptadas)

Una diferencia solo se excluye si el propio JSON la explica; se cuentan aquí para que quede a la vista.

- 6,784 valores en que la tasa publicada contradice su propia fila base del JSON (historia reprocesada), que la fórmula sí reproduce.
- 845 valores con 0 publicado (valor faltante en el JSON).
- 431 valores en que el ROA publicado contradice el ROE publicado, que la fórmula sí reproduce.

## Fuera de este paso

- EFI04/SFN04 grupos FUENTES, USOS y RESULTADOS: dependen del signo de la variación (no lineales); quedan JSON.
- tasa_X cuya X no es una cuenta ni tiene SUMA en el catálogo: se resuelven cuando X tenga fórmula (paso 3).

**Aceptadas:** 476 fórmulas (entidad + sector) · análisis horizontal: sí.

