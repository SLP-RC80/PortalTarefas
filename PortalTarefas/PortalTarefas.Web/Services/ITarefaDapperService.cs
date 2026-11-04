using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Services;

public interface ITarefaDapperService
{
    Task<Tarefa?> ObterPorIdAsync(
        int id,
        CancellationToken cancellationToken = default);
}
