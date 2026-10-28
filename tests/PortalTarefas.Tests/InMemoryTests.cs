using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.DTOs;
using PortalTarefas.Web.Services;
using Xunit;

namespace PortalTarefas.Tests;

public sealed class InMemoryTests
{
    private static readonly UsuarioContexto Alice = new("alice", "Equipe A", true, false, false);
    private static readonly UsuarioContexto Bruno = new("bruno", "Equipe B", true, false, false);
    private static readonly UsuarioContexto Colega = new("colega", "Equipe A", true, false, false);
    private static readonly UsuarioContexto Admin = new("admin", "Geral", true, true, true);
    private static readonly UsuarioContexto Visitante = new(null, "", false, false, false);

    [Fact]
    public async Task VisibilidadeRespeitaPublicacaoEquipeEAdministrador()
    {
        var service = new InMemoryTarefaService();
        var privadaA = await service.CriarAsync(Input("Privada da equipe A"), Alice);
        var publicaB = await service.CriarAsync(Input("Pública da equipe B", "Equipe B", publica: true), Bruno);
        var privadaB = await service.CriarAsync(Input("Privada da equipe B", "Equipe B"), Bruno);

        var anonimo = await service.ListarAsync(new(), Visitante);
        Assert.Equal(publicaB.Id, Assert.Single(anonimo.Itens).Id);
        var equipe = await service.ListarAsync(new(), Colega);
        Assert.Equal(2, equipe.Total);
        Assert.Contains(equipe.Itens, t => t.Id == privadaA.Id);
        Assert.DoesNotContain(equipe.Itens, t => t.Id == privadaB.Id);
        Assert.Equal(3, (await service.ListarAsync(new(), Admin)).Total);
        Assert.Null(await service.ObterAsync(privadaB.Id, Alice));
        Assert.Null(await service.ObterAsync(privadaA.Id, Visitante));
        Assert.NotNull(await service.ObterAsync(privadaA.Id, Colega));
    }

    [Fact]
    public async Task IndicadoresUsamAMesmaVisibilidadeDaLista()
    {
        var service = new InMemoryTarefaService();
        var vencida = Input("Publicada e vencida", publica: true);
        vencida.Prazo = new DateOnly(2000, 1, 1);
        await service.CriarAsync(vencida, Alice);
        var concluida = Input("Concluída e vencida", publica: true);
        concluida.Prazo = new DateOnly(2000, 1, 1);
        concluida.Situacao = "Concluida";
        await service.CriarAsync(concluida, Alice);
        await service.CriarAsync(Input("Privada da equipe A"), Alice);
        Assert.Equal(new PainelDto(2, 1, 1, 1), await service.PainelAsync(Visitante));
        Assert.Equal(new PainelDto(3, 2, 1, 1), await service.PainelAsync(Alice));
        var lista = await service.ListarAsync(new() { Vencidas = true }, Visitante);
        Assert.Equal("Publicada e vencida", Assert.Single(lista.Itens).Titulo);
    }

    [Fact]
    public async Task FiltrosEOrdenacaoSaoAplicadosAntesDaPaginacao()
    {
        var service = new InMemoryTarefaService();
        foreach (var titulo in new[] { "Entrega Charlie", "Entrega Alpha", "Entrega Bravo" })
            await service.CriarAsync(Input(titulo), Alice);
        await service.CriarAsync(Input("Outra tarefa"), Alice);
        var pagina = await service.ListarAsync(new() { Busca = "entrega", OrdenarPor = "titulo", Direcao = "asc", Pagina = 2, Tamanho = 2 }, Alice);
        Assert.Equal(3, pagina.Total);
        Assert.Equal(2, pagina.TotalPaginas);
        Assert.Equal("Entrega Charlie", Assert.Single(pagina.Itens).Titulo);
        var descendente = await service.ListarAsync(new() { Busca = "Entrega", OrdenarPor = "titulo", Direcao = "desc", Tamanho = 2 }, Alice);
        Assert.Equal(new[] { "Entrega Charlie", "Entrega Bravo" }, descendente.Itens.Select(t => t.Titulo).ToArray());
    }

    [Theory]
    [InlineData("titulo desc; DROP TABLE Tarefas", "asc", 10)]
    [InlineData("titulo", "aleatoria", 10)]
    [InlineData("titulo", "asc", 51)]
    public async Task OrdenacaoEPaginacaoInvalidasSaoRejeitadas(string campo, string direcao, int tamanho)
    {
        var service = new InMemoryTarefaService();
        await Assert.ThrowsAsync<ValidationException>(() => service.ListarAsync(new()
            { OrdenarPor = campo, Direcao = direcao, Tamanho = tamanho }, Visitante));
    }

    [Fact]
    public async Task PaginaMuitoAltaNaoTransbordaNemRetornaPrimeiraPagina()
    {
        var service = new InMemoryTarefaService();
        await service.CriarAsync(Input("Uma tarefa visível", publica: true), Alice);
        var pagina = await service.ListarAsync(new() { Pagina = int.MaxValue, Tamanho = 50 }, Visitante);
        Assert.Empty(pagina.Itens);
        Assert.Equal(1, pagina.Total);
    }

    [Fact]
    public async Task CriacaoNaoPermiteEscolherEquipeDeTerceiros()
    {
        var service = new InMemoryTarefaService();
        await Assert.ThrowsAsync<PermissaoException>(() => service.CriarAsync(Input("Tarefa sem login"), Visitante));
        await Assert.ThrowsAsync<PermissaoException>(() => service.CriarAsync(Input("Equipe indevida", "Equipe B"), Alice));
        var criada = await service.CriarAsync(Input("Alocação administrativa", "Equipe B"), Admin);
        Assert.Equal("Equipe B", criada.Equipe);
        Assert.Equal(Admin.Id, criada.ProprietarioId);
        Assert.Single(service.ConsultarAuditoria());
    }

