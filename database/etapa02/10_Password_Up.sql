-- Ejecutar completo. No requiere Modo SQLCMD. Conserva usuarios, MFA y operaciones comerciales.
USE [PortalMercado_ET01_Test];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRAN;
 DECLARE @r int,@resource nvarchar(255)=N'PortalMercado.ET02.SecurityMigration';
 EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @r<0 THROW 50040,'Migración ocupada.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-003') THROW 50041,'Primero aplicar ET02-003.',1;
 IF EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-004') BEGIN COMMIT; PRINT 'ET02-004 ya estaba aplicada.'; RETURN; END;
 IF OBJECT_ID('dbo.UsuarioTokenPassword','U') IS NOT NULL OR OBJECT_ID('dbo.RecuperacionLimite','U') IS NOT NULL
 THROW 50042,'Objetos presentes sin versión; revisar antes de continuar.',1;
 CREATE TABLE dbo.UsuarioTokenPassword(
 IdToken uniqueidentifier NOT NULL CONSTRAINT PK_UsuarioTokenPassword PRIMARY KEY,
 IdUsuario bigint NOT NULL,
 TokenHash char(64) NOT NULL,
 Sello uniqueidentifier NOT NULL,
 CreadoEn datetime2(3) NOT NULL,
 ExpiraEn datetime2(3) NOT NULL,
 ConsumidoEn datetime2(3) NULL,
 RevocadoEn datetime2(3) NULL,
 CONSTRAINT FK_UsuarioTokenPassword_Usuario FOREIGN KEY(IdUsuario) REFERENCES dbo.Usuario(IdUsuario),
 CONSTRAINT CK_UsuarioTokenPassword_Fechas CHECK(ExpiraEn>CreadoEn));
 CREATE UNIQUE INDEX UX_UsuarioTokenPassword_Hash ON dbo.UsuarioTokenPassword(TokenHash);
 CREATE INDEX IX_UsuarioTokenPassword_UsuarioExpira ON dbo.UsuarioTokenPassword(IdUsuario,ExpiraEn);
 CREATE TABLE dbo.RecuperacionLimite(
 CorreoHash char(64) NOT NULL CONSTRAINT PK_RecuperacionLimite PRIMARY KEY,
 VentanaUtc datetime2(3) NOT NULL,
 Solicitudes int NOT NULL,
 CONSTRAINT CK_RecuperacionLimite_Solicitudes CHECK(Solicitudes BETWEEN 0 AND 3));
 INSERT dbo.SeguridadVersion(Codigo,AplicadoEn) VALUES('ET02-004',SYSUTCDATETIME());
 COMMIT; PRINT 'ET02-004 aplicada: recuperación y cambio de contraseña preparados.';
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
GO
