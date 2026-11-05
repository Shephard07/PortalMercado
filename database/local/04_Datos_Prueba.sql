-- Ejecutar el archivo COMPLETO, sin seleccionar fragmentos. No requiere modo SQLCMD.
USE [PortalMercado_ET01_Test];
SET XACT_ABORT ON;
IF ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
 OR CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')) <> HOST_NAME()
 THROW 50001, 'Se requiere SQLEXPRESS del mismo equipo que SSMS.', 1;

BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @IdMercado bigint, @IdSector bigint, @IdComerciante bigint, @IdNegocio bigint;
 SELECT @IdMercado=IdMercado FROM dbo.Mercado WHERE Nombre=N'Mercado de prueba ET01';
 IF @IdMercado IS NULL
 BEGIN
  INSERT dbo.Mercado(Nombre,Direccion,Activo,CreadoEn)
   VALUES(N'Mercado de prueba ET01',N'Dirección ficticia de pruebas',1,SYSUTCDATETIME());
  SET @IdMercado=SCOPE_IDENTITY();
  INSERT dbo.Sector(IdMercado,Codigo,Nombre,Activo)
   VALUES(@IdMercado,'ET01',N'Sector de prueba',1);
  SET @IdSector=SCOPE_IDENTITY();
  INSERT dbo.Puesto(IdSector,Codigo,Ubicacion,Estado)
   VALUES(@IdSector,'P-ET01',N'Ubicación ficticia','DISPONIBLE');
  INSERT dbo.Comerciante(TipoDocumento,NumeroDocumento,Nombres,Activo)
   VALUES('TEST','ET01-COMERCIANTE',N'Comerciante ficticio ET01',1);
  SET @IdComerciante=SCOPE_IDENTITY();
  INSERT dbo.Negocio(IdComerciante,IdMercado,NombreComercial,Slug,Estado,CreadoEn)
   VALUES(@IdComerciante,@IdMercado,N'Negocio de prueba ET01','negocio-prueba-et01','ACTIVO',SYSUTCDATETIME());
  INSERT dbo.Usuario(Correo,PasswordHash,Nombre,Activo,EmailConfirmado,DosFactoresHabilitado,CreadoEn)
   VALUES('et01@example.invalid',N'NO_ES_UN_HASH_VALIDO_USUARIO_DE_PRUEBA',N'Usuario ficticio ET01',0,0,0,SYSUTCDATETIME());
 END;
 -- Una segunda ejecucion valida el conjunto existente sin insertar duplicados.
 IF (SELECT COUNT(*) FROM dbo.Mercado WHERE Nombre=N'Mercado de prueba ET01')<>1
  OR (SELECT COUNT(*) FROM dbo.Sector WHERE IdMercado=@IdMercado AND Codigo='ET01')<>1
  OR (SELECT COUNT(*) FROM dbo.Puesto p JOIN dbo.Sector s ON p.IdSector=s.IdSector
      WHERE s.IdMercado=@IdMercado AND s.Codigo='ET01' AND p.Codigo='P-ET01')<>1
  OR (SELECT COUNT(*) FROM dbo.Negocio WHERE IdMercado=@IdMercado AND Slug='negocio-prueba-et01')<>1
  OR (SELECT COUNT(*) FROM dbo.Usuario WHERE Correo='et01@example.invalid' AND Activo=0)<>1
  THROW 50010, 'Conjunto de prueba incompleto o duplicado. No se modificaron datos existentes.', 1;
 SELECT @IdNegocio=IdNegocio FROM dbo.Negocio
  WHERE IdMercado=@IdMercado AND Slug='negocio-prueba-et01';
 COMMIT;
 SELECT @IdMercado AS IdMercadoPrueba,@IdNegocio AS IdNegocioPrueba;
 EXEC dbo.pm_ConsultarSaldos @IdNegocio=@IdNegocio;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
