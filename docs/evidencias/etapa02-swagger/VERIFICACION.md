# Corrección Swagger

2026-11-03. Debug/Release correctos y 45 pruebas sin SQL aprobadas. Node: comprobación del interceptor con GET sin petición CSRF, POST con token y rechazo cuando falla su preparación. Se reprodujo además el SyntaxError del bloque anterior y se comprobó que el nuevo bloque carga con parseFunction de Swashbuckle. No se ejecutó SQL ni se comprobó visualmente en el navegador del usuario.
