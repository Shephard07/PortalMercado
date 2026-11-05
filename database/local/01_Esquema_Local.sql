:ON ERROR EXIT
-- Ejecutar en SSMS con Consulta > Modo SQLCMD activado.
USE [master];
GO
IF ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'') <> N'SQLEXPRESS'
   OR CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')) <> HOST_NAME()
    THROW 50001, 'Se requiere la instancia SQLEXPRESS del mismo equipo que SSMS.', 1;
GO
USE [PortalMercado_ET01_Test];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
    THROW 50002, 'La base ya contiene tablas. No continuar ni reinstalar encima.', 1;
GO
/****** Object:  Table [dbo].[Auditoria]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Auditoria](
	[IdAuditoria] [bigint] IDENTITY(1,1) NOT NULL,
	[IdUsuario] [bigint] NULL,
	[IdNegocio] [bigint] NULL,
	[Entidad] [varchar](100) NOT NULL,
	[IdRegistro] [varchar](100) NULL,
	[Accion] [varchar](60) NOT NULL,
	[Detalle] [nvarchar](max) NULL,
	[IP] [varchar](45) NULL,
	[Fecha] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdAuditoria] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Banner]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Banner](
	[IdBanner] [bigint] IDENTITY(1,1) NOT NULL,
	[IdMercado] [bigint] NOT NULL,
	[IdCampania] [bigint] NULL,
	[Titulo] [nvarchar](180) NOT NULL,
	[ImagenUrl] [nvarchar](500) NOT NULL,
	[DestinoUrl] [nvarchar](500) NULL,
	[Inicio] [datetime2](3) NOT NULL,
	[Fin] [datetime2](3) NOT NULL,
	[Activo] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdBanner] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Campania]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Campania](
	[IdCampania] [bigint] IDENTITY(1,1) NOT NULL,
	[IdMercado] [bigint] NOT NULL,
	[Nombre] [nvarchar](180) NOT NULL,
	[Inicio] [datetime2](3) NOT NULL,
	[Fin] [datetime2](3) NOT NULL,
	[Estado] [varchar](20) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdCampania] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Carrito]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Carrito](
	[IdCarrito] [bigint] IDENTITY(1,1) NOT NULL,
	[IdCliente] [bigint] NOT NULL,
	[Estado] [varchar](15) NOT NULL,
	[CreadoEn] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdCarrito] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CarritoDetalle]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CarritoDetalle](
	[IdCarritoDetalle] [bigint] IDENTITY(1,1) NOT NULL,
	[IdCarrito] [bigint] NOT NULL,
	[IdProducto] [bigint] NOT NULL,
	[Cantidad] [decimal](18, 3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdCarritoDetalle] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Categoria]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Categoria](
	[IdCategoria] [bigint] IDENTITY(1,1) NOT NULL,
	[IdPadre] [bigint] NULL,
	[Nombre] [nvarchar](130) NOT NULL,
	[Slug] [varchar](160) NOT NULL,
	[Activa] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdCategoria] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Slug] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Cliente]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Cliente](
	[IdCliente] [bigint] IDENTITY(1,1) NOT NULL,
	[IdUsuario] [bigint] NOT NULL,
	[TipoDocumento] [varchar](4) NULL,
	[NumeroDocumento] [varchar](20) NULL,
	[Apellidos] [nvarchar](150) NULL,
	[Celular] [varchar](30) NULL,
	[FechaRegistro] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdCliente] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[IdUsuario] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ClienteConsentimiento]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ClienteConsentimiento](
	[IdConsentimiento] [bigint] IDENTITY(1,1) NOT NULL,
	[IdCliente] [bigint] NOT NULL,
	[Tipo] [varchar](40) NOT NULL,
	[VersionDocumento] [varchar](40) NOT NULL,
	[Aceptado] [bit] NOT NULL,
	[RegistradoEn] [datetime2](3) NOT NULL,
	[IP] [varchar](45) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdConsentimiento] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ClienteDireccion]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ClienteDireccion](
	[IdDireccion] [bigint] IDENTITY(1,1) NOT NULL,
	[IdCliente] [bigint] NOT NULL,
	[Etiqueta] [nvarchar](80) NULL,
	[Direccion] [nvarchar](300) NOT NULL,
	[Referencia] [nvarchar](250) NULL,
	[Distrito] [nvarchar](120) NULL,
	[Provincia] [nvarchar](120) NULL,
	[Departamento] [nvarchar](120) NULL,
	[Latitud] [decimal](10, 7) NULL,
	[Longitud] [decimal](10, 7) NULL,
	[Principal] [bit] NOT NULL,
	[Activa] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdDireccion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Comerciante]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Comerciante](
	[IdComerciante] [bigint] IDENTITY(1,1) NOT NULL,
	[TipoDocumento] [varchar](4) NOT NULL,
	[NumeroDocumento] [varchar](20) NOT NULL,
	[Nombres] [nvarchar](150) NOT NULL,
	[Apellidos] [nvarchar](150) NULL,
	[Celular] [varchar](30) NULL,
	[Correo] [varchar](254) NULL,
	[Activo] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdComerciante] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Comprobante]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Comprobante](
	[IdComprobante] [bigint] IDENTITY(1,1) NOT NULL,
	[IdEmisor] [bigint] NOT NULL,
	[IdSerie] [bigint] NOT NULL,
	[IdPedidoNegocio] [bigint] NULL,
	[IdComprobanteReferencia] [bigint] NULL,
	[Tipo] [varchar](3) NOT NULL,
	[Numero] [bigint] NOT NULL,
	[FechaEmision] [datetime2](3) NOT NULL,
	[ClienteTipoDoc] [varchar](4) NULL,
	[ClienteNumeroDoc] [varchar](20) NULL,
	[ClienteNombre] [nvarchar](220) NULL,
	[Moneda] [char](3) NOT NULL,
	[Gravada] [decimal](18, 2) NOT NULL,
	[Exonerada] [decimal](18, 2) NOT NULL,
	[Inafecta] [decimal](18, 2) NOT NULL,
	[IGV] [decimal](18, 2) NOT NULL,
	[Total] [decimal](18, 2) NOT NULL,
	[Estado] [varchar](20) NOT NULL,
	[XmlUrl] [nvarchar](500) NULL,
	[PdfUrl] [nvarchar](500) NULL,
	[CdrUrl] [nvarchar](500) NULL,
	[HashDocumento] [varchar](128) NULL,
	[MotivoNota] [nvarchar](500) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdComprobante] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Comprobante] UNIQUE NONCLUSTERED 
(
	[IdSerie] ASC,
	[Numero] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ComprobanteDetalle]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ComprobanteDetalle](
	[IdComprobanteDetalle] [bigint] IDENTITY(1,1) NOT NULL,
	[IdComprobante] [bigint] NOT NULL,
	[IdProducto] [bigint] NULL,
	[Descripcion] [nvarchar](300) NOT NULL,
	[Cantidad] [decimal](18, 3) NOT NULL,
	[ValorUnitario] [decimal](18, 4) NOT NULL,
	[PrecioUnitario] [decimal](18, 4) NOT NULL,
	[IGV] [decimal](18, 2) NOT NULL,
	[Total] [decimal](18, 2) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdComprobanteDetalle] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ComprobanteEnvio]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ComprobanteEnvio](
	[IdEnvio] [bigint] IDENTITY(1,1) NOT NULL,
	[IdComprobante] [bigint] NOT NULL,
	[Fecha] [datetime2](3) NOT NULL,
	[Estado] [varchar](30) NOT NULL,
	[CodigoRespuesta] [varchar](80) NULL,
	[Mensaje] [nvarchar](1000) NULL,
	[Correlacion] [varchar](160) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdEnvio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ConciliacionDetalle]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ConciliacionDetalle](
	[IdConciliacionDetalle] [bigint] IDENTITY(1,1) NOT NULL,
	[IdConciliacion] [bigint] NOT NULL,
	[IdTransaccion] [bigint] NOT NULL,
	[MontoConciliado] [decimal](18, 2) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdConciliacionDetalle] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ConciliacionDetalle] UNIQUE NONCLUSTERED 
(
	[IdConciliacion] ASC,
	[IdTransaccion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ConciliacionPago]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ConciliacionPago](
	[IdConciliacion] [bigint] IDENTITY(1,1) NOT NULL,
	[Proveedor] [varchar](70) NOT NULL,
	[FechaCorte] [date] NOT NULL,
	[MontoReportado] [decimal](18, 2) NOT NULL,
	[MontoSistema] [decimal](18, 2) NOT NULL,
	[Diferencia]  AS ([MontoReportado]-[MontoSistema]) PERSISTED,
	[Estado] [varchar](20) NOT NULL,
	[IdUsuario] [bigint] NULL,
	[Fecha] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdConciliacion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CuentaComerciante]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CuentaComerciante](
	[IdCuenta] [bigint] IDENTITY(1,1) NOT NULL,
	[IdNegocio] [bigint] NOT NULL,
	[Titular] [nvarchar](220) NOT NULL,
	[Banco] [nvarchar](120) NOT NULL,
	[NumeroCuentaProtegido] [varbinary](max) NULL,
	[Ultimos4] [char](4) NULL,
	[CCIProtegido] [varbinary](max) NULL,
	[Verificada] [bit] NOT NULL,
	[Activa] [bit] NOT NULL,
	[ActualizadaEn] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdCuenta] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[IdNegocio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EmisorFiscal]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EmisorFiscal](
	[IdEmisor] [bigint] IDENTITY(1,1) NOT NULL,
	[IdNegocio] [bigint] NOT NULL,
	[RUC] [varchar](11) NOT NULL,
	[RazonSocial] [nvarchar](220) NOT NULL,
	[DireccionFiscal] [nvarchar](300) NOT NULL,
	[Regimen] [varchar](40) NULL,
	[Modalidad] [varchar](30) NOT NULL,
	[Habilitado] [bit] NOT NULL,
	[ConfiguracionSecretaRef] [nvarchar](300) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdEmisor] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[IdNegocio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Entrega]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Entrega](
	[IdEntrega] [bigint] IDENTITY(1,1) NOT NULL,
	[IdPedido] [bigint] NOT NULL,
	[Estado] [varchar](20) NOT NULL,
	[Operador] [nvarchar](180) NULL,
	[CodigoValidacionHash] [varbinary](64) NULL,
	[FechaProgramada] [datetime2](3) NULL,
	[FechaEntregada] [datetime2](3) NULL,
	[EvidenciaUrl] [nvarchar](500) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdEntrega] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[IdPedido] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EntregaEvento]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EntregaEvento](
	[IdEvento] [bigint] IDENTITY(1,1) NOT NULL,
	[IdEntrega] [bigint] NOT NULL,
	[Estado] [varchar](20) NOT NULL,
	[Descripcion] [nvarchar](500) NULL,
	[IdUsuario] [bigint] NULL,
	[Fecha] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdEvento] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Inventario]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Inventario](
	[IdProducto] [bigint] NOT NULL,
	[Existencia] [decimal](18, 3) NOT NULL,
	[Reservado] [decimal](18, 3) NOT NULL,
	[ActualizadoEn] [datetime2](3) NOT NULL,
	[RowVer] [timestamp] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdProducto] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Liquidacion]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Liquidacion](
	[IdLiquidacion] [bigint] IDENTITY(1,1) NOT NULL,
	[IdNegocio] [bigint] NOT NULL,
	[IdCuenta] [bigint] NOT NULL,
	[PeriodoDesde] [datetime2](3) NOT NULL,
	[PeriodoHasta] [datetime2](3) NOT NULL,
	[Total] [decimal](18, 2) NOT NULL,
	[Estado] [varchar](20) NOT NULL,
	[CreadaEn] [datetime2](3) NOT NULL,
	[AprobadaEn] [datetime2](3) NULL,
	[IdAprobador] [bigint] NULL,
PRIMARY KEY CLUSTERED 
(
	[IdLiquidacion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[LiquidacionDetalle]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LiquidacionDetalle](
	[IdLiquidacionDetalle] [bigint] IDENTITY(1,1) NOT NULL,
	[IdLiquidacion] [bigint] NOT NULL,
	[IdMovimientoCuenta] [bigint] NOT NULL,
	[Importe] [decimal](18, 2) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdLiquidacionDetalle] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[IdMovimientoCuenta] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Mercado]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Mercado](
	[IdMercado] [bigint] IDENTITY(1,1) NOT NULL,
	[Nombre] [nvarchar](180) NOT NULL,
	[RUC] [varchar](11) NULL,
	[Direccion] [nvarchar](250) NULL,
	[Telefono] [varchar](30) NULL,
	[Correo] [varchar](254) NULL,
	[LogoUrl] [nvarchar](500) NULL,
	[Activo] [bit] NOT NULL,
	[CreadoEn] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdMercado] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MovimientoCuenta]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MovimientoCuenta](
	[IdMovimientoCuenta] [bigint] IDENTITY(1,1) NOT NULL,
	[IdNegocio] [bigint] NOT NULL,
	[IdPedidoNegocio] [bigint] NULL,
	[IdReembolso] [bigint] NULL,
	[Tipo] [varchar](25) NOT NULL,
	[Importe] [decimal](18, 2) NOT NULL,
	[Fecha] [datetime2](3) NOT NULL,
	[Referencia] [varchar](100) NOT NULL,
	[Descripcion] [nvarchar](300) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdMovimientoCuenta] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_MovCuenta_Referencia] UNIQUE NONCLUSTERED 
(
	[IdNegocio] ASC,
	[Referencia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MovimientoInventario]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MovimientoInventario](
	[IdMovimiento] [bigint] IDENTITY(1,1) NOT NULL,
	[IdProducto] [bigint] NOT NULL,
	[Tipo] [varchar](20) NOT NULL,
	[Cantidad] [decimal](18, 3) NOT NULL,
	[Motivo] [nvarchar](250) NULL,
	[RegistradoEn] [datetime2](3) NOT NULL,
	[IdUsuario] [bigint] NULL,
PRIMARY KEY CLUSTERED 
(
	[IdMovimiento] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Negocio]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Negocio](
	[IdNegocio] [bigint] IDENTITY(1,1) NOT NULL,
	[IdComerciante] [bigint] NOT NULL,
	[IdMercado] [bigint] NOT NULL,
	[NombreComercial] [nvarchar](180) NOT NULL,
	[Slug] [varchar](160) NOT NULL,
	[RUC] [varchar](11) NULL,
	[RazonSocial] [nvarchar](220) NULL,
	[DireccionFiscal] [nvarchar](250) NULL,
	[Descripcion] [nvarchar](max) NULL,
	[LogoUrl] [nvarchar](500) NULL,
	[Telefono] [varchar](30) NULL,
	[Correo] [varchar](254) NULL,
	[Horario] [nvarchar](500) NULL,
	[Estado] [varchar](20) NOT NULL,
	[CreadoEn] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdNegocio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[NegocioPuesto]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[NegocioPuesto](
	[IdNegocioPuesto] [bigint] IDENTITY(1,1) NOT NULL,
	[IdNegocio] [bigint] NOT NULL,
	[IdPuesto] [bigint] NOT NULL,
	[Desde] [date] NOT NULL,
	[Hasta] [date] NULL,
PRIMARY KEY CLUSTERED 
(
	[IdNegocioPuesto] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Notificacion]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Notificacion](
	[IdNotificacion] [bigint] IDENTITY(1,1) NOT NULL,
	[IdUsuario] [bigint] NOT NULL,
	[Canal] [varchar](20) NOT NULL,
	[Asunto] [nvarchar](200) NULL,
	[Mensaje] [nvarchar](max) NOT NULL,
	[Estado] [varchar](20) NOT NULL,
	[CreadaEn] [datetime2](3) NOT NULL,
	[EnviadaEn] [datetime2](3) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdNotificacion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OrdenPago]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OrdenPago](
	[IdOrdenPago] [bigint] IDENTITY(1,1) NOT NULL,
	[IdPedido] [bigint] NOT NULL,
	[Moneda] [char](3) NOT NULL,
	[Monto] [decimal](18, 2) NOT NULL,
	[Estado] [varchar](20) NOT NULL,
	[IdempotencyKey] [varchar](120) NOT NULL,
	[CreadaEn] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdOrdenPago] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[IdPedido] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[IdempotencyKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ParametroSistema]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ParametroSistema](
	[Clave] [varchar](120) NOT NULL,
	[Valor] [nvarchar](2000) NULL,
	[Descripcion] [nvarchar](300) NULL,
PRIMARY KEY CLUSTERED 
(
	[Clave] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Pedido]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Pedido](
	[IdPedido] [bigint] IDENTITY(1,1) NOT NULL,
	[Codigo] [varchar](35) NOT NULL,
	[IdCliente] [bigint] NOT NULL,
	[IdMercado] [bigint] NOT NULL,
	[IdDireccion] [bigint] NULL,
	[IdZona] [bigint] NULL,
	[ModalidadEntrega] [varchar](15) NOT NULL,
	[Estado] [varchar](25) NOT NULL,
	[EstadoPago] [varchar](20) NOT NULL,
	[Subtotal] [decimal](18, 2) NOT NULL,
	[Descuento] [decimal](18, 2) NOT NULL,
	[CostoEntrega] [decimal](18, 2) NOT NULL,
	[Total] [decimal](18, 2) NOT NULL,
	[Fecha] [datetime2](3) NOT NULL,
	[Observaciones] [nvarchar](500) NULL,
	[RowVer] [timestamp] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdPedido] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Codigo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PedidoDetalle]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PedidoDetalle](
	[IdPedidoDetalle] [bigint] IDENTITY(1,1) NOT NULL,
	[IdPedidoNegocio] [bigint] NOT NULL,
	[IdProducto] [bigint] NOT NULL,
	[ProductoNombre] [nvarchar](200) NOT NULL,
	[UnidadCodigo] [varchar](20) NOT NULL,
	[Cantidad] [decimal](18, 3) NOT NULL,
	[PrecioUnitario] [decimal](18, 4) NOT NULL,
	[Descuento] [decimal](18, 2) NOT NULL,
	[Total] [decimal](18, 2) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdPedidoDetalle] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PedidoEstadoHistorial]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PedidoEstadoHistorial](
	[IdHistorial] [bigint] IDENTITY(1,1) NOT NULL,
	[IdPedidoNegocio] [bigint] NOT NULL,
	[EstadoAnterior] [varchar](25) NULL,
	[EstadoNuevo] [varchar](25) NOT NULL,
	[Motivo] [nvarchar](500) NULL,
	[IdUsuario] [bigint] NULL,
	[Fecha] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdHistorial] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PedidoNegocio]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PedidoNegocio](
	[IdPedidoNegocio] [bigint] IDENTITY(1,1) NOT NULL,
	[IdPedido] [bigint] NOT NULL,
	[IdNegocio] [bigint] NOT NULL,
	[Estado] [varchar](25) NOT NULL,
	[Subtotal] [decimal](18, 2) NOT NULL,
	[Descuento] [decimal](18, 2) NOT NULL,
	[Total] [decimal](18, 2) NOT NULL,
	[FechaConfirmacion] [datetime2](3) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdPedidoNegocio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_PedidoNegocio] UNIQUE NONCLUSTERED 
(
	[IdPedido] ASC,
	[IdNegocio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Permiso]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Permiso](
	[IdPermiso] [int] IDENTITY(1,1) NOT NULL,
	[Codigo] [varchar](90) NOT NULL,
	[Descripcion] [nvarchar](250) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdPermiso] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Codigo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Producto]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Producto](
	[IdProducto] [bigint] IDENTITY(1,1) NOT NULL,
	[IdNegocio] [bigint] NOT NULL,
	[IdCategoria] [bigint] NOT NULL,
	[IdUnidad] [int] NOT NULL,
	[SKU] [varchar](70) NULL,
	[Nombre] [nvarchar](200) NOT NULL,
	[Descripcion] [nvarchar](max) NULL,
	[Precio] [decimal](18, 2) NOT NULL,
	[ControlaStock] [bit] NOT NULL,
	[Estado] [varchar](20) NOT NULL,
	[CreadoEn] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdProducto] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductoImagen]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductoImagen](
	[IdImagen] [bigint] IDENTITY(1,1) NOT NULL,
	[IdProducto] [bigint] NOT NULL,
	[Url] [nvarchar](500) NOT NULL,
	[Orden] [int] NOT NULL,
	[Principal] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdImagen] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductoPrecio]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductoPrecio](
	[IdProductoPrecio] [bigint] IDENTITY(1,1) NOT NULL,
	[IdProducto] [bigint] NOT NULL,
	[Precio] [decimal](18, 2) NOT NULL,
	[VigenteDesde] [datetime2](3) NOT NULL,
	[VigenteHasta] [datetime2](3) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdProductoPrecio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Promocion]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Promocion](
	[IdPromocion] [bigint] IDENTITY(1,1) NOT NULL,
	[IdCampania] [bigint] NULL,
	[IdNegocio] [bigint] NOT NULL,
	[IdProducto] [bigint] NOT NULL,
	[PrecioPromocional] [decimal](18, 2) NOT NULL,
	[Inicio] [datetime2](3) NOT NULL,
	[Fin] [datetime2](3) NOT NULL,
	[Aprobada] [bit] NOT NULL,
	[Financiador] [varchar](20) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdPromocion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Puesto]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Puesto](
	[IdPuesto] [bigint] IDENTITY(1,1) NOT NULL,
	[IdSector] [bigint] NOT NULL,
	[Codigo] [varchar](30) NOT NULL,
	[Ubicacion] [nvarchar](250) NULL,
	[Estado] [varchar](20) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdPuesto] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Reembolso]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Reembolso](
	[IdReembolso] [bigint] IDENTITY(1,1) NOT NULL,
	[IdTransaccionOriginal] [bigint] NOT NULL,
	[IdPedidoNegocio] [bigint] NULL,
	[Monto] [decimal](18, 2) NOT NULL,
	[Motivo] [nvarchar](500) NOT NULL,
	[Estado] [varchar](20) NOT NULL,
	[ReferenciaExterna] [varchar](160) NULL,
	[Fecha] [datetime2](3) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdReembolso] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Rol]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Rol](
	[IdRol] [int] IDENTITY(1,1) NOT NULL,
	[Codigo] [varchar](50) NOT NULL,
	[Nombre] [nvarchar](100) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdRol] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Codigo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[RolPermiso]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RolPermiso](
	[IdRol] [int] NOT NULL,
	[IdPermiso] [int] NOT NULL,
 CONSTRAINT [PK_RolPermiso] PRIMARY KEY CLUSTERED 
(
	[IdRol] ASC,
	[IdPermiso] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Sector]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Sector](
	[IdSector] [bigint] IDENTITY(1,1) NOT NULL,
	[IdMercado] [bigint] NOT NULL,
	[Codigo] [varchar](30) NOT NULL,
	[Nombre] [nvarchar](120) NOT NULL,
	[Activo] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdSector] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SerieComprobante]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SerieComprobante](
	[IdSerie] [bigint] IDENTITY(1,1) NOT NULL,
	[IdEmisor] [bigint] NOT NULL,
	[Tipo] [varchar](3) NOT NULL,
	[Serie] [varchar](8) NOT NULL,
	[SiguienteNumero] [bigint] NOT NULL,
	[Activa] [bit] NOT NULL,
	[RowVer] [timestamp] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdSerie] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Serie] UNIQUE NONCLUSTERED 
(
	[IdEmisor] ASC,
	[Tipo] ASC,
	[Serie] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TarifaEntrega]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TarifaEntrega](
	[IdTarifa] [bigint] IDENTITY(1,1) NOT NULL,
	[IdZona] [bigint] NOT NULL,
	[Monto] [decimal](18, 2) NOT NULL,
	[Desde] [datetime2](3) NOT NULL,
	[Hasta] [datetime2](3) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdTarifa] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TransaccionPago]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TransaccionPago](
	[IdTransaccion] [bigint] IDENTITY(1,1) NOT NULL,
	[IdOrdenPago] [bigint] NOT NULL,
	[Proveedor] [varchar](70) NOT NULL,
	[ReferenciaExterna] [varchar](160) NULL,
	[Tipo] [varchar](20) NOT NULL,
	[Estado] [varchar](20) NOT NULL,
	[Monto] [decimal](18, 2) NOT NULL,
	[Moneda] [char](3) NOT NULL,
	[Fecha] [datetime2](3) NOT NULL,
	[RespuestaCodigo] [varchar](60) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdTransaccion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Transferencia]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Transferencia](
	[IdTransferencia] [bigint] IDENTITY(1,1) NOT NULL,
	[IdLiquidacion] [bigint] NOT NULL,
	[ReferenciaBanco] [varchar](160) NULL,
	[Monto] [decimal](18, 2) NOT NULL,
	[Estado] [varchar](20) NOT NULL,
	[Fecha] [datetime2](3) NULL,
	[IdUsuario] [bigint] NULL,
PRIMARY KEY CLUSTERED 
(
	[IdTransferencia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[IdLiquidacion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UnidadMedida]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UnidadMedida](
	[IdUnidad] [int] IDENTITY(1,1) NOT NULL,
	[Codigo] [varchar](20) NOT NULL,
	[Nombre] [nvarchar](100) NOT NULL,
	[PermiteFraccion] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdUnidad] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Codigo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Usuario]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Usuario](
	[IdUsuario] [bigint] IDENTITY(1,1) NOT NULL,
	[Correo] [varchar](254) NOT NULL,
	[PasswordHash] [nvarchar](500) NOT NULL,
	[Nombre] [nvarchar](150) NOT NULL,
	[Telefono] [varchar](30) NULL,
	[Activo] [bit] NOT NULL,
	[EmailConfirmado] [bit] NOT NULL,
	[DosFactoresHabilitado] [bit] NOT NULL,
	[CreadoEn] [datetime2](3) NOT NULL,
	[UltimoAcceso] [datetime2](3) NULL,
PRIMARY KEY CLUSTERED 
(
	[IdUsuario] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UsuarioMercado]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UsuarioMercado](
	[IdUsuario] [bigint] NOT NULL,
	[IdMercado] [bigint] NOT NULL,
 CONSTRAINT [PK_UsuarioMercado] PRIMARY KEY CLUSTERED 
(
	[IdUsuario] ASC,
	[IdMercado] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UsuarioNegocio]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UsuarioNegocio](
	[IdUsuario] [bigint] NOT NULL,
	[IdNegocio] [bigint] NOT NULL,
	[Activo] [bit] NOT NULL,
 CONSTRAINT [PK_UsuarioNegocio] PRIMARY KEY CLUSTERED 
(
	[IdUsuario] ASC,
	[IdNegocio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UsuarioRol]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UsuarioRol](
	[IdUsuario] [bigint] NOT NULL,
	[IdRol] [int] NOT NULL,
 CONSTRAINT [PK_UsuarioRol] PRIMARY KEY CLUSTERED 
(
	[IdUsuario] ASC,
	[IdRol] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[VentaDirecta]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[VentaDirecta](
	[IdVentaDirecta] [bigint] IDENTITY(1,1) NOT NULL,
	[IdNegocio] [bigint] NOT NULL,
	[IdUsuario] [bigint] NOT NULL,
	[Fecha] [datetime2](3) NOT NULL,
	[Total] [decimal](18, 2) NOT NULL,
	[Estado] [varchar](20) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdVentaDirecta] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[VentaDirectaDetalle]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[VentaDirectaDetalle](
	[IdVentaDirectaDetalle] [bigint] IDENTITY(1,1) NOT NULL,
	[IdVentaDirecta] [bigint] NOT NULL,
	[IdProducto] [bigint] NULL,
	[Descripcion] [nvarchar](200) NOT NULL,
	[Cantidad] [decimal](18, 3) NOT NULL,
	[Precio] [decimal](18, 4) NOT NULL,
	[Total] [decimal](18, 2) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdVentaDirectaDetalle] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ZonaEntrega]    Script Date: 29/10/2026 11:53:55 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ZonaEntrega](
	[IdZona] [bigint] IDENTITY(1,1) NOT NULL,
	[IdMercado] [bigint] NOT NULL,
	[Nombre] [nvarchar](130) NOT NULL,
	[Descripcion] [nvarchar](250) NULL,
	[Activa] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdZona] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Index [IX_Auditoria_Fecha]    Script Date: 29/10/2026 11:53:55 ******/
