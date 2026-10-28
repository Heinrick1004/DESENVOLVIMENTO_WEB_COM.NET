using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.DTOs;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Services;

internal static class TarefaRegras
{
    // A data civil do portal usa São Paulo mesmo se a hospedagem estiver em UTC.
    internal static DateOnly Hoje => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime);

    internal static void Validar(object input)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), errors, true))
            throw new ValidationException(string.Join(" ", errors.Select(x => x.ErrorMessage)));
        if (input is TarefaInputModel tarefa && tarefa.Titulo.Trim().Length < 3)
            throw new ValidationException("O título deve ter pelo menos 3 caracteres úteis.");
    }

    internal static byte[] LerVersao(string version)
    {
        if (!VersaoTarefa.Valida(version)) throw new ValidationException("A versão deve ser uma sequência Base64 de 8 bytes.");
        return Convert.FromBase64String(version);
    }

    internal static bool MesmaEquipe(string equipe, UsuarioContexto usuario) => string.Equals(equipe, usuario.Equipe, StringComparison.OrdinalIgnoreCase);

    internal static bool PodeVer(Tarefa tarefa, UsuarioContexto usuario) => tarefa.Publica ||
        (usuario.Autenticado && (usuario.Administrador || MesmaEquipe(tarefa.Equipe, usuario)));

    internal static void ExigirAutenticacao(UsuarioContexto usuario)
    {
        if (!usuario.Autenticado || string.IsNullOrWhiteSpace(usuario.Id)) throw new PermissaoException("Entre na sua conta para executar esta ação.");
    }

    internal static void ExigirCriacao(TarefaInputModel input, UsuarioContexto usuario)
    {
        ExigirAutenticacao(usuario);
        if (!usuario.Administrador && !MesmaEquipe(input.Equipe.Trim(), usuario))
            throw new PermissaoException("Você pode criar tarefas apenas para sua equipe.");
    }

    internal static void ExigirEdicao(Tarefa tarefa, TarefaInputModel input, UsuarioContexto usuario)
    {
        ExigirAutenticacao(usuario);
        var proprietario = tarefa.ProprietarioId == usuario.Id;
        if (!MesmaEquipe(tarefa.Equipe, usuario) && !proprietario && !usuario.PodeEditarOutrasEquipes)
            throw new PermissaoException("Editar tarefas de outra equipe exige permissão específica.");
        if (!MesmaEquipe(input.Equipe.Trim(), usuario) && !usuario.PodeEditarOutrasEquipes)
            throw new PermissaoException("Transferir tarefas para outra equipe exige permissão específica.");
    }

    internal static void ExigirExclusao(UsuarioContexto usuario)
    {
        ExigirAutenticacao(usuario);
        if (!usuario.Administrador) throw new PermissaoException("Somente administradores podem excluir tarefas.");
    }

    internal static void Aplicar(Tarefa tarefa, TarefaInputModel input)
    {
        tarefa.Titulo = input.Titulo.Trim();
        tarefa.Descricao = string.IsNullOrWhiteSpace(input.Descricao) ? null : input.Descricao.Trim();
        tarefa.Situacao = input.Situacao;
        tarefa.Prioridade = input.Prioridade;
        tarefa.Prazo = input.Prazo;
        tarefa.Equipe = input.Equipe.Trim();
        tarefa.Publica = input.Publica;
    }

    internal static TarefaResumoDto Resumo(Tarefa t) => new(t.Id, t.Titulo, t.Descricao, t.Situacao, t.Prioridade,
        t.Prazo, t.Equipe, t.Publica, t.ProprietarioId);

    internal static TarefaDetalheDto Detalhe(Tarefa t) => new(t.Id, t.Titulo, t.Descricao, t.Situacao, t.Prioridade,
        t.Prazo, t.Equipe, t.Publica, t.ProprietarioId, Convert.ToBase64String(t.RowVersion), t.CriadaEm);
}
