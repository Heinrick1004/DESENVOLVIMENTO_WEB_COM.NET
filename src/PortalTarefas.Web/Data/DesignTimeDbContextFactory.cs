using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PortalTarefas.Web.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PortalTarefasDbContext>
{
    public PortalTarefasDbContext CreateDbContext(string[] args)
    {
        // Criar uma migração não exige conexão. Aplicá-la exige configuração real externa.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__PortalTarefas")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=PortalTarefas;Trusted_Connection=True;TrustServerCertificate=True";
        return new(new DbContextOptionsBuilder<PortalTarefasDbContext>().UseSqlServer(connection).Options);
    }
}