CREATE NONCLUSTERED INDEX [IX_Auditoria_Fecha] ON [dbo].[Auditoria]
(
	[Fecha] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_CarritoDetalle_Producto]    Script Date: 29/10/2026 11:53:55 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_CarritoDetalle_Producto] ON [dbo].[CarritoDetalle]
(
	[IdCarrito] ASC,
	[IdProducto] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_ClienteDireccion_Principal]    Script Date: 29/10/2026 11:53:55 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_ClienteDireccion_Principal] ON [dbo].[ClienteDireccion]
(
	[IdCliente] ASC
)
WHERE ([Principal]=(1) AND [Activa]=(1))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Comerciante_Documento]    Script Date: 29/10/2026 11:53:55 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Comerciante_Documento] ON [dbo].[Comerciante]
(
	[TipoDocumento] ASC,
	[NumeroDocumento] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Comprobante_EmisorFecha]    Script Date: 29/10/2026 11:53:55 ******/
CREATE NONCLUSTERED INDEX [IX_Comprobante_EmisorFecha] ON [dbo].[Comprobante]
(
	[IdEmisor] ASC,
	[FechaEmision] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Liquidacion_NegocioEstado]    Script Date: 29/10/2026 11:53:55 ******/
CREATE NONCLUSTERED INDEX [IX_Liquidacion_NegocioEstado] ON [dbo].[Liquidacion]
(
	[IdNegocio] ASC,
	[Estado] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_MovimientoCuenta_NegocioFecha]    Script Date: 29/10/2026 11:53:55 ******/
