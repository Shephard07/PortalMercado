using System.Text.Json;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Clientes;

namespace PortalMercado.Tests.Clientes;

public sealed class ClientesValidacionTests
{
    [Fact]
    public void Perfil_permite_borrar_opcionales_sin_cambiar_la_identidad()
    {
        var limpio = ClientesValidacion.Perfil(new() { Apellidos = " ", Celular = " ", TipoDocumento = " ", NumeroDocumento = " " });
        Assert.Null(limpio.Apellidos); Assert.Null(limpio.Celular);
        Assert.Null(limpio.TipoDocumento); Assert.Null(limpio.NumeroDocumento);
    }
    [Theory]
    [InlineData("apellidos-largos")]
    [InlineData("celular-largo")]
    [InlineData("tipo-largo")]
    [InlineData("numero-largo")]
    [InlineData("solo-tipo")]
    [InlineData("solo-numero")]
    [InlineData("celular-control")]
    [InlineData("documento-unicode")]
    public void Perfil_rechaza_capacidades_y_documento_incompleto(string caso)
    {
        var d = new ClientePerfilDatos();
        switch (caso)
        {
            case "apellidos-largos": d.Apellidos = new('x', 151); break;
            case "celular-largo": d.Celular = new('1', 31); break;
            case "tipo-largo": d.TipoDocumento = "ABCDE"; d.NumeroDocumento = "1"; break;
            case "numero-largo": d.TipoDocumento = "OTRO"; d.NumeroDocumento = new('1', 21); break;
            case "solo-tipo": d.TipoDocumento = "DNI"; break;
            case "solo-numero": d.NumeroDocumento = "12345678"; break;
            case "celular-control": d.Celular = "99\n99"; break;
            case "documento-unicode": d.TipoDocumento = "OTRO"; d.NumeroDocumento = "ñ123"; break;
        }
        Assert.Equal(400, Assert.Throws<AccessFailure>(() => ClientesValidacion.Perfil(d)).Status);
    }
    [Fact]
    public void Direccion_limpia_textos_y_conserva_coordenadas_sin_mutar_entrada()
    {
        var entrada = ClientesPrueba.Direccion(); var salida = ClientesValidacion.Direccion(entrada);
        Assert.Equal("Calle de prueba 123", salida.Direccion); Assert.Equal("Casa", salida.Etiqueta);
        Assert.Equal("Lima", salida.Provincia); Assert.Equal(entrada.Latitud, salida.Latitud);
        Assert.Equal(" Calle de prueba 123 ", entrada.Direccion);
    }
    [Fact]
    public void Direccion_admite_coordenadas_ausentes_y_campos_ubicacion_nulos_del_esquema()
    {
        var salida = ClientesValidacion.Direccion(new() { Direccion = "Dirección de prueba" });
        Assert.Null(salida.Latitud); Assert.Null(salida.Longitud); Assert.Null(salida.Distrito);
    }
    [Theory]
    [InlineData("vacia")]
    [InlineData("espacios")]
    [InlineData("direccion-larga")]
    [InlineData("etiqueta-larga")]
    [InlineData("referencia-larga")]
    [InlineData("distrito-largo")]
    [InlineData("provincia-larga")]
    [InlineData("departamento-largo")]
    [InlineData("latitud")]
    [InlineData("longitud")]
    [InlineData("sin-longitud")]
    [InlineData("sin-latitud")]
    [InlineData("precision")]
    [InlineData("control")]
    public void Direccion_rechaza_datos_fuera_del_contrato(string caso)
    {
        var d = ClientesPrueba.Direccion();
        switch (caso)
        {
            case "vacia": d.Direccion = ""; break;
            case "espacios": d.Direccion = " "; break;
            case "direccion-larga": d.Direccion = new('x', 301); break;
            case "etiqueta-larga": d.Etiqueta = new('x', 81); break;
            case "referencia-larga": d.Referencia = new('x', 251); break;
            case "distrito-largo": d.Distrito = new('x', 121); break;
            case "provincia-larga": d.Provincia = new('x', 121); break;
            case "departamento-largo": d.Departamento = new('x', 121); break;
            case "latitud": d.Latitud = 90.1m; break;
            case "longitud": d.Longitud = -180.1m; break;
            case "sin-longitud": d.Longitud = null; break;
            case "sin-latitud": d.Latitud = null; break;
            case "precision": d.Latitud = -11.12345678m; break;
            case "control": d.Referencia = "Puerta\u0000azul"; break;
        }
        Assert.Equal(400, Assert.Throws<AccessFailure>(() => ClientesValidacion.Direccion(d)).Status);
    }
    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(0, 0)]
    public void Coordenadas_limite_son_validas(int latitud, int longitud)
    {
        var d = ClientesPrueba.Direccion(); d.Latitud = latitud; d.Longitud = longitud;
        Assert.Equal(d.Latitud, ClientesValidacion.Direccion(d).Latitud);
    }
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("2001:db8::1")]
    [InlineData(null)]
    public void Contexto_usa_fecha_servidor_y_ip_literal(string? ip)
    {
        var operacion = ClientesValidacion.Operacion(7, new("traza-01", ip), new RelojClientes());
        Assert.Equal(RelojClientes.Fecha, operacion.FechaUtc); Assert.Equal(ip, operacion.Ip);
    }
    [Theory]
    [InlineData("", null)]
    [InlineData("linea\n2", null)]
    [InlineData("traza", "1.2.3.4, 5.6.7.8")]
    [InlineData("traza", "no-es-ip")]
    [InlineData("traza", "fe80::1%12")]
    public void Contexto_rechaza_metadatos_que_no_deben_llegar_a_auditoria(string traza, string? ip)
    {
        Assert.Throws<ArgumentException>(() => ClientesValidacion.Operacion(7, new(traza, ip), new RelojClientes()));
    }
    [Theory]
    [InlineData("registro", "{\"IdUsuario\":99}")]
    [InlineData("registro", "{\"Rol\":\"ADMIN_GENERAL\"}")]
    [InlineData("registro", "{\"EmailConfirmado\":true}")]
    [InlineData("perfil", "{\"Correo\":\"otro@example.invalid\"}")]
    [InlineData("perfil", "{\"IdCliente\":99}")]
    [InlineData("direccion", "{\"IdCliente\":99}")]
    [InlineData("direccion", "{\"Principal\":true}")]
    [InlineData("consentimiento", "{\"IdCliente\":99,\"Aceptado\":true}")]
    [InlineData("consentimiento", "{\"Tipo\":\"PROMOCIONES\"}")]
    public void Dto_no_acepta_propiedad_identidad_o_campos_protegidos(string contrato, string json)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() =>
        {
            switch (contrato)
            {
                case "registro": JsonSerializer.Deserialize<RegistroClienteSolicitud>(json, options); break;
                case "perfil": JsonSerializer.Deserialize<ClientePerfilDatos>(json, options); break;
                case "direccion": JsonSerializer.Deserialize<ClienteDireccionDatos>(json, options); break;
                case "consentimiento": JsonSerializer.Deserialize<ClienteConsentimientoDatos>(json, options); break;
            }
        });
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://externo.invalid/tiendas")]
    [InlineData("//externo.invalid")]
    [InlineData("/\\externo.invalid")]
    [InlineData("/%2fexterno.invalid")]
    [InlineData("/%252fexterno.invalid")]
    [InlineData("/%5cexterno.invalid")]
    [InlineData("/%255cexterno.invalid")]
    [InlineData("/tiendas%0d%0aLocation:externo")]
    [InlineData("/tiendas\n")]
    [InlineData("javascript:alert(1)")]
    public void Destino_ajeno_o_codificado_no_se_conserva(string? url)
    {
        Assert.Equal(DestinoCliente.Predeterminado, DestinoCliente.Normalizar(url));
    }
    [Theory]
    [InlineData("/")]
    [InlineData("/tiendas/negocio-prueba-et01")]
    [InlineData("/tiendas/negocio-prueba-et01?q=tomate&pagina=2")]
    [InlineData("/explorar?q=fruta%20fresca")]
    public void Conserva_destino_local_con_filtros(string url)
    {
        Assert.Equal(url, DestinoCliente.Normalizar(url));
    }
}
