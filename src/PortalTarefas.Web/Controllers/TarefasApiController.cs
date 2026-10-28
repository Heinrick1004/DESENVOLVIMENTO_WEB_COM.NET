using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using PortalTarefas.Web.DTOs;
using PortalTarefas.Web.Infrastructure;
using PortalTarefas.Web.Security;
using PortalTarefas.Web.Services;

namespace PortalTarefas.Web.Controllers;

[ApiController, Route("api/tarefas"), EnableRateLimiting("api")]
[ProducesResponseType<ApiProblemDetails>(400), ProducesResponseType<ProblemDetails>(429)]
public sealed class TarefasApiController(ITarefaService tarefas, IOutputCacheStore cache) : ControllerBase
{
    /// <summary>Lista tarefas visíveis, com filtros, ordenação permitida e páginas de até 50 itens.</summary>
    [HttpGet, AllowAnonymous, ProducesResponseType<PaginaDto<TarefaResumoDto>>(200)]
    public Task<PaginaDto<TarefaResumoDto>> Listar([FromQuery] TarefaConsulta consulta, CancellationToken ct) => tarefas.ListarAsync(consulta, User.ToContexto(), ct);

    /// <summary>Consulta os detalhes e a versão atual de uma tarefa visível.</summary>
    [HttpGet("{id:int}", Name = "ObterTarefa"), AllowAnonymous]
    [ProducesResponseType<TarefaDetalheDto>(200), ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<TarefaDetalheDto>> Obter(int id, CancellationToken ct)
    {
        var tarefa = await tarefas.ObterAsync(id, User.ToContexto(), ct);
        return tarefa is null ? Problem(statusCode: 404, title: "Tarefa não encontrada.") : Ok(tarefa);
    }

    /// <summary>Cria tarefa para a equipe autorizada e retorna Location.</summary>
    [HttpPost, Authorize(Policy = "CriarTarefa")]
    [ProducesResponseType<TarefaDetalheDto>(201), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403)]
    public async Task<ActionResult<TarefaDetalheDto>> Criar(CriarTarefaDto input, CancellationToken ct)
    {
        using var activity = Telemetria.Traces.StartActivity("tarefa.criar");
        var tarefa = await tarefas.CriarAsync(input, User.ToContexto(), ct);
        Telemetria.TarefasCriadas.Add(1, new KeyValuePair<string, object?>("origem", "api"));
        await cache.EvictByTagAsync("painel-publico", ct);
        return CreatedAtRoute("ObterTarefa", new { id = tarefa.Id }, tarefa);
    }

    /// <summary>Atualiza com RowVersion. A versão obsoleta retorna 409 sem sobrescrever dados.</summary>
    [HttpPut("{id:int}"), Authorize]
    [ProducesResponseType<TarefaDetalheDto>(200), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403),
     ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
    public async Task<ActionResult<TarefaDetalheDto>> Atualizar(int id, AtualizarTarefaDto input, CancellationToken ct)
    {
        var tarefa = await tarefas.AtualizarAsync(id, input, input.RowVersion, User.ToContexto(), ct);
        await cache.EvictByTagAsync("painel-publico", ct);
        return Ok(tarefa);
    }

    /// <summary>Exclui tarefa com a versão atual. Exige perfil Administrador.</summary>
    [HttpDelete("{id:int}"), Authorize(Roles = "Administrador")]
    [ProducesResponseType(204), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(403),
     ProducesResponseType<ProblemDetails>(404), ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Excluir(int id, [FromQuery, Required] string rowVersion, CancellationToken ct)
    {
        if (!VersaoTarefa.Valida(rowVersion)) return Problem(statusCode: 400, title: "A versão deve ser uma sequência Base64 de 8 bytes.");
        await tarefas.ExcluirAsync(id, rowVersion, User.ToContexto(), ct);
        await cache.EvictByTagAsync("painel-publico", ct);
        return NoContent();
    }
}

[ApiController, Route("api/painel")]
public sealed class PainelController(ITarefaService tarefas) : ControllerBase
{
    [HttpGet, Authorize, ProducesResponseType<PainelDto>(200), ProducesResponseType<ProblemDetails>(401)]
    public Task<PainelDto> Ef(CancellationToken ct) => tarefas.PainelAsync(User.ToContexto(), ct);

    [HttpGet("ado"), Authorize, ProducesResponseType<PainelDto>(200), ProducesResponseType<ProblemDetails>(501), ProducesResponseType<ProblemDetails>(401)]
    public async Task<ActionResult<PainelDto>> Ado(CancellationToken ct)
    {
        try { return await tarefas.PainelAdoAsync(User.ToContexto(), ct); }
        catch (NotSupportedException) { return Problem(statusCode: 501, title: "A comparação ADO.NET exige SQL Server."); }
    }
}
