-- Solo laboratorio, después de respaldo y aprobación. Detener API/Web.
-- Deshacer exige que NO haya sesiones ni auditoría; no borra evidencia automáticamente.
USE [PortalMercado_ET01_Test];
GO
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int,@resource nvarchar(255)=N'PortalMercado.ET02.SecurityMigration';
 EXEC @lock=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
 IF @lock<0 THROW 50210,'Migración ocupada.',1;
 IF OBJECT_ID('dbo.SeguridadVersion','U') IS NULL
    THROW 50211,'No existe versión ET02 conocida.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-001')
    OR EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo<>'ET02-001')
    THROW 50212,'Hay otras versiones o bootstrap: rollback requiere revisión manual.',1;
 IF EXISTS(SELECT 1 FROM dbo.UsuarioSesion) OR EXISTS(SELECT 1 FROM dbo.AuditoriaAcceso)
    THROW 50213,'Existen sesiones/auditoría. Conservar evidencia y acordar reversión manual.',1;
 DROP TABLE dbo.UsuarioSesion;
 DROP TABLE dbo.AuditoriaAcceso;
 DROP TABLE dbo.UsuarioSeguridad;
 DROP TABLE dbo.SeguridadVersion;
 DROP INDEX UX_Usuario_CorreoNormalizado ON dbo.Usuario;
 ALTER TABLE dbo.Usuario DROP COLUMN CorreoNormalizado;
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
