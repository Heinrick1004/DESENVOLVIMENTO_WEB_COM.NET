namespace PortalTarefas.Tests;

// Exclusivo da branch experimental: comprova que uma falha impede empacotamento.
public sealed class CiPublicationGateTests
{
    [Fact]
    public void FalhaControladaDeveInterromperPublicacao()
    {
        Assert.Fail("Falha intencional autorizavel do ensaio de bloqueio CI; remover apos a verificacao.");
    }
}
