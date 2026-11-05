-- Ampliación incremental ET02-002, solo laboratorio local.
USE [PortalMercado_ET01_Test];
GO
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int,@resource nvarchar(255)=N'PortalMercado.ET02.SecurityMigration';
 EXEC @lock=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
 IF @lock<0 THROW 50240,'Migración ocupada.',1;
 IF OBJECT_ID('dbo.SeguridadVersion','U') IS NULL THROW 50241,'Falta ET02-001.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-001') THROW 50241,'Falta ET02-001.',1;
 IF EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-002')
 BEGIN COMMIT; PRINT 'ET02-002 ya aplicada. Sin cambios.'; RETURN; END;
 IF OBJECT_ID('dbo.UsuarioMfa','U') IS NOT NULL OR OBJECT_ID('dbo.UsuarioMfaReto','U') IS NOT NULL
    OR OBJECT_ID('dbo.UsuarioMfaRecuperacion','U') IS NOT NULL OR COL_LENGTH('dbo.UsuarioSesion','MfaVerificado') IS NOT NULL
    THROW 50242,'Objetos MFA preexistentes sin versión conocida.',1;
 ALTER TABLE dbo.UsuarioSesion ADD MfaVerificado bit NOT NULL CONSTRAINT DF_UsuarioSesion_Mfa DEFAULT(0);
 CREATE TABLE dbo.UsuarioMfa(
  IdUsuario bigint NOT NULL CONSTRAINT PK_UsuarioMfa PRIMARY KEY,
  SecretProtegido nvarchar(2048) NULL,
  PendienteBootstrap bit NOT NULL,
  UltimoPasoTotp bigint NULL,
  ActivadoEn datetime2(3) NULL,
  CONSTRAINT FK_UsuarioMfa_Usuario FOREIGN KEY(IdUsuario) REFERENCES dbo.Usuario(IdUsuario));
 CREATE TABLE dbo.UsuarioMfaReto(
  IdReto uniqueidentifier NOT NULL CONSTRAINT PK_UsuarioMfaReto PRIMARY KEY,
  IdUsuario bigint NOT NULL,
  TokenHash char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
  Host varchar(8) NOT NULL,
  Enrolamiento bit NOT NULL,
  Sello uniqueidentifier NOT NULL,
  ExpiraEn datetime2(3) NOT NULL,
  ConsumidoEn datetime2(3) NULL,
  Intentos int NOT NULL,
  CONSTRAINT CK_UsuarioMfaReto_Intentos CHECK(Intentos BETWEEN 0 AND 5),
  CONSTRAINT CK_UsuarioMfaReto_Host CHECK(Host IN('API','MVC')),
  CONSTRAINT FK_UsuarioMfaReto_Usuario FOREIGN KEY(IdUsuario) REFERENCES dbo.Usuario(IdUsuario));
 CREATE UNIQUE INDEX UX_UsuarioMfaReto_Hash ON dbo.UsuarioMfaReto(TokenHash);
 CREATE INDEX IX_UsuarioMfaReto_Expira ON dbo.UsuarioMfaReto(ExpiraEn);
 CREATE INDEX IX_UsuarioMfaReto_Usuario ON dbo.UsuarioMfaReto(IdUsuario,ConsumidoEn);
 CREATE TABLE dbo.UsuarioMfaRecuperacion(
  IdUsuario bigint NOT NULL,
  CodigoHash char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
  UsadoEn datetime2(3) NULL,
  CONSTRAINT PK_UsuarioMfaRecuperacion PRIMARY KEY(IdUsuario,CodigoHash),
  CONSTRAINT FK_UsuarioMfaRecuperacion_Usuario FOREIGN KEY(IdUsuario) REFERENCES dbo.Usuario(IdUsuario));
 -- Compatibilidad: si el bootstrap anterior ya dejó al primer admin inactivo,
 -- preparar SOLO esa cuenta auditada, nunca cualquier cuenta inactiva.
 INSERT dbo.UsuarioMfa(IdUsuario,PendienteBootstrap)
 SELECT DISTINCT u.IdUsuario,1 FROM dbo.Usuario u
 JOIN dbo.AuditoriaAcceso a ON a.IdActor=u.IdUsuario AND a.Evento='ADMIN_APROVISIONADO_INACTIVO'
 JOIN dbo.UsuarioRol ur ON ur.IdUsuario=u.IdUsuario JOIN dbo.Rol r ON r.IdRol=ur.IdRol
 WHERE u.Activo=0 AND u.DosFactoresHabilitado=0 AND r.Codigo='ADMIN_GENERAL';
 INSERT dbo.SeguridadVersion VALUES('ET02-002',SYSUTCDATETIME());
 COMMIT; PRINT 'ET02-002 aplicada: MFA preparado.';
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
