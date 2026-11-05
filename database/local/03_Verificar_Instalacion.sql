:ON ERROR EXIT
-- Ejecutar en SSMS con Consulta > Modo SQLCMD activado.
USE [master];
GO
IF ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')) <> HOST_NAME()
    THROW 50001, 'Se requiere la instancia SQLEXPRESS del mismo equipo que SSMS.', 1;
GO
USE [PortalMercado_ET01_Test];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
SELECT @@SERVERNAME AS Servidor, DB_NAME() AS BaseActual;
SELECT
 (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo')) AS TablasDbo,
 (SELECT COUNT(*) FROM sys.procedures WHERE schema_id=SCHEMA_ID(N'dbo')) AS ProcedimientosDbo,
 (SELECT COUNT(*) FROM sys.foreign_keys) AS ClavesForaneas;
SELECT name, is_disabled, is_not_trusted
FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1;
SELECT SCHEMA_NAME(schema_id) AS Esquema, name AS Procedimiento
FROM sys.procedures ORDER BY name;
SELECT OBJECT_NAME(object_id) AS Procedimiento, name AS Parametro,
 TYPE_NAME(user_type_id) AS Tipo, max_length, precision, scale, is_output
FROM sys.parameters
WHERE object_id=OBJECT_ID(N'dbo.pm_ConsultarSaldos');
IF (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo')) <> 56
 OR (SELECT COUNT(*) FROM sys.procedures WHERE schema_id=SCHEMA_ID(N'dbo')) <> 9
 OR (SELECT COUNT(*) FROM sys.foreign_keys) <> 84
    THROW 50005, 'Inventario distinto al esperado. Revisar resultados.', 1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
    THROW 50006, 'Existen claves foraneas deshabilitadas o sin validar.', 1;
PRINT N'Inventario esperado: 56 tablas, 9 procedimientos y 84 claves foraneas.';
