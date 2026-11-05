using System.Text.Json.Nodes;
using PortalMercado.Application.Administration;
using PortalMercado.Application.Security;
namespace PortalMercado.API.OpenApi;

internal static class RequestExamples
{
    // Ejemplos ficticios. Los marcadores de contraseña/token no son credenciales.
    public static JsonNode? For(Type type) => type.Name switch
    {
        nameof(LoginRequest) => new JsonObject { ["correo"] = "vendedor.et02@example.invalid", ["password"] = "REEMPLAZAR_CLAVE_LOCAL" },
        nameof(ForgotPasswordRequest) => new JsonObject { ["correo"] = "vendedor.et02@example.invalid" },
        nameof(ResetPasswordRequest) => new JsonObject { ["token"] = "REEMPLAZAR_TOKEN_DEL_CORREO", ["newPassword"] = "REEMPLAZAR_CLAVE_NUEVA", ["confirmPassword"] = "REEMPLAZAR_CLAVE_NUEVA" },
        nameof(ChangePasswordRequest) => new JsonObject { ["currentPassword"] = "REEMPLAZAR_CLAVE_ACTUAL", ["newPassword"] = "REEMPLAZAR_CLAVE_NUEVA", ["confirmPassword"] = "REEMPLAZAR_CLAVE_NUEVA" },
        nameof(MfaChallengeRequest) => new JsonObject { ["challenge"] = "REEMPLAZAR_RETO_MFA" },
        nameof(MfaVerifyRequest) => new JsonObject { ["challenge"] = "REEMPLAZAR_RETO_MFA", ["code"] = "REEMPLAZAR_CODIGO_MFA", ["regenerateRecoveryCodes"] = false },
        nameof(CreateUserRequest) => new JsonObject { ["correo"] = "demo.et02@example.invalid", ["nombre"] = "Cuenta ficticia", ["telefono"] = null,
            ["password"] = "REEMPLAZAR_CLAVE_INICIAL", ["rol"] = "VENDEDOR", ["negocios"] = new JsonArray(JsonValue.Create(1)), ["mercados"] = new JsonArray() },
        nameof(EditUserRequest) => new JsonObject { ["nombre"] = "Cuenta ficticia actualizada", ["telefono"] = null },
        nameof(StateRequest) => new JsonObject { ["activo"] = false },
        nameof(RoleAssignment) or nameof(ScopeAssignment) or nameof(PermissionAssignment) => new JsonObject { ["ids"] = new JsonArray(JsonValue.Create(1)) },
        nameof(ContextRequest) => new JsonObject { ["idNegocio"] = 1 },
        _ => null
    };
}
