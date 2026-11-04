using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace PortalTarefas.Web.Controllers;

[Route("Tarefas")]
public class TarefasPagesController : Controller
{
    private readonly ITarefaService _tarefaService;

    public TarefasPagesController(ITarefaService tarefaService)
    {
        _tarefaService = tarefaService;
    }

    // GET: /Tarefas
    // Lista todas as tarefas cadastradas no banco.
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var tarefas = await _tarefaService.ListarAsync(
            HttpContext.RequestAborted);

        return View(tarefas);
    }

    // GET: /Tarefas/1
    // Apresenta os detalhes de uma tarefa.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var tarefa = await _tarefaService.ObterPorIdAsync(
            id,
            HttpContext.RequestAborted);

        if (tarefa == null)
        {
            return NotFound();
        }

        return View(tarefa);
    }

    // GET: /Tarefas/Criar
    // Exibe o formulário de cadastro.
    [HttpGet("Criar")]
    public IActionResult Create()
    {
        return View(new TarefaInputModel());
    }

    // POST: /Tarefas/Criar
    // Recebe, valida e persiste os dados.
    [HttpPost("Criar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TarefaInputModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var tarefa = new Tarefa
        {
            Titulo = model.Titulo,
            Descricao = model.Descricao,
            Prioridade = model.Prioridade,
            Prazo = model.Prazo
        };

        await _tarefaService.CriarAsync(
            tarefa,
            HttpContext.RequestAborted);

        TempData["Mensagem"] = "Tarefa cadastrada com sucesso!";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Tarefas/1/Editar
    // Carrega os dados para edição.
    [HttpGet("{id:int}/Editar")]
    public async Task<IActionResult> Edit(int id)
    {
        var tarefa = await _tarefaService.ObterPorIdAsync(
            id,
            HttpContext.RequestAborted);

        if (tarefa == null)
        {
            return NotFound();
        }

        var model = new TarefaInputModel
        {
            Titulo = tarefa.Titulo,
            Descricao = tarefa.Descricao,
            Prioridade = tarefa.Prioridade,
            Prazo = tarefa.Prazo,
            Versao = tarefa.Versao
        };

        return View(model);
    }

    // POST: /Tarefas/1/Editar
    // Atualiza os dados da tarefa.

[HttpPost("{id:int}/Editar")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(
    int id,
    TarefaInputModel model)
{
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    if (model.Versao == Guid.Empty)
    {
        ModelState.AddModelError(
            string.Empty,
            "Não foi possível validar a versão da tarefa.");

        return View(model);
    }

    var tarefa = await _tarefaService.ObterPorIdAsync(
        id,
        HttpContext.RequestAborted);

    if (tarefa == null)
    {
        return NotFound();
    }

    tarefa.Titulo = model.Titulo;
    tarefa.Descricao = model.Descricao;
    tarefa.Prioridade = model.Prioridade;
    tarefa.Prazo = model.Prazo;

    // Preserva a versão originalmente recebida do formulário.
    tarefa.Versao = model.Versao;

    try
    {
        if (!await _tarefaService.AtualizarAsync(
            tarefa,
            HttpContext.RequestAborted))
        {
            return NotFound();
        }
    }
    catch (DbUpdateConcurrencyException)
    {
        ModelState.AddModelError(
            string.Empty,
            "Esta tarefa foi modificada por outro usuário. " +
            "Recarregue a página para consultar os dados atualizados.");

        return View(model);
    }

    TempData["Mensagem"] = "Tarefa atualizada com sucesso!";

    return RedirectToAction(nameof(Index));
}

}
