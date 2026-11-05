
using Microsoft.AspNetCore.Identity;

namespace PortalTarefas.Web.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string NomeExibicao { get; set; } = string.Empty;
}
