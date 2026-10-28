using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.Infrastructure;

public sealed class PortalOptions
{
    [RegularExpression("SqlServer|Memory", ErrorMessage = "Portal:Storage deve ser SqlServer ou Memory.")]
    public string Storage { get; set; } = "SqlServer";
    [Required] public string FrontendOrigin { get; set; } = "https://localhost:5173";
}
