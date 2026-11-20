# Resultados y guía de evidencias — Ciclo 3

Fecha: 20 de noviembre de 2026. Este documento registra resultados reportados, no un acta de aprobación.

## Pruebas reportadas por el desarrollador

| Ejecución | Total | Aprobadas | Errores | Omitidas | Archivo reportado |
|---|---:|---:|---:|---:|---|
| Clientes bloque 01 | 110 | 110 | 0 | 0 | pruebas-ciclo3-clientes-bloque01.trx |
| Clientes bloque 02 | 167 | 167 | 0 | 0 | pruebas-ciclo3-clientes-bloque02.trx |
| Clientes bloque 03 | 203 | 203 | 0 | 0 | pruebas-ciclo3-clientes-bloque03.trx |
| Regresión de acceso tras bloque 03 | 4 | 4 | 0 | 0 | pruebas-ciclo3-regresion-acceso.trx |
| Clientes bloque 04 | 252 | 252 | 0 | 0 | pruebas-ciclo3-clientes-bloque04.trx |
| Clientes bloque 05 | 284 | 284 | 0 | 0 | pruebas-ciclo3-clientes-bloque05.trx |
| Solución sin activar SQL | 634 | 587 | 0 | 47 | pruebas-ciclo3-solucion-completa.trx |
| Solución con SQL local tras bloque 05 | 634 | 634 | 0 | 0 | pruebas-ciclo3-solucion-sql-local.trx |
| Clientes bloque 07 API | 321 | 321 | 0 | 0 | pruebas-ciclo3-clientes-bloque07-api.trx |
| Clientes bloque 08 compra futura | 329 | 329 | 0 | 0 | pruebas-ciclo3-clientes-bloque08-compra-futura.trx |
| Clientes bloque 09 demostración | 351 | 351 | 0 | 0 | pruebas-ciclo3-clientes-bloque09-demostracion.trx |
| Solución antes del bloque 11 | 701 | 701 | 0 | 0 | pruebas-ciclo3-solucion-final-sql-local.trx |
| Clientes bloque 11 seguridad | 383 | 383 | 0 | 0 | pruebas-ciclo3-clientes-bloque11-seguridad.trx |
| Solución bloque 11 con SQL local existente | 733 | 733 | 0 | 0 | pruebas-ciclo3-solucion-bloque11-sql-local.trx |

Los totales por bloque son acumulativos: no deben sumarse. La última ejecución tuvo 22.1 s de pruebas y compilación correcta en 24.5 s, según la salida recibida. Las 47 omitidas de la ejecución previa correspondían a pruebas SQL de infraestructura, administración, MFA, recuperación, catálogo y tiendas. En la ejecución final se activaron y aprobaron.

**Límite:** el SQL aprobado corresponde a los componentes ya integrados. Los tests de clientes del ciclo 3 utilizan dobles y el proveedor de demostración en memoria para registro, perfil, direcciones y consentimientos. No hay todavía evidencia de alta/CRUD/auditoría SQL de clientes.

## Evidencias manuales observadas

Las imágenes se compartieron en la conversación; este ZIP contiene su inventario, sin duplicarlas. Conservar los originales junto a los TRX en la entrega.

| Evidencia | Resultado visible | Archivo de captura |
|---|---|---|
| Perfil guardado | Mensaje Perfil actualizado y campos de cliente | Capturas de recorrido del 19/11 |
| Promociones | Aceptado y No autorizado para demo-promociones-1 con fechas; retiro registrado | image(20261120-165545).png |
| Guardado de dirección | casa 2, referencia esquina y mensaje Dirección guardada | image(20261120-170031).png |
| Confirmación de eliminación | Identifica casa 2 y ofrece confirmar/cancelar | image(20261120-170208).png |
| Eliminación | Dirección eliminada; queda departamento | image(20261120-170212).png |
| Principal | departamento muestra Dirección principal | image(20261120-170404).png |
| Acceso ajeno | /Cliente/Editar/2 muestra La dirección no está disponible sin datos de la dirección, tras el recorrido indicado con otra cuenta | image(20261120-170646).png |
| Público móvil | Bloques y botones visibles ajustados al ancho 390 × 858 | image(20261120-170928).png; image(20261120-171031).png; image(20261120-171047).png |
| Cuenta móvil | Cuenta de demostración sin roles ni asignaciones | image(20261120-171845).png |
| Direcciones móvil | Estado vacío en la cuenta nueva; no se interpreta como pérdida de otra cuenta | image(20261120-171906).png |
| Consentimientos móvil | Historial ajustado al ancho, fechas en varias líneas | image(20261120-171925).png |
| Perfil de cliente móvil | Cuatro campos, etiquetas y botón dentro del ancho visible | image(20261120-172308).png |

La evidencia de acceso ajeno depende del cambio de cuenta realizado por el desarrollador; la captura sola no identifica la sesión. Las pruebas automatizadas complementan esa comprobación. La primera captura de guardado de dirección no es por sí sola evidencia del valor anterior a una edición; el caso de edición cuenta además con pruebas automatizadas.

También se compartieron previamente portada SQL, búsqueda Mango/Negocio, orden por precio, enlace al catálogo filtrado y regreso desde compra futura con los filtros conservados.

## Evidencias aún pendientes

- Alta, rollback, propiedad, principal concurrente e historial/auditoría en SQL de clientes autorizado.
- TRX XML final, revisión de Git, rama requerida y PR.
- Manual, documento técnico consolidado y video específicos del ciclo 3.

## Conservar los resultados desde Git Bash

Ejecutar desde la raíz de la solución; no requiere detener la demostración:

```bash
mkdir -p docs/evidencias/ciclo3
cp PortalMercado.Tests/TestResults/pruebas-ciclo3-clientes-bloque11-seguridad.trx docs/evidencias/ciclo3/
cp PortalMercado.Tests/TestResults/pruebas-ciclo3-solucion-bloque11-sql-local.trx docs/evidencias/ciclo3/
```

No se ejecutaron estos comandos en la PC del desarrollador desde aquí. Conservar datos ficticios y revisar que las evidencias no expongan contraseñas, cookies, tokens ni secretos. Un TRX solo acredita el código de su ejecución; generar otro si se cambia código posteriormente.

## Bloque 11 comprobado por el desarrollador

- Clientes: 383 aprobadas, 0 errores, 0 omitidas; 18.1 s de pruebas y compilación correcta en 30.9 s.
- Solución: 733 aprobadas, 0 errores, 0 omitidas; 22.1 s de pruebas y compilación correcta en 24.5 s.
- Cambio de contraseña e ingreso posterior: confirmado y capturas image(20261120-192842).png e image(20261120-192856).png.
- Recuperación mediante EML local y cambio de correo: confirmaciones manuales del desarrollador, sin capturas adicionales de los enlaces.
- Registro, dirección, correo y portal a 390 por 858: capturas image(20261120-193703).png, image(20261120-193732).png, image(20261120-193759).png, image(20261120-193804).png, image(20261120-193900).png e image(20261120-193909).png.
- Teclado y Siguiente/Anterior conservando filtros: funcionamiento confirmado por el desarrollador; la revisión básica no es una auditoría integral de accesibilidad.

Conservar los dos TRX del bloque 11. Las capturas no incluyen el contenido de EML ni tokens. El ZIP documental no incluye un guion ni incorpora cambios de código.
