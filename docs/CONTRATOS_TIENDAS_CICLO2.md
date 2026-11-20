# Tiendas del Ciclo 2

Estado vigente: adaptación SQL local autorizada y ejecutada por el usuario.
Tras la corrección de EF/trigger, la suite completa comunicada el 13 de noviembre
terminó con 350 pruebas correctas, cero errores y cero omitidas. El archivo real
pruebas-ciclo2-solucion-sql-local.trx se conserva en el equipo Windows del usuario.
La entrega no se declara cerrada: revisar el contraste de requisitos al final.
Las secciones por bloque documentan la evolución y sus decisiones anteriores.

## Bloque 01

Base de Application e Infrastructure para consulta pública, con datos ficticios y
pruebas de servicio. El directorio aplica visibilidad antes del total y la
paginación. La ficha publica contactos únicamente con aprobación explícita.
El Bloque 01 se verifica desde las pruebas. Las pantallas y endpoints iniciales
están descritos en la sección del Bloque 02. La edición privada y el adaptador
SQL continúan pendientes.

## Contrato de lectura

| Operación | Entrada | Resultado y condiciones |
| --- | --- | --- |
| ListarPublicasAsync | MercadoId y CategoriaId opcionales, Buscar de hasta 180 caracteres, Pagina de 1 a 100000, Tamano de 1 a 100 | Página y total de negocios ACTIVO, mercado activo y visibilidad aprobada. Filtros antes de paginar. Orden por nombre e ID. Categoría corresponde a un producto PUBLICADO del negocio. |
| BuscarPorSlugAsync | Slug normalizado de hasta 160 caracteres | Hasta dos coincidencias, incluidas las ocultas, para detectar duplicados. Comparación completa. |
| ITiendaService.ObtenerAsync | Slug con letras ASCII, números y guiones simples | Ficha pública o 404 genérico para inexistencia, invisibilidad y ambigüedad. Esta sintaxis es provisional hasta confirmar el contrato de slug. |

La proyección interna contiene indicadores explícitos de visibilidad y publicidad
de contactos. El proveedor deberá obtenerlos de una fuente aprobada. Estos
indicadores son parte del contrato C# y de los datos simulados. Su incorporación
no supone nuevos campos SQL.

TiendaResumen y TiendaPublica enumeran los campos de salida y omiten RUC,
DirecciónFiscal e IdComerciante. El teléfono y correo de la ficha son nulos cuando
carecen de aprobación pública. Las vistas deberán codificar el texto. El logo
admite URL HTTPS bien formada de hasta 500 caracteres, sin credenciales en URL.
Una URL descartada se representa como nula para que la vista muestre un recurso
de reemplazo. La carga de archivos queda pendiente de definir con el proveedor
autorizado.

## Datos simulados

RepositorioTiendasDemostracion contiene dos tiendas visibles, en dos mercados,
y cinco casos ocultos por estado, visibilidad o mercado inactivo. Los IDs 1001 y
1002, mercados 100 y 101 y categorías 1 y 2 pertenecen al conjunto ficticio en
memoria y no representan registros de la base local del Ciclo 1.

El proveedor simulado se instancia desde las pruebas. Su uso en Web/API deberá
habilitarse explícitamente en Development y mostrarse como demostración.
Su comportamiento no acredita integración con SQL Server.

## Dependencias por resolver

| Dependencia | Decisión requerida |
| --- | --- |
| Slug | La base actual garantiza unicidad por mercado, mientras la ruta propuesta usa solo slug. Confirmar unicidad global o un cambio aprobado de ruta. Por ahora, un slug ambiguo devuelve 404. |
| Visibilidad | Confirmar fuente que determina visibilidad aprobada, además del estado ACTIVO y mercado activo. |
| Contactos | Confirmar consentimiento público para teléfono y correo. Sin aprobación, el servicio devuelve nulo. |
| Ubicación | A/DBA proporcionan mercado, sector y puestos vigentes, reglas de estado y límites Desde/Hasta, con fecha y zona horaria acordadas. |
| Catálogo | Lectura por IdNegocio y PUBLICADO, con precio vigente, imagen principal, unidad, disponibilidad, filtros, orden y paginación. |
| Perfil editable | Aprobar campos y longitudes. Descripcion, LogoUrl y Horario son base del alcance. Teléfono y correo dependen de autorización. |
| Escritura y auditoría | Contrato que valide el actor, ámbito, campos permitidos y persistencia atómica del cambio con su auditoría. Definir edición concurrente. |
| Permisos | Definir permisos para consultar y editar perfil privado, y su asignación por rol y negocio. |
| Imágenes | Proveedor externo autorizado, reglas de URL y política de almacenamiento. |

No hay modificaciones de SQL en este bloque. El documento de Ciclo 2 reserva al
DBA los cambios de tablas, SP, índices y permisos. Los contratos pendientes se
implementarán mediante adaptadores cuando estén disponibles.

## Verificación

