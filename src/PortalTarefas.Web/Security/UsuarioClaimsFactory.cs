using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Security;

public sealed class UsuarioClaimsFactory(UserManager<Usuario> users, RoleManager<IdentityRole> roles,
    IOptions<IdentityOptions> options) : UserClaimsPrincipalFactory<Usuario, IdentityRole>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Usuario user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("equipe", user.Equipe));
        return identity;
    }
}
