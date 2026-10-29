using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Data;
using PortalTarefas.Web.DTOs;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Controllers;

public sealed record LoginDto(
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "Informe um e-mail válido.")] string Email,
    [Required(ErrorMessage = "Informe a senha.")] string Senha);
public sealed record CsrfDto(string Token);
public sealed record SessaoDto(string Id, string Email, string Equipe, string[] Roles, bool PodeEditarOutrasEquipes);

[ApiController, Route("api/auth")]
public sealed class AuthController(SignInManager<Usuario> signIn, UserManager<Usuario> users,
    IAntiforgery antiforgery, PortalTarefasDbContext db) : ControllerBase
{
    /// <summary>Obtém token CSRF vinculado ao cookie e à sessão atual.</summary>
    [HttpGet("csrf"), AllowAnonymous, ProducesResponseType<CsrfDto>(200)]
    public ActionResult<CsrfDto> Csrf()
    {
        Response.Headers.CacheControl = "no-store";
        return new CsrfDto(antiforgery.GetAndStoreTokens(HttpContext).RequestToken!);
    }

    /// <summary>Inicia sessão por cookie protegido. Requer X-CSRF-TOKEN.</summary>
    [HttpPost("login"), AllowAnonymous]
    [ProducesResponseType<SessaoDto>(200), ProducesResponseType<ApiProblemDetails>(400, "application/problem+json"), ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    public async Task<ActionResult<SessaoDto>> Login(LoginDto input)
    {
        var user = await users.FindByEmailAsync(input.Email);
        var result = user is null ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signIn.PasswordSignInAsync(user, input.Senha, isPersistent: false, lockoutOnFailure: true);
        db.Auditorias.Add(new Auditoria { Acao = result.Succeeded ? "LoginSucesso" : "LoginFalha", UsuarioId = user?.Id });
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        if (!result.Succeeded) return Problem(statusCode: 401, title: "E-mail ou senha inválidos, ou acesso temporariamente bloqueado.");
        return await Sessao(user!);
    }

    /// <summary>Restaura a sessão atual. Retorna 401 sem sessão.</summary>
    [HttpGet("me"), Authorize]
    [ProducesResponseType<SessaoDto>(200), ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    public async Task<ActionResult<SessaoDto>> Me()
    {
        Response.Headers.CacheControl = "no-store";
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : await Sessao(user);
    }

    /// <summary>Encerra a sessão atual. Requer X-CSRF-TOKEN.</summary>
    [HttpPost("logout"), Authorize]
    [ProducesResponseType(204), ProducesResponseType<ProblemDetails>(400, "application/problem+json"), ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    private async Task<SessaoDto> Sessao(Usuario user) => new(user.Id, user.Email!, user.Equipe,
        (await users.GetRolesAsync(user)).ToArray(),
        (await users.GetClaimsAsync(user)).Any(c => c.Type == "tarefas:editar-outra-equipe" && c.Value == "true"));
}
