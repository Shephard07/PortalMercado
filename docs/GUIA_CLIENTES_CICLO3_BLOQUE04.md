# Ciclo 3 — Bloque 04: perfil de cliente y direcciones

## Funcionalidad preparada

- Rutas privadas MVC `/Cliente/Perfil`, `/Cliente/Direcciones`, `/Cliente/Nueva`, `/Cliente/Editar?id=...` y `/Cliente/Eliminar?id=...`.
- Edición de apellidos, celular y par tipo/número de documento. Nombre y correo proceden de la cuenta de acceso y no son editables aquí.
- Direcciones con etiqueta, dirección, referencia, distrito, provincia, departamento y coordenadas opcionales.
- Crear y editar mediante POST con validación; eliminación con página de confirmación; dirección principal mediante POST independiente.
- Nunca acepta propietario, estado activo ni principal como campos de edición. El identificador de dirección se contrasta con las direcciones activas del usuario autenticado.
- Acceso con sesión existente; una cuenta administrativa tampoco sustituye la propiedad del cliente.
- DTO privados, vistas con codificación HTML, protección CSRF, páginas sin caché y mensajes sin detalles de infraestructura.
- Enlaces **Mi cuenta** en el portal público autenticado y **Perfil de cliente y direcciones** en los datos de acceso.
- Navegación posterior a cambios mediante POST/redirect/GET, con avisos breves sin incluir datos personales en TempData.

## Persistencia pendiente: comportamiento esperado ahora

Este paquete prepara servicios, vistas y pruebas con dobles. **No implementa todavía persistencia SQL de clientes o direcciones.** Mantiene la restricción de los requerimientos de no inventar ni alterar contratos de base de datos.

El proveedor predeterminado `PerfilDireccionesPendiente` responde 503. Con una cuenta existente podrás abrir **Mi cuenta**, pero verás el aviso de perfil no disponible hasta conectar el proveedor aprobado. No se asigna un cliente ficticio a una cuenta existente ni se mezclan los datos comerciales con los personales.

Los formularios completos y las operaciones correctas se comprueban en las pruebas HTTP con repositorios exclusivos de Tests. No hay modo de demostración ni botón de habilitación que simule guardar en la aplicación real.

Para habilitar esta parte se necesitan implementaciones aprobadas de:

- `IRepositorioPerfilCliente`: obtener por usuario y actualizar únicamente los cuatro campos admitidos con auditoría en la misma transacción.
- `IRepositorioDireccionesCliente`: listar por usuario y operaciones que vuelvan a verificar propiedad/estado dentro de la transacción de escritura.

Se registran mediante DI en lugar de `PerfilDireccionesPendiente`. Esto no autoriza DDL ni presupone procedimientos SQL existentes. Se conservan la solicitud de contrato y las dependencias documentadas en los bloques anteriores.

## Contrato de dirección principal y eliminación

`DireccionClienteServicio` realiza **una sola llamada** a `MarcarPrincipalPorUsuarioAsync`. No ejecuta dos escrituras independientes para desmarcar y marcar.

El adaptador aprobado deberá, dentro de una sola transacción y con bloqueo que serialice las mutaciones del mismo cliente:

1. Resolver Cliente desde el IdUsuario autenticado.
2. Revalidar propiedad y estado activo de la dirección seleccionada.
3. Desmarcar la anterior y marcar la elegida.
4. Registrar auditoría y confirmar todo junto; cualquier error revierte la operación.
5. Mantener como defensa el índice único existente para principal activa, sin crear ni modificar índices desde este módulo.

La comprobación previa en la aplicación no sustituye la revalidación transaccional. Creación, eliminación y marcado concurrentes deben participar del mismo control por cliente. Ante conflicto se propaga 409 y la web pide recargar, sin afirmar éxito.

Al crear, la web no solicita principal; por ahora la selección es explícita. Se permite **cero o una principal activa**. Al eliminar la principal, el contrato debe desactivarla y quitar principal atómicamente; no elige otra por sorpresa. La selección posterior se realiza desde la lista.

Las pruebas nuevas verifican propiedad, validaciones y delegación en una sola llamada. **No prueban aislamiento, rollback ni concurrencia de SQL real**: esas evidencias dependen del adaptador aprobado y siguen pendientes.

## Aplicación del paquete

1. Detener la aplicación y guardar el trabajo.
2. Copiar el contenido de la carpeta `PortalMercado` sobre la raíz de la solución conservando su estructura y reemplazando los archivos incluidos.
3. Requiere los bloques 01, 02 y 03 del ciclo 3.
4. No ejecutar migraciones ni scripts SQL. No cambia la conexión local de tiendas.
5. No sobrescribe `RegistroClienteServicioTests.cs` ni sus correcciones.

## Pruebas desde Git Bash

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~Clientes" --logger "trx;LogFileName=pruebas-ciclo3-clientes-bloque04.trx"
```

Se agregan 49 casos (27 de servicio y 22 HTTP) a los 203 anteriores: se esperan **252** si no hubo otros cambios. Confirmar el resultado real.

Regresión de acceso existente:

```bash
dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --filter "FullyQualifiedName~MfaWebTests|FullyQualifiedName~WebIntegrationTests|FullyQualifiedName~AccessTests" --logger "trx;LogFileName=pruebas-ciclo3-regresion-acceso-bloque04.trx"
```

Estos nuevos casos usan repositorios de prueba. No requieren SQL ni variables de activación de pruebas SQL. No se incluye un TRX que simule una ejecución.

Este entorno no tiene SDK .NET: aquí se revisaron sintaxis léxica, delimitadores y el contenido del paquete. La compilación C#, Razor y ejecución deben confirmarse en Windows.

## Comprobación manual disponible

```bash
Tiendas__SqlLocal=true Tiendas__Demostracion=false Tiendas__NegocioLocalDemostracion= dotnet run --project PortalMercado.Web --launch-profile https
```

- En incógnito, abrir `https://localhost:7275/Cliente/Perfil`: debe pedir iniciar sesión y conservar el destino.
- Tras acceder con una cuenta existente, mientras falte el contrato se espera la página de perfil no disponible (503), con enlaces de regreso. No se espera un perfil creado automáticamente.
- Confirmar que `/explorar` sigue mostrando el catálogo SQL y conserva el retorno de login validado en el bloque 03.

Cuando haya un adaptador aprobado: comprobar creación, edición, eliminación y elección de principal con dos cuentas cliente distintas y en móvil de 390 px. Las capturas con nombres, documentos, teléfonos y direcciones reales deben evitarse; usar cuentas y datos de prueba.

## Próximo trabajo

Consentimientos y preparación de acciones futuras de compra, sin crear pedidos ni pagos. Continúan pendientes el alta y perfil persistentes, contratos de direcciones y las evidencias de concurrencia/rollback SQL. Este bloque no declara cerrado el ciclo 3.
