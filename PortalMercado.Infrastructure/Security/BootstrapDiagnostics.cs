using Microsoft.Data.SqlClient;
using PortalMercado.Application.Security;

namespace PortalMercado.Infrastructure.Security;

public static class BootstrapDiagnostics
{
    public static string Describe(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is BootstrapFailureException known)
                return $"No se completó el aprovisionamiento [{known.Code}]. {known.Message}";
            if (current is SqlException sql)
            {
                var hint = sql.Number switch
                {
                    207 or 208 => "Verifica la base local y la ampliación ET02-001 con 03_Verificar.sql.",
                    2601 or 2627 => "Ya existe un registro único; comprueba si la cuenta ficticia ya fue creada.",
                    547 => "Revisa las relaciones y restricciones de seguridad en la base local.",
                    18456 or 4060 => "La cuenta Windows no pudo acceder a la base configurada.",
                    334 or 4186 => "SQL rechazó OUTPUT en la inserción; revisar el mapeo EF y columnas calculadas o triggers.",
                    _ => "Revisa disponibilidad, permisos y esquema de la instancia local."
                };
                return $"No se completó el aprovisionamiento [SQL:{sql.Number}]. {hint}";
            }
        }
        // No imprimir exception.Message, ToString, InnerException ni credenciales.
        return $"No se completó el aprovisionamiento [TIPO:{exception.GetType().Name}]. Revisa configuración y modelo; conserva este código para diagnóstico.";
    }
}
