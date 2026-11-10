# PortalMercado · Ciclo 1 · Catálogo

Implementación local del catálogo de categorías, unidades y productos.
Application valida datos y exige permisos usando
la autorización de Etapa 02. Los repositorios representan la interfaz interna de C#.
En la copia local, `SqlCatalogo` llama a los procedimientos de
`database/ciclo1/01_Procedimientos_Catalogo_Local.sql`. El contrato para DEV
centralizado y los permisos definitivos requieren coordinación con el DBA.

## Contrato SQL de la copia local

- `dbo.pm_Catalogo_Categorias_Listar` devuelve la página y `@Total` como parámetro de salida.
- `dbo.pm_Catalogo_Categorias_Guardar` devuelve la fila creada o editada.
  Un `@IdCategoria` nulo crea una categoría activa.
- `dbo.pm_Catalogo_Categorias_CambiarEstado` actualiza el estado sin devolver filas.
- `dbo.pm_Catalogo_Unidades_Listar` devuelve las unidades de medida.

La instalación local no otorga permisos. El usuario de servicio de DEV necesita los
permisos de ejecución autorizados por el DBA.

| Operación | Entrada | Salida y condiciones |
| --- | --- | --- |
| Listar categorías | Búsqueda por nombre o slug, estado opcional, página y tamaño | Id, IdPadre, NombrePadre, Nombre, Slug, Activa y total. Orden estable por nombre e ID. |
| Crear categoría | Nombre, Slug, IdPadre opcional, actor y correlación | Categoría creada activa en la copia local. Slug único global y padre existente si se indica. |
| Editar categoría | Id, Nombre, Slug, IdPadre opcional, actor y correlación | Categoría actualizada conservando el estado. Validar la cadena de padres completa y evitar ciclos bajo concurrencia. |
| Activar o desactivar | Id, Activa, actor y correlación | Sin filas o error de categoría inexistente. Sin borrado físico. |
| Consultar unidades | Sin filtros requeridos | IdUnidad, Codigo, Nombre y PermiteFraccion. Solo lectura. |

El adaptador traduce los errores de negocio 51062 a 51068 a mensajes de aplicación.
Las escrituras de categoría y su auditoría se guardan en una sola transacción.
La búsqueda trata el texto como literal y usa la intercalación de SQL Server.

## Autorización y datos de prueba

Confirmar quién consulta y quién mantiene las categorías, ya que la jerarquía es global.
Los códigos propuestos son `CATEGORIA_VER`, `CATEGORIA_GESTIONAR` y `UNIDAD_MEDIDA_VER`.
En la copia local, el script `database/ciclo1/02_Permisos_Catalogo_Local.sql`
asigna los tres permisos a `ADMIN_GENERAL`. En DEV, confirmar qué roles deben recibirlos.

Para integrar productos, solicitar dos negocios de prueba y usuarios con acceso a uno
solo, categorías activas e inactivas, unidades enteras y fraccionarias, y existencias con
reservas. Confirmar también las transiciones de publicación, el contrato transaccional
de precio e historial, las fuentes de imágenes y el alcance de consulta administrativa.
El inventario se consultará sin modificar existencias ni reservas.

## Verificación local

Con los cuatro procedimientos instalados en `PortalMercado_ET01_Test`, desde la raíz
de la solución ejecutar en Git Bash:

```bash
PM_RUN_CATALOGO_SQL_TESTS=1 dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~CatalogoServicioTests|FullyQualifiedName~CatalogoSqlTests"
```

Las pruebas SQL son de lectura y usan autenticación integrada en `.\SQLEXPRESS`.
Las pruebas de `CatalogoHttpTests` verifican el rechazo de consultas anónimas.
La escritura se comprueba desde la pantalla con un administrador de la copia local.

## Rutas del catálogo

| Método y ruta | Operación |
| --- | --- |
| GET `/api/catalogo/categorias` | Listar. Filtros opcionales `buscar`, `activa`, `pagina` y `tamano`. |
| POST `/api/catalogo/categorias` | Crear con `nombre`, `slug` e `idPadre` opcional. |
| PUT `/api/catalogo/categorias/{id}` | Editar con los mismos campos de creación. |
| PATCH `/api/catalogo/categorias/{id}/estado` | Activar o desactivar con `{ "activa": true }` o `false`. |
| GET `/api/catalogo/unidades` | Consultar las unidades de medida. |

Las escrituras API requieren el encabezado CSRF del flujo de autenticación.
Las pantallas MVC están en `/Catalogo` y `/Catalogo/Unidades`.

