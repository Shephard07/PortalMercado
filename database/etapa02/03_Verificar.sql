USE [PortalMercado_ET01_Test];
GO
SET NOCOUNT ON;
SELECT @@SERVERNAME Servidor,DB_NAME() BaseActual;
IF OBJECT_ID('dbo.SeguridadVersion','U') IS NULL THROW 50230,'ET02-001 no aplicada.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-001') THROW 50231,'Falta versión ET02-001.',1;
IF OBJECT_ID('dbo.UsuarioSeguridad','U') IS NULL OR OBJECT_ID('dbo.UsuarioSesion','U') IS NULL
   OR OBJECT_ID('dbo.AuditoriaAcceso','U') IS NULL OR COL_LENGTH('dbo.Usuario','CorreoNormalizado') IS NULL
   THROW 50232,'Esquema ET02 incompleto.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Usuario')
              AND name='UX_Usuario_CorreoNormalizado' AND is_unique=1 AND is_disabled=0)
   THROW 50233,'Falta índice único de correo normalizado.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
   THROW 50234,'Hay FK sin validar o deshabilitadas.',1;
SELECT Codigo,AplicadoEn FROM dbo.SeguridadVersion;
SELECT (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')) TablasDbo,
       (SELECT COUNT(*) FROM sys.procedures WHERE schema_id=SCHEMA_ID('dbo')) ProcedimientosDbo,
       (SELECT COUNT(*) FROM sys.foreign_keys) ClavesForaneas;
-- No mostrar TokenHash ni PasswordHash.
SELECT Evento,COUNT(*) Cantidad FROM dbo.AuditoriaAcceso GROUP BY Evento;
