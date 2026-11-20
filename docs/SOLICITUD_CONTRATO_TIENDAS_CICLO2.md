# Contrato propuesto de integración · Tiendas del Ciclo 2

Estado: propuesta para la integración central, pendiente de A/DBA.
El usuario autorizó posteriormente una adaptación SQL exclusivamente LOCAL;
su instalación se describe en GUIA_TIENDAS_SQL_LOCAL_CICLO2.md. Esa adaptación
no equivale a aprobación ni despliegue en la base central.
No contiene DDL ni procedimientos ejecutables. No cambia la configuración de la aplicación.

## Punto de partida verificado

- Base local revisada: PortalMercado_ET01_Test.
- Negocio dispone de Descripcion nvarchar(max), LogoUrl nvarchar(500),
  Telefono varchar(30), Correo varchar(254) y Horario nvarchar(500), todos opcionales.
- El índice UX_Negocio_MercadoSlug garantiza unicidad de (IdMercado, Slug).
- La consulta de metadatos no mostró una fuente identificable de visibilidad
  pública, consentimiento de contactos comerciales ni versión de Negocio.
- Las asignaciones físicas usan NegocioPuesto.Desde/Hasta, Puesto y Sector.
- Existen procedimientos del catálogo del Ciclo 1; no se ha recibido un
  contrato específico aprobado para persistencia de tiendas.
- Evidencia comunicada: 331 pruebas correctas, 0 errores, 0 omitidas.
  Esto incluye 148 casos de Tiendas con proveedores simulados/controlados.

## Operaciones solicitadas

Los nombres siguientes son identificadores propuestos del contrato; no se
presupone su existencia en SQL. A/DBA podrá resolverlos mediante vistas,
consultas parametrizadas o procedimientos y comunicar los nombres definitivos.

| Operación | Parámetros | Salida y regla |
| --- | --- | --- |
| OpcionesPublicas | Sin filtros | Mercados y categorías de tiendas visibles; categorías con productos PUBLICADO. |
| ListarPublicas | MercadoId bigint?, CategoriaId bigint?, Buscar nvarchar(180)?, Pagina int, Tamano int | Total y página. Aplicar visibilidad y filtros antes de contar/paginar. Orden por NombreComercial e IdNegocio. |
| BuscarPorSlug | Slug varchar(160) | Hasta dos negocios coincidentes, con indicadores de visibilidad. Cero, oculto o ambiguo se traducen a 404 público uniforme. |
| UbicacionesVigentes | IdNegocio bigint, Fecha date | Mercado, sector, puesto y referencia pública, con indicadores de estado y Desde/Hasta para comprobar vigencia. |
| CatalogoPublico | IdNegocio bigint, CategoriaId bigint?, Buscar nvarchar(200)?, Orden varchar(20), Pagina int, Tamano int | Total, página de productos PUBLICADO del negocio y categorías propias sin paginar. |
| PerfilObtener | IdNegocio bigint, actor/contexto según contrato de seguridad | Perfil comercial, consentimiento de contactos, versión y metadata del último cambio. Nunca otros negocios. |
| PerfilGuardar | IdNegocio bigint, Actor bigint, VersionEsperada, campos comerciales, Correlacion varchar(100) | Comparación de versión, actualización limitada, nueva versión y auditoría en una transacción. Sin éxito parcial. |

Paginación: página desde 1 hasta 100000; tamaño desde 1 hasta 100.
Búsqueda nula significa no filtrar; filtros parametrizados, sin concatenar SQL.
Orden de productos permitido: nombre, precio-asc y precio-desc; desempate por IdProducto.
Los totales deben estar disponibles aunque una página no tenga filas.

## Proyecciones C# que deben poder construirse

| Contrato de Application | Datos necesarios |
| --- | --- |
| TiendaLectura | IdNegocio, Slug, NombreComercial, IdMercado, Mercado, Estado, MercadoActivo, VisibilidadAprobada, Descripcion, LogoUrl, Horario, Telefono, TelefonoPublico, Correo, CorreoPublico, CategoriasPublicadas. |
| UbicacionTiendaLectura | IdNegocio, IdMercado, IdPuesto, Mercado, Sector, Puesto, Referencia, MercadoActivo, SectorActivo, PuestoHabilitado, Desde, Hasta. |
| ProductoTiendaLectura | IdNegocio y Estado, más IdProducto, categoría, nombre, descripción, precio vigente, unidad, ControlaStock, Disponible e ImagenPrincipal. |
| PerfilTienda | IdNegocio real, NombreComercial, Slug, indicadores de contacto, datos editables, versión y último cambio. |

Los indicadores son conceptos del contrato, no columnas que B creará.
CategoriasPublicadas puede provenir de otro resultado relacionado; no exige
una columna de arreglo en SQL Server. Se excluirán datos fiscales y de
identidad del comerciante de los DTO públicos.

## Decisiones que deben quedar explícitas

1. **Visibilidad:** indicar la fuente aprobada de publicación de cada tienda.
   ACTIVO y mercado activo son necesarios, pero no sustituyen esa aprobación.
   La ausencia de aprobación mantiene oculta la tienda.
2. **Contactos:** indicar la fuente independiente que autoriza teléfono y correo
   públicos. Un valor guardado no implica consentimiento. ClienteConsentimiento
   no se reutiliza automáticamente como autorización de un negocio.