Desde la raíz de la solución en Git Bash:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Tiendas" --logger "trx;LogFileName=pruebas-ciclo2-tiendas-bloque01.trx"
```

Estas pruebas usan datos en memoria y no requieren habilitar pruebas SQL.
Comprueban visibilidad, 404 uniforme, normalización, filtros y paginación,
contactos privados, ambigüedad de slug y descarte de logos inseguros.
La compilación y ejecución se deben confirmar en el equipo con el SDK del proyecto.

## Rama de desarrollo

Desde la rama del Ciclo 1 ya publicada y con el árbol de trabajo limpio:

```bash
git switch programador-B/Catalogo
git switch -c feature/b-tiendas-ciclo2
```

Crear la rama antes de copiar este bloque sobre la solución. Si la rama ya
existe, seleccionarla mediante `git switch feature/b-tiendas-ciclo2`.

## Bloque 02 · Directorio y ficha navegables

El Bloque 01 se compiló en el equipo de desarrollo con 26 pruebas aprobadas, cero
errores y cero omitidas. El Bloque 02 conecta el servicio a MVC y API, agrega
opciones de filtro procedentes de tiendas visibles, un layout público y pruebas
HTTP que requieren ejecutar la aplicación de pruebas.

| Método y ruta | Comportamiento |
| --- | --- |
| GET `/tiendas` | Directorio público con q, mercadoId, categoriaId, pagina y tamano. |
| GET `/tiendas/{slug}` | Información comercial pública de la tienda. |
| GET `/api/tiendas` | Página JSON del directorio con los mismos filtros. |
| GET `/api/tiendas/{slug}` | DTO de ficha pública. |

Las rutas de consulta admiten visitantes anónimos. Una tienda oculta o inexistente
responde 404. Los filtros inválidos responden 400. Los hosts registran el módulo
mediante AddTiendas. Sin un proveedor habilitado, las consultas responden 503 y
el resto del sistema continúa disponible.

Para habilitar los datos ficticios, usar `Tiendas:Demostracion=true` exclusivamente
en Development. El registro rechaza esa opción en otros ambientes. La Web muestra
la indicación «Demostración · Tiendas y contactos ficticios». La API también usa
el proveedor ficticio si se inicia con esa opción.

Desde Git Bash, iniciar Web:

```bash
Tiendas__Demostracion=true ConnectionStrings__PortalMercado='Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;' dotnet run --project PortalMercado.Web --launch-profile https
```

Abrir `https://localhost:7275/tiendas`. El directorio muestra Abarrotes Lima y
Frutería El Sol. Sus IDs son ficticios y pertenecen al repositorio en memoria.
Seleccionar el mercado Norte o la categoría Abarrotes debe devolver únicamente
Abarrotes Lima. Una búsqueda sin coincidencias muestra un estado vacío.

Para probar paginación con las dos tiendas, abrir
`https://localhost:7275/tiendas?q=a&tamano=1` y pulsar Siguiente.
La búsqueda y tamaño se conservan en el enlace.

Las fichas están en `/tiendas/fruteria-el-sol` y `/tiendas/abarrotes-lima`.
La primera omite teléfono y correo, y la segunda muestra los contactos ficticios
aprobados. Las imágenes nulas o fallidas usan un SVG local de reemplazo.
El texto se codifica mediante Razor. La búsqueda y los filtros permanecen
disponibles en móvil.

Para iniciar la API en una segunda terminal:

```bash
Tiendas__Demostracion=true ConnectionStrings__PortalMercado='Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;' dotnet run --project PortalMercado.API --launch-profile https
```

Abrir `https://localhost:7161/api/tiendas`. Las consultas de este bloque se
resuelven en memoria. La configuración de conexión sigue siendo necesaria para
iniciar los otros servicios del sistema.

