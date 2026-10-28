using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.Services;

namespace PortalTarefas.Web.Data;

public static class SeedData
{
    public static async Task InicializarAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        foreach (var role in new[] { "Usuario", "Administrador" })
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await roles.RoleExistsAsync(role)) Verificar(await roles.CreateAsync(new IdentityRole(role)), "criar o perfil");
        }
        var admin = await GarantirUsuario(users, configuration, "Admin", "Administrador", cancellationToken);
        await GarantirUsuario(users, configuration, "User", "Usuario", cancellationToken);
        if (admin is not null)
        {
            var claims = await users.GetClaimsAsync(admin);
            if (!claims.Any(c => c.Type == "tarefas:editar-outra-equipe" && c.Value == "true"))
                Verificar(await users.AddClaimAsync(admin, new Claim("tarefas:editar-outra-equipe", "true")), "atribuir permissão administrativa");
        }
        if (configuration.GetValue<bool>("Seed:SampleData"))
            await PopularExemplosAsync(scope.ServiceProvider.GetRequiredService<PortalTarefasDbContext>(), cancellationToken);
    }

    private static async Task<Usuario?> GarantirUsuario(UserManager<Usuario> users, IConfiguration configuration,
        string prefixo, string role, CancellationToken cancellationToken)
    {
        var email = configuration[$"Seed:{prefixo}Email"];
        var senha = configuration[$"Seed:{prefixo}Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrEmpty(senha)) return null;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(senha))
            throw new InvalidOperationException($"Configure Seed:{prefixo}Email e Seed:{prefixo}Password juntos em uma fonte externa de segredos.");
        cancellationToken.ThrowIfCancellationRequested();
        var usuario = await users.FindByEmailAsync(email);
        if (usuario is null)
        {
            usuario = new Usuario { UserName = email, Email = email, EmailConfirmed = true,
                Equipe = configuration[$"Seed:{prefixo}Equipe"] ?? "Geral" };
            Verificar(await users.CreateAsync(usuario, senha), "criar o usuário de carga inicial");
        }
        if (!await users.IsInRoleAsync(usuario, role)) Verificar(await users.AddToRoleAsync(usuario, role), "atribuir perfil");
        return usuario;
    }

    private static async Task PopularExemplosAsync(PortalTarefasDbContext db, CancellationToken cancellationToken)
    {
        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            if (!await db.Tarefas.AnyAsync(cancellationToken))
            {
                var exemplos = DadosExemplo.Criar().Select(input =>
                {
                    var tarefa = new Tarefa();
                    TarefaRegras.Aplicar(tarefa, input);
                    return tarefa;
                }).ToArray();
                db.Tarefas.AddRange(exemplos);
                await db.SaveChangesAsync(cancellationToken);
                db.Auditorias.AddRange(exemplos.Select(t => new Auditoria { Acao = "CargaInicial", TarefaId = t.Id }));
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private static void Verificar(IdentityResult result, string operacao)
    {
        // Somente códigos públicos de validação; credenciais nunca são registradas.
        if (!result.Succeeded) throw new InvalidOperationException($"Falha ao {operacao}: {string.Join(", ", result.Errors.Select(e => e.Code))}.");
    }
}
