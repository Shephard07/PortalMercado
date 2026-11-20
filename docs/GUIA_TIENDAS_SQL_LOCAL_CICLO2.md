# Tiendas conectadas a SQL local · Bloque 07

Adaptación solicitada expresamente por el usuario el 12 de noviembre de 2026,
al trabajar individualmente sin contrato ni base nueva de compañeros.
Se limita a PortalMercado_ET01_Test en SQLEXPRESS del mismo equipo.
No es una publicación de cambios en la base central del proyecto.

## 1. Copiar el bloque

Detener Web/API y extraer el ZIP en la raíz de PortalMercado, reemplazando los
archivos incluidos. Se conservan el menú y las funciones de los bloques 01–06.
No se crean objetos SQL al iniciar la aplicación.

## 2. Instalar manualmente en SSMS

Seleccionar PortalMercado_ET01_Test y ejecutar cada archivo COMPLETO, por orden:

1. `database/ciclo2-local/01_Instalar_Tiendas_Local.sql`.
2. `database/ciclo2-local/02_Habilitar_Tienda_Prueba.sql`.
3. `database/ciclo2-local/03_Verificar_Tiendas_Local.sql` para consultar el estado.

El primero instala en una transacción una tabla auxiliar, función de alcance,
trigger de versión, vistas y procedimientos con prefijo pm_TiendaLocal.
Los scripts comprueban base, instancia y equipo. No cambian contraseñas,
credenciales ni asignaciones de roles. Si falla la instalación, se revierte.

El segundo habilita exclusivamente el negocio ficticio `negocio-prueba-et01`
del Mercado de prueba ET01. Conserva los datos comerciales existentes y rellena
descripción/horario solo si estaban vacíos. No copia los cambios de los mocks.
Crea, si falta, el producto publicado `TDA-LOCAL-01` con precio e imagen propios,
sin alterar los productos CAT-PAG/CAT-LOCAL ni sus existencias.

También vincula el puesto ficticio P-ET01 si está disponible sin asignación
vigente. No reemplaza asignaciones ajenas. No incorpora mantenimiento de
puestos ni comerciantes a la aplicación. El script muestra ID y ruta pública.

Teléfono y correo permanecen privados; los negocios nuevos permanecen ocultos.
La aprobación de publicación se guarda explícitamente en TiendaPublicacionLocal.
Reejecutar el instalador no vuelve a ocultar tiendas ni reinicia sus perfiles.
Reejecutar el script de datos habilita de nuevo el negocio de prueba y renueva
su versión al actualizarlo; no ejecutarlo con formularios abiertos pendientes.

## 3. Ejecutar las pruebas de tiendas

Desde Git Bash:

```bash
PM_RUN_TIENDAS_SQL_TESTS=1 dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Tiendas" --logger "trx;LogFileName=pruebas-ciclo2-tiendas-bloque07.trx"
```

Se esperan 167 casos: los 148 anteriores, 10 de configuración y 9 SQL locales.
Sin la variable nueva, los 9 SQL quedan omitidos deliberadamente.
Los casos SQL requieren el vendedor ficticio y el negocio ajeno de los datos
08 del Ciclo 1, ya utilizados en la suite anterior.

Las pruebas de escritura restauran los campos del perfil al terminar, pero
conservan eventos de auditoría de prueba y restauración. Las versiones cambian.
Los casos que crean datos/trigger de fallo lo hacen en transacciones revertidas.
No se borran usuarios ni productos existentes al finalizar las pruebas.

La suite completa, habilitando también las pruebas SQL previas:

```bash
PM_RUN_SQL_TESTS=1 PM_RUN_CATALOGO_SQL_TESTS=1 PM_RUN_PRODUCTOS_SQL_TESTS=1 PM_RUN_CATALOGO_FIXTURE_TESTS=1 PM_RUN_CATALOGO_PAGINACION_TESTS=1 PM_RUN_TIENDAS_SQL_TESTS=1 dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --logger "trx;LogFileName=pruebas-ciclo2-solucion-sql-local.trx"
```

Se esperan 350 casos en total. La última suite confirmada antes de este bloque
fue de 331 correctas; el código y SQL nuevos requieren ejecución en el equipo
Windows, porque el entorno de elaboración no tiene SDK .NET ni SQL Server.

## 4. Iniciar la Web conectada a SQL

Usar la conexión local de Windows que ya utiliza el proyecto. Debe indicar
Server=.\SQLEXPRESS, Database=PortalMercado_ET01_Test e Integrated Security=True.
La configuración SQL local rechaza otra base, servidor remoto y producción.
También comprueba la instancia real al abrir cada conexión.

```bash
Tiendas__SqlLocal=true Tiendas__Demostracion=false Tiendas__NegocioLocalDemostracion= dotnet run --project PortalMercado.Web --launch-profile https
```

El último valor vacío elimina el vínculo ficticio del bloque 05 para esta
sesión. No guardar contraseñas ni cadenas con credenciales en archivos del repo.

Si la conexión de la Web todavía apunta a otra base, establecerla con user-secrets:

```bash
dotnet user-secrets set "ConnectionStrings:PortalMercado" 'Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;' --project PortalMercado.Web
```

La API puede iniciarse de igual forma cambiando el proyecto a PortalMercado.API,
con su conexión local configurada. Web y API ahora comparten los datos SQL.
Para volver al modo sin proveedor, desactivar SqlLocal y Demostracion: responderá
503 controlado; no se elimina la información guardada en SQL.

