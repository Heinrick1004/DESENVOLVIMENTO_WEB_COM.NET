using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Data;
using PortalTarefas.Web.DTOs;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Services;

public sealed class EfTarefaService(PortalTarefasDbContext db) : ITarefaService
{
    private IQueryable<Tarefa> Visiveis(UsuarioContexto usuario)
    {
        var autenticado = usuario.Autenticado;
        var admin = usuario.Administrador;
        var equipe = usuario.Equipe;
        return db.Tarefas.AsNoTracking().Where(t => t.Publica || (autenticado && (admin || t.Equipe == equipe)));
    }

    public async Task<PaginaDto<TarefaResumoDto>> ListarAsync(TarefaConsulta consulta, UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        TarefaRegras.Validar(consulta);
        var query = Visiveis(usuario);
        if (!string.IsNullOrWhiteSpace(consulta.Busca))
        {
            var busca = consulta.Busca.Trim();
            query = query.Where(t => t.Titulo.Contains(busca) || (t.Descricao != null && t.Descricao.Contains(busca)));
        }
        if (!string.IsNullOrEmpty(consulta.Situacao)) query = query.Where(t => t.Situacao == consulta.Situacao);
        if (consulta.Prioridade.HasValue) query = query.Where(t => t.Prioridade == consulta.Prioridade.Value);
        if (consulta.Vencidas)
        {
            var hoje = TarefaRegras.Hoje;
            query = query.Where(t => t.Prazo < hoje && t.Situacao != "Concluida");
        }
        var total = await query.CountAsync(cancellationToken);
        var desc = consulta.Direcao == "desc";
        // A ordenação é uma lista de expressões permitidas; nenhum nome entra em SQL dinâmico.
        IOrderedQueryable<Tarefa> ordered = consulta.OrdenarPor switch
        {
            "titulo" => desc ? query.OrderByDescending(t => t.Titulo) : query.OrderBy(t => t.Titulo),
            "prioridade" => desc ? query.OrderByDescending(t => t.Prioridade) : query.OrderBy(t => t.Prioridade),
            "situacao" => desc ? query.OrderByDescending(t => t.Situacao) : query.OrderBy(t => t.Situacao),
            "criadaem" => desc ? query.OrderByDescending(t => t.CriadaEm) : query.OrderBy(t => t.CriadaEm),
            "id" => desc ? query.OrderByDescending(t => t.Id) : query.OrderBy(t => t.Id),
            _ => desc ? query.OrderByDescending(t => t.Prazo) : query.OrderBy(t => t.Prazo)
        };
        var offset = Math.Min((long)(consulta.Pagina - 1) * consulta.Tamanho, int.MaxValue);
        var itens = await ordered.ThenBy(t => t.Id).Skip((int)offset).Take(consulta.Tamanho)
            .Select(t => new TarefaResumoDto(t.Id, t.Titulo, t.Descricao, t.Situacao, t.Prioridade,
                t.Prazo, t.Equipe, t.Publica, t.ProprietarioId)).ToListAsync(cancellationToken);
        return new(itens, consulta.Pagina, consulta.Tamanho, total, (int)Math.Ceiling((double)total / consulta.Tamanho));
    }

    public async Task<TarefaDetalheDto?> ObterAsync(int id, UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        var tarefa = await Visiveis(usuario).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        return tarefa is null ? null : TarefaRegras.Detalhe(tarefa);
    }

    public async Task<TarefaDetalheDto> CriarAsync(TarefaInputModel input, UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        TarefaRegras.Validar(input);
        TarefaRegras.ExigirCriacao(input, usuario);
        Tarefa? tentativaAnterior = null;
        var estrategia = db.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync(async () =>
        {
            if (tentativaAnterior is not null)
            {
                db.ChangeTracker.Clear();
                // Se a conexão caiu após o COMMIT, recuperar o recurso confirmado evita duplicar a criação.
                var anterior = await db.Tarefas.AsNoTracking().SingleOrDefaultAsync(t =>
                    t.Id == tentativaAnterior.Id && t.CriadaEm == tentativaAnterior.CriadaEm, cancellationToken);
                if (anterior is not null && await db.Auditorias.AnyAsync(a => a.TarefaId == anterior.Id &&
                    a.Acao == "Criar" && a.UsuarioId == usuario.Id, cancellationToken))
                    return TarefaRegras.Detalhe(anterior);
            }
            var tarefa = new Tarefa { ProprietarioId = usuario.Id };
            tentativaAnterior = tarefa;
            TarefaRegras.Aplicar(tarefa, input);
            // O primeiro SaveChanges obtém o ID, o segundo grava o evento: ambos na mesma transação.
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.Tarefas.Add(tarefa);
            await db.SaveChangesAsync(cancellationToken);
            db.Auditorias.Add(Evento("Criar", tarefa.Id, usuario));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TarefaRegras.Detalhe(tarefa);
        });
    }

