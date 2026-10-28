using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PortalTarefas.Web.Controllers;

public sealed record DiagnosticoDto(int Id, string? Filtro, string? Cliente, string Metodo);

[ApiController, Route("api/diagnostico"), AllowAnonymous, EnableRateLimiting("diagnostico")]
public sealed class DiagnosticoController : ControllerBase
{
    /// <summary>Demonstra binding de rota, query e cabeçalho no controller MVC.</summary>
    [HttpGet("{id:int}"), ProducesResponseType<DiagnosticoDto>(200)]
    public DiagnosticoDto Obter([FromRoute] int id, [FromQuery] string? filtro, [FromHeader(Name = "X-Cliente")] string? cliente)
        => new(id, filtro, cliente, Request.Method);
}
