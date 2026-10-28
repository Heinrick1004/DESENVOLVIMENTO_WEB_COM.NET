using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.Controllers;

public sealed class ContaController(SignInManager<Usuario> signInManager, ILogger<ContaController> logger) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return DestinoLocal(returnUrl);
        return View(new LoginViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var resultado = await signInManager.PasswordSignInAsync(
            model.Email, model.Senha, model.LembrarMe, lockoutOnFailure: true);

        logger.LogInformation("Autenticação MVC. Sucesso: {Sucesso}; Bloqueada: {Bloqueada}",
            resultado.Succeeded, resultado.IsLockedOut);

        if (resultado.Succeeded) return DestinoLocal(model.ReturnUrl);

        ModelState.AddModelError(string.Empty, resultado.IsLockedOut
            ? "Acesso temporariamente bloqueado. Aguarde alguns minutos e tente novamente."
            : "Não foi possível entrar. Verifique seu e-mail e sua senha.");
        // Password Tag Helper não repopula a senha no HTML devolvido.
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        logger.LogInformation("Sessão MVC encerrada.");
        return RedirectToAction("Index", "Tarefas");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AcessoNegado()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private IActionResult DestinoLocal(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Tarefas");
}
