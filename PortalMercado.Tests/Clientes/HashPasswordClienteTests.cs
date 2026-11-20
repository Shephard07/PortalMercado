using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Clientes;

namespace PortalMercado.Tests.Clientes;

public sealed class HashPasswordClienteTests
{
    [Fact]
    public void Hash_de_registro_es_compatible_con_el_verificador_de_identidad_existente()
    {
        var hasher = new PasswordHasher<Usuario>(Options.Create(new PasswordHasherOptions { IterationCount = 10_000 }));
        var servicio = new HashPasswordCliente(hasher);
        const string password = "Compra-Prueba2026!";
        var hash = servicio.Crear("Comprador", "comprador@example.invalid", password);
        var usuario = new Usuario { Nombre = "Comprador", Correo = "comprador@example.invalid" };
        Assert.NotEqual(password, hash);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(usuario, hash, password));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(usuario, hash, "Otra-Prueba2026!"));
        Assert.NotEqual(hash, servicio.Crear(usuario.Nombre, usuario.Correo, password));
    }
}
