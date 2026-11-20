using Microsoft.AspNetCore.Identity;
using PortalMercado.Application.Clientes;
using PortalMercado.Domain.Entities;

namespace PortalMercado.Infrastructure.Clientes;

// Misma dependencia y configuración de hash que SqlAuthService y SqlPasswordService.
public sealed class HashPasswordCliente(IPasswordHasher<Usuario> hasher) : IHashPasswordCliente
{
    public string Crear(string nombre, string correo, string password) =>
        hasher.HashPassword(new Usuario { Nombre = nombre, Correo = correo }, password);
}
