# Recuperación y cambio de contraseña

Este bloque añade recuperación por enlace HTTPS y cambio desde una sesión autenticada. Continúa ET02-003 y conserva usuarios, roles, asociaciones, claves y códigos MFA. El cierre de la Etapa 02 requiere validar esta versión en SQL local y revisar los criterios de aceptación completos.

## Actualización de tu copia

1. Detén API y Web con Ctrl+C y cierra Visual Studio.
2. Conserva una copia de tu solución actual en otra carpeta.
3. Extrae el ZIP y copia el contenido de `PortalMercado` sobre tu solución existente, aceptando reemplazar archivos del proyecto. Conserva `.git`, User Secrets externos y la carpeta de claves de Data Protection. No repitas bootstrap ni enrolamiento MFA.
4. En SSMS, conectado a `.\SQLEXPRESS`, abre y ejecuta **completo** `database/etapa02/10_Password_Up.sql`. No requiere Modo SQLCMD.
5. Ejecuta completo `database/etapa02/11_Password_Verificar.sql`.

Esperado sobre la copia con ET02-003:

| Comprobación | Resultado |
|---|---|
| Versión | ET02-004 |
| Tablas dbo | 66 |
| Procedimientos dbo | 9 |
| Claves foráneas | 92 |
| FK deshabilitadas / sin validar | Ninguna fila |

Las tablas nuevas son `UsuarioTokenPassword` y `RecuperacionLimite`. La primera contiene solo el hash SHA-256 del token aleatorio, usuario, sello de seguridad, fechas de creación/vencimiento/consumo/revocación; tiene índice único de hash, índice por usuario/vencimiento y FK sin cascada a Usuario. El token tiene 256 bits aleatorios; SHA-256 se usa para ese token, **no** para las contraseñas. La segunda contiene el hash del correo normalizado y el contador de solicitudes en una ventana de 15 minutos.

No se modifican procedimientos financieros. El script es idempotente y transaccional. `12_Password_Down.sql` revierte únicamente antes de usar el bloque; se detiene si existen tokens, solicitudes, auditoría PASSWORD o versiones posteriores. Después del uso requiere reversión específica que conserve datos y auditoría. No ejecutar los scripts Down para probar los flujos normales.

## Compilar y abrir

```powershell
Set-Location 'C:\Users\BRYAN\source\repos\PortalMercado'
dotnet build PortalMercado.sln -c Debug
dotnet build PortalMercado.sln -c Release
dotnet run --project PortalMercado.Web --launch-profile https
```

En otra terminal, desde la misma carpeta:

```powershell
dotnet run --project PortalMercado.API --launch-profile https
```

Las terminales permanecen abiertas con Application started. Web: `https://localhost:7275`; Swagger: `https://localhost:7161/swagger`.

## Prueba de cambio de contraseña

Utiliza primero el vendedor ficticio para familiarizarte con el proceso:

1. Entra en Web con `vendedor.et02@example.invalid` y su contraseña actual privada.
2. Abre **Mi perfil → Cambiar contraseña**.
3. Escribe la contraseña actual y una nueva dos veces. La nueva debe tener 12–128 caracteres, mayúscula, minúscula, número y símbolo, y ser diferente.
4. Pulsa **Cambiar contraseña**. Aparece **Contraseña actualizada**; ya no conservas la sesión anterior.
5. Inicia sesión con la nueva contraseña. Conserva la clave de forma privada: no la compartas en capturas o mensajes.

La operación revoca todas las sesiones de la cuenta en API y MVC y los enlaces de recuperación/retos MFA anteriores. No elimina roles, asignaciones, secreto MFA ni códigos de segundo factor. Un administrador sigue necesitando su aplicación autenticadora o código MFA de recuperación al entrar de nuevo.

Cinco contraseñas actuales incorrectas acumulan el bloqueo temporal de 15 minutos y dejan de permitir la sesión. La pantalla no refleja las contraseñas introducidas. No hace falta probar manualmente el bloqueo con tu administrador; existe una prueba SQL con cuenta efímera.

