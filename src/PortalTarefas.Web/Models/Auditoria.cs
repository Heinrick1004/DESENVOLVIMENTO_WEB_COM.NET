namespace PortalTarefas.Web.Models;

public sealed class Auditoria
{
    public long Id { get; set; }
    public string Acao { get; set; } = string.Empty;
    public string? UsuarioId { get; set; }
    // Identificador histórico: sem FK para preservar a evidência após a exclusão.
    public int? TarefaId { get; set; }
    public DateTimeOffset Instante { get; set; } = DateTimeOffset.UtcNow;
}
