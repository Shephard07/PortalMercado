-- Solo antes de utilizar el bloque. Nunca borra usuarios ni auditoría.
USE [PortalMercado_ET01_Test];
GO
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRAN;
 DECLARE @lock int,@resource nvarchar(255)=N'PortalMercado.ET02.SecurityMigration';
 EXEC @lock=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 50030,'No se pudo bloquear la migración.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-003') BEGIN COMMIT; RETURN; END;
 IF EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo>'ET02-003' AND Codigo LIKE 'ET02-[0-9][0-9][0-9]') THROW 50033,'Revertir primero versiones posteriores.',1;
 IF EXISTS(SELECT 1 FROM dbo.AuditoriaAcceso WHERE Evento LIKE 'ADMIN[_]%') OR EXISTS(SELECT 1 FROM dbo.UsuarioSesion WHERE IdNegocioActivo IS NOT NULL)
 THROW 50034,'El bloque ya fue utilizado; conservar datos y preparar reversión específica.',1;
 IF EXISTS(SELECT 1 FROM dbo.SeguridadSemillaET02 s JOIN dbo.Rol r ON r.Codigo=s.Codigo JOIN dbo.UsuarioRol ur ON ur.IdRol=r.IdRol WHERE s.Tipo='ROL')
 THROW 50035,'Un rol nuevo ya está asignado. No se elimina.',1;
 IF EXISTS(SELECT 1 FROM dbo.RolPermiso rp JOIN dbo.Rol r ON r.IdRol=rp.IdRol JOIN dbo.Permiso p ON p.IdPermiso=rp.IdPermiso
 WHERE (EXISTS(SELECT 1 FROM dbo.SeguridadSemillaET02 s WHERE s.Tipo='ROL' AND s.Codigo=r.Codigo) OR EXISTS(SELECT 1 FROM dbo.SeguridadSemillaET02 s WHERE s.Tipo='PERMISO' AND s.Codigo=p.Codigo))
 AND NOT EXISTS(SELECT 1 FROM dbo.SeguridadSemillaET02 s WHERE s.Tipo='ASIGNACION' AND s.Codigo=r.Codigo AND s.Relacion=p.Codigo))
 THROW 50036,'Existen asignaciones personalizadas. No se eliminan.',1;
 DELETE rp FROM dbo.RolPermiso rp JOIN dbo.Rol r ON r.IdRol=rp.IdRol JOIN dbo.Permiso p ON p.IdPermiso=rp.IdPermiso
 JOIN dbo.SeguridadSemillaET02 s ON s.Tipo='ASIGNACION' AND s.Codigo=r.Codigo AND s.Relacion=p.Codigo;
 DELETE p FROM dbo.Permiso p JOIN dbo.SeguridadSemillaET02 s ON s.Tipo='PERMISO' AND s.Codigo=p.Codigo;
 DELETE r FROM dbo.Rol r JOIN dbo.SeguridadSemillaET02 s ON s.Tipo='ROL' AND s.Codigo=r.Codigo;
 ALTER TABLE dbo.UsuarioSesion DROP CONSTRAINT FK_UsuarioSesion_NegocioActivo;
 ALTER TABLE dbo.UsuarioSesion DROP COLUMN IdNegocioActivo;
 ALTER TABLE dbo.AuditoriaAcceso DROP COLUMN IdObjetivo;
 DROP TABLE dbo.SeguridadSemillaET02;
 DELETE dbo.SeguridadVersion WHERE Codigo='ET02-003';
 COMMIT; PRINT 'ET02-003 revertida sin eliminar usuarios.';
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
GO
