using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortalTarefas.Web.Data;
using PortalTarefas.Web.DTOs;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.Services;
using Xunit;

namespace PortalTarefas.Tests;

// O atributo mantém a ausência de infraestrutura visível no relatório de testes.
// A suíte não cria/remove bancos nem aplica migrações: a conexão é fornecida externamente.
public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORTAL_TEST_CONNECTION")))
            Skip = "SQL Server não configurado: forneça PORTAL_TEST_CONNECTION para um banco de verificação já migrado.";
    }
}

[CollectionDefinition("SqlIntegration", DisableParallelization = true)]
public sealed class SqlIntegrationCollection { }

[Collection("SqlIntegration")]
public sealed class SqlIntegrationTests
{
    [SqlFact]
    public async Task RowVersionRealImpedeSobrescritaEntreDoisContextosEAuditoriaDaFalha()
    {
        await using var primeiro = Contexto();
        await using var segundo = Contexto();
        var serviceA = new EfTarefaService(primeiro);
        var serviceB = new EfTarefaService(segundo);
        var usuario = Usuario();
        var criada = await serviceA.CriarAsync(Input("Concorrência SQL", usuario.Equipe), usuario);
        Assert.Equal(8, Convert.FromBase64String(criada.RowVersion).Length);
        var leituraAntiga = await serviceB.ObterAsync(criada.Id, usuario);
        Assert.NotNull(leituraAntiga);

        var revisada = await serviceA.AtualizarAsync(criada.Id, Input("Revisão confirmada SQL", usuario.Equipe), criada.RowVersion, usuario);
        Assert.NotEqual(criada.RowVersion, revisada.RowVersion);
        await Assert.ThrowsAsync<ConflitoException>(() => serviceB.AtualizarAsync(criada.Id,
            Input("Revisão obsoleta SQL", usuario.Equipe), leituraAntiga!.RowVersion, usuario));

        await using var verificacao = Contexto();
        var persistida = await verificacao.Tarefas.AsNoTracking().SingleAsync(t => t.Id == criada.Id);
        Assert.Equal(revisada.Titulo, persistida.Titulo);
        Assert.Equal(revisada.RowVersion, Convert.ToBase64String(persistida.RowVersion));
        var eventos = await verificacao.Auditorias.AsNoTracking().Where(a => a.TarefaId == criada.Id).OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(new[] { "Criar", "Atualizar" }, eventos.Select(a => a.Acao).ToArray());
        Assert.All(eventos, a => Assert.Equal(usuario.Id, a.UsuarioId));
    }

    [SqlFact]
    public async Task ExclusaoComVersaoObsoletaFazRollbackEAuditoriaSobreviveAExclusaoValida()
    {
        await using var primeiro = Contexto();
        await using var segundo = Contexto();
        var serviceA = new EfTarefaService(primeiro);
        var serviceB = new EfTarefaService(segundo);
        var usuario = Usuario();
        var admin = Usuario() with { Administrador = true, PodeEditarOutrasEquipes = true };
        var criada = await serviceA.CriarAsync(Input("Exclusão concorrente SQL", usuario.Equipe), usuario);
        var revisada = await serviceA.AtualizarAsync(criada.Id, Input("Exclusão após revisão SQL", usuario.Equipe), criada.RowVersion, usuario);
        await Assert.ThrowsAsync<ConflitoException>(() => serviceB.ExcluirAsync(criada.Id, criada.RowVersion, admin));

        await using (var verificacao = Contexto())
        {
            Assert.True(await verificacao.Tarefas.AnyAsync(t => t.Id == criada.Id));
            Assert.Equal(2, await verificacao.Auditorias.CountAsync(a => a.TarefaId == criada.Id));
        }
        await serviceB.ExcluirAsync(criada.Id, revisada.RowVersion, admin);
        await using var depois = Contexto();
        Assert.False(await depois.Tarefas.AnyAsync(t => t.Id == criada.Id));
        var eventos = await depois.Auditorias.AsNoTracking().Where(a => a.TarefaId == criada.Id).OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(new[] { "Criar", "Atualizar", "Excluir" }, eventos.Select(a => a.Acao).ToArray());
        Assert.Equal(admin.Id, eventos.Last().UsuarioId);
    }

