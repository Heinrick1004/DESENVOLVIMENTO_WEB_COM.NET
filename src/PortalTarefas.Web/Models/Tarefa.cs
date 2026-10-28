namespace PortalTarefas.Web.Models;

public sealed class Tarefa
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string Situacao { get; set; } = "Pendente";
    public int Prioridade { get; set; } = 1;
    public DateOnly? Prazo { get; set; }
    public string Equipe { get; set; } = "Geral";
    public bool Publica { get; set; }
    public string? ProprietarioId { get; set; }
    public DateTimeOffset CriadaEm { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