    [Fact]
    public async Task EdicaoEntreEquipesExigeClaimMesmoEmTarefaPublica()
    {
        var service = new InMemoryTarefaService();
        var criada = await service.CriarAsync(Input("Tarefa pública", publica: true), Alice);
        await Assert.ThrowsAsync<PermissaoException>(() => service.AtualizarAsync(criada.Id,
            Input("Tentativa indevida", "Equipe A", publica: true), criada.RowVersion, Bruno));
        var autorizado = Bruno with { PodeEditarOutrasEquipes = true };
        var atualizada = await service.AtualizarAsync(criada.Id, Input("Edição autorizada", "Equipe A", publica: true), criada.RowVersion, autorizado);
        Assert.Equal("Edição autorizada", atualizada.Titulo);
    }

    [Fact]
    public async Task MembroDaMesmaEquipePodeEditarMasNaoExcluir()
    {
        var service = new InMemoryTarefaService();
        var criada = await service.CriarAsync(Input("Tarefa compartilhada"), Alice);
        var atualizada = await service.AtualizarAsync(criada.Id, Input("Revisão do colega"), criada.RowVersion, Colega);
        Assert.Equal("Revisão do colega", atualizada.Titulo);
        await Assert.ThrowsAsync<PermissaoException>(() => service.ExcluirAsync(criada.Id, atualizada.RowVersion, Colega));
        Assert.NotNull(await service.ObterAsync(criada.Id, Alice));
    }

    [Fact]
    public async Task VersaoObsoletaNaoSobrescreveNemProduzAuditoriaDeSucesso()
    {
        var service = new InMemoryTarefaService();
        var criada = await service.CriarAsync(Input("Texto inicial"), Alice);
        var atualizada = await service.AtualizarAsync(criada.Id, Input("Texto já revisado"), criada.RowVersion, Colega);
        Assert.NotEqual(criada.RowVersion, atualizada.RowVersion);
        await Assert.ThrowsAsync<ConflitoException>(() => service.AtualizarAsync(criada.Id,
            Input("Texto vindo de aba antiga"), criada.RowVersion, Alice));
        Assert.Equal("Texto já revisado", (await service.ObterAsync(criada.Id, Alice))!.Titulo);
        Assert.Equal(new[] { "Criar", "Atualizar" }, service.ConsultarAuditoria().Select(a => a.Acao).ToArray());
    }

    [Fact]
    public async Task DuasEdicoesConcorrentesDaMesmaVersaoTemSomenteUmVencedor()
    {
        var service = new InMemoryTarefaService();
        var criada = await service.CriarAsync(Input("Revisão concorrente"), Alice);
        async Task<bool> Tentar(string titulo)
        {
            try { await service.AtualizarAsync(criada.Id, Input(titulo), criada.RowVersion, Alice); return true; }
            catch (ConflitoException) { return false; }
        }
        var resultados = await Task.WhenAll(Task.Run(() => Tentar("Primeira revisão")), Task.Run(() => Tentar("Segunda revisão")));
        Assert.Equal(1, resultados.Count(sucesso => sucesso));
        Assert.Equal(2, service.ConsultarAuditoria().Count);
    }

    [Fact]
    public async Task ExclusaoAdministrativaTambemRespeitaVersaoEPreservaHistorico()
    {
        var service = new InMemoryTarefaService();
        var criada = await service.CriarAsync(Input("Tarefa para exclusão"), Alice);
        var atualizada = await service.AtualizarAsync(criada.Id, Input("Tarefa atualizada"), criada.RowVersion, Alice);
        await Assert.ThrowsAsync<ConflitoException>(() => service.ExcluirAsync(criada.Id, criada.RowVersion, Admin));
        Assert.NotNull(await service.ObterAsync(criada.Id, Admin));
        await service.ExcluirAsync(criada.Id, atualizada.RowVersion, Admin);
        Assert.Null(await service.ObterAsync(criada.Id, Admin));
        var evento = service.ConsultarAuditoria().Last();
        Assert.Equal("Excluir", evento.Acao);
        Assert.Equal(criada.Id, evento.TarefaId);
        Assert.Equal(Admin.Id, evento.UsuarioId);
    }

    [Fact]
    public async Task ValidacaoCruzadaTambemProtegeChamadasDiretasAoServico()
    {
        var service = new InMemoryTarefaService();
        var input = Input("Urgente sem contexto");
        input.Prioridade = 3;
        await Assert.ThrowsAsync<ValidationException>(() => service.CriarAsync(input, Alice));
        Assert.Equal(0, (await service.ListarAsync(new(), Admin)).Total);
        Assert.Empty(service.ConsultarAuditoria());
    }

    [Fact]
    public async Task CargaDeExemplosPodeSerRepetidaSemDuplicarTarefas()
    {
        var service = new InMemoryTarefaService();
        service.PopularExemplos();
        var primeira = await service.ListarAsync(new(), Visitante);
        service.PopularExemplos();
        var segunda = await service.ListarAsync(new(), Visitante);
        Assert.NotEmpty(primeira.Itens);
        Assert.Equal(primeira.Total, segunda.Total);
    }

    [Fact]
    public async Task ModoMemoryNaoDeclaraTerExecutadoConsultaAdo()
    {
        var service = new InMemoryTarefaService();
        await Assert.ThrowsAsync<NotSupportedException>(() => service.PainelAdoAsync(Visitante));
    }

    private static TarefaInputModel Input(string titulo, string equipe = "Equipe A", bool publica = false) =>
        new() { Titulo = titulo, Equipe = equipe, Publica = publica, Situacao = "Pendente", Prioridade = 1 };
}
