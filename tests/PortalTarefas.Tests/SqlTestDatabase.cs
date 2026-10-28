using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace PortalTarefas.Tests;

/// <summary>Recusa executar integrações em um banco que não seja dedicado aos testes.</summary>
public static partial class SqlTestDatabase
{
    [GeneratedRegex("^PortalTarefas_Tests_[A-Za-z0-9_]+$")]
    private static partial Regex NomePermitido();

    public static string ConnectionString => Validar(Environment.GetEnvironmentVariable("PORTAL_TEST_CONNECTION"));

    public static string Validar(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure PORTAL_TEST_CONNECTION para um banco exclusivo de testes.");
        var builder = new SqlConnectionStringBuilder(connection);
        if (!NomePermitido().IsMatch(builder.InitialCatalog) || !string.IsNullOrEmpty(builder.AttachDBFilename))
            throw new InvalidOperationException("Integração recusada: o banco deve se chamar PortalTarefas_Tests_<identificador>, sem AttachDBFilename.");
        return builder.ConnectionString;
    }
}

public sealed class SqlTestDatabaseGuardTests
{
    [Theory]
    [InlineData("Server=localhost;Database=PortalTarefas;Integrated Security=true")]
    [InlineData("Server=localhost;Database=master;Integrated Security=true")]
    [InlineData("Server=localhost;Database=PortalTarefas_Tests_;Integrated Security=true")]
    [InlineData("Server=localhost;Integrated Security=true")]
    public void RecusaBancoQueNaoEExclusivoDeTestes(string connection)
        => Assert.Throws<InvalidOperationException>(() => SqlTestDatabase.Validar(connection));

    [Fact]
    public void AceitaNomeExclusivoSemRevelarCredenciais()
        => Assert.Contains("PortalTarefas_Tests_guard", SqlTestDatabase.Validar(
            "Server=localhost;Database=PortalTarefas_Tests_guard;Integrated Security=true"));
}
