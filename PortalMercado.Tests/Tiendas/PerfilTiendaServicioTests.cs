using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;
using PortalMercado.Infrastructure.Tiendas;

namespace PortalMercado.Tests.Tiendas;

public sealed class PerfilTiendaServicioTests
{
    [Fact]
    public async Task Cambia_campos_permitidos_sin_tocar_identidad_ni_consentimiento()
    {
        var (servicio, publico, _) = Crear();
        var antes = await publico.ObtenerAsync("fruteria-el-sol", default);
        var perfil = await servicio.ObtenerAsync(77, "sesion", "MVC", 1, default);
        perfil.Datos.Descripcion = "  Descripción actualizada  ";
        perfil.Datos.Horario = "Lunes a viernes";
        perfil.Datos.LogoUrl = "https://example.invalid/logo.jpg";
        perfil.Datos.Telefono = "987654321";
        perfil.Datos.Correo = "nuevo@example.invalid";
        await servicio.GuardarAsync(77, "sesion", "MVC", 1, perfil.Datos, "traza-ficticia", default);
        var despues = await publico.ObtenerAsync("fruteria-el-sol", default);
        Assert.Equal("Descripción actualizada", despues.Descripcion);
        Assert.Equal("Lunes a viernes", despues.Horario);
        Assert.Equal(perfil.Datos.LogoUrl, despues.LogoUrl);
        Assert.Equal(antes.IdNegocio, despues.IdNegocio);
        Assert.Equal(antes.IdMercado, despues.IdMercado);
        Assert.Equal(antes.NombreComercial, despues.NombreComercial);
        Assert.Null(despues.Telefono);
        Assert.Null(despues.Correo);
        var actualizado = await servicio.ObtenerAsync(77, "sesion", "MVC", 1, default);
        Assert.NotEqual(perfil.Datos.Version, actualizado.Datos.Version);
        Assert.Equal(77, actualizado.UltimoCambio!.Actor);
        Assert.Equal(1, actualizado.UltimoCambio.IdNegocio);
        Assert.Equal("traza-ficticia", actualizado.UltimoCambio.Correlacion);
    }

    [Theory]
    [InlineData("http://example.invalid/logo.jpg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AA")]
    [InlineData("https://usuario:clave@example.invalid/logo.jpg")]
    [InlineData("/images/logo.jpg")]
    public async Task Rechaza_logo_inseguro_sin_mutar_ni_auditar(string logo)
    {
        var (servicio, _, _) = Crear();
        var perfil = await servicio.ObtenerAsync(77, "sesion", "MVC", 1, default);
        perfil.Datos.LogoUrl = logo;
        var error = await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.GuardarAsync(77, "sesion", "MVC", 1, perfil.Datos, "traza", default));
        Assert.Equal(400, error.Status);
        var despues = await servicio.ObtenerAsync(77, "sesion", "MVC", 1, default);
        Assert.Equal(perfil.Datos.Version, despues.Datos.Version);
        Assert.Null(despues.Datos.LogoUrl);
        Assert.Null(despues.UltimoCambio);
    }

    [Theory]
    [InlineData("descripcion")]
    [InlineData("horario")]
    [InlineData("telefono")]
    [InlineData("correo")]
    [InlineData("control")]
    [InlineData("version")]
    public async Task Valida_limites_y_formato_en_el_servicio(string caso)
    {
        var (servicio, _, _) = Crear();
        var perfil = await servicio.ObtenerAsync(77, "sesion", "MVC", 1, default);
        switch (caso)
        {
            case "descripcion": perfil.Datos.Descripcion = new string('x', 4001); break;
            case "horario": perfil.Datos.Horario = new string('x', 501); break;
            case "telefono": perfil.Datos.Telefono = new string('1', 31); break;
            case "correo": perfil.Datos.Correo = "correo-invalido"; break;
            case "control": perfil.Datos.Telefono = "123\n456"; break;
            case "version": perfil.Datos.Version = Guid.Empty; break;
        }
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.GuardarAsync(77, "sesion", "MVC", 1, perfil.Datos, "traza", default))).Status);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 90)]
    [InlineData(true, 0)]
    public async Task Exige_permiso_y_pertenencia_tambien_al_guardar(bool permiso, long negocio)
    {
        var (servicio, _, usuarios) = Crear();
        usuarios.Habilitado = permiso;
        Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.ObtenerAsync(77, "sesion", "API", negocio, default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.GuardarAsync(77, "sesion", "API", negocio, new(), "traza", default))).Status);
    }

    [Fact]
    public async Task Version_anterior_no_sobrescribe_y_conserva_la_auditoria_del_cambio_valido()
    {
        var (servicio, publico, _) = Crear();
        var a = await servicio.ObtenerAsync(77, "sesion", "MVC", 1, default);
        var b = await servicio.ObtenerAsync(77, "sesion", "MVC", 1, default);
        a.Datos.Descripcion = "Primera ventana";
        await servicio.GuardarAsync(77, "sesion", "MVC", 1, a.Datos, "traza-1", default);
        b.Datos.Descripcion = "Segunda ventana";
        Assert.Equal(409, (await Assert.ThrowsAsync<AccessFailure>(() =>
            servicio.GuardarAsync(77, "sesion", "MVC", 1, b.Datos, "traza-2", default))).Status);
        Assert.Equal("Primera ventana", (await publico.ObtenerAsync("fruteria-el-sol", default)).Descripcion);
        Assert.Equal("traza-1", (await servicio.ObtenerAsync(77, "sesion", "MVC", 1, default)).UltimoCambio!.Correlacion);
    }

    [Fact]
    public async Task Sin_vinculo_explicito_no_hay_perfil_de_demostracion()
    {
        var repositorio = new RepositorioPerfilDemostracion(new(), new(null));
        Assert.Null(await repositorio.ObtenerAsync(1, default));
        Assert.Equal(404, (await Assert.ThrowsAsync<AccessFailure>(() =>
            repositorio.GuardarAsync(1, new(), new(77, 1, DateTimeOffset.UtcNow, "traza"), default))).Status);
    }

    [Fact]
    public async Task Campos_vacios_quitan_logo_y_textos_con_reemplazo_publico()
    {
        var (servicio, publico, _) = Crear();
        var p = await servicio.ObtenerAsync(77, "sesion", "MVC", 1, default);
        await servicio.GuardarAsync(77, "sesion", "MVC", 1, new() { Version = p.Datos.Version, LogoUrl = "   " }, "traza", default);
        var despues = await publico.ObtenerAsync("fruteria-el-sol", default);
        Assert.Null(despues.LogoUrl);
        Assert.Null(despues.Descripcion);
    }

    [Fact]
    public async Task Cancelacion_no_actualiza_perfil()
    {
        var (servicio, _, _) = Crear();
        var perfil = await servicio.ObtenerAsync(77, "sesion", "MVC", 1, default);
        using var cancelado = new CancellationTokenSource();
        cancelado.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            servicio.GuardarAsync(77, "sesion", "MVC", 1, perfil.Datos, "traza", cancelado.Token));
        Assert.Equal(perfil.Datos.Version, (await servicio.ObtenerAsync(77, "sesion", "MVC", 1, default)).Datos.Version);
    }

    private static (PerfilTiendaServicio, TiendaServicio, TiendasUsuariosPrueba) Crear()
    {
        var tiendas = new RepositorioTiendasDemostracion();
        var usuarios = new TiendasUsuariosPrueba();
        return (new(usuarios, new RepositorioPerfilDemostracion(tiendas, new(1))), new(tiendas), usuarios);
    }
}
