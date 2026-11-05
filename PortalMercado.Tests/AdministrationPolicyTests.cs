using PortalMercado.Application.Administration;
using Xunit;
namespace PortalMercado.Tests;
public sealed class AdministrationPolicyTests
{
    private static AccessSnapshot Owner(bool active = true, string[]? permissions = null) => new(1, active, ["PROPIETARIO_NEGOCIO"], permissions ?? [Permissions.Edit], [], [10]);
    [Fact] public void OwnerCannotModifyForeignBusiness() => Assert.False(Owner().CanManage(2, ["VENDEDOR"], [], [20]));
    [Fact] public void OwnerCannotModifySharedIdentity() => Assert.False(Owner().CanManage(2, ["VENDEDOR"], [], [10, 20]));
    [Fact] public void OwnerCanManageOwnSeller() => Assert.True(Owner().CanManage(2, ["VENDEDOR"], [], [10]));
    [Fact] public void OwnerCannotManageUnassignedIdentity() => Assert.False(Owner().CanManage(2, ["VENDEDOR"], [], []));
    [Fact] public void OwnerCannotEditLowercaseAdministrator() => Assert.False(Owner().CanManage(2, ["admin_general"], [], [10]));
    [Fact] public void OwnerCannotEditAdministrator() => Assert.False(Owner().CanManage(2, ["ADMIN_GENERAL"], [], [10]));
    [Fact] public void OwnerCannotElevateSelf() => Assert.False(Owner().CanManage(1, ["PROPIETARIO_NEGOCIO"], [], [10]));
    [Theory] [InlineData("ADMIN_GENERAL")] [InlineData("ADMIN_MERCADO")] [InlineData("PROPIETARIO_NEGOCIO")]
    public void OwnerCannotAssignPrivilegedRoles(string role) => Assert.False(Owner().CanAssign(role));
    [Fact] public void OwnerCanAssignSeller() => Assert.True(Owner().CanAssign("VENDEDOR"));
    [Fact] public void InactiveOwnerCannotUsePermission() => Assert.Throws<AccessFailure>(() => Owner(false).Demand(Permissions.Edit));
    [Fact] public void ScopeDoesNotGrantPermission() => Assert.Throws<AccessFailure>(() => Owner(permissions: []).Demand(Permissions.Edit));
    [Fact] public void SellerEvenWithPermissionCannotManageIdentity() => Assert.False(new AccessSnapshot(1, true, ["VENDEDOR"], [Permissions.Edit], [], [10]).CanManage(2, ["VENDEDOR"], [], [10]));
    [Fact] public void GeneralStillRequiresExplicitPermission() => Assert.Throws<AccessFailure>(() => new AccessSnapshot(1, true, ["ADMIN_GENERAL"], [], [], []).Demand(Permissions.Manage));
    [Fact] public void MarketAdminRejectsForeignMarket() => Assert.False(new AccessSnapshot(1, true, ["ADMIN_MERCADO"], [Permissions.Edit], [10], [100]).CanManage(2, ["VENDEDOR"], [20], [100]));
    [Fact] public void MarketAdminAcceptsAssignedMarketAndBusiness() => Assert.True(new AccessSnapshot(1, true, ["ADMIN_MERCADO"], [Permissions.Edit], [10], [100]).CanManage(2, ["VENDEDOR"], [10], [100]));
    [Fact] public void GeneralCannotModifyOwnIdentity() => Assert.False(new AccessSnapshot(1, true, ["ADMIN_GENERAL"], Permissions.All, [], []).CanManage(1, ["ADMIN_GENERAL"], [], []));
}