Detener los hosts antes de compilar y ejecutar:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Tiendas" --logger "trx;LogFileName=pruebas-ciclo2-tiendas-bloque02.trx"
```

La suite de tiendas contiene 45 casos, incluyendo los del Bloque 01. Comprueba
rutas anónimas, privacidad en HTML y JSON, codificación de texto, filtros,
paginación, estados 400/404/503 y restricción de demostración a Development.
Su compilación y ejecución están pendientes de confirmación en el equipo con
.NET. El catálogo público de productos se incorpora en el Bloque 03. La ubicación de puestos se incorpora en el Bloque 04. La edición privada y el
menú lateral administrativo continúan pendientes.

## Bloque 03 · Productos publicados por tienda

La ejecución compartida del Bloque 02 terminó con 45 pruebas aprobadas, cero
errores y cero omitidas. Las capturas recibidas muestran el directorio con filtros
y la ficha de Frutería El Sol con contactos privados ocultos.

El catálogo público utiliza CatalogoTiendaServicio y el contrato independiente
IRepositorioCatalogoPublico. La identidad del negocio se resuelve desde el slug
y su visibilidad en el servidor. Las consultas públicas no usan permisos del
listado administrativo del Ciclo 1.

| Ruta | Resultado |
| --- | --- |
| GET `/tiendas/{slug}` | Ficha comercial con tarjetas de productos y filtros. |
| GET `/api/tiendas/{slug}/productos` | Tienda pública, filtro normalizado, página de productos y categorías de esa tienda. |

Ambas rutas de catálogo reciben `categoriaId`, `q`, `orden`, `pagina` y
`tamano`. Los órdenes admitidos son `nombre`, `precio-asc` y `precio-desc`.
El tamaño predeterminado es 12 y el máximo es 100. La búsqueda admite hasta 200
caracteres y se aplica al nombre del producto.

El repositorio debe aplicar IdNegocio y PUBLICADO antes del total y la
paginación. Las categorías se calculan sobre todos los productos publicados
del negocio. Los órdenes desempatan por ID. El contrato SQL pendiente deberá
obtener el precio vigente y calcular disponible como existencia menos reservado.
El conjunto en memoria contiene los valores ficticios de esa proyección.

El servicio rechaza con 503 una respuesta del proveedor que mezcle negocios,
estados privados, productos duplicados, precios negativos o categorías distintas
de la solicitada. Las URLs de imágenes se validan mediante la misma función
que los logos. Cuando el producto no controla stock, disponible se devuelve nulo.
Una disponibilidad cero conserva visible el producto PUBLICADO y muestra
«Sin disponibilidad». El botón Agregar al carrito permanece deshabilitado.

### Datos ficticios para comprobar

| Tienda | Productos publicados | Categorías |
| --- | --- | --- |
| Frutería El Sol, ID 1001 | 15. Página 1 con 12 y página 2 con 3. | Frutas ID 1 y Verduras ID 3. |
| Abarrotes Lima, ID 1002 | 4, distintos de los de Frutería. | Abarrotes ID 2. |

El conjunto incluye además tres productos de Frutería en BORRADOR, AGOTADO y
SUSPENDIDO, y un producto de una tienda suspendida. Esos registros permiten
probar su exclusión. Los IDs son ficticios y no corresponden a los productos
de SQL Server del Ciclo 1.

| Acción | Resultado esperado |
| --- | --- |
| Abrir `/tiendas/fruteria-el-sol` | Total 15, tarjetas con precio y unidad, Siguiente disponible. |
| Pulsar Siguiente | Tres productos, total 15 y enlace Anterior. |
| Categoría Verduras y búsqueda tomate | Tomate italiano, con disponibilidad cero. |
| Menor precio | Sandía primero, a S/ 2.00. |
| Mayor precio | Uva primero, a S/ 12.00. |
| Abrir `/tiendas/abarrotes-lima` | Cuatro productos de abarrotes, sin frutas del otro negocio. |
| Solicitar categoría 2 en Frutería | Sin coincidencias. Las categorías ofrecidas siguen siendo Frutas y Verduras. |
| Buscar un nombre inexistente | Estado vacío con opción de restablecer la consulta. |
| Abrir catálogo de tienda oculta o suspendida | 404 sin datos comerciales ni productos. |

La imagen de Mango usa la URL HTTPS directa de Wikimedia probada durante el
Ciclo 1. Si falla, se muestra el SVG local de reemplazo. La integración de DEV
requiere confirmar el proveedor de imágenes autorizado.

### Verificación del Bloque 03

Después de copiar el ZIP sobre la solución, detener los hosts y ejecutar:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Tiendas" --logger "trx;LogFileName=pruebas-ciclo2-tiendas-bloque03.trx"
```

La compilación y ejecución de los casos nuevos están pendientes de confirmación
en el equipo con .NET. Se conservan los comandos de inicio del Bloque 02 con
Tiendas__Demostracion=true. Para explorar la API, iniciar el host API con la misma
opción y abrir `https://localhost:7161/api/tiendas/fruteria-el-sol/productos`.

Este bloque continúa con datos en memoria. La ubicación física vigente se incorpora en el Bloque 04. La edición
autenticada del perfil y la integración de datos autorizada siguen pendientes.

## Bloque 04 · Ubicación física vigente

El resultado recibido del Bloque 03 fue de 74 pruebas aprobadas, cero errores y
cero omitidas. Se recibieron capturas de búsqueda, disponibilidad, catálogo por
tienda y orden por precio. Se entregó además una corrección CSS para contener
las imágenes dentro de sus tarjetas. Este bloque incluye ese CSS actualizado.

La ficha MVC y el DTO público incorporan `Ubicaciones`, con mercado, sector,
código del puesto y referencia de acceso pública. La información procede de
IRepositorioUbicacionesTienda. Los datos fiscales quedan fuera del contrato.
El repositorio simulado se registra con el modo de demostración ya existente.

### Reglas provisionales de demostración

La fecha comercial se calcula en el servidor con TimeProvider y zona
`America/Lima`. Una asignación se muestra si Desde es anterior o igual a la
fecha comercial, y Hasta es nulo o posterior o igual a esa fecha. Se requiere
mercado activo, sector activo y puesto habilitado. Las ubicaciones se ordenan
por sector y código y se eliminan repeticiones del mismo IdPuesto.

Las reglas inclusivas y la zona horaria son decisiones para avanzar con los
datos ficticios. El contrato de A/DBA deberá confirmarlas, junto con los estados
de puesto admitidos. PuestoHabilitado es un indicador del contrato C# y no supone
una columna nueva en SQL Server.

La identidad del negocio se obtiene del slug autorizado para consulta pública.
Las asignaciones de otro negocio o de otro mercado causan un error 503 genérico.
La visibilidad de la tienda se verifica antes de consultar sus asignaciones.

| Tienda | Resultado de la demostración |
| --- | --- |
| Frutería El Sol | Puestos A-101 y A-102 en el sector Frutas y verduras del Mercado de demostración Centro. |
| Abarrotes Lima | Mensaje «Ubicación de puestos por confirmar». |

