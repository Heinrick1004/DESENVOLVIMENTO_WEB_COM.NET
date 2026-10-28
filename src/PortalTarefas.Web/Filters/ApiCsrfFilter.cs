using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PortalTarefas.Web.Filters;

public sealed class ApiCsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Request.Path.StartsWithSegments("/api") ||
            HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method) ||
            HttpMethods.IsOptions(context.HttpContext.Request.Method)) return;
        // Executar depois da autorização: operações anônimas protegidas devem retornar 401.
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException)
        {
            context.Result = new ObjectResult(new ProblemDetails { Status = 400, Title = "Token de proteção CSRF ausente ou inválido." })
                { StatusCode = 400, ContentTypes = { "application/problem+json" } };
        }
    }
}
