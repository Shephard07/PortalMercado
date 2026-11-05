# Corrección del diagnóstico de aprovisionamiento

2026-11-03. SDK .NET 10.0.401, Linux. Debug y Release correctos sin errores ni advertencias. Suite sin SQL: 45 aprobadas, cero fallidas. Se añadieron cuatro pruebas para clasificar errores y comprobar que el diagnóstico no revela contraseñas ni mensajes internos.

No se ha ejecutado SQL ni el aprovisionamiento contra la instancia del usuario. La causa del fallo local original sigue pendiente de identificar con el nuevo código de diagnóstico. Los tres casos SqlIntegration están excluidos por el filtro; no se presentan como aprobados.

La evidencia anterior de 41 casos se conserva por separado y corresponde a la revisión inicial.
