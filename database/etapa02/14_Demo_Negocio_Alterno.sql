-- Opcional para el video. Añade solo un negocio ficticio en la copia local; no cambia permisos.
USE [PortalMercado_ET01_Test];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'') <> N'SQLEXPRESS'
   OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')) <> HOST_NAME()
   THROW 50050,'Usar SQLEXPRESS en el mismo equipo que SSMS.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SeguridadVersion WHERE Codigo='ET02-004')
   THROW 50051,'Se requiere ET02-004.',1;
BEGIN TRY
 BEGIN TRAN;
 DECLARE @mercado bigint,@comerciante bigint;
 SELECT @mercado=n.IdMercado,@comerciante=n.IdComerciante FROM dbo.Negocio n
 JOIN dbo.Mercado m ON m.IdMercado=n.IdMercado
 WHERE n.Slug='negocio-prueba-et01' AND n.Estado='ACTIVO' AND m.Activo=1;
 IF @mercado IS NULL OR @comerciante IS NULL THROW 50052,'No se encontró el negocio ficticio activo de ET01.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Negocio WITH(UPDLOCK,HOLDLOCK) WHERE IdMercado=@mercado AND Slug='negocio-demo-et02-b')
 INSERT dbo.Negocio(IdMercado,IdComerciante,NombreComercial,Slug,Estado,CreadoEn)
 VALUES(@mercado,@comerciante,N'Segundo negocio de prueba ET02','negocio-demo-et02-b','ACTIVO',SYSUTCDATETIME());
 COMMIT;
 SELECT IdNegocio, NombreComercial, Estado FROM dbo.Negocio
 WHERE IdMercado=@mercado AND Slug IN('negocio-prueba-et01','negocio-demo-et02-b') ORDER BY IdNegocio;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
