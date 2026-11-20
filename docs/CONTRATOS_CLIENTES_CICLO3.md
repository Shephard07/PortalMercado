# Contratos de clientes del Ciclo 3

Bloque 01 · 17 de noviembre de 2026 · Base de integración.

Este bloque añade contratos, validadores, orquestación del registro y servicio de
perfil propio. No incorpora pantallas, endpoints, registro de dependencias en los
hosts ni un proveedor de persistencia. No ejecuta SQL y no da por aprobado un
contrato del DBA. La Web de ciclos anteriores conserva su comportamiento.

## Alcance implementado

- `RegistroClienteSolicitud`, `ClientePerfilDatos`, `ClienteDireccionDatos` y
  `ClienteConsentimientoDatos`: modelos cerrados frente a miembros JSON ajenos.
- `IClienteService`, `IDireccionService` e `IConsentimientoService`: operaciones
  privadas basadas en credencial y host, no en el IdCliente enviado por navegador.
- Repositorios separados de registro, perfil, direcciones y consentimientos.
- `RegistroClienteServicio`: valida correo y contraseña mediante CredentialPolicy,
  confirmación, campos de entrada, versiones vigentes y aceptaciones necesarias.
  Entrega un solo comando con hash al repositorio que deberá garantizar atomicidad.
- `HashPasswordCliente`: adapta IPasswordHasher<Usuario>, ya usado por autenticación
  y recuperación. No implementa un algoritmo nuevo ni cambia sus iteraciones.
- `PerfilClienteServicio`: valida la sesión con IAuthService, obtiene el usuario
  autenticado y limita consulta y escritura al perfil de ese mismo usuario.
- `ClientesValidacion`: límites, documento opcional completo, coordenadas e IP.
- `DestinoCliente.Normalizar`: conserva destinos locales y rechaza externos,
  caracteres de control, barras invertidas y variantes codificadas.

Direcciones y consentimientos tienen contratos y validaciones de entrada; sus
servicios operativos, repositorios y pantallas se implementarán en bloques
posteriores. La política de retorno aún no se conecta al login ni al flujo MFA.

## Modelo contrastado con el esquema original

Referencia de lectura: database/local/01_Esquema_Local.sql. No se modificó ese
archivo. Su presencia no acredita que la base del destinatario tenga esos objetos
instalados ni que sus permisos o procedimientos sean los definitivos.

| Campo | Capacidad del esquema |
| --- | --- |
| Cliente.Apellidos | nvarchar(150), opcional |
| Cliente.Celular | varchar(30), opcional |
| Cliente.TipoDocumento | varchar(4), opcional |
| Cliente.NumeroDocumento | varchar(20), opcional |
| ClienteDireccion.Etiqueta | nvarchar(80), opcional |
| ClienteDireccion.Direccion | nvarchar(300), obligatorio |
| ClienteDireccion.Referencia | nvarchar(250), opcional |
| Distrito / Provincia / Departamento | nvarchar(120), opcionales |
| Latitud / Longitud | decimal(10,7), opcionales |
| Consentimiento.Tipo / VersionDocumento | varchar(40) |
| Consentimiento.IP | varchar(45), opcional |

El esquema declara Cliente.IdUsuario único e índice único filtrado
UX_ClienteDireccion_Principal sobre IdCliente cuando Principal=1 y Activa=1.
Este índice asegura como máximo una dirección principal activa; no garantiza por
sí solo que exista una ni sustituye la transacción del cambio de principal.

Las coordenadas se envían juntas y admiten hasta siete decimales. No se inventa
un catálogo de tipos de documento o validación oficial de DNI. Se exige que tipo
y número estén ambos informados o ambos vacíos. Los campos VARCHAR se limitan
conservadoramente a ASCII hasta acordar codificación y reglas con DBA.

## Contrato de registro atómico

`IRepositorioRegistroCliente.RegistrarAsync` recibe un `RegistroClienteAlta`
interno: nombre, correo de presentación, correo normalizado, teléfono, hash de
identidad, consentimientos y contexto creado en servidor.

El proveedor aprobado deberá realizar en UNA transacción:

1. Comprobar unicidad del correo normalizado también bajo solicitudes simultáneas.
2. Crear Usuario y el estado de seguridad que exige la autenticación existente.
3. Crear Cliente asociado al nuevo IdUsuario único.
4. Registrar aceptaciones y rechazo u opción promocional según documentos vigentes.
5. Registrar auditoría y confirmar todos los cambios.

Cualquier fallo debe revertir todo el alta, incluso si ya se insertó Usuario.
No se puede adjuntar un Cliente a una cuenta ya existente sin un flujo autenticado
expreso. No asignar roles administrativos, de negocio ni permisos elevados.
El IdUsuario=0 del contexto de alta significa visitante anónimo; no se debe insertar
como FK de auditoría. El proveedor resolverá el nuevo actor o un actor nulo según
el contrato real, conservando correlación y fecha de la operación.

