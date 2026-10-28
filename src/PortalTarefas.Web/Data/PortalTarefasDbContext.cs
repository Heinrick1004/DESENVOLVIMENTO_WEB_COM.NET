using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Data;

// Somente options no construtor: compatível com AddDbContextPool, sem estado do usuário.
public sealed class PortalTarefasDbContext(DbContextOptions<PortalTarefasDbContext> options)
    : IdentityDbContext<Usuario>(options)
{
    public DbSet<Tarefa> Tarefas => Set<Tarefa>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Usuario>().Property(x => x.Equipe).HasMaxLength(80).IsRequired();
        builder.Entity<Tarefa>(tarefa =>
        {
            tarefa.ToTable("Tarefas", table =>
            {
                table.HasCheckConstraint("CK_Tarefas_Prioridade", "[Prioridade] BETWEEN 1 AND 3");
                table.HasCheckConstraint("CK_Tarefas_Situacao", "[Situacao] IN ('Pendente','EmAndamento','Concluida')");
            });
            tarefa.HasKey(x => x.Id);
            tarefa.Property(x => x.Titulo).HasMaxLength(120).IsRequired();
            tarefa.Property(x => x.Descricao).HasMaxLength(2000);
            tarefa.Property(x => x.Situacao).HasMaxLength(20).IsRequired();
            tarefa.Property(x => x.Equipe).HasMaxLength(80).IsRequired();
            tarefa.Property(x => x.ProprietarioId).HasMaxLength(450);
            tarefa.Property(x => x.Prazo).HasColumnType("date");
            tarefa.Property(x => x.RowVersion).IsRowVersion();
            tarefa.HasIndex(x => new { x.Equipe, x.Situacao, x.Prazo });
            tarefa.HasIndex(x => new { x.Publica, x.Prazo });
            tarefa.HasIndex(x => x.ProprietarioId);
        });
        builder.Entity<Auditoria>(audit =>
        {
            audit.ToTable("Auditorias");
            audit.HasKey(x => x.Id);
            audit.Property(x => x.Acao).HasMaxLength(80).IsRequired();
            audit.Property(x => x.UsuarioId).HasMaxLength(450);
            audit.HasIndex(x => new { x.TarefaId, x.Instante });
        });
    }
}
