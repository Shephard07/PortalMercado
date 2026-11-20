-- Lectura de comprobación; no devuelve contactos ni datos fiscales.
SET NOCOUNT ON;
IF DB_NAME() <> N'PortalMercado_ET01_Test'
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
 OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')), N'') <> ISNULL(HOST_NAME(), N'')
 THROW 51100, 'Solo se permite PortalMercado_ET01_Test en el SQLEXPRESS de este equipo.', 1;

SELECT DB_NAME() AS BaseActual;
SELECT t.IdNegocio,n.Slug,t.Visible,t.TelefonoPublico,t.CorreoPublico,t.Version,t.UltimoActor,t.UltimoCambioUtc
FROM dbo.TiendaPublicacionLocal t JOIN dbo.Negocio n ON n.IdNegocio=t.IdNegocio ORDER BY t.IdNegocio;
SELECT TOP(20) IdAuditoria,IdUsuario,IdNegocio,Accion,Fecha FROM dbo.Auditoria
WHERE Accion='TIENDA_PERFIL_ACTUALIZADO' ORDER BY IdAuditoria DESC;
