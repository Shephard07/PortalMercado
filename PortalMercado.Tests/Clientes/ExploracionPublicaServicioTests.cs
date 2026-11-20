using System.Text.Json;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Exploracion;
using PortalMercado.Application.Tiendas;

namespace PortalMercado.Tests.Clientes;

internal sealed class ExploracionRepoPrueba : IRepositorioExploracionPublica
{
    public int Consultas { get; private set; }
    public ExploracionLectura Datos { get; set; } = CrearDatos();
    public Task<ExploracionLectura> LeerAsync(CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); Consultas++; return Task.FromResult(Datos); }
    public static ExploracionLectura CrearDatos() => new(
        [new(1, "la-fruteria", "La Frutería", 10, "Mercado Central", "http://insegura.invalid/logo.png"),
         new(2, "abastos", "Abastos", 20, "Mercado Norte", null)],
        [new(1, new(101, 1, "Frutas", "Mango", "Texto público", 6m, "KG", true, 12m, "http://insegura.invalid/mango.png")),
         new(1, new(102, 1, "Frutas", "Pera", null, 3m, "UND", false, 99m, null)),
         new(2, new(201, 2, "Abarrotes", "Arroz", null, 4.4m, "KG", true, 30m, null))]);
}
public sealed class ExploracionPublicaServicioTests
{
    [Fact]
    public async Task Portada_reutiliza_opciones_publicas_y_no_anuncia_stock_sin_control()
    {
        var repo = new ExploracionRepoPrueba(); var s = new ExploracionPublicaServicio(repo);
        var datos = await s.PortadaAsync(default);
        Assert.Equal(2, datos.Mercados.Length); Assert.Equal(2, datos.Categorias.Length);
        Assert.Equal(2, datos.Tiendas.Length); Assert.Equal(3, datos.Productos.Length);
        Assert.Null(Assert.Single(datos.Productos, p => p.Id == 102).Disponible);
        Assert.Null(Assert.Single(datos.Productos, p => p.Id == 101).Imagen);
    }
    [Theory]
    [InlineData("todos", 7)]
    [InlineData("productos", 3)]
    [InlineData("tiendas", 2)]
    [InlineData("categorias", 2)]
    public async Task Tipos_tienen_totales_coherentes(string tipo, int total)
    {
        var datos = await new ExploracionPublicaServicio(new ExploracionRepoPrueba()).BuscarAsync(new(Tipo: tipo), default);
        Assert.Equal(total, datos.Resultados.Total); Assert.Equal(total, datos.Resultados.Items.Length);
    }
    [Theory]
    [InlineData("fruteria", 3)]
    [InlineData("FRUTERÍA", 3)]
    [InlineData("  Frutas  ", 4)]
    [InlineData("mango", 1)]
    [InlineData("no-existe", 0)]
    public async Task Busca_nombres_tiendas_y_categorias_sin_distinguir_tildes(string q, int total)
    {
        var datos = await new ExploracionPublicaServicio(new ExploracionRepoPrueba()).BuscarAsync(new(q), default);
        Assert.Equal(total, datos.Resultados.Total);
    }
    [Fact]
    public async Task Mercado_y_categoria_se_aplican_antes_del_total_y_la_paginacion()
    {
        var s = new ExploracionPublicaServicio(new ExploracionRepoPrueba());
        var primera = await s.BuscarAsync(new(MercadoId: 10, CategoriaId: 1, Tipo: "productos", Pagina: 1, Tamano: 1), default);
        var segunda = await s.BuscarAsync(primera.Filtro with { Pagina = 2 }, default);
        Assert.Equal(2, primera.Resultados.Total); Assert.Equal(2, segunda.Resultados.Total);
        Assert.Equal(101, Assert.Single(primera.Resultados.Items).Id);
        Assert.Equal(102, Assert.Single(segunda.Resultados.Items).Id);
        Assert.Equal(10L, segunda.Filtro.MercadoId); Assert.Equal(1L, segunda.Filtro.CategoriaId);
        Assert.DoesNotContain(segunda.Categorias, c => c.Id == 2);
    }
    [Fact]
    public async Task Categoria_de_otro_mercado_no_produce_resultados()
    {
        var datos = await new ExploracionPublicaServicio(new ExploracionRepoPrueba()).BuscarAsync(new(MercadoId: 20, CategoriaId: 1), default);
        Assert.Empty(datos.Resultados.Items); Assert.Equal(0, datos.Resultados.Total);
    }
    [Theory]
    [InlineData("precio-asc", 102)]
    [InlineData("precio-desc", 101)]
    public async Task Productos_ordenan_por_precio_antes_de_paginar(string orden, long primerId)
    {
        var datos = await new ExploracionPublicaServicio(new ExploracionRepoPrueba()).BuscarAsync(new(Tipo: "productos", Orden: orden, Tamano: 1), default);
        Assert.Equal(primerId, Assert.Single(datos.Resultados.Items).Id);
        Assert.Equal(3, datos.Resultados.Total);
    }
    [Fact]
    public async Task Pagina_fuera_del_total_esta_vacia_sin_cambiar_el_total()
    {
        var datos = await new ExploracionPublicaServicio(new ExploracionRepoPrueba()).BuscarAsync(new(Tipo: "productos", Pagina: 99), default);
        Assert.Equal(3, datos.Resultados.Total); Assert.Empty(datos.Resultados.Items);
    }
    [Theory]
    [InlineData("mercado")]
    [InlineData("categoria")]
    [InlineData("pagina")]
    [InlineData("tamano")]
    [InlineData("tamano-max")]
    [InlineData("q-largo")]
    [InlineData("q-control")]
    [InlineData("tipo")]
    [InlineData("orden")]
    [InlineData("precio-todos")]
    public async Task Filtros_invalidos_no_consultan_al_proveedor(string caso)
    {
        var repo = new ExploracionRepoPrueba(); var f = new ExploracionFiltro();
        f = caso switch
        {
            "mercado" => f with { MercadoId = 0 }, "categoria" => f with { CategoriaId = -1 },
            "pagina" => f with { Pagina = 0 }, "tamano" => f with { Tamano = 0 },
            "tamano-max" => f with { Tamano = 101 }, "q-largo" => f with { Buscar = new string('x', 201) },
            "q-control" => f with { Buscar = "mango\n" }, "tipo" => f with { Tipo = "usuarios" },
            "orden" => f with { Orden = "sql" }, _ => f with { Orden = "precio-desc" }
        };
        Assert.Equal(400, (await Assert.ThrowsAsync<AccessFailure>(() => new ExploracionPublicaServicio(repo).BuscarAsync(f, default))).Status);
        Assert.Equal(0, repo.Consultas);
    }
    [Theory]
    [InlineData("producto-ajeno")]
    [InlineData("producto-duplicado")]
    [InlineData("tienda-duplicada")]
    [InlineData("slug-externo")]
    [InlineData("slug-duplicado")]
    [InlineData("precio-negativo")]
    [InlineData("categoria-incoherente")]
    public async Task Rechaza_un_contrato_que_mezcla_identidades_o_datos_invalidos(string caso)
    {
        var repo = new ExploracionRepoPrueba(); var d = repo.Datos;
        repo.Datos = caso switch
        {
            "producto-ajeno" => d with { Productos = [d.Productos[0] with { IdNegocio = 999 }] },
            "producto-duplicado" => d with { Productos = [d.Productos[0], d.Productos[0]] },
            "tienda-duplicada" => d with { Tiendas = [d.Tiendas[0], d.Tiendas[0]] },
            "slug-externo" => d with { Tiendas = [d.Tiendas[0] with { Slug = "//externo.invalid" }] },
            "slug-duplicado" => d with { Tiendas = [d.Tiendas[0], d.Tiendas[1] with { Slug = d.Tiendas[0].Slug }] },
            "precio-negativo" => d with { Productos = [d.Productos[0] with { Producto = d.Productos[0].Producto with { Precio = -1m } }] },
            _ => d with { Productos = [d.Productos[0], d.Productos[1] with { Producto = d.Productos[1].Producto with { Categoria = "Otra" } }] }
        };
        Assert.Equal(503, (await Assert.ThrowsAsync<AccessFailure>(() => new ExploracionPublicaServicio(repo).BuscarAsync(new(), default))).Status);
    }
    [Fact]
    public async Task Destinos_son_locales_y_la_respuesta_no_lleva_contactos_ni_ids_de_cliente()
    {
        var repo = new ExploracionRepoPrueba();
        repo.Datos.Productos[0] = repo.Datos.Productos[0] with { Producto = repo.Datos.Productos[0].Producto with { Nombre = "Mango & pera" } };
        var resultado = await new ExploracionPublicaServicio(repo).BuscarAsync(new(), default);
        Assert.All(resultado.Resultados.Items, x => Assert.StartsWith("/", x.Destino));
        Assert.Contains("Mango%20%26%20pera", Assert.Single(resultado.Resultados.Items, x => x.Id == 101).Destino);
        var json = JsonSerializer.Serialize(resultado, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var privado in new[] { "password", "correo", "telefono", "ruc", "direccionFiscal", "idCliente", "idUsuario" })
            Assert.DoesNotContain(privado, json, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Portada_limita_solo_la_muestra_no_la_busqueda()
    {
        var repo = new ExploracionRepoPrueba();
        repo.Datos = repo.Datos with { Productos = Enumerable.Range(1, 15).Select(i => new ProductoExploracion(1,
            repo.Datos.Productos[0].Producto with { Id = i, Nombre = "Producto " + i.ToString("D2") })).ToArray() };
        var servicio = new ExploracionPublicaServicio(repo);
        Assert.Equal(8, (await servicio.PortadaAsync(default)).Productos.Length);
        Assert.Equal(15, (await servicio.BuscarAsync(new(Tipo: "productos"), default)).Resultados.Total);
    }
}
