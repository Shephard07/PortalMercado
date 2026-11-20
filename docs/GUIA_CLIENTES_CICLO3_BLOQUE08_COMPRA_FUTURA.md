# Ciclo 3 — Bloque 08: botón público para compra futura

## Cambio

Se añade un bloque público con el botón **Comprar (próximamente)** en:

- Portada anónima `/` y portada `/explorar`.
- Búsqueda global `/explorar/buscar`.
- Catálogo público dentro de la ficha de cada tienda.

Se presenta una sola vez por página, separado de las tarjetas de productos y acompañado del aviso de que las compras todavía no están disponibles. El enlace lleva a la entrada informativa protegida `/Compras/Preparar` del bloque 05. No selecciona un producto para comprar ni indica que haya una compra iniciada.

Sin sesión: botón → login existente → MFA si corresponde → aviso informativo → Volver a la consulta. Con sesión válida: botón → aviso → consulta. Se conserva la ruta, los filtros, el orden, el tamaño y la página que estaban en la URL. En una tienda se añade `#catalogo` para regresar a sus productos.

Las tarjetas mantienen **Agregar al carrito** deshabilitado. No se crean carrito, pedido o pago, no se reserva inventario y no se modifica la persistencia. La validación del destino local continúa en `ComprasController` y en el login existente, sin confiar en un destino externo.

El bloque de compra tiene etiquetas visibles, foco y una altura mínima de enlace de 44 px. En 390 px el botón ocupa el ancho disponible; falta confirmar visualmente esta nueva sección en tu navegador.

## Aplicar

1. Detener Web y guardar los cambios.
2. Copiar la carpeta PortalMercado del ZIP sobre la raíz de la solución, conservando su estructura y reemplazando los archivos incluidos.
3. Requiere los bloques previos; no cambia controladores, autenticación, conexión ni SQL.
4. Ejecutar en Git Bash:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Clientes" --logger "trx;LogFileName=pruebas-ciclo3-clientes-bloque08-compra-futura.trx"
```

Se añaden **8 casos HTTP** a los 321 reportados del bloque 07: se esperan **329 pruebas de Clientes** si no hubo otros cambios. Cubren presencia pública del botón, exigencia de sesión, recorrido del clic a login/MFA, conservación de filtros/página/sección y acceso ya autenticado manteniendo carrito deshabilitado.

Se revisaron sintaxis léxica/delimitadores del nuevo C#, estructura del paquete y hashes. No hay SDK .NET en este entorno; compilación/Razor y ejecución de los casos deben confirmarse en Windows. No se incluye un TRX inventado.

## Comprobación manual

Levantar Web como ya se venía usando con tiendas locales:

```bash
Tiendas__SqlLocal=true Tiendas__Demostracion=false Tiendas__NegocioLocalDemostracion= dotnet run --project PortalMercado.Web --launch-profile https
```

1. Abrir en incógnito `https://localhost:7275/explorar` y buscar Mango.
2. Seleccionar Productos y ordenar por precio. Pulsar **Comprar (próximamente)**.
3. Debe pedir iniciar sesión; utilizar una cuenta existente y completar MFA si corresponde.
4. Debe regresar al aviso **Las compras estarán disponibles próximamente**.
5. Pulsar **Volver a la consulta** y comprobar búsqueda/filtros.
6. Repetir en el catálogo de una tienda y comprobar regreso a `#catalogo`.
7. Revisar en 390 px sin desbordamiento visible y mediante Tab/Enter.

Guardar capturas del botón, aviso y consulta recuperada con datos ficticios. No mostrar contraseña, secreto MFA ni códigos de recuperación.

Esta comprobación no depende de los adaptadores pendientes de clientes: el aviso solo exige la sesión existente y no ejecuta una compra. El ciclo 4 deberá validar cliente real, producto, precio, cantidad, dirección y stock al implementar operaciones.

## Actualización del cumplimiento

Este bloque prepara el punto de extensión público solicitado por RF-B3-12 y CA-11. Una vez confirmados los casos y el recorrido manual, se podrá registrar su evidencia. No habilita ni acredita los pendientes de SQL, documentos legales, confirmación de correo, auditoría persistente o concurrencia de direcciones.

Con los bloques 07 y 08, la regresión completa pasa a **679 casos esperados** (634 anteriores +37 API +8 botón). El TRX anterior de 634 acredita el estado previo, no estos cambios posteriores.
