using System.Security.Claims;
using PortalMercado.Web.Models;

namespace PortalMercado.Tests.Tiendas;

public sealed class MenuPrincipalTests
{
    [Fact]
    public void Anonimo_no_obtiene_menu_privado() =>
        Assert.Empty(MenuPrincipal.Crear(new ClaimsPrincipal(new ClaimsIdentity())));

    [Theory]
    [InlineData("NEGOCIO_CONTEXTO", "Mis negocios")]
    [InlineData("PRODUCTO_GESTIONAR", "Mi tienda")]
    [InlineData("PRODUCTO_VER", "Productos")]
    [InlineData("CATEGORIA_VER", "Categorías")]
    [InlineData("UNIDAD_MEDIDA_VER", "Unidades")]
    [InlineData("USUARIO_VER", "Usuarios")]
    [InlineData("PERMISO_GESTIONAR", "Roles y permisos")]
    public void Cada_permiso_habilita_solo_su_acceso(string permiso, string nombre)
    {
        var usuario = new ClaimsPrincipal(new ClaimsIdentity([new Claim("pm_permission", permiso)], "prueba"));
        var menu = MenuPrincipal.Crear(usuario);
        Assert.Equal(3, menu.Length);
        Assert.Contains(menu, a => a.Nombre == nombre);
        Assert.Contains(menu, a => a.Nombre == "Inicio");
        Assert.Contains(menu, a => a.Nombre == "Mi perfil");
    }

    [Fact]
    public void Rol_administrador_no_suplanta_permisos_ausentes()
    {
        var usuario = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "ADMIN_GENERAL")], "prueba"));
        Assert.Equal(new[] { "Inicio", "Mi perfil" }, MenuPrincipal.Crear(usuario).Select(a => a.Nombre).ToArray());
    }

    [Theory]
    [InlineData("Catalogo", "Index", "Index", true)]
    [InlineData("Catalogo", "Unidades", "Index", false)]
    [InlineData("Catalogo", "Unidades", "Unidades", true)]
    [InlineData("Productos", "Detalle", "Index", true)]
    [InlineData("Home", "Privacy", "Index", false)]
    [InlineData("PerfilTienda", "Editar", "Index", true)]
    public void Marca_activa_la_seccion_correcta(string controlador, string accion, string accionMenu, bool esperado)
    {
        var acceso = new AccesoMenu("grupo", "nombre", "descripcion", controlador, accionMenu, "");
        Assert.Equal(esperado, acceso.Activo(controlador, accion));
    }
}
