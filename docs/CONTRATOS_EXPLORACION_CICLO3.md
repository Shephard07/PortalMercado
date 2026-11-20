# Contratos de exploración pública del Ciclo 3

Bloque 02 · 19 de noviembre de 2026.

## Arquitectura

ExploracionPublicaServicio consume IRepositorioExploracionPublica. La adaptación
ExploracionDesdeTiendas reutiliza ITiendaService y CatalogoTiendaServicio y, por
ellos, los repositorios públicos del Ciclo 2. No consulta entidades privadas,
modifica DDL o crea procedimientos nuevos. Se registra mediante AddTiendas en
Web y API. El registro es de lectura y no activa todavía los servicios del cliente.

La búsqueda filtra antes de contar, ordenar y paginar. Una categoría, un producto
y una tienda mantienen su tipo e identidad; IDs coincidentes entre tipos no se
interpretan como el mismo resultado. El orden por nombre se desempata por tipo
luego ID; los productos por precio se desempatan por nombre e ID.

## Rutas implementadas

| Método y ruta | Resultado |
| --- | --- |
| GET /explorar | Portada pública independientemente de la sesión |
| GET /explorar/buscar | Resultados MVC y opciones de filtros |
| GET /api/publico/mercados | Mercados con tiendas públicas visibles del proveedor actual |
| GET /api/publico/buscar | Filtro normalizado, página y opciones públicas |

La raíz / conserva el panel si la cuenta está autenticada; para visitantes muestra
la portada mediante PortadaPublicaViewComponent. Esta decisión evita modificar
login/MFA y sus destinos durante el bloque de exploración. /explorar siempre es
público. La búsqueda de módulos del panel sigue siendo independiente de la pública.

## Parámetros

- q: hasta 200 caracteres, se recortan espacios y se rechazan controles.
- mercadoId y categoriaId: positivos si se especifican.
- tipo: todos, productos, tiendas o categorias.
- orden: nombre; precio-asc y precio-desc solo con tipo=productos.
- pagina: 1 a 100000; tamano: 1 a 100, predeterminado 12.

La búsqueda ignora mayúsculas y tildes en español. Los productos pueden coincidir
por nombre de producto, categoría o tienda. Las tiendas coinciden por su nombre
o categoría publicada; los resultados categoría coinciden por el nombre de categoría.
Los filtros de mercado y categoría acotan las coincidencias antes de la paginación.
Categorías y mercados inexistentes producen conjunto vacío, no datos de otro ámbito.

Los destinos se construyen en servidor con slug validado e IDs numéricos. El nombre
buscado en un enlace de producto se codifica como parámetro, no como ruta. Razor
codifica títulos y valores de formulario; no se utiliza Html.Raw ni innerHTML para
resultados de usuario. Imágenes HTTPS sin credenciales; reemplazo local en carga
fallida o URL descartada. MIME/tamaño remoto sigue siendo un pendiente del Ciclo 2.

## Datos públicos y límites de este adaptador

La proyección de búsqueda contiene nombres comerciales, mercado, categoría,
precio, unidad, disponibilidad y destinos públicos. No contiene Cliente, Usuario,
correo, teléfono, dirección fiscal o campos de autenticación. Los productos se
obtienen mediante el catálogo que ya filtra negocio y PUBLICADO. El servicio
comprueba que pertenezcan a tiendas del conjunto, identidades únicas y metadatos
coherentes; descarta cantidades de stock para productos sin control.

Los mercados se derivan de las tiendas visibles y las categorías de sus productos
publicados. No se afirma que aparezcan todos los mercados activos sin tiendas:
esa lectura global de mercados y categorías requiere el contrato compartido de A/DBA.

La adaptación actual recopila secuencialmente páginas públicas de 100 elementos.
No paraleliza el DbContext ni introduce una caché que conserve visibilidad retirada.
Admite como máximo 100 tiendas y 2000 productos en total y un presupuesto de lectura
de 10 segundos. Si se supera, el proveedor devuelve 503, NO un total parcial o
resultados truncados. Los cambios detectados entre páginas, duplicados y desaparición
de una tienda también impiden presentar una lectura incompleta como válida.

No se garantiza una instantánea transaccional entre todas las consultas: una
actualización concurrente que no altere conteos puede producir datos de momentos
próximos. Para producción se necesita una búsqueda global paginada del DBA,
proyección consistente de lectura, índices/permisos aprobados y pruebas de carga.
La implementación actual permite continuar en demostración y copia local existente;
no se presenta como motor de búsqueda escalable ni contrato central aprobado.

## Interfaz y accesibilidad

Se reutilizan cabecera, colores y recursos del prototipo público, retirando los
flujos de pago, delivery y checkout fuera del alcance. Portada con mercados,
categorías, muestra de ocho productos y seis tiendas; la búsqueda mantiene el
conjunto completo soportado por el adaptador. Los límites de muestra no afectan
su total. Los catálogos sin elementos y las consultas no disponibles tienen estados
diferenciados. Se muestra estado de envío al aplicar filtros; la búsqueda sigue
funcionando sin JavaScript mediante GET.

Los controles tienen etiquetas, enlace para saltar al contenido y foco visible.
CSS define adaptación para 1000, 760 y 480 px. La revisión visual real a 390/768 px
y la comprobación con teclado deben realizarse en la Web antes de declarar CA-10
completo. Este entorno no ejecuta .NET ni acredita esa revisión mediante un mock.

## Pruebas y aceptación

57 casos nuevos preparados: 34 de servicio, 4 de adaptación y 19 HTTP MVC/API.
Cubren tipos, búsqueda con tildes, filtros/total/página, precio, privacidad, destinos,
contratos incoherentes, límites, cancelación, codificación y errores controlados.
No se declara que hayan pasado hasta ejecutarlos en Windows. Se conservan los
110 casos de base confirmados por el desarrollador. La regresión del menú y MFA
se solicita expresamente en la guía.

RF-B3-01/02/03 tienen implementación de integración local con los límites anteriores.
RF-B3-13 y CA-10 requieren revisión visual de viewport. Registro, perfil, direcciones,
consentimientos y acceso autenticado a compra futura no se declaran terminados.

## Solicitudes adicionales al DBA y Programador A

- Contrato público para mercados activos y categorías incluso sin tiendas/productos.
- Búsqueda global autorizada y paginada por producto, tienda y categoría.
- Reglas de normalización, comparación de texto, precio vigente e imagen principal.
- Consistencia ante cambios de visibilidad y estado, límites y permisos mínimos.
- Datos de pruebas multi mercado y autorización para pruebas de carga.

No se crean SP o índices por cuenta propia y no se envía esta solicitud automáticamente.
