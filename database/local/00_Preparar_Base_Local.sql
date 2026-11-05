:ON ERROR EXIT
-- Ejecutar en SSMS con Consulta > Modo SQLCMD activado.
USE [master];
GO
IF ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')) <> HOST_NAME()
    THROW 50001, 'Se requiere la instancia SQLEXPRESS del mismo equipo que SSMS.', 1;
GO
IF DB_ID(N'PortalMercado_ET01_Test') IS NULL
    EXEC(N'CREATE DATABASE [PortalMercado_ET01_Test]');
GO
USE [PortalMercado_ET01_Test];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
    THROW 50002, 'La base ya contiene tablas. No continuar ni reinstalar encima.', 1;
GO
ALTER DATABASE [PortalMercado_ET01_Test] SET COMPATIBILITY_LEVEL = 150;
GO
SELECT @@SERVERNAME AS Servidor, DB_NAME() AS BaseActual;
