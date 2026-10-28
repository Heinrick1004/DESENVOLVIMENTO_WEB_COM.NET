using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace PortalTarefas.Web.DTOs;

public sealed class TarefaConsulta
{
    [FromQuery(Name = "pagina"), Range(1, int.MaxValue)] public int Pagina { get; set; } = 1;
    [FromQuery(Name = "tamanho"), Range(1, 50)] public int Tamanho { get; set; } = 10;
    [FromQuery(Name = "ordenarPor")]
    [RegularExpression("^(prazo|titulo|prioridade|situacao|criadaem|id)$", ErrorMessage = "Ordenação inválida.")]
    public string OrdenarPor { get; set; } = "prazo";
    [FromQuery(Name = "direcao")]
    [RegularExpression("^(asc|desc)$", ErrorMessage = "Direção inválida.")]
    public string Direcao { get; set; } = "asc";
    [FromQuery(Name = "busca"), StringLength(120)] public string? Busca { get; set; }
    [FromQuery(Name = "situacao")]
    [RegularExpression("^(Pendente|EmAndamento|Concluida)$")]
    public string? Situacao { get; set; }
    [FromQuery(Name = "prioridade"), Range(1, 3)] public int? Prioridade { get; set; }
    [FromQuery(Name = "vencidas")] public bool Vencidas { get; set; }
}
