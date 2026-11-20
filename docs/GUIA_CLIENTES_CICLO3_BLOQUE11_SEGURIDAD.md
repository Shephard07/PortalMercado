# Bloque 11 — Detalles de seguridad y comprobación de paginación

Fecha: 20/11/2026. Aplicar después de los bloques 01–10. Código preparado; falta compilar y ejecutar en la PC del desarrollador. No se dispone de SDK dotnet ni SQL Server en el entorno de preparación.

## Qué cambia

- Cambio de contraseña autenticado en demostración mediante las pantallas y el contrato IPasswordService existentes. Verifica contraseña actual y política existente; revoca sesiones MVC/API del mismo almacén y enlaces pendientes.
- Recuperación temporal con el transporte Pickup ya existente: mensaje EML local, enlace aleatorio de 43 caracteres, almacenamiento de su hash, caducidad de 15 minutos y consumo único. Solicitar otro invalida el anterior. Fallar al entregar revoca el generado.
- Cuenta desconocida: misma respuesta y mismo tipo de mensaje, pero enlace sin asociación. Límite compartido por correo de tres solicitudes/15 minutos, además de la limitación HTTP existente.
- Cambio de correo desde perfil: exige sesión y contraseña actual; confirma mediante enlace destinado al nuevo correo ficticio y POST con CSRF. GET no cambia datos. Conserva IdUsuario, perfil, direcciones e historial; al confirmar revoca sesiones/enlaces y exige login con el nuevo correo.
- Enlaces de password y correo separados por propósito y propietario. Una cuenta no puede confirmar el enlace de otra. Revalida unicidad dentro del bloqueo de memoria al confirmar.
- Reutiliza RecoveryDelivery para el transporte de mensajes; no incorpora otro algoritmo de hash, generador de token, login, pantalla de reset ni transporte SMTP alternativo.
- Etiquetas de autofill de dirección para navegación móvil.
- Prueba HTTP de ida/vuelta por los enlaces reales de paginación conservando búsqueda, mercado, categoría, tipo, orden y tamaño.

## Aplicación

Copiar las carpetas del ZIP en la raíz de PortalMercado, reemplazando los archivos incluidos. No borrar archivos propios. Si hay cambios preparados en Git, volver a ejecutar git add sobre estos archivos después de aplicar el ZIP.

Este bloque sí cambia código: las 701 pruebas anteriores acreditan el estado previo. Se añaden 32 casos (26 de servicio, 5 de HTTP/entrega, 1 de paginación). Esperado si están todos los bloques: Clientes 383, solución 733; son expectativas, no resultados ejecutados aquí.

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Clientes" --logger "trx;LogFileName=pruebas-ciclo3-clientes-bloque11-seguridad.trx"
```

Regresión recomendada después de que pase Clientes, porque se amplió el transporte compartido de correo:

```bash
PM_RUN_SQL_TESTS=1 \
PM_RUN_CATALOGO_SQL_TESTS=1 \
PM_RUN_PRODUCTOS_SQL_TESTS=1 \
PM_RUN_CATALOGO_FIXTURE_TESTS=1 \
PM_RUN_CATALOGO_PAGINACION_TESTS=1 \
PM_RUN_TIENDAS_SQL_TESTS=1 \
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --logger "trx;LogFileName=pruebas-ciclo3-solucion-bloque11-sql-local.trx"
```

## Iniciar la demostración con correo local

Detener la ejecución anterior con Ctrl+C y relanzar; se borrarán los clientes temporales, por lo que hay que registrar nuevamente una cuenta ficticia. No se cambia configuración permanente ni se envía correo por Internet:

```bash
Clientes__Demostracion=true Recovery__Provider=Pickup Recovery__WebBaseUrl=https://localhost:7275/ Tiendas__SqlLocal=true Tiendas__Demostracion=false Tiendas__NegocioLocalDemostracion= dotnet run --project PortalMercado.Web --launch-profile https
```

En Windows, Win+R y abrir:

```text
%LOCALAPPDATA%\PortalMercado\CorreoPruebas
```

Cada solicitud genera un archivo .eml. Abrir el más reciente con Bloc de notas (Abrir con), comprobar destinatario/asunto y copiar su enlace HTTPS al mismo navegador. No se expone el token en la página de éxito ni mediante un buzón web público. No subir estos archivos a Git ni incluir enlaces/tokens en capturas o videos. Si el archivo no llega, revisar configuración y mensaje; nunca afirmar entrega exitosa ante un fallo.

La ruta puede cambiar con Recovery:PickupPath, siempre absoluta y fuera de la solución/wwwroot. Usar una carpeta de pruebas controlada. Pickup solo funciona en Development; en la demostración se rechaza Smtp incluso si existen credenciales SMTP configuradas.

## Prueba manual, en este orden

1. Registrar cliente1@example.invalid con contraseña fuerte y entrar.
2. Mi perfil → Cambiar contraseña. Probar contraseña actual incorrecta una vez; después la correcta y una nueva. Debe cerrar sesión, rechazar la anterior y aceptar la nueva.
3. Cerrar sesión → Recuperar contraseña. Solicitar enlace para cliente1@example.invalid; abrir el EML y restablecer. Probar el enlace otra vez: debe rechazarse. Entrar con la nueva contraseña.
4. Perfil de cliente → Cambiar correo de acceso. Pedir cliente1-nuevo@example.invalid y repetirlo, usando la contraseña actual. El correo anterior sigue vigente hasta confirmar.
5. Abrir el EML destinado al nuevo correo en el mismo navegador con la sesión original. El GET muestra confirmación; solo al pulsar Confirmar cambia el correo y cierra sesiones. Entrar con el nuevo correo y comprobar que conserva direcciones/perfil.
6. A 390 × 858 revisar Registro, Agregar dirección, Cambiar correo y contraseñas. Con Tab/Shift+Tab/Enter comprobar orden, foco visible, errores y envío. La inspección estática no sustituye esta comprobación real.
7. Buscar Productos con suficiente cantidad, aplicar mercado/categoría y recorrer Siguiente/Anterior. Comprobar filtros y productos. Hay cobertura HTTP nueva, pero la captura manual sigue pendiente.

No compartir el contenido EML, tokens ni contraseñas. Para capturas, usar únicamente mensajes finales, formularios sin valores secretos y datos ficticios.

## Límites y pendientes

- Cliente, sesiones y tokens permanecen en memoria; un reinicio invalida enlaces aunque su EML siga en la carpeta. Web/API tienen almacenes separados. Probar este recorrido en Web; un enlace emitido desde API no recupera una cuenta creada en otro proceso Web. La API conserva los endpoints de password existentes para su propio almacén.
- La confirmación del cambio de correo es una prueba local de posesión del archivo, no una verificación externa del buzón. EmailConfirmado no se marca true. Confirmación inicial de registro no habilitada; su política/configuración y contrato aprobado siguen pendientes.
- Fuera de demostración el cambio de correo devuelve 503 hasta disponer del contrato de identidad aprobado. Autenticación, MFA y recuperación SQL anteriores conservan sus servicios.
- No se añaden DDL, SP, índices, permisos ni adaptadores SQL de clientes. Tampoco se modifica la consulta global para inventar mercados/categorías que los contratos actuales no devuelven.
- Documentos legales de demo siguen siendo ficticios. No se afirma aceptación final del ciclo.
