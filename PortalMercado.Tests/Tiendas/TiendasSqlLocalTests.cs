using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PortalMercado.Domain.Entities;
using PortalMercado.Infrastructure.Persistence;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;
using PortalMercado.Infrastructure.Tiendas;

namespace PortalMercado.Tests.Tiendas;

// Ejecutar tras los scripts 01 y 02 locales. Los casos de esta clase son secuenciales.
public sealed class TiendasSqlLocalTests
{
    private const string Conexion = @"Server=.\SQLEXPRESS;Database=PortalMercado_ET01_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5;";
    private static RepositorioTiendasSqlLocal Repo() => new(TiendasSqlLocalOpciones.Crear(Conexion));

    [TiendasSqlLocalFact]
    public async Task Directorio_y_catalogo_consultan_los_registros_reales_de_la_copia_local()
    {
        var f = await Datos();
        var servicio = new TiendaServicio(Repo(),Repo());
        var tiendas = await servicio.ListarAsync(new(Buscar:f.Nombre),default);
        Assert.Contains(tiendas.Items,t=>t.IdNegocio==f.Id);
        var catalogo = await new CatalogoTiendaServicio(servicio,Repo()).ConsultarAsync(f.Slug,new(Buscar:"Mango de tienda local"),default);
        Assert.Equal(f.Id,catalogo.Tienda.IdNegocio);
        Assert.NotEmpty(catalogo.Productos.Items);
        Assert.All(catalogo.Productos.Items,p=>Assert.Equal("Mango de tienda local",p.Nombre));
        Assert.DoesNotContain("fruteria-el-sol",catalogo.Tienda.Slug);
    }

    [TiendasSqlLocalFact]
    public async Task Guardado_persiste_en_otra_instancia_y_audita_sin_copiar_contactos()
    {
        var f = await Datos(); var original = (await Repo().ObtenerAsync(f.Id,default))!;
        var traza = Guid.NewGuid().ToString("N");
        try
        {
            var datos = Copiar(original.Datos);
            datos.Descripcion = "Cambio persistente " + traza;
            datos.Telefono = "987654321"; datos.Correo="privado@example.invalid";
            await Repo().GuardarAsync(f.Id,datos,new(f.Actor,f.Id,DateTimeOffset.UtcNow,traza),default);
            var despues = (await Repo().ObtenerAsync(f.Id,default))!;
            Assert.Equal(datos.Descripcion,despues.Datos.Descripcion);
            Assert.NotEqual(original.Datos.Version,despues.Datos.Version);
            Assert.Equal(traza,despues.UltimoCambio!.Correlacion);
            var auditoria = await Escalar<string>("SELECT Detalle FROM dbo.Auditoria WHERE Accion='TIENDA_PERFIL_ACTUALIZADO' AND Detalle=@traza;",("@traza","Correlacion="+traza));
            Assert.Equal("Correlacion="+traza,auditoria);
            Assert.DoesNotContain(datos.Telefono,auditoria);
            Assert.DoesNotContain(datos.Correo,auditoria);
            var publico = await new TiendaServicio(Repo()).ObtenerAsync(f.Slug,default);
            Assert.Null(publico.Telefono); Assert.Null(publico.Correo);
        }
        finally { await Restaurar(f,original.Datos); }
    }

    [TiendasSqlLocalFact]
    public async Task Version_anterior_no_sobrescribe_ni_agrega_auditoria_de_exito()
    {
        var f = await Datos(); var original=(await Repo().ObtenerAsync(f.Id,default))!;
        var traza=Guid.NewGuid().ToString("N");
        try
        {
            await Repo().GuardarAsync(f.Id,original.Datos,new(f.Actor,f.Id,DateTimeOffset.UtcNow,traza),default);
            var valido=(await Repo().ObtenerAsync(f.Id,default))!;
            var error=await Assert.ThrowsAsync<AccessFailure>(()=>Repo().GuardarAsync(f.Id,original.Datos,new(f.Actor,f.Id,DateTimeOffset.UtcNow,traza+"-vieja"),default));
            Assert.Equal(409,error.Status);
            Assert.Equal(valido.Datos.Version,(await Repo().ObtenerAsync(f.Id,default))!.Datos.Version);
            Assert.Equal(0,await CuentaAuditoria(traza+"-vieja"));
        }
        finally { await Restaurar(f,original.Datos); }
    }

