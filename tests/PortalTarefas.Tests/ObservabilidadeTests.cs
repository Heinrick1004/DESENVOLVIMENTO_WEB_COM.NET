using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PortalTarefas.Tests;

public sealed class RespostaControladaHandler(Func<int, HttpStatusCode> status) : HttpMessageHandler
{
    public int Chamadas;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(status(Interlocked.Increment(ref Chamadas))));
}

[Collection("SqlIntegration")]
public sealed class ObservabilidadeTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    [SqlFact]
    public async Task Rate_limiting_em_carga_controlada_retorna_429_e_retry_after()
    {
        using var isolated = factory.WithWebHostBuilder(_ => { });
        using var client = isolated.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.GetAsync("/api/diagnostico/1")));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests));
        Assert.All(responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests), r => Assert.NotNull(r.Headers.RetryAfter));
        foreach (var response in responses) response.Dispose();
    }

    [SqlFact]
    public async Task Liveness_independe_de_readiness_que_verifica_dependencia()
    {
        var handler = new RespostaControladaHandler(_ => HttpStatusCode.ServiceUnavailable);
        using var isolated = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                { ["Dependencia:Url"] = "https://dependencia.invalid/saude" }));
            builder.ConfigureServices(services => services.AddHttpClient("dependencia").ConfigurePrimaryHttpMessageHandler(() => handler));
        });
        using var client = isolated.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
        Assert.InRange(handler.Chamadas, 1, 3);
    }

    [SqlFact]
    public async Task Retry_finito_recupera_get_transitorio_sem_repetir_post()
    {
        var getHandler = new RespostaControladaHandler(n => n < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        using var isolated = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient("dependencia").ConfigurePrimaryHttpMessageHandler(() => getHandler)));
        using var client = isolated.Services.GetRequiredService<IHttpClientFactory>().CreateClient("dependencia");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("https://dependencia.invalid/teste")).StatusCode);
        Assert.Equal(3, getHandler.Chamadas);
        var postHandler = new RespostaControladaHandler(_ => HttpStatusCode.ServiceUnavailable);
        using var postApp = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient("dependencia").ConfigurePrimaryHttpMessageHandler(() => postHandler)));
        using var post = postApp.Services.GetRequiredService<IHttpClientFactory>().CreateClient("dependencia");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await post.PostAsync("https://dependencia.invalid/teste", null)).StatusCode);
        Assert.Equal(1, postHandler.Chamadas);
    }

    [SqlFact]
    public async Task Circuit_breaker_interrompe_novas_chamadas_apos_falhas()
    {
        var handler = new RespostaControladaHandler(_ => HttpStatusCode.ServiceUnavailable);
        using var isolated = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient("dependencia").ConfigurePrimaryHttpMessageHandler(() => handler)));
        using var client = isolated.Services.GetRequiredService<IHttpClientFactory>().CreateClient("dependencia");
        var opened = false;
        for (var i = 0; i < 6; i++)
        {
            try { using var response = await client.GetAsync("https://dependencia.invalid/teste"); }
            catch (Polly.CircuitBreaker.BrokenCircuitException) { opened = true; break; }
        }
        Assert.True(opened);
        var chamadasAoAbrir = handler.Chamadas;
        await Assert.ThrowsAnyAsync<Polly.CircuitBreaker.BrokenCircuitException>(() => client.GetAsync("https://dependencia.invalid/teste"));
        Assert.Equal(chamadasAoAbrir, handler.Chamadas);
    }
}
