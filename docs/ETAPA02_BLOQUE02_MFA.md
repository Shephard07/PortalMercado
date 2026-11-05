# Etapa 02: segundo factor administrativo

Este bloque amplía la autenticación inicial. Incluye MFA TOTP para administradores, activación del primer administrador tras enrolamiento, códigos de recuperación y documentación de respuestas OpenAPI. Todavía no completa la Etapa 02: gestión de usuarios/permisos/ámbitos, selector de negocio y recuperación/cambio de contraseña continúan pendientes.

## Qué cambia

- Otp.NET 1.4.1: TOTP de seis dígitos con periodo de 30 segundos y margen de un periodo anterior/posterior. Se guarda el último paso aceptado para impedir reutilización.
- Login administrativo en dos fases: contraseña correcta devuelve un reto de cinco minutos; no crea sesión ni actualiza UltimoAcceso hasta completar MFA.
- Los retos tienen hash, host, sello, consumo único y máximo de cinco fallos. Abrir un reto nuevo invalida el anterior de la cuenta, incluso entre API/MVC. Los fallos de ambos factores acumulan el bloqueo de cuenta de cinco intentos durante quince minutos.
- El primer administrador nace inactivo. Solo su fila de bootstrap pendiente permite enrolamiento; confirmar el TOTP activa la cuenta y DosFactoresHabilitado en la misma transacción.
- Se emiten diez códigos aleatorios de recuperación de 128 bits, mostrados una sola vez y almacenados únicamente mediante hash. Cada uno requiere contraseña y reto válidos antes de consumirse.
- En la verificación de un login posterior se pueden regenerar los códigos. Requiere la contraseña recién comprobada y TOTP/código de recuperación válido; invalida los códigos anteriores y las sesiones/retos anteriores mediante cambio de sello.
- Los secretos TOTP se cifran mediante ASP.NET Core Data Protection. No aparecen en logs ni datos semilla. La clave de enrolamiento se muestra exclusivamente en el flujo autorizado por la contraseña.
- Las sesiones guardan si MFA fue comprobado. Otorgar un rol administrativo a una sesión sin MFA no convierte esa sesión en administrativa.
- El login normal del vendedor conserva respuesta 200. El administrativo devuelve 202 mientras falta el segundo factor. OpenAPI documenta 204 de logout y 401 de acceso sin sesión.
- Preparar CSRF tiene límite separado de 60 peticiones/minuto por IP; login y operaciones MFA mantienen diez/minuto. No se deshabilita antiforgery.

## Actualizar el laboratorio

1. Detener API y Web con Ctrl+C.
2. Conservar el ZIP del bloque anterior. Actualizar el código de la copia de trabajo con este bloque y abrir PortalMercado.sln.
3. Conectar SSMS a `.\SQLEXPRESS`, base `PortalMercado_ET01_Test`.
4. Ejecutar completo `database/etapa02/04_Mfa_Up.sql` y luego `05_Mfa_Verificar.sql`. No requieren SQLCMD. No repetir instalación ET01 ni revertir la ampliación ET02-001.
5. Esperado, si no hay otras ampliaciones: **63 tablas dbo, 9 SP y 90 FK**, sin FK inválidas y con ET02-001/ET02-002 registradas.
6. Compilar Debug/Release. La actualización de la configuración de protección de claves exige volver a iniciar sesión en las dos aplicaciones.

La autorización para este trabajo se aplica al laboratorio local indicado. La migración usa transacción y versión; una segunda ejecución no duplica objetos. No cambia procedimientos financieros. Reversión: `06_Mfa_Down.sql`, antes de usar MFA y con los hosts detenidos. Se rechaza si hay secretos/retos/códigos/evidencia MFA o ampliaciones posteriores; después de uso debe acordarse una restauración que conserve evidencia. Volver atrás también exige código compatible con el bloque anterior.

## Aprovisionar el primer administrador

Si no se ha ejecutado bootstrap administrativo, ejecutar desde la solución:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Bootstrap__Correo = "admin.et02@example.invalid"
$env:Bootstrap__Nombre = "Administrador ficticio ET02"
$claveAdminEt02 = Read-Host "Elige una contraseña nueva para el administrador" -AsSecureString
try {
    $env:Bootstrap__Password = [System.Net.NetworkCredential]::new("", $claveAdminEt02).Password
    dotnet run --project PortalMercado.API --launch-profile https -- --bootstrap-admin
} finally {
    Remove-Item Env:Bootstrap__Password -ErrorAction SilentlyContinue
    Remove-Item Env:Bootstrap__Correo -ErrorAction SilentlyContinue
    Remove-Item Env:Bootstrap__Nombre -ErrorAction SilentlyContinue
    Remove-Variable claveAdminEt02 -ErrorAction SilentlyContinue
}
```

La contraseña debe cumplir la política de 12 a 128 caracteres, mayúscula/minúscula/número/símbolo. No usar la del vendedor ni publicarla. El comando termina, no deja un servidor ejecutándose. El bootstrap sigue siendo de un solo uso. Si ya se creó el administrador inactivo en el bloque anterior, no repetirlo: la migración prepara únicamente aquella cuenta auditada para el enrolamiento.

## Enrolamiento MVC

1. Arrancar API y Web en terminales distintas.
2. Abrir `https://localhost:7275/Cuenta/Login` e ingresar con la cuenta administrativa recién creada.
3. Se abre Configura la verificación en dos pasos. En una app compatible con TOTP agregar una cuenta mediante clave de configuración, tipo basado en tiempo. Nombre de cuenta: PortalMercado; clave: la mostrada en esa pantalla.
4. Escribir los seis dígitos de la app y pulsar Confirmar y activar mi cuenta. Si el reto vence mientras se configura el teléfono, volver al login y repetir; la clave pendiente se conserva.
5. Guardar privadamente los diez códigos de recuperación mostrados una sola vez. No capturar esa pantalla para evidencias. Pulsar Ya guardé mis códigos.
6. En Mi perfil debe aparecer ADMIN_GENERAL. En este bloque aún no hay panel de administración de usuarios.
7. Cerrar sesión. El siguiente login debe pedir el código, sin volver a mostrar la clave secreta.
8. Después de usar un TOTP, esperar a que la app muestre un código nuevo para otro login. Un mismo paso temporal no se acepta otra vez.

