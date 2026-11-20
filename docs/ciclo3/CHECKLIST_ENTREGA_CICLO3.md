# Lista de entrega del ciclo 3

Corte: 20/11/2026. Avance funcional local con simulación autorizada por la sección 2 del requerimiento; aprobación formal pendiente.

| Elemento | Estado |
|---|---|
| MVC, API y servicios | Implementados hasta bloque 11; persistencia de clientes temporal |
| Pruebas | 383 Clientes y 733 solución aprobadas según salidas compartidas |
| Revisión manual | Flujos de cliente, acceso ajeno, seguridad local, móvil, teclado y paginación confirmados |
| Rama requerida | Creada: feature/b-clientes-portal-ciclo3 |
| Commit y subida | Pendientes de confirmación después de aplicar la documentación |
| Pull request | Pendiente |
| Manual e informe técnico | Incluidos en el paquete documental |
| Matrices RF y CA | Actualizadas en Markdown y CSV |
| Video del ciclo 3 | Pendiente; se preparará por separado |
| Integración de clientes SQL | Contratos del DBA pendientes |

Antes del commit, revisar diff y archivos preparados. No incluir bin, obj, secretos, claves, EML, tokens ni contraseñas. Los scripts database/ciclo2-local pertenecen al trabajo previo de tiendas y deben identificarse como tal en el PR acumulado.

Guardar los TRX del bloque 11 en docs/evidencias/ciclo3. Revisar rama, diff y git diff --cached --check; no se ha ejecutado Git en la PC del desarrollador desde aquí. No declarar una aprobación técnica que no haya emitido el revisor.

Pendientes de aceptación: contratos/adaptadores SQL, documentos y políticas aprobados, decisión de confirmación inicial de correo, consulta global completa y pruebas reales de persistencia/concurrencia/auditoría de clientes.
