using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PortalMercado.Application.Security;
using PortalMercado.Infrastructure.Security;
namespace PortalMercado.Tests;
public sealed class PasswordDeliveryTests
{
    [Fact] public async Task PickupWritesMailOutsideProjectAndPreservesHttpsLink()
    {
        var path = Path.Combine(Path.GetTempPath(), "pm-mail-" + Guid.NewGuid().ToString("N"));
        try
        {
            var delivery = new RecoveryDelivery(Config("Pickup", path), new Env());
            var link = new Uri("https://localhost:7275/Cuenta/Restablecer?token=" + new string('a',43));
            await delivery.DeliverAsync("ficticio@example.invalid", link, default);
            var text = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(path, "*.eml")));
            Assert.Contains(link.AbsoluteUri, text); Assert.Contains("To: ficticio@example.invalid", text);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    [Fact] public void ProductionNeverUsesPickup()
    {
        var error = Assert.Throws<PasswordFailure>(() => new RecoveryDelivery(Config("Pickup", Path.GetTempPath()), new Env { EnvironmentName = "Production" }).CheckConfiguration());
        Assert.Equal(503,error.Status);
    }
    [Fact] public void PickupInsideSolutionIsRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "pm-solution");
        Assert.Throws<PasswordFailure>(() => new RecoveryDelivery(Config("Pickup", Path.Combine(root, "Web", "wwwroot", "mail")), new Env { ContentRootPath = Path.Combine(root, "Web") }).CheckConfiguration());
    }
    [Fact] public async Task FileWriteFailureIsReportedWithoutPathOrToken()
    {
        var file = Path.GetTempFileName();
        try
        {
            var e = await Assert.ThrowsAsync<PasswordFailure>(() => new RecoveryDelivery(Config("Pickup",file),new Env()).DeliverAsync("ficticio@example.invalid",new Uri("https://localhost/?token=PRIVADO"),default));
            Assert.Equal(503,e.Status); Assert.DoesNotContain(file,e.Message); Assert.DoesNotContain("PRIVADO",e.Message);
        }
        finally { File.Delete(file); }
    }
    [Fact] public void MissingSmtpConfigurationDoesNotPretendDeliverySuccess() => Assert.Throws<PasswordFailure>(() => new RecoveryDelivery(Config("Smtp",null),new Env()).CheckConfiguration());
    private static IConfiguration Config(string provider,string? path) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Recovery:Provider"]=provider,["Recovery:PickupPath"]=path }).Build();
    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; }="Development"; public string ApplicationName { get; set; }="Tests";
        public string ContentRootPath { get; set; }=""; public IFileProvider ContentRootFileProvider { get; set; }=new NullFileProvider();
    }
}