A-101 comenzó antes de la fecha de consulta y carece de fecha de fin.
A-102 comienza y termina en esa fecha para comprobar los límites inclusivos.
Las fechas son relativas a cada consulta para mantener los escenarios
demostrativos disponibles en cualquier día. Hay asignaciones vencidas, futuras,
de sector inactivo y de puesto no habilitado, que deben permanecer ocultas.

### Contrato y compatibilidad

IRepositorioUbicacionesTienda.ConsultarAsync recibe IdNegocio y fecha comercial,
y devuelve la proyección de asignaciones para su evaluación. El adaptador SQL
futuro puede prefiltrar vigencia para reducir datos, conservando las
comprobaciones de servidor. La referencia debe corresponder al acceso físico
público del puesto.

El servicio de tiendas mantiene la construcción previa con un único repositorio
para las pruebas y los consumidores del Bloque 01. Si no se proporciona el
repositorio de ubicación, la ficha obtiene una lista vacía. Los hosts registran
el proveedor de ubicaciones explícitamente mediante AddTiendas.

Las rutas existentes `/tiendas/{slug}`, `/api/tiendas/{slug}` y el catálogo de
productos incorporan las ubicaciones dentro de la ficha pública. No hay
operaciones de escritura de asignaciones en este contrato.

### Verificación

Detener los hosts, copiar el bloque sobre la solución y ejecutar:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Tiendas" --logger "trx;LogFileName=pruebas-ciclo2-tiendas-bloque04.trx"
```

Después iniciar la Web con el comando de demostración del Bloque 02.
Abrir Frutería El Sol y comprobar «Dónde encontrarnos» con ambos puestos.
Abrir Abarrotes Lima para comprobar el mensaje de ubicación pendiente.
Las asignaciones VENCIDO, FUTURO, INACTIVO y NO-HABILITADO quedan excluidas.
La API debe devolver únicamente datos públicos de las ubicaciones, sin
IdPuesto, Desde ni Hasta.

Las pruebas nuevas cubren límites de fechas, cambio de día entre UTC y Lima,
estados, duplicados, orden, aislamiento y renderizado de ficha. La compilación,
ejecución y comprobación visual de este bloque quedan pendientes en el equipo
con .NET. La edición autenticada del perfil y la integración autorizada siguen
pendientes.


## Bloque 05 · Edición autenticada del perfil comercial

El usuario confirmó 96 pruebas satisfactorias del Bloque 04 y las pantallas
con los puestos A-101/A-102 y la ubicación pendiente de Abarrotes Lima.

### Rutas y autorización

- MVC: GET `/mis-tiendas`, GET/POST `/mis-tiendas/{idNegocio}/perfil`.
- API: GET/PUT `/api/mis-tiendas/{idNegocio}/perfil`.
- Sesión real existente, identidad del actor tomada del servidor, cookies y CSRF.
- Se reconsulta `IUserAdministration.AccessAsync` y `ContextAsync` en cada lectura
  y escritura. El negocio debe figurar en `Disponibles`, incluso para un administrador.
- Permiso provisional `PRODUCTO_GESTIONAR`, para trabajar con las cuentas actuales.
  Queda pendiente aprobar un permiso específico de perfil y su asignación con A/DBA.
- El menú privado incorpora «Mi tienda» cuando la cuenta tiene ese permiso.
- Anónimo: redirección al login en MVC y 401 en API. Sin permiso/pertenencia: 403.

### Campos y concurrencia

`PerfilTiendaDatos` acepta solamente Version, Descripcion, LogoUrl, Horario,
Telefono y Correo. Los límites provisionales son 4000/500/500/30/254 caracteres,
respectivamente; el correo se valida y el logo exige HTTPS sin credenciales.
Los textos vacíos se convierten a null. Se permite descripción multilineal;
los otros campos rechazan caracteres de control. El logo se descarga en el
navegador, no en el servidor. Conserva el reemplazo visual del portal público.

El formulario MVC usa una lista explícita de propiedades para el enlace de datos.
La API rechaza propiedades JSON desconocidas con 400. NombreComercial, Slug,
Estado, IdMercado, comerciantes y datos fiscales no forman parte del comando.
Los indicadores de consentimiento de contactos tampoco se editan: un contacto
privado puede actualizarse desde el perfil autenticado y permanece oculto al público.

Version es un token Guid provisional de concurrencia. Una actualización consume
la versión y crea otra; reutilizar una versión anterior devuelve 409 sin cambios.
La vista permite recargar el perfil. El adaptador SQL podrá convertir un token
opaco en el mecanismo aprobado por DBA, por ejemplo rowversion, manteniendo la regla.

### Demostración y vínculo con cuentas locales

Los negocios ficticios públicos (1001/1002) no son los negocios reales de tus
cuentas. Para probar con el negocio local 1, se configura de forma explícita:

```bash
Tiendas__Demostracion=true Tiendas__NegocioLocalDemostracion=1 dotnet run --project PortalMercado.Web --launch-profile https
```

La variable vincula exclusivamente el negocio local 1 con Frutería El Sol
(IdNegocio público ficticio 1001). El servicio exige primero permiso y acceso
al negocio local 1; esta configuración no concede acceso. Sin vínculo, un negocio
autorizado devuelve 404 de perfil no disponible. Otros negocios no se vinculan
implícitamente. Un vínculo cero/negativo, o fuera del modo de demostración,
impide iniciar el host. La demostración sigue limitada a Development.

El repositorio de tiendas ficticias ahora es singleton por host para conservar
los cambios entre solicitudes y reflejarlos en la ficha/directorio públicos.
Web y API son procesos independientes: cada uno conserva su propia copia de
los datos simulados. Los cambios se pierden al detener o reiniciar ese host.
No se escribe ninguna tabla ni procedimiento SQL en este bloque.

### Auditoría y contrato pendiente de persistencia

IRepositorioPerfilTienda recibe el ID real autorizado, datos permitidos y
AuditoriaPerfilTienda (actor, negocio, fecha UTC y correlación). Guardar debe
comparar versión, actualizar y auditar de forma atómica. El proveedor simulado
realiza estas operaciones bajo un mismo bloqueo y conserva el último cambio,
sin contenidos de descripción, contactos, URL ni credenciales en la auditoría.
Los controladores registran el evento y rechazos de servicio con identificadores
y estado, sin registrar el formulario. La API y la vista privada muestran la
metadata del último cambio; la ficha pública no la expone.

La auditoría es demostrativa en memoria y registros de aplicación; no equivale
a una bitácora SQL durable. El adaptador autorizado debe implementar una
transacción de actualización y bitácora, comparación de versión y aislamiento
por negocio. Quedan pendientes su procedimiento/contrato SQL, tipos y límites
definitivos, permisos específicos y el contrato de consentimiento. El proveedor
sin demostración continúa respondiendo 503 hasta disponer de esa integración.

### Verificación en el equipo con .NET

Detener los hosts, extraer el ZIP en la raíz de la solución, reemplazar sus
archivos y ejecutar:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Tiendas" --logger "trx;LogFileName=pruebas-ciclo2-tiendas-bloque05.trx"
```

