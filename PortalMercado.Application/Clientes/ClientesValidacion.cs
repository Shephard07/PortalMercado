using System.ComponentModel.DataAnnotations;
using System.Net;
using PortalMercado.Application.Administration;

namespace PortalMercado.Application.Clientes;

public static class ClientesValidacion
{
    // Las capacidades proceden del esquema original, no de una instalación SQL nueva.
    public static ClientePerfilDatos Perfil(ClientePerfilDatos datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ValidarModelo(datos);
        var limpio = new ClientePerfilDatos
        {
            Apellidos = Texto(datos.Apellidos), Celular = Texto(datos.Celular),
            TipoDocumento = Texto(datos.TipoDocumento), NumeroDocumento = Texto(datos.NumeroDocumento)
        };
        if ((limpio.TipoDocumento is null) != (limpio.NumeroDocumento is null))
            throw Invalido("Completa el tipo y número de documento, o deja ambos vacíos.");
        if (!Ascii(limpio.Celular) || !Ascii(limpio.TipoDocumento) || !Ascii(limpio.NumeroDocumento))
            throw Invalido("Revisa el celular y los datos del documento.");
        return limpio;
    }
    public static ClienteDireccionDatos Direccion(ClienteDireccionDatos datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ValidarModelo(datos);
        var limpio = new ClienteDireccionDatos
        {
            Etiqueta = Texto(datos.Etiqueta), Direccion = Texto(datos.Direccion) ?? "",
            Referencia = Texto(datos.Referencia), Distrito = Texto(datos.Distrito),
            Provincia = Texto(datos.Provincia), Departamento = Texto(datos.Departamento),
            Latitud = datos.Latitud, Longitud = datos.Longitud
        };
        if (limpio.Direccion.Length == 0 || limpio.Latitud.HasValue != limpio.Longitud.HasValue)
            throw Invalido("Escribe una dirección y, si usas coordenadas, completa ambas.");
        if ((limpio.Latitud.HasValue && decimal.Round(limpio.Latitud.Value, 7) != limpio.Latitud.Value) ||
            (limpio.Longitud.HasValue && decimal.Round(limpio.Longitud.Value, 7) != limpio.Longitud.Value))
            throw Invalido("Las coordenadas admiten hasta siete decimales.");
        return limpio;
    }
    public static ClienteOperacion Operacion(long idUsuario, ClienteOperacionContexto contexto, TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        if (contexto.Correlacion is not { Length: >= 1 and <= 64 } ||
            contexto.Correlacion.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            throw new ArgumentException("El host debe proporcionar una correlación segura.", nameof(contexto));
        string? ip = null;
        if (!string.IsNullOrWhiteSpace(contexto.Ip))
        {
            // Solo direcciones IP literales, sin puertos, cadenas de proxy ni identificadores de zona.
            if (contexto.Ip.Contains('%') || !IPAddress.TryParse(contexto.Ip, out var valor))
                throw new ArgumentException("El host debe proporcionar una IP válida.", nameof(contexto));
            ip = valor.ToString();
        }
        return new(idUsuario, reloj.GetUtcNow().ToUniversalTime(), contexto.Correlacion, ip);
    }
    internal static void ValidarModelo(object datos)
    {
        if (!Validator.TryValidateObject(datos, new ValidationContext(datos), [], true))
            throw Invalido("Revisa los campos y sus longitudes máximas.");
    }
    internal static string? Texto(string? valor)
    {
        if (valor?.Any(char.IsControl) == true)
            throw Invalido("Los campos no admiten caracteres de control.");
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }
    // Criterio conservador para VARCHAR, hasta acordar la codificación con DBA.
    internal static bool Ascii(string? valor) => valor is null || valor.All(c => c is >= ' ' and <= '~');
    internal static AccessFailure Invalido(string mensaje) => new(400, mensaje);
}

public static class DestinoCliente
{
    public const string Predeterminado = "/tiendas";
    // Defensa adicional; el host MVC deberá terminar con LocalRedirect/IsLocalUrl.
    public static string Normalizar(string? destino)
    {
        if (destino is null || destino.Length > 2048) return Predeterminado;
        var comprobacion = destino;
        for (var i = 0; i < 4; i++)
        {
            if (!comprobacion.StartsWith('/') || comprobacion.StartsWith("//", StringComparison.Ordinal) ||
                comprobacion.Contains('\\') || comprobacion.Any(char.IsControl)) return Predeterminado;
            string decodificado;
            try { decodificado = Uri.UnescapeDataString(comprobacion); }
            catch (UriFormatException) { return Predeterminado; }
            if (decodificado == comprobacion) return destino;
            comprobacion = decodificado;
        }
        return Predeterminado;
    }
}
