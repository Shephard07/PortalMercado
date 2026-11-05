namespace PortalMercado.Domain.Entities;

public class Negocio
{
    public long IdNegocio { get; set; }
    public long IdComerciante { get; set; }
    public long IdMercado { get; set; }
    public string NombreComercial { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? RUC { get; set; }
    public string? RazonSocial { get; set; }
    public string? DireccionFiscal { get; set; }
    public string? Descripcion { get; set; }
    public string? LogoUrl { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Horario { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; }
}