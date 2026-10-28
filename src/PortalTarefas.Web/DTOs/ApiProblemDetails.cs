using Microsoft.AspNetCore.Mvc;

namespace PortalTarefas.Web.DTOs;

/// <summary>Contrato de falha HTTP; errors está presente nas falhas de validação por campo.</summary>
public sealed class ApiProblemDetails : ProblemDetails
{
    public IDictionary<string, string[]>? Errors { get; init; }
}
