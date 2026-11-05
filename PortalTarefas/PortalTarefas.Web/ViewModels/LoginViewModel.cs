using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Informe seu e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe sua senha.")]
    [DataType(DataType.Password)]
    public string Senha { get; set; } = string.Empty;

    public bool LembrarMe { get; set; }
}