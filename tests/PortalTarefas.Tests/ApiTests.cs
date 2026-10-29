using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortalTarefas.Web.Data;
using PortalTarefas.Web.DTOs;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Tests;

public sealed class PortalFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string Senha { get; } = "Aa1!" + Guid.NewGuid().ToString("N");
    public string EmailUsuario { get; } = $"usuario-{Guid.NewGuid():N}@teste.invalid";
    public string EmailAdmin { get; } = $"admin-{Guid.NewGuid():N}@teste.invalid";
    public string EmailOutraEquipe { get; } = $"outro-{Guid.NewGuid():N}@teste.invalid";
    public string Equipe { get; } = "Teste-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PortalTarefas"] = SqlTestDatabase.ConnectionString,
            ["Portal:Storage"] = "SqlServer", ["Telemetry:Console"] = "false", ["Seed:Enabled"] = "false"
        }));
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddHttpsRedirection(o => o.HttpsPort = 7240);
        });
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PORTAL_TEST_CONNECTION"))) return;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalTarefasDbContext>();
        // O runner aplica migrations explicitamente, após validar o destino exclusivo.
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Usuario", "Administrador" })
            if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        foreach (var (email, equipe, role) in new[] { (EmailUsuario, Equipe, "Usuario"), (EmailAdmin, Equipe, "Administrador"), (EmailOutraEquipe, "Outra-" + Equipe, "Usuario") })
        {
            var user = new Usuario { UserName = email, Email = email, EmailConfirmed = true, Equipe = equipe };
            Assert.True((await users.CreateAsync(user, Senha)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            if (role == "Administrador") Assert.True((await users.AddClaimAsync(user, new("tarefas:editar-outra-equipe", "true"))).Succeeded);
        }
    }

    public HttpClient NovoCliente() => CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });

    public async Task<HttpClient> Autenticado(string email)
    {
        var client = NovoCliente();
        await Csrf(client);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, senha = Senha });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await Csrf(client); // A identidade mudou: renovar token associado à sessão.
        return client;
    }

    public static async Task Csrf(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    Task IAsyncLifetime.DisposeAsync() { Dispose(); return Task.CompletedTask; }
}

