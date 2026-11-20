using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PortalMercado.Application.Tiendas;
using PortalMercado.Infrastructure.Tiendas;

namespace PortalMercado.Tests.Tiendas;

public sealed class TiendasSqlLocalConfiguracionTests
{
    [Theory]
    [InlineData(@"Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;")]
    [InlineData(@"Server=localhost\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;")]
    public void Acepta_solo_la_copia_local_con_autenticacion_windows(string conexion) =>
        Assert.NotEmpty(TiendasSqlLocalOpciones.Crear(conexion).Conexion);

    [Theory]
    [InlineData(@"Server=sql.example.invalid;Database=PortalMercado_ET01_Test;Integrated Security=True;")]
    [InlineData(@"Server=.\SQLEXPRESS;Database=PortalMercado;Integrated Security=True;")]
    [InlineData(@"Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;User ID=sa;Password=ficticia;")]
    [InlineData("cadena invalida")]
    [InlineData("")]
    public void Rechaza_otra_base_servidor_o_credenciales_sql(string conexion) =>
        Assert.Throws<InvalidOperationException>(() => TiendasSqlLocalOpciones.Crear(conexion));

    [Theory]
    [InlineData(true, "Development")]
    [InlineData(false, "Production")]
    public void Modo_sql_local_rechaza_demo_simultanea_y_produccion(bool demo,string ambiente)
    {
        var config = Config(demo);
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddTiendas(config,new Entorno { EnvironmentName=ambiente }));
    }

    [Fact]
    public void Selecciona_adaptadores_sql_sin_abrir_una_conexion()
    {
        using var proveedor = new ServiceCollection().AddTiendas(Config(false),new Entorno()).BuildServiceProvider();
        using var scope = proveedor.CreateScope();
        Assert.IsType<RepositorioTiendasSqlLocal>(scope.ServiceProvider.GetRequiredService<IRepositorioTiendas>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<IRepositorioTiendas>(),scope.ServiceProvider.GetRequiredService<IRepositorioPerfilTienda>());
    }
    private static IConfiguration Config(bool demo) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    {
        ["Tiendas:SqlLocal"]="true",["Tiendas:Demostracion"]=demo.ToString(),
        ["ConnectionStrings:PortalMercado"]=@"Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;"
    }).Build();
    private sealed class Entorno : IHostEnvironment
    {
        public string EnvironmentName { get; set; }="Development";
        public string ApplicationName { get; set; }="Pruebas";
        public string ContentRootPath { get; set; }=Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; }=new NullFileProvider();
    }
}
