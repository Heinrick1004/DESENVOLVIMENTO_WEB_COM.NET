using PortalTarefas.Web.DTOs;

namespace PortalTarefas.Web.Services;

internal static class DadosExemplo
{
    internal static TarefaInputModel[] Criar() =>
    [
        new() { Titulo = "Preparar apresentação do projeto", Descricao = "Organizar os resultados e as evidências dos roteiros.",
            Equipe = "Geral", Situacao = "EmAndamento", Prioridade = 3, Prazo = TarefaRegras.Hoje.AddDays(3), Publica = true },
        new() { Titulo = "Revisar os requisitos acadêmicos", Descricao = "Conferir a matriz de requisitos e atualizar o relatório.",
            Equipe = "Geral", Situacao = "Pendente", Prioridade = 2, Prazo = TarefaRegras.Hoje.AddDays(-1), Publica = true },
        new() { Titulo = "Configurar o ambiente de desenvolvimento", Descricao = "Validar os pré-requisitos e registrar a versão do SDK.",
            Equipe = "Geral", Situacao = "Concluida", Prioridade = 1, Prazo = TarefaRegras.Hoje, Publica = true }
    ];
}
