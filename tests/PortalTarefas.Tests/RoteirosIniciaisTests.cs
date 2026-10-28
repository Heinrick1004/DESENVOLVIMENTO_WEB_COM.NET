using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace PortalTarefas.Tests;

// Autenticação somente do host de testes. A aplicação entregue continua usando Identity/cookie.
public sealed class UsuarioInicialHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-User")) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "roteiros-iniciais"), new Claim(ClaimTypes.Name, "Aluno"),
            new Claim(ClaimTypes.Role, "Usuario"), new Claim("equipe", "Inicial")
        }, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

public sealed class LogsIniciais : ILoggerProvider
{
    public ConcurrentQueue<string> Eventos { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Captura(categoryName, Eventos);
    public void Dispose() { }
    private sealed class Captura(string categoria, ConcurrentQueue<string> eventos) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => categoria.StartsWith("PortalTarefas", StringComparison.Ordinal);
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level)) eventos.Enqueue($"{categoria}: {formatter(state, exception)}");
        }
    }
}

[ApiController, Route("__test/falha"), AllowAnonymous]
public sealed class FalhaDeTesteController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => throw new InvalidOperationException("detalhe-interno-somente-no-teste");
}

public sealed class InicialFactory : WebApplicationFactory<Program>
{
    public LogsIniciais Logs { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Portal:Storage"] = "Memory", ["Seed:Enabled"] = "false", ["Seed:SampleData"] = "false",
            ["Telemetry:Console"] = "false", ["Logging:LogLevel:PortalTarefas"] = "Debug"
        }));
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddHttpsRedirection(o => o.HttpsPort = 7240);
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = "TesteInicial"; o.DefaultChallengeScheme = "TesteInicial";
                o.DefaultForbidScheme = "TesteInicial";
            }).AddScheme<AuthenticationSchemeOptions, UsuarioInicialHandler>("TesteInicial", _ => { });
            services.AddControllersWithViews().AddApplicationPart(typeof(FalhaDeTesteController).Assembly);
            services.AddSingleton<ILoggerProvider>(Logs);
        });
    }

    public HttpClient Cliente(bool autenticado = false)
    {
        var client = CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (autenticado) client.DefaultRequestHeaders.Add("X-Test-User", "aluno");
        return client;
    }
}

