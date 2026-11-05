USE [PortalMercado_ET01_Test];
GO
SELECT @@SERVERNAME AS Servidor,DB_NAME() AS BaseActual;
SELECT Codigo,AplicadoEn FROM dbo.SeguridadVersion ORDER BY Codigo;
SELECT (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')) AS TablasDbo,
(SELECT COUNT(*) FROM sys.procedures WHERE schema_id=SCHEMA_ID('dbo')) AS ProcedimientosDbo,
(SELECT COUNT(*) FROM sys.foreign_keys) AS ClavesForaneas;
SELECT name,is_disabled,is_not_trusted FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1;
-- Solo conteos y metadatos: nunca mostrar tokens, hashes, contraseñas o claves MFA.
SELECT COUNT(*) AS TokensRegistrados,
COALESCE(SUM(CASE WHEN ConsumidoEn IS NOT NULL THEN 1 ELSE 0 END),0) AS TokensConsumidos,
COALESCE(SUM(CASE WHEN RevocadoEn IS NOT NULL THEN 1 ELSE 0 END),0) AS TokensRevocados
FROM dbo.UsuarioTokenPassword;
SELECT name AS Indice FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.UsuarioTokenPassword') AND name IS NOT NULL;
-- Esperado tras ET02-004 sin otras ampliaciones: 66 tablas dbo, 9 SP y 92 FK. Sin FK inválidas.