## 5. Comprobar persistencia

1. Iniciar sesión con una cuenta autorizada del negocio local.
2. Abrir Mi tienda y el perfil del Negocio de prueba ET01. El ID real aparece en
   el resultado del script; ya no se usa el ID ficticio público 1001.
3. Cambiar descripción y horario; guardar y abrir la tienda pública.
4. Detener la Web con Ctrl+C, iniciarla con el mismo comando y abrir de nuevo
   `/tiendas/negocio-prueba-et01`: los valores deben conservarse.
5. Ejecutar el script 03: debe aparecer TIENDA_PERFIL_ACTUALIZADO con actor,
   negocio y fecha, sin contenidos del formulario.
6. Abrir dos pestañas del perfil. Guardar primero en una y luego en la otra:
   el segundo guardado debe devolver conflicto sin sobrescribir.
7. Con el vendedor local, intentar abrir el perfil del negocio ajeno: 403.
   Los contactos privados y los datos fiscales no deben aparecer en público.

El nombre Frutería El Sol y el slug fruteria-el-sol correspondían al mock.
En modo SQL se consultan los negocios reales de la copia local.

## Reglas locales implementadas

- Publicar exige Estado ACTIVO, mercado activo y Visible=1 explícito.
- Se conserva el índice de slug por mercado. Un slug repetido en otro mercado
  no se publica en el directorio y obtiene 404 en la ruta pública.
- Categorías públicas derivadas de productos PUBLICADO con categoría activa.
- Precio: última fila de ProductoPrecio vigente en UTC, con inicio inclusivo y
  fin exclusivo; si no hay fila vigente, se usa Producto.Precio del Ciclo 1.
- Imagen: principal por Orden/IdImagen; URL insegura o fallida usa reemplazo.
- Stock: Existencia menos Reservado; sin fila de inventario equivale a cero
  para productos con control de stock, y sin control se devuelve null.
- Ubicación: fecha de Lima, Desde/Hasta inclusivos, mercado/sector activos y
  puesto DISPONIBLE u OCUPADO. No se expone la dirección fiscal.
- Perfil: lista cerrada de campos; permiso PRODUCTO_GESTIONAR más el contexto
  NEGOCIO_CONTEXTO, con pertenencia al negocio. SQL revalida actor y alcance
  antes de escribir. No se modifican consentimiento, slug, estado ni mercado.
- Guardado, renovación de versión y auditoría son atómicos. El trigger también
  invalida versiones cuando otro escritor cambia Negocio.
- Version sigue siendo Guid, ahora guardado en SQL. Los contactos varchar se
  validan contra su capacidad y codificación para evitar truncamiento.
- Permisos y publicación son reglas de esta adaptación local, no un contrato
  central aprobado por compañeros. No se despliega en producción.

## Objetos añadidos

Tabla TiendaPublicacionLocal; función pm_TiendaLocal_PuedeEditar;
trigger tr_TiendaLocal_Version sobre Negocio; vistas pm_TiendaLocal_Datos,
pm_TiendaLocal_Visibles y pm_TiendaLocal_Productos; procedimientos
pm_TiendaLocal_Opciones/Listar/Buscar/Ubicaciones/Catalogo/Perfil/Guardar.

No se alteran columnas ni índices de Negocio. Los cambios de perfil se registran
en la tabla Auditoria ya existente con Accion=TIENDA_PERFIL_ACTUALIZADO.
Los datos y objetos se mantienen al desactivar SqlLocal. La integración con una
base central futura deberá revisar estas decisiones y sustituir el proveedor.


## Corrección del 13 de noviembre · Guardado EF con trigger

El usuario confirmó 167 pruebas de tiendas correctas y la ficha pública SQL
actualizada. La suite completa produjo 340 correctas y 10 errores; el fragmento
compartido no contiene los mensajes principales de esas excepciones.
Al revisar se encontró que NegocioConfiguration seguía utilizando OUTPUT directo,
incompatible con el trigger añadido sobre Negocio. Las pruebas de administración
crean negocios mediante Entity Framework y recorren ese guardado.

Se configura UseSqlOutputClause(false) únicamente para dbo.Negocio, conservando
sus columnas, relaciones, identidad y restricciones. Esta configuración también
funciona si el trigger no está instalado. No se reinstala ni elimina SQL.
La prueba existente de negocio nuevo/slug ambiguo ahora inserta el negocio con
DbContext.SaveChangesAsync, comprueba ID/fecha generados y el registro auxiliar,
y revierte la transacción. Así cubre el camino de EF junto con el trigger real.

Copiar los archivos de la corrección sobre la solución con los hosts detenidos
y repetir la suite completa con las seis variables SQL activadas. El total
permanece en 350: se adaptó un caso existente. No hay SDK .NET ni SQL Server en
el entorno de elaboración; la nueva ejecución queda pendiente en Windows.
Si quedan errores, se necesita el nombre y mensaje de la primera prueba fallida,
no solo el final del seguimiento de pila, para confirmar la causa restante.

Referencia del proveedor SQL Server de EF Core:
https://learn.microsoft.com/en-us/ef/core/providers/sql-server/misc#savechanges-database-triggers-and-unsupported-computed-columns
