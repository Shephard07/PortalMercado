# Ciclo 3 — Bloque 09: demostración funcional de clientes

## Por qué se añade

En la consulta de metadatos de PortalMercado_ET01_Test, el desarrollador tiene VIEW DEFINITION y solo apareció pm_RegistrarPedido como procedimiento candidato. Ese procedimiento de pedidos queda fuera del ciclo 3; no proporciona los contratos de clientes solicitados.

La sección 2 del requisito permite simulaciones tras interfaces si faltan contratos. Este paquete habilita una demostración explícita para recorrer registro, acceso, perfil, direcciones y consentimientos sin modificar SQL. **No sustituye la integración aprobada, ni acredita persistencia SQL o conformidad final.**

## Activación

1. Detener Web/API y copiar la carpeta PortalMercado sobre la solución. Requiere bloques 01–08.
2. Ejecutar las pruebas antes de levantar la aplicación.
3. En Git Bash, desde la raíz, levantar Web:

```bash
Clientes__Demostracion=true Tiendas__SqlLocal=true Tiendas__Demostracion=false Tiendas__NegocioLocalDemostracion= dotnet run --project PortalMercado.Web --launch-profile https
```

Esto mantiene tiendas y catálogo en la copia SQL local existente, pero clientes en memoria. El perfil HTTPS debe usar Development, como el actual. La demostración se rechaza al iniciar si el ambiente no es Development. No editar appsettings de producción ni agregar claves para simular aprobaciones.

Abrir en una ventana de incógnito:

```text
https://localhost:7275/Registro
```

Se verá **Clientes en demostración · datos temporales** en registro, login, perfil y demás páginas. Utilizar únicamente nombres/contactos/documentos/direcciones ficticios y correos `@example.invalid`. No se precargan usuarios ni contraseñas: crear la cuenta desde el formulario.

## Recorrido de ejemplo

1. Crear `cliente1@example.invalid` con nombre ficticio. Elegir una contraseña de prueba que cumpla la política existente; no reutilizar contraseñas personales.
2. Leer los textos marcados como ficticios y aceptar términos/privacidad de demostración. Dejar promociones sin marcar.
3. Tras el aviso de registro, iniciar sesión con esa cuenta. No hay autologin.
4. Pulsar **Mi cuenta** y editar apellidos, celular y documento de prueba.
5. Abrir **Mis direcciones**, agregar Casa y Trabajo ficticios; editar, elegir principal y eliminar.
6. Abrir **Mis consentimientos**, autorizar promociones y retirar la autorización. Las decisiones anteriores siguen visibles.
7. Para cerrar sesión: desde el perfil de cliente abrir **Seguridad y datos de acceso** y pulsar **Cerrar sesión**.
8. Crear `cliente2@example.invalid`, entrar y comprobar que no ve las direcciones del primero. Probar una URL de edición del primer cliente: debe responder 404 sin mostrar su dirección.
9. Revisar en escritorio y a 390 px. Guardar capturas solo con datos de prueba y sin contraseña, secretos o cookies.

Las cuentas y los datos duran hasta detener ese proceso. Reiniciar crea un almacén vacío; una cookie anterior ya no autentica a nadie. El límite de la demostración es de 100 cuentas, 100 direcciones creadas por cuenta, 1000 eventos de consentimiento por cuenta y 1000 sesiones activas por proceso. Son límites de pruebas, no capacidades del sistema final.

## Acceso y aislamiento

En modo de demostración **solo se reconocen los clientes temporales creados en ese proceso**. No iniciar sesión con el administrador SQL en esa ejecución; sus cuentas no se importan ni se modifican. Una cookie SQL anterior no se reutiliza como acceso de demostración: usar incógnito para evitar confusión con la sesión anterior.

Se mantiene la infraestructura de cookies seguras, CSRF, limitación de peticiones y hash IPasswordHasher<Usuario> existente con sus opciones. Se sustituye explícitamente IAuthService por el proveedor simulado de clientes: no es el motor de identidad de producción. Ningún cliente temporal recibe roles, permisos o negocios administrativos.

Las sesiones usan credenciales aleatorias del generador existente, almacenadas por hash en memoria, duran 30 minutos y están separadas por host API/MVC. Logout revoca la sesión. Cinco contraseñas incorrectas bloquean la cuenta temporal durante 15 minutos. Es comportamiento de la simulación, no evidencia de bloqueo distribuido real.

## Recuperación, MFA y documentos

