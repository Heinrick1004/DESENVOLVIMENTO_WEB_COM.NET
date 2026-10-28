using System.Diagnostics;
using System.Text.RegularExpressions;
using PortalTarefas.Web.Infrastructure;

namespace PortalTarefas.Web.Middleware;

public sealed partial class RequisicaoMiddleware(RequestDelegate next, ILogger<RequisicaoMiddleware> logger)
{
    [GeneratedRegex("^[a-zA-Z0-9_-]{1,64}$")]
    private static partial Regex CorrelacaoValida();

    public async Task InvokeAsync(HttpContext context)
    {
        var recebida = context.Request.Headers["X-Correlation-ID"].ToString();
        var id = CorrelacaoValida().IsMatch(recebida) ? recebida : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id;
        context.Response.Headers["X-Correlation-ID"] = id;
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id });
        var inicio = Stopwatch.GetTimestamp();
        logger.LogDebug("Pipeline entrada {Metodo} {Caminho}", context.Request.Method, context.Request.Path);
        try { await next(context); }
        finally
        {
            var duracao = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
            Telemetria.DuracaoHttp.Record(duracao, new KeyValuePair<string, object?>("metodo", context.Request.Method),
                new KeyValuePair<string, object?>("status", context.Response.StatusCode));
            logger.LogInformation("Pipeline saída {Metodo} {Caminho} {Status} {DuracaoMs}ms",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, duracao);
        }
    }
}

public static class RequisicaoMiddlewareExtensions
{
    public static IApplicationBuilder UseRegistroRequisicoes(this IApplicationBuilder app) => app.UseMiddleware<RequisicaoMiddleware>();
}
