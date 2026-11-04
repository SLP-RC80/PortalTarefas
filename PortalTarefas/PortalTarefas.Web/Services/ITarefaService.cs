using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Services;

public interface ITarefaService
{
    Task<IReadOnlyList<Tarefa>> ListarAsync(
        CancellationToken cancellationToken = default);

    Task<Tarefa?> ObterPorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Tarefa> CriarAsync(
        Tarefa tarefa,
        CancellationToken cancellationToken = default);

    Task<bool> AtualizarAsync(
        Tarefa tarefa,
        CancellationToken cancellationToken = default);
}
