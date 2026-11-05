> Documento del bloque anterior. En el bloque actual MFA está implementado; seguir `ETAPA02_BLOQUE02_MFA.md`.

# Etapa 02: autenticación inicial

Estado: primer bloque de desarrollo. No constituye la aceptación de la Etapa 02.

## Alcance disponible

- Mapeos de UsuarioRol, RolPermiso, Rol, Permiso, UsuarioNegocio y UsuarioMercado.
- Modelo ampliado con UsuarioSeguridad, UsuarioSesion, AuditoriaAcceso y SeguridadVersion.
- Login MVC y API con cookies separadas por host, Secure/HttpOnly/SameSite=Strict, 30 minutos de duración absoluta y sin renovación automática.
- Logout revoca la credencial en SQL; volver a enviar una cookie anterior no restaura la sesión.
- Validación de usuario activo, bloqueo y sello de seguridad en cada solicitud autenticada. Los roles y pertenencias se recargan desde SQL.
- Cuenta bloqueada 15 minutos tras cinco contraseñas incorrectas. Los intentos concurrentes de una cuenta se serializan mediante bloqueo de fila SQL.
- Rate limiting por IP: diez solicitudes por minuto en login y preparación CSRF. Es local a cada proceso; un despliegue con varias instancias requerirá un límite compartido.
- Formularios MVC y POST de autenticación API con antiforgery. No se devuelve el token de sesión en JSON; solo se guarda su hash en SQL.
- Perfil sin PasswordHash ni credenciales de sesión.
- Aprovisionamiento del primer administrador desde CLI, una sola vez, INACTIVO.
- Aprovisionamiento opcional de vendedor ficticio sin permisos comerciales, exclusivamente en Development y SQLEXPRESS local.

## Pendientes de la etapa completa

Administración de usuarios, matriz y políticas de permisos, asignaciones, pruebas de acceso cruzado, selección de negocio activo, recuperación y cambio de contraseña, proveedor de correo y MFA con enrolamiento/códigos de recuperación. Tampoco se considera completada la auditoría de estas operaciones pendientes.

No activar administradores manualmente: hasta implementar MFA, el servicio rechaza administradores y cualquier cuenta que tenga DosFactoresHabilitado, incluso con una contraseña correcta. No es un diferimiento aprobado de MFA ni una habilitación para producción. No publicar este avance como sistema terminado.

## 1. Preparación

Trabajar en una copia separada de la Etapa 01, con SDK 10.0.401. Ambos hosts necesitan la conexión local configurada en sus User Secrets, tal como en la etapa anterior. No cambiarla a una base remota para probar este avance.

Detener API/Web. Guardar un respaldo de la base local antes de aplicar DDL. No repetir scripts 00/01/02 de instalación de la Etapa 01.

## 2. Diagnóstico y propuesta SQL

En SSMS conectar a `.\SQLEXPRESS` con autenticación de Windows. Abrir y ejecutar completo `database/etapa02/00_Diagnostico.sql`. Es solo lectura; no requiere Modo SQLCMD. No publicar listados que contengan correos de personas reales.

La ampliación `01_Seguridad_Up.sql` requiere revisión y aprobación del responsable antes de ejecutarse. El documento de requisitos establece: «Se requiere aprobación para cualquier cambio al esquema».

Cambios propuestos ET02-001:

| Objeto | Finalidad |
|---|---|
| Usuario.CorreoNormalizado | Columna calculada persistida y un índice único. |
| UsuarioSeguridad | Intentos, bloqueo y sello, con PK/FK a Usuario. |
| UsuarioSesion | Hash único de credencial, host, sello, expiración y revocación. |
| AuditoriaAcceso | Actor opcional, evento, UTC y correlación. |
| SeguridadVersion | Registro de ampliación y bootstrap de un solo uso. |

Normalización: trim de espacios externos y mayúsculas; collation explícita Latin1_General_100_CI_AS. Aplicación acepta correos ASCII para coincidir con VARCHAR/normalización SQL. La migración detecta duplicados, espacios internos y caracteres fuera de ASCII; se detiene sin modificar cuentas. La deduplicación requiere revisión humana. El índice original de Correo permanece.

El script usa transacción, XACT_ABORT y bloqueo de aplicación. Una segunda ejecución con la versión registrada no modifica nada. Objetos de seguridad preexistentes sin versión conocida causan rechazo. No cambia tablas financieras, procedimientos ni datos semilla de ET01.

Después de aplicar la propuesta aprobada, ejecutar completo `03_Verificar.sql`. Sobre una instalación ET01 de 56 tablas/9 SP/84 FK sin otros cambios, el resultado será 60 tablas/9 SP/87 FK; todas las FK deben estar habilitadas y validadas.

Reversión: `02_Seguridad_Down.sql`, con servicios detenidos y respaldo. Se niega a borrar sesiones/auditoría o a revertir después del bootstrap/otras versiones. En esos casos exige un plan manual de conservación/restauración de evidencia; no borra cuentas ni roles silenciosamente. Para volver al código ET01 también debe restaurarse el código de esa etapa.

