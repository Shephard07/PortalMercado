# Portal público y búsqueda global del Ciclo 3

Bloque 02 · 19 de noviembre de 2026.

## Aplicación del paquete

1. Detener Web y API.
2. Extraer el ZIP en la raíz de la solución PortalMercado, junto al .sln.
3. Conservar los archivos del bloque 01 y su corrección de pruebas. Este paquete
   no incluye ni reemplaza RegistroClienteServicioTests.cs.
4. Ejecutar las pruebas del bloque con el comando siguiente.

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Clientes" --logger "trx;LogFileName=pruebas-ciclo3-clientes-bloque02.trx"
```

Se prepararon 57 casos nuevos y se conservan los 110 confirmados del bloque 01.
El filtro debería descubrir 167 casos si la copia local coincide. No se requieren
variables de SQL para estos casos; los hosts HTTP usan demostración explícita.

Para verificar también la navegación y tiendas previas:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Tiendas|FullyQualifiedName~WebIntegrationTests|FullyQualifiedName~MfaWebTests" --logger "trx;LogFileName=pruebas-ciclo3-regresion-portal.trx"
```

No hay SDK dotnet en el entorno de elaboración. No se declara compilación ni
pruebas correctas de este bloque hasta ejecutarlas en Windows. El total de 110
correctas sigue correspondiendo al bloque 01, no acredita los casos nuevos.

## Iniciar con las tiendas SQL locales ya preparadas

Desde Git Bash, en la carpeta de la solución:

```bash
Tiendas__SqlLocal=true Tiendas__Demostracion=false Tiendas__NegocioLocalDemostracion= dotnet run --project PortalMercado.Web --launch-profile https
```

No se añaden scripts ni objetos SQL. Se reutilizan las lecturas de tiendas y
catálogo del Ciclo 2. Deben seguir instaladas y habilitadas en tu copia local.
La conexión de la Web debe mantener su configuración local anterior.

Para una demostración explícita con los datos ficticios existentes:

```bash
Tiendas__SqlLocal=false Tiendas__Demostracion=true Tiendas__NegocioLocalDemostracion= dotnet run --project PortalMercado.Web --launch-profile https
```

La cabecera distingue demostración de entorno SQL local. No se presentan como
persistidos los datos del modo demostración.

## Qué abrir

- https://localhost:7275/ sin sesión: nueva portada pública.
- https://localhost:7275/ con sesión: panel actual, sin cambiar su menú o buscador.
- https://localhost:7275/explorar: portada pública para cualquier visitante,
  incluso si ya inició sesión.
- https://localhost:7275/explorar/buscar: buscador global.
- https://localhost:7275/tiendas: directorio anterior, conservado.

El enlace Inicio y el logo públicos abren /explorar. Mi panel mantiene el acceso
al inicio privado. El login, logout y MFA no se modifican en este bloque.

## Recorrido de comprobación

1. Abrir /explorar y revisar mercados, categorías, productos y tiendas.
2. Buscar por producto, tienda y categoría; las tildes y mayúsculas no impiden
   coincidencias. Los resultados se identifican por tipo.
3. Filtrar por mercado y categoría. Para ordenar por precio, seleccionar Productos.
4. Cambiar de página: se conservan búsqueda, mercado, categoría, tipo, orden y tamaño.
   En datos de prueba, usar tamano=1 permite comprobar páginas sin insertar productos.
5. Abrir un producto: el enlace lleva al catálogo de su tienda con el nombre buscado.
   Una categoría abre la consulta de productos; una tienda abre su ficha.
6. Buscar algo inexistente, probar filtros inválidos y verificar mensajes controlados.
7. Iniciar sesión con una cuenta existente y comprobar que el panel y su menú siguen
   funcionando. Desde el portal público usar Mi panel.
8. Revisar a 390 y 768 px, navegación con teclado, etiquetas, foco e imágenes de reemplazo.

Agregar al carrito permanece deshabilitado. No se incorpora registro de compradores,
perfil, direcciones, pedidos, pagos, login nuevo ni un flujo simulado de checkout.
Las pantallas y operaciones del cliente se conectarán en los siguientes bloques.

## Datos no disponibles

La portada conserva navegación y devuelve HTML 200 con un aviso si el proveedor
público no está disponible; no inventa tiendas o precios. El buscador y la API
responden 503 controlado cuando no pueden consultar datos. Una búsqueda sin
coincidencias devuelve 200 con estado vacío. Parámetros inválidos devuelven 400.

Si aparece el aviso, comprobar el modo de arranque, las tiendas publicadas y el
estado de la conexión local. No confundir un proveedor ausente con cero resultados.

## Cambios compartidos acotados

- TiendasRegistro.cs incorpora el registro del adaptador y servicio de exploración.
- Views/Home/Index.cshtml sustituye solo el contenido del visitante por el componente
  de portada y selecciona el layout público; el bloque autenticado se conserva.
- _LayoutPublico.cshtml actualiza logo, Inicio y formulario de búsqueda global,
  e incorpora explorar.css y explorar.js.

No se alteran los controladores ni mantenimientos del Programador A. Si tu copia
incluye cambios posteriores en estos tres archivos, integrar su contenido en vez
de reemplazar esos cambios sin revisarlos.

## Evidencias

Copiar el TRX real a docs/evidencias/ciclo3 y conservar capturas de portada,
búsqueda, paginación, estado vacío y vista móvil. No hay capturas de la aplicación
.NET nueva ni TRX generado en este paquete: deben obtenerse en Windows.
