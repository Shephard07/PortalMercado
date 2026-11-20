using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Infrastructure.Tiendas;

// Adaptación local autorizada para desarrollo individual. No es el adaptador central del DBA.
public sealed class RepositorioTiendasSqlLocal(TiendasSqlLocalOpciones opciones) :
    IRepositorioTiendas, IRepositorioCatalogoPublico, IRepositorioUbicacionesTienda, IRepositorioPerfilTienda
{
    private async Task<T> Ejecutar<T>(string procedimiento, Action<SqlParameterCollection>? parametros,
        Func<SqlDataReader, CancellationToken, Task<T>> leer, CancellationToken ct)
    {
        try
        {
            await using var connection = new SqlConnection(opciones.Conexion);
            await connection.OpenAsync(ct);
            // Validar también la instancia real que atendió la conexión, no solo su cadena.
            await using (var guardia = new SqlCommand("""
                SELECT CASE WHEN DB_NAME()=N'PortalMercado_ET01_Test'
                 AND CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))=N'SQLEXPRESS'
                 AND CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=HOST_NAME() THEN 1 ELSE 0 END;
                """, connection) { CommandTimeout = 15 })
            {
                if (Convert.ToInt32(await guardia.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) != 1)
                    throw new AccessFailure(503, "La conexión de tiendas locales no está disponible.");
            }
            await using var command = new SqlCommand(procedimiento, connection)
                { CommandType = CommandType.StoredProcedure, CommandTimeout = 15 };
            parametros?.Invoke(command.Parameters);
            await using var reader = await command.ExecuteReaderAsync(ct);
            return await leer(reader, ct);
        }
        catch (SqlException error) when (!ct.IsCancellationRequested)
        {
            throw error.Number switch
            {
                51104 => new AccessFailure(400, "Revisa los campos y los límites del perfil o los filtros."),
                51143 => new AccessFailure(403, "No tienes acceso al perfil de ese negocio."),
                51144 => new AccessFailure(404, "No se encontró la tienda o el perfil solicitado."),
                51149 => new AccessFailure(409, "El perfil cambió en otra ventana. Recarga antes de guardar."),
                _ => new AccessFailure(503, "Las tiendas locales no están disponibles en este momento.")
            };
        }
        catch (Exception error) when (error is InvalidCastException or IndexOutOfRangeException or FormatException or OverflowException or System.Data.SqlTypes.SqlNullValueException)
        {
            throw new AccessFailure(503, "El contrato SQL local no coincide con la instalación esperada.");
        }
    }

    public Task<TiendaOpciones> OpcionesPublicasAsync(CancellationToken ct) =>
        Ejecutar("dbo.pm_TiendaLocal_Opciones", null, async (reader, token) =>
        {
            var mercados = await Nombres(reader, token);
            await Siguiente(reader, token);
            return new TiendaOpciones(mercados, await Nombres(reader, token));
        }, ct);

    public Task<Page<TiendaLectura>> ListarPublicasAsync(TiendaFiltro filtro, CancellationToken ct) =>
        Ejecutar("dbo.pm_TiendaLocal_Listar", p =>
        {
            Param(p, "@MercadoId", SqlDbType.BigInt, filtro.MercadoId);
            Param(p, "@CategoriaId", SqlDbType.BigInt, filtro.CategoriaId);
            Param(p, "@Buscar", SqlDbType.NVarChar, filtro.Buscar, 180);
            Param(p, "@Pagina", SqlDbType.Int, filtro.Pagina);
            Param(p, "@Tamano", SqlDbType.Int, filtro.Tamano);
        }, async (reader, token) =>
        {
            var total = await Total(reader, token);
            await Siguiente(reader, token);
            return new Page<TiendaLectura>(await Tiendas(reader, token), total, filtro.Pagina, filtro.Tamano);
        }, ct);

    public Task<TiendaLectura[]> BuscarPorSlugAsync(string slug, CancellationToken ct) =>
        Ejecutar("dbo.pm_TiendaLocal_Buscar", p => Param(p, "@Slug", SqlDbType.VarChar, slug, 160), Tiendas, ct);

    public Task<UbicacionTiendaLectura[]> ConsultarAsync(long idNegocio, DateOnly fecha, CancellationToken ct) =>
        Ejecutar("dbo.pm_TiendaLocal_Ubicaciones", p =>
        {
            Param(p, "@IdNegocio", SqlDbType.BigInt, idNegocio);
            Param(p, "@Fecha", SqlDbType.Date, fecha.ToDateTime(TimeOnly.MinValue));
        }, async (reader, token) =>
        {
            var filas = new List<UbicacionTiendaLectura>();
            while (await reader.ReadAsync(token))
                filas.Add(new(I64(reader,"IdNegocio"), I64(reader,"IdMercado"), I64(reader,"IdPuesto"),
                    Texto(reader,"Mercado"), Texto(reader,"Sector"), Texto(reader,"Puesto"), Opcional(reader,"Referencia"),
                    Bit(reader,"MercadoActivo"), Bit(reader,"SectorActivo"), Bit(reader,"PuestoHabilitado"),
                    DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("Desde"))),
                    reader.IsDBNull(reader.GetOrdinal("Hasta")) ? null : DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("Hasta")))));
            return filas.ToArray();
        }, ct);

    public Task<CatalogoTiendaLectura> ConsultarAsync(long idNegocio, ProductoTiendaFiltro filtro, CancellationToken ct) =>
        Ejecutar("dbo.pm_TiendaLocal_Catalogo", p =>
        {
            Param(p,"@IdNegocio",SqlDbType.BigInt,idNegocio);
            Param(p,"@CategoriaId",SqlDbType.BigInt,filtro.CategoriaId);
            Param(p,"@Buscar",SqlDbType.NVarChar,filtro.Buscar,200);
            Param(p,"@Orden",SqlDbType.VarChar,filtro.Orden,20);
            Param(p,"@Pagina",SqlDbType.Int,filtro.Pagina);
            Param(p,"@Tamano",SqlDbType.Int,filtro.Tamano);
        }, async (reader, token) =>
        {
            var total = await Total(reader, token);
            await Siguiente(reader, token);
            var filas = new List<ProductoTiendaLectura>();
            while (await reader.ReadAsync(token))
                filas.Add(new(I64(reader,"IdNegocio"), Texto(reader,"Estado"),
                    new(I64(reader,"IdProducto"), I64(reader,"IdCategoria"), Texto(reader,"Categoria"),
                        Texto(reader,"Nombre"), Opcional(reader,"Descripcion"), reader.GetDecimal(reader.GetOrdinal("Precio")),
                        Texto(reader,"Unidad"), Bit(reader,"ControlaStock"),
                        reader.IsDBNull(reader.GetOrdinal("Disponible")) ? null : reader.GetDecimal(reader.GetOrdinal("Disponible")),
                        Opcional(reader,"ImagenPrincipal"))));
            await Siguiente(reader, token);
            return new CatalogoTiendaLectura(new(filas.ToArray(),total,filtro.Pagina,filtro.Tamano), await Nombres(reader,token));
        }, ct);

    public Task<PerfilTienda?> ObtenerAsync(long idNegocio, CancellationToken ct) =>
        Ejecutar<PerfilTienda?>("dbo.pm_TiendaLocal_Perfil", p => Param(p,"@IdNegocio",SqlDbType.BigInt,idNegocio),
        async (reader, token) =>
        {
            if (!await reader.ReadAsync(token)) return null;
            AuditoriaPerfilTienda? cambio = reader.IsDBNull(reader.GetOrdinal("UltimoActor")) ? null :
                new(I64(reader,"UltimoActor"), I64(reader,"IdNegocio"),
                    new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal("UltimoCambioUtc")),DateTimeKind.Utc)),
                    Texto(reader,"UltimaCorrelacion"));
            return new(I64(reader,"IdNegocio"), Texto(reader,"NombreComercial"), Texto(reader,"Slug"),
                Bit(reader,"TelefonoPublico"),Bit(reader,"CorreoPublico"), new PerfilTiendaDatos
                {
                    Version = reader.GetGuid(reader.GetOrdinal("Version")), Descripcion = Opcional(reader,"Descripcion"),
                    LogoUrl = Opcional(reader,"LogoUrl"), Horario = Opcional(reader,"Horario"),
                    Telefono = Opcional(reader,"Telefono"), Correo = Opcional(reader,"Correo")
                },cambio);
        },ct);

    public async Task GuardarAsync(long idNegocio, PerfilTiendaDatos datos, AuditoriaPerfilTienda auditoria, CancellationToken ct)
    {
        await Ejecutar("dbo.pm_TiendaLocal_Guardar", p =>
        {
            Param(p,"@IdNegocio",SqlDbType.BigInt,idNegocio);
            Param(p,"@IdActor",SqlDbType.BigInt,auditoria.Actor);
            Param(p,"@Version",SqlDbType.UniqueIdentifier,datos.Version);
            // MAX evita truncar antes de que SQL pueda validar límites.
            Param(p,"@Descripcion",SqlDbType.NVarChar,datos.Descripcion,-1);
            Param(p,"@LogoUrl",SqlDbType.NVarChar,datos.LogoUrl,-1);
            Param(p,"@Horario",SqlDbType.NVarChar,datos.Horario,-1);
            Param(p,"@Telefono",SqlDbType.NVarChar,datos.Telefono,-1);
            Param(p,"@Correo",SqlDbType.NVarChar,datos.Correo,-1);
            Param(p,"@Correlacion",SqlDbType.VarChar,auditoria.Correlacion,100);
        }, (_, _) => Task.FromResult(true),ct);
    }

    private static void Param(SqlParameterCollection parametros,string nombre,SqlDbType tipo,object? valor,int? tamano=null)
    {
        var parametro = tamano.HasValue ? parametros.Add(nombre,tipo,tamano.Value) : parametros.Add(nombre,tipo);
        parametro.Value = valor ?? DBNull.Value;
    }
    private static string Texto(SqlDataReader r,string nombre) => r.GetString(r.GetOrdinal(nombre));
    private static string? Opcional(SqlDataReader r,string nombre) => r.IsDBNull(r.GetOrdinal(nombre)) ? null : Texto(r,nombre);
    private static long I64(SqlDataReader r,string nombre) => r.GetInt64(r.GetOrdinal(nombre));
    private static bool Bit(SqlDataReader r,string nombre) => r.GetBoolean(r.GetOrdinal(nombre));
    private static async Task Siguiente(SqlDataReader r,CancellationToken ct)
    {
        if (!await r.NextResultAsync(ct)) throw new AccessFailure(503,"El contrato SQL local está incompleto.");
    }
    private static async Task<int> Total(SqlDataReader r,CancellationToken ct)
    {
        if (!await r.ReadAsync(ct)) throw new AccessFailure(503,"El contrato SQL local está incompleto.");
        return r.GetInt32(r.GetOrdinal("Total"));
    }
    private static async Task<NamedId[]> Nombres(SqlDataReader r,CancellationToken ct)
    {
        var filas = new List<NamedId>();
        while (await r.ReadAsync(ct)) filas.Add(new(I64(r,"Id"),Texto(r,"Nombre")));
        return filas.ToArray();
    }
    private static async Task<TiendaLectura[]> Tiendas(SqlDataReader r,CancellationToken ct)
    {
        var filas = new List<TiendaLectura>();
        while (await r.ReadAsync(ct))
            filas.Add(new(I64(r,"IdNegocio"),Texto(r,"Slug"),Texto(r,"NombreComercial"),I64(r,"IdMercado"),Texto(r,"Mercado"),
                Texto(r,"Estado"),Bit(r,"MercadoActivo"),Bit(r,"VisibilidadAprobada"),Opcional(r,"Descripcion"),Opcional(r,"LogoUrl"),
                Opcional(r,"Horario"),Opcional(r,"Telefono"),Bit(r,"TelefonoPublico"),Opcional(r,"Correo"),Bit(r,"CorreoPublico"),
                Texto(r,"Categorias").Split(',',StringSplitOptions.RemoveEmptyEntries).Select(x => long.Parse(x,CultureInfo.InvariantCulture)).ToArray()));
        return filas.ToArray();
    }
}
