using Microsoft.AspNetCore.Identity;

namespace PortalTarefas.Web.Models;

public sealed class Usuario : IdentityUser
{
    public string Equipe { get; set; } = "Geral";
}