Se agregan 19 casos de servicio y 11 HTTP: se esperan 126 casos de Tiendas en total.
Estas pruebas usan proveedores controlados de autenticación únicamente dentro
del proyecto de pruebas; el código de la aplicación conserva el login real.
La compilación y ejecución quedan pendientes: este entorno no contiene dotnet.

Prueba manual con el comando de inicio anterior:

1. Iniciar sesión con una cuenta con PRODUCTO_GESTIONAR y acceso al negocio 1.
2. Abrir «Mi tienda» y el perfil del negocio 1. Actualizar descripción y horario.
3. Abrir `/tiendas/fruteria-el-sol` y comprobar ambos cambios; los contactos
   privados de Frutería El Sol deben continuar ocultos.
4. Probar un logo HTTPS directo, uno HTTP y el campo vacío. HTTPS debe guardarse,
   HTTP debe rechazarse y vacío debe mostrar el reemplazo en la tienda pública.
5. Abrir el formulario en dos pestañas. Guardar en la primera y luego en la
   segunda: debe aparecer el conflicto y conservarse la primera actualización.
6. Con una cuenta sin acceso al negocio 90, abrir `/mis-tiendas/90/perfil`:
   debe devolver 403. Repetir tras cerrar sesión: debe pedir login.

Guardar como evidencia una captura del formulario guardado y otra de la ficha
pública actualizada. El cierre global de Ciclo 2, revisión móvil, menú del
prototipo administrativo, integración autorizada, evidencias y video siguen
pendientes; este bloque no declara cumplimiento completo del ciclo.


## Bloque 06 · Menú principal y navegación adaptable

El usuario confirmó 126 pruebas correctas del Bloque 05 y una captura pública
con la descripción y el horario actualizados. Los contactos privados permanecen
ocultos. La comprobación manual del conflicto entre pestañas sigue pendiente.

### Diseño y comportamiento

El marco privado toma del prototipo el menú lateral agrupado, cabecera con
búsqueda, verde/naranja y tarjetas de acceso. El panel muestra funciones
implementadas y habilitadas para la cuenta, sin consultar ni inventar métricas
comerciales. Los módulos de mercados/sectores/puestos, pedidos, pagos y reportes
requieren sus implementaciones antes de convertirse en accesos navegables.

`MenuPrincipal.Crear` centraliza los enlaces para menú y tarjetas. Usa los
permisos de los claims actualizados por PortalCookieEvents en cada solicitud.
El nombre de rol no concede enlaces sin permiso. Cada controlador/servicio
conserva sus propias validaciones de servidor: ocultar enlaces no autoriza
operaciones ni sustituye las comprobaciones del negocio.

| Acceso | Permiso para mostrarlo | Destino |
| --- | --- | --- |
| Inicio | Sesión autenticada | Home/Index |
| Mis negocios | NEGOCIO_CONTEXTO | Negocios/Index |
| Mi tienda | PRODUCTO_GESTIONAR, provisional del bloque 05 | PerfilTienda/Index |
| Productos | PRODUCTO_VER | Productos/Index |
| Categorías | CATEGORIA_VER | Catalogo/Index |
| Unidades | UNIDAD_MEDIDA_VER | Catalogo/Unidades |
| Usuarios | USUARIO_VER | Usuarios/Index |
| Roles y permisos | PERMISO_GESTIONAR | Administracion/Permisos |
| Mi perfil | Sesión autenticada | Cuenta/Perfil |

