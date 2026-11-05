using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Models;
using Microsoft.AspNetCore.Authorization;
using PortalTarefas.Web.Authorization;

namespace PortalTarefas.Web.Controllers;

[Authorize(Policy = AuthorizationConstants.PolicyAccessTasks)]
[ApiController]
[Route("api/mvc/tarefas")]
public class TarefasController : ControllerBase
{
    // Dados temporários para demonstração
    private static readonly List<TarefaDto> Tarefas = new()
    {
        new TarefaDto
        {
            Id = 1,
            Titulo = "Estudar ASP.NET Core",
            Descricao = "Revisar Model Binding e MVC",
            Situacao = "Pendente"
        },
        new TarefaDto
        {
            Id = 2,
            Titulo = "Implementar Middleware",
            Descricao = "Desenvolver auditoria HTTP",
            Situacao = "Concluída"
        }
    };

    private static int _proximoId = 3;

    // 1. Listagem com Query String e Header
    [HttpGet]
    public IActionResult Listar(
        [FromQuery] string? termo,
        [FromQuery] int pagina = 1,
        [FromHeader(Name = "X-Cliente")] string? cliente = null)
    {
        var filtradas = Tarefas
            .Where(t => string.IsNullOrWhiteSpace(termo)
                || t.Titulo.Contains(
                    termo,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Ok(new
        {
            Origem = "MVC Controller",
            Termo = termo,
            Pagina = pagina,
            Cliente = cliente,
            Total = filtradas.Count,
            Tarefas = filtradas
        });
    }

    // 2. Consulta por identificador (Route)
    [HttpGet("{id:int}")]
    public IActionResult ObterPorId(
        [FromRoute] int id)
    {
        var tarefa = Tarefas.FirstOrDefault(t => t.Id == id);

        if (tarefa is null)
        {
            return NotFound(new
            {
                Mensagem = $"Tarefa com ID {id} não encontrada."
            });
        }

        return Ok(tarefa);
    }

    // 3. Cadastro com JSON Body
[Authorize(Policy = AuthorizationConstants.PolicyManageTasks)]
[HttpPost]
public IActionResult Criar(
    [FromBody] TarefaDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var novaTarefa = new TarefaDto
        {
            Id = _proximoId++,
            Titulo = request.Titulo,
            Descricao = request.Descricao,
            Situacao = request.Situacao
        };

        Tarefas.Add(novaTarefa);

        return CreatedAtAction(
            nameof(ObterPorId),
            new { id = novaTarefa.Id },
            novaTarefa
        );
    }
}