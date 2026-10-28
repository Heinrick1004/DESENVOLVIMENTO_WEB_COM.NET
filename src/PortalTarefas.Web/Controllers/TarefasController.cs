using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using PortalTarefas.Web.DTOs;
using PortalTarefas.Web.Infrastructure;
using PortalTarefas.Web.Security;
using PortalTarefas.Web.Services;
using PortalTarefas.Web.ViewModels;

namespace PortalTarefas.Web.Controllers;

public sealed class TarefasController(ITarefaService tarefas, IOutputCacheStore cache) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index([FromQuery] TarefaConsulta consulta, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(new TarefasListaViewModel(consulta,
                new PaginaDto<TarefaResumoDto>([], consulta.Pagina, consulta.Tamanho, 0, 0)));
        }

        var pagina = await tarefas.ListarAsync(consulta, User.ToContexto(), cancellationToken);
        return View(new TarefasListaViewModel(consulta, pagina));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var tarefa = await tarefas.ObterAsync(id, User.ToContexto(), cancellationToken);
        return tarefa is null ? NaoEncontrada() : View(tarefa);
    }

    [HttpGet]
    [Authorize]
    public IActionResult Create() => View(new TarefaFormModel { Equipe = User.ToContexto().Equipe });

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TarefaFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            var criada = await tarefas.CriarAsync(model, User.ToContexto(), cancellationToken);
            await cache.EvictByTagAsync("painel-publico", cancellationToken);
            Telemetria.TarefasCriadas.Add(1, new KeyValuePair<string, object?>("origem", "mvc"));
            TempData["Sucesso"] = "Tarefa criada com sucesso.";
            return RedirectToAction(nameof(Details), new { id = criada.Id });
        }
        catch (ValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
        catch (PermissaoException) { return Forbid(); }
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var usuario = User.ToContexto();
        var tarefa = await tarefas.ObterAsync(id, usuario, cancellationToken);
        if (tarefa is null) return NaoEncontrada();
        if (!TarefaApresentacao.PodeEditar(tarefa, usuario)) return Forbid();
        return View(TarefaFormModel.De(tarefa));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TarefaFormModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id) return BadRequest();
        if (!VersaoTarefa.Valida(model.RowVersion))
            ModelState.AddModelError(nameof(model.RowVersion), "A versão da tarefa é inválida. Recarregue os dados.");
        if (!ModelState.IsValid) return View(model);

        try
        {
            await tarefas.AtualizarAsync(id, model, model.RowVersion, User.ToContexto(), cancellationToken);
            await cache.EvictByTagAsync("painel-publico", cancellationToken);
            TempData["Sucesso"] = "Alterações salvas com sucesso.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (ConflitoException exception)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
        catch (ValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
        catch (PermissaoException) { return Forbid(); }
        catch (RecursoNaoEncontradoException) { return NaoEncontrada(); }
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var tarefa = await tarefas.ObterAsync(id, User.ToContexto(), cancellationToken);
        return tarefa is null ? NaoEncontrada() : View(tarefa);
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, string rowVersion, CancellationToken cancellationToken)
    {
        if (!VersaoTarefa.Valida(rowVersion)) return BadRequest("Versão da tarefa inválida.");
        try
        {
            await tarefas.ExcluirAsync(id, rowVersion, User.ToContexto(), cancellationToken);
            await cache.EvictByTagAsync("painel-publico", cancellationToken);
            TempData["Sucesso"] = "Tarefa excluída com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (ConflitoException exception)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            return View("Erro", new ErroViewModel(409, "A tarefa foi alterada", exception.Message));
        }
        catch (PermissaoException) { return Forbid(); }
        catch (RecursoNaoEncontradoException) { return NaoEncontrada(); }
    }

    private IActionResult NaoEncontrada()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("Erro", new ErroViewModel(404, "Tarefa não encontrada",
            "Esta tarefa não está disponível. Ela pode ter sido removida ou você pode não ter acesso a ela."));
    }
}