CREATE NONCLUSTERED INDEX [IX_MovimientoCuenta_NegocioFecha] ON [dbo].[MovimientoCuenta]
(
	[IdNegocio] ASC,
	[Fecha] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Negocio_MercadoSlug]    Script Date: 29/10/2026 11:53:55 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Negocio_MercadoSlug] ON [dbo].[Negocio]
(
	[IdMercado] ASC,
	[Slug] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_NegocioPuesto_Activo]    Script Date: 29/10/2026 11:53:55 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_NegocioPuesto_Activo] ON [dbo].[NegocioPuesto]
(
	[IdPuesto] ASC
)
WHERE ([Hasta] IS NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Pedido_ClienteFecha]    Script Date: 29/10/2026 11:53:55 ******/
CREATE NONCLUSTERED INDEX [IX_Pedido_ClienteFecha] ON [dbo].[Pedido]
(
	[IdCliente] ASC,
	[Fecha] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_PedidoNegocio_NegocioEstado]    Script Date: 29/10/2026 11:53:55 ******/
CREATE NONCLUSTERED INDEX [IX_PedidoNegocio_NegocioEstado] ON [dbo].[PedidoNegocio]
(
	[IdNegocio] ASC,
	[Estado] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Producto_Categoria]    Script Date: 29/10/2026 11:53:55 ******/
CREATE NONCLUSTERED INDEX [IX_Producto_Categoria] ON [dbo].[Producto]
(
	[IdCategoria] ASC,
	[Estado] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Producto_NegocioEstado]    Script Date: 29/10/2026 11:53:55 ******/
CREATE NONCLUSTERED INDEX [IX_Producto_NegocioEstado] ON [dbo].[Producto]
(
	[IdNegocio] ASC,
	[Estado] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Producto_NegocioSKU]    Script Date: 29/10/2026 11:53:55 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Producto_NegocioSKU] ON [dbo].[Producto]
(
	[IdNegocio] ASC,
	[SKU] ASC
)
WHERE ([SKU] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Puesto_SectorCodigo]    Script Date: 29/10/2026 11:53:55 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Puesto_SectorCodigo] ON [dbo].[Puesto]
(
	[IdSector] ASC,
	[Codigo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Sector_MercadoCodigo]    Script Date: 29/10/2026 11:53:55 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Sector_MercadoCodigo] ON [dbo].[Sector]
(
	[IdMercado] ASC,
	[Codigo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_TransaccionPago_Orden]    Script Date: 29/10/2026 11:53:55 ******/
CREATE NONCLUSTERED INDEX [IX_TransaccionPago_Orden] ON [dbo].[TransaccionPago]
(
	[IdOrdenPago] ASC,
	[Fecha] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Transaccion_Externa]    Script Date: 29/10/2026 11:53:55 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Transaccion_Externa] ON [dbo].[TransaccionPago]
