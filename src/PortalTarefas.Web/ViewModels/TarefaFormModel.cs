using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.DTOs;

namespace PortalTarefas.Web.ViewModels;

public sealed class TarefaFormModel : TarefaInputModel
{
    public int Id { get; set; }

    [Display(Name = "Versão da tarefa")]
    // O navegador envia o campo oculto vazio no cadastro. Somente Edit exige versão.
    public string? RowVersion { get; set; }

    public static TarefaFormModel De(TarefaDetalheDto tarefa) => new()
    {
        Id = tarefa.Id,
        Titulo = tarefa.Titulo,
        Descricao = tarefa.Descricao,
        Situacao = tarefa.Situacao,
        Prioridade = tarefa.Prioridade,
        Prazo = tarefa.Prazo,
        Equipe = tarefa.Equipe,
        Publica = tarefa.Publica,
        RowVersion = tarefa.RowVersion
    };
}