`NoDisponible` representa un rechazo genérico, incluido correo repetido. No se
hace una consulta pública previa de existencia y no se exponen IDs de cuentas.
`RequiereConfirmacionCorreo` lo decide el proveedor aprobado; este bloque no crea
tokens, correos de confirmación o sesiones automáticas.

El servicio orquesta una única llamada, pero NO demuestra rollback o unicidad
SQL: esas pruebas requieren el proveedor de integración aún pendiente.

## Autenticación y perfil propio

El controlador futuro extraerá la credencial existente del contexto autenticado.
No vinculará actor, host, IdUsuario, IdCliente o metadatos desde JSON/formulario.
PerfilClienteServicio valida la sesión y consulta por su IdUsuario. También
rechaza un repositorio que devuelva un perfil de otra cuenta. Una cuenta de
administración sin perfil Cliente recibe rechazo; su rol no la convierte en
comprador ni permite leer perfiles ajenos.

El DTO editable solo contiene Apellidos, Celular, TipoDocumento y NumeroDocumento.
Correo, contraseña, nombre de identidad, rol y estado quedan fuera. Los cambios de
correo usarán el flujo de seguridad que se acuerde; no se modifica Usuario desde
este servicio. El repositorio deberá revalidar propiedad en el mismo guardado y
auditarlo atómicamente.

## Documentos y consentimiento

`DocumentosClienteVigentes` se debe construir con versiones aprobadas y textos
publicados por el responsable. No contiene valores productivos predeterminados.
Las versiones `test-*` aparecen exclusivamente en pruebas; no son términos reales.

- Términos y privacidad requieren aceptación expresa y versión vigente.
- Promociones es opcional y por defecto falso. Si existe versión promocional,
  el comando registra por separado true/false; si no existe, no inventa un evento.
- Una aceptación promocional sin documento disponible se rechaza.
- Las versiones recibidas se contrastan con las del servidor.
- Fecha UTC, correlación e IP se establecen en servidor. Usar RemoteIpAddress y
  configurar proxies confiables antes de utilizar cabeceras reenviadas.
- El contrato de consentimiento posterior debe conservar historial y definir
  revocación o rechazo de consentimientos necesarios; no se da por resuelto aquí.

No registrar DTO, contraseña, hash, dirección completa, documento o IP en logs de
aplicación. La evidencia de IP se entrega exclusivamente al almacenamiento de
consentimiento aprobado. El resumen de auditoría usará identificadores y eventos.

## Direcciones y principal

Las escrituras reciben IdUsuario autenticado e IdDireccion como destino; el
repositorio debe comprobar propiedad y estado en servidor, dentro de la operación.
`Principal` y `Activa` no se pueden enviar en el DTO normal de dirección.
El cambio de principal tiene una operación separada y deberá serializarse por
cliente, limpiar la anterior y marcar la nueva en una sola transacción.

Pendientes de acuerdo: borrado físico o baja lógica; destino de la principal al
eliminarla; primera dirección automática o manual; obligatoriedad de distrito,
provincia y departamento; tratamiento del intento repetido de borrado.

## Próximos endpoints propuestos

Son planificación, no endpoints disponibles en este bloque:

- GET /api/publico/mercados y GET /api/publico/buscar.
- POST /api/clientes/registro.
- GET/PUT /api/mi-perfil.
- GET/POST /api/mis-direcciones y PUT/DELETE /api/mis-direcciones/{id}.
- PUT /api/mis-direcciones/{id}/principal.
- POST /api/mis-consentimientos y consulta privada conforme al contrato.

Los hosts aplicarán antiforgery/CSRF, HTTPS, cookies seguras y limitación de
intentos. El destino se preservará también al recorrer MFA y se ejecutará con
LocalRedirect/IsLocalUrl; el helper por sí solo no modifica la navegación actual.

## Pruebas y límite de la evidencia

Se prepararon 110 casos xUnit bajo PortalMercado.Tests.Clientes: validación,
privacidad de DTO, sesión y propiedad, retorno local, consentimiento y adaptación
del hash. Los repositorios de prueba son dobles unitarios sin SQL y sin cuentas
reales. No son un modo demostración conectado a Web/API.

No se ejecutaron ni se declara compilación correcta en este entorno: no hay SDK
dotnet ni SQL Server. Deben ejecutarse en Windows antes de continuar la integración.
La cifra de 350 corresponde a la última suite anterior, no es un resultado de
este bloque. Si la base de código local coincide, el filtro Clientes debería
descubrir 110 casos; cambios posteriores pueden variar ese total.

## Decisiones pendientes de identidad

SqlAuthService y SqlPasswordService ya usan IPasswordHasher<Usuario>. La política
actual exige 12–128 caracteres con mayúscula, minúscula, número y símbolo.
El login actual termina en Home y necesita integración del destino local.
Existe recuperación de contraseña, pero no se encontró un flujo completo de
confirmación de correo en la copia revisada. No se duplicará seguridad ni se
confundirá MFA con confirmación de correo.