    [SqlFact]
    public async Task PainelEfEAdoConcordamNosEscoposAnonimoEquipeEAdministrador()
    {
        await using var db = Contexto();
        var service = new EfTarefaService(db);
        var usuarioA = Usuario();
        var usuarioB = Usuario();
        var publica = Input("Pública para painel SQL", usuarioA.Equipe, publica: true);
        publica.Prazo = new DateOnly(2000, 1, 1);
        var tarefaPublica = await service.CriarAsync(publica, usuarioA);
        var privadaA = Input("Privada concluída SQL", usuarioA.Equipe);
        privadaA.Situacao = "Concluida";
        var tarefaA = await service.CriarAsync(privadaA, usuarioA);
        var tarefaB = await service.CriarAsync(Input("Privada da segunda equipe SQL", usuarioB.Equipe), usuarioB);
        var anonimo = new UsuarioContexto(null, "", false, false, false);
        var admin = Usuario() with { Administrador = true, PodeEditarOutrasEquipes = true };

        foreach (var contexto in new[] { anonimo, usuarioA, usuarioB, admin })
        {
            var ef = await service.PainelAsync(contexto);
            var ado = await service.PainelAdoAsync(contexto);
            Assert.Equal(ef, ado);
        }
        var painelPublico = await service.PainelAsync(anonimo);
        Assert.Equal(painelPublico.Total + 1, (await service.PainelAsync(usuarioA)).Total);
        Assert.Equal(painelPublico.Total + 1, (await service.PainelAsync(usuarioB)).Total);
        Assert.NotNull(await service.ObterAsync(tarefaPublica.Id, anonimo));
        Assert.Null(await service.ObterAsync(tarefaA.Id, anonimo));
        Assert.Null(await service.ObterAsync(tarefaB.Id, usuarioA));
        Assert.NotNull(await service.ObterAsync(tarefaB.Id, admin));
    }

    [SqlFact]
    public async Task PermissoesReaisProtegemEscritaEAtribuemProprietario()
    {
        await using var db = Contexto();
        var service = new EfTarefaService(db);
        var usuario = Usuario();
        var outraEquipe = Usuario();
        var colega = Usuario() with { Equipe = usuario.Equipe };
        var anonimo = new UsuarioContexto(null, "", false, false, false);
        var inicio = DateTimeOffset.UtcNow.AddSeconds(-1);
        var criada = await service.CriarAsync(Input("Tarefa com proprietário SQL", usuario.Equipe), usuario);
        Assert.Equal(usuario.Id, criada.ProprietarioId);
        Assert.Equal(usuario.Equipe, criada.Equipe);
        Assert.InRange(criada.CriadaEm, inicio, DateTimeOffset.UtcNow.AddSeconds(1));

        await Assert.ThrowsAsync<PermissaoException>(() => service.CriarAsync(Input("Criada sem login SQL", usuario.Equipe), anonimo));
        await Assert.ThrowsAsync<PermissaoException>(() => service.CriarAsync(Input("Criação fora da equipe SQL", outraEquipe.Equipe), usuario));
        await Assert.ThrowsAsync<PermissaoException>(() => service.AtualizarAsync(criada.Id,
            Input("Edição fora da equipe SQL", usuario.Equipe), criada.RowVersion, outraEquipe));
        await Assert.ThrowsAsync<PermissaoException>(() => service.ExcluirAsync(criada.Id, criada.RowVersion, usuario));
        var revisada = await service.AtualizarAsync(criada.Id, Input("Colega editou SQL", usuario.Equipe), criada.RowVersion, colega);
        var comClaim = outraEquipe with { PodeEditarOutrasEquipes = true };
        var revisadaPorClaim = await service.AtualizarAsync(criada.Id, Input("Claim autorizou SQL", usuario.Equipe), revisada.RowVersion, comClaim);
        Assert.Equal(usuario.Id, revisadaPorClaim.ProprietarioId);
        Assert.Equal(3, await db.Auditorias.CountAsync(a => a.TarefaId == criada.Id));

        var altaInvalida = Input("Alta sem prazo SQL", usuario.Equipe);
        altaInvalida.Prioridade = 3;
        await Assert.ThrowsAsync<ValidationException>(() => service.CriarAsync(altaInvalida, usuario));
    }

