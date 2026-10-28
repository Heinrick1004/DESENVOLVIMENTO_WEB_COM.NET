using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PortalTarefas.Web.Filters;

/// <summary>Observa ações MVC sem registrar argumentos, corpos, credenciais ou tokens.</summary>
public sealed class LogAcaoFilter(ILogger<LogAcaoFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var inicio = Stopwatch.GetTimestamp();
        string resultado = "Excecao";
        try
        {
            var executada = await next();
            resultado = executada.Exception is not null && !executada.ExceptionHandled
                ? "Excecao"
                : executada.Result?.GetType().Name ?? "SemResultado";
        }
        finally
        {
            logger.LogInformation("Ação {Acao} concluída em {DuracaoMs:F2} ms; resultado {Resultado}",
                context.ActionDescriptor.DisplayName,
                Stopwatch.GetElapsedTime(inicio).TotalMilliseconds,
                resultado);
        }
    }
}
