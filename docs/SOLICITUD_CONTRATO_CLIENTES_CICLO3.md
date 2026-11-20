# Solicitud de contratos para clientes del Ciclo 3

Fecha: 17 de noviembre de 2026. Documento preparado para revisión; no acredita
que haya sido enviado ni aprobado.

## Contratos requeridos al responsable de datos e identidad

| Operación | Garantías necesarias |
| --- | --- |
| Alta Usuario y Cliente | Transacción única, unicidad de correo normalizado y Cliente.IdUsuario, sin registros parciales |
| Estado de seguridad | Compatible con login, bloqueo, sello y recuperación existentes; sin roles elevados |
| Perfil propio | Obtener y actualizar por IdUsuario autenticado; solo campos aprobados |
| Direcciones propias | CRUD con propiedad y estado comprobados; parámetros tipados |
| Principal | Transacción serializada por cliente y compatibilidad con el índice filtrado existente |
| Consentimientos | Tipo, versión, aceptación, fecha UTC e IP; historial y reglas de revocación |
| Auditoría | Eventos de alta/cambio, actor y correlación; sin secretos ni formularios en logs |
| Permisos | Cuenta de aplicación con accesos mínimos a los contratos aprobados |

No se presupone nombre de SP, vista o permiso nuevo. Confirmar nombres, parámetros,
resultados y errores de negocio antes de desarrollar el adaptador SQL.

## Aclaraciones solicitadas

1. Tipos de documento permitidos y política para su edición o eliminación.
2. Política de registro si el correo ya corresponde a una cuenta de otro rol.
   Propuesta inicial: rechazo genérico; nunca asociar perfiles sin autenticar al titular.
3. Confirmación de correo habilitada o no, proveedor de entrega y flujo existente.
4. Documentos reales de términos, privacidad y promociones, versiones y contenido.
5. Baja lógica o borrado físico de direcciones, y comportamiento al retirar la principal.
6. Si el registro exige celular y qué campo representa el contacto inicial. El modelo
   permite Telefono opcional en Usuario; no se duplicará en Cliente.Celular sin acuerdo.
7. Obligatoriedad de distrito/provincia/departamento y precisión aceptada de coordenadas.
8. Reglas de idempotencia y concurrencia para registro, principal y consentimiento.
9. Catálogo público de mercados y categorías y resultado de búsqueda global por tienda,
   producto y categoría, sin proyecciones de datos privados.
10. Base de integración y datos ficticios autorizados para las pruebas CA-01 a CA-12.

La hoja estima 2 días en portada y describe 5 días en su cronograma. Se requiere
confirmación del plazo aplicable.

## Estrategia mientras falten contratos

Continuar tras interfaces y dobles unitarios; implementar simulaciones explícitas
cuando se conecten las pantallas. No activar proveedores SQL ficticios ni declarar
persistencia o registro integrado si no existen. La autorización local del Ciclo 2
no se extiende automáticamente al Ciclo 3.

No se añade ni modifica DDL, SP, índice, credencial o permiso mediante este bloque.
