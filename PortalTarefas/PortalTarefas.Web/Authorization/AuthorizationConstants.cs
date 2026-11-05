
namespace PortalTarefas.Web.Authorization;

public static class AuthorizationConstants
{
    // Papéis do sistema
    public const string RoleAdministrator = "Administrador";
    public const string RoleUser = "Usuario";

    // Claims
    public const string PermissionClaimType = "Permissao";

    public const string PermissionManageTasks =
        "Tarefas.Gerenciar";

    // Policies
    public const string PolicyAccessTasks =
        "AcessarTarefas";

    public const string PolicyManageTasks =
        "GerenciarTarefas";
}
