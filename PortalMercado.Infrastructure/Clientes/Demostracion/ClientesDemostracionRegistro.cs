using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Clientes;
using PortalMercado.Application.Security;

namespace PortalMercado.Infrastructure.Clientes.Demostracion;

public static class ClientesDemostracionRegistro
{
    internal static void Agregar(IServiceCollection servicios)
    {
        servicios.AddSingleton<ClientesDemostracion>();
        servicios.Replace(ServiceDescriptor.Singleton<IRepositorioRegistroCliente>(p => p.GetRequiredService<ClientesDemostracion>()));
        servicios.Replace(ServiceDescriptor.Singleton<IRepositorioPerfilCliente>(p => p.GetRequiredService<ClientesDemostracion>()));
        servicios.Replace(ServiceDescriptor.Singleton<IRepositorioDireccionesCliente>(p => p.GetRequiredService<ClientesDemostracion>()));
        servicios.Replace(ServiceDescriptor.Singleton<IRepositorioConsentimientosCliente>(p => p.GetRequiredService<ClientesDemostracion>()));
        servicios.AddScoped<SeguridadClientesDemostracion>();
        servicios.Replace(ServiceDescriptor.Scoped<IAuthService>(p => p.GetRequiredService<SeguridadClientesDemostracion>()));
        servicios.Replace(ServiceDescriptor.Scoped<IPasswordService>(p => p.GetRequiredService<SeguridadClientesDemostracion>()));
        servicios.Replace(ServiceDescriptor.Scoped<ICorreoClienteService>(p => p.GetRequiredService<SeguridadClientesDemostracion>()));
        servicios.Replace(ServiceDescriptor.Scoped<IMfaService>(p => p.GetRequiredService<SeguridadClientesDemostracion>()));
    }
    internal static DocumentacionRegistro Documentos() => new(
        new("demo-terminos-1", "DOCUMENTO FICTICIO PARA DEMOSTRACIÓN. Solo se utilizan cuentas y datos de prueba. Los datos de clientes se guardan en memoria mientras este proceso esté abierto; se pierden al detenerlo. No hay compras ni pedidos. Este texto no es un documento legal aprobado."),
        new("demo-privacidad-1", "DOCUMENTO FICTICIO PARA DEMOSTRACIÓN. No introduzcas datos personales reales. Perfil, direcciones y decisiones se mantienen temporalmente en memoria. Se conserva evidencia técnica temporal para probar los flujos. Este texto no sustituye una política de privacidad aprobada."),
        new("demo-promociones-1", "DOCUMENTO FICTICIO PARA DEMOSTRACIÓN. Autorizar promociones es opcional; se puede retirar la autorización en Mis consentimientos. No se envía publicidad. Los mensajes de seguridad de prueba se guardan localmente y no se envían por Internet. Este texto no es una autorización comercial para producción."));
}
