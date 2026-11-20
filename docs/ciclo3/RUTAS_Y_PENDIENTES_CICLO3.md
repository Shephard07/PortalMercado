# Rutas y pendientes — Ciclo 3

Corte: 20/11/2026. Fuentes: controladores actuales y requerimiento versión 1.0.

## Rutas MVC

| Método | Ruta | Comportamiento |
|---|---|---|
| GET | /explorar; /explorar/buscar | Público, reutiliza tiendas/catálogo local |
| GET/POST | /Registro | Alta temporal si Clientes:Demostracion=true en Development; proveedor pendiente fuera de demo |
| GET | /Registro/Documento?tipo=terminos, privacidad o promociones | Texto configurado; en demo explícitamente ficticio |
| GET/POST | /Cliente/Perfil | Perfil propio |
| GET | /Cliente/Direcciones; /Cliente/Nueva; /Cliente/Editar/{id}; /Cliente/Eliminar/{id} | Listado/formulario/confirmación propios; también acepta id por query convencional |
| POST | /Cliente/Guardar; /Cliente/ConfirmarEliminar; /Cliente/Principal | Mutaciones propias con antiforgery |
| GET | /Consentimientos | Historial/preferencia propios |
| POST | /Consentimientos/Autorizar; /Consentimientos/Retirar | Decisión promocional explícita |
| GET/POST | /Cuenta/CambiarCorreo; /Cuenta/ConfirmarCorreo | Cambio autenticado y confirmación local en demo; contrato persistente pendiente |
| GET | /Cuenta/CambioCorreoSolicitado | Aviso tras solicitar, sin token visible |
| GET | /Compras/Preparar?returnUrl=... | Sesión requerida, aviso informativo y regreso local; no crea pedido |

Cuenta/Login, logout, MFA y recuperación conservan sus verbos y protección existentes. En demo la identidad de clientes sustituye el proveedor para ese proceso; no autentica cuentas SQL. El bloque 11 incorpora recuperación/cambio de contraseña temporal con Pickup y transporte existente. Pruebas y recorrido manual confirmados; última regresión reportada 733 aprobadas, sin errores ni omitidas.

## Endpoints API implementados

| Método | Ruta |
|---|---|
| GET | /api/publico/mercados; /api/publico/buscar |
| POST | /api/clientes/registro |
| GET/PUT | /api/mi-perfil |
| GET/POST | /api/mis-direcciones |
| GET/PUT/DELETE | /api/mis-direcciones/{id} |
| PUT | /api/mis-direcciones/{id}/principal |
| GET/POST | /api/mis-consentimientos |

Las rutas privadas requieren sesión API. La API usa antiforgery con cookies: obtener token en /api/auth/csrf y enviar X-CSRF-TOKEN; renovar tras cambiar identidad. DTO cerrados rechazan campos no permitidos. Propietario derivado de la credencial, sin confiar en IdCliente del navegador.

## Configuración de demostración

Development exclusivamente, activación explícita; no activar en producción. Desde Git Bash, Web:

```bash
Clientes__Demostracion=true Recovery__Provider=Pickup Recovery__WebBaseUrl=https://localhost:7275/ Tiendas__SqlLocal=true Tiendas__Demostracion=false Tiendas__NegocioLocalDemostracion= dotnet run --project PortalMercado.Web --launch-profile https
```

Abrir https://localhost:7275/Registro y crear una cuenta ficticia @example.invalid. No hay usuarios ni contraseñas predefinidos. Los datos de clientes desaparecen al detener el proceso; el catálogo sigue utilizando su proveedor local SQL.

API independiente:

```bash
Clientes__Demostracion=true dotnet run --project PortalMercado.API --launch-profile https
```

Cada proceso tiene su almacén temporal. Una cuenta creada en Web no existe automáticamente en API. El modo API informa X-PortalMercado-Clientes-Modo: Demostracion-temporal. La demostración no envía correos por Internet ni permite compras. El bloque 11 usa archivos EML locales para recuperación y cambio de correo; ver su guía. Sus documentos no son textos aprobados.

Para volver al proveedor normal, detener Web y relanzar omitiendo Clientes__Demostracion=true; asegurarse de no tener esa opción activada en otra configuración. Los proveedores de clientes pendientes vuelven a responder 503; no hay guardado SQL de clientes oculto.

## Próximo trabajo

1. Conservar los TRX y evidencias del bloque 11; móvil, teclado y paginación fueron confirmados por el desarrollador.
2. Manual e informe técnico preparados; video pendiente por separado, mostrando el alcance de la demostración.
3. Revisar diff y archivos preparados; rama del ciclo 3 creada. Confirmar commit/subida y PR.
4. Recibir contratos y documentos aprobados; implementar adaptadores y pruebas SQL de clientes cuando estén disponibles.
5. Sustituir búsqueda temporal por consulta global autorizada, consistente y paginada.

## Contratos aún no recibidos

Consultar docs/SOLICITUD_CONTRATO_CLIENTES_CICLO3.md. Está preparado para revisión: no se ha enviado a terceros ni hay evidencia de aprobación.

- Alta atómica Usuario + estado de seguridad + Cliente + consentimientos + auditoría.
- Lectura/edición de perfil por usuario autenticado.
- Direcciones por propietario; mutaciones revalidadas dentro de la transacción.
- Principal serializada por cliente y compatible con el índice único existente.
- Consentimientos con historial, evento/orden estable, evidencia y auditoría.
- Lecturas públicas globales y permisos mínimos del ambiente de integración.

## Dependencias del ciclo 4

El ciclo 4 deberá implementar carrito/pedido y volver a verificar cliente, tienda, producto publicado, cantidad, precio, stock, dirección propia y permisos al escribir. No confiar en datos de precio/propietario enviados por navegador ni en una validación anterior al login. No consumir o reservar inventario desde la pantalla informativa actual.

La portada del requisito estima 2 días hábiles, pero el cronograma describe cinco jornadas. Se conserva la discrepancia como pendiente de coordinación; no se deduce una fecha de entrega aprobada.
