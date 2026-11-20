# Ciclo 3 — Bloque 03: registro web y retorno después del acceso

## Qué incluye

- Formulario público `/Registro`: nombre, correo, teléfono opcional, contraseña y confirmación.
- Validaciones del bloque 01; términos y privacidad obligatorios; promociones opcionales sin preselección.
- Lectura de documentos versionados como texto codificado, en otra pestaña para conservar el formulario.
- CSRF, limitador `auth`, páginas sin caché, eliminación de contraseñas al volver a mostrar errores.
- Un único comando al repositorio de registro; nunca crea sesión automáticamente.
- Patrón POST/redirect/GET: la página de éxito necesita un indicador protegido de una operación previa y solo se muestra una vez.
- Inicio de sesión existente con `returnUrl` local. Se conserva en errores y después de MFA. Al generar códigos de recuperación se continúa mediante un enlace local, sin saltar la pantalla de códigos.
- Cookie de destino MFA cifrada/autenticada mediante Data Protection, vinculada al hash del reto, con expiración y atributos Secure/HttpOnly/SameSite=Strict. No se admiten destinos externos.
- Reutilización de recuperación de contraseña existente; no se implementa otra seguridad de acceso.

## Estado real y dependencia pendiente

**Este bloque no habilita todavía el alta persistente en SQL.** La implementación predeterminada `RegistroClientePendiente` responde 503. No modifica tablas, procedimientos, índices ni permisos. No crea usuarios en memoria y no añade cuentas de demostración.

El formulario se habilita únicamente cuando se hayan incorporado ambos elementos:

1. Adaptador aprobado de `IRepositorioRegistroCliente` que garantice una sola transacción Usuario + seguridad de acceso + Cliente + consentimientos + auditoría, correo único incluso en concurrencia, sin roles administrativos. Referencias: `CONTRATOS_CLIENTES_CICLO3.md` y `SOLICITUD_CONTRATO_CLIENTES_CICLO3.md`.
2. Textos aprobados y versiones vigentes de términos y privacidad. Las condiciones de promociones son opcionales; si se configuran, requieren texto y versión completos.

La autorización anterior de SQL local para tiendas no constituye un contrato de alta de clientes. No se presupone un procedimiento que no se ha recibido.

Configuración esperada (sin versiones ni textos legales inventados):

```text
Clientes:Documentos:Terminos:Version
Clientes:Documentos:Terminos:Texto
Clientes:Documentos:Privacidad:Version
Clientes:Documentos:Privacidad:Texto
Clientes:Documentos:Promociones:Version   (opcional)
Clientes:Documentos:Promociones:Texto     (opcional)
```

Los textos se publican como texto plano: sin `Html.Raw` ni ejecución de HTML configurado. No colocar contraseñas ni datos de clientes en estos valores. El adaptador se registra mediante DI en lugar de `RegistroClientePendiente`; cambiar solo configuración no habilita SQL.

Los dobles que permiten probar el formulario y el alta exitosa existen **solo dentro de Tests**. Sus textos `test-1` no son documentos válidos de producción. El resultado de estas pruebas no demuestra transacción ni persistencia real en SQL.

La confirmación de correo depende del proveedor de identidad aprobado: el formulario respeta `RequiereConfirmacionCorreo`, pero este bloque no fabrica tokens ni acredita un correo como confirmado. La entrega/consumo de confirmación queda pendiente del flujo aprobado. La recuperación existente se conserva.

## Aplicar

1. Detener Web y API, y guardar el trabajo actual.
2. Extraer la carpeta `PortalMercado` del ZIP sobre la raíz de la solución, conservando su estructura y reemplazando los archivos incluidos.
3. El paquete no sobrescribe `RegistroClienteServicioTests.cs`, donde corregiste las dos advertencias de xUnit.
4. No ejecutar scripts SQL ni migraciones para este bloque.

## Pruebas desde Git Bash

Desde la raíz de la solución:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Clientes" --logger "trx;LogFileName=pruebas-ciclo3-clientes-bloque03.trx"
```

Se agregan 36 casos al conjunto previo de 167: se esperan 203 si están instalados los bloques anteriores y no hubo otros cambios. Confirmar el resumen real; no se adjunta un TRX inventado.

Regresión del acceso y MFA:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~MfaWebTests|FullyQualifiedName~WebIntegrationTests|FullyQualifiedName~AccessTests" --logger "trx;LogFileName=pruebas-ciclo3-regresion-acceso.trx"
```

Las pruebas nuevas usan dobles de identidad/repositorio y servidor de pruebas. No necesitan SQL ni `PM_RUN_TIENDAS_SQL_TESTS`.

Aquí se efectuó revisión léxica y de delimitadores C# y comprobación del ZIP. Este entorno no dispone de SDK .NET; la compilación, Razor y ejecución de las pruebas deben confirmarse en tu equipo.

## Comprobación manual disponible ahora

Levantar con la conexión local de tiendas que ya funciona:

```bash
Tiendas__SqlLocal=true Tiendas__Demostracion=false Tiendas__NegocioLocalDemostracion= dotnet run --project PortalMercado.Web --launch-profile https
```

1. Abrir una ventana de incógnito en `https://localhost:7275/explorar`.
2. Buscar un producto y pulsar **Ingresar**: iniciar sesión con una cuenta existente debe regresar a la consulta, también si esa cuenta solicita MFA.
3. Sin iniciar sesión, abrir **Crear cuenta**: mientras no se instale el contrato y los documentos, se espera el aviso de registro no disponible (503). El catálogo continúa público.
4. Abrir `/Cuenta/Login?returnUrl=https%3A%2F%2Fexterno.invalid`: tras un acceso válido, el destino debe ser `/tiendas`, nunca el sitio externo.
5. Verificar escritorio y 390 px. No incluir contraseñas, secretos MFA ni códigos de recuperación en capturas.

## Alcance pendiente

- Alta real y rollback/concurrencia en SQL: bloqueados por contrato de persistencia.
- Confirmación real de correo: pendiente de identidad aprobada.
- Edición de perfil, direcciones y dirección principal: siguientes bloques.
- No se habilitan pedidos, pagos ni carrito en este bloque.

Este avance cubre la capa web y las pruebas de integración con interfaces; no declara terminados los criterios de alta persistente, confirmación de correo o direcciones del ciclo 3.
