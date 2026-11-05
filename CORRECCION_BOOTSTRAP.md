# Corrección del diagnóstico de aprovisionamiento

El mensaje genérico del primer bloque ocultaba también los errores de validación del vendedor ficticio. Este cambio identifica el motivo sin imprimir contraseñas, cadenas de conexión ni excepciones completas. No cambia el esquema SQL.

## Actualizar la copia de trabajo

1. Cerrar API/Web si están ejecutándose.
2. Extraer este ZIP en otra carpeta temporal.
3. Copiar el contenido de su carpeta `PortalMercado` sobre la copia de trabajo de Etapa 02, aceptando reemplazar los archivos de código. Los secretos se guardan fuera del proyecto; no vienen incluidos en el ZIP.
4. Compilar con `dotnet build PortalMercado.sln`.
5. Si la ampliación ET02-001 ya fue verificada con 60 tablas, 9 SP y 87 FK, no volver a instalar la base.
6. Repetir el bloque de PowerShell de creación del vendedor de `docs/ETAPA02_BLOQUE01.md`. Hay que volver a escribir la contraseña: el bloque anterior eliminó las variables del entorno al terminar.

## Resultados del comando

| Código | Acción |
|---|---|
| PASSWORD_INVALIDA | Elegir de 12 a 128 caracteres con mayúscula, minúscula, número y símbolo. No compartirla. |
| CORREO_INVALIDO | En la prueba ficticia, usar vendedor.et02@example.invalid. |
| NOMBRE_INVALIDO | Configurar un nombre de 1 a 150 caracteres. |
| ENTORNO_NO_PERMITIDO | Verificar Development, SQLEXPRESS local, autenticación Windows y PortalMercado_ET01_Test. |
| USUARIO_FICTICIO_EXISTENTE | No recrear ni borrar la cuenta. Usar la contraseña elegida cuando se creó. |
| FALTA_MIGRACION | Comprobar conexión y ejecutar el verificador de la ampliación existente. |
| SQL:número | Conservar el código y revisar el diagnóstico indicado. |
| TIPO:nombre | Conservar el tipo de error para investigar configuración o persistencia. |

Un mensaje de diagnóstico más preciso no demuestra por sí solo que SQL funcione; la prueba real sigue siendo crear la cuenta y luego iniciar sesión en la instancia local del usuario.

Comprobación de esta revisión: Debug y Release sin errores ni advertencias; 45 pruebas sin SQL aprobadas. El aprovisionamiento real en SQLEXPRESS sigue pendiente de comprobar.