    [TiendasSqlLocalFact]
    public async Task Escritura_directa_sin_actor_autorizado_es_rechazada_por_sql()
    {
        var f=await Datos(); var p=(await Repo().ObtenerAsync(f.Id,default))!;
        var traza=Guid.NewGuid().ToString("N");
        var error=await Assert.ThrowsAsync<AccessFailure>(()=>Repo().GuardarAsync(f.Id,p.Datos,new(long.MaxValue,f.Id,DateTimeOffset.UtcNow,traza),default));
        Assert.Equal(403,error.Status);
        Assert.Equal(p.Datos.Version,(await Repo().ObtenerAsync(f.Id,default))!.Datos.Version);
        Assert.Equal(0,await CuentaAuditoria(traza));
    }

    [TiendasSqlLocalFact]
    public async Task Vendedor_no_puede_guardar_el_negocio_ajeno_aunque_conozca_su_version()
    {
        var f=await Datos();
        var ajeno=await Escalar<long>("SELECT IdNegocio FROM dbo.Negocio WHERE Slug='negocio-prueba-catalogo-ajeno';");
        var p=(await Repo().ObtenerAsync(ajeno,default))!;
        Assert.NotNull(p);
        var error=await Assert.ThrowsAsync<AccessFailure>(()=>Repo().GuardarAsync(ajeno,p.Datos,new(f.Actor,ajeno,DateTimeOffset.UtcNow,Guid.NewGuid().ToString("N")),default));
        Assert.Equal(403,error.Status);
        Assert.Equal(p.Datos.Version,(await Repo().ObtenerAsync(ajeno,default))!.Datos.Version);
    }

    [TiendasSqlLocalFact]
    public async Task Actualizacion_de_negocio_por_otro_escritor_invalida_la_version()
    {
        var f=await Datos();var antes=(await Repo().ObtenerAsync(f.Id,default))!;
        await Ejecutar("UPDATE dbo.Negocio SET Descripcion=Descripcion WHERE IdNegocio=@id;",("@id",f.Id));
        Assert.NotEqual(antes.Datos.Version,(await Repo().ObtenerAsync(f.Id,default))!.Datos.Version);
        var error=await Assert.ThrowsAsync<AccessFailure>(()=>Repo().GuardarAsync(f.Id,antes.Datos,new(f.Actor,f.Id,DateTimeOffset.UtcNow,Guid.NewGuid().ToString("N")),default));
        Assert.Equal(409,error.Status);
    }

