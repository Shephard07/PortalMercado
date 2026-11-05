# PortalMercado

PortalMercado es un proyecto de plataforma comercial multinegocio para mercados de abastos. Su base técnica integra una aplicación web MVC, una API y acceso a SQL Server mediante una arquitectura por capas.

La versión actual permite consultar el estado de los servicios, verificar la conexión SQL, leer las entidades piloto con Entity Framework Core y ejecutar una consulta de saldos mediante un procedimiento almacenado. La Etapa 02 incorpora autenticación inicial, perfil y cierre de sesión. Incluye MFA administrativo TOTP, activación tras enrolamiento y códigos de recuperación. Incluye administración de usuarios, roles, permisos, asignaciones a mercados/negocios y selección del negocio activo. La recuperación utiliza enlaces HTTPS de un solo uso con vencimiento; el cambio de contraseña revoca sesiones anteriores y conserva MFA.

## Autenticación en desarrollo

La ampliación SQL de seguridad se prepara en `database/etapa02`; requiere revisión y aprobación antes de aplicarse. La pantalla de acceso está en `/Cuenta/Login`. Las sesiones MVC y API son independientes, vencen a los 30 minutos y se revalidan en SQL en cada solicitud.

Los administradores requieren MFA antes de recibir sesión. Configuración del bloque actual: [Recuperación y cambio de contraseña](docs/ETAPA02_BLOQUE04_PASSWORD.md). La [administración de usuarios y negocios](docs/ETAPA02_BLOQUE03_ADMINISTRACION.md) conserva su guía. El [MFA administrativo](docs/ETAPA02_BLOQUE02_MFA.md) conserva su guía de enrolamiento. La etapa completa aún no habilita uso productivo. La [autenticación inicial](docs/ETAPA02_BLOQUE01.md) se conserva como referencia del bloque anterior.

## Tecnologías

| Componente | Tecnología |
|---|---|
| Lenguaje y plataforma | C# / .NET 10 |
| Aplicación web | ASP.NET Core MVC, Razor y Bootstrap |
| API | ASP.NET Core Web API |
| Acceso a datos | EF Core SQL Server y ADO.NET |
| Documentación de API | OpenAPI y Swagger UI |
| Pruebas | xUnit y WebApplicationFactory |
| Base de datos | SQL Server 2019 o superior |

## Requisitos

- Windows para el entorno local con SQL Server Express y autenticación integrada.
- SDK .NET **10.0.401**. `global.json` admite parches de la misma banda y excluye versiones preliminares.
- Visual Studio 2026 con la carga de trabajo **Desarrollo de ASP.NET y web**, o un editor con soporte para .NET.
- SQL Server Express local con instancia `.\SQLEXPRESS` y SQL Server Management Studio (SSMS).

Para habilitar HTTPS en desarrollo:

```powershell
dotnet dev-certs https --trust
```

## Arquitectura

La solución `PortalMercado.sln` contiene seis proyectos:

| Proyecto | Responsabilidad |
|---|---|
| `PortalMercado.Domain` | Entidades y modelo de dominio |
| `PortalMercado.Application` | Interfaces, contratos y DTOs |
| `PortalMercado.Infrastructure` | DbContext, mapeos EF, conexión SQL y ejecución de procedimientos |
| `PortalMercado.API` | Endpoints técnicos, middleware y documentación OpenAPI |
| `PortalMercado.Web` | Controladores MVC, vistas Razor y recursos estáticos |
| `PortalMercado.Tests` | Pruebas de configuración, middleware, HTTP, MVC e integración SQL |

Application depende de Domain. Infrastructure implementa las interfaces de Application. API y Web utilizan Infrastructure para registrar los servicios, mientras que sus controladores trabajan con las interfaces de Application.

Las entidades piloto son **Mercado, Sector, Puesto, Negocio y Usuario**. Domain no depende de EF Core ni de ASP.NET Core.