## Listado de productos por negocio

El script `database/ciclo1/03_Productos_Consulta_Local.sql` instala
`dbo.pm_Catalogo_Productos_Listar`, asigna `PRODUCTO_VER` a `ADMIN_GENERAL` y agrega
las unidades locales de prueba KG y UND si faltan.

- MVC `/Productos`. Usa el negocio activo de la sesión si no se selecciona otro.
- API `GET /api/catalogo/productos`. Filtros opcionales `idNegocio`, `idCategoria`,
  `estado`, `buscar`, `pagina` y `tamano`. Requiere `PRODUCTO_VER` y `NEGOCIO_CONTEXTO`.
- SQL filtra por negocio antes de paginar. Devuelve el precio, el código de unidad,
  la disponibilidad si hay inventario y la URL de la imagen principal. Devuelve
  el total por `@Total`. El servicio comprueba que el negocio esté en el contexto autorizado.
- La ficha, el alta y la edición están documentadas en la sección siguiente.

Con los scripts locales de los bloques instalados en orden, probar en Git Bash:

```bash
PM_RUN_CATALOGO_SQL_TESTS=1 PM_RUN_PRODUCTOS_SQL_TESTS=1 dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Catalogo"
```

## Registro y edición de productos

Instalar `database/ciclo1/04_Productos_Gestion_Local.sql` en la copia local después del Bloque 04.
Registra `PRODUCTO_GESTIONAR` para `ADMIN_GENERAL` e instala los procedimientos de ficha y guardado.
La escritura exige además `PRODUCTO_VER`, `NEGOCIO_CONTEXTO` y acceso al negocio.

| Ruta | Resultado |
| --- | --- |
| GET `/api/catalogo/productos/{id}?idNegocio=...` | Ficha del producto en el negocio indicado. |
| POST `/api/catalogo/productos?idNegocio=...` | Crea un borrador y devuelve 201. |
| PUT `/api/catalogo/productos/{id}?idNegocio=...` | Edita los datos y devuelve 204. |

Las escrituras requieren CSRF. Enviar `idCategoria`, `idUnidad`, `nombre`, `precio`, `controlaStock`
y, opcionalmente, `sku` y `descripcion`. El SKU admite caracteres ASCII imprimibles y es único
por negocio cuando se indica. El precio admite hasta dos decimales. El guardado conserva el estado
de productos existentes y registra el precio inicial o el cambio de precio y la auditoría en la misma
transacción. La ficha se consulta siempre por producto y negocio. El cambio de estado se realiza desde la ficha.

En MVC, selecciona el negocio en Productos y usa Nuevo producto o Editar. Los ID de referencia
figuran en las pantallas Categorías y Unidades. El control de stock del formulario indica si
aplica inventario. Las existencias y las reservas se gestionan fuera de este flujo.

## Detalle, precios e inventario

Ejecutar `database/ciclo1/05_Productos_Detalle_Local.sql` después del Bloque 05.
La ficha MVC se abre pulsando el nombre del producto en el listado. La API usa
`GET /api/catalogo/productos/{id}/detalle?idNegocio=...&pagina=1`.

Se mantienen los permisos de consulta de productos y el ámbito por negocio. El procedimiento
devuelve dos conjuntos de resultados. El primero tiene los datos del producto, su precio actual
y el inventario cuando aplica. El segundo devuelve hasta 21 vigencias ordenadas de más reciente
a más antigua. El servicio entrega 20 por página y usa la fila adicional para indicar si hay
una siguiente. Las fechas del historial se muestran en UTC. Existencia y reservado son de
solo lectura. Disponible se calcula como existencia menos reservado.

## Imágenes de producto

Ejecutar `database/ciclo1/06_Productos_Imagenes_Local.sql` después del Bloque 06.
Abrir la ficha del producto y pulsar **Imágenes**. El listado ordena por `Orden` e
identificador. El backend almacena únicamente la URL HTTPS externa y el navegador carga
la vista previa. Usar URLs directas del servicio de imágenes autorizado.

| Método y ruta | Operación |
| --- | --- |
| GET `/api/catalogo/productos/{id}/imagenes?idNegocio=...` | Listar imágenes del producto en el negocio indicado. |
| POST `/api/catalogo/productos/{id}/imagenes?idNegocio=...` | Registrar URL, orden y si es principal. Devuelve 201. |
| PUT `/api/catalogo/productos/{id}/imagenes/{idImagen}?idNegocio=...` | Editar la imagen del producto indicado. Devuelve 204. |