    [TiendasSqlLocalFact]
    public async Task Error_de_auditoria_revierte_el_perfil_y_su_version()
    {
        var f=await Datos();var antes=(await Repo().ObtenerAsync(f.Id,default))!;
        var traza="rollback-"+Guid.NewGuid().ToString("N");
        await using var c=new SqlConnection(Conexion);await c.OpenAsync();
        await using (var tx=(SqlTransaction)await c.BeginTransactionAsync())
        {
            try
            {
                // Trigger de fallo solo dentro de esta transacción de prueba; se revierte al terminar.
                await using var preparar=new SqlCommand("""
                    EXEC sys.sp_executesql N'CREATE OR ALTER TRIGGER dbo.tr_TiendaLocal_PruebaRollback ON dbo.Auditoria AFTER INSERT AS
                    BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Accion=''TIENDA_PERFIL_ACTUALIZADO'') THROW 51198,''Fallo ficticio de auditoria.'',1; END';
                    """,c,tx);
                await preparar.ExecuteNonQueryAsync();
                await using var comando=new SqlCommand("dbo.pm_TiendaLocal_Guardar",c,tx){CommandType=CommandType.StoredProcedure};
                comando.Parameters.AddWithValue("@IdNegocio",f.Id);comando.Parameters.AddWithValue("@IdActor",f.Actor);
                comando.Parameters.AddWithValue("@Version",antes.Datos.Version);
                comando.Parameters.AddWithValue("@Descripcion","No debe persistir");
                comando.Parameters.AddWithValue("@Correlacion",traza);
                var error=await Assert.ThrowsAsync<SqlException>(()=>comando.ExecuteNonQueryAsync());
                Assert.Contains(error.Errors.Cast<SqlError>(),e=>e.Number==51198);
            }
            finally { if(tx.Connection is not null) await tx.RollbackAsync(); }
        }
        var despues=(await Repo().ObtenerAsync(f.Id,default))!;
        Assert.Equal(antes.Datos.Version,despues.Datos.Version);
        Assert.Equal(antes.Datos.Descripcion,despues.Datos.Descripcion);
        Assert.Equal(0,await CuentaAuditoria(traza));
    }

    [TiendasSqlLocalFact]
    public async Task Directorio_no_interpreta_comodines_como_sql_y_preserva_total_en_pagina_vacia()
    {
        var f=await Datos();var repo=Repo();
        var primera=await repo.ListarPublicasAsync(new(Buscar:f.Nombre,Tamano:1),default);
        var vacia=await repo.ListarPublicasAsync(new(Buscar:f.Nombre,Pagina:100000,Tamano:1),default);
        Assert.True(primera.Total>=1);Assert.Equal(primera.Total,vacia.Total);Assert.Empty(vacia.Items);
        Assert.Empty((await repo.ListarPublicasAsync(new(Buscar:"%_';--"),default)).Items);
    }