3. **Slug:** mantener por ahora el índice existente y rechazar ambiguos.
   Si se exige unicidad global, el DBA debe revisar datos e índice, o aprobar
   una ruta con mercado. B no renombra negocios ni cambia el índice.
4. **Vigencia:** confirmar zona America/Lima y Desde/Hasta inclusivos.
   Informar cuáles estados de Puesto se consideran habilitados.
5. **Precio e inventario:** confirmar regla de vigencia de precios, redondeo,
   precisión y selección determinista de imagen principal. Disponible es
   Existencia menos Reservado cuando ControlaStock; sin control, es null.
6. **Concurrencia:** definir el token persistente y todos los escritores que lo
   actualizan. Actualmente el comando C# usa Guid. Si se acuerda rowversion,
   se adaptará explícitamente el DTO/transporte; no se convertirá a Guid mediante
   truncamiento ni se confiará en una versión generada solo en memoria.
7. **Permisos:** acordar permisos específicos de lectura/edición de perfil y su
   asignación por rol/negocio. PRODUCTO_GESTIONAR es provisional en demostración.
8. **Campos de contacto:** confirmar que Telefono y Correo son contactos del
   perfil comercial y que el usuario del negocio puede editarlos.

## Escritura permitida y auditoría

El comando solo acepta Descripcion, LogoUrl, Horario y, cuando esté autorizado,
Telefono y Correo. No cambia Slug, Estado, IdMercado, IdComerciante, RUC ni
RazonSocial. Tampoco modifica consentimiento público ni asignaciones de puestos.

Límites de aplicación actuales: descripción 4000 caracteres, URL 500, horario
500, teléfono 30 y correo 254. La descripción conserva el límite de aplicación
aunque la columna sea MAX. Logo HTTPS sin credenciales, correo válido y texto
codificado al renderizar; no se descargan imágenes en el servidor.

PerfilGuardar debe:

1. Validar la identidad del actor y su alcance según el contrato de seguridad.
2. Comprobar que el registro pertenece al negocio recibido y está disponible
   para edición según la regla aprobada.
3. Comparar la versión dentro de la transacción. Si no coincide, devolver conflicto.
4. Actualizar únicamente la lista de campos permitidos y renovar la versión.
5. Insertar auditoría y confirmar todo junto. Si falla la auditoría, revertir el cambio.

Auditoría propuesta: evento TIENDA_PERFIL_ACTUALIZADO, actor, negocio, entidad,
registro, fecha UTC y correlación. El DBA acordará su correspondencia con
IdUsuario/IdNegocio/Entidad/IdRegistro/Accion/Fecha/Detalle existentes.
No registrar contraseñas, tokens de sesión, cadenas de conexión ni valores
completos de contactos o URLs. La correlación se genera en servidor.

Los permisos SQL de la cuenta de aplicación se limitarán a las operaciones
aprobadas; B no modifica credenciales ni concede permisos.

## Traducción de resultados al servicio

| Condición | Respuesta HTTP |
| --- | --- |
| Parámetros inválidos | 400 con mensaje controlado. |
| Sin sesión | 401 en API; login en MVC. |
| Sin permiso o fuera del negocio | 403. |
| Tienda inexistente, oculta o slug ambiguo | 404 público uniforme. |
| Perfil inexistente dentro del alcance autorizado | 404 privado. |
| Versión antigua | 409; ningún cambio ni evento de éxito. |
| Contrato no instalado o fallo de integración | 503 genérico, sin detalles SQL al cliente. |

El DBA debe comunicar códigos de resultado o números de error estables.
El adaptador no clasificará errores comparando mensajes SQL en texto.

## Entrega solicitada a A/DBA

- Fuente de visibilidad, consentimiento y regla de estado/vigencia aprobadas.
- Contrato por operación: nombre, parámetros, tipos, resultados, orden de
  columnas o nombres de columnas y códigos de error estables.
- Regla de concurrencia y transacción de actualización/auditoría.
- Configuración segura de conexión y permisos de ejecución, sin secretos en Git.
- Datos de prueba propios/ajenos, tienda visible/oculta, slug ambiguo,
  contacto privado/público, stock cero, sin logo y versión antigua.

Hasta recibir ese contrato se conserva el repositorio simulado, como permite
la sección 2 de los requerimientos de Ciclo 2. La integración SQL no se presenta
como terminada. El entregable contempla listar las solicitudes pendientes al DBA.

## Adaptación local autorizada

El usuario confirmó expresamente que desea completar también la parte SQL en
su copia local al trabajar individualmente. Se implementa en
PortalMercado_ET01_Test con guardas de base/instancia/equipo y opt-in de
Development. Los scripts se entregan separados en database/ciclo2-local y no
se ejecutan desde Web/API. Se utilizan los campos comerciales existentes,
una tabla auxiliar de publicación/versiones y la tabla Auditoria existente.

La adaptación conserva estos contratos C# y el permiso provisional existente.
La versión Guid ahora persiste en SQL y se renueva mediante trigger para
incluir cambios de otros escritores. El índice original por mercado/slug se
conserva; los slugs ambiguos siguen sin publicarse. Detalles y límites se
encuentran en GUIA_TIENDAS_SQL_LOCAL_CICLO2.md. La integración central de
compañeros permanece pendiente y deberá revisar estas decisiones locales.
