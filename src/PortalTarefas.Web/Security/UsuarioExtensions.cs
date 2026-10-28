using System.Security.Claims;
using PortalTarefas.Web.Services;

namespace PortalTarefas.Web.Security;

public static class UsuarioExtensions
{
    public static UsuarioContexto ToContexto(this ClaimsPrincipal user) => new(
        user.FindFirstValue(ClaimTypes.NameIdentifier), user.FindFirstValue("equipe") ?? "Geral",
        user.Identity?.IsAuthenticated == true, user.IsInRole("Administrador"),
        user.HasClaim("tarefas:editar-outra-equipe", "true"));
}
