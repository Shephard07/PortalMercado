using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Catalogo;
using PortalMercado.Infrastructure.Persistence;

namespace PortalMercado.Infrastructure.Catalogo;

public sealed class SqlCatalogo(PortalMercadoDbContext context) : IRepositorioCategorias, IRepositorioUnidadesMedida
{
    public async Task<Page<CategoriaDto>> ListarAsync(CategoriaFiltro filtro, CancellationToken ct)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await using var command = CrearComando(connection, "dbo.pm_Catalogo_Categorias_Listar");
        command.Parameters.Add("@Buscar", SqlDbType.NVarChar, 4000).Value = (object?)filtro.Buscar ?? DBNull.Value;
        command.Parameters.Add("@Activa", SqlDbType.Bit).Value = (object?)filtro.Activa ?? DBNull.Value;
        command.Parameters.Add("@Pagina", SqlDbType.Int).Value = filtro.Pagina;
        command.Parameters.Add("@Tamano", SqlDbType.Int).Value = filtro.Tamano;
        var total = command.Parameters.Add("@Total", SqlDbType.Int);
        total.Direction = ParameterDirection.Output;

        var categorias = new List<CategoriaDto>();
        await connection.OpenAsync(ct);
        try
        {
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                    categorias.Add(LeerCategoria(reader));
            }
        }
        catch (SqlException ex) when (ex.Number == 51062)
        {
            throw TraducirError(ex.Number);
        }

        return new Page<CategoriaDto>(categorias.ToArray(), (int)total.Value, filtro.Pagina, filtro.Tamano);
    }

    public async Task<CategoriaDto> GuardarAsync(long? id, CategoriaDatos datos, long actor, string correlacion, CancellationToken ct)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await using var command = CrearComando(connection, "dbo.pm_Catalogo_Categorias_Guardar");
        command.Parameters.Add("@IdCategoria", SqlDbType.BigInt).Value = (object?)id ?? DBNull.Value;
        command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 4000).Value = datos.Nombre;
        command.Parameters.Add("@Slug", SqlDbType.VarChar, 8000).Value = datos.Slug;
        command.Parameters.Add("@IdPadre", SqlDbType.BigInt).Value = (object?)datos.IdPadre ?? DBNull.Value;
        AgregarAuditoria(command, actor, correlacion);

        await connection.OpenAsync(ct);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("El procedimiento no devolvió la categoría guardada.");

            return LeerCategoria(reader);
        }
        catch (SqlException ex) when (ex.Number is >= 51062 and <= 51068)
        {
            throw TraducirError(ex.Number);
        }
    }

    public async Task CambiarEstadoAsync(long id, bool activa, long actor, string correlacion, CancellationToken ct)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await using var command = CrearComando(connection, "dbo.pm_Catalogo_Categorias_CambiarEstado");
        command.Parameters.Add("@IdCategoria", SqlDbType.BigInt).Value = id;
        command.Parameters.Add("@Activa", SqlDbType.Bit).Value = activa;
        AgregarAuditoria(command, actor, correlacion);

        await connection.OpenAsync(ct);
        try
        {
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex) when (ex.Number is >= 51062 and <= 51068)
        {
            throw TraducirError(ex.Number);
        }
    }

    public async Task<UnidadMedidaDto[]> ListarAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await using var command = CrearComando(connection, "dbo.pm_Catalogo_Unidades_Listar");
        await connection.OpenAsync(ct);

        var unidades = new List<UnidadMedidaDto>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        int id = reader.GetOrdinal("IdUnidad");
        int codigo = reader.GetOrdinal("Codigo");
        int nombre = reader.GetOrdinal("Nombre");
        int permiteFraccion = reader.GetOrdinal("PermiteFraccion");

        while (await reader.ReadAsync(ct))
            unidades.Add(new UnidadMedidaDto(reader.GetInt32(id), reader.GetString(codigo), reader.GetString(nombre),
                reader.GetBoolean(permiteFraccion)));

        return unidades.ToArray();
    }

    private static SqlCommand CrearComando(SqlConnection connection, string procedimiento) =>
        new(procedimiento, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };

    private static void AgregarAuditoria(SqlCommand command, long actor, string correlacion)
    {
        command.Parameters.Add("@IdActor", SqlDbType.BigInt).Value = actor;
        command.Parameters.Add("@Correlacion", SqlDbType.VarChar, 8000).Value = correlacion;
    }

    private static CategoriaDto LeerCategoria(SqlDataReader reader)
    {
        int padre = reader.GetOrdinal("IdPadre");
        int nombrePadre = reader.GetOrdinal("NombrePadre");
        return new CategoriaDto(
            reader.GetInt64(reader.GetOrdinal("IdCategoria")),
            reader.IsDBNull(padre) ? null : reader.GetInt64(padre),
            reader.IsDBNull(nombrePadre) ? null : reader.GetString(nombrePadre),
            reader.GetString(reader.GetOrdinal("Nombre")),
            reader.GetString(reader.GetOrdinal("Slug")),
            reader.GetBoolean(reader.GetOrdinal("Activa")));
    }

    private static AccessFailure TraducirError(int codigo) => codigo switch
    {
        51063 => new(404, "La categoría no existe."),
        51064 => new(400, "La categoría padre no existe."),
        51065 => new(400, "La relación indicada genera un ciclo de categorías."),
        51066 => new(409, "El slug ya está en uso."),
        51067 => new(409, "El catálogo está ocupado. Intenta nuevamente."),
        51068 => new(401, "La sesión ya no está disponible."),
        _ => new(400, "Revisa los datos de la operación de catálogo.")
    };
}
