# Ciclo 3 — Bloque 05: consentimientos y entrada para futuras compras

## Preparado en este bloque

- `/Consentimientos`: página privada con historial propio, tipo, versión, decisión y fecha UTC.
- Autorización de promociones mediante una acción explícita, sin casillas preseleccionadas.
- Retiro de la autorización previa, incluso si su documento ya no está vigente o los documentos actuales no están disponibles.
- Cada decisión se envía como un nuevo evento al repositorio; no se elimina ni sobrescribe el historial.
- Términos y privacidad se registran en el alta y se muestran en el historial. Los botones de promociones no alteran esos documentos.
- IdUsuario, fecha e IP proceden de la sesión y del host. El navegador no elige propietario, tipo ni estado de la decisión; los dos botones POST determinan autorizar o retirar.
- Protección CSRF, sesión existente, páginas sin caché y mensajes sin detalles de infraestructura.
- Enlace **Mis consentimientos** desde el perfil de cliente.
- `/Compras/Preparar?returnUrl=...`: entrada autenticada informativa que conserva el destino mediante el login existente y ofrece un regreso local seguro.

## Límites de la entrada de compras

Esta pantalla informa que las compras llegarán en otro ciclo. No crea carrito, pedido ni pago; no descuenta o reserva inventario ni guarda una intención de compra. Los botones Agregar al carrito existentes permanecen deshabilitados.

Se añadió un enlace descriptivo desde el perfil para revisar esta entrada. No se presenta un botón de compra operativa en el catálogo.

En el ciclo 4 se deberá validar el perfil de cliente real, producto/tienda, disponibilidad, cantidades, precios, consentimientos vigentes y permisos antes de cualquier mutación. Una sesión válida por sí sola no autoriza una compra. El destino de regreso se comprueba con `DestinoCliente.Normalizar`; no se aceptan sitios externos ni esquemas alternativos.

## Persistencia y documentos pendientes

**No se habilita SQL de clientes ni se incluyen documentos legales inventados.** El repositorio `ConsentimientosClientePendiente` responde 503, igual que los contratos pendientes de alta, perfil y direcciones de los bloques anteriores.

El historial y el cambio de preferencia funcionan en las pruebas con dobles exclusivos de Tests. La aplicación real mostrará el aviso de cuenta no disponible hasta contar con los adaptadores aprobados.

Adaptador requerido: `IRepositorioConsentimientosCliente`, registrado por DI en lugar de `ConsentimientosClientePendiente`. Debe resolver el cliente por IdUsuario autenticado y revalidar la propiedad al escribir. Debe insertar un evento y la auditoría en una sola transacción, sin reemplazar el pasado. Fecha e IP vienen del servidor; no guardar contraseñas, tokens ni secretos MFA.

Para autorizar se exige la versión actual del documento promocional configurado, además de textos documentados disponibles en la web. Para retirar se admite una versión que ya pertenezca al historial del usuario: retirar no depende de que los textos actuales estén configurados. No se infiere una autorización a partir de ausencia de datos.

El historial se ordena por fecha descendente. Como el DTO actual no tiene IdEvento, si el proveedor devuelve decisiones diferentes del mismo tipo con idéntica fecha, se responde 503 en vez de inventar cuál fue la última. El contrato DBA deberá acordar el desempate estable o ampliar el DTO antes de integrar un proveedor que admita esos empates. No se modifica el esquema para resolverlo aquí.

Las transacciones, auditoría persistente, concurrencia y rollback reales siguen pendientes del adaptador. Estos tests no acreditan esas propiedades de SQL.

## Instalar

1. Detener Web y guardar el trabajo.
2. Copiar la carpeta `PortalMercado` del ZIP sobre la raíz de la solución, conservando su estructura y reemplazando los archivos incluidos.
3. Requiere bloques 01–04. No sobrescribe `RegistroClienteServicioTests.cs`.
4. No ejecutar migraciones ni scripts SQL.

## Pruebas en Git Bash

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Clientes" --logger "trx;LogFileName=pruebas-ciclo3-clientes-bloque05.trx"
```

Se agregan **32 casos** (19 de servicio y 13 HTTP) a los 252 aprobados del bloque anterior: se esperan **284** si no hubo otros cambios.

Se comprueban propiedad, historial, evidencias del servidor, versiones, autorización explícita, retiro de versión anterior, rechazo de estados ambiguos, CSRF, manejo del proveedor pendiente y regreso local tras login para la futura entrada de compras.

Este entorno no tiene SDK .NET. Se revisaron sintaxis léxica y delimitadores de los archivos C# y la integridad del ZIP. La compilación, Razor y ejecución deben confirmarse en tu equipo. No se incluye un TRX ficticio.

## Comprobación manual disponible ahora

Mantener la conexión de tiendas que ya funciona:

```bash
Tiendas__SqlLocal=true Tiendas__Demostracion=false Tiendas__NegocioLocalDemostracion= dotnet run --project PortalMercado.Web --launch-profile https
```

En incógnito abrir:

```text
https://localhost:7275/Compras/Preparar?returnUrl=%2Fexplorar%2Fbuscar%3Fq%3DMango
```

Debe pedir iniciar sesión, regresar a la pantalla informativa y permitir **Volver a la consulta** de Mango. No debe añadir productos ni modificar inventario.

`/Consentimientos` también requiere sesión. Mientras falte el contrato, se espera el aviso 503 sin un historial inventado.

La confirmación manual de autorizar y retirar en la aplicación real, y la captura de estas pantallas con datos de prueba, se realizará después de integrar los contratos. Las pruebas HTTP incluidas comprueban la capa web con dobles.

## Próximo bloque

Revisión de RF/CA contra el PDF original, matriz de cobertura y pendientes, regresión completa, guía de evidencias y preparación de la entrega. El ciclo 3 todavía no está completo: faltan persistencia real, documentos aprobados, confirmación de correo aprobada y pruebas SQL correspondientes.