public sealed class RoteirosIniciaisTests(ITestOutputHelper output)
{
    [Fact]
    public async Task R1_pipeline_binding_quatro_fontes_status_e_endpoints_equivalentes()
    {
        using var factory = new InicialFactory();
        using var client = factory.Cliente(true);
        client.DefaultRequestHeaders.Add("X-Cliente", "roteiro-1");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "r1-pipeline");
        var minimal = await client.GetAsync("/api/minimal/diagnostico/42?filtro=aberta");
        var mvc = await client.GetAsync("/api/diagnostico/42?filtro=aberta");
        Assert.Equal(HttpStatusCode.OK, minimal.StatusCode);
        Assert.Equal(await minimal.Content.ReadAsStringAsync(), await mvc.Content.ReadAsStringAsync());
        var binding = await mvc.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(42, binding.GetProperty("id").GetInt32());
        Assert.Equal("aberta", binding.GetProperty("filtro").GetString());
        Assert.Equal("roteiro-1", binding.GetProperty("cliente").GetString());
        Assert.Equal("r1-pipeline", mvc.Headers.GetValues("X-Correlation-ID").Single());

        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        var created = await client.PostAsJsonAsync("/api/tarefas", new
            { titulo = "Tarefa por JSON", descricao = "Binding do corpo verificado", prioridade = 1, equipe = "Inicial", publica = true });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var detalhe = await client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, detalhe.StatusCode);
        Assert.Contains("Tarefa por JSON", await detalhe.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/tarefas", new { titulo = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/tarefas/2147483647")).StatusCode);
        using var http = factory.CreateClient(new() { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.TemporaryRedirect, (await http.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/css/site.css")).StatusCode);

        var eventos = factory.Logs.Eventos.ToArray();
        var entrada = Array.FindIndex(eventos, e => e.Contains("Pipeline entrada GET /api/diagnostico/42"));
        var acao = Array.FindIndex(eventos, e => e.Contains("DiagnosticoController.Obter") && e.Contains("resultado"));
        var saida = Array.FindIndex(eventos, e => e.Contains("Pipeline saída GET /api/diagnostico/42 200"));
        Assert.True(entrada >= 0 && acao > entrada && saida > acao, string.Join(Environment.NewLine, eventos));
        output.WriteLine("GET rota/query/header: {0}", await mvc.Content.ReadAsStringAsync());
        output.WriteLine("POST JSON: {0} {1}", (int)created.StatusCode, await created.Content.ReadAsStringAsync());
        foreach (var evento in eventos) output.WriteLine(evento);
    }

    [Fact]
    public async Task R1_excecao_centralizada_preserva_correlacao_sem_expor_detalhe()
    {
        using var factory = new InicialFactory();
        using var client = factory.Cliente();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "r1-excecao");
        var response = await client.GetAsync("/__test/falha");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("detalhe-interno", body);
        Assert.Contains("r1-excecao", body);
        Assert.Equal("r1-excecao", response.Headers.GetValues("X-Correlation-ID").Single());
        output.WriteLine("GET falha controlada: {0} {1}", (int)response.StatusCode, body);
    }

    [Fact]
    public async Task R2_razor_cadastro_edicao_detalhes_preservacao_validacao_e_filtro()
    {
        using var factory = new InicialFactory();
        using var client = factory.Cliente(true);
        var form = await client.GetStringAsync("/Tarefas/Create");
        var entrada = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(form), ["Titulo"] = "x", ["Descricao"] = "Texto preservado",
            ["Prioridade"] = "3", ["Equipe"] = "Inicial", ["Situacao"] = "Pendente", ["Publica"] = "true"
        };
        var invalid = await client.PostAsync("/Tarefas/Create", new FormUrlEncodedContent(entrada));
        var html = await invalid.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("value=\"x\"", html);
        Assert.Contains("Texto preservado", html);
        Assert.Contains("entre 3 e 120", WebUtility.HtmlDecode(html));
        entrada["Titulo"] = "Tarefa inicial válida";
        var semPrazo = await client.PostAsync("/Tarefas/Create", new FormUrlEncodedContent(entrada));
        Assert.Contains("exigem prazo", WebUtility.HtmlDecode(await semPrazo.Content.ReadAsStringAsync()));
        entrada["Prazo"] = "2026-12-01";
        var created = await client.PostAsync("/Tarefas/Create", new FormUrlEncodedContent(entrada));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var details = await client.GetStringAsync(created.Headers.Location);
        Assert.Contains("Tarefa inicial", details);
        var id = Regex.Match(created.Headers.Location!.ToString(), @"\d+$").Value;
        Assert.NotEmpty(id);
        var edit = await client.GetStringAsync("/Tarefas/Edit/" + id);
        entrada["__RequestVerificationToken"] = Token(edit);
        entrada["Id"] = id;
        entrada["RowVersion"] = Campo(edit, "RowVersion");
        entrada["Titulo"] = "Tarefa inicial editada";
        var saved = await client.PostAsync("/Tarefas/Edit/" + id, new FormUrlEncodedContent(entrada));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var updated = await client.GetStringAsync(saved.Headers.Location);
        Assert.Contains("Tarefa inicial editada", updated);
        var list = await client.GetStringAsync("/Tarefas/Index");
        Assert.Contains("Tarefa inicial editada", list);
        Assert.Contains("Portal de Tarefas", list);
        var logs = factory.Logs.Eventos.Where(e => e.Contains("TarefasController.Create") || e.Contains("TarefasController.Edit")).ToArray();
        Assert.Contains(logs, e => e.Contains("ViewResult"));
        Assert.Contains(logs, e => e.Contains("RedirectToActionResult"));
        output.WriteLine("Formulário inválido preservou título/descrição e exibiu erro de prazo; cadastro e edição redirecionaram aos detalhes.");
        foreach (var log in logs) output.WriteLine(log);
    }

    private static string Token(string html) => Campo(html, "__RequestVerificationToken");
    private static string Campo(string html, string nome)
    {
        var input = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(nome) + "\"[^>]*>").Value;
        var value = Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value;
        Assert.NotEmpty(value);
        return WebUtility.HtmlDecode(value);
    }
}
