using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PortalTarefas.Web.Data;

namespace PortalTarefas.Web.Infrastructure;

public sealed class BancoHealthCheck(PortalTarefasDbContext db, IConfiguration config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (config["Portal:Storage"] == "Memory") return HealthCheckResult.Unhealthy();
        try { return await db.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy(); }
        catch { return HealthCheckResult.Unhealthy(); }
    }
}

public sealed class DependenciaHealthCheck(IHttpClientFactory clients, IConfiguration config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // URL vazia significa que não existe dependência HTTP essencial neste domínio.
        if (string.IsNullOrEmpty(config["Dependencia:Url"])) return HealthCheckResult.Healthy();
        try
        {
            using var response = await clients.CreateClient("dependencia").GetAsync(config["Dependencia:Url"], cancellationToken);
            return response.IsSuccessStatusCode ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch { return HealthCheckResult.Unhealthy(); }
    }
}
