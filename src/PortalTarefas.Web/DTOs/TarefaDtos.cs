using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.DTOs;

public class TarefaInputModel : IValidatableObject
{
    [Required(ErrorMessage = "Informe o título.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "O título deve ter entre 3 e 120 caracteres.")]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "A descrição deve ter até 2000 caracteres.")]
    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Required(ErrorMessage = "Informe a situação.")]
    [RegularExpression("^(Pendente|EmAndamento|Concluida)$", ErrorMessage = "Selecione uma situação válida.")]
    [Display(Name = "Situação")]
    public string Situacao { get; set; } = "Pendente";

    [Range(1, 3, ErrorMessage = "A prioridade deve estar entre 1 e 3.")]
    public int Prioridade { get; set; } = 1;

    [DataType(DataType.Date)]
    public DateOnly? Prazo { get; set; }

    [Required(ErrorMessage = "Informe a equipe.")]
    [StringLength(80, ErrorMessage = "A equipe deve ter até 80 caracteres.")]
    public string Equipe { get; set; } = "Geral";

    [Display(Name = "Visível publicamente")]
    public bool Publica { get; set; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Prioridade != 3) yield break;
        if (!Prazo.HasValue)
            yield return new ValidationResult("Tarefas de prioridade alta exigem prazo.", [nameof(Prazo)]);
        if (string.IsNullOrWhiteSpace(Descricao) || Descricao.Trim().Length < 10)
            yield return new ValidationResult("Tarefas de prioridade alta exigem descrição com pelo menos 10 caracteres.", [nameof(Descricao)]);
    }
}

public sealed class CriarTarefaDto : TarefaInputModel { }

public sealed class AtualizarTarefaDto : TarefaInputModel
{
    [Required(ErrorMessage = "Informe a versão da tarefa.")]
    [StringLength(12, MinimumLength = 12, ErrorMessage = "A versão deve ter 12 caracteres Base64.")]
    public string RowVersion { get; set; } = string.Empty;

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var error in base.Validate(validationContext)) yield return error;
        if (!VersaoTarefa.Valida(RowVersion))
            yield return new ValidationResult("A versão deve ser uma sequência Base64 de 8 bytes.", [nameof(RowVersion)]);
    }
}

public sealed record TarefaResumoDto(int Id, string Titulo, string? Descricao, string Situacao,
    int Prioridade, DateOnly? Prazo, string Equipe, bool Publica, string? ProprietarioId);

public sealed record TarefaDetalheDto(int Id, string Titulo, string? Descricao, string Situacao,
    int Prioridade, DateOnly? Prazo, string Equipe, bool Publica, string? ProprietarioId,
    string RowVersion, DateTimeOffset CriadaEm);

public sealed record PaginaDto<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, int Total, int TotalPaginas);
public sealed record PainelDto(int Total, int Pendentes, int Concluidas, int Vencidas);
public sealed record VersionDto(string Version);

public static class VersaoTarefa
{
    public static bool Valida(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        Span<byte> bytes = stackalloc byte[8];
        return Convert.TryFromBase64String(value, bytes, out var count) && count == 8;
    }
}