    [SqlFact]
    public async Task ContextoReutilizadoPeloPoolNaoCarregaEquipeDoUsuarioAnterior()
    {
        var services = new ServiceCollection();
        services.AddDbContextPool<PortalTarefasDbContext>(options => options.UseSqlServer(Conexao()), poolSize: 1);
        await using var provider = services.BuildServiceProvider();
        var usuarioA = Usuario();
        var usuarioB = Usuario();
        int idA;
        int idB;
        PortalTarefasDbContext primeiro;
        await using (var scope = provider.CreateAsyncScope())
        {
            primeiro = scope.ServiceProvider.GetRequiredService<PortalTarefasDbContext>();
            var service = new EfTarefaService(primeiro);
            idA = (await service.CriarAsync(Input("Privada para pool A SQL", usuarioA.Equipe), usuarioA)).Id;
            idB = (await service.CriarAsync(Input("Privada para pool B SQL", usuarioB.Equipe), usuarioB)).Id;
            Assert.NotNull(await service.ObterAsync(idA, usuarioA));
            Assert.Null(await service.ObterAsync(idB, usuarioA));
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var reutilizado = scope.ServiceProvider.GetRequiredService<PortalTarefasDbContext>();
            Assert.Same(primeiro, reutilizado);
            var service = new EfTarefaService(reutilizado);
            Assert.NotNull(await service.ObterAsync(idB, usuarioB));
            Assert.Null(await service.ObterAsync(idA, usuarioB));
            var painel = await service.PainelAsync(usuarioB);
            Assert.Equal(painel, await service.PainelAdoAsync(usuarioB));
        }
    }

    [SqlFact]
    public async Task SeedRepetidoNaoDuplicaContasPerfisOuClaimsNemRedefineSenha()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PortalTarefasDbContext>(options => options.UseSqlServer(Conexao()));
        services.AddIdentityCore<Usuario>().AddRoles<IdentityRole>().AddEntityFrameworkStores<PortalTarefasDbContext>();
        await using var provider = services.BuildServiceProvider();
        var sufixo = Guid.NewGuid().ToString("N");
        var adminEmail = $"admin-{sufixo}@example.test";
        var userEmail = $"user-{sufixo}@example.test";
        var senhaOriginal = SenhaAleatoria();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seed:AdminEmail"] = adminEmail,
            ["Seed:AdminPassword"] = senhaOriginal,
            ["Seed:AdminEquipe"] = $"Equipe-{sufixo}",
            ["Seed:UserEmail"] = userEmail,
            ["Seed:UserPassword"] = SenhaAleatoria(),
            ["Seed:UserEquipe"] = $"Equipe-{sufixo}",
            ["Seed:SampleData"] = "false"
        }).Build();
        await SeedData.InicializarAsync(provider, configuration);
        var outraSenha = SenhaAleatoria();
        configuration["Seed:AdminPassword"] = outraSenha;
        await SeedData.InicializarAsync(provider, configuration);

        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var admin = await users.FindByEmailAsync(adminEmail);
        var user = await users.FindByEmailAsync(userEmail);
        Assert.NotNull(admin);
        Assert.NotNull(user);
        Assert.Equal(2, await users.Users.CountAsync(u => u.Email == adminEmail || u.Email == userEmail));
        Assert.True(await roles.RoleExistsAsync("Administrador"));
        Assert.True(await roles.RoleExistsAsync("Usuario"));
        Assert.True(await users.IsInRoleAsync(admin!, "Administrador"));
        Assert.True(await users.IsInRoleAsync(user!, "Usuario"));
        Assert.Single((await users.GetClaimsAsync(admin!)).Where(c => c.Type == "tarefas:editar-outra-equipe" && c.Value == "true"));
        Assert.True(await users.CheckPasswordAsync(admin!, senhaOriginal));
        Assert.False(await users.CheckPasswordAsync(admin!, outraSenha));
    }

    private static PortalTarefasDbContext Contexto() => new(new DbContextOptionsBuilder<PortalTarefasDbContext>()
        .UseSqlServer(Conexao(), sql => sql.EnableRetryOnFailure(2)).Options);

    private static string Conexao() => Environment.GetEnvironmentVariable("PORTAL_TEST_CONNECTION")
        ?? throw new InvalidOperationException("PORTAL_TEST_CONNECTION não foi configurada.");

    private static UsuarioContexto Usuario()
    {
        var id = Guid.NewGuid().ToString("N");
        return new($"teste-{id}", $"Equipe-{id}", true, false, false);
    }

    private static TarefaInputModel Input(string titulo, string equipe, bool publica = false) =>
        new() { Titulo = $"{titulo} {Guid.NewGuid():N}", Equipe = equipe, Publica = publica,
            Situacao = "Pendente", Prioridade = 1, Descricao = "Registro criado por teste de integração com SQL Server." };

    private static string SenhaAleatoria() => $"aA1!{Guid.NewGuid():N}";
}
