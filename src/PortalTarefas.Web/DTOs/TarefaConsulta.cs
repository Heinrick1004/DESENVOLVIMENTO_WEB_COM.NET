using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.DTOs;

public sealed class TarefaConsulta
{
    [Range(1, int.MaxValue)] public int Pagina { get; set; } = 1;
    [Range(1, 50)] public int Tamanho { get; set; } = 10;
    [RegularExpression("^(prazo|titulo|prioridade|situacao|criadaem|id)$", ErrorMessage = "Ordenação inválida.")]
    public string OrdenarPor { get; set; } = "prazo";
    [RegularExpression("^(asc|desc)$", ErrorMessage = "Direção inválida.")]
    public string Direcao { get; set; } = "asc";
    [StringLength(120)] public string? Busca { get; set; }
    [RegularExpression("^(Pendente|EmAndamento|Concluida)$")]
    public string? Situacao { get; set; }
    [Range(1, 3)] public int? Prioridade { get; set; }
    public bool Vencidas { get; set; }
}
