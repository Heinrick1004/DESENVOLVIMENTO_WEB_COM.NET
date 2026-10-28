using PortalTarefas.Web.DTOs;
using PortalTarefas.Web.Services;

namespace PortalTarefas.Web.ViewModels;

public sealed record TarefasListaViewModel(TarefaConsulta Consulta, PaginaDto<TarefaResumoDto> Resultado);

public static class TarefaApresentacao
{
    public static string Situacao(string value) => value switch
    {
        "EmAndamento" => "Em andamento",
        "Concluida" => "Concluída",
        _ => "Pendente"
    };

    public static string Prioridade(int value) => value switch
    {
        3 => "Alta",
        2 => "Média",
        _ => "Baixa"
    };

    // Apenas controla a apresentação. O serviço valida novamente toda gravação.
    public static bool PodeEditar(TarefaDetalheDto tarefa, UsuarioContexto usuario) =>
        usuario.Autenticado && (tarefa.ProprietarioId == usuario.Id ||
            string.Equals(tarefa.Equipe, usuario.Equipe, StringComparison.OrdinalIgnoreCase) ||
            usuario.PodeEditarOutrasEquipes);
}