La simulación **no envía correo, no confirma direcciones de correo, no recupera/restablece/cambia contraseñas y no emite retos MFA**. Esas acciones no se presentan como realizadas. Los formularios de recuperación/cambio responden con un aviso 503 cuando corresponda; para reiniciar la prueba usar una cuenta ficticia nueva o detener el proceso.

No se concede ningún rol administrativo que permita omitir MFA. Para verificar la seguridad SQL y sus flujos existentes, desactivar la demostración y usar las pruebas de regresión anteriores.

Los documentos `demo-terminos-1`, `demo-privacidad-1` y `demo-promociones-1` dicen expresamente que son ficticios. No son políticas legales aprobadas ni autorizan el uso de datos reales. Solo se incorporan en memoria al activar este modo; no reemplazan documentos de producción.

## Estado de datos y trazabilidad de demostración

El almacén único por proceso mantiene cuentas, perfiles, direcciones y decisiones bajo exclusión mutua para poder comprobar flujos concurrentes de prueba. Revalida propiedad al escribir. Eliminar la principal la desactiva y quita su marca sin elegir otra automáticamente. Los DTO se copian para impedir modificaciones indirectas del almacén mediante referencias.

Los cambios de consentimiento añaden eventos. Cuando el reloj coincide, se usa un incremento mínimo de ticks para ordenar la simulación; la evidencia técnica original se conserva internamente. Este mecanismo no define un nuevo contrato temporal SQL.

Se conserva una cola temporal de hasta 1000 eventos técnicos de demostración con actor, acción, fecha y correlación; no almacena contraseñas, formularios ni secretos. No se expone un endpoint de auditoría. Esta cola no sustituye el contrato de auditoría persistente aprobado.

## API de demostración

Si se desea probar la API de forma independiente:

```bash
Clientes__Demostracion=true dotnet run --project PortalMercado.API --launch-profile https
```

Base https://localhost:7161. Utilizar las rutas del bloque 07 con la cookie API y el token X-CSRF-TOKEN. La cabecera `X-PortalMercado-Clientes-Modo: Demostracion-temporal` identifica este modo.

**Web y API son procesos distintos y tienen almacenes separados.** Una cuenta creada en Web no existe en la demostración API; registrar otra mediante POST /api/clientes/registro en el proceso API y luego acceder allí. No ejecutar ambos esperando compartir cuentas o direcciones en memoria.

## Pruebas

Sin exportar Clientes__Demostracion globalmente, ejecutar desde Git Bash:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Clientes" --logger "trx;LogFileName=pruebas-ciclo3-clientes-bloque09-demostracion.trx"
```

Se añaden **22 casos** (17 unitarios/de almacén y 5 HTTP completos) a los 329 previos: se esperan **351 pruebas de Clientes**. Cubren activación restringida, correos ficticios, duplicados simultáneos, sesión/host/logout/expiración/bloqueo, reinicio, aislamiento, principal concurrente en memoria, historial y recorridos MVC/API sin conexión SQL de clientes.

Los casos de concurrencia son **de la simulación en memoria**, no de SQL. Los tests HTTP configuran una conexión SQL deliberadamente inaccesible y ejercitan los clientes sin usarla; no prueban que se hayan guardado filas SQL.

Aquí se revisaron sintaxis léxica, delimitadores C# y el paquete; no hay SDK .NET, por lo que compilación/Razor y ejecución quedan por confirmar en Windows. La regresión completa pasa a 701 casos esperados si están aplicados todos los bloques (679 +22); generar un nuevo TRX después de confirmar el filtro.

## Desactivar y volver al acceso habitual

Detener el proceso y levantar Web con el comando anterior de tiendas, sin la variable de clientes:

```bash
Tiendas__SqlLocal=true Tiendas__Demostracion=false Tiendas__NegocioLocalDemostracion= dotnet run --project PortalMercado.Web --launch-profile https
```

La variable puesta al principio de un comando Git Bash afecta solo a ese comando. No usar `export` para esta activación. Al desactivar se recuperan los proveedores de seguridad SQL existentes y los contratos pendientes de clientes vuelven a 503. Las cuentas de demostración no se migran ni se conservan.

## Cumplimiento y entrega

Este modo permite producir capturas y un video de **demostración simulada**, indicando ese alcance en la presentación y documentación. No presentar esas capturas como integración SQL real ni marcar aprobados los CA de transacciones, rollback o concurrencia SQL.

Siguen pendientes contratos/autorizaciones de datos, documentos/políticas aprobados, confirmación de correo real, búsqueda pública global y las pruebas de integración correspondientes. No se modifica ningún SP, tabla, índice o permiso SQL.
