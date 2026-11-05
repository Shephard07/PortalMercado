namespace PortalMercado.Domain.Entities;

public class Usuario
{
    public long IdUsuario { get; set; }
    public string Correo { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public bool Activo { get; set; }
    public bool EmailConfirmado { get; set; }
    public bool DosFactoresHabilitado { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? UltimoAcceso { get; set; }
}