El buscador GET de la cabecera filtra las tarjetas del inicio por nombre o
descripción, hasta 80 caracteres. Conserva el menú completo y permite volver a
ver todos los accesos. Sin coincidencias presenta un mensaje; no habilita
módulos ajenos. Las respuestas del inicio no se almacenan en caché.

Después del login correcto se abre Home/Index. En MFA se mantiene la verificación
antes de iniciar sesión. Si se generan códigos de recuperación, se muestran
antes de continuar al panel. El cierre de sesión conserva POST con CSRF.
El enlace «Mi panel» en el portal público permite regresar a la administración.

El inicio anónimo presenta una bienvenida con accesos al directorio y login.
El diagnóstico técnico continúa en sus endpoints existentes y no se presenta
como contenido del inicio comercial.

### Navegación y pantallas pequeñas

Desde 992 px se muestra el menú lateral. Por debajo se abre con «Menú», usando
Bootstrap local para el estado y aria-expanded. Escape cierra el menú móvil
y devuelve el foco al botón. La búsqueda ocupa una segunda fila cuando hace
falta. Las tarjetas pasan de tres a dos y una columna. Se conservan etiquetas,
foco visible, enlace de salto y la navegación de teclado.

El CSS nuevo `menu-principal.css` se carga en el layout del sistema. El portal
público continúa usando `tiendas.css` y sus puntos de adaptación de los bloques
anteriores. No se añaden dependencias remotas ni bibliotecas nuevas.

### Validación

Se añaden 15 casos de selección de accesos y marcado activo, y 7 HTTP con el
login y Razor reales sobre un proveedor de autenticación controlado de pruebas.
Cubren inicio anónimo, acceso al panel, codificación del nombre, filtrado,
permiso retirado, logout con CSRF, enlace desde el portal público y límite de
búsqueda. La expectativa de redirección de MfaWebTests se actualiza al panel,
conservando las comprobaciones de verificación y emisión de sesión.

Se comprobó la sintaxis JavaScript con Node y la interacción de Escape con un
entorno de eventos controlado (cierre/foco en móvil, sin efecto en escritorio).
No hay SDK .NET en este entorno. El renderizado y la revisión visual deben
confirmarse en el equipo de desarrollo; no se declara una revisión móvil completa.

Detener los hosts, extraer el bloque sobre la raíz de la solución y ejecutar:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Tiendas" --logger "trx;LogFileName=pruebas-ciclo2-tiendas-bloque06.trx"
```

Se esperan 148 casos de Tiendas (126 previos y 22 nuevos). Después ejecutar la
suite completa para cubrir también el ajuste de navegación tras MFA:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --logger "trx;LogFileName=pruebas-ciclo2-solucion-bloque06.trx"
```

Iniciar la Web manteniendo el modo de demostración:

```bash
Tiendas__Demostracion=true Tiendas__NegocioLocalDemostracion=1 dotnet run --project PortalMercado.Web --launch-profile https
```

Comprobaciones manuales:

1. Sin sesión, abrir `/`: deben aparecer la bienvenida, tiendas y login.
2. Iniciar sesión: debe abrirse el panel con los accesos de la cuenta. Abrir
   productos, categorías, unidades y perfil comercial si tienes esos permisos.
3. Buscar «productos» y luego un nombre sin coincidencias. Usar «Ver todos».
4. Probar otra cuenta con menos permisos y confirmar sus accesos reducidos.
5. En el modo adaptable del navegador, probar 390 × 844 y 768 × 1024:
   abrir/cerrar menú, pulsar Escape y comprobar el foco. Revisar también a
   1440 × 900. No debe aparecer desplazamiento horizontal de la página.
6. A 390 px revisar directorio, ficha pública, catálogo y formulario comercial.
   Comprobar orden de campos, tarjetas, imágenes y acceso a botones con teclado.
7. Volver desde «Mi panel» y cerrar sesión. El panel privado debe desaparecer.

Conservar capturas de panel en escritorio y móvil. Siguen pendientes las
comprobaciones manuales indicadas, integración autorizada con A/DBA, contrato
SQL de tiendas, auditoría durable, cierre global de pruebas, evidencias y video.


## Estado confirmado después del Bloque 06

El usuario ejecutó la suite completa con los cinco indicadores SQL habilitados:
331 correctas, cero errores y cero omitidas, archivo
`pruebas-ciclo2-solucion-completa.trx`. Esto cubre los módulos existentes y
148 casos de Tiendas. Las pruebas SQL anteriores no acreditan persistencia SQL
del módulo nuevo de Tiendas. Las capturas confirman panel de escritorio,
panel móvil a 390 px y menú móvil desplegado. La navegación por enlaces,
Escape y el conflicto entre pestañas no se han confirmado manualmente.

