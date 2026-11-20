using PortalMercado.Application.Administration;
using PortalMercado.Application.Tiendas;
namespace PortalMercado.Tests.Tiendas;
internal sealed class TiendasUsuariosPrueba : IUserAdministration
    {
        private static UserDto User => new(2, "ficticio@example.invalid", "<script>FICTICIO</script>", null, true, ["VENDEDOR"], [1], [], false);
        public bool Habilitado { get; set; } = true;
        public Task<AccessSnapshot> AccessAsync(long actor, CancellationToken ct) => Task.FromResult(new AccessSnapshot(actor, true, ["ADMIN_GENERAL"], Habilitado ? [PerfilTiendaServicio.Permiso] : [], [], []));
        public Task<Page<UserDto>> ListAsync(long actor, int page, int size, CancellationToken ct) => Task.FromResult(new Page<UserDto>([User], 1, 1, 20));
        public Task<UserDto> GetAsync(long actor, long id, CancellationToken ct) => Task.FromResult(User);
        public Task<AdminCatalog> CatalogAsync(long actor, CancellationToken ct) => Task.FromResult(new AdminCatalog([new RoleDto(1, "VENDEDOR", "Vendedor", [1])], [new PermissionDto(1, Permissions.Context, "Contexto")], [new NamedId(1, "Negocio ficticio")], [new NamedId(1, "Mercado ficticio")]));
        public Task<BusinessContext> ContextAsync(long actor, string credential, string host, CancellationToken ct) => Task.FromResult(new BusinessContext([new NamedId(1, "Negocio ficticio")], 1));
        public Task<UserDto> CreateAsync(long actor, CreateUserRequest request, string correlation, CancellationToken ct) => throw new AccessFailure(403, "Operación no autorizada.");
        public Task EditAsync(long actor, long id, EditUserRequest request, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task StateAsync(long actor, long id, bool active, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task RolesAsync(long actor, long id, int[] roles, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task BusinessesAsync(long actor, long id, long[] ids, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task MarketsAsync(long actor, long id, long[] ids, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task PermissionsAsync(long actor, int role, int[] ids, string correlation, CancellationToken ct) => throw new NotSupportedException();
        public Task SelectContextAsync(long actor, string credential, string host, long business, string correlation, CancellationToken ct) => throw new NotSupportedException();
    }
