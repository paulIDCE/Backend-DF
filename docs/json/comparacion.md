# Comparación de la BD JSON

- Anterior: 2026-10-13T14:34:32 · `C:\Users\IDCE_\Documents\Proyectos\BackendDF\BackendDF\src\BackendDF\Database`
- Nueva: 2026-10-13T14:39:21 · `C:\Users\IDCE_\Documents\Proyectos\BackendDF\BackendDF\src\BackendDF\Database`

## Archivos base

| Archivo | Estado | Filas | Períodos | Cuadros |
|---|---|---|---|---|
| base_anual.json | CAMBIA | 2292→2292 | 2000-12→2025-12 ⇒ 2000-12→2025-12 | solo valores |
| base_balances.json | CAMBIA | 39207→39207 | 2021-05→2026-07 ⇒ 2021-05→2026-08 | solo valores |
| base_cartera.json | CAMBIA | 7051→72175 | 2021-05→2027-01 ⇒ 2019-03→2027-02 | +['GEO01', 'GEO02', 'GEO03', 'GEO04', 'PDA01', 'PDA02'] −[] |
| base_estru_sistema.json | CAMBIA | 6650→71774 | 2021-05→2027-02 ⇒ 2019-03→2027-02 | +['GEO01', 'GEO02', 'GEO03', 'GEO04', 'PDA01', 'PDA02'] −[] |
| base_mensual.json | CAMBIA | 2041→2041 | 2000-01→2026-08 ⇒ 2000-01→2026-08 | solo valores |
| base_notas.json | igual | 234 | | |
| base_trimestral.json | igual | 1094 | | |
| entidades.json | NUEVO | | | |
| entidades_lista.json | igual | 229 | | |

## Carpetas por entidad

| Carpeta | Archivos | Nuevos | Eliminados | Cambiados | Períodos | Plantillas |
|---|---|---|---|---|---|---|
| balances/ | 330→229 | 0 | 101 | 228 | 2021-05→2026-07 ⇒ 2021-05→2026-08 | iguales |
| entidades/ | 229→229 | 0 | 0 | 229 | 2021-05→2027-01 ⇒ 2019-03→2027-02 | 1029 nuevas / 0 desaparecen |
| reportes/ | 231→231 | 0 | 0 | 231 | 2021-05→2026-07 ⇒ 2021-05→2026-07 | iguales |

## Catálogo de entidades

229 → 229 entidades; 0 nuevas, 0 eliminadas o renombradas.

## Impacto en el backend

- `entidades.json` nuevo.
- `balances/`: 0 archivos nuevos, 101 eliminados → revisar `mapa-entidades.json`. Eliminados: COAC___15_DE_AGOSTO_LTDA, COAC___18_DE_NOVIEMBRE, COAC___1_DE_JULIO, COAC___26_DE_JULIO_LTDA, COAC___27_DE_NOVIEMBRE, COAC___ACHIK_INTI_LTDA, COAC___ACHIK_NAN, COAC___ALLI_TARPUK_LTDA….
- `entidades/`: cambian plantillas de ['GEE01', 'GEE03', 'GEE04', 'PDE01', 'PDE02'] → regenerar la semilla del catálogo.
