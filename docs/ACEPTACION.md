# Criterios de aceptación y estado

| Criterio | Evidencia recibida | Cierre pendiente después de la revisión |
|---|---|---|
| CA-01 | Solución abre y compila; builds anteriores Debug/Release | Recompilar ambos con los cambios actuales |
| CA-02 | API 7161 y MVC 7275 por HTTPS | Captura final de MVC y Swagger |
| CA-03 | health devuelve 200 | Cubierto por prueba HTTP; repetir suite actual |
| CA-04 | Conexión real; 503 seguro; middleware 500 correlacionado | Repetir suite actual |
| CA-05 | Cinco entidades materializadas en SQL aislado | Semilla reproducible incluida; repetir SQL tests |
| CA-06 | Interface y executor ADO.NET probado con ceros | Repetir SQL tests |
| CA-07 | Capturas 56/9/84 y consulta de parámetros | Diagnóstico redactado; adjuntar capturas a evidencias |
| CA-08 | No se detectaron credenciales reales en archivos fuente/configuración del ZIP | Revisar índice Git real; no se recibió historial Git |
| CA-09 | TRX previo con 13 aprobadas | Nuevos casos sin ejecutar: esperado final 23 con SQL habilitado |
| CA-10 | README y scripts de instalación incluidos en revisión | Confirmar pasos en Windows/otro equipo según disponibilidad |

## Revisión del código corregido

- Quitados AddControllers, AddOpenApi y middleware/mapeo repetidos en API.
- CORS localhost trasladado a Development; Production/común sin orígenes permitidos.
- Web incorpora logs JSON y correlación; errores seguros y acción Error restaurada.
- Configuración de cadena ausente/malformada validada sin revelar su contenido.
- Incorporadas cinco pruebas de configuración, tres de CORS por ambiente y dos de MVC.
- Incorporada la semilla 04 faltante y documentación de instalación/reversión.
- No se cambian el esquema ni las reglas de los SP.

La evidencia previa demuestra el código recibido, no el modificado. En el entorno
revisor no hay dotnet ni SQL Server: la validación final requiere ejecutar en Windows.
No hay video, commit ni etiqueta creados por esta revisión. No se publica el proyecto.
