namespace PortalMercado.Domain.Entities;

public class Sector
{
    public long IdSector { get; set; }
    public long IdMercado { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
}