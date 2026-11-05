# Usuarios, permisos y acceso por negocio

Este bloque continúa el login y MFA existentes. Añade administración MVC/API, asignaciones y contexto de negocio. La recuperación y el cambio de contraseña se implementarán en el siguiente bloque; la Etapa 02 aún no está completa.

## Actualizar tu solución existente

1. Detén API y Web con **Ctrl+C** en sus respectivas terminales. Cierra Visual Studio.
2. Conserva una copia de tu carpeta actual fuera de la solución.
3. Extrae el ZIP nuevo. Copia el contenido de su carpeta `PortalMercado` sobre tu carpeta actual `C:\Users\BRYAN\source\repos\PortalMercado`, aceptando reemplazar archivos del proyecto. No elimines tu carpeta `.git` ni las claves locales de Data Protection.
4. Se conservan los `UserSecretsId`: no tienes que volver a poner la conexión ni crear de nuevo vendedor o administrador. Los datos SQL, el segundo factor del administrador y sus códigos actuales se conservan.

## Actualizar SQL local

Abre SSMS y conecta a `.\SQLEXPRESS` con autenticación de Windows. La base de trabajo sigue siendo `PortalMercado_ET01_Test`.

1. Abre `database/etapa02/07_Administracion_Up.sql`.
2. Ejecuta **todo el archivo con F5**, sin seleccionar fragmentos. Este archivo **no necesita Modo SQLCMD**.
3. Espera el mensaje `ET02-003 aplicada: usuarios, permisos y contexto preparados.` Si hay un error, conserva el mensaje y detente.
4. Abre `database/etapa02/08_Administracion_Verificar.sql` y ejecuta todo con F5.

Resultado esperado sobre la copia que ya tenía ET02-001 y ET02-002:

| Comprobación | Resultado |
|---|---|
| Versión nueva | ET02-003 |
| Tablas dbo | 64 |
| Procedimientos dbo | 9 |
| Claves foráneas | 91 |
| FK deshabilitadas o sin validar | Ninguna fila |
| ContextoBigint8 / ObjetivoBigint8 | 8 / 8 |

La migración añade `UsuarioSesion.IdNegocioActivo` con FK a Negocio, `AuditoriaAcceso.IdObjetivo` y una tabla pequeña de trazabilidad de la semilla. No cambia procedimientos financieros. Añade cuatro roles y once permisos cuando faltan; conserva nombres, permisos personalizados y asignaciones existentes. Si ET02-003 ya existe, termina sin volver a otorgar permisos eliminados posteriormente por un administrador.

`09_Administracion_Down.sql` permite revertir antes de usar el bloque. Usa transacción y bloqueo de migración; retira solo los datos que añadió la semilla, conservando los que ya existían. Se detiene si hubo operaciones administrativas, contexto seleccionado, roles nuevos asignados, permisos personalizados relacionados o migraciones posteriores. Después de utilizarlo se necesita una reversión específica que conserve la trazabilidad.

## Compilar y ejecutar

En PowerShell, desde la carpeta de la solución:

```powershell
Set-Location 'C:\Users\BRYAN\source\repos\PortalMercado'
dotnet build PortalMercado.sln -c Debug
dotnet build PortalMercado.sln -c Release
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project PortalMercado.API --launch-profile https
```

En **otra terminal**, desde la misma carpeta:

```powershell
dotnet run --project PortalMercado.Web --launch-profile https
```

Las terminales se quedan mostrando `Application started`: es normal; los dos servicios deben seguir abiertos.

## Comprobar las pantallas

