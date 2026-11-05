using PortalMercado.Application.Security;
using PortalMercado.Infrastructure.Security;

namespace PortalMercado.Tests;

public sealed class BootstrapDiagnosticsTests
{
    [Fact]
    public void PasswordInsuficienteSeIdentificaSinMostrarla()
    {
        const string password = "Clave7!";
        var ex = Assert.Throws<BootstrapFailureException>(() => BootstrapValidation.Validate(
            "vendedor.et02@example.invalid", "Vendedor ficticio", password, true));
        var message = BootstrapDiagnostics.Describe(ex);
        Assert.Contains("PASSWORD_INVALIDA", message);
        Assert.DoesNotContain(password, message);
    }
    [Fact]
    public void PasswordValidaNoProduceError() => BootstrapValidation.Validate(
        "vendedor.et02@example.invalid", "Vendedor ficticio", "EjemploDePrueba123!", true);
    [Fact]
    public void ExcepcionDesconocidaNoRevelaContenidoNiInnerException()
    {
        var ex = new InvalidOperationException("Password=MARCADOR_PRIVADO", new Exception("Server=SERVIDOR_PRIVADO"));
        var message = BootstrapDiagnostics.Describe(ex);
        Assert.Contains("TIPO:InvalidOperationException", message);
        Assert.DoesNotContain("MARCADOR_PRIVADO", message);
        Assert.DoesNotContain("SERVIDOR_PRIVADO", message);
    }
    [Fact]
    public void FallaConocidaAnidadaSeIdentifica()
    {
        var ex = new InvalidOperationException("INTERNO", new BootstrapFailureException("USUARIO_FICTICIO_EXISTENTE", "La cuenta ficticia ya existe."));
        var message = BootstrapDiagnostics.Describe(ex);
        Assert.Contains("USUARIO_FICTICIO_EXISTENTE", message);
        Assert.DoesNotContain("INTERNO", message);
    }
}