Expiración: las sesiones vencen a los 30 minutos aunque el registro siga en SQL. Los registros no se eliminan automáticamente. La retención/purga de sesiones y auditoría debe acordarse con el responsable; ningún trabajo de limpieza está habilitado.

## 3. Comprobar compilación y pruebas sin SQL

Desde la carpeta que contiene PortalMercado.sln:

```powershell
dotnet build PortalMercado.sln -c Debug
dotnet build PortalMercado.sln -c Release
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "Category!=SqlIntegration" --logger "trx;LogFileName=pruebas-etapa02-bloque01.trx" --results-directory ./TestResults
```

Las pruebas HTTP de autenticación sustituyen IAuthService. Comprueban el flujo HTTP/cookies/antiforgery y revocación con un servicio controlado; no demuestran bloqueo, concurrencia ni persistencia de SqlAuthService en SQL real. Estos casos SQL y el recorrido manual quedan pendientes de ejecutar en Windows después de aprobar la ampliación. Conservar el TRX de ET01 con su nombre original.

## 4. Usuario ficticio para verificar login local

Solo después de aplicar y verificar ET02-001. En PowerShell, desde la solución, ejecutar este bloque. La contraseña se escribe en un prompt oculto; no queda en el comando, código ni semillas. Elegir una de 12 a 128 caracteres con mayúscula, minúscula, número y símbolo. No usar una contraseña personal.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Bootstrap__Correo = "vendedor.et02@example.invalid"
$env:Bootstrap__Nombre = "Vendedor ficticio ET02"
$claveEt02 = Read-Host "Contraseña ficticia para la prueba" -AsSecureString
try {
    $env:Bootstrap__Password = [System.Net.NetworkCredential]::new("", $claveEt02).Password
    dotnet run --project PortalMercado.API --launch-profile https -- --provision-test-user
} finally {
    Remove-Item Env:Bootstrap__Password -ErrorAction SilentlyContinue
    Remove-Item Env:Bootstrap__Correo -ErrorAction SilentlyContinue
    Remove-Item Env:Bootstrap__Nombre -ErrorAction SilentlyContinue
    Remove-Variable claveEt02 -ErrorAction SilentlyContinue
}
```

El comando debe terminar; no arranca el servidor. Rechaza cualquier conexión que no sea SQLEXPRESS local, base PortalMercado_ET01_Test, autenticación Windows y ambiente Development. Crea una cuenta nueva activa con rol VENDEDOR, sin permisos ni pertenencias. Repetirlo no cambia la contraseña de una cuenta existente. No modifica `et01@example.invalid`.

## 5. Prueba manual MVC/API

Arrancar API y Web en terminales distintas, como en ET01. Abrir `https://localhost:7275/Cuenta/Login` y entrar con el correo y la contraseña elegidos. Debe aparecer Mi perfil. Cerrar sesión y volver al perfil debe solicitar login. Cinco intentos incorrectos bloquean la cuenta temporalmente; esperar quince minutos para otra prueba. No modificar directamente la marca MFA.

En `https://localhost:7161/swagger`, los POST preparan antiforgery automáticamente mediante un interceptor. La sesión API es independiente de MVC. GET `/api/auth/me` sin login devuelve 401. Después de POST login, GET me devuelve el perfil. POST logout revoca la sesión. No existen todavía endpoints administrativos.

Si se llama desde otro cliente HTTP:
1. GET `/api/auth/csrf`, conservar su cookie y requestToken.
2. POST `/api/auth/login` con JSON correo/password y cabecera X-CSRF-TOKEN.
3. Conservar la cookie de autenticación. Obtener un NUEVO requestToken después de login, porque cambió la identidad.
4. GET me o POST logout. El logout requiere el nuevo token y ambas cookies.

Las cookies requieren HTTPS. CORS no habilita credenciales entre orígenes en este bloque. MVC utiliza la misma capa de aplicación/infraestructura directamente y no requiere JavaScript llamando a API desde otro puerto.

## 6. Primer administrador

Cuando se acuerde el aprovisionamiento, ejecutar el mismo patrón de variables secretas usando un correo administrativo y el argumento `--bootstrap-admin` en lugar de `--provision-test-user`. Solo puede completarse una vez; un administrador general ya asignado impide repetirlo. La cuenta nace INACTIVA y el sello de bootstrap se guarda en SeguridadVersion en la misma transacción.

No ejecutar bootstrap ahora para probar la pantalla: usar el vendedor ficticio. El siguiente bloque completará enrolamiento MFA/activación administrativa con una política definida, sin contraseñas fijas ni bypass público.

## Evidencia de este avance

Debug y Release compilados con SDK 10.0.401, sin errores ni advertencias. Suite sin SQL de la revisión inicial: **41 aprobadas**, cero fallidas. La evidencia se encuentra en `docs/evidencias/etapa02-bloque01`. No se han ejecutado migraciones ni pruebas sobre SQL Server real.

Corrección posterior del diagnóstico de bootstrap: 45 pruebas sin SQL aprobadas, Debug/Release correctos. Evidencias en `docs/evidencias/etapa02-bootstrap-diagnostico`. Instrucciones de actualización en `CORRECCION_BOOTSTRAP.md`.
