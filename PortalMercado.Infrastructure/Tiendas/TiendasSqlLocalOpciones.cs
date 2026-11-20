using Microsoft.Data.SqlClient;

namespace PortalMercado.Infrastructure.Tiendas;

public sealed record TiendasSqlLocalOpciones(string Conexion)
{
    public static TiendasSqlLocalOpciones Crear(string? conexion)
    {
        SqlConnectionStringBuilder builder;
        try { builder = new(conexion ?? ""); }
        catch (ArgumentException) { throw new InvalidOperationException("La conexión de tiendas locales no es válida."); }
        var equipo = Environment.MachineName;
        var servidores = new[] { @".\SQLEXPRESS", @"localhost\SQLEXPRESS", @"(local)\SQLEXPRESS",
            @"127.0.0.1\SQLEXPRESS", equipo + @"\SQLEXPRESS" };
        if (!string.Equals(builder.InitialCatalog, "PortalMercado_ET01_Test", StringComparison.OrdinalIgnoreCase) ||
            !servidores.Contains(builder.DataSource, StringComparer.OrdinalIgnoreCase) || !builder.IntegratedSecurity)
            throw new InvalidOperationException("Tiendas SQL local requiere PortalMercado_ET01_Test en el SQLEXPRESS de este equipo con autenticación de Windows.");
        return new(builder.ConnectionString);
    }
}
