using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.DTOs;
using PortalTarefas.Web.Security;
using PortalTarefas.Web.Services;

namespace PortalTarefas.Web.ViewModels;

public sealed class TarefasVencidasViewComponent(ITarefaService tarefas) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var resultado = await tarefas.ListarAsync(new TarefaConsulta
        {
            Vencidas = true,
            Tamanho = 3,
            OrdenarPor = "prazo"
        }, HttpContext.User.ToContexto(), HttpContext.RequestAborted);
        return View(resultado);
    }
}
