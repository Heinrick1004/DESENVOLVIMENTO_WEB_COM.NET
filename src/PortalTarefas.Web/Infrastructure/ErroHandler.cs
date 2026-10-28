using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Services;

namespace PortalTarefas.Web.Infrastructure;

public sealed class ErroHandler(IProblemDetailsService problems, ILogger<ErroHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, titulo) = exception switch
        {
            RecursoNaoEncontradoException => (404, "Tarefa não encontrada."),
            PermissaoException => (403, "Você não tem permissão para esta operação."),
            ConflitoException => (409, "A tarefa foi alterada por outra sessão. Recarregue os dados antes de tentar novamente."),
            ValidationException => (400, "Os dados enviados são inválidos."),
            BadHttpRequestException => (400, "A requisição é inválida."),
            _ => (500, "Não foi possível concluir a operação.")
        };
        // Não registrar exception.ToString(): alguns provedores incluem dados de conexão.
        if (status == 500) logger.LogError("Operação falhou {TipoErro} {CorrelationId}", exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = status;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context, ProblemDetails = new ProblemDetails
            {
                Status = status, Title = titulo,
                Detail = status == 400 && exception is ValidationException ? exception.Message : null,
                Extensions = { ["correlationId"] = context.TraceIdentifier }
            }
        });
        return true;
    }
}
