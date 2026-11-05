USE [PortalMercado_ET01_Test];
GO
SELECT @@SERVERNAME AS Servidor,DB_NAME() AS BaseActual;
SELECT Codigo,AplicadoEn FROM dbo.SeguridadVersion ORDER BY Codigo;
SELECT (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')) AS TablasDbo,
(SELECT COUNT(*) FROM sys.procedures WHERE schema_id=SCHEMA_ID('dbo')) AS ProcedimientosDbo,
(SELECT COUNT(*) FROM sys.foreign_keys) AS ClavesForaneas;
SELECT name,is_disabled,is_not_trusted FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1;
SELECT r.Codigo AS Rol,p.Codigo AS Permiso FROM dbo.Rol r JOIN dbo.RolPermiso rp ON rp.IdRol=r.IdRol JOIN dbo.Permiso p ON p.IdPermiso=rp.IdPermiso ORDER BY r.Codigo,p.Codigo;
SELECT COL_LENGTH('dbo.UsuarioSesion','IdNegocioActivo') AS ContextoBigint8,COL_LENGTH('dbo.AuditoriaAcceso','IdObjetivo') AS ObjetivoBigint8;
-- Esperado sin otras migraciones: 64 tablas dbo / 9 SP / 91 FK; sin FK inválidas.
