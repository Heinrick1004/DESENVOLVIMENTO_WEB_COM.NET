using PortalTarefas.Web.DTOs;

namespace PortalTarefas.Web.Services;

public sealed record UsuarioContexto(string? Id, string Equipe, bool Autenticado,
    bool Administrador, bool PodeEditarOutrasEquipes);

public interface ITarefaService
{
    Task<PaginaDto<TarefaResumoDto>> ListarAsync(TarefaConsulta consulta, UsuarioContexto usuario, CancellationToken cancellationToken = default);
    Task<TarefaDetalheDto?> ObterAsync(int id, UsuarioContexto usuario, CancellationToken cancellationToken = default);
    Task<TarefaDetalheDto> CriarAsync(TarefaInputModel input, UsuarioContexto usuario, CancellationToken cancellationToken = default);
    Task<TarefaDetalheDto> AtualizarAsync(int id, TarefaInputModel input, string rowVersion, UsuarioContexto usuario, CancellationToken cancellationToken = default);
    Task ExcluirAsync(int id, string rowVersion, UsuarioContexto usuario, CancellationToken cancellationToken = default);
    Task<PainelDto> PainelAsync(UsuarioContexto usuario, CancellationToken cancellationToken = default);
    Task<PainelDto> PainelAdoAsync(UsuarioContexto usuario, CancellationToken cancellationToken = default);
}

public sealed class RecursoNaoEncontradoException(string message = "Tarefa não encontrada.") : Exception(message) { }
public sealed class PermissaoException(string message = "Você não tem permissão para executar esta ação.") : Exception(message) { }
public sealed class ConflitoException(string message = "Esta tarefa foi alterada por outra pessoa. Recarregue os dados antes de salvar.") : Exception(message) { }