Las escrituras usan `url`, `orden` y `principal`. Requieren `PRODUCTO_GESTIONAR`,
permiso de consulta, ámbito por negocio y CSRF. Al marcar una imagen como principal,
se desmarcan las demás dentro de la transacción que registra la auditoría.

## Publicación de productos

Ejecutar `database/ciclo1/07_Productos_Publicacion_Local.sql` después del Bloque 07.
La ficha muestra **Cambiar estado** a quien tiene permiso de gestión.
API `PATCH /api/catalogo/productos/{id}/estado?idNegocio=...` recibe
`{ "estadoActual": "BORRADOR", "nuevoEstado": "PUBLICADO" }` y devuelve 204 al guardar.
Exige `PRODUCTO_GESTIONAR`, permiso de consulta, acceso al negocio y CSRF.

La hoja RF-B09 no detalla las transiciones. Esta matriz es la propuesta para pruebas
en la copia local y queda pendiente de aprobación funcional.

| Estado actual | Siguientes estados |
| --- | --- |
| BORRADOR | PUBLICADO |
| PUBLICADO | AGOTADO, SUSPENDIDO |
| AGOTADO | PUBLICADO, SUSPENDIDO |
| SUSPENDIDO | BORRADOR |

Para publicar se exige nombre, categoría activa, unidad válida, precio no negativo e
imagen principal con URL HTTPS. Si el producto controla stock, necesita un registro
de inventario con existencia mayor que reservado. Agotado es un estado manual y no
modifica inventario. El procedimiento compara el estado de la solicitud con el vigente
y guarda el cambio y la auditoría en una sola transacción.

## Prueba local de inventario y aislamiento

Ejecutar completo `database/ciclo1/08_Datos_Prueba_Aislamiento_Inventario.sql` en
`PortalMercado_ET01_Test` después del Bloque 08. Requiere la cuenta ficticia
`vendedor.et02@example.invalid`, activa y asignada únicamente al negocio de prueba
ET01, una categoría activa y la unidad KG. El rol auxiliar `CATALOGO_PRUEBA_LOCAL`
se asigna a esa cuenta para probar permisos de productos dentro de su ámbito.
La matriz de roles para DEV queda pendiente de coordinación con el DBA.

El script agrega un negocio ajeno y dos productos ficticios. Conserva los datos de
prueba existentes al volver a ejecutarlo. Mantener intactos los productos
`CAT-LOCAL-STOCK` y `CAT-LOCAL-AJENO` durante estas pruebas. El primero nace con
existencia de 12.500 kg, reserva de 2.250 kg y disponibilidad de 10.250 kg.

Desde Git Bash, ejecutar la suite del catálogo y guardar el resultado TRX:

```bash
PM_RUN_CATALOGO_SQL_TESTS=1 PM_RUN_PRODUCTOS_SQL_TESTS=1 PM_RUN_CATALOGO_FIXTURE_TESTS=1 dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Catalogo" --logger "trx;LogFileName=pruebas-ciclo1-catalogo.trx"
```

Las dos pruebas nuevas comprueban el cálculo en listado y ficha, que la versión
del inventario se conserva al consultarlo, y que SQL rechaza la lectura y edición
de un producto existente cuando se combina su ID con otro negocio.

Para verificar la navegación con una sesión real, vuelve a iniciar sesión como
vendedor. En **Productos** debe aparecer únicamente el negocio ET01. Abre
`CAT-LOCAL-STOCK` y verifica las tres cantidades en la ficha. Sustituye los
marcadores de estas rutas por los IDs devueltos por el script:

| Ruta MVC | Resultado esperado para el vendedor |
| --- | --- |
| `/Productos/Detalle/{producto-ajeno}?idNegocio={negocio-ajeno}` | 403 por negocio fuera del ámbito. |
| `/Productos/Editar/{producto-ajeno}?idNegocio={negocio-ajeno}` | 403 por negocio fuera del ámbito. |
| `/Productos/Detalle/{producto-ajeno}?idNegocio={negocio-propio}` | 404 por producto inexistente en el negocio propio. |

## Ejecución y evidencias del Ciclo 1

La guía `docs/GUIA_CATALOGO_CICLO1.md` reúne configuración local, uso del módulo,
validación de filtros y paginación, comandos de regresión y dependencias de
integración. El script `database/ciclo1/09_Datos_Prueba_Paginacion.sql` agrega un
conjunto local de 25 productos para comprobar dos páginas y filtros combinados.