[Collection("SqlIntegration")]
public sealed class ApiTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    private object Entrada(string titulo, bool publica = true) => new { titulo, descricao = "Descrição para teste real", prioridade = 2,
        prazo = "2026-12-01", situacao = "Pendente", equipe = factory.Equipe, publica };

    [SqlFact]
    public async Task Pipeline_binding_minimal_mvc_https_correlacao_e_openapi()
    {
        using var client = factory.NovoCliente();
        client.DefaultRequestHeaders.Add("X-Cliente", "integracao");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "teste-correla-01");
        var minimal = await client.GetAsync("/api/minimal/diagnostico/42?filtro=aberta");
        var mvc = await client.GetAsync("/api/diagnostico/42?filtro=aberta");
        Assert.Equal(HttpStatusCode.OK, minimal.StatusCode);
        Assert.Equal(await minimal.Content.ReadAsStringAsync(), await mvc.Content.ReadAsStringAsync());
        Assert.Equal("teste-correla-01", mvc.Headers.GetValues("X-Correlation-ID").Single());
        var result = await mvc.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(42, result.GetProperty("id").GetInt32());
        Assert.Equal("aberta", result.GetProperty("filtro").GetString());
        Assert.Equal("integracao", result.GetProperty("cliente").GetString());
        var schema = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.True(schema.GetProperty("paths").TryGetProperty("/api/tarefas/{id}", out var path));
        Assert.True(path.GetProperty("put").GetProperty("responses").TryGetProperty("409", out _));
        Assert.Contains("AtualizarTarefaDto", schema.GetProperty("components").GetProperty("schemas").ToString());
        var post = schema.GetProperty("paths").GetProperty("/api/tarefas").GetProperty("post");
        Assert.Equal(2, post.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .GetProperty("example").GetProperty("prioridade").GetInt32());
        Assert.True(post.GetProperty("responses").GetProperty("400").GetProperty("content")
            .GetProperty("application/problem+json").GetProperty("example").GetProperty("errors").TryGetProperty("Titulo", out _));
        Assert.Equal("X-CSRF-TOKEN", schema.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty("CsrfToken").GetProperty("name").GetString());
        var http = factory.CreateClient(new() { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.TemporaryRedirect, (await http.GetAsync("/health/live")).StatusCode);
    }

    [SqlFact]
    public async Task Crud_location_validacao_pagina_vazia_concorrencia_e_delete()
    {
        using var user = await factory.Autenticado(factory.EmailUsuario);
        var invalida = await user.PostAsJsonAsync("/api/tarefas", Entrada("x"));
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        Assert.Equal("application/problem+json", invalida.Content.Headers.ContentType?.MediaType);
        Assert.True((await invalida.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("errors", out _));
        var created = await user.PostAsJsonAsync("/api/tarefas", Entrada("Tarefa HTTP " + Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var tarefa = (await created.Content.ReadFromJsonAsync<TarefaDetalheDto>())!;
        var update = new { titulo = "Alterada via HTTP", descricao = tarefa.Descricao, prioridade = 2,
            prazo = tarefa.Prazo, situacao = "EmAndamento", equipe = tarefa.Equipe, publica = true, rowVersion = tarefa.RowVersion };
        var success = await user.PutAsJsonAsync($"/api/tarefas/{tarefa.Id}", update);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await user.PutAsJsonAsync($"/api/tarefas/{tarefa.Id}", update)).StatusCode);
        var latest = (await success.Content.ReadFromJsonAsync<TarefaDetalheDto>())!;
        var empty = await user.GetFromJsonAsync<PaginaDto<TarefaResumoDto>>("/api/tarefas?busca=naoexiste-" + Guid.NewGuid());
        Assert.Empty(empty!.Itens);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.GetAsync("/api/tarefas?tamanho=51&ordenarPor=DROP")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync("/api/tarefas/2147483647")).StatusCode);
        using var admin = await factory.Autenticado(factory.EmailAdmin);
        var deleted = await admin.DeleteAsync($"/api/tarefas/{tarefa.Id}?rowVersion={Uri.EscapeDataString(latest.RowVersion)}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync($"/api/tarefas/{tarefa.Id}")).StatusCode);
    }

    [SqlFact]
    public async Task Matriz_401_403_200_privacidade_csrf_logout_e_sessao()
    {
        using var anon = factory.NovoCliente();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/tarefas", Entrada("Sem sessão"))).StatusCode);
        using var user = await factory.Autenticado(factory.EmailUsuario);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/auth/me")).StatusCode);
        user.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsJsonAsync("/api/tarefas", Entrada("Sem CSRF"))).StatusCode);
        await PortalFactory.Csrf(user);
        var created = await user.PostAsJsonAsync("/api/tarefas", Entrada("Privada " + Guid.NewGuid(), false));
        var tarefa = (await created.Content.ReadFromJsonAsync<TarefaDetalheDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/tarefas/{tarefa.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.DeleteAsync($"/api/tarefas/{tarefa.Id}?rowVersion={Uri.EscapeDataString(tarefa.RowVersion)}")).StatusCode);
        using var outro = await factory.Autenticado(factory.EmailOutraEquipe);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.GetAsync($"/api/tarefas/{tarefa.Id}")).StatusCode);
        var input = new { titulo = "Tentativa externa", descricao = "Descrição válida", prioridade = 1, situacao = "Pendente",
            equipe = tarefa.Equipe, publica = false, rowVersion = tarefa.RowVersion };
        Assert.Equal(HttpStatusCode.Forbidden, (await outro.PutAsJsonAsync($"/api/tarefas/{tarefa.Id}", input)).StatusCode);
        using var admin = await factory.Autenticado(factory.EmailAdmin);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/tarefas/{tarefa.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/tarefas/{tarefa.Id}", input)).StatusCode);
        var logout = await user.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync("/api/auth/me")).StatusCode);
    }

    [SqlFact]
    public async Task Razor_listagem_detalhes_formulario_invalido_preserva_entrada()
    {
        using var user = await factory.Autenticado(factory.EmailUsuario);
        var lista = await user.GetStringAsync("/Tarefas/Index");
        Assert.Contains("Portal de Tarefas", lista);
        var form = await user.GetStringAsync("/Tarefas/Create");
        var token = System.Text.RegularExpressions.Regex.Match(form, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var result = await user.PostAsync("/Tarefas/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = token, ["Titulo"] = "x", ["Descricao"] = "Entrada preservada", ["Equipe"] = factory.Equipe, ["Prioridade"] = "3", ["Situacao"] = "Pendente" }));
        var html = await result.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Contains("Entrada preservada", html);
        Assert.Contains("value=\"x\"", html);
        Assert.Contains("120", html);
    }

    [SqlFact]
    public async Task Razor_cadastro_com_versao_vazia_edicao_e_conflito_sql()
    {
        using var user = await factory.Autenticado(factory.EmailUsuario);
        var form = await user.GetStringAsync("/Tarefas/Create");
        var entrada = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Campo(form, "__RequestVerificationToken"),
            ["Id"] = "0", ["RowVersion"] = "", ["Titulo"] = "Cadastro Razor SQL " + Guid.NewGuid().ToString("N"),
            ["Descricao"] = "Formulário real com os campos ocultos do navegador.", ["Equipe"] = factory.Equipe,
            ["Prioridade"] = "1", ["Situacao"] = "Pendente", ["Publica"] = "false"
        };
        var created = await user.PostAsync("/Tarefas/Create", new FormUrlEncodedContent(entrada));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var id = System.Text.RegularExpressions.Regex.Match(created.Headers.Location.ToString(), @"\d+$").Value;
        Assert.NotEmpty(id);
        var edit = await user.GetStringAsync("/Tarefas/Edit/" + id);
        entrada["__RequestVerificationToken"] = Campo(edit, "__RequestVerificationToken");
        entrada["Id"] = id;
        entrada["RowVersion"] = Campo(edit, "RowVersion");
        entrada["Titulo"] = "Edição Razor SQL confirmada";
        var saved = await user.PostAsync("/Tarefas/Edit/" + id, new FormUrlEncodedContent(entrada));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        // Outra aba envia a versão anterior: preservar a edição confirmada no SQL Server.
        entrada["Titulo"] = "Edição obsoleta preservada no formulário";
        var stale = await user.PostAsync("/Tarefas/Edit/" + id, new FormUrlEncodedContent(entrada));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var conflictHtml = WebUtility.HtmlDecode(await stale.Content.ReadAsStringAsync());
        Assert.Contains("Edição obsoleta preservada no formulário", conflictHtml);
        Assert.Contains("Recarregue", conflictHtml);
        var details = WebUtility.HtmlDecode(await user.GetStringAsync("/Tarefas/Details/" + id));
        Assert.Contains("Edição Razor SQL confirmada", details);
        Assert.DoesNotContain("Edição obsoleta", details);
    }

    private static string Campo(string html, string nome)
    {
        var input = System.Text.RegularExpressions.Regex.Match(html,
            "<input[^>]*name=\"" + System.Text.RegularExpressions.Regex.Escape(nome) + "\"[^>]*>").Value;
        var value = System.Text.RegularExpressions.Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value;
        Assert.NotEmpty(value);
        return WebUtility.HtmlDecode(value);
    }

    [SqlFact]
    public async Task Cache_publico_reutiliza_e_invalida_apos_escrita()
    {
        using var anon = factory.NovoCliente();
        var first = await anon.GetAsync("/api/publico/painel");
        var second = await anon.GetAsync("/api/publico/painel");
        Assert.Equal(first.Headers.GetValues("X-Cache-Gerado-Em"), second.Headers.GetValues("X-Cache-Gerado-Em"));
        using var user = await factory.Autenticado(factory.EmailUsuario);
        Assert.Equal(HttpStatusCode.Created, (await user.PostAsJsonAsync("/api/tarefas", Entrada("Invalidação " + Guid.NewGuid()))).StatusCode);
        var third = await anon.GetAsync("/api/publico/painel");
        Assert.NotEqual(first.Headers.GetValues("X-Cache-Gerado-Em").Single(), third.Headers.GetValues("X-Cache-Gerado-Em").Single());
    }
}
