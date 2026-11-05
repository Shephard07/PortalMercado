-- PROPUESTA ET02-001. Requiere revisión/aprobación antes de aplicar.
-- Solo laboratorio. Una ventana SSMS normal; no requiere SQLCMD.
USE [PortalMercado_ET01_Test];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int, @resource nvarchar(255) = N'PortalMercado.ET02.SecurityMigration';
 EXEC @lock=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
 IF @lock<0 THROW 50200,'No se obtuvo el bloqueo de migración.',1;
 IF OBJECT_ID('dbo.Usuario','U') IS NULL OR OBJECT_ID('dbo.UsuarioRol','U') IS NULL
    OR OBJECT_ID('dbo.RolPermiso','U') IS NULL OR OBJECT_ID('dbo.UsuarioNegocio','U') IS NULL
    OR OBJECT_ID('dbo.UsuarioMercado','U') IS NULL
    THROW 50201,'Falta el esquema de la etapa 01.',1;
 IF OBJECT_ID('dbo.SeguridadVersion','U') IS NOT NULL
 BEGIN
  IF EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-001')
  BEGIN
   COMMIT; PRINT 'ET02-001 ya aplicada. Sin cambios.'; RETURN;
  END;
 END;
 -- No corregir ni fusionar cuentas automáticamente.
 IF EXISTS(SELECT 1 FROM dbo.Usuario WHERE Correo COLLATE Latin1_General_100_BIN2 LIKE '%[^ -~]%'
           OR LEN(LTRIM(RTRIM(Correo)))=0 OR LTRIM(RTRIM(Correo)) LIKE '% %')
    THROW 50202,'Hay correos vacíos, no ASCII o con espacios internos. Revisar antes de migrar.',1;
 IF EXISTS(SELECT UPPER(LTRIM(RTRIM(Correo)) COLLATE Latin1_General_100_CI_AS)
           FROM dbo.Usuario GROUP BY UPPER(LTRIM(RTRIM(Correo)) COLLATE Latin1_General_100_CI_AS)
           HAVING COUNT(*)>1)
    THROW 50203,'Hay correos duplicados normalizados. Resolver manualmente antes de migrar.',1;
 IF OBJECT_ID('dbo.UsuarioSeguridad','U') IS NOT NULL OR OBJECT_ID('dbo.UsuarioSesion','U') IS NOT NULL
    OR OBJECT_ID('dbo.AuditoriaAcceso','U') IS NOT NULL OR OBJECT_ID('dbo.SeguridadVersion','U') IS NOT NULL
    OR COL_LENGTH('dbo.Usuario','CorreoNormalizado') IS NOT NULL
    THROW 50204,'Objetos ET02 preexistentes sin versión conocida. Revisar manualmente.',1;
 ALTER TABLE dbo.Usuario ADD CorreoNormalizado AS
    UPPER(LTRIM(RTRIM(Correo)) COLLATE Latin1_General_100_CI_AS) PERSISTED;
 -- SQL dinámico: la columna nueva se compila después de ALTER TABLE.
 EXEC(N'CREATE UNIQUE INDEX UX_Usuario_CorreoNormalizado ON dbo.Usuario(CorreoNormalizado);');
 CREATE TABLE dbo.UsuarioSeguridad(
    IdUsuario bigint NOT NULL CONSTRAINT PK_UsuarioSeguridad PRIMARY KEY,
    IntentosFallidos int NOT NULL CONSTRAINT DF_UsuarioSeguridad_Intentos DEFAULT(0),
    BloqueadoHasta datetime2(3) NULL,
    Sello uniqueidentifier NOT NULL CONSTRAINT DF_UsuarioSeguridad_Sello DEFAULT(NEWID()),
    CONSTRAINT CK_UsuarioSeguridad_Intentos CHECK(IntentosFallidos>=0),
    CONSTRAINT FK_UsuarioSeguridad_Usuario FOREIGN KEY(IdUsuario) REFERENCES dbo.Usuario(IdUsuario));
 INSERT dbo.UsuarioSeguridad(IdUsuario) SELECT IdUsuario FROM dbo.Usuario;
 CREATE TABLE dbo.UsuarioSesion(
    IdSesion uniqueidentifier NOT NULL CONSTRAINT PK_UsuarioSesion PRIMARY KEY,
    IdUsuario bigint NOT NULL,
    TokenHash char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Host varchar(8) NOT NULL,
    Sello uniqueidentifier NOT NULL,
    CreadoEn datetime2(3) NOT NULL,
    ExpiraEn datetime2(3) NOT NULL,
    RevocadoEn datetime2(3) NULL,
    CONSTRAINT FK_UsuarioSesion_Usuario FOREIGN KEY(IdUsuario) REFERENCES dbo.Usuario(IdUsuario),
    CONSTRAINT CK_UsuarioSesion_Host CHECK(Host IN('API','MVC')),
    CONSTRAINT CK_UsuarioSesion_Expira CHECK(ExpiraEn>CreadoEn));
 CREATE UNIQUE INDEX UX_UsuarioSesion_TokenHash ON dbo.UsuarioSesion(TokenHash);
 CREATE INDEX IX_UsuarioSesion_Usuario ON dbo.UsuarioSesion(IdUsuario,ExpiraEn) INCLUDE(RevocadoEn);
 CREATE INDEX IX_UsuarioSesion_Expira ON dbo.UsuarioSesion(ExpiraEn);
 CREATE TABLE dbo.AuditoriaAcceso(
    IdAuditoria bigint IDENTITY NOT NULL CONSTRAINT PK_AuditoriaAcceso PRIMARY KEY,
    IdActor bigint NULL,
    Evento varchar(60) NOT NULL,
    FechaUtc datetime2(3) NOT NULL,
    CorrelationId varchar(64) NOT NULL,
    CONSTRAINT FK_AuditoriaAcceso_Actor FOREIGN KEY(IdActor) REFERENCES dbo.Usuario(IdUsuario));
 CREATE INDEX IX_AuditoriaAcceso_Fecha ON dbo.AuditoriaAcceso(FechaUtc,IdActor);
 CREATE TABLE dbo.SeguridadVersion(
    Codigo varchar(80) NOT NULL CONSTRAINT PK_SeguridadVersion PRIMARY KEY,
    AplicadoEn datetime2(3) NOT NULL);
 INSERT dbo.SeguridadVersion VALUES('ET02-001',SYSUTCDATETIME());
 COMMIT;
 PRINT 'ET02-001 aplicada. No se cambiaron procedimientos financieros.';
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
