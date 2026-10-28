using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PortalTarefas.Web.Controllers;
using PortalTarefas.Web.Data;
using PortalTarefas.Web.Filters;
using PortalTarefas.Web.Infrastructure;
using PortalTarefas.Web.Middleware;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.Security;
using PortalTarefas.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddOptions<PortalOptions>().BindConfiguration("Portal").ValidateDataAnnotations()
    .Validate(o => Uri.TryCreate(o.FrontendOrigin, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "https" || uri.Scheme == "http") && uri.AbsolutePath == "/", "Portal:FrontendOrigin deve conter uma origem HTTP válida.")
    .ValidateOnStart();
var memory = builder.Configuration["Portal:Storage"] == "Memory";
var connection = builder.Configuration.GetConnectionString("PortalTarefas");
if (!memory && string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("Configure ConnectionStrings:PortalTarefas por User Secrets ou variável de ambiente.");
if (memory && !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException("O armazenamento Memory é permitido somente para demonstração em Development ou Testing.");

// O contexto guarda apenas opções; nenhum estado de usuário/equipe persiste no pool.
builder.Services.AddDbContextPool<PortalTarefasDbContext>(o => o.UseSqlServer(connection ?? "Server=localhost;Database=PortalTarefasDemo;Integrated Security=true;Encrypt=true;TrustServerCertificate=true"));
if (memory)
{
    builder.Services.AddSingleton<InMemoryTarefaService>();
    builder.Services.AddSingleton<ITarefaService>(sp => sp.GetRequiredService<InMemoryTarefaService>());
}
else builder.Services.AddScoped<ITarefaService, EfTarefaService>();

builder.Services.AddIdentity<Usuario, IdentityRole>(o =>
{
    o.Password.RequiredLength = 12;
    o.Password.RequireDigit = true; o.Password.RequireUppercase = true; o.Password.RequireLowercase = true;
    o.Password.RequireNonAlphanumeric = true;
    o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    o.User.RequireUniqueEmail = true;
}).AddEntityFrameworkStores<PortalTarefasDbContext>().AddDefaultTokenProviders();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<Usuario>, UsuarioClaimsFactory>();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "__Host-PortalTarefas"; o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30); o.SlidingExpiration = true;
    o.LoginPath = "/Conta/Login"; o.AccessDeniedPath = "/Conta/AcessoNegado";
    o.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 401;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 403;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("CriarTarefa", p => p.RequireAuthenticatedUser().RequireRole("Usuario", "Administrador"));
    o.AddPolicy("EditarOutraEquipe", p => p.RequireClaim("tarefas:editar-outra-equipe", "true"));
});
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "__Host-PortalTarefas-CSRF";
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.AddControllersWithViews(o => { o.Filters.Add<LogAcaoFilter>(); o.Filters.Add<ApiCsrfFilter>(); });
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = context =>
{
    var details = new ValidationProblemDetails(context.ModelState) { Status = 400, Title = "Confira os campos informados." };
    details.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
    return new BadRequestObjectResult(details) { ContentTypes = { "application/problem+json" } };
});
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c => c.ProblemDetails.Extensions["correlationId"] = c.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ErroHandler>();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddPolicy("FrontendLocal", p => p.WithOrigins(builder.Configuration["Portal:FrontendOrigin"] ?? "http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("X-Correlation-ID", "Location")));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("diagnostico", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 5, Window = TimeSpan.FromSeconds(10), QueueLimit = 0, AutoReplenishment = true }));
    o.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    o.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "10";
        await Results.Problem(statusCode: 429, title: "Limite de requisições atingido. Tente novamente em instantes.").ExecuteAsync(context.HttpContext);
    };
});
builder.Services.AddOutputCache(o => o.AddPolicy("Publico", p => p.Expire(TimeSpan.FromSeconds(15)).Tag("painel-publico")));
builder.Services.AddHealthChecks().AddCheck<BancoHealthCheck>("banco", tags: ["ready"])
    .AddCheck<DependenciaHealthCheck>("dependencia", tags: ["ready"]);
builder.Services.AddHttpClient("dependencia", c => c.Timeout = TimeSpan.FromSeconds(15))
    .AddStandardResilienceHandler(o =>
    {
        o.Retry.MaxRetryAttempts = 2; o.Retry.Delay = TimeSpan.FromMilliseconds(200); o.Retry.UseJitter = true;
        o.Retry.DisableForUnsafeHttpMethods();
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        o.CircuitBreaker.MinimumThroughput = 4; o.CircuitBreaker.FailureRatio = .5;
        o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
    });
var telemetry = builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService(Telemetria.Nome));
telemetry.WithTracing(t =>
{
    t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddSqlClientInstrumentation().AddSource(Telemetria.Nome);
    if (builder.Configuration.GetValue<bool>("Telemetry:Console")) t.AddConsoleExporter();
});
telemetry.WithMetrics(m =>
{
    m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddMeter(Telemetria.Nome);
    if (builder.Configuration.GetValue<bool>("Telemetry:Console")) m.AddConsoleExporter();
});

var app = builder.Build();
if (args.Contains("--initialize"))
{
    if (memory) throw new InvalidOperationException("A inicialização de banco exige SqlServer.");
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<PortalTarefasDbContext>().Database.MigrateAsync();
    await SeedData.InicializarAsync(app.Services, app.Configuration);
    return;
}
if (memory && builder.Configuration.GetValue<bool>("Seed:SampleData")) app.Services.GetRequiredService<InMemoryTarefaService>().PopularExemplos();
if (!memory && builder.Configuration.GetValue<bool>("Seed:Enabled")) await SeedData.InicializarAsync(app.Services, app.Configuration);

app.UseRegistroRequisicoes();
app.UseExceptionHandler();
app.UseStatusCodePages(async context => await Results.Problem(statusCode: context.HttpContext.Response.StatusCode).ExecuteAsync(context.HttpContext));
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("FrontendLocal");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseOutputCache();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => { o.SwaggerEndpoint("/openapi/v1.json", "Portal de Tarefas v1"); o.RoutePrefix = "swagger"; });
}
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapGet("/version", () => new { version = typeof(Program).Assembly
    .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
    .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "local" });
app.MapGet("/api/minimal/diagnostico/{id:int}", (int id, string? filtro,
    [FromHeader(Name = "X-Cliente")] string? cliente, HttpRequest request) => new DiagnosticoDto(id, filtro, cliente, request.Method))
    .WithName("DiagnosticoMinimal").WithSummary("Binding de rota, query e cabeçalho em Minimal API.").RequireRateLimiting("diagnostico");
app.MapGet("/api/publico/painel", async (ITarefaService service, HttpContext context, CancellationToken ct) =>
{
    // Usa SEMPRE o escopo anônimo: cache nunca contém estatística privada.
    context.Response.Headers["X-Cache-Gerado-Em"] = DateTimeOffset.UtcNow.ToString("O");
    return await service.PainelAsync(new UsuarioContexto(null, "Geral", false, false, false), ct);
}).CacheOutput("Publico").WithName("PainelPublico");
app.MapControllers();
app.MapControllerRoute("default", "{controller=Tarefas}/{action=Index}/{id?}");
app.MapFallbackToFile("app/{*path:nonfile}", "app/index.html");
await app.RunAsync();

public partial class Program { }
