-- Solo antes de utilizar recuperación/cambio de contraseña. No elimina trazabilidad.
USE [PortalMercado_ET01_Test];
GO
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRAN;
 DECLARE @r int,@resource nvarchar(255)=N'PortalMercado.ET02.SecurityMigration';
 EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @r<0 THROW 50040,'Migración ocupada.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-004') BEGIN COMMIT; RETURN; END;
 IF EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo LIKE 'ET02-[0-9][0-9][0-9]' AND Codigo>'ET02-004')
 THROW 50043,'Revertir primero las versiones posteriores.',1;
 IF EXISTS(SELECT 1 FROM dbo.UsuarioTokenPassword) OR EXISTS(SELECT 1 FROM dbo.RecuperacionLimite)
 OR EXISTS(SELECT 1 FROM dbo.AuditoriaAcceso WHERE Evento LIKE 'PASSWORD[_]%')
 THROW 50044,'Este bloque ya se utilizó. Preparar reversión específica conservando auditoría.',1;
 DROP TABLE dbo.UsuarioTokenPassword;
 DROP TABLE dbo.RecuperacionLimite;
 DELETE dbo.SeguridadVersion WHERE Codigo='ET02-004';
 COMMIT; PRINT 'ET02-004 revertida sin modificar usuarios ni MFA.';
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
GO
