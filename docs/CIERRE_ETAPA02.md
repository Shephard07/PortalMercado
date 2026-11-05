# Revisión de cierre · PortalMercado Etapa 02

La revisión conserva ET02-004. Añade aviso de sesión expirada, ejemplos de solicitudes OpenAPI, validación explícita de cambios de estado/asignaciones y controles más estrictos de mercado y asociaciones inactivas. No requiere aplicar otra migración.

## Validar la revisión en Windows

1. Detén Web/API con Ctrl+C y cierra Visual Studio. Conserva una copia de la solución existente.
2. Extrae el ZIP y copia el contenido de `PortalMercado` sobre tu solución actual. Conserva `.git`, los User Secrets y tus evidencias locales. No repitas las migraciones, el bootstrap ni el enrolamiento MFA.
3. En PowerShell, desde la raíz de la solución:

```powershell
dotnet build PortalMercado.sln -c Debug
dotnet build PortalMercado.sln -c Release
$env:PM_RUN_SQL_TESTS = '1'
try {
    dotnet test PortalMercado.Tests/PortalMercado.Tests.csproj --logger 'trx;LogFileName=pruebas-etapa02-cierre-sql.trx' --results-directory ./TestResults
}
finally {
    Remove-Item Env:PM_RUN_SQL_TESTS -ErrorAction SilentlyContinue
}
```

Resultado esperado: **144 aprobadas, 0 errores, 0 omitidas**. Incluye 117 sin SQL y 27 con SQL. Las 133 pruebas que pasaron previamente en Windows pertenecen al bloque 04 antes de esta revisión. No sobrescribas aquel TRX.

```powershell
New-Item -ItemType Directory -Force '.\docs\evidencias\etapa02-cierre' | Out-Null
Copy-Item '.\TestResults\pruebas-etapa02-cierre-sql.trx' '.\docs\evidencias\etapa02-cierre\pruebas-etapa02-cierre-sql.trx'
```

En SSMS local ejecuta `database/etapa02/13_Evidencias_ET02.sql` completo. Obtendrás las versiones SQL, inventario 66/9/92, FK inválidas si las hubiera, permisos, auditoría y estado MFA sin secretos. No cambia la base. No muestres filas de tablas que contienen hashes, tokens o material MFA.

## Evidencias a reunir

| Evidencia | Qué debe verse |
|---|---|
| Build Debug y Release | Compilación de los seis proyectos |
| Suite completa | Resumen y TRX final con SQL habilitado |
| Inventario y auditoría | ET02-004, 66/9/92, FK válidas, evento y correlación |
| MFA | Pantalla de solicitud de segundo factor y perfil autorizado; códigos ocultos |
| Administración | Usuarios, permisos, asignaciones y cuenta de prueba desactivada |
| Ámbito | Cambio entre dos negocios autorizados y rechazo administrativo del vendedor |
| API | 401 sin sesión y 403 con vendedor en `/api/usuarios` |
| Reversión | Inventario de la base desechable antes y después; nueve huellas de SP iguales |

Usa `GUION_VIDEO_ET02.md` para la demostración sin voz y `REVERSIBILIDAD_ETAPA02.md` para el ensayo de rollback. La matriz `MATRIZ_ACEPTACION_ET02.md` vincula las funciones y casos de aceptación con sus pruebas. No incluyas contraseñas, cookies, tokens, códigos TOTP/recuperación, certificados privados, correos `.eml` ni backups de base en los archivos de entrega.

## Etiqueta de entrega

El ZIP de revisión no contiene historial Git. Realiza los pasos en tu **repositorio original** cuando estén validadas las evidencias y aprobada la versión. Revisa los archivos preparados antes de confirmar.

```powershell
git status --short --branch
git add -A
git --no-pager diff --cached --stat
git --no-pager diff --cached --name-only
git --no-pager diff --cached --check
git tag --list PM-ET02-v1.0
```

Si aparece un archivo privado en el índice, retíralo antes del commit y corrige `.gitignore`. Si la etiqueta ya existe, verifica su commit; no la fuerces. Con el contenido revisado y la etiqueta libre:

```powershell
git commit -m 'Completar autenticacion y control de acceso de PortalMercado Etapa 02'
git tag -a PM-ET02-v1.0 -m 'PortalMercado Etapa 02'
git --no-pager show --stat PM-ET02-v1.0
git archive --format=zip --prefix=PortalMercado/ --output=../PortalMercado_ET02_v1.0.zip PM-ET02-v1.0
```

La conformidad técnica debe registrar el estado de la Etapa 01, la revisión de scripts para el entorno destino y el resultado de los criterios CA02-01 a CA02-12. Pickup satisface el flujo de prueba de recuperación. El envío SMTP real, la configuración productiva y su aprobación se comprueban aparte si se despliega.