1. Abre `https://localhost:7275/Cuenta/Login` e inicia sesión con **tu administrador existente**. Introduce el código actual de tu aplicación autenticadora. No repitas el bootstrap.
2. Abre **Usuarios**. Aparecen las cuentas paginadas y su estado; el administrador general ve toda la plataforma.
3. Abre la cuenta `vendedor.et02@example.invalid` con **Ver cuenta**.
4. En **Negocios autorizados**, marca `Negocio de prueba ET01` y pulsa **Guardar negocios**. Esto revoca las sesiones anteriores del vendedor.
5. En **Roles y permisos**, puedes revisar los permisos de cada rol. Los del rol que posee tu propia cuenta quedan protegidos frente a cambios desde esa misma cuenta.
6. Abre una **ventana de incógnito**, entra a la web e inicia sesión como vendedor con su contraseña privada.
7. En **Mi perfil**, comprueba la asignación al negocio. En **Mis negocios**, selecciona el negocio y pulsa **Usar este negocio**. El contexto actual debe mostrar su nombre.
8. En esa misma ventana del vendedor, escribe manualmente `https://localhost:7275/Usuarios`. Debe responder **403 / Operación no disponible**. La ausencia del enlace por sí sola no es la prueba: el servidor también deniega la URL.
9. Conserva la ventana normal del administrador para crear otras cuentas con **Usuarios → Nuevo usuario**. Las contraseñas deben tener de 12 a 128 caracteres, mayúscula, minúscula, número y símbolo. No las incluyas en capturas.

Las cuentas ADMIN_GENERAL o ADMIN_MERCADO nuevas quedan inactivas y pendientes de enrolamiento. Deben entrar con su contraseña inicial y completar MFA para activarse. El botón Activar no permite saltarse este paso. Desactivar una cuenta pendiente cancela la autorización de enrolamiento; un administrador general puede volver a asignarle el rol administrativo para autorizar nuevamente el proceso.

No se permite cambiar la propia identidad, roles o asignaciones desde la administración. El último administrador general activo está protegido. Para modificar una cuenta de ámbito limitado, **todas** sus asociaciones deben pertenecer al actor: una cuenta compartida con negocios ajenos queda reservada al administrador general. Las asociaciones históricas inactivas también se tienen en cuenta en esa comprobación conservadora.

## Permisos y ámbitos

La matriz completa está en `MATRIZ_PERMISOS_ET02.md`. Cada operación verifica permisos explícitos y recursos en el servidor. Los permisos y pertenencias se vuelven a consultar; una selección de contexto o un ID recibido no concede acceso.

- ADMIN_GENERAL: ámbito global, con permisos explícitos.
- ADMIN_MERCADO: mercados activos asignados y sus negocios activos. Una asociación directa a un negocio ajeno no amplía su alcance administrativo.
- PROPIETARIO_NEGOCIO: negocios activos con asociación activa. Puede asignar VENDEDOR; no administradores ni propietarios.
- VENDEDOR: contexto de negocios autorizados; sin administración por defecto.

Las identidades globales compartidas se administran solo cuando el actor controla todos sus ámbitos. Cambios de estado, roles, nombre/teléfono o asignaciones rotan el sello de seguridad; las sesiones anteriores dejan de servir en la siguiente petición. Cambiar permisos de un rol revoca las sesiones de sus miembros. El contexto se guarda por sesión y host; una selección en MVC no cambia la sesión API. Un contexto cuyo negocio deja de estar autorizado se devuelve como no seleccionado.

La modificación de permisos globales requiere ADMIN_GENERAL y PERMISO_GESTIONAR. No se puede modificar un rol propio ni conceder permisos que el actor no posee. No se borran usuarios físicamente. Las asociaciones UsuarioNegocio se desactivan/reactivan conservando sus filas.

## API y Swagger

Abre `https://localhost:7161/swagger`. El interceptor obtiene CSRF para operaciones de escritura. El login API es independiente del login MVC. Usa el flujo MFA existente para entrar como administrador: login → 202 → mfa/verify → 200. Después puedes consultar usuarios y catálogos.