    [TiendasSqlLocalFact]
    public async Task Negocio_nuevo_se_crea_oculto_y_slug_ambiguo_no_se_publica()
    {
        var f=await Datos();
        await using var c=new SqlConnection(Conexion);await c.OpenAsync();
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync();
        try
        {
            await using var crearMercado=new SqlCommand("""
                INSERT dbo.Mercado(Nombre,Activo,CreadoEn) VALUES(N'Mercado transaccional de prueba tiendas',1,SYSUTCDATETIME());
                SELECT CONVERT(bigint,SCOPE_IDENTITY());
                """,c,tx);
            var mercado=Convert.ToInt64(await crearMercado.ExecuteScalarAsync());
            await using var db=new PortalMercadoDbContext(new DbContextOptionsBuilder<PortalMercadoDbContext>().UseSqlServer(c).Options);
            await db.Database.UseTransactionAsync(tx);
            var propio=await db.Negocios.AsNoTracking().SingleAsync(n=>n.IdNegocio==f.Id);
            var nuevo=new Negocio
            {
                IdMercado=mercado,IdComerciante=propio.IdComerciante,
                NombreComercial="Tienda duplicada transaccional",Slug=propio.Slug,Estado="ACTIVO"
            };
            db.Negocios.Add(nuevo);
            // Regresión: guardar mediante EF con el trigger instalado debe recuperar ID y fecha.
            await db.SaveChangesAsync();
            Assert.True(nuevo.IdNegocio>0);
            Assert.NotEqual(default(DateTime),nuevo.CreadoEn);
            await using var cmd=new SqlCommand("""
                SELECT CONVERT(int,Visible) FROM dbo.TiendaPublicacionLocal WHERE IdNegocio=@nuevo;
                SELECT COUNT(*) FROM dbo.pm_TiendaLocal_Visibles WHERE IdNegocio IN(@id,@nuevo);
                """,c,tx);
            cmd.Parameters.AddWithValue("@id",f.Id);
            cmd.Parameters.AddWithValue("@nuevo",nuevo.IdNegocio);
            await using var reader=await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());Assert.Equal(0,reader.GetInt32(0));
            Assert.True(await reader.NextResultAsync());Assert.True(await reader.ReadAsync());Assert.Equal(0,reader.GetInt32(0));
        }
        finally { await tx.RollbackAsync(); }
        Assert.NotNull(await new TiendaServicio(Repo()).ObtenerAsync(f.Slug,default));
    }

    private sealed record Fixture(long Id,long Actor,string Nombre,string Slug);
    private static async Task<Fixture> Datos()
    {
        await using var c=new SqlConnection(Conexion);await c.OpenAsync();
        await using var cmd=new SqlCommand("""
            SELECT n.IdNegocio,u.IdUsuario,n.NombreComercial,n.Slug
            FROM dbo.Negocio n JOIN dbo.Mercado m ON m.IdMercado=n.IdMercado
            JOIN dbo.TiendaPublicacionLocal t ON t.IdNegocio=n.IdNegocio CROSS JOIN dbo.Usuario u
            WHERE n.Slug='negocio-prueba-et01' AND m.Nombre=N'Mercado de prueba ET01'
             AND u.Correo='vendedor.et02@example.invalid' AND dbo.pm_TiendaLocal_PuedeEditar(u.IdUsuario,n.IdNegocio)=1
             AND t.Visible=1 AND t.TelefonoPublico=0 AND t.CorreoPublico=0;
            """,c);
        await using var reader=await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(),"Instala los scripts 01 y 02; se requiere vendedor local autorizado y contactos privados.");
        return new(reader.GetInt64(0),reader.GetInt64(1),reader.GetString(2),reader.GetString(3));
    }
    private static PerfilTiendaDatos Copiar(PerfilTiendaDatos d)=>new(){Version=d.Version,Descripcion=d.Descripcion,LogoUrl=d.LogoUrl,Horario=d.Horario,Telefono=d.Telefono,Correo=d.Correo};
    private static async Task Restaurar(Fixture f,PerfilTiendaDatos original)
    {
        var actual=(await Repo().ObtenerAsync(f.Id,default))!;
        var datos=Copiar(original);datos.Version=actual.Datos.Version;
        await Repo().GuardarAsync(f.Id,datos,new(f.Actor,f.Id,DateTimeOffset.UtcNow,"restaurar-"+Guid.NewGuid().ToString("N")),default);
    }
    private static Task<int> CuentaAuditoria(string traza)=>Escalar<int>("SELECT COUNT(*) FROM dbo.Auditoria WHERE Accion='TIENDA_PERFIL_ACTUALIZADO' AND Detalle=@traza;",("@traza","Correlacion="+traza));
    private static async Task<T> Escalar<T>(string sql,params (string Nombre,object Valor)[] parametros)
    {
        await using var c=new SqlConnection(Conexion);await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);
        foreach(var p in parametros)cmd.Parameters.AddWithValue(p.Nombre,p.Valor);
        var valor=await cmd.ExecuteScalarAsync();Assert.NotNull(valor);Assert.NotEqual(DBNull.Value,valor);
        return (T)valor!;
    }
    private static async Task Ejecutar(string sql,params (string Nombre,object Valor)[] parametros)
    {
        await using var c=new SqlConnection(Conexion);await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);
        foreach(var p in parametros)cmd.Parameters.AddWithValue(p.Nombre,p.Valor);
        await cmd.ExecuteNonQueryAsync();
    }
}

public sealed class TiendasSqlLocalFactAttribute : FactAttribute
{
    public TiendasSqlLocalFactAttribute()
    {
        if(Environment.GetEnvironmentVariable("PM_RUN_TIENDAS_SQL_TESTS")!="1")
            Skip="Requiere PM_RUN_TIENDAS_SQL_TESTS=1 y scripts database/ciclo2-local 01 y 02 en SQL local.";
    }
}
