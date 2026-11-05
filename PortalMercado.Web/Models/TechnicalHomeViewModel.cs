namespace PortalMercado.Web.Models;

public sealed class TechnicalHomeViewModel
{
    public string ApplicationName { get; init; } = "PortalMercado";
    public string Version { get; init; } = string.Empty;
    public string EnvironmentName { get; init; } = string.Empty;
}