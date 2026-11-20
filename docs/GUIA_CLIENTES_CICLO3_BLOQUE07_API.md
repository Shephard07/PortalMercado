# Ciclo 3 — Bloque 07: API de clientes

## Cambio

Se exponen las operaciones de clientes que faltaban en API, reutilizando los servicios de Application y la seguridad de acceso existente. No se duplican validadores ni reglas de propiedad. `Program.cs` registra los mismos contratos de clientes usados por MVC.

| Método | Ruta | Acceso | Resultado correcto |
|---|---|---|---|
| POST | /api/clientes/registro | Público, CSRF y limitador auth | 201 con requiereConfirmacionCorreo; no crea sesión |
| GET | /api/mi-perfil | Sesión API + perfil cliente propio | 200 perfil privado |
| PUT | /api/mi-perfil | Sesión API + CSRF | 204 |
| GET | /api/mis-direcciones | Sesión API + cliente propio | 200 direcciones activas |
| GET | /api/mis-direcciones/{id} | Sesión API + propiedad | 200 dirección propia |
| POST | /api/mis-direcciones | Sesión API + CSRF | 201 y Location de la dirección creada |
| PUT | /api/mis-direcciones/{id} | Sesión API + CSRF + propiedad | 204 |
| DELETE | /api/mis-direcciones/{id} | Sesión API + CSRF + propiedad | 204 |
| PUT | /api/mis-direcciones/{id}/principal | Sesión API + CSRF + propiedad | 204; una operación al repositorio |
| GET | /api/mis-consentimientos | Sesión API + cliente propio | 200 historial privado |
| POST | /api/mis-consentimientos | Sesión API + CSRF + cliente propio | 204 |

Se adoptan las rutas orientativas del PDF y se añaden GET de dirección individual y GET de historial para consultar los recursos. Las rutas MVC se conservan.

## Seguridad y respuestas

- El propietario deriva de la credencial de sesión API y se valida en los servicios. No hay parámetros de IdCliente/IdUsuario para seleccionar propietarios.
- Los DTO JSON existentes rechazan miembros desconocidos. El perfil solo admite apellidos, celular y el par de documento; las direcciones no admiten propietario, principal ni estado activo como campos editables.
- Toda mutación usa `ApiAntiforgeryFilter`, incluido el registro anónimo. El registro tiene además el limitador `auth` existente.
- Obtener primero GET /api/auth/csrf, conservar la cookie y enviar el requestToken en X-CSRF-TOKEN. Tras iniciar sesión o cambiar identidad, obtener otro token autenticado antes de operar.
- El acceso API usa la sesión API existente. Una sesión MVC no sustituye automáticamente la sesión API.
- Lecturas y mutaciones privadas requieren autenticación. Una cuenta con rol comercial o administrativo sin Cliente asociado no obtiene automáticamente un perfil de cliente.
- Errores de negocio con ProblemDetails y traceId; se registran estado y correlación, sin copiar formularios ni contraseñas. Los mensajes de errores de proveedor >=500 se sustituyen por un texto genérico.
- La API no publica IP de consentimientos, hash, credenciales o secretos de seguridad. Las respuestas privadas no deben reutilizarse en búsqueda pública.
- No se amplía CORS, no se cambian cookies ni algoritmos de identidad, no se habilitan pedidos ni pagos.

Códigos esperados: 400 entrada/CSRF no válidos; 401 sesión ausente o revocada; 403 cuenta sin perfil cliente; 404 dirección no disponible para ese propietario; 409 conflicto; 503 proveedor/documentos pendientes. Identificadores no numéricos pueden no coincidir con la ruta y producir 404; los numéricos no positivos se rechazan como entrada inválida. La infraestructura existente gestiona fallos no controlados sin exponer la excepción.

## Registro y documentos

El cuerpo usa RegistroClienteSolicitud del bloque 01. Para un registro válido se requieren nombre, correo, contraseña/confirmación válidas, aceptación de términos y privacidad y sus versiones vigentes. Teléfono y promociones son opcionales.

El registro no autentica ni devuelve IdUsuario, password/hash o tokens; únicamente requiereConfirmacionCorreo. La confirmación real sigue siendo una dependencia del flujo de identidad aprobado.

Como en MVC, la API no permite registrar cuando faltan textos/versiones documentados o el adaptador de alta. No basta con activar configuración para simular persistencia.

