-- Consulta de evidencia: solo lectura, sin hashes de credenciales, tokens, correos ni secretos MFA.
USE [PortalMercado_ET01_Test];
GO
SELECT DB_NAME() AS BaseActual;
SELECT Codigo, AplicadoEn FROM dbo.SeguridadVersion ORDER BY Codigo;
SELECT (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')) AS TablasDbo,
(SELECT COUNT(*) FROM sys.procedures WHERE schema_id=SCHEMA_ID('dbo')) AS ProcedimientosDbo,
(SELECT COUNT(*) FROM sys.foreign_keys) AS ClavesForaneas;
SELECT name,is_disabled,is_not_trusted FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1;
SELECT r.Codigo AS Rol,p.Codigo AS Permiso FROM dbo.RolPermiso rp
JOIN dbo.Rol r ON r.IdRol=rp.IdRol JOIN dbo.Permiso p ON p.IdPermiso=rp.IdPermiso
ORDER BY r.Codigo,p.Codigo;
SELECT TOP (30) IdAuditoria, Evento, IdActor, IdObjetivo, FechaUtc, CorrelationId
FROM dbo.AuditoriaAcceso ORDER BY IdAuditoria DESC;
SELECT u.IdUsuario, u.Activo, u.DosFactoresHabilitado, m.ActivadoEn
FROM dbo.UsuarioMfa m JOIN dbo.Usuario u ON u.IdUsuario=m.IdUsuario
ORDER BY u.IdUsuario;
SELECT p.name AS Procedimiento, CONVERT(varchar(64),HASHBYTES('SHA2_256',OBJECT_DEFINITION(p.object_id)),2) AS HuellaDefinicion
FROM sys.procedures p WHERE p.schema_id=SCHEMA_ID('dbo') ORDER BY p.name;