(
	[Proveedor] ASC,
	[ReferenciaExterna] ASC,
	[Tipo] ASC
)
WHERE ([ReferenciaExterna] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_TransaccionPago_Referencia]    Script Date: 29/10/2026 11:53:55 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_TransaccionPago_Referencia] ON [dbo].[TransaccionPago]
(
	[Proveedor] ASC,
	[ReferenciaExterna] ASC,
	[Tipo] ASC
)
WHERE ([ReferenciaExterna] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Usuario_Correo]    Script Date: 29/10/2026 11:53:55 ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Usuario_Correo] ON [dbo].[Usuario]
(
	[Correo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Auditoria] ADD  DEFAULT (sysutcdatetime()) FOR [Fecha]
GO
ALTER TABLE [dbo].[Banner] ADD  DEFAULT ((1)) FOR [Activo]
GO
ALTER TABLE [dbo].[Campania] ADD  DEFAULT ('BORRADOR') FOR [Estado]
GO
ALTER TABLE [dbo].[Carrito] ADD  DEFAULT ('ABIERTO') FOR [Estado]
GO
ALTER TABLE [dbo].[Carrito] ADD  DEFAULT (sysutcdatetime()) FOR [CreadoEn]
GO
ALTER TABLE [dbo].[Categoria] ADD  DEFAULT ((1)) FOR [Activa]
GO
ALTER TABLE [dbo].[Cliente] ADD  DEFAULT (sysutcdatetime()) FOR [FechaRegistro]
GO
ALTER TABLE [dbo].[ClienteConsentimiento] ADD  DEFAULT (sysutcdatetime()) FOR [RegistradoEn]
GO
ALTER TABLE [dbo].[ClienteDireccion] ADD  DEFAULT ((0)) FOR [Principal]
GO
ALTER TABLE [dbo].[ClienteDireccion] ADD  DEFAULT ((1)) FOR [Activa]
GO
ALTER TABLE [dbo].[Comerciante] ADD  DEFAULT ((1)) FOR [Activo]
GO
ALTER TABLE [dbo].[Comprobante] ADD  DEFAULT ('PEN') FOR [Moneda]
GO
ALTER TABLE [dbo].[Comprobante] ADD  DEFAULT ((0)) FOR [Gravada]
GO
ALTER TABLE [dbo].[Comprobante] ADD  DEFAULT ((0)) FOR [Exonerada]
GO
ALTER TABLE [dbo].[Comprobante] ADD  DEFAULT ((0)) FOR [Inafecta]
GO
ALTER TABLE [dbo].[Comprobante] ADD  DEFAULT ((0)) FOR [IGV]
GO
ALTER TABLE [dbo].[Comprobante] ADD  DEFAULT ('PENDIENTE') FOR [Estado]
GO
ALTER TABLE [dbo].[ComprobanteDetalle] ADD  DEFAULT ((0)) FOR [IGV]
GO
ALTER TABLE [dbo].[ComprobanteEnvio] ADD  DEFAULT (sysutcdatetime()) FOR [Fecha]
GO
ALTER TABLE [dbo].[ConciliacionPago] ADD  DEFAULT ('PENDIENTE') FOR [Estado]
GO
ALTER TABLE [dbo].[ConciliacionPago] ADD  DEFAULT (sysutcdatetime()) FOR [Fecha]
GO
ALTER TABLE [dbo].[CuentaComerciante] ADD  DEFAULT ((0)) FOR [Verificada]
GO
ALTER TABLE [dbo].[CuentaComerciante] ADD  DEFAULT ((1)) FOR [Activa]
GO
ALTER TABLE [dbo].[CuentaComerciante] ADD  DEFAULT (sysutcdatetime()) FOR [ActualizadaEn]
GO
ALTER TABLE [dbo].[EmisorFiscal] ADD  DEFAULT ((0)) FOR [Habilitado]
GO
ALTER TABLE [dbo].[Entrega] ADD  DEFAULT ('PENDIENTE') FOR [Estado]
GO
ALTER TABLE [dbo].[EntregaEvento] ADD  DEFAULT (sysutcdatetime()) FOR [Fecha]
GO
ALTER TABLE [dbo].[Inventario] ADD  DEFAULT ((0)) FOR [Existencia]
GO
ALTER TABLE [dbo].[Inventario] ADD  DEFAULT ((0)) FOR [Reservado]
GO
ALTER TABLE [dbo].[Inventario] ADD  DEFAULT (sysutcdatetime()) FOR [ActualizadoEn]
GO
ALTER TABLE [dbo].[Liquidacion] ADD  DEFAULT ('PENDIENTE') FOR [Estado]
GO
ALTER TABLE [dbo].[Liquidacion] ADD  DEFAULT (sysutcdatetime()) FOR [CreadaEn]
GO
ALTER TABLE [dbo].[Mercado] ADD  DEFAULT ((1)) FOR [Activo]
GO
ALTER TABLE [dbo].[Mercado] ADD  DEFAULT (sysutcdatetime()) FOR [CreadoEn]
GO
ALTER TABLE [dbo].[MovimientoCuenta] ADD  DEFAULT (sysutcdatetime()) FOR [Fecha]
GO
ALTER TABLE [dbo].[MovimientoInventario] ADD  DEFAULT (sysutcdatetime()) FOR [RegistradoEn]
GO
ALTER TABLE [dbo].[Negocio] ADD  DEFAULT ('PENDIENTE') FOR [Estado]
GO
ALTER TABLE [dbo].[Negocio] ADD  DEFAULT (sysutcdatetime()) FOR [CreadoEn]
GO
ALTER TABLE [dbo].[NegocioPuesto] ADD  DEFAULT (CONVERT([date],getdate())) FOR [Desde]
GO
ALTER TABLE [dbo].[Notificacion] ADD  DEFAULT ('PENDIENTE') FOR [Estado]
GO
ALTER TABLE [dbo].[Notificacion] ADD  DEFAULT (sysutcdatetime()) FOR [CreadaEn]
GO
ALTER TABLE [dbo].[OrdenPago] ADD  DEFAULT ('PEN') FOR [Moneda]
GO
ALTER TABLE [dbo].[OrdenPago] ADD  DEFAULT ('PENDIENTE') FOR [Estado]
GO
ALTER TABLE [dbo].[OrdenPago] ADD  DEFAULT (sysutcdatetime()) FOR [CreadaEn]
GO
ALTER TABLE [dbo].[Pedido] ADD  DEFAULT ('REGISTRADO') FOR [Estado]
GO
ALTER TABLE [dbo].[Pedido] ADD  DEFAULT ('PENDIENTE') FOR [EstadoPago]
GO
ALTER TABLE [dbo].[Pedido] ADD  DEFAULT ((0)) FOR [Descuento]
GO
ALTER TABLE [dbo].[Pedido] ADD  DEFAULT ((0)) FOR [CostoEntrega]
GO
ALTER TABLE [dbo].[Pedido] ADD  DEFAULT (sysutcdatetime()) FOR [Fecha]
GO
ALTER TABLE [dbo].[PedidoDetalle] ADD  DEFAULT ((0)) FOR [Descuento]
GO
ALTER TABLE [dbo].[PedidoEstadoHistorial] ADD  DEFAULT (sysutcdatetime()) FOR [Fecha]
GO
ALTER TABLE [dbo].[PedidoNegocio] ADD  DEFAULT ('PENDIENTE') FOR [Estado]
GO
ALTER TABLE [dbo].[PedidoNegocio] ADD  DEFAULT ((0)) FOR [Descuento]
GO
ALTER TABLE [dbo].[Producto] ADD  DEFAULT ((0)) FOR [ControlaStock]
GO
ALTER TABLE [dbo].[Producto] ADD  DEFAULT ('BORRADOR') FOR [Estado]
GO
ALTER TABLE [dbo].[Producto] ADD  DEFAULT (sysutcdatetime()) FOR [CreadoEn]
GO
ALTER TABLE [dbo].[ProductoImagen] ADD  DEFAULT ((0)) FOR [Orden]
GO
ALTER TABLE [dbo].[ProductoImagen] ADD  DEFAULT ((0)) FOR [Principal]
GO
ALTER TABLE [dbo].[Promocion] ADD  DEFAULT ((0)) FOR [Aprobada]
GO
ALTER TABLE [dbo].[Promocion] ADD  DEFAULT ('COMERCIANTE') FOR [Financiador]
GO
ALTER TABLE [dbo].[Puesto] ADD  DEFAULT ('DISPONIBLE') FOR [Estado]
GO
ALTER TABLE [dbo].[Reembolso] ADD  DEFAULT ('SOLICITADO') FOR [Estado]
GO
ALTER TABLE [dbo].[Reembolso] ADD  DEFAULT (sysutcdatetime()) FOR [Fecha]
GO
ALTER TABLE [dbo].[Sector] ADD  DEFAULT ((1)) FOR [Activo]
GO
ALTER TABLE [dbo].[SerieComprobante] ADD  DEFAULT ((1)) FOR [SiguienteNumero]
GO
ALTER TABLE [dbo].[SerieComprobante] ADD  DEFAULT ((1)) FOR [Activa]
GO
ALTER TABLE [dbo].[TransaccionPago] ADD  DEFAULT ('PEN') FOR [Moneda]
GO
ALTER TABLE [dbo].[TransaccionPago] ADD  DEFAULT (sysutcdatetime()) FOR [Fecha]
GO
ALTER TABLE [dbo].[Transferencia] ADD  DEFAULT ('PENDIENTE') FOR [Estado]
GO
ALTER TABLE [dbo].[UnidadMedida] ADD  DEFAULT ((0)) FOR [PermiteFraccion]
GO
ALTER TABLE [dbo].[Usuario] ADD  DEFAULT ((1)) FOR [Activo]
GO
ALTER TABLE [dbo].[Usuario] ADD  DEFAULT ((0)) FOR [EmailConfirmado]
GO
ALTER TABLE [dbo].[Usuario] ADD  DEFAULT ((0)) FOR [DosFactoresHabilitado]
GO
ALTER TABLE [dbo].[Usuario] ADD  DEFAULT (sysutcdatetime()) FOR [CreadoEn]
GO
ALTER TABLE [dbo].[UsuarioNegocio] ADD  DEFAULT ((1)) FOR [Activo]
GO
ALTER TABLE [dbo].[VentaDirecta] ADD  DEFAULT (sysutcdatetime()) FOR [Fecha]
GO
ALTER TABLE [dbo].[VentaDirecta] ADD  DEFAULT ('REGISTRADA') FOR [Estado]
GO
ALTER TABLE [dbo].[ZonaEntrega] ADD  DEFAULT ((1)) FOR [Activa]
GO
ALTER TABLE [dbo].[Auditoria]  WITH CHECK ADD  CONSTRAINT [FK_Auditoria_IdNegocio] FOREIGN KEY([IdNegocio])
REFERENCES [dbo].[Negocio] ([IdNegocio])
GO
ALTER TABLE [dbo].[Auditoria] CHECK CONSTRAINT [FK_Auditoria_IdNegocio]
GO
ALTER TABLE [dbo].[Auditoria]  WITH CHECK ADD  CONSTRAINT [FK_Auditoria_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[Auditoria] CHECK CONSTRAINT [FK_Auditoria_IdUsuario]
GO
ALTER TABLE [dbo].[Banner]  WITH CHECK ADD  CONSTRAINT [FK_Banner_IdCampania] FOREIGN KEY([IdCampania])
REFERENCES [dbo].[Campania] ([IdCampania])
GO
ALTER TABLE [dbo].[Banner] CHECK CONSTRAINT [FK_Banner_IdCampania]
GO
ALTER TABLE [dbo].[Banner]  WITH CHECK ADD  CONSTRAINT [FK_Banner_IdMercado] FOREIGN KEY([IdMercado])
REFERENCES [dbo].[Mercado] ([IdMercado])
GO
ALTER TABLE [dbo].[Banner] CHECK CONSTRAINT [FK_Banner_IdMercado]
GO
ALTER TABLE [dbo].[Campania]  WITH CHECK ADD  CONSTRAINT [FK_Campania_IdMercado] FOREIGN KEY([IdMercado])
REFERENCES [dbo].[Mercado] ([IdMercado])
GO
ALTER TABLE [dbo].[Campania] CHECK CONSTRAINT [FK_Campania_IdMercado]
GO
ALTER TABLE [dbo].[Carrito]  WITH CHECK ADD  CONSTRAINT [FK_Carrito_IdCliente] FOREIGN KEY([IdCliente])
REFERENCES [dbo].[Cliente] ([IdCliente])
GO
ALTER TABLE [dbo].[Carrito] CHECK CONSTRAINT [FK_Carrito_IdCliente]
GO
ALTER TABLE [dbo].[CarritoDetalle]  WITH CHECK ADD  CONSTRAINT [FK_CarritoDetalle_IdCarrito] FOREIGN KEY([IdCarrito])
REFERENCES [dbo].[Carrito] ([IdCarrito])
GO
ALTER TABLE [dbo].[CarritoDetalle] CHECK CONSTRAINT [FK_CarritoDetalle_IdCarrito]
GO
ALTER TABLE [dbo].[CarritoDetalle]  WITH CHECK ADD  CONSTRAINT [FK_CarritoDetalle_IdProducto] FOREIGN KEY([IdProducto])
REFERENCES [dbo].[Producto] ([IdProducto])
GO
ALTER TABLE [dbo].[CarritoDetalle] CHECK CONSTRAINT [FK_CarritoDetalle_IdProducto]
GO
ALTER TABLE [dbo].[Categoria]  WITH CHECK ADD  CONSTRAINT [FK_Categoria_IdPadre] FOREIGN KEY([IdPadre])
REFERENCES [dbo].[Categoria] ([IdCategoria])
GO
ALTER TABLE [dbo].[Categoria] CHECK CONSTRAINT [FK_Categoria_IdPadre]
GO
ALTER TABLE [dbo].[Cliente]  WITH CHECK ADD  CONSTRAINT [FK_Cliente_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[Cliente] CHECK CONSTRAINT [FK_Cliente_IdUsuario]
GO
ALTER TABLE [dbo].[ClienteConsentimiento]  WITH CHECK ADD  CONSTRAINT [FK_ClienteConsentimiento_IdCliente] FOREIGN KEY([IdCliente])
REFERENCES [dbo].[Cliente] ([IdCliente])
GO
ALTER TABLE [dbo].[ClienteConsentimiento] CHECK CONSTRAINT [FK_ClienteConsentimiento_IdCliente]
GO
ALTER TABLE [dbo].[ClienteDireccion]  WITH CHECK ADD  CONSTRAINT [FK_ClienteDireccion_IdCliente] FOREIGN KEY([IdCliente])
REFERENCES [dbo].[Cliente] ([IdCliente])
GO
ALTER TABLE [dbo].[ClienteDireccion] CHECK CONSTRAINT [FK_ClienteDireccion_IdCliente]
GO
ALTER TABLE [dbo].[Comprobante]  WITH CHECK ADD  CONSTRAINT [FK_Comprobante_IdComprobanteReferencia] FOREIGN KEY([IdComprobanteReferencia])
REFERENCES [dbo].[Comprobante] ([IdComprobante])
GO
ALTER TABLE [dbo].[Comprobante] CHECK CONSTRAINT [FK_Comprobante_IdComprobanteReferencia]
GO
ALTER TABLE [dbo].[Comprobante]  WITH CHECK ADD  CONSTRAINT [FK_Comprobante_IdEmisor] FOREIGN KEY([IdEmisor])
REFERENCES [dbo].[EmisorFiscal] ([IdEmisor])
GO
ALTER TABLE [dbo].[Comprobante] CHECK CONSTRAINT [FK_Comprobante_IdEmisor]
GO
ALTER TABLE [dbo].[Comprobante]  WITH CHECK ADD  CONSTRAINT [FK_Comprobante_IdPedidoNegocio] FOREIGN KEY([IdPedidoNegocio])
REFERENCES [dbo].[PedidoNegocio] ([IdPedidoNegocio])
GO
ALTER TABLE [dbo].[Comprobante] CHECK CONSTRAINT [FK_Comprobante_IdPedidoNegocio]
GO
ALTER TABLE [dbo].[Comprobante]  WITH CHECK ADD  CONSTRAINT [FK_Comprobante_IdSerie] FOREIGN KEY([IdSerie])
REFERENCES [dbo].[SerieComprobante] ([IdSerie])
GO
ALTER TABLE [dbo].[Comprobante] CHECK CONSTRAINT [FK_Comprobante_IdSerie]
GO
ALTER TABLE [dbo].[ComprobanteDetalle]  WITH CHECK ADD  CONSTRAINT [FK_ComprobanteDetalle_IdComprobante] FOREIGN KEY([IdComprobante])
REFERENCES [dbo].[Comprobante] ([IdComprobante])
GO
ALTER TABLE [dbo].[ComprobanteDetalle] CHECK CONSTRAINT [FK_ComprobanteDetalle_IdComprobante]
GO
ALTER TABLE [dbo].[ComprobanteDetalle]  WITH CHECK ADD  CONSTRAINT [FK_ComprobanteDetalle_IdProducto] FOREIGN KEY([IdProducto])
REFERENCES [dbo].[Producto] ([IdProducto])
GO
ALTER TABLE [dbo].[ComprobanteDetalle] CHECK CONSTRAINT [FK_ComprobanteDetalle_IdProducto]
GO
ALTER TABLE [dbo].[ComprobanteEnvio]  WITH CHECK ADD  CONSTRAINT [FK_ComprobanteEnvio_IdComprobante] FOREIGN KEY([IdComprobante])
REFERENCES [dbo].[Comprobante] ([IdComprobante])
GO
ALTER TABLE [dbo].[ComprobanteEnvio] CHECK CONSTRAINT [FK_ComprobanteEnvio_IdComprobante]
GO
ALTER TABLE [dbo].[ConciliacionDetalle]  WITH CHECK ADD  CONSTRAINT [FK_ConciliacionDetalle_IdConciliacion] FOREIGN KEY([IdConciliacion])
REFERENCES [dbo].[ConciliacionPago] ([IdConciliacion])
GO
ALTER TABLE [dbo].[ConciliacionDetalle] CHECK CONSTRAINT [FK_ConciliacionDetalle_IdConciliacion]
GO
ALTER TABLE [dbo].[ConciliacionDetalle]  WITH CHECK ADD  CONSTRAINT [FK_ConciliacionDetalle_IdTransaccion] FOREIGN KEY([IdTransaccion])
REFERENCES [dbo].[TransaccionPago] ([IdTransaccion])
GO
ALTER TABLE [dbo].[ConciliacionDetalle] CHECK CONSTRAINT [FK_ConciliacionDetalle_IdTransaccion]
GO
ALTER TABLE [dbo].[ConciliacionPago]  WITH CHECK ADD  CONSTRAINT [FK_ConciliacionPago_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[ConciliacionPago] CHECK CONSTRAINT [FK_ConciliacionPago_IdUsuario]
GO
ALTER TABLE [dbo].[CuentaComerciante]  WITH CHECK ADD  CONSTRAINT [FK_CuentaComerciante_IdNegocio] FOREIGN KEY([IdNegocio])
REFERENCES [dbo].[Negocio] ([IdNegocio])
GO
ALTER TABLE [dbo].[CuentaComerciante] CHECK CONSTRAINT [FK_CuentaComerciante_IdNegocio]
GO
ALTER TABLE [dbo].[EmisorFiscal]  WITH CHECK ADD  CONSTRAINT [FK_EmisorFiscal_IdNegocio] FOREIGN KEY([IdNegocio])
REFERENCES [dbo].[Negocio] ([IdNegocio])
GO
ALTER TABLE [dbo].[EmisorFiscal] CHECK CONSTRAINT [FK_EmisorFiscal_IdNegocio]
GO
ALTER TABLE [dbo].[Entrega]  WITH CHECK ADD  CONSTRAINT [FK_Entrega_IdPedido] FOREIGN KEY([IdPedido])
REFERENCES [dbo].[Pedido] ([IdPedido])
GO
ALTER TABLE [dbo].[Entrega] CHECK CONSTRAINT [FK_Entrega_IdPedido]
GO
ALTER TABLE [dbo].[EntregaEvento]  WITH CHECK ADD  CONSTRAINT [FK_EntregaEvento_IdEntrega] FOREIGN KEY([IdEntrega])
REFERENCES [dbo].[Entrega] ([IdEntrega])
GO
ALTER TABLE [dbo].[EntregaEvento] CHECK CONSTRAINT [FK_EntregaEvento_IdEntrega]
GO
ALTER TABLE [dbo].[EntregaEvento]  WITH CHECK ADD  CONSTRAINT [FK_EntregaEvento_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[EntregaEvento] CHECK CONSTRAINT [FK_EntregaEvento_IdUsuario]
GO
ALTER TABLE [dbo].[Inventario]  WITH CHECK ADD  CONSTRAINT [FK_Inventario_IdProducto] FOREIGN KEY([IdProducto])
REFERENCES [dbo].[Producto] ([IdProducto])
GO
ALTER TABLE [dbo].[Inventario] CHECK CONSTRAINT [FK_Inventario_IdProducto]
GO
ALTER TABLE [dbo].[Liquidacion]  WITH CHECK ADD  CONSTRAINT [FK_Liquidacion_IdAprobador] FOREIGN KEY([IdAprobador])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[Liquidacion] CHECK CONSTRAINT [FK_Liquidacion_IdAprobador]
GO
ALTER TABLE [dbo].[Liquidacion]  WITH CHECK ADD  CONSTRAINT [FK_Liquidacion_IdCuenta] FOREIGN KEY([IdCuenta])
REFERENCES [dbo].[CuentaComerciante] ([IdCuenta])
GO
ALTER TABLE [dbo].[Liquidacion] CHECK CONSTRAINT [FK_Liquidacion_IdCuenta]
GO
ALTER TABLE [dbo].[Liquidacion]  WITH CHECK ADD  CONSTRAINT [FK_Liquidacion_IdNegocio] FOREIGN KEY([IdNegocio])
REFERENCES [dbo].[Negocio] ([IdNegocio])
GO
ALTER TABLE [dbo].[Liquidacion] CHECK CONSTRAINT [FK_Liquidacion_IdNegocio]
GO
ALTER TABLE [dbo].[LiquidacionDetalle]  WITH CHECK ADD  CONSTRAINT [FK_LiquidacionDetalle_IdLiquidacion] FOREIGN KEY([IdLiquidacion])
REFERENCES [dbo].[Liquidacion] ([IdLiquidacion])
GO
ALTER TABLE [dbo].[LiquidacionDetalle] CHECK CONSTRAINT [FK_LiquidacionDetalle_IdLiquidacion]
GO
ALTER TABLE [dbo].[LiquidacionDetalle]  WITH CHECK ADD  CONSTRAINT [FK_LiquidacionDetalle_IdMovimientoCuenta] FOREIGN KEY([IdMovimientoCuenta])
REFERENCES [dbo].[MovimientoCuenta] ([IdMovimientoCuenta])
GO
ALTER TABLE [dbo].[LiquidacionDetalle] CHECK CONSTRAINT [FK_LiquidacionDetalle_IdMovimientoCuenta]
GO
ALTER TABLE [dbo].[MovimientoCuenta]  WITH CHECK ADD  CONSTRAINT [FK_MovimientoCuenta_IdNegocio] FOREIGN KEY([IdNegocio])
REFERENCES [dbo].[Negocio] ([IdNegocio])
GO
ALTER TABLE [dbo].[MovimientoCuenta] CHECK CONSTRAINT [FK_MovimientoCuenta_IdNegocio]
GO
ALTER TABLE [dbo].[MovimientoCuenta]  WITH CHECK ADD  CONSTRAINT [FK_MovimientoCuenta_IdPedidoNegocio] FOREIGN KEY([IdPedidoNegocio])
REFERENCES [dbo].[PedidoNegocio] ([IdPedidoNegocio])
GO
ALTER TABLE [dbo].[MovimientoCuenta] CHECK CONSTRAINT [FK_MovimientoCuenta_IdPedidoNegocio]
GO
ALTER TABLE [dbo].[MovimientoCuenta]  WITH CHECK ADD  CONSTRAINT [FK_MovimientoCuenta_IdReembolso] FOREIGN KEY([IdReembolso])
REFERENCES [dbo].[Reembolso] ([IdReembolso])
GO
ALTER TABLE [dbo].[MovimientoCuenta] CHECK CONSTRAINT [FK_MovimientoCuenta_IdReembolso]
GO
ALTER TABLE [dbo].[MovimientoInventario]  WITH CHECK ADD  CONSTRAINT [FK_MovimientoInventario_IdProducto] FOREIGN KEY([IdProducto])
REFERENCES [dbo].[Producto] ([IdProducto])
GO
ALTER TABLE [dbo].[MovimientoInventario] CHECK CONSTRAINT [FK_MovimientoInventario_IdProducto]
GO
ALTER TABLE [dbo].[MovimientoInventario]  WITH CHECK ADD  CONSTRAINT [FK_MovimientoInventario_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[MovimientoInventario] CHECK CONSTRAINT [FK_MovimientoInventario_IdUsuario]
GO
ALTER TABLE [dbo].[Negocio]  WITH CHECK ADD  CONSTRAINT [FK_Negocio_IdComerciante] FOREIGN KEY([IdComerciante])
REFERENCES [dbo].[Comerciante] ([IdComerciante])
GO
ALTER TABLE [dbo].[Negocio] CHECK CONSTRAINT [FK_Negocio_IdComerciante]
GO
ALTER TABLE [dbo].[Negocio]  WITH CHECK ADD  CONSTRAINT [FK_Negocio_IdMercado] FOREIGN KEY([IdMercado])
REFERENCES [dbo].[Mercado] ([IdMercado])
GO
ALTER TABLE [dbo].[Negocio] CHECK CONSTRAINT [FK_Negocio_IdMercado]
GO
ALTER TABLE [dbo].[NegocioPuesto]  WITH CHECK ADD  CONSTRAINT [FK_NegocioPuesto_IdNegocio] FOREIGN KEY([IdNegocio])
REFERENCES [dbo].[Negocio] ([IdNegocio])
GO
ALTER TABLE [dbo].[NegocioPuesto] CHECK CONSTRAINT [FK_NegocioPuesto_IdNegocio]
GO
ALTER TABLE [dbo].[NegocioPuesto]  WITH CHECK ADD  CONSTRAINT [FK_NegocioPuesto_IdPuesto] FOREIGN KEY([IdPuesto])
REFERENCES [dbo].[Puesto] ([IdPuesto])
GO
ALTER TABLE [dbo].[NegocioPuesto] CHECK CONSTRAINT [FK_NegocioPuesto_IdPuesto]
GO
ALTER TABLE [dbo].[Notificacion]  WITH CHECK ADD  CONSTRAINT [FK_Notificacion_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[Notificacion] CHECK CONSTRAINT [FK_Notificacion_IdUsuario]
GO
ALTER TABLE [dbo].[OrdenPago]  WITH CHECK ADD  CONSTRAINT [FK_OrdenPago_IdPedido] FOREIGN KEY([IdPedido])
REFERENCES [dbo].[Pedido] ([IdPedido])
GO
ALTER TABLE [dbo].[OrdenPago] CHECK CONSTRAINT [FK_OrdenPago_IdPedido]
GO
ALTER TABLE [dbo].[Pedido]  WITH CHECK ADD  CONSTRAINT [FK_Pedido_IdCliente] FOREIGN KEY([IdCliente])
REFERENCES [dbo].[Cliente] ([IdCliente])
GO
ALTER TABLE [dbo].[Pedido] CHECK CONSTRAINT [FK_Pedido_IdCliente]
GO
ALTER TABLE [dbo].[Pedido]  WITH CHECK ADD  CONSTRAINT [FK_Pedido_IdDireccion] FOREIGN KEY([IdDireccion])
REFERENCES [dbo].[ClienteDireccion] ([IdDireccion])
GO
ALTER TABLE [dbo].[Pedido] CHECK CONSTRAINT [FK_Pedido_IdDireccion]
GO
ALTER TABLE [dbo].[Pedido]  WITH CHECK ADD  CONSTRAINT [FK_Pedido_IdMercado] FOREIGN KEY([IdMercado])
REFERENCES [dbo].[Mercado] ([IdMercado])
GO
ALTER TABLE [dbo].[Pedido] CHECK CONSTRAINT [FK_Pedido_IdMercado]
GO
ALTER TABLE [dbo].[Pedido]  WITH CHECK ADD  CONSTRAINT [FK_Pedido_IdZona] FOREIGN KEY([IdZona])
REFERENCES [dbo].[ZonaEntrega] ([IdZona])
GO
ALTER TABLE [dbo].[Pedido] CHECK CONSTRAINT [FK_Pedido_IdZona]
GO
ALTER TABLE [dbo].[PedidoDetalle]  WITH CHECK ADD  CONSTRAINT [FK_PedidoDetalle_IdPedidoNegocio] FOREIGN KEY([IdPedidoNegocio])
REFERENCES [dbo].[PedidoNegocio] ([IdPedidoNegocio])
GO
ALTER TABLE [dbo].[PedidoDetalle] CHECK CONSTRAINT [FK_PedidoDetalle_IdPedidoNegocio]
GO
ALTER TABLE [dbo].[PedidoDetalle]  WITH CHECK ADD  CONSTRAINT [FK_PedidoDetalle_IdProducto] FOREIGN KEY([IdProducto])
REFERENCES [dbo].[Producto] ([IdProducto])
GO
ALTER TABLE [dbo].[PedidoDetalle] CHECK CONSTRAINT [FK_PedidoDetalle_IdProducto]
GO
ALTER TABLE [dbo].[PedidoEstadoHistorial]  WITH CHECK ADD  CONSTRAINT [FK_PedidoEstadoHistorial_IdPedidoNegocio] FOREIGN KEY([IdPedidoNegocio])
REFERENCES [dbo].[PedidoNegocio] ([IdPedidoNegocio])
GO
ALTER TABLE [dbo].[PedidoEstadoHistorial] CHECK CONSTRAINT [FK_PedidoEstadoHistorial_IdPedidoNegocio]
GO
ALTER TABLE [dbo].[PedidoEstadoHistorial]  WITH CHECK ADD  CONSTRAINT [FK_PedidoEstadoHistorial_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[PedidoEstadoHistorial] CHECK CONSTRAINT [FK_PedidoEstadoHistorial_IdUsuario]
GO
ALTER TABLE [dbo].[PedidoNegocio]  WITH CHECK ADD  CONSTRAINT [FK_PedidoNegocio_IdNegocio] FOREIGN KEY([IdNegocio])
REFERENCES [dbo].[Negocio] ([IdNegocio])
GO
ALTER TABLE [dbo].[PedidoNegocio] CHECK CONSTRAINT [FK_PedidoNegocio_IdNegocio]
GO
ALTER TABLE [dbo].[PedidoNegocio]  WITH CHECK ADD  CONSTRAINT [FK_PedidoNegocio_IdPedido] FOREIGN KEY([IdPedido])
REFERENCES [dbo].[Pedido] ([IdPedido])
GO
ALTER TABLE [dbo].[PedidoNegocio] CHECK CONSTRAINT [FK_PedidoNegocio_IdPedido]
GO
ALTER TABLE [dbo].[Producto]  WITH CHECK ADD  CONSTRAINT [FK_Producto_IdCategoria] FOREIGN KEY([IdCategoria])
REFERENCES [dbo].[Categoria] ([IdCategoria])
GO
ALTER TABLE [dbo].[Producto] CHECK CONSTRAINT [FK_Producto_IdCategoria]
GO
ALTER TABLE [dbo].[Producto]  WITH CHECK ADD  CONSTRAINT [FK_Producto_IdNegocio] FOREIGN KEY([IdNegocio])
REFERENCES [dbo].[Negocio] ([IdNegocio])
GO
ALTER TABLE [dbo].[Producto] CHECK CONSTRAINT [FK_Producto_IdNegocio]
GO
ALTER TABLE [dbo].[Producto]  WITH CHECK ADD  CONSTRAINT [FK_Producto_IdUnidad] FOREIGN KEY([IdUnidad])
REFERENCES [dbo].[UnidadMedida] ([IdUnidad])
GO
ALTER TABLE [dbo].[Producto] CHECK CONSTRAINT [FK_Producto_IdUnidad]
GO
ALTER TABLE [dbo].[ProductoImagen]  WITH CHECK ADD  CONSTRAINT [FK_ProductoImagen_IdProducto] FOREIGN KEY([IdProducto])
REFERENCES [dbo].[Producto] ([IdProducto])
GO
ALTER TABLE [dbo].[ProductoImagen] CHECK CONSTRAINT [FK_ProductoImagen_IdProducto]
GO
ALTER TABLE [dbo].[ProductoPrecio]  WITH CHECK ADD  CONSTRAINT [FK_ProductoPrecio_IdProducto] FOREIGN KEY([IdProducto])
REFERENCES [dbo].[Producto] ([IdProducto])
GO
ALTER TABLE [dbo].[ProductoPrecio] CHECK CONSTRAINT [FK_ProductoPrecio_IdProducto]
GO
ALTER TABLE [dbo].[Promocion]  WITH CHECK ADD  CONSTRAINT [FK_Promocion_IdCampania] FOREIGN KEY([IdCampania])
REFERENCES [dbo].[Campania] ([IdCampania])
GO
ALTER TABLE [dbo].[Promocion] CHECK CONSTRAINT [FK_Promocion_IdCampania]
GO
ALTER TABLE [dbo].[Promocion]  WITH CHECK ADD  CONSTRAINT [FK_Promocion_IdNegocio] FOREIGN KEY([IdNegocio])
REFERENCES [dbo].[Negocio] ([IdNegocio])
GO
ALTER TABLE [dbo].[Promocion] CHECK CONSTRAINT [FK_Promocion_IdNegocio]
GO
ALTER TABLE [dbo].[Promocion]  WITH CHECK ADD  CONSTRAINT [FK_Promocion_IdProducto] FOREIGN KEY([IdProducto])
REFERENCES [dbo].[Producto] ([IdProducto])
GO
ALTER TABLE [dbo].[Promocion] CHECK CONSTRAINT [FK_Promocion_IdProducto]
GO
ALTER TABLE [dbo].[Puesto]  WITH CHECK ADD  CONSTRAINT [FK_Puesto_IdSector] FOREIGN KEY([IdSector])
REFERENCES [dbo].[Sector] ([IdSector])
GO
ALTER TABLE [dbo].[Puesto] CHECK CONSTRAINT [FK_Puesto_IdSector]
GO
ALTER TABLE [dbo].[Reembolso]  WITH CHECK ADD  CONSTRAINT [FK_Reembolso_IdPedidoNegocio] FOREIGN KEY([IdPedidoNegocio])
REFERENCES [dbo].[PedidoNegocio] ([IdPedidoNegocio])
GO
ALTER TABLE [dbo].[Reembolso] CHECK CONSTRAINT [FK_Reembolso_IdPedidoNegocio]
GO
ALTER TABLE [dbo].[Reembolso]  WITH CHECK ADD  CONSTRAINT [FK_Reembolso_IdTransaccionOriginal] FOREIGN KEY([IdTransaccionOriginal])
REFERENCES [dbo].[TransaccionPago] ([IdTransaccion])
GO
ALTER TABLE [dbo].[Reembolso] CHECK CONSTRAINT [FK_Reembolso_IdTransaccionOriginal]
GO
ALTER TABLE [dbo].[RolPermiso]  WITH CHECK ADD  CONSTRAINT [FK_RolPermiso_IdPermiso] FOREIGN KEY([IdPermiso])
REFERENCES [dbo].[Permiso] ([IdPermiso])
GO
ALTER TABLE [dbo].[RolPermiso] CHECK CONSTRAINT [FK_RolPermiso_IdPermiso]
GO
ALTER TABLE [dbo].[RolPermiso]  WITH CHECK ADD  CONSTRAINT [FK_RolPermiso_IdRol] FOREIGN KEY([IdRol])
REFERENCES [dbo].[Rol] ([IdRol])
GO
ALTER TABLE [dbo].[RolPermiso] CHECK CONSTRAINT [FK_RolPermiso_IdRol]
GO
ALTER TABLE [dbo].[Sector]  WITH CHECK ADD  CONSTRAINT [FK_Sector_IdMercado] FOREIGN KEY([IdMercado])
REFERENCES [dbo].[Mercado] ([IdMercado])
GO
ALTER TABLE [dbo].[Sector] CHECK CONSTRAINT [FK_Sector_IdMercado]
GO
ALTER TABLE [dbo].[SerieComprobante]  WITH CHECK ADD  CONSTRAINT [FK_SerieComprobante_IdEmisor] FOREIGN KEY([IdEmisor])
REFERENCES [dbo].[EmisorFiscal] ([IdEmisor])
GO
ALTER TABLE [dbo].[SerieComprobante] CHECK CONSTRAINT [FK_SerieComprobante_IdEmisor]
GO
ALTER TABLE [dbo].[TarifaEntrega]  WITH CHECK ADD  CONSTRAINT [FK_TarifaEntrega_IdZona] FOREIGN KEY([IdZona])
REFERENCES [dbo].[ZonaEntrega] ([IdZona])
GO
ALTER TABLE [dbo].[TarifaEntrega] CHECK CONSTRAINT [FK_TarifaEntrega_IdZona]
GO
ALTER TABLE [dbo].[TransaccionPago]  WITH CHECK ADD  CONSTRAINT [FK_TransaccionPago_IdOrdenPago] FOREIGN KEY([IdOrdenPago])
REFERENCES [dbo].[OrdenPago] ([IdOrdenPago])
GO
ALTER TABLE [dbo].[TransaccionPago] CHECK CONSTRAINT [FK_TransaccionPago_IdOrdenPago]
GO
ALTER TABLE [dbo].[Transferencia]  WITH CHECK ADD  CONSTRAINT [FK_Transferencia_IdLiquidacion] FOREIGN KEY([IdLiquidacion])
REFERENCES [dbo].[Liquidacion] ([IdLiquidacion])
GO
ALTER TABLE [dbo].[Transferencia] CHECK CONSTRAINT [FK_Transferencia_IdLiquidacion]
GO
ALTER TABLE [dbo].[Transferencia]  WITH CHECK ADD  CONSTRAINT [FK_Transferencia_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[Transferencia] CHECK CONSTRAINT [FK_Transferencia_IdUsuario]
GO
ALTER TABLE [dbo].[UsuarioMercado]  WITH CHECK ADD  CONSTRAINT [FK_UsuarioMercado_IdMercado] FOREIGN KEY([IdMercado])
REFERENCES [dbo].[Mercado] ([IdMercado])
GO
ALTER TABLE [dbo].[UsuarioMercado] CHECK CONSTRAINT [FK_UsuarioMercado_IdMercado]
GO
ALTER TABLE [dbo].[UsuarioMercado]  WITH CHECK ADD  CONSTRAINT [FK_UsuarioMercado_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[UsuarioMercado] CHECK CONSTRAINT [FK_UsuarioMercado_IdUsuario]
GO
ALTER TABLE [dbo].[UsuarioNegocio]  WITH CHECK ADD  CONSTRAINT [FK_UsuarioNegocio_IdNegocio] FOREIGN KEY([IdNegocio])
REFERENCES [dbo].[Negocio] ([IdNegocio])
GO
ALTER TABLE [dbo].[UsuarioNegocio] CHECK CONSTRAINT [FK_UsuarioNegocio_IdNegocio]
GO
ALTER TABLE [dbo].[UsuarioNegocio]  WITH CHECK ADD  CONSTRAINT [FK_UsuarioNegocio_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[UsuarioNegocio] CHECK CONSTRAINT [FK_UsuarioNegocio_IdUsuario]
GO
ALTER TABLE [dbo].[UsuarioRol]  WITH CHECK ADD  CONSTRAINT [FK_UsuarioRol_IdRol] FOREIGN KEY([IdRol])
REFERENCES [dbo].[Rol] ([IdRol])
GO
ALTER TABLE [dbo].[UsuarioRol] CHECK CONSTRAINT [FK_UsuarioRol_IdRol]
GO
ALTER TABLE [dbo].[UsuarioRol]  WITH CHECK ADD  CONSTRAINT [FK_UsuarioRol_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[UsuarioRol] CHECK CONSTRAINT [FK_UsuarioRol_IdUsuario]
GO
ALTER TABLE [dbo].[VentaDirecta]  WITH CHECK ADD  CONSTRAINT [FK_VentaDirecta_IdNegocio] FOREIGN KEY([IdNegocio])
REFERENCES [dbo].[Negocio] ([IdNegocio])
GO
ALTER TABLE [dbo].[VentaDirecta] CHECK CONSTRAINT [FK_VentaDirecta_IdNegocio]
GO
ALTER TABLE [dbo].[VentaDirecta]  WITH CHECK ADD  CONSTRAINT [FK_VentaDirecta_IdUsuario] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuario] ([IdUsuario])
GO
ALTER TABLE [dbo].[VentaDirecta] CHECK CONSTRAINT [FK_VentaDirecta_IdUsuario]
GO
ALTER TABLE [dbo].[VentaDirectaDetalle]  WITH CHECK ADD  CONSTRAINT [FK_VentaDirectaDetalle_IdProducto] FOREIGN KEY([IdProducto])
REFERENCES [dbo].[Producto] ([IdProducto])
GO
ALTER TABLE [dbo].[VentaDirectaDetalle] CHECK CONSTRAINT [FK_VentaDirectaDetalle_IdProducto]
GO
ALTER TABLE [dbo].[VentaDirectaDetalle]  WITH CHECK ADD  CONSTRAINT [FK_VentaDirectaDetalle_IdVentaDirecta] FOREIGN KEY([IdVentaDirecta])
REFERENCES [dbo].[VentaDirecta] ([IdVentaDirecta])
GO
ALTER TABLE [dbo].[VentaDirectaDetalle] CHECK CONSTRAINT [FK_VentaDirectaDetalle_IdVentaDirecta]
GO
ALTER TABLE [dbo].[ZonaEntrega]  WITH CHECK ADD  CONSTRAINT [FK_ZonaEntrega_IdMercado] FOREIGN KEY([IdMercado])
REFERENCES [dbo].[Mercado] ([IdMercado])
GO
ALTER TABLE [dbo].[ZonaEntrega] CHECK CONSTRAINT [FK_ZonaEntrega_IdMercado]
GO
ALTER TABLE [dbo].[Banner]  WITH CHECK ADD  CONSTRAINT [CK_Banner_Fechas] CHECK  (([Fin]>[Inicio]))
GO
ALTER TABLE [dbo].[Banner] CHECK CONSTRAINT [CK_Banner_Fechas]
GO
ALTER TABLE [dbo].[Campania]  WITH CHECK ADD  CONSTRAINT [CK_Campania_Fechas] CHECK  (([Fin]>[Inicio]))
GO
ALTER TABLE [dbo].[Campania] CHECK CONSTRAINT [CK_Campania_Fechas]
GO
ALTER TABLE [dbo].[Carrito]  WITH CHECK ADD  CONSTRAINT [CK_Carrito_Estado] CHECK  (([Estado]='ABANDONADO' OR [Estado]='CONVERTIDO' OR [Estado]='ABIERTO'))
GO
ALTER TABLE [dbo].[Carrito] CHECK CONSTRAINT [CK_Carrito_Estado]
GO
ALTER TABLE [dbo].[CarritoDetalle]  WITH CHECK ADD  CONSTRAINT [CK_CarritoDetalle_Cantidad] CHECK  (([Cantidad]>(0)))
GO
ALTER TABLE [dbo].[CarritoDetalle] CHECK CONSTRAINT [CK_CarritoDetalle_Cantidad]
GO
ALTER TABLE [dbo].[Comprobante]  WITH CHECK ADD  CONSTRAINT [CK_Comprobante_Estado] CHECK  (([Estado]='ANULADO' OR [Estado]='RECHAZADO' OR [Estado]='OBSERVADO' OR [Estado]='ACEPTADO' OR [Estado]='ENVIADO' OR [Estado]='GENERADO' OR [Estado]='PENDIENTE'))
GO
ALTER TABLE [dbo].[Comprobante] CHECK CONSTRAINT [CK_Comprobante_Estado]
GO
ALTER TABLE [dbo].[Comprobante]  WITH CHECK ADD  CONSTRAINT [CK_Comprobante_Numero] CHECK  (([Numero]>(0)))
GO
ALTER TABLE [dbo].[Comprobante] CHECK CONSTRAINT [CK_Comprobante_Numero]
GO
ALTER TABLE [dbo].[Comprobante]  WITH CHECK ADD  CONSTRAINT [CK_Comprobante_Total] CHECK  (([Total]>=(0)))
GO
ALTER TABLE [dbo].[Comprobante] CHECK CONSTRAINT [CK_Comprobante_Total]
GO
ALTER TABLE [dbo].[ComprobanteDetalle]  WITH CHECK ADD  CONSTRAINT [CK_ComprobanteDetalle] CHECK  (([Cantidad]>(0) AND [Total]>=(0)))
GO
ALTER TABLE [dbo].[ComprobanteDetalle] CHECK CONSTRAINT [CK_ComprobanteDetalle]
GO
ALTER TABLE [dbo].[EmisorFiscal]  WITH CHECK ADD  CONSTRAINT [CK_Emisor_Modalidad] CHECK  (([Modalidad]='OTRO_AUTORIZADO' OR [Modalidad]='PSE' OR [Modalidad]='OSE' OR [Modalidad]='PROPIO'))
GO
ALTER TABLE [dbo].[EmisorFiscal] CHECK CONSTRAINT [CK_Emisor_Modalidad]
GO
ALTER TABLE [dbo].[Entrega]  WITH CHECK ADD  CONSTRAINT [CK_Entrega_Estado] CHECK  (([Estado]='CANCELADO' OR [Estado]='INCIDENCIA' OR [Estado]='ENTREGADO' OR [Estado]='DESPACHO' OR [Estado]='LISTO' OR [Estado]='PENDIENTE'))
GO
ALTER TABLE [dbo].[Entrega] CHECK CONSTRAINT [CK_Entrega_Estado]
GO
ALTER TABLE [dbo].[Inventario]  WITH CHECK ADD  CONSTRAINT [CK_Inventario] CHECK  (([Existencia]>=(0) AND [Reservado]>=(0) AND [Reservado]<=[Existencia]))
GO
ALTER TABLE [dbo].[Inventario] CHECK CONSTRAINT [CK_Inventario]
GO
ALTER TABLE [dbo].[Liquidacion]  WITH CHECK ADD  CONSTRAINT [CK_Liquidacion_Estado] CHECK  (([Estado]='ANULADA' OR [Estado]='AJUSTADA' OR [Estado]='TRANSFERIDA' OR [Estado]='APROBADA' OR [Estado]='CONCILIADA' OR [Estado]='PENDIENTE'))
GO
ALTER TABLE [dbo].[Liquidacion] CHECK CONSTRAINT [CK_Liquidacion_Estado]
GO
ALTER TABLE [dbo].[Liquidacion]  WITH CHECK ADD  CONSTRAINT [CK_Liquidacion_Fechas] CHECK  (([PeriodoHasta]>[PeriodoDesde]))
GO
ALTER TABLE [dbo].[Liquidacion] CHECK CONSTRAINT [CK_Liquidacion_Fechas]
GO
ALTER TABLE [dbo].[MovimientoCuenta]  WITH CHECK ADD  CONSTRAINT [CK_MovCuenta_Importe] CHECK  (([Importe]<>(0)))
GO
ALTER TABLE [dbo].[MovimientoCuenta] CHECK CONSTRAINT [CK_MovCuenta_Importe]
GO
ALTER TABLE [dbo].[MovimientoInventario]  WITH CHECK ADD  CONSTRAINT [CK_MovInv_Cantidad] CHECK  (([Cantidad]>(0)))
GO
ALTER TABLE [dbo].[MovimientoInventario] CHECK CONSTRAINT [CK_MovInv_Cantidad]
GO
ALTER TABLE [dbo].[MovimientoInventario]  WITH CHECK ADD  CONSTRAINT [CK_MovInv_Tipo] CHECK  (([Tipo]='LIBERACION' OR [Tipo]='RESERVA' OR [Tipo]='AJUSTE_NEG' OR [Tipo]='AJUSTE_POS' OR [Tipo]='SALIDA' OR [Tipo]='ENTRADA'))
GO
ALTER TABLE [dbo].[MovimientoInventario] CHECK CONSTRAINT [CK_MovInv_Tipo]
GO
ALTER TABLE [dbo].[Negocio]  WITH CHECK ADD  CONSTRAINT [CK_Negocio_Estado] CHECK  (([Estado]='BAJA' OR [Estado]='SUSPENDIDO' OR [Estado]='ACTIVO' OR [Estado]='PENDIENTE'))
GO
ALTER TABLE [dbo].[Negocio] CHECK CONSTRAINT [CK_Negocio_Estado]
GO
ALTER TABLE [dbo].[NegocioPuesto]  WITH CHECK ADD  CONSTRAINT [CK_NegocioPuesto_Fechas] CHECK  (([Hasta] IS NULL OR [Hasta]>=[Desde]))
GO
ALTER TABLE [dbo].[NegocioPuesto] CHECK CONSTRAINT [CK_NegocioPuesto_Fechas]
GO
ALTER TABLE [dbo].[OrdenPago]  WITH CHECK ADD  CONSTRAINT [CK_OrdenPago_Estado] CHECK  (([Estado]='REEMBOLSADO' OR [Estado]='PARCIAL' OR [Estado]='ANULADO' OR [Estado]='FALLIDO' OR [Estado]='PAGADO' OR [Estado]='AUTORIZADO' OR [Estado]='PENDIENTE'))
GO
ALTER TABLE [dbo].[OrdenPago] CHECK CONSTRAINT [CK_OrdenPago_Estado]
GO
ALTER TABLE [dbo].[OrdenPago]  WITH CHECK ADD  CONSTRAINT [CK_OrdenPago_Monto] CHECK  (([Monto]>=(0)))
GO
ALTER TABLE [dbo].[OrdenPago] CHECK CONSTRAINT [CK_OrdenPago_Monto]
GO
ALTER TABLE [dbo].[Pedido]  WITH CHECK ADD  CONSTRAINT [CK_Pedido_Domicilio] CHECK  (([ModalidadEntrega]='RECOJO' OR [IdDireccion] IS NOT NULL))
GO
ALTER TABLE [dbo].[Pedido] CHECK CONSTRAINT [CK_Pedido_Domicilio]
GO
ALTER TABLE [dbo].[Pedido]  WITH CHECK ADD  CONSTRAINT [CK_Pedido_Entrega] CHECK  (([ModalidadEntrega]='DOMICILIO' OR [ModalidadEntrega]='RECOJO'))
GO
ALTER TABLE [dbo].[Pedido] CHECK CONSTRAINT [CK_Pedido_Entrega]
GO
ALTER TABLE [dbo].[Pedido]  WITH CHECK ADD  CONSTRAINT [CK_Pedido_Estado] CHECK  (([Estado]='CANCELADO' OR [Estado]='COMPLETADO' OR [Estado]='PREPARACION' OR [Estado]='CONFIRMADO' OR [Estado]='REGISTRADO'))
GO
ALTER TABLE [dbo].[Pedido] CHECK CONSTRAINT [CK_Pedido_Estado]
GO
ALTER TABLE [dbo].[Pedido]  WITH CHECK ADD  CONSTRAINT [CK_Pedido_Montos] CHECK  (([Subtotal]>=(0) AND [Descuento]>=(0) AND [CostoEntrega]>=(0) AND [Total]>=(0) AND [Total]=(([Subtotal]-[Descuento])+[CostoEntrega])))
GO
ALTER TABLE [dbo].[Pedido] CHECK CONSTRAINT [CK_Pedido_Montos]
GO
ALTER TABLE [dbo].[Pedido]  WITH CHECK ADD  CONSTRAINT [CK_Pedido_Pago] CHECK  (([EstadoPago]='PARCIAL' OR [EstadoPago]='REEMBOLSADO' OR [EstadoPago]='FALLIDO' OR [EstadoPago]='CONFIRMADO' OR [EstadoPago]='AUTORIZADO' OR [EstadoPago]='PENDIENTE'))
GO
ALTER TABLE [dbo].[Pedido] CHECK CONSTRAINT [CK_Pedido_Pago]
GO
ALTER TABLE [dbo].[PedidoDetalle]  WITH CHECK ADD  CONSTRAINT [CK_PedidoDetalle_Montos] CHECK  (([Cantidad]>(0) AND [PrecioUnitario]>=(0) AND [Descuento]>=(0) AND [Total]>=(0)))
GO
ALTER TABLE [dbo].[PedidoDetalle] CHECK CONSTRAINT [CK_PedidoDetalle_Montos]
GO
ALTER TABLE [dbo].[PedidoNegocio]  WITH CHECK ADD  CONSTRAINT [CK_PedidoNegocio_Estado] CHECK  (([Estado]='RECHAZADO' OR [Estado]='CANCELADO' OR [Estado]='ENTREGADO' OR [Estado]='LISTO' OR [Estado]='PREPARACION' OR [Estado]='CONFIRMADO' OR [Estado]='PENDIENTE'))
GO
ALTER TABLE [dbo].[PedidoNegocio] CHECK CONSTRAINT [CK_PedidoNegocio_Estado]
GO
ALTER TABLE [dbo].[PedidoNegocio]  WITH CHECK ADD  CONSTRAINT [CK_PedidoNegocio_Montos] CHECK  (([Subtotal]>=(0) AND [Descuento]>=(0) AND [Total]>=(0) AND [Total]=([Subtotal]-[Descuento])))
GO
ALTER TABLE [dbo].[PedidoNegocio] CHECK CONSTRAINT [CK_PedidoNegocio_Montos]
GO
ALTER TABLE [dbo].[Producto]  WITH CHECK ADD  CONSTRAINT [CK_Producto_Estado] CHECK  (([Estado]='SUSPENDIDO' OR [Estado]='AGOTADO' OR [Estado]='PUBLICADO' OR [Estado]='BORRADOR'))
GO
ALTER TABLE [dbo].[Producto] CHECK CONSTRAINT [CK_Producto_Estado]
GO
ALTER TABLE [dbo].[Producto]  WITH CHECK ADD  CONSTRAINT [CK_Producto_Precio] CHECK  (([Precio]>=(0)))
GO
ALTER TABLE [dbo].[Producto] CHECK CONSTRAINT [CK_Producto_Precio]
GO
ALTER TABLE [dbo].[ProductoPrecio]  WITH CHECK ADD  CONSTRAINT [CK_ProductoPrecio] CHECK  (([Precio]>=(0) AND ([VigenteHasta] IS NULL OR [VigenteHasta]>[VigenteDesde])))
GO
ALTER TABLE [dbo].[ProductoPrecio] CHECK CONSTRAINT [CK_ProductoPrecio]
GO
ALTER TABLE [dbo].[Promocion]  WITH CHECK ADD  CONSTRAINT [CK_Promocion] CHECK  (([PrecioPromocional]>=(0) AND [Fin]>[Inicio] AND ([Financiador]='ADMINISTRACION' OR [Financiador]='COMERCIANTE')))
GO
ALTER TABLE [dbo].[Promocion] CHECK CONSTRAINT [CK_Promocion]
GO
ALTER TABLE [dbo].[Puesto]  WITH CHECK ADD  CONSTRAINT [CK_Puesto_Estado] CHECK  (([Estado]='INACTIVO' OR [Estado]='SUSPENDIDO' OR [Estado]='OCUPADO' OR [Estado]='DISPONIBLE'))
GO
ALTER TABLE [dbo].[Puesto] CHECK CONSTRAINT [CK_Puesto_Estado]
GO
ALTER TABLE [dbo].[Reembolso]  WITH CHECK ADD  CONSTRAINT [CK_Reembolso_Estado] CHECK  (([Estado]='RECHAZADO' OR [Estado]='PROCESADO' OR [Estado]='APROBADO' OR [Estado]='SOLICITADO'))
GO
ALTER TABLE [dbo].[Reembolso] CHECK CONSTRAINT [CK_Reembolso_Estado]
GO
ALTER TABLE [dbo].[Reembolso]  WITH CHECK ADD  CONSTRAINT [CK_Reembolso_Monto] CHECK  (([Monto]>(0)))
GO
ALTER TABLE [dbo].[Reembolso] CHECK CONSTRAINT [CK_Reembolso_Monto]
GO
ALTER TABLE [dbo].[SerieComprobante]  WITH CHECK ADD  CONSTRAINT [CK_Serie_Numero] CHECK  (([SiguienteNumero]>(0)))
GO
ALTER TABLE [dbo].[SerieComprobante] CHECK CONSTRAINT [CK_Serie_Numero]
GO
ALTER TABLE [dbo].[TarifaEntrega]  WITH CHECK ADD  CONSTRAINT [CK_Tarifa] CHECK  (([Monto]>=(0) AND ([Hasta] IS NULL OR [Hasta]>[Desde])))
GO
ALTER TABLE [dbo].[TarifaEntrega] CHECK CONSTRAINT [CK_Tarifa]
GO
ALTER TABLE [dbo].[TransaccionPago]  WITH CHECK ADD  CONSTRAINT [CK_Transaccion_Monto] CHECK  (([Monto]>=(0)))
GO
ALTER TABLE [dbo].[TransaccionPago] CHECK CONSTRAINT [CK_Transaccion_Monto]
GO
ALTER TABLE [dbo].[TransaccionPago]  WITH CHECK ADD  CONSTRAINT [CK_Transaccion_Tipo] CHECK  (([Tipo]='CONTRACARGO' OR [Tipo]='REEMBOLSO' OR [Tipo]='ANULACION' OR [Tipo]='CAPTURA' OR [Tipo]='COBRO'))
GO
ALTER TABLE [dbo].[TransaccionPago] CHECK CONSTRAINT [CK_Transaccion_Tipo]
GO
ALTER TABLE [dbo].[Transferencia]  WITH CHECK ADD  CONSTRAINT [CK_Transferencia_Estado] CHECK  (([Estado]='RECHAZADA' OR [Estado]='CONFIRMADA' OR [Estado]='ENVIADA' OR [Estado]='PENDIENTE'))
GO
ALTER TABLE [dbo].[Transferencia] CHECK CONSTRAINT [CK_Transferencia_Estado]
GO
ALTER TABLE [dbo].[Transferencia]  WITH CHECK ADD  CONSTRAINT [CK_Transferencia_Monto] CHECK  (([Monto]>=(0)))
GO
ALTER TABLE [dbo].[Transferencia] CHECK CONSTRAINT [CK_Transferencia_Monto]
GO
ALTER TABLE [dbo].[VentaDirecta]  WITH CHECK ADD  CONSTRAINT [CK_VentaDirecta_Total] CHECK  (([Total]>=(0)))
GO
ALTER TABLE [dbo].[VentaDirecta] CHECK CONSTRAINT [CK_VentaDirecta_Total]
GO
ALTER TABLE [dbo].[VentaDirectaDetalle]  WITH CHECK ADD  CONSTRAINT [CK_VentaDirectaDetalle] CHECK  (([Cantidad]>(0) AND [Precio]>=(0) AND [Total]>=(0)))
GO
ALTER TABLE [dbo].[VentaDirectaDetalle] CHECK CONSTRAINT [CK_VentaDirectaDetalle]
GO
