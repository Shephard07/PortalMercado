USE [PortalMercado_ET01_Test];
GO
IF OBJECT_ID('dbo.UsuarioMfa','U') IS NULL OR OBJECT_ID('dbo.UsuarioMfaReto','U') IS NULL
 OR OBJECT_ID('dbo.UsuarioMfaRecuperacion','U') IS NULL OR COL_LENGTH('dbo.UsuarioSesion','MfaVerificado') IS NULL
 THROW 50243,'Esquema MFA incompleto.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-002') THROW 50244,'Falta versión MFA.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) THROW 50245,'Hay FK inválidas.',1;
SELECT @@SERVERNAME Servidor,DB_NAME() BaseActual;
SELECT Codigo,AplicadoEn FROM dbo.SeguridadVersion;
SELECT (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')) TablasDbo,
 (SELECT COUNT(*) FROM sys.procedures WHERE schema_id=SCHEMA_ID('dbo')) ProcedimientosDbo,
 (SELECT COUNT(*) FROM sys.foreign_keys) ClavesForaneas;
-- No mostrar claves, retos, hashes ni códigos de recuperación.
SELECT u.IdUsuario,u.Activo,u.DosFactoresHabilitado,m.PendienteBootstrap,m.ActivadoEn
FROM dbo.Usuario u JOIN dbo.UsuarioMfa m ON m.IdUsuario=u.IdUsuario;
