using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortalMercado.Application.Interfaces;

namespace PortalMercado.Tests;

public sealed class ApiFactory
    : WebApplicationFactory<PortalMercado.API.Controllers.HealthController>
{
    private readonly string _environment;
    private readonly bool _throwOnDatabaseCheck;

    public ApiFactory(
        string environment = "Development",
        bool throwOnDatabaseCheck = false)
    {
        _environment = environment;
        _throwOnDatabaseCheck = throwOnDatabaseCheck;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        // Configuración ficticia: estas pruebas no abren conexiones SQL.
        builder.UseSetting(
            "ConnectionStrings:PortalMercado",
            "Server=127.0.0.1;Database=PortalMercado_Test;" +
            "Integrated Security=True;Encrypt=True;Connect Timeout=1;");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDatabaseHealthService>();

            services.AddSingleton<IDatabaseHealthService>(
                new ControlledDatabaseHealthService(
                    _throwOnDatabaseCheck));
        });
    }

    private sealed class ControlledDatabaseHealthService
        : IDatabaseHealthService
    {
        private readonly bool _throwOnCheck;

        public ControlledDatabaseHealthService(bool throwOnCheck)
        {
            _throwOnCheck = throwOnCheck;
        }

        public Task<bool> CanConnectAsync(
            CancellationToken cancellationToken = default)
        {
            return GetResult();
        }

        public Task<bool> CanReadMercadosAsync(
            CancellationToken cancellationToken = default)
        {
            return GetResult();
        }

        private Task<bool> GetResult()
        {
            if (_throwOnCheck)
            {
                throw new InvalidOperationException(
                    "DATO_INTERNO_FICTICIO");
            }

            return Task.FromResult(false);
        }
    }
}