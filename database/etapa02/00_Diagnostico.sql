-- Solo lectura. No requiere SQLCMD. Usar únicamente la base local.
USE [PortalMercado_ET01_Test];
GO
SELECT @@SERVERNAME Servidor, DB_NAME() BaseActual;
SELECT t.name Tabla, c.name Columna, ty.name Tipo,c.max_length,c.is_nullable
FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE SCHEMA_NAME(t.schema_id)='dbo' AND t.name IN
('Usuario','Rol','Permiso','UsuarioRol','RolPermiso','UsuarioNegocio','UsuarioMercado',
 'UsuarioSeguridad','UsuarioSesion','AuditoriaAcceso','SeguridadVersion')
ORDER BY t.name,c.column_id;
-- Solo conteos: no expone correos, contraseñas ni hashes.
SELECT COUNT(*) GruposDuplicadosNormalizados FROM
(SELECT UPPER(LTRIM(RTRIM(Correo)) COLLATE Latin1_General_100_CI_AS) Correo
 FROM dbo.Usuario GROUP BY UPPER(LTRIM(RTRIM(Correo)) COLLATE Latin1_General_100_CI_AS)
 HAVING COUNT(*)>1) d;
SELECT COUNT(*) CuentasConSegundoFactorMarcado FROM dbo.Usuario WHERE DosFactoresHabilitado=1;
SELECT name,is_disabled,is_not_trusted FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1;