Para probar recuperación, iniciar sesión con contraseña y usar un código de recuperación en lugar del código de la app. Un segundo intento con ese código debe fallar. La casilla de generar nuevos códigos permite renovar la reserva, invalidando la anterior. No elimina ni deshabilita MFA. El cambio de dispositivo/rotación del secreto TOTP no tiene todavía pantalla propia; recuperar acceso mediante los códigos no restablece la contraseña.

## API y Swagger

- POST `/api/auth/login`: vendedor 200; administrador con contraseña correcta 202, con challenge, enrollmentRequired y expiresUtc. No se emite cookie de sesión en 202.
- POST `/api/auth/mfa/setup`: enviar `{ "challenge": "valor privado del 202" }` para obtener los datos de enrolamiento inicial; un reto de login normal no permite leer el secreto.
- POST `/api/auth/mfa/verify`: enviar `{ "challenge": "valor privado", "code": "código del teléfono", "regenerateRecoveryCodes": false }`. Correcto: 200, cookie y perfil; primer enrolamiento/renovación también devuelve recoveryCodes, una sola vez. Incorrecto/vencido/consumido/otro host: 401.
- GET `/api/auth/me` continúa en 401 hasta completar MFA. Logout devuelve 204 y luego me vuelve a 401.

El reto es específico de API o MVC; no copiarlo entre aplicaciones. Los POST mantienen CSRF. Swagger lo prepara con el interceptor corregido. No publicar claves, códigos, retos ni el cuerpo del login. Un 429 pide esperar un minuto.

## Claves de cifrado

Ambos hosts usan el mismo application name y repositorio. En desarrollo Windows, por defecto: `%LOCALAPPDATA%\PortalMercado\Keys\Development`. Los archivos se protegen con DPAPI del usuario de Windows; API y MVC deben ejecutarse con la misma identidad. No incluirlos en el ZIP ni borrarlos: perderlos impide descifrar las claves TOTP y obliga a un procedimiento de recuperación.

Fuera de Development, esta implementación MFA exige Windows y `Security:DataProtectionKeyPath` explícito, con permisos limitados a la cuenta del servicio. Utiliza la misma ruta e identidad para ambos hosts. Un despliegue con otra plataforma/identidades requiere adaptar y validar el almacén de claves. Este avance no representa una habilitación productiva de la etapa completa.

## Pruebas

Sin SQL, detener los hosts y ejecutar:

```powershell
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "Category!=SqlIntegration" --logger "trx;LogFileName=pruebas-etapa02-mfa-local.trx" --results-directory ./TestResults
```

Las pruebas de criptografía usan un vector RFC 6238, rechazo de reutilización y vencimiento, formato de códigos y protección del secreto entre proveedores. Las HTTP usan servicios controlados: verifican 202 sin sesión, MFA incorrecto, verificación y metadatos OpenAPI.

Hay **cuatro pruebas SQL MFA nuevas**, optativas y exclusivamente locales. Crean cuentas ficticias temporales con contraseñas aleatorias y limpian solo sus filas de usuario, MFA, sesión y auditoría al terminar. Si falta ADMIN_GENERAL, lo agregan como rol sin permisos y lo conservan. No afectan cuentas del operador ni tablas financieras. Se suman a las tres lecturas SQL de ET01; la suite SQL ya no es solo de lectura.

Después de aplicar ET02-002 y con API/Web detenidas:

```powershell
$env:PM_RUN_SQL_TESTS = "1"
try {
    dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --logger "trx;LogFileName=pruebas-etapa02-mfa-sql.trx" --results-directory ./TestResults
} finally {
    Remove-Item Env:PM_RUN_SQL_TESTS -ErrorAction SilentlyContinue
}
```

La ejecución en el entorno de desarrollo de este ZIP no dispone de SQL Server del usuario. Las pruebas SQL y el enrolamiento real deben validarse en Windows; no se declaran aprobados sin su TRX. Las evidencias anteriores se conservan con sus nombres originales.

Retención: los retos vencen a cinco minutos y se rechazan aunque su fila permanezca. Los códigos gastados conservan su marca de uso hasta renovación. No hay purga automática; acordar retención de retos, sesiones y auditoría antes del despliegue.

Referencias técnicas: https://github.com/kspearrin/Otp.NET y https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview

## Evidencia de esta revisión

Debug/Release sin errores ni advertencias. **61 pruebas sin SQL aprobadas**. Evidencia en `docs/evidencias/etapa02-mfa`. Los siete casos SQL están pendientes de ejecución local; no confundir esa evidencia con validación SQL real.
