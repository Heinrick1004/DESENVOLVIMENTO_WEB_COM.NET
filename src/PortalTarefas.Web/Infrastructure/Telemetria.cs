using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PortalTarefas.Web.Infrastructure;

public static class Telemetria
{
    public const string Nome = "PortalTarefas";
    public static readonly ActivitySource Traces = new(Nome);
    public static readonly Meter Meter = new(Nome);
    public static readonly Counter<long> TarefasCriadas = Meter.CreateCounter<long>("portal.tarefas.criadas", description: "Tarefas criadas com sucesso");
    public static readonly Histogram<double> DuracaoHttp = Meter.CreateHistogram<double>("portal.http.duracao", "ms");
}