    public async Task<TarefaDetalheDto> AtualizarAsync(int id, TarefaInputModel input, string rowVersion, UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        TarefaRegras.Validar(input);
        TarefaRegras.ExigirAutenticacao(usuario);
        var versao = TarefaRegras.LerVersao(rowVersion);
        var tarefa = await db.Tarefas.SingleOrDefaultAsync(t => t.Id == id, cancellationToken) ?? throw new RecursoNaoEncontradoException();
        TarefaRegras.ExigirEdicao(tarefa, input, usuario);
        db.Entry(tarefa).Property(t => t.RowVersion).OriginalValue = versao;
        TarefaRegras.Aplicar(tarefa, input);
        // Mesmo sem alteração nos demais campos, o cliente precisa confirmar a versão enviada.
        db.Entry(tarefa).Property(t => t.Titulo).IsModified = true;
        db.Auditorias.Add(Evento("Atualizar", tarefa.Id, usuario));
        await SalvarComConcorrencia(cancellationToken);
        return TarefaRegras.Detalhe(tarefa);
    }

    public async Task ExcluirAsync(int id, string rowVersion, UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        TarefaRegras.ExigirExclusao(usuario);
        var versao = TarefaRegras.LerVersao(rowVersion);
        var tarefa = await db.Tarefas.SingleOrDefaultAsync(t => t.Id == id, cancellationToken) ?? throw new RecursoNaoEncontradoException();
        db.Entry(tarefa).Property(t => t.RowVersion).OriginalValue = versao;
        db.Tarefas.Remove(tarefa);
        db.Auditorias.Add(Evento("Excluir", tarefa.Id, usuario));
        await SalvarComConcorrencia(cancellationToken);
    }

    private async Task SalvarComConcorrencia(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw new ConflitoException(); }
    }

    private static Auditoria Evento(string acao, int id, UsuarioContexto usuario) => new()
        { Acao = acao, TarefaId = id, UsuarioId = usuario.Id, Instante = DateTimeOffset.UtcNow };

    public async Task<PainelDto> PainelAsync(UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        var hoje = TarefaRegras.Hoje;
        return await Visiveis(usuario).GroupBy(t => 1).Select(g => new PainelDto(g.Count(),
            g.Count(t => t.Situacao == "Pendente"), g.Count(t => t.Situacao == "Concluida"),
            g.Count(t => t.Prazo < hoje && t.Situacao != "Concluida")))
            .SingleOrDefaultAsync(cancellationToken) ?? new PainelDto(0, 0, 0, 0);
    }

    public async Task<PainelDto> PainelAdoAsync(UsuarioContexto usuario, CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        var abrir = connection.State != ConnectionState.Open;
        if (abrir) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*),
                  COALESCE(SUM(CASE WHEN [Situacao] = @pendente THEN 1 ELSE 0 END), 0),
                  COALESCE(SUM(CASE WHEN [Situacao] = @concluida THEN 1 ELSE 0 END), 0),
                  COALESCE(SUM(CASE WHEN [Prazo] < @hoje AND [Situacao] <> @concluida THEN 1 ELSE 0 END), 0)
                FROM [Tarefas]
                WHERE [Publica] = @publica OR (@autenticado = 1 AND (@admin = 1 OR [Equipe] = @equipe))
                """;
            Parametro(command, "@pendente", DbType.String, "Pendente");
            Parametro(command, "@concluida", DbType.String, "Concluida");
            Parametro(command, "@hoje", DbType.Date, TarefaRegras.Hoje.ToDateTime(TimeOnly.MinValue));
            Parametro(command, "@publica", DbType.Boolean, true);
            Parametro(command, "@autenticado", DbType.Boolean, usuario.Autenticado);
            Parametro(command, "@admin", DbType.Boolean, usuario.Administrador);
            Parametro(command, "@equipe", DbType.String, usuario.Equipe);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return new(0, 0, 0, 0);
            return new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
        }
        finally { if (abrir) await connection.CloseAsync(); }
    }

    private static void Parametro(DbCommand command, string nome, DbType tipo, object valor)
    {
        var parametro = command.CreateParameter();
        parametro.ParameterName = nome;
        parametro.DbType = tipo;
        parametro.Value = valor;
        if (tipo == DbType.String) parametro.Size = nome == "@equipe" ? 80 : 20;
        command.Parameters.Add(parametro);
    }
}
