using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Web.Models;

public sealed record TiendasDirectorioModelo(TiendaFiltro Filtro, Page<TiendaResumen> Pagina, TiendaOpciones Opciones);
public sealed record TiendasErrorModelo(int Estado, string Mensaje);
