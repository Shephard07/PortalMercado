# Verificación del primer bloque de Etapa 02

Fecha: 2026-11-03. Entorno de comprobación: Linux, SDK .NET 10.0.401.

- Debug: correcto, cero errores y advertencias.
- Release: correcto, cero errores y advertencias.
- Suite sin SQL: 41 aprobadas, cero fallidas, cero omitidas dentro del filtro.
- El filtro Category!=SqlIntegration excluye las tres pruebas SQL de ET01; no se declaran aprobadas en este entorno.
- Las pruebas de autenticación HTTP usan IAuthService controlado: no acreditan bloqueo, concurrencia ni revocación contra SQL Server real.
- No se ejecutó ningún script SQL ni se accedió a bases locales/remotas del usuario.
- Los seis scripts SQL de la línea base permanecen idénticos byte a byte al ZIP revisado ET01.
- Recorrido manual MVC/Swagger, ampliación SQL, bootstrap y login real: pendientes de comprobar en Windows/SQLEXPRESS después de aprobación.

Este avance no completa los criterios de aceptación de la Etapa 02. MFA y administración por permisos siguen pendientes; los administradores no pueden iniciar sesión en este bloque.
