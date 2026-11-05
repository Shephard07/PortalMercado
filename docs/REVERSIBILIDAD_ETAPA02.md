# Ensayo de reversión en base separada

Prueba de CA02-12. La base activa de trabajo `PortalMercado_ET01_Test` contiene sesiones y auditoría. Los scripts Down se niegan a revertirla después del uso para conservar esa evidencia. Ensaya las versiones en una **base desechable nueva**; no apuntes API/Web a esa copia ni hagas bootstrap en ella.

Desde la raíz de la solución en PowerShell, prepara copias de los scripts con un nombre distinto. Este bloque **solo genera archivos**, no ejecuta SQL:

```powershell
$baseEnsayo = 'PortalMercado_ET02_Rollback_' + (Get-Date -Format 'yyyyMMdd_HHmmss')
$rutaEnsayo = Join-Path $env:TEMP $baseEnsayo
foreach ($carpeta in @('local', 'etapa02')) {
    $destino = Join-Path $rutaEnsayo $carpeta
    New-Item -ItemType Directory -Force $destino | Out-Null
    Get-ChildItem -Path (Join-Path '.\database' $carpeta) -Filter '*.sql' -File | ForEach-Object {
        $contenido = Get-Content -LiteralPath $_.FullName -Raw
        $contenido.Replace('PortalMercado_ET01_Test', $baseEnsayo) |
            Set-Content -LiteralPath (Join-Path $destino $_.Name) -Encoding UTF8
    }
}
$rutaEnsayo
```

Conecta SSMS a `.\SQLEXPRESS`. Abre los archivos **desde la carpeta temporal impresa**, nunca desde `database/` de la solución para este ensayo. Verifica el nombre `PortalMercado_ET02_Rollback_...` en cada ventana antes de ejecutar. Ejecuta cada archivo completo. Si falla uno, detente y conserva el mensaje.

| Orden | Archivos de la copia temporal | Comprobación |
|---|---|---|
| 1 | `local/00_Preparar_Base_Local.sql`, `01_Esquema_Local.sql`, `02_Procedimientos_Local.sql`, `03_Verificar_Instalacion.sql` | Activar Modo SQLCMD en cada ventana. Base nueva: 56 tablas, 9 SP, 84 FK. No hace falta semilla ET01. |
| 2 | `etapa02/01_Seguridad_Up.sql`, `04_Mfa_Up.sql`, `07_Administracion_Up.sql`, `10_Password_Up.sql` | Migraciones en orden, sin iniciar aplicación. Usa Modo SQLCMD si el archivo comienza con `:ON ERROR EXIT`. |
| 3 | `etapa02/11_Password_Verificar.sql`, `13_Evidencias_ET02.sql` | Capturar 66/9/92 y las huellas de SP. No hay sesiones, tokens ni operaciones administrativas. |
| 4 | `etapa02/12_Password_Down.sql`, `09_Administracion_Down.sql`, `06_Mfa_Down.sql`, `02_Seguridad_Down.sql` | Reversión en orden inverso. Cada script solo elimina sus objetos y semilla cuando las guardas lo permiten. |
| 5 | `local/03_Verificar_Instalacion.sql` | Modo SQLCMD. Debe volver a 56/9/84, sin FK inválidas. |

Para comparar huellas de procedimientos después de revertir, ejecuta **solo** esta consulta en la base de ensayo seleccionada en SSMS (no ejecutes `13_Evidencias_ET02.sql` completo después del Down, porque ya no existen sus tablas de seguridad):

```sql
SELECT DB_NAME() AS BaseActual;
SELECT p.name AS Procedimiento,
       CONVERT(varchar(64), HASHBYTES('SHA2_256', OBJECT_DEFINITION(p.object_id)), 2) AS HuellaDefinicion
FROM sys.procedures p
WHERE p.schema_id = SCHEMA_ID('dbo')
ORDER BY p.name;
```

Las nueve huellas deben coincidir con las capturadas antes del Down. Conserva las capturas y el nombre de la base de ensayo. El procedimiento no elimina la base ni modifica los scripts originales. La reversión de un entorno ya utilizado exige respaldo y planificación específica para preservar auditoría, MFA y cuentas.
