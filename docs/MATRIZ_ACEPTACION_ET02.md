# Matriz de aceptación · PortalMercado Etapa 02

Referencia: *PortalMercado · Requerimiento de desarrollo, Etapa 02*, versión 1.0, 26/10/2026. Alcance: identidad, acceso y administración. El laboratorio verificado es la copia local `PortalMercado_ET01_Test`, no el servidor remoto.

| Requisito | Implementación y verificación |
|---|---|
| RF02-01 · Inicio | Web y API validan estado/credenciales, dan error genérico y registran último acceso solo al emitir sesión completa, después de MFA cuando corresponde. |
| RF02-02 · Cierre | Revoca sesión SQL y elimina cookie. No se usan refresh tokens. La Web muestra aviso específico si recibe una sesión inválida. |
| RF02-03 · Usuarios | Alta administrativa, listado paginado, edición de nombre/teléfono y activación/desactivación. DTO de salida sin hash. |
| RF02-04 · Hash | `PasswordHasher<Usuario>` adaptativo. Correo ASCII normalizado con índice único; collation documentada en bloque 01. |
| RF02-05 · Recuperación | Enlace HTTPS, 15 minutos, consumo único, hash en SQL y revocación de sesiones. Prueba local con Pickup. SMTP configurable sin afirmar que hubo envío real. |
| RF02-06 · Roles y permisos | Asignaciones usuario–rol y rol–permiso con reemplazo explícito; se bloquea editar roles propios o elevarse. Semilla idempotente. |
| RF02-07 · Ámbitos | Usuarios asociados a mercados y negocios; autorización del recurso en servidor, con pertenencia activa. `ADMIN_MERCADO` queda dentro de mercados asignados. |
| RF02-08 · Contexto | Selección por sesión/host entre negocios autorizados; no amplía permisos. |
| RF02-09 · API | 401/403, cookies seguras y antiforgery para escrituras. DTO, ProblemDetails, ejemplos OpenAPI y solicitudes `.http`. |
| RF02-10 · Auditoría | Accesos, bloqueos, recuperación y cambios administrativos con actor/objetivo cuando aplica, UTC y correlación. Consulta segura `13_Evidencias_ET02.sql`. |
| RF02-11 · MFA | TOTP administrativo con enrolamiento, recuperación de un solo uso y conservación del segundo factor tras restablecer contraseña. |
| RF02-12 · Bootstrap | Comando de aprovisionamiento de un solo uso, contraseña elegida por operador, cuenta administrativa sin sesión hasta MFA. |

| Criterio | Evidencia disponible | Verificación de cierre |
|---|---|---|
| CA02-01 · Login genérico | Web/API probados, `AccessHttpTests` y pruebas MFA | Suite final; grabar inicio válido |
| CA02-02 · Cuenta desactivada | Revocación en `DeactivationAndAssignmentsInvalidatePreviousSession`; rechazo en `SqlAuthService` | Suite final y prueba con una cuenta ficticia desactivada |
| CA02-03 · Hash y salida segura | `OwnerCreatesOnlyOwnSellerWithSafeHashAndAudit`; pruebas de hash y DTO/OpenAPI | Suite final |
| CA02-04 · Token vencible/de un uso | Recuperación manual local; `PasswordSqlTests` cubre vencimiento, reutilización y concurrencia | Suite final; no mostrar tokens |
| CA02-05 · Aislamiento A/B | `OwnerListAndIdLookupExcludeForeignAndSharedUsers`; rechazo de edición/asignación por ID en SQL; casos nuevos de mercado ajeno y asociación inactiva | Ejecutar casos SQL de la revisión final |
| CA02-06 · Vendedor sin privilegios | Denegación Web capturada; `SellerCannotEditAndOwnerCannotAssignForeignBusinessOrElevateSelf`; permisos restringidos | Grabar rechazo con vendedor |
| CA02-07 · HTTP 401/403 | Pruebas HTTP de ausencia de sesión y autorización, controles de alcance en servicio | Demostrar 401 anónimo y 403 como vendedor en el endpoint real |
| CA02-08 · Persistencia SQL | ET02-003 aplicada; 106 pruebas del bloque de administración; asignación y contexto probados | Suite final, incluida selección entre dos negocios |
| CA02-09 · Auditoría | Pruebas SQL validan actor/objetivo y eventos de recuperación | Captura de `13_Evidencias_ET02.sql` |
| CA02-10 · MFA | Enrolamiento de administrador y acceso con TOTP comprobados; SQL prueba recuperación/reutilización | Grabar transición a segundo factor sin mostrar códigos |
| CA02-11 · Build y pruebas | Las 133 pruebas del bloque 04 se reportaron correctas en Windows, 0 omitidas | Confirmar Debug/Release y suite completa de **esta revisión** |
| CA02-12 · Migraciones reversibles | ET02-001 a ET02-004 aplicadas; 66 tablas, 9 SP, 92 FK, sin FK inválidas. Scripts Down con guardas y línea base financiera preservada | Ensayar rollback en base desechable según `REVERSIBILIDAD_ETAPA02.md` |

Los casos de pruebas SQL crean datos ficticios temporales; no requieren operar con datos comerciales. Los controles de aislamiento se prueban en el servicio SQL y el tratamiento HTTP en pruebas de integración; el video añade una comprobación del endpoint real.

El correo local reproduce la entrega al proveedor de pruebas admitido por el requerimiento. No equivale a validar SMTP ni a aprobar un despliegue.