using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Catalogo;
using PortalMercado.Infrastructure.Persistence;

namespace PortalMercado.Infrastructure.Catalogo;

public sealed class SqlProductos(PortalMercadoDbContext context) : IRepositorioProductos
{
    public async Task<Page<ProductoResumen>> ListarAsync(long idNegocio, ProductoFiltro filtro, CancellationToken ct)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await using var command = new SqlCommand("dbo.pm_Catalogo_Productos_Listar", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 15
        };
        command.Parameters.Add("@IdNegocio", SqlDbType.BigInt).Value = idNegocio;
        command.Parameters.Add("@IdCategoria", SqlDbType.BigInt).Value = (object?)filtro.IdCategoria ?? DBNull.Value;
        command.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = (object?)filtro.Estado ?? DBNull.Value;
        command.Parameters.Add("@Buscar", SqlDbType.NVarChar, 200).Value = (object?)filtro.Buscar ?? DBNull.Value;
        command.Parameters.Add("@Pagina", SqlDbType.Int).Value = filtro.Pagina;
        command.Parameters.Add("@Tamano", SqlDbType.Int).Value = filtro.Tamano;
        var total = command.Parameters.Add("@Total", SqlDbType.Int);
        total.Direction = ParameterDirection.Output;

        await connection.OpenAsync(ct);
        var items = new List<ProductoResumen>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                items.Add(new ProductoResumen(
                    reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4),
                    reader.GetDecimal(5), reader.GetString(6), reader.GetString(7), reader.GetBoolean(8),
                    reader.IsDBNull(9) ? null : reader.GetDecimal(9), reader.IsDBNull(10) ? null : reader.GetString(10)));
            }
        }

        return new Page<ProductoResumen>(items.ToArray(), (int)total.Value, filtro.Pagina, filtro.Tamano);
    }

    public async Task<ProductoFicha?> ObtenerAsync(long idNegocio, long id, CancellationToken ct)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await using var command = new SqlCommand("dbo.pm_Catalogo_Productos_Obtener", connection)
        {
            CommandType = CommandType.StoredProcedure, CommandTimeout = 15
        };
        command.Parameters.Add("@IdNegocio", SqlDbType.BigInt).Value = idNegocio;
        command.Parameters.Add("@IdProducto", SqlDbType.BigInt).Value = id;
        await connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new ProductoFicha(id, idNegocio, reader.GetString(7), new ProductoDatos
        {
            IdCategoria = reader.GetInt64(0),
            IdUnidad = reader.GetInt32(1),
            Sku = reader.IsDBNull(2) ? null : reader.GetString(2),
            Nombre = reader.GetString(3),
            Descripcion = reader.IsDBNull(4) ? null : reader.GetString(4),
            Precio = reader.GetDecimal(5),
            ControlaStock = reader.GetBoolean(6)
        });
    }

    public async Task<ProductoDetalle?> DetalleAsync(long idNegocio, long id, int pagina, CancellationToken ct)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await using var command = new SqlCommand("dbo.pm_Catalogo_Productos_Detalle", connection)
        {
            CommandType = CommandType.StoredProcedure, CommandTimeout = 15
        };
        command.Parameters.Add("@IdNegocio", SqlDbType.BigInt).Value = idNegocio;
        command.Parameters.Add("@IdProducto", SqlDbType.BigInt).Value = id;
        command.Parameters.Add("@Pagina", SqlDbType.Int).Value = pagina;
        await connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var inventario = reader.IsDBNull(7) ? null : new InventarioProducto(reader.GetDecimal(7), reader.GetDecimal(8));
        var detalle = new ProductoDetalle(id, idNegocio, reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5),
            reader.GetBoolean(6), inventario, [], pagina, false);

        var precios = new List<PrecioProducto>();
        if (!await reader.NextResultAsync(ct))
            throw new InvalidOperationException("El procedimiento no devolvió el historial de precios.");

        while (await reader.ReadAsync(ct))
        {
            precios.Add(new PrecioProducto(reader.GetDecimal(0), DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc),
                reader.IsDBNull(2) ? null : DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc)));
        }

        return detalle with { Precios = precios.Take(20).ToArray(), HayMasPrecios = precios.Count > 20 };
    }

    public async Task<long> GuardarAsync(long idNegocio, long? id, ProductoDatos datos, long actor, string correlacion,
        CancellationToken ct)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await using var command = new SqlCommand("dbo.pm_Catalogo_Productos_Guardar", connection)
        {
            CommandType = CommandType.StoredProcedure, CommandTimeout = 15
        };
        command.Parameters.Add("@IdNegocio", SqlDbType.BigInt).Value = idNegocio;
        command.Parameters.Add("@IdProducto", SqlDbType.BigInt).Value = (object?)id ?? DBNull.Value;
        command.Parameters.Add("@IdCategoria", SqlDbType.BigInt).Value = datos.IdCategoria;
        command.Parameters.Add("@IdUnidad", SqlDbType.Int).Value = datos.IdUnidad;
        command.Parameters.Add("@Sku", SqlDbType.VarChar, 70).Value = (object?)datos.Sku ?? DBNull.Value;
        command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 200).Value = datos.Nombre;
        command.Parameters.Add("@Descripcion", SqlDbType.NVarChar, -1).Value = (object?)datos.Descripcion ?? DBNull.Value;
        var precio = command.Parameters.Add("@Precio", SqlDbType.Decimal);
        precio.Precision = 18;
        precio.Scale = 2;
        precio.Value = (object?)datos.Precio ?? DBNull.Value;
        command.Parameters.Add("@ControlaStock", SqlDbType.Bit).Value = datos.ControlaStock;
        command.Parameters.Add("@IdActor", SqlDbType.BigInt).Value = actor;
        command.Parameters.Add("@Correlacion", SqlDbType.VarChar, 64).Value = correlacion;

        await connection.OpenAsync(ct);
        try
        {
            return (long)(await command.ExecuteScalarAsync(ct)
                ?? throw new InvalidOperationException("El guardado no devolvió un identificador."));
        }
        catch (SqlException ex) when (ex.Number is >= 51070 and <= 51074)
        {
            throw ex.Number switch
            {
                51071 => new AccessFailure(404, "No se encontró el producto en ese negocio."),
                51072 => new AccessFailure(409, "Ese SKU ya existe en el negocio."),
                51073 => new AccessFailure(409, "El historial de precios tiene una vigencia incompatible con este cambio."),
                51074 => new AccessFailure(403, "El actor o el negocio ya no están habilitados."),
                _ => new AccessFailure(400, "Revisa los datos, la categoría y la unidad del producto.")
            };
        }
    }

    public async Task<ImagenesProducto?> ImagenesAsync(long idNegocio, long id, CancellationToken ct)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await using var command = new SqlCommand("dbo.pm_Catalogo_Imagenes_Listar", connection)
        {
            CommandType = CommandType.StoredProcedure, CommandTimeout = 15
        };
        command.Parameters.Add("@IdNegocio", SqlDbType.BigInt).Value = idNegocio;
        command.Parameters.Add("@IdProducto", SqlDbType.BigInt).Value = id;
        await connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var nombre = reader.GetString(0);
        var imagenes = new List<ImagenProducto>();
        do
        {
            if (!reader.IsDBNull(1))
                imagenes.Add(new ImagenProducto(reader.GetInt64(1), reader.GetString(2), reader.GetInt32(3), reader.GetBoolean(4)));
        }
        while (await reader.ReadAsync(ct));

        return new ImagenesProducto(id, idNegocio, nombre, imagenes.ToArray());
    }

    public async Task<long> GuardarImagenAsync(long idNegocio, long id, long? idImagen, ImagenProductoDatos datos,
        long actor, string correlacion, CancellationToken ct)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await using var command = new SqlCommand("dbo.pm_Catalogo_Imagenes_Guardar", connection)
        {
            CommandType = CommandType.StoredProcedure, CommandTimeout = 15
        };
        command.Parameters.Add("@IdNegocio", SqlDbType.BigInt).Value = idNegocio;
        command.Parameters.Add("@IdProducto", SqlDbType.BigInt).Value = id;
        command.Parameters.Add("@IdImagen", SqlDbType.BigInt).Value = (object?)idImagen ?? DBNull.Value;
        command.Parameters.Add("@Url", SqlDbType.NVarChar, 500).Value = datos.Url;
        command.Parameters.Add("@Orden", SqlDbType.Int).Value = datos.Orden;
        command.Parameters.Add("@Principal", SqlDbType.Bit).Value = datos.Principal;
        command.Parameters.Add("@IdActor", SqlDbType.BigInt).Value = actor;
        command.Parameters.Add("@Correlacion", SqlDbType.VarChar, 64).Value = correlacion;
        await connection.OpenAsync(ct);
        try
        {
            return (long)(await command.ExecuteScalarAsync(ct)
                ?? throw new InvalidOperationException("El guardado no devolvió el identificador de la imagen."));
        }
        catch (SqlException ex) when (ex.Number is >= 51076 and <= 51079)
        {
            throw ex.Number switch
            {
                51077 => new AccessFailure(404, "No se encontró el producto o la imagen en ese negocio."),
                51078 => new AccessFailure(403, "El actor o el negocio ya no están habilitados."),
                51079 => new AccessFailure(409, "Hay más de una imagen principal. Marca una para conservarla como principal."),
                _ => new AccessFailure(400, "Revisa la URL y el orden de la imagen.")
            };
        }
    }

    public async Task CambiarEstadoAsync(long idNegocio, long id, ProductoEstadoDatos datos, long actor,
        string correlacion, CancellationToken ct)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await using var command = new SqlCommand("dbo.pm_Catalogo_Productos_CambiarEstado", connection)
        {
            CommandType = CommandType.StoredProcedure, CommandTimeout = 15
        };
        command.Parameters.Add("@IdNegocio", SqlDbType.BigInt).Value = idNegocio;
        command.Parameters.Add("@IdProducto", SqlDbType.BigInt).Value = id;
        command.Parameters.Add("@EstadoActual", SqlDbType.VarChar, 20).Value = datos.EstadoActual;
        command.Parameters.Add("@NuevoEstado", SqlDbType.VarChar, 20).Value = datos.NuevoEstado;
        command.Parameters.Add("@IdActor", SqlDbType.BigInt).Value = actor;
        command.Parameters.Add("@Correlacion", SqlDbType.VarChar, 64).Value = correlacion;

        await connection.OpenAsync(ct);
        try
        {
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException ex) when (ex.Number is >= 51080 and <= 51087)
        {
            throw ex.Number switch
            {
                51081 => new AccessFailure(404, "No se encontró el producto en ese negocio."),
                51082 => new AccessFailure(409, "El estado del producto cambió. Revisa el estado actual e inténtalo de nuevo."),
                51083 => new AccessFailure(409, "La transición solicitada no está permitida."),
                51084 => new AccessFailure(409, "Para publicar, revisa el nombre, la categoría activa, la unidad y el precio."),
                51085 => new AccessFailure(409, "Para publicar, marca una imagen HTTPS como principal."),
                51086 => new AccessFailure(409, "Para publicar un producto que controla stock, debe tener disponibilidad positiva."),
                51087 => new AccessFailure(403, "El actor o el negocio ya no están habilitados."),
                _ => new AccessFailure(400, "Revisa los datos del cambio de estado.")
            };
        }
    }
}
