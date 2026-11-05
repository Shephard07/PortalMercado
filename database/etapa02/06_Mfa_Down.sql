-- Detener hosts y conservar respaldo. No borrar evidencia ni deshabilitar MFA activo.
USE [PortalMercado_ET01_Test];
GO
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int,@resource nvarchar(255)=N'PortalMercado.ET02.SecurityMigration';
 EXEC @lock=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
 IF @lock<0 THROW 50246,'Migración ocupada.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-002') THROW 50247,'No existe ET02-002.',1;
 IF EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo NOT IN('ET02-001','ET02-002','ET02-BOOTSTRAP'))
   THROW 50249,'Hay ampliaciones posteriores; revisar orden de reversión.',1;
 IF EXISTS(SELECT 1 FROM dbo.UsuarioMfaReto) OR EXISTS(SELECT 1 FROM dbo.UsuarioMfaRecuperacion)
   OR EXISTS(SELECT 1 FROM dbo.UsuarioMfa WHERE SecretProtegido IS NOT NULL OR ActivadoEn IS NOT NULL)
   OR EXISTS(SELECT 1 FROM dbo.UsuarioSesion WHERE MfaVerificado=1)
   OR EXISTS(SELECT 1 FROM dbo.AuditoriaAcceso WHERE Evento LIKE 'MFA[_]%')
   THROW 50248,'MFA utilizado: conservar evidencia y definir reversión manual.',1;
 DROP TABLE dbo.UsuarioMfaRecuperacion;
 DROP TABLE dbo.UsuarioMfaReto;
 DROP TABLE dbo.UsuarioMfa;
 ALTER TABLE dbo.UsuarioSesion DROP CONSTRAINT DF_UsuarioSesion_Mfa;
 ALTER TABLE dbo.UsuarioSesion DROP COLUMN MfaVerificado;
 DELETE dbo.SeguridadVersion WHERE Codigo='ET02-002';
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
