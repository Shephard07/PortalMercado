# Matriz inicial de permisos y ámbitos

Semilla ET02-003. Las asignaciones personalizadas permanecen; esta tabla describe los valores iniciales añadidos si faltan. La semilla no se reaplica después de registrar la versión.

| Permiso | ADMIN_GENERAL | ADMIN_MERCADO | PROPIETARIO_NEGOCIO | VENDEDOR |
|---|:---:|:---:|:---:|:---:|
| USUARIO_VER | Sí | Sí | Sí | — |
| USUARIO_CREAR | Sí | Sí | Sí | — |
| USUARIO_EDITAR | Sí | Sí | Sí | — |
| USUARIO_DESACTIVAR | Sí | Sí | Sí | — |
| ROL_ASIGNAR | Sí | Sí | Sí | — |
| PERMISO_GESTIONAR | Sí | — | — | — |
| NEGOCIO_USUARIO_ASIGNAR | Sí | Sí | Sí | — |
| MERCADO_USUARIO_ASIGNAR | Sí | Sí | — | — |
| ROL_VER | Sí | Sí | Sí | — |
| PERMISO_VER | Sí | Sí | Sí | — |
| NEGOCIO_CONTEXTO | Sí | Sí | Sí | Sí |

| Rol | Ámbito efectivo | Roles que puede asignar |
|---|---|---|
| ADMIN_GENERAL | Global | Roles existentes, excepto sobre su propia cuenta |
| ADMIN_MERCADO | Mercados activos asignados y sus negocios activos | PROPIETARIO_NEGOCIO, VENDEDOR |
| PROPIETARIO_NEGOCIO | Negocios activos con asociación activa y mercado activo | VENDEDOR |
| VENDEDOR | Negocios autorizados activos para contexto | Ninguno |

El permiso por sí solo no basta: el backend comprueba rol de administración, ámbito y estado. Un vendedor no obtiene administración de identidades aunque se le conceda accidentalmente USUARIO_EDITAR. El administrador general también necesita los permisos explícitos; el rol no sustituye esa verificación.

Las cuentas de ámbitos distintos no se listan. Para modificar una identidad global se exige contener **todas** sus asociaciones, incluso históricas inactivas; las cuentas compartidas quedan fuera de la administración limitada. No se puede editar la propia cuenta desde estas pantallas, cambiar un rol que el actor posee, otorgar permisos que no tiene, ni modificar el último administrador general activo. Administrar permisos globales exige ADMIN_GENERAL.

MFA obligatorio en roles cuyo código comienza por ADMIN_. Una cuenta nueva o promovida sin MFA queda inactiva y pendiente de enrolamiento. No existe activación manual que omita la verificación del segundo factor.

API y MVC validan la sesión contra SQL en cada solicitud. Los permisos se consultan otra vez y los cambios de privilegios rotan sellos; no requieren reiniciar la aplicación.