Más detalles: [Arquitectura](docs/ARQUITECTURA.md) y [matriz SQL–C#](docs/MATRIZ_SQL_CSHARP.md).

## Instalación de la base local

El entorno de pruebas utiliza la base **`PortalMercado_ET01_Test`** en **`.\SQLEXPRESS`**, con autenticación de Windows.

En SSMS, conectar a esa instancia y ejecutar los archivos completos en el siguiente orden:

| Orden | Archivo | Función |
|---|---|---|
| 1 | `database/local/00_Preparar_Base_Local.sql` | Crear y preparar la base vacía |
| 2 | `database/local/01_Esquema_Local.sql` | Crear tablas, índices y restricciones |
| 3 | `database/local/02_Procedimientos_Local.sql` | Crear los procedimientos almacenados |
| 4 | `database/local/03_Verificar_Instalacion.sql` | Comprobar objetos y relaciones |
| 5 | `database/local/04_Datos_Prueba.sql` | Cargar o validar los datos ficticios |

**Activar Consulta → Modo SQLCMD en cada ventana de los archivos 00 a 03.** Estos utilizan `:ON ERROR EXIT` para detener la ejecución ante errores. El archivo 04 no requiere ese modo.

La línea base ET01 contiene **56 tablas, 9 procedimientos y 84 claves foráneas**. La ampliación ET02-001 añade cuatro tablas y tres FK. ET02-002 añade tres tablas MFA y tres FK: ET02-003 incorpora el contexto por sesión, el objetivo de auditoría y la trazabilidad de la semilla: ET02-004 añade dos tablas de recuperación: el laboratorio actualizado contiene 66 tablas, 9 SP y 92 FK. Se verifica con `database/etapa02/11_Password_Verificar.sql`. La cuenta de Windows necesita permisos para crear la base.

Si la base ya está instalada, ejecutar 03 para verificarla; no repetir los archivos 00, 01 y 02 sobre el esquema existente. El archivo 04 valida los datos ficticios existentes sin duplicarlos.

El export original se conserva en `database/original/Portal.sql`. Contiene rutas y configuración del servidor de origen; para instalar el entorno local deben utilizarse los scripts de `database/local`.

El modelo EF Core es parcial y trabaja sobre ese esquema existente. No deben utilizarse `EnsureCreated`, `Migrate` ni migraciones automáticas para generarlo o reemplazarlo.

## Configuración

Desde la carpeta que contiene `PortalMercado.sln`:

```powershell
dotnet restore PortalMercado.sln
dotnet user-secrets init --project PortalMercado.API
dotnet user-secrets init --project PortalMercado.Web
```

API y Web tienen archivos de secretos independientes. En Visual Studio, abrir **Administrar secretos de usuario** en cada proyecto y configurar:

```json
{
  "ConnectionStrings": {
    "PortalMercado": "Server=.\\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;"
  }
}
```

Esta conexión utiliza la cuenta de Windows y no requiere una contraseña SQL. `TrustServerCertificate=True` corresponde al entorno local de desarrollo. En producción se debe utilizar un certificado confiable con `TrustServerCertificate=False` y configurar la conexión mediante un gestor de secretos o la variable de entorno **`ConnectionStrings__PortalMercado`**.

La aplicación valida la presencia y la sintaxis de la cadena durante el arranque; la disponibilidad del servidor se comprueba por separado. Las credenciales no deben almacenarse en los archivos versionados.

### Ambientes y CORS

- **Development:** habilita Swagger y los endpoints de diagnóstico SQL. CORS autoriza `https://localhost:7275` para solicitudes GET.
- **Production:** Swagger y los diagnósticos SQL no están disponibles. CORS no autoriza orígenes de forma predeterminada; deben configurarse los correspondientes al despliegue.

Los perfiles locales `https` de `launchSettings.json` establecen el ambiente Development.

## Compilación y ejecución

```powershell
dotnet build PortalMercado.sln -c Debug
dotnet build PortalMercado.sln -c Release
```

Iniciar la API:

```powershell
dotnet run --project PortalMercado.API --launch-profile https
```

En otra terminal, iniciar la aplicación MVC:

```powershell
dotnet run --project PortalMercado.Web --launch-profile https
```

| Servicio | Dirección local |
|---|---|
| Aplicación web | https://localhost:7275 |
| API | https://localhost:7161 |
| Swagger UI | https://localhost:7161/swagger |

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/health` | Estado del proceso API; devuelve 200 |
| GET | `/api/health/database` | Conexión SQL; devuelve 200 o un 503 controlado |
| GET | `/api/health/database/mapping` | Ejecuta una consulta EF sobre Mercado |
| GET | `/openapi/v1.json` | Documento OpenAPI |

Los tres últimos endpoints solo están disponibles en Development. Las solicitudes de ejemplo se encuentran en **`PortalMercado.API/PortalMercado.API.http`**.

La consulta de mapeo puede ejecutarse sobre una tabla vacía; las pruebas de integración verifican además la materialización de las cinco entidades con datos ficticios.

### Autenticación API

| Método | Ruta | Función |
|---|---|---|
| GET | `/api/auth/csrf` | Preparar la protección antiforgery |
| POST | `/api/auth/login` | Iniciar sesión con cookie segura |
| POST | `/api/auth/logout` | Revocar la sesión actual (204) |
| POST | `/api/auth/mfa/setup` | Preparar enrolamiento con reto válido |
| POST | `/api/auth/mfa/verify` | Completar MFA y emitir sesión |
| GET | `/api/auth/me` | Consultar el perfil autenticado |

Ejemplos y manejo de cookies/CSRF: `PortalMercado.API/Etapa02.Auth.http` y `docs/ETAPA02_BLOQUE01.md`.

## Procedimientos almacenados

`IStoredProcedureExecutor` define el contrato de acceso y `StoredProcedureExecutor` implementa la llamada mediante ADO.NET, con parámetros SQL tipados.

La consulta implementada es **`dbo.pm_ConsultarSaldos`**, que recibe `@IdNegocio` y devuelve el identificador, nombre comercial, saldo contable, pendiente de liquidación e importe en liquidaciones abiertas. El resultado se representa mediante `SaldoNegocioDto` y no se publica en los endpoints técnicos de salud.

Esta consulta no registra cobros ni modifica movimientos financieros.

## Errores y observabilidad

API y Web utilizan logs JSON y agregan **`X-Correlation-ID`** a las respuestas. El mismo identificador se incorpora al ámbito de logging de la solicitud.

El middleware transforma las excepciones no controladas en respuestas **500 con ProblemDetails**, sin exponer mensajes internos ni credenciales. MVC también dispone de la vista `/Home/Error`, con código 500 y un identificador de seguimiento.

## Pruebas

La suite cubre configuración, manejo de errores, correlación, CORS, restricciones por ambiente, MVC y acceso a SQL Server. La Etapa 02 añade pruebas de política de credenciales, protección antiforgery, cookies, revocación y respuestas 401/403. El resultado del avance y sus límites se documentan en [Autenticación inicial](docs/ETAPA02_BLOQUE01.md); los resultados de ET01 no sustituyen la validación del código nuevo.

Detener API y Web con **Ctrl+C** antes de compilar o ejecutar pruebas para evitar que los ejecutables queden bloqueados.

### Pruebas sin SQL Server

```powershell
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "Category!=SqlIntegration"
```

### Suite completa

Preparar primero la base y los datos ficticios. Habilitar las pruebas SQL en la terminal:

```powershell
$env:PM_RUN_SQL_TESTS = "1"

try {
    dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --logger "trx;LogFileName=pruebas-etapa02-bloque01-sql.trx" --results-directory ./TestResults
}
finally {
    Remove-Item Env:PM_RUN_SQL_TESTS -ErrorAction SilentlyContinue
}
```

Las tres pruebas SQL de ET01 realizan lecturas locales. El bloque MFA añade cuatro pruebas con cuentas ficticias temporales y limpieza de sus propias filas; requieren ET02-002. Ver alcance y ejecución en `docs/ETAPA02_BLOQUE02_MFA.md`. Todas se limitan al laboratorio `.\SQLEXPRESS` y, sin `PM_RUN_SQL_TESTS=1`, se omiten explícitamente.

Las pruebas HTTP utilizan un servidor de pruebas y sustituyen el servicio de salud SQL para comprobar respuestas controladas sin acceder a la base remota.

Las pruebas nuevas de autenticación HTTP sustituyen IAuthService; su aprobación no acredita persistencia ni bloqueo en SQL real.

Los resultados se generan en **`TestResults`** en formato TRX. El mensaje de error provocado durante la prueba del middleware es esperado; el resultado final de la suite determina si la ejecución fue satisfactoria.

## Administración de identidades y negocios

Usuarios permite listar cuentas paginadas, crear usuarios con contraseña inicial, editar nombre/teléfono, desactivar/activar y administrar roles y asignaciones. Los administradores nuevos completan MFA antes de activarse. No se eliminan cuentas físicamente.

Roles y permisos presenta el catálogo y sus asignaciones; la modificación global exige un administrador general autorizado. Mis negocios permite seleccionar un negocio activo para la sesión. API y MVC vuelven a comprobar permisos, estado y ámbito en el servidor; las modificaciones de privilegios revocan sesiones anteriores.

Consulta la [matriz inicial de permisos](docs/MATRIZ_PERMISOS_ET02.md). La API incluye `/api/usuarios`, `/api/roles`, `/api/permisos` y `/api/negocios/contexto`; Swagger documenta DTOs y códigos de respuesta.

## Recuperación de acceso

El login ofrece Olvidé mi contraseña y el perfil permite cambiarla con la clave actual. Los enlaces de recuperación vencen a los 15 minutos; solicitar otro invalida el anterior. Los tokens solo se almacenan como hash en SQL. El restablecimiento no activa cuentas ni inicia sesión automáticamente y mantiene el segundo factor administrativo.

Development utiliza salida local de correo de pruebas, fuera de la solución. Para entrega por Internet se configura SMTP con TLS y secretos externos al repositorio. Los fallos de entrega se comunican mediante un error seguro y revocan el token; no se simula un envío correcto.

## Sesión y alcance administrativo

La Web muestra un aviso cuando recibe una sesión inválida y ofrece iniciar sesión de nuevo. El rol ADMIN_MERCADO se limita a mercados activos asignados y sus negocios activos. Una relación inactiva no concede acceso; las relaciones históricas se conservan para impedir que un administrador limitado modifique cuentas compartidas fuera de su ámbito.

Los contratos de escritura en Swagger incluyen ejemplos ficticios. En API, los cambios de estado y asignaciones deben indicar explícitamente `activo` o `ids`; omitir estos campos devuelve 400. Consulta la [matriz de aceptación](docs/MATRIZ_ACEPTACION_ET02.md) para las pruebas asociadas a cada función.
