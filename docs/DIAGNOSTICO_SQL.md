# Diagnóstico SQL - PM-ET01

## Insumo y trazabilidad

Se recibió un export único `Portalt.sql`, archivado por el usuario como
`database/original/Portal.sql`. Comparación de bytes: idénticos al adjunto original.
SHA256: `4caff0c5be1a255ddfa2fe5382c80815d4bbdd897e21bf5c119ce8a941a6556a`.
Incluye esquema y procedimientos; sustituye los dos archivos separados descritos
por la hoja como insumo de trabajo, sin afirmar aprobación formal de esa diferencia.

## Inventario verificado

| Control | Origen / export | Laboratorio aislado |
|---|---|---|
| Motor | SQL Server 2019 Enterprise, 15.0.2000.5 | SQL Server 2022 Express, 16.0.1000.6 |
| Base | Portal | PortalMercado_ET01_Test |
| Tablas dbo | 56 | 56 |
| SP dbo | 9 | 9 |
| FK | 84 | 84 |
| FK deshabilitadas/no confiables | Ninguna en la consulta recibida | Ninguna en la captura de verificación |

El análisis estático encontró 43 restricciones CHECK y referencias FK a tablas y
columnas existentes. La matriz detalla las cinco entidades y los nueve procedimientos.
No se validaron todos los flujos financieros ni se certifica producción.

## Adaptaciones de instalación

00 crea únicamente la base local vacía y fija compatibilidad 150.
01 contiene el esquema y sus restricciones; 02 contiene las nueve definiciones SP.
03 comprueba inventario, FK y parámetros de pm_ConsultarSaldos.
04 agrega datos ficticios reutilizables sin alterar la estructura.
Se omiten CREATE DATABASE Portal, rutas D:\datas19, usuario del origen y db_owner.
No se cambian cuerpos SP ni estructura original en 01/02. No se usan migraciones EF.

## Bitácora basada en evidencia recibida

| Fecha | Acción / hallazgo | Resultado y tratamiento |
|---|---|---|
| 2026-10-29 | Consulta de inventario en origen | 56/9/84; sin FK deshabilitadas/no confiables |
| 2026-10-29 | Conexión desde API | Cadena de certificado no confiable; ajuste temporal de desarrollo documentado |
| 2026-10-29 | Instalación local del esquema | Al repetir 01 se detuvo con 50002 porque ya había tablas; inventario confirmó 56 tablas y 84 FK; no se reinstaló encima |
| 2026-10-29 | Instalación de SP y verificación 03 | Captura confirmó 56 tablas, 9 SP, 84 FK y parámetro BIGINT de lectura |
| 2026-10-29 | Primer intento de semilla | Sintaxis cerca de ':' por SQLCMD desactivado; consulta posterior no encontró tabla al no aplicarse el contexto |
| 2026-10-29 | Reintento de semilla | Hubo error de ':' y se ejecutaron lotes posteriores; se comprobó persistencia mediante SELECT antes de repetir |
| 2026-10-29 | Verificación de semilla/SP | Mercado y negocio ET01 con identificador 1 en esa ejecución; tres importes cero |
| 2026-10-30 | Suite integrada recibida | TRX: 13 ejecutadas, 13 aprobadas, 0 omitidas, 0 fallidas |
| 2026-10-30 | Revisión del paquete | Faltaba 04 en el ZIP; se agregó versión de un solo lote sin directivas SQLCMD |

Las fechas de ejecución de capturas provienen de la conversación; el TRX incluido
retiene sus tiempos originales. No se inventan logs de instalación ni horas no registradas.
La bitácora justifica los errores: no equivale a una instalación original sin incidencias.

## Advertencia de agregación con NULL

pm_ConsultarSaldos usa LEFT JOIN y agregaciones; un negocio sin movimientos genera
valores NULL intermedios. SQL puede advertir su eliminación; COALESCE entrega ceros.
La consulta de prueba devolvió los tres ceros esperados. No se deshabilitó ANSI_WARNINGS
ni se modificó el SP original para ocultarla.

## Reversión y recuperación

No se aplica ninguna adaptación al servidor remoto. Conservar el export original.
Las guardas no permiten reinstalar encima de tablas existentes. Si falla 01/02,
conservar el error y el inventario parcial; no reintentar ciegamente.
Para revertir el laboratorio completo: detener los hosts y pruebas, confirmar servidor
local y nombre exacto, y retirar únicamente la base PortalMercado_ET01_Test mediante
SSMS con revisión explícita del destino. No se incluye un DROP automático.
Para conservar el esquema y retirar la semilla, revisar dependencias y borrar solo
registros ET01 en orden inverso dentro de una transacción; si hay nuevas referencias,
detenerse. No borrar datos por identificadores numéricos supuestos ni usar TRUNCATE.

Los cambios son una instalación aislada, no una migración de producción. No se detectó
necesidad de alterar el esquema original; cualquier incompatibilidad futura requiere
script incremental, reversión y aprobación antes de aplicarla.
