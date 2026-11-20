using System.Security.Claims;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Catalogo;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Web.Models;

public sealed record AccesoMenu(string Grupo, string Nombre, string Descripcion,
    string Controlador, string Accion, string Icono)
{
    public bool Activo(string? controlador, string? accion) =>
        string.Equals(Controlador, controlador, StringComparison.OrdinalIgnoreCase) &&
        (Controlador is not ("Catalogo" or "Home") || string.Equals(Accion, accion, StringComparison.OrdinalIgnoreCase));
}

public static class MenuPrincipal
{
    public static AccesoMenu[] Crear(ClaimsPrincipal usuario)
    {
        if (usuario.Identity?.IsAuthenticated != true) return [];
        var accesos = new List<AccesoMenu>
        {
            new("Principal", "Inicio", "Tu espacio de trabajo y accesos disponibles.", "Home", "Index", "M3 10 12 3l9 7M5 9v12h14V9M9 21v-7h6v7")
        };
        void Agregar(string permiso, string grupo, string nombre, string descripcion, string controlador, string accion, string icono)
        {
            if (usuario.HasClaim("pm_permission", permiso))
                accesos.Add(new(grupo, nombre, descripcion, controlador, accion, icono));
        }
        Agregar(Permissions.Context, "Negocios", "Mis negocios", "Consulta y selecciona tu negocio de trabajo.",
            "Negocios", "Index", "M3 21h18M5 21V5h14v16M8 8h2m4 0h2M8 12h2m4 0h2M10 21v-5h4v5");
        Agregar(PerfilTiendaServicio.Permiso, "Negocios", "Mi tienda", "Actualiza el perfil comercial de tu tienda.",
            "PerfilTienda", "Index", "M3 10 5 3h14l2 7M4 13v8h16v-8M9 21v-6h6v6M3 10c0 4 6 4 6 0 0 4 6 4 6 0 0 4 6 4 6 0");
        Agregar(ProductoServicio.PermisoConsultar, "Catálogo", "Productos", "Gestiona tu catálogo, imágenes y estados.",
            "Productos", "Index", "m3 7 9-4 9 4v10l-9 4-9-4V7Zm0 0 9 4 9-4M12 11v10M7 5l10 4");
        Agregar(PermisosCatalogo.ConsultarCategorias, "Catálogo", "Categorías", "Organiza las categorías del catálogo.",
            "Catalogo", "Index", "M3 4h7l11 11-6 6L3 9V4ZM7 7h.01");
        Agregar(PermisosCatalogo.ConsultarUnidades, "Catálogo", "Unidades", "Consulta las unidades de medida disponibles.",
            "Catalogo", "Unidades", "m3 16 13-13 5 5L8 21l-5-5Zm6-6 2 2m2-6 2 2m2-6 2 2");
        Agregar(Permissions.View, "Administración", "Usuarios", "Consulta y administra usuarios según tu alcance.",
            "Usuarios", "Index", "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm11 10v-2a4 4 0 0 0-3-4m-1-12a4 4 0 0 1 0 8");
        Agregar(Permissions.Manage, "Administración", "Roles y permisos", "Administra los permisos de los roles autorizados.",
            "Administracion", "Permisos", "m12 3 9 4v6c0 5-9 9-9 9S3 18 3 13V7l9-4Zm-4 9 3 3 5-5");
        accesos.Add(new("Cuenta", "Mi perfil", "Consulta tu cuenta y opciones de seguridad.", "Cuenta", "Perfil",
            "M20 21v-2a7 7 0 0 0-14 0v2M13 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z"));
        return accesos.ToArray();
    }
}

public sealed record PanelInicioModelo(AccesoMenu[] Accesos, string? Buscar);
