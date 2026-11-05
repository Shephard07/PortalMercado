namespace PortalMercado.Domain.Entities;

public sealed class Rol
{
    public int IdRol { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
}
public sealed class Permiso
{
    public int IdPermiso { get; set; }
    public string Codigo { get; set; } = "";
    public string? Descripcion { get; set; }
}
public sealed class UsuarioRol { public long IdUsuario { get; set; } public int IdRol { get; set; } }
public sealed class RolPermiso { public int IdRol { get; set; } public int IdPermiso { get; set; } }
public sealed class UsuarioNegocio
{
    public long IdUsuario { get; set; }
    public long IdNegocio { get; set; }
    public bool Activo { get; set; }
}
public sealed class UsuarioMercado { public long IdUsuario { get; set; } public long IdMercado { get; set; } }
public sealed class UsuarioSeguridad
{
    public long IdUsuario { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public Guid Sello { get; set; }
}
public sealed class UsuarioSesion
{
    public Guid IdSesion { get; set; }
    public bool MfaVerificado { get; set; }
    public long? IdNegocioActivo { get; set; }
    public long IdUsuario { get; set; }
    public string TokenHash { get; set; } = "";
    public string Host { get; set; } = "";
    public Guid Sello { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime ExpiraEn { get; set; }
    public DateTime? RevocadoEn { get; set; }
}
public sealed class AuditoriaAcceso
{
    public long IdAuditoria { get; set; }
    public long? IdActor { get; set; }
    public long? IdObjetivo { get; set; }
    public string Evento { get; set; } = "";
    public DateTime FechaUtc { get; set; }
    public string CorrelationId { get; set; } = "";
}
public sealed class SeguridadVersion
{
    public string Codigo { get; set; } = "";
    public DateTime AplicadoEn { get; set; }
}

public sealed class UsuarioMfa
{
    public long IdUsuario { get; set; }
    public string? SecretProtegido { get; set; }
    public bool PendienteBootstrap { get; set; }
    public long? UltimoPasoTotp { get; set; }
    public DateTime? ActivadoEn { get; set; }
}
public sealed class UsuarioMfaReto
{
    public Guid IdReto { get; set; }
    public long IdUsuario { get; set; }
    public string TokenHash { get; set; } = "";
    public string Host { get; set; } = "";
    public bool Enrolamiento { get; set; }
    public Guid Sello { get; set; }
    public DateTime ExpiraEn { get; set; }
    public DateTime? ConsumidoEn { get; set; }
    public int Intentos { get; set; }
}
public sealed class UsuarioMfaRecuperacion
{
    public long IdUsuario { get; set; }
    public string CodigoHash { get; set; } = "";
    public DateTime? UsadoEn { get; set; }
}

public sealed class UsuarioTokenPassword
{
    public Guid IdToken { get; set; }
    public long IdUsuario { get; set; }
    public string TokenHash { get; set; } = "";
    public Guid Sello { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime ExpiraEn { get; set; }
    public DateTime? ConsumidoEn { get; set; }
    public DateTime? RevocadoEn { get; set; }
}
public sealed class RecuperacionLimite
{
    public string CorreoHash { get; set; } = "";
    public DateTime VentanaUtc { get; set; }
    public int Solicitudes { get; set; }
}