Siguiente paso: ejecutar `Diagnostico_Tiendas_Ciclo2.sql` en la base local y
contrastar metadatos instalados con el esquema entregado. El diagnóstico solo
consulta sys.* y el contexto actual. Devuelve seis resultados: base/permisos
de metadatos, columnas de Negocio, objetos relacionados, índices únicos,
posibles fuentes de consentimiento/visibilidad/versión y columnas de
asignaciones/auditoría. Un nombre de columna no acredita aprobación de negocio.

El esquema local entregado contiene descripción, logo, horario, teléfono y
correo en Negocio. No define en esa tabla VisibilidadAprobada,
TelefonoPublico, CorreoPublico ni un token de versión. La ruta pública utiliza
solo slug, pero el índice entregado es único por mercado y slug. Estos hechos
del archivo deben contrastarse con la base instalada antes de acordar el
adaptador. La decisión de visibilidad y consentimiento requiere una fuente
explícita aprobada; no se infiere de que el negocio esté ACTIVO.


## Bloque 07 · Persistencia SQL local autorizada

El usuario autorizó expresamente desarrollar también la parte SQL en la copia
local, al no recibir contratos ni una nueva base de compañeros. Esta excepción
se limita a PortalMercado_ET01_Test, SQLEXPRESS del mismo equipo y Development.
Los cambios se entregan separados en database/ciclo2-local. La aplicación no
instala objetos ni modifica una base central al iniciarse.

El esquema diagnosticado confirmó las columnas comerciales existentes. Se
agrega TiendaPublicacionLocal para visibilidad, consentimiento, versión Guid
y metadata del último cambio. Los indicadores nacen desactivados; solo el
negocio ficticio del Ciclo 1 se publica mediante el script de datos explícito.
Un trigger invalida la versión ante cualquier INSERT/UPDATE de Negocio, también
cuando la modificación procede de otro escritor. No se cambian columnas ni el
índice original del slug. La ambigüedad permanece oculta con 404 uniforme.

El proveedor RepositorioTiendasSqlLocal implementa los cuatro contratos con
parámetros tipados, resultados por nombre, cancelación y mensajes de error
controlados. La cuenta SQL utiliza autenticación Windows local. El modo nuevo
es opt-in mediante Tiendas:SqlLocal; no puede combinarse con demostración ni
usarse en producción. La conexión y la instancia real se comprueban antes de
consultar. El modo anterior de demostración continúa disponible por separado.

El guardado comprueba actor activo, PRODUCTO_GESTIONAR, NEGOCIO_CONTEXTO y
alcance: administrador general, mercados del administrador de mercado o
asociación activa al negocio. Es coherente con el contexto de administración
existente y se revalida en SQL. Guarda únicamente los campos comerciales,
renueva versión e inserta TIENDA_PERFIL_ACTUALIZADO en Auditoria en la misma
transacción. La auditoría no contiene el formulario, contactos ni URL.

Los perfiles ahora utilizan ID real y negocio-prueba-et01. La tienda mock
fruteria-el-sol no se copia ni se mezcla con la base local. La guía describe
instalación manual, pruebas, comando de inicio, reinicio para comprobar
persistencia y consulta de auditoría.

Se agregan 10 casos de configuración y 9 de integración local opt-in. Estos
últimos comprueban lecturas reales, persistencia entre conexiones, auditoría,
privacidad, concurrencia, rechazo de actores/negocios ajenos, renovación ante
otro escritor, rollback ante fallo de bitácora, datos nuevos ocultos y slug
ambiguo. Se esperan 167 casos de Tiendas y 350 de la solución completa con
todas las variables SQL activadas. La ejecución no se ha realizado en este
entorno sin .NET/SQL Server: los números son expectativas de descubrimiento,
no resultados de una ejecución nueva.


## Revisión de la hoja reenviada · 13 de noviembre de 2026

Se contrastó nuevamente la versión 1.0 del requerimiento de Programador B,
Ciclo 2. El resumen de 350 correctas acredita la ejecución automatizada
comunicada por el usuario; no acredita por sí solo todos los criterios manuales,
los controles no implementados ni una aprobación externa.

### Funcionalidad

| Requisito | Estado real | Evidencia y límite |
| --- | --- | --- |
| RF-B2-01 · Directorio | Implementado en demo y SQL local | Filtra activo/mercado/visibilidad; nombre/categoría; pruebas de servicio, HTTP y SQL. |
| RF-B2-02 · Slug | Implementado con regla local | Ruta pública, validación y 404; slugs ambiguos no se publican. El índice por mercado se conserva. Contrato central sin aprobación de DBA. |
| RF-B2-03 · Perfil público | Implementado | Proyección explícita sin RUC/dirección fiscal; contactos con indicadores separados. Captura de ficha SQL y pruebas de privacidad. |
| RF-B2-04 · Ubicación | Implementado | Vigencia con fecha de Lima, estados y aislamiento. Pruebas de fechas/estados; capturas previas del mock. Conviene capturar ubicación de la copia SQL. |
| RF-B2-05 · Productos publicados | Implementado | Aislamiento por negocio, precio/imagen/unidad/disponibilidad y paginación. Pruebas de servicio/HTTP/SQL. |
| RF-B2-06 · Filtros de productos | Implementado | Nombre/categoría/orden y conservación de filtros al paginar. Falta cerrar demostración visual móvil del portal público. |
| RF-B2-07 · Edición limitada | Implementado en SQL local | Bind/DTO cerrados, versiones y escritura SQL atómica. No edita datos fiscales, slug, mercado ni estado. |
| RF-B2-08 · Imágenes | PARCIAL | HTTPS sin credenciales, recurso externo por URL y reemplazo. No se verifica extensión/MIME ni tamaño del archivo remoto; no hay carga de archivos ni proveedor con límites acordados. No marcar completo. |
| RF-B2-09 · Autorización | Implementado | Sesión, permisos y negocio en servidor; MVC/API, CSRF y alcance SQL de escritura. Pruebas de consulta/edición ajenas. |
| RF-B2-10 · Responsive | Implementado; revisión visual parcial | Panel y menú verificados a 390 px. Revisar directorio, ficha, catálogo con paginación y formulario a 390/768 px; HTTP no mide diseño ni interacción de viewport. |
| RF-B2-11 · Trazabilidad | Implementada para cambios; cobertura de errores por completar | Auditoria existente con actor/negocio/fecha/correlación y rollback ante fallo. Hay logs de rechazos/conflictos y consultas no disponibles; no se ha acreditado un registro uniforme de todos los rechazos privados/modelo en MVC/API. |
| RF-B2-12 · Carrito futuro | Implementado | Botón deshabilitado en tarjetas; no hay pedidos/cobros nuevos. |

