using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.DTOs;
using Xunit;

namespace PortalTarefas.Tests;

public sealed class DomainTests
{
    [Fact]
    public void PrioridadeAltaExigePrazoEDescricaoUtil()
    {
        var input = new CriarTarefaDto { Titulo = "Preparar relatório", Equipe = "Geral", Prioridade = 3, Descricao = "  curta " };
        var erros = Validar(input);
        Assert.Contains(erros, e => e.MemberNames.Contains(nameof(TarefaInputModel.Prazo)));
        Assert.Contains(erros, e => e.MemberNames.Contains(nameof(TarefaInputModel.Descricao)));
    }

    [Fact]
    public void PrioridadeAltaAceitaPrazoEDescricaoCompleta()
    {
        var input = new CriarTarefaDto { Titulo = "Preparar relatório", Equipe = "Geral", Prioridade = 3,
            Descricao = "Conferir os requisitos e registrar resultados.", Prazo = DateOnly.FromDateTime(DateTime.Today).AddDays(1) };
        Assert.Empty(Validar(input));
    }

    [Fact]
    public void PrioridadeNormalPermiteTarefaSemPrazo()
    {
        Assert.Empty(Validar(new CriarTarefaDto { Titulo = "Investigar melhoria", Equipe = "Geral", Prioridade = 1 }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void PrioridadeForaDoIntervaloERejeitada(int prioridade)
    {
        var erros = Validar(new CriarTarefaDto { Titulo = "Tarefa inválida", Equipe = "Geral", Prioridade = prioridade });
        Assert.Contains(erros, e => e.MemberNames.Contains(nameof(TarefaInputModel.Prioridade)));
    }

    [Theory]
    [InlineData("Finalizada")]
    [InlineData("Pendente OR 1=1")]
    public void SituacaoPrecisaPertencerAoDominio(string situacao)
    {
        var erros = Validar(new CriarTarefaDto { Titulo = "Tarefa inválida", Equipe = "Geral", Situacao = situacao });
        Assert.Contains(erros, e => e.MemberNames.Contains(nameof(TarefaInputModel.Situacao)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("!!!!!!!!!!!!")]
    [InlineData("AQID")]
    [InlineData("AQIDBAUGBwgJCg==")]
    public void AtualizacaoRejeitaVersaoQueNaoRepresentaOitoBytes(string rowVersion)
    {
        var input = new AtualizarTarefaDto { Titulo = "Atualizar relatório", Equipe = "Geral", RowVersion = rowVersion };
        Assert.Contains(Validar(input), e => e.MemberNames.Contains(nameof(AtualizarTarefaDto.RowVersion)));
    }

    [Fact]
    public void AtualizacaoAceitaRowVersionSqlDeOitoBytes()
    {
        var input = new AtualizarTarefaDto { Titulo = "Atualizar relatório", Equipe = "Geral", RowVersion = "AQIDBAUGBwg=" };
        Assert.Empty(Validar(input));
    }

    [Fact]
    public void AtualizacaoMantemValidacaoCruzadaDoFormularioBase()
    {
        var input = new AtualizarTarefaDto { Titulo = "Atualizar relatório", Equipe = "Geral", Prioridade = 3,
            RowVersion = "AQIDBAUGBwg=" };
        var erros = Validar(input);
        Assert.Contains(erros, e => e.MemberNames.Contains(nameof(TarefaInputModel.Prazo)));
        Assert.Contains(erros, e => e.MemberNames.Contains(nameof(TarefaInputModel.Descricao)));
    }

    private static IReadOnlyList<ValidationResult> Validar(object input)
    {
        var erros = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), erros, true);
        return erros;
    }
}
