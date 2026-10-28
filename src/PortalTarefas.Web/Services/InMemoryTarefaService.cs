using System.Buffers.Binary;
using PortalTarefas.Web.DTOs;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Services;

// Modo de demonstração explícito. Os dados duram somente enquanto o processo está ativo.
// Registrar como singleton; todas as operações sobre os dados usam o mesmo lock.
public sealed class InMemoryTarefaService : ITarefaService
{
    private readonly object sync = new();
    private readonly Dictionary<int, Tarefa> tarefas = [];
    private readonly List<Auditoria> auditorias = [];
    private int proximoId;
    private ulong versao;

    public Task<PaginaDto<TarefaResumoDto>> ListarAsync(TarefaConsulta consulta, UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TarefaRegras.Validar(consulta);
        lock (sync)
        {
            IEnumerable<Tarefa> query = tarefas.Values.Where(t => TarefaRegras.PodeVer(t, usuario));
            if (!string.IsNullOrWhiteSpace(consulta.Busca))
            {
                var busca = consulta.Busca.Trim();
                query = query.Where(t => t.Titulo.Contains(busca, StringComparison.OrdinalIgnoreCase) ||
                    t.Descricao?.Contains(busca, StringComparison.OrdinalIgnoreCase) == true);
            }
            if (!string.IsNullOrWhiteSpace(consulta.Situacao)) query = query.Where(t => t.Situacao == consulta.Situacao);
            if (consulta.Prioridade.HasValue) query = query.Where(t => t.Prioridade == consulta.Prioridade);
            if (consulta.Vencidas) query = query.Where(t => t.Prazo < TarefaRegras.Hoje && t.Situacao != "Concluida");
            var filtered = query.ToArray();
            var desc = consulta.Direcao == "desc";
            IOrderedEnumerable<Tarefa> ordered = consulta.OrdenarPor switch
            {
                "titulo" => desc ? filtered.OrderByDescending(t => t.Titulo, StringComparer.OrdinalIgnoreCase) : filtered.OrderBy(t => t.Titulo, StringComparer.OrdinalIgnoreCase),
                "prioridade" => desc ? filtered.OrderByDescending(t => t.Prioridade) : filtered.OrderBy(t => t.Prioridade),
                "situacao" => desc ? filtered.OrderByDescending(t => t.Situacao) : filtered.OrderBy(t => t.Situacao),
                "criadaem" => desc ? filtered.OrderByDescending(t => t.CriadaEm) : filtered.OrderBy(t => t.CriadaEm),
                "id" => desc ? filtered.OrderByDescending(t => t.Id) : filtered.OrderBy(t => t.Id),
                _ => desc ? filtered.OrderByDescending(t => t.Prazo) : filtered.OrderBy(t => t.Prazo)
            };
            var offset = Math.Min((long)(consulta.Pagina - 1) * consulta.Tamanho, int.MaxValue);
            var itens = ordered.ThenBy(t => t.Id).Skip((int)offset).Take(consulta.Tamanho).Select(TarefaRegras.Resumo).ToArray();
            return Task.FromResult(new PaginaDto<TarefaResumoDto>(itens, consulta.Pagina, consulta.Tamanho,
                filtered.Length, (int)Math.Ceiling((double)filtered.Length / consulta.Tamanho)));
        }
    }

    public Task<TarefaDetalheDto?> ObterAsync(int id, UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            var existe = tarefas.TryGetValue(id, out var tarefa) && TarefaRegras.PodeVer(tarefa, usuario);
            return Task.FromResult(existe ? TarefaRegras.Detalhe(tarefa!) : null);
        }
    }

    public Task<TarefaDetalheDto> CriarAsync(TarefaInputModel input, UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TarefaRegras.Validar(input);
        TarefaRegras.ExigirCriacao(input, usuario);
        lock (sync)
        {
            var tarefa = new Tarefa { Id = ++proximoId, ProprietarioId = usuario.Id, RowVersion = ProximaVersao() };
            TarefaRegras.Aplicar(tarefa, input);
            tarefas.Add(tarefa.Id, tarefa);
            Auditar("Criar", tarefa.Id, usuario.Id);
            return Task.FromResult(TarefaRegras.Detalhe(tarefa));
        }
    }

    public Task<TarefaDetalheDto> AtualizarAsync(int id, TarefaInputModel input, string rowVersion, UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TarefaRegras.Validar(input);
        TarefaRegras.ExigirAutenticacao(usuario);
        var original = TarefaRegras.LerVersao(rowVersion);
        lock (sync)
        {
            var tarefa = ObterObrigatoria(id);
            TarefaRegras.ExigirEdicao(tarefa, input, usuario);
            ConferirVersao(tarefa, original);
            TarefaRegras.Aplicar(tarefa, input);
            tarefa.RowVersion = ProximaVersao();
            Auditar("Atualizar", id, usuario.Id);
            return Task.FromResult(TarefaRegras.Detalhe(tarefa));
        }
    }

    public Task ExcluirAsync(int id, string rowVersion, UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TarefaRegras.ExigirExclusao(usuario);
        var original = TarefaRegras.LerVersao(rowVersion);
        lock (sync)
        {
            var tarefa = ObterObrigatoria(id);
            ConferirVersao(tarefa, original);
            tarefas.Remove(id);
            Auditar("Excluir", id, usuario.Id);
        }
        return Task.CompletedTask;
    }

    public Task<PainelDto> PainelAsync(UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            var visiveis = tarefas.Values.Where(t => TarefaRegras.PodeVer(t, usuario)).ToArray();
            var hoje = TarefaRegras.Hoje;
            return Task.FromResult(new PainelDto(visiveis.Length, visiveis.Count(t => t.Situacao == "Pendente"),
                visiveis.Count(t => t.Situacao == "Concluida"), visiveis.Count(t => t.Prazo < hoje && t.Situacao != "Concluida")));
        }
    }

    public Task<PainelDto> PainelAdoAsync(UsuarioContexto usuario, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("O painel ADO.NET exige o modo SqlServer. O modo Memory não possui conexão relacional.");

    public IReadOnlyList<Auditoria> ConsultarAuditoria()
    {
        lock (sync)
            return auditorias.Select(a => new Auditoria { Id = a.Id, Acao = a.Acao, TarefaId = a.TarefaId,
                UsuarioId = a.UsuarioId, Instante = a.Instante }).ToArray();
    }

    public void PopularExemplos()
    {
        lock (sync)
        {
            if (tarefas.Count > 0) return;
            foreach (var input in DadosExemplo.Criar())
            {
                var tarefa = new Tarefa { Id = ++proximoId, RowVersion = ProximaVersao() };
                TarefaRegras.Aplicar(tarefa, input);
                tarefas.Add(tarefa.Id, tarefa);
                Auditar("CargaInicial", tarefa.Id, null);
            }
        }
    }

    private Tarefa ObterObrigatoria(int id) => tarefas.TryGetValue(id, out var tarefa) ? tarefa : throw new RecursoNaoEncontradoException();

    private static void ConferirVersao(Tarefa tarefa, byte[] original)
    {
        if (!tarefa.RowVersion.AsSpan().SequenceEqual(original)) throw new ConflitoException();
    }

    private byte[] ProximaVersao()
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, ++versao);
        return bytes;
    }

    private void Auditar(string acao, int id, string? usuarioId) => auditorias.Add(new Auditoria
        { Id = auditorias.Count + 1L, Acao = acao, TarefaId = id, UsuarioId = usuarioId });
}
