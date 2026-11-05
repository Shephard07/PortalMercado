namespace PortalMercado.Domain.Entities;

public class Puesto
{
    public long IdPuesto { get; set; }
    public long IdSector { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string? Ubicacion { get; set; }
    public string Estado { get; set; } = string.Empty;
}