# Catálogo del Ciclo 1

## Configuración y ejecución local

Requiere el SDK indicado en `global.json` y SQL Server Express del mismo equipo,
con la copia `PortalMercado_ET01_Test`, Etapas 1 y 2 instaladas y los scripts de
`database/ciclo1` ejecutados por número. Los scripts locales comprueban la base,
la instancia y el equipo. Para DEV centralizado, coordinar instalación y permisos
con el DBA.

Desde la raíz de la solución en Git Bash:

```bash
ConnectionStrings__PortalMercado='Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;' dotnet run --project PortalMercado.Web --launch-profile https
```

La Web abre en `https://localhost:7275`. Para iniciar la API, usar otra terminal:

```bash
ConnectionStrings__PortalMercado='Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;' dotnet run --project PortalMercado.API --launch-profile https
```

La API escucha en `https://localhost:7161`. Cada host utiliza su propio inicio de
sesión. Las contraseñas de las cuentas de prueba se conservan de la preparación
local. Para conexiones con credenciales, usar user-secrets o configuración segura.
No incluir contraseñas ni tokens en los archivos de configuración versionados.

## Uso del módulo

| Pantalla | Operación |
| --- | --- |
| Categorías | Buscar por nombre o slug, filtrar por estado, crear, editar y activar o desactivar. Indicar el ID de padre para jerarquías. |
| Unidades | Consultar ID, código, nombre y si admite fracciones. |
| Productos | Seleccionar un negocio autorizado y filtrar por categoría, estado, nombre o SKU. Cambiar de página con Anterior y Siguiente. |
| Nuevo producto / Editar | Indicar categoría y unidad existentes, nombre, precio y control de stock. SKU y descripción son opcionales. El alta crea un borrador. |
| Detalle | Abrir el nombre del producto para consultar precio, historial e inventario. Disponible equivale a existencia menos reservado. |
| Imágenes | Registrar URL HTTPS directa, orden y principal. Al seleccionar otra principal, se desmarca la anterior. |
| Estado del producto | Elegir uno de los destinos habilitados y pulsar Cambiar estado. La matriz local está en el documento de contratos. |

Los enlaces de gestión dependen de los permisos y cada operación comprueba el
negocio en servidor. Para consultar productos con un usuario restringido en la
copia local, el script 08 habilita a `vendedor.et02@example.invalid` mediante el
rol auxiliar `CATALOGO_PRUEBA_LOCAL`.

## Datos y prueba de paginación

Ejecutar `database/ciclo1/09_Datos_Prueba_Paginacion.sql` en SSMS. Agrega 25
productos ficticios `CAT-PAG-01` a `CAT-PAG-25` al negocio ET01, dos categorías y
sus precios iniciales. Sus estados alternan BORRADOR y SUSPENDIDO para probar
filtros. Una nueva ejecución inserta los registros que faltan y conserva los
existentes. Mantener este conjunto intacto durante las pruebas.

El resultado SQL devuelve los IDs de negocio y categorías A y B. Con la sesión
del vendedor o administrador, seleccionar ese negocio y comprobar:

| Acción | Resultado esperado sobre los datos del script 09 |
| --- | --- |
| Buscar `CAT-PAG-` | Total 25, orden 01 a 25, con 20 productos en página 1 y 5 en página 2. |
| Pulsar Siguiente y Anterior | Se conserva la búsqueda y se recorren las dos páginas sin duplicados. |
| Buscar `CAT-PAG-`, categoría A, estado BORRADOR | Total 8, SKU impares entre 01 y 15. |
| Buscar `CAT-PAG-`, categoría B, estado SUSPENDIDO | Total 5, SKU 16, 18, 20, 22 y 24. |
| Buscar `Producto de paginación local 25` | Una coincidencia, SKU CAT-PAG-25. |
| Buscar `CAT-PAG-%` | Sin coincidencias. El porcentaje se busca como texto literal. |

## Pruebas automatizadas

Detener la Web y la API antes de compilar. Desde Git Bash en la raíz, después de
instalar los scripts 08 y 09:

```bash
PM_RUN_CATALOGO_SQL_TESTS=1 PM_RUN_PRODUCTOS_SQL_TESTS=1 PM_RUN_CATALOGO_FIXTURE_TESTS=1 PM_RUN_CATALOGO_PAGINACION_TESTS=1 dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Catalogo" --logger "trx;LogFileName=pruebas-ciclo1-catalogo.trx"
```

El catálogo contiene 39 casos. Las tres pruebas de paginación verifican páginas,
orden y total, filtros combinados antes de paginar, búsqueda literal y separación
por negocio. La ejecución completa de la solución compartida el 10/11/2026
finalizó con 183 pruebas aprobadas, cero errores y cero omitidas, incluyendo estos
casos con las opciones SQL habilitadas.

Para la regresión de toda la solución, incluidas las pruebas SQL de Etapas 1 y 2:

```bash
dotnet build PortalMercado.sln -c Release
PM_RUN_SQL_TESTS=1 PM_RUN_CATALOGO_SQL_TESTS=1 PM_RUN_PRODUCTOS_SQL_TESTS=1 PM_RUN_CATALOGO_FIXTURE_TESTS=1 PM_RUN_CATALOGO_PAGINACION_TESTS=1 dotnet test PortalMercado.sln -c Release --no-build --logger "trx;LogFileName=pruebas-ciclo1-solucion.trx"
```

