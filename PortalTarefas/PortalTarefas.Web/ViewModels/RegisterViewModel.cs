using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Informe seu nome.")]
    [StringLength(80)]
    public string NomeExibicao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe sua senha.")]
    [StringLength(100, MinimumLength = 8,
        ErrorMessage = "A senha deve possuir pelo menos 8 caracteres.")]
    [DataType(DataType.Password)]
    public string Senha { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme sua senha.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Senha),
        ErrorMessage = "As senhas não coincidem.")]
    public string ConfirmarSenha { get; set; } = string.Empty;
}