La implementación utiliza clases y contratos separados por capas. La vista
Editar y el controlador PerfilTienda son equivalentes funcionales a EditarPerfil
orientativo. Las rutas privadas reales son /api/mis-tiendas/{idNegocio}/perfil;
la sección 7 de la hoja presenta las rutas como orientativas. No es necesario
crear otra ruta solo para coincidir con un ejemplo sujeto al contrato compartido.

### Criterios de aceptación

CA-01/02/03/06/07/08: cubiertos funcionalmente por las implementaciones y pruebas
mencionadas. CA-04 y CA-05: completar evidencias visuales móviles y capturas
actuales del perfil/ubicación SQL. CA-09: existen pruebas y reemplazos/estados
vacíos; incluirlos en el recorrido de demostración. CA-10: compilación y suite
confirmadas, pero no hubo revisión conjunta con A/DBA; queda como dependencia
externa, no como aprobación obtenida.

### Entregables

| Entregable de la hoja | Estado |
| --- | --- |
| Código MVC/API/Application/Infrastructure/Tests | Implementado y validado en el equipo Windows, con límites anteriores. |
| Rama feature/b-tiendas-ciclo2 y solicitud de integración | Pendientes de confirmar con git branch/status y la URL de la solicitud real. |
| Manual breve con capturas | Preparado en MANUAL_TIENDAS_CICLO2.md. Las imágenes de demo están identificadas; la ficha SQL es real. Completar revisión visual antes del cierre. |
| Documento técnico | Este documento, guía SQL y solicitud de contrato disponibles. |
| Evidencias automatizadas | Resultado de 350 correctas comunicado; copiar el TRX real del equipo Windows. No se genera uno sustituto. |
| Datos de prueba | Scripts 01/02 locales; nuevo script 04 opcional para demostrar paginación pública con más productos. |
| Video | Guion preparado en GUION_VIDEO_TIENDAS_CICLO2.md; video todavía no grabado. |
| Pendientes DBA y riesgos | SOLICITUD_CONTRATO_TIENDAS_CICLO2.md y guía local. Revisión e integración central no realizadas. |

### Excepción local y exclusiones

La hoja reserva DDL/SP/índices al DBA y permite mocks si falta contrato. El usuario,
trabajando individualmente y sin recursos comunicados de compañeros, autorizó
una adaptación limitada a su base local. Se entrega separada y con guardas.
Esto permite demostrar persistencia, pero no se presenta como conformidad del
DBA ni como despliegue a SQL central en la nube. La conexión Windows local no
acredita permisos mínimos de una cuenta de aplicación de producción.

No se añadieron pantallas de alta/baja de negocios, mantenimiento de comerciantes,
asignación física, pedidos, cobros o inventario. La carga de datos ficticios es
preparación local de pruebas, no una función administrativa nueva de la Web.
La integración de menú/login y el ajuste del mapeo de Negocio son cambios
compartidos documentados; no hay coordinación externa ficticia para aprobarlos.

### Ruta de cierre

1. Completar RF-B2-08 con una estrategia explícita de validación de imágenes
   remotas/proveedor, o recibir una aclaración del alcance que acepte el modo URL.
2. Uniformar y comprobar el registro seguro de errores relevantes del perfil
   para cerrar RF-B2-11 sin registrar el formulario ni secretos.
3. Ejecutar el script 04 opcional y demostrar filtros/paginación con datos SQL;
   revisar portal y formulario móvil, capturar resultados y conflicto entre pestañas.
4. Copiar TRX, terminar evidencias del manual y grabar el video con el guion.
5. Revisar diff y rama, commit/push y solicitud de integración. Confirmar sus URLs
   reales antes de declarar completa la entrega. La conformidad final del revisor
   y la revisión A/DBA quedan sujetas a disponibilidad externa.

No se asigna un porcentaje de aprobación oficial: hay requisitos funcionales
parciales y entregables pendientes, aunque la suite completa esté en verde.
