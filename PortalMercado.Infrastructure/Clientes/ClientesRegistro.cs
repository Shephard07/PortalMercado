using Microsoft.Extensions.Hosting;
using PortalMercado.Infrastructure.Clientes.Demostracion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;

namespace PortalMercado.Infrastructure.Clientes;

public sealed record DocumentoRegistro(string Version, string Texto);
public sealed record DocumentacionRegistro(DocumentoRegistro Terminos, DocumentoRegistro Privacidad,
    DocumentoRegistro? Promociones)
{
    public bool Disponible
    {
        get
        {
            try
            {
                Versiones.ExigirConfiguracion();
                return !string.IsNullOrWhiteSpace(Terminos.Texto) && !string.IsNullOrWhiteSpace(Privacidad.Texto)
                    && (Promociones is null || !string.IsNullOrWhiteSpace(Promociones.Texto));
            }
            catch (AccessFailure) { return false; }
        }
    }
    public DocumentosClienteVigentes Versiones => new(Terminos.Version, Privacidad.Version, Promociones?.Version);
}

// Sustituir mediante DI cuando se disponga del contrato transaccional aprobado.
// Nunca crea cuentas en memoria ni informa un alta que no haya ocurrido.
public sealed class RegistroClientePendiente : IRepositorioRegistroCliente
{
    public Task<RegistroClientePersistido> RegistrarAsync(RegistroClienteAlta alta, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        throw new AccessFailure(503, "El registro no está disponible en este momento.");
    }
}

public static class ClientesRegistro
{
    public static IServiceCollection AddRegistroClientes(this IServiceCollection servicios, IConfiguration config, IHostEnvironment? ambiente = null)
    {
        var demostracion = config.GetValue<bool>("Clientes:Demostracion");
        if (demostracion && ambiente?.IsDevelopment() != true)
            throw new InvalidOperationException("La demostración de clientes solo puede activarse explícitamente en Development.");
        servicios.TryAddSingleton(new ClientesModo(demostracion));
        DocumentoRegistro Leer(string tipo) => new(config[$"Clientes:Documentos:{tipo}:Version"] ?? "",
            config[$"Clientes:Documentos:{tipo}:Texto"] ?? "");
        var promocional = Leer("Promociones");
        var documentos = new DocumentacionRegistro(Leer("Terminos"), Leer("Privacidad"),
            string.IsNullOrEmpty(promocional.Version) && string.IsNullOrEmpty(promocional.Texto) ? null : promocional);
        if (demostracion) documentos = ClientesDemostracionRegistro.Documentos();
        servicios.TryAddSingleton(documentos);
        servicios.TryAddSingleton(documentos.Versiones);
        servicios.TryAddScoped<PortalMercado.Application.Security.IAccountDelivery, PortalMercado.Infrastructure.Security.RecoveryDelivery>();
        servicios.TryAddScoped<ICorreoClienteService, CorreoClientePendiente>();
        servicios.TryAddScoped<IHashPasswordCliente, HashPasswordCliente>();
        servicios.TryAddScoped<IRepositorioRegistroCliente, RegistroClientePendiente>();
        servicios.TryAddScoped<RegistroClienteServicio>();
        servicios.TryAddScoped<IRepositorioPerfilCliente, PerfilDireccionesPendiente>();
        servicios.TryAddScoped<IRepositorioDireccionesCliente, PerfilDireccionesPendiente>();
        servicios.TryAddScoped<IClienteService, PerfilClienteServicio>();
        servicios.TryAddScoped<IDireccionService, DireccionClienteServicio>();
        servicios.TryAddScoped<IRepositorioConsentimientosCliente, ConsentimientosClientePendiente>();
        servicios.TryAddScoped<IConsentimientoService, ConsentimientoClienteServicio>();
        if (demostracion) ClientesDemostracionRegistro.Agregar(servicios);
        return servicios;
    }
}
