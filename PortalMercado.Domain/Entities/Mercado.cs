namespace PortalMercado.Domain.Entities;

public class Mercado
{
    public long IdMercado { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? RUC { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? LogoUrl { get; set; }
    public bool Activo { get; set; }
    public DateTime CreadoEn { get; set; }
}