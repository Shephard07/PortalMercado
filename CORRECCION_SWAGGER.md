# Swagger en blanco

El cargador parseFunction reconstruye una función ordinaria, por lo que el interceptor anterior con async/await fallaba al cargar la interfaz. Se sustituyó por una función que devuelve una promesa mediante fetch/then.

Actualizar PortalMercado.API/Program.cs con esta revisión, detener y reiniciar la API y recargar /swagger/index.html con Ctrl+F5. No requiere cambios SQL ni recrear cuentas. La protección antiforgery sigue activa.

Regresión JavaScript opcional, con Node instalado: `node scripts/test-swagger-interceptor.cjs`.