Conservar los TRX generados en `PortalMercado.Tests/TestResults` y las capturas
del recorrido de páginas. Registrar fallos con el nombre de la prueba y el error.
Un resultado esperado se convierte en evidencia cuando se ejecuta y se conserva
su salida.

## Evidencia recibida

| Comprobación | Resultado compartido |
| --- | --- |
| Suite del catálogo hasta Bloque 09 | 36 aprobadas, 0 errores y 0 omitidas. |
| Inventario en SQL y ficha | Existencia 12.500, reservado 2.250 y disponible 10.250 KG. |
| Producto ajeno con negocio propio | Mensaje de producto inexistente en ese negocio. |
| Consulta y edición del negocio 90 con vendedor | Ambas rechazadas con mensaje de acceso al catálogo denegado. |
| Auditoría del cambio de estado de MANGO-01 | BORRADOR → PUBLICADO → SUSPENDIDO → BORRADOR, persistido en la copia local. |
| Filtro por categoría B, ID 3 | La captura muestra los diez productos CAT-PAG-16 a CAT-PAG-25. |
| Paginación con búsqueda CAT-PAG- | Capturas de ambas páginas, con 20 y 5 productos, total 25 y búsqueda conservada. |
| Regresión completa en Release del 10/11/2026 | Salida compartida con 183 aprobadas, 0 errores, 0 omitidas y compilación correcta. Pruebas 9.9 s, compilación y ejecución 19.7 s. |

Las capturas de rechazo muestran el comportamiento de la interfaz. Las pruebas
automatizadas comprueban los códigos y las condiciones de sus casos respectivos.
La salida de pruebas fue compartida en la conversación. El archivo TRX completo
permanece en el equipo de ejecución y debe incorporarse a la entrega.

Las capturas recibidas se incluyen en `docs/evidencias/ciclo1`:

| Archivo | Comprobación |
| --- | --- |
| `inventario.png` | Cantidades de existencia, reserva y disponible en la ficha. |
| `acceso-ajeno-detalle.png` | Consulta denegada al negocio ajeno. |
| `acceso-ajeno-edicion.png` | Edición denegada al negocio ajeno. |
| `auditoria-estados.png` | Tres cambios de estado persistidos. |
| `paginacion-primera-inicio.png` | Búsqueda CAT-PAG- y primeras filas. |
| `paginacion-primera-fin.png` | Final de página 1 y total de 25. |
| `paginacion-segunda.png` | Cinco productos en página 2 y búsqueda conservada. |

Copiar el TRX desde la raíz de la solución en Git Bash:

```bash
cp PortalMercado.Tests/TestResults/pruebas-ciclo1-solucion.trx docs/evidencias/ciclo1/
```

## Dependencias para integrar y entregar

| Pendiente | Condición de cierre |
| --- | --- |
| Contratos SQL de DEV | El DBA revisa y versiona parámetros, resultados y errores de los procedimientos locales para el ambiente centralizado. |
| Permisos | El DBA define permisos de ejecución del usuario de servicio y la matriz de roles del catálogo. El rol auxiliar local sirve para las pruebas. |
| Publicación RF-B09 | Coordinación aprueba transiciones y datos mínimos de la propuesta local. |
| Precio e historial RF-B07 | El DBA valida el contrato transaccional, concurrencia y ausencia de intervalos superpuestos en DEV. |
| Servicio de imágenes | Confirmar servicio y procedencia autorizados para las URLs de DEV. |
| Integración RF-B12 / CA-08 | Compilar y probar junto a la versión del módulo A en la rama de integración. |
| Entrega Git | Revisar cambios en programador-B/Catalogo, realizar commit y push y abrir el pull request de integración. |

Los endpoints y contratos utilizados están descritos en
`docs/CONTRATOS_CATALOGO_CICLO1.md`. Esta guía registra las comprobaciones locales
y los pendientes de integración.

## Preparación de la entrega Git

El paquete consolidado contiene la versión actual de los archivos del catálogo
de los bloques 01 a 10. Se aplica sobre la solución con Etapas 1 y 2. Incluye
`DependencyInjection.cs` y `_Layout.cshtml`, que son archivos compartidos y deben
compararse con la rama de integración para preservar aportes de otros módulos.
En un equipo que ya recibió los diez bloques, esta consolidación actualiza la
guía y agrega las capturas.

Antes de crear el commit, revisar el estado del repositorio local:

```bash
git branch --show-current
git status --short
git diff --stat
```

La rama de trabajo registrada es `programador-B/Catalogo`. El listado de cambios
permite seleccionar los archivos de esta entrega y revisar los cambios compartidos.

Para el pull request, usar el título «Implementar catálogo del Ciclo 1».
Describir categorías, unidades y productos por negocio, imágenes, historial,
inventario de consulta y estados. Indicar el resultado local de 183 pruebas
aprobadas y las dependencias de DEV descritas en esta guía. La comprobación
con el módulo A y la aprobación de contratos y reglas corresponden a la integración.
