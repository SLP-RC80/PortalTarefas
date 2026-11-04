using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Services;

namespace PortalTarefas.Web.ViewComponents;

public sealed class TarefasVencidasViewComponent : ViewComponent
{
    private readonly ITarefaService _tarefaService;

    public TarefasVencidasViewComponent(
        ITarefaService tarefaService)
    {
        _tarefaService = tarefaService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var tarefas = await _tarefaService.ListarAsync();

        var tarefasVencidas = tarefas
            .Where(t =>
                t.Prazo.HasValue &&
                t.Prazo.Value.Date < DateTime.Today)
            .OrderBy(t => t.Prazo)
            .ToList();

        return View(tarefasVencidas);
    }
}
