using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    public string Senha { get; set; } = string.Empty;

    [Display(Name = "Manter sessão neste navegador")]
    public bool LembrarMe { get; set; }

    public string? ReturnUrl { get; set; }
}