## Consentimientos

POST /api/mis-consentimientos recibe tipo, versionDocumento y aceptado. `aceptado` es obligatorio en JSON: omitirlo no equivale a retirar autorización.

Este endpoint gestiona la preferencia PROMOCIONES. Los términos y privacidad se aceptan al registrar; no se alteran mediante la preferencia comercial. Autorizar exige documento vigente; retirar puede referirse a una versión presente en el historial aunque ya no exista documento actual. Se conserva el historial mediante inserciones, según el contrato de persistencia pendiente.

## Persistencia: límite actual

**Este paquete no implementa SQL de clientes.** Los proveedores predeterminados siguen respondiendo 503. No se crean cuentas o direcciones de demostración ni se convierten cuentas de otros roles en clientes. Los tests nuevos utilizan repositorios de prueba.

El alta única, rollback, propiedad SQL, principal concurrente y auditoría persistente siguen pendientes del contrato autorizado. No se añaden ni modifican tablas, SP, índices o permisos.

## Aplicar y probar

1. Detener Web/API y guardar el trabajo actual.
2. Copiar la carpeta PortalMercado sobre la raíz de la solución. Requiere bloques 01–05; el bloque 06 solo añade documentación.
3. El paquete reemplaza Program.cs de API y añade controller, pruebas y esta guía. No reemplaza pruebas anteriores ni configuración de conexión.
4. Ejecutar en Git Bash:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Clientes" --logger "trx;LogFileName=pruebas-ciclo3-clientes-bloque07-api.trx"
```

Se agregan 37 casos al total anterior de 284: se esperan **321 pruebas de Clientes** si no hubo otros cambios. Incluyen registro público, no autologin, duplicado genérico, rechazo de campos JSON no permitidos, sesión/CSRF, CRUD, acceso ajeno, principal, consentimiento explícito y proveedores pendientes.

Revisión léxica/delimitadores C# e integridad del ZIP realizadas aquí. Este entorno no tiene SDK .NET: no se afirma compilación ni ejecución de los 37 casos hasta confirmarlas en tu equipo.

Regresión completa con SQL existente (después de que pase el filtro de Clientes):

```bash
PM_RUN_SQL_TESTS=1 \
PM_RUN_CATALOGO_SQL_TESTS=1 \
PM_RUN_PRODUCTOS_SQL_TESTS=1 \
PM_RUN_CATALOGO_FIXTURE_TESTS=1 \
PM_RUN_CATALOGO_PAGINACION_TESTS=1 \
PM_RUN_TIENDAS_SQL_TESTS=1 \
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --logger "trx;LogFileName=pruebas-ciclo3-solucion-api-sql-local.trx"
```

Se esperan 671 pruebas totales (634 previas +37), sin omisiones con la copia local y scripts previos. Este nuevo resultado deberá registrarse aparte; el TRX de 634 no acredita este código posterior.

## Comprobación manual de API disponible

Desde Git Bash:

```bash
dotnet run --project PortalMercado.API --launch-profile https
```

Usar https://localhost:7161 como base. Con un cliente HTTP que conserve cookies:

1. GET /api/mi-perfil sin sesión: esperar 401, sin redirección a HTML.
2. GET /api/auth/csrf y conservar cookie/requestToken.
3. POST /api/auth/login con una cuenta existente y X-CSRF-TOKEN. Completar MFA mediante las rutas existentes cuando corresponda.
4. GET /api/auth/csrf nuevamente ya autenticado.
5. GET /api/mi-perfil: sin el contrato aprobado se espera 503 controlado, no datos inventados.
6. Cuando haya adaptadores, realizar alta/CRUD, probar dos clientes y verificar accesos ajenos, auditoría y concurrencia reales. Los tests de este paquete comprueban la capa API con dobles.

## Actualización de la revisión del bloque 06

La afirmación de que faltaba API privada correspondía al estado previo a este paquete. Aquí se añade su implementación; sigue pendiente confirmar los 37 casos y la integración real.

No cambia por sí sola a aprobado ningún CA dependiente de persistencia. Siguen pendientes botón público de extensión, documentos/identidad aprobados, transacciones SQL, lectura pública global, revisión visual completa y entrega final. Las matrices del bloque 06 son un corte histórico; deben actualizarse con los resultados confirmados del bloque 07.
