using Xunit;

namespace PortalMercado.Tests;

public sealed class SqlIntegrationFactAttribute : FactAttribute
{
    public SqlIntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PM_RUN_SQL_TESTS") != "1")
        {
            Skip = "Requiere habilitar PM_RUN_SQL_TESTS=1 y preparar SQL local.";
        }
    }
}