## Prueba de recuperación con correo local

Los archivos `appsettings.Development.json` de ambos hosts incluyen explícitamente:

```json
"Recovery": {
  "Provider": "Pickup",
  "WebBaseUrl": "https://localhost:7275/"
}
```

**Pickup es salida local de pruebas:** escribe un archivo de correo `.eml`; no envía nada por Internet. No se registra como envío SMTP. Solo se permite en Development. La carpeta predeterminada de Windows es `%LOCALAPPDATA%\PortalMercado\CorreoPruebas`, fuera de la solución y de wwwroot. No se publican endpoints para descargar estos mensajes.

1. Cierra sesión en Web y pulsa **Olvidé mi contraseña**.
2. Escribe `vendedor.et02@example.invalid` y pulsa **Solicitar enlace**.
3. Aparece una respuesta genérica y el aviso del modo local. Esa respuesta no confirma si un correo pertenece a una cuenta.
4. En **otra ventana de PowerShell**, abre el último correo local:

```powershell
$carpetaCorreo = Join-Path $env:LOCALAPPDATA 'PortalMercado\CorreoPruebas'
$ultimoCorreo = Get-ChildItem -Path $carpetaCorreo -Filter '*.eml' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
notepad $ultimoCorreo.FullName
```

5. En el Bloc de notas, copia el enlace completo `https://localhost:7275/Cuenta/Restablecer?token=...` y ábrelo en el navegador. No compartas el enlace ni muestres el token en un video.
6. Escribe una nueva contraseña y repítela. Pulsa **Guardar nueva contraseña**.
7. Entra con esa contraseña nueva. La anterior ya no debe funcionar.
8. Vuelve a abrir el mismo enlace: la página puede mostrar el formulario, pero **al intentar guardar nuevamente** debe rechazar el token consumido. Abrir el enlace con GET nunca cambia la contraseña ni consume el token.

Cada enlace vence a los **15 minutos**. Solicitar otro invalida el anterior. Solo se procesan tres solicitudes por correo normalizado en 15 minutos, compartidas entre API y Web; las adicionales reciben la misma respuesta genérica sin generar otro correo. También existe un límite de tres peticiones por minuto/IP en cada host, que devuelve 429 con Retry-After. Los intentos de restablecimiento/cambio usan el límite auth existente.

Para evitar revelar la existencia de cuentas por el resultado del proveedor, se entrega el mismo tipo de correo a cada dirección válida solicitante. Si la cuenta no existe o está inactiva, el enlace contiene un valor aleatorio sin asociación SQL y no puede restablecer ninguna contraseña. El mensaje explica que el enlace solo funciona para una cuenta habilitada. No se activa ninguna cuenta mediante recuperación.

La carpeta local contiene enlaces privados. No la incluyas en Git, ZIP, evidencias ni capturas; elimina esos archivos locales cuando ya no los necesites. En Windows se utilizan los permisos heredados del perfil del usuario; en Unix los archivos se crean accesibles solo por su propietario. Si configuras PickupPath, debe ser una ruta absoluta privada fuera de la solución.

## Configurar SMTP

La interfaz `IRecoveryDelivery` permite sustituir el proveedor. La implementación SMTP incluida exige TLS, usuario, contraseña y remitente, y limita a 10 segundos el intento de entrega. En un entorno real configura ambos hosts con User Secrets durante desarrollo o un gestor de secretos/variables de entorno en despliegue:

| Clave | Valor |
|---|---|
| Recovery:Provider | Smtp |
| Recovery:WebBaseUrl | Origen público HTTPS de Web, terminado en /, sin ruta, consulta o credenciales |
| Recovery:Smtp:Host | Servidor SMTP contratado o de pruebas |
| Recovery:Smtp:Port | Puerto del proveedor con STARTTLS, normalmente 587 |
| Recovery:Smtp:User | Usuario del proveedor |
| Recovery:Smtp:Password | Contraseña o clave del proveedor, fuera del repositorio |
| Recovery:Smtp:From | Remitente autorizado |

No construir enlaces con el encabezado Host ni aceptar un origen desde el cliente. `WebBaseUrl` debe ser el origen de confianza definido por el operador. Los secretos SMTP no deben incluirse en appsettings versionados. Pickup está bloqueado fuera de Development; sin proveedor real configurado, recuperación devuelve 503 seguro, no una confirmación falsa. Los demás servicios pueden iniciar y funcionar sin SMTP.

Si falla la entrega (incluida la escritura del archivo local), se comunica un 503 seguro y se revoca el token creado. La auditoría registra el fallo sin excepción completa, datos de SMTP ni enlaces. No se afirma recepción en bandeja de entrada: SMTP solo confirma aceptación por el servidor. Esta versión prueba Pickup y fallos controlados; el envío contra un proveedor SMTP real debe validarse con sus credenciales en tu entorno.

## API

Los tres endpoints requieren antiforgery, incluso los públicos. Swagger lo obtiene mediante el interceptor existente. Login/MFA API es independiente de MVC.

| Método y ruta | Resultado |
|---|---|
| POST /api/auth/forgot-password | 202 con mensaje genérico; 400 validación; 429 límite IP; 503 proveedor/configuración no disponible |
| POST /api/auth/reset-password | 204 al cambiar; 400 token/contraseña inválidos; 429 límite |
| POST /api/auth/change-password | Autenticado: 204 y cierre de cookie; 400 datos inválidos; 401 sin sesión; 429 límite |

Forgot recibe `{ "correo": "vendedor.et02@example.invalid" }`. Reset recibe `token`, `newPassword`, `confirmPassword`. Change recibe `currentPassword`, `newPassword`, `confirmPassword`; la identidad se toma de la sesión, nunca de un IdUsuario enviado. No se devuelve ni imprime el token en las respuestas API. El enlace se entrega por el proveedor de correo.

`PortalMercado.API/Password.http` contiene plantillas sin contraseñas ni tokens reales. Restablecer no inicia sesión automáticamente. Las respuestas son NoStore y la pantalla del enlace utiliza Referrer-Policy: no-referrer. Las contraseñas siguen usando PasswordHasher adaptativo, no cifrado reversible.

## Pruebas

Detén API y Web con Ctrl+C. Sin SQL:

```powershell
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter 'Category!=SqlIntegration'
```

Completa, después de ET02-004:

```powershell
$env:PM_RUN_SQL_TESTS = '1'
try {
    dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --logger 'trx;LogFileName=pruebas-etapa02-password-sql.trx' --results-directory ./TestResults
} finally {
    Remove-Item Env:PM_RUN_SQL_TESTS -ErrorAction SilentlyContinue
}
```

Las pruebas SQL nuevas crean solo sus cuentas y datos de seguridad con nombres GUID y limpian sus filas al terminar. No envían correo a Internet: utilizan un capturador en memoria. Cubren hash del token, consumo único, caducidad, enlace anterior, cuentas desconocidas/inactivas, error de entrega, revocación de sesiones API/MVC, bloqueo por contraseña actual incorrecta, consumo concurrente y conservación de MFA.

Resultado esperado: **109 pruebas sin SQL** o **133 completas (109 + 24 SQL)**. PM_RUN_SQL_TESTS debe estar habilitada para ejecutar las 24 SQL, sin omitidas.

La evidencia incluida distingue lo ejecutado sin SQL de las pruebas pendientes en Windows. Conserva el resultado SQL nuevo en su propia carpeta sin reemplazar evidencias anteriores:

```powershell
New-Item -ItemType Directory -Force '.\docs\evidencias\etapa02-password' | Out-Null
Copy-Item '.\TestResults\pruebas-etapa02-password-sql.trx' '.\docs\evidencias\etapa02-password\pruebas-etapa02-password-sql.trx'
```

Las fechas de auditoría y vencimiento son UTC. No hay limpieza automática de auditoría o tokens históricos: su retención y depuración deben definirse antes del despliegue.