| Método | Ruta | Autorización |
|---|---|---|
| GET | /api/usuarios?pagina=1&tamano=20 | USUARIO_VER y ámbito |
| GET | /api/usuarios/{id} | USUARIO_VER y ámbito |
| POST | /api/usuarios | USUARIO_CREAR, ROL_ASIGNAR y permisos de las asignaciones incluidas |
| PUT | /api/usuarios/{id} | USUARIO_EDITAR y ámbito |
| PATCH | /api/usuarios/{id}/estado | USUARIO_DESACTIVAR y ámbito |
| GET | /api/roles | ROL_VER |
| GET | /api/permisos | PERMISO_VER |
| PUT | /api/usuarios/{id}/roles | ROL_ASIGNAR y ámbito |
| PUT | /api/usuarios/{id}/negocios | NEGOCIO_USUARIO_ASIGNAR y ámbito |
| PUT | /api/usuarios/{id}/mercados | MERCADO_USUARIO_ASIGNAR y ámbito |
| PUT | /api/roles/{id}/permisos | ADMIN_GENERAL + PERMISO_GESTIONAR |
| GET | /api/administracion/catalogos | Algún permiso de administración o consulta de catálogos |
| GET / PUT | /api/negocios/contexto | NEGOCIO_CONTEXTO y pertenencia |

Las asignaciones PUT reemplazan el conjunto seleccionado; un array vacío elimina las asignaciones activas, excepto donde haría perder el ámbito de una cuenta administrada por un actor limitado. Las páginas admiten tamaños de 1 a 100. Los DTO de salida no contienen PasswordHash ni credenciales de sesión. Las peticiones PUT/PATCH/POST requieren antiforgery además del login.

Errores: 400 campos/CSRF inválidos, 401 sesión ausente o revocada, 403 permiso o ámbito insuficiente, 409 correo duplicado o activación administrativa antes de MFA. Se devuelve el mismo 403 para IDs inexistentes o fuera de ámbito. MVC muestra un mensaje seguro; API usa ProblemDetails. `PortalMercado.API/Administracion.http` incluye ejemplos sin contraseñas.

## Pruebas

Detén ambos servicios con Ctrl+C. Suite sin SQL:

```powershell
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter 'Category!=SqlIntegration'
```

Para la suite completa, prepara primero ET02-003 y conserva la semilla ET01. Estas pruebas SQL crean cuentas, dos mercados y dos negocios **ficticios y temporales**, con GUID; limpian solo sus filas al terminar. Reutilizan el identificador del comerciante de la semilla sin modificarlo. No operan pedidos, cobros ni liquidaciones. La cuenta Windows necesita permisos de lectura/escritura sobre la base local de pruebas.

```powershell
$env:PM_RUN_SQL_TESTS = '1'
try {
    dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --logger 'trx;LogFileName=pruebas-etapa02-administracion-sql.trx' --results-directory ./TestResults
} finally {
    Remove-Item Env:PM_RUN_SQL_TESTS -ErrorAction SilentlyContinue
}
```

Resultado esperado con esta versión: **92 pruebas sin SQL** o **106 pruebas completas (92 + 14 SQL)**, todas aprobadas y sin omitidas al habilitar PM_RUN_SQL_TESTS. Sin esa variable, las SQL se omiten explícitamente.

La suite incorpora acceso cruzado A/B, identidad compartida, creación autorizada y hash, vendedor sin privilegios administrativos, bloqueo de autoelevación, revocación de sesiones, contexto por host y activación condicionada a MFA. Los siete casos SQL nuevos están compilados pero deben ejecutarse en tu Windows: el entorno de desarrollo de este paquete no dispone de SQL Server. La evidencia del bloque anterior no valida automáticamente estas nuevas pruebas.

Para conservar el resultado local nuevo:

```powershell
New-Item -ItemType Directory -Force '.\docs\evidencias\etapa02-administracion' | Out-Null
Copy-Item '.\TestResults\pruebas-etapa02-administracion-sql.trx' '.\docs\evidencias\etapa02-administracion\pruebas-etapa02-administracion-sql.trx'
```

La próxima ampliación es recuperación y cambio de contraseña. No etiquetar todavía esta versión como la Etapa 02 completa.
