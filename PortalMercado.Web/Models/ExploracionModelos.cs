using PortalMercado.Application.Exploracion;
namespace PortalMercado.Web.Models;
public sealed record PortadaPublicaModelo(ExploracionPortada? Datos, bool NoDisponible = false);
