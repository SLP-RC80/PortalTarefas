
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.Services;

namespace PortalTarefas.Web.Controllers;

[ApiController]
[Route("api/v1/tarefas")]
[Produces("application/json")]
public sealed class TarefasV1Controller : ControllerBase
{
    private readonly ITarefaService _tarefaService;

    public TarefasV1Controller(ITarefaService tarefaService)
    {
        _tarefaService = tarefaService;
    }

    // GET /api/v1/tarefas
    // Listagem com filtro e paginação.
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? termo = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 10,
        CancellationToken cancellationToken = default)
    {
        if (pagina < 1 || tamanhoPagina < 1 ||
            tamanhoPagina > 100)
        {
            return BadRequest(CriarProblema(
                400,
                "Parâmetros de paginação inválidos",
                "A página deve ser maior que zero e o tamanho " +
                "deve estar entre 1 e 100."));
        }

        var todas = await _tarefaService.ListarAsync(
            cancellationToken);

        var filtradas = todas
            .Where(t => string.IsNullOrWhiteSpace(termo) ||
                t.Titulo.Contains(
                    termo,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        var totalRegistros = filtradas.Count;

        long deslocamento =
            ((long)pagina - 1) * tamanhoPagina;

        if (deslocamento > int.MaxValue)
        {
            return BadRequest(CriarProblema(
                400,
                "Página fora do intervalo permitido",
                "O deslocamento solicitado é muito grande."));
        }

        var itens = filtradas
            .Skip((int)deslocamento)
            .Take(tamanhoPagina)
            .ToList();

        var totalPaginas = (int)Math.Ceiling(
            totalRegistros / (double)tamanhoPagina);

        return Ok(new
        {
            Pagina = pagina,
            TamanhoPagina = tamanhoPagina,
            TotalRegistros = totalRegistros,
            TotalPaginas = totalPaginas,
            Tarefas = itens
        });
    }

    // GET /api/v1/tarefas/1
    // Consulta por identificador.
    [HttpGet("{id:int}", Name = "ObterTarefaV1")]
    [ProducesResponseType(typeof(Tarefa),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(CriarProblema(
                400,
                "Identificador inválido",
                "Informe um identificador positivo."));
        }

        var tarefa = await _tarefaService.ObterPorIdAsync(
            id,
            cancellationToken);

        if (tarefa == null)
        {
            return NotFound(CriarProblema(
                404,
                "Tarefa não encontrada",
                $"Não existe tarefa cadastrada com o ID {id}."));
        }

        return Ok(tarefa);
    }

    // POST /api/v1/tarefas
    // Cadastro utilizando persistência EF Core.
    [HttpPost]
    [ProducesResponseType(typeof(Tarefa),
        StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar(
        [FromBody] TarefaInputModel model,
        CancellationToken cancellationToken)
    {
        var tarefa = new Tarefa
        {
            Titulo = model.Titulo,
            Descricao = model.Descricao,
            Prioridade = model.Prioridade,
            Prazo = model.Prazo
        };

        var criada = await _tarefaService.CriarAsync(
            tarefa,
            cancellationToken);

        return CreatedAtRoute(
            "ObterTarefaV1",
            new { id = criada.Id },
            criada);
    }

    // PUT /api/v1/tarefas/1
    // Atualização com controle de concorrência.
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(Tarefa),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails),
        StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Atualizar(
        [FromRoute] int id,
        [FromBody] TarefaInputModel model,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(CriarProblema(
                400,
                "Identificador inválido",
                "Informe um identificador positivo."));
        }

        if (model.Versao == Guid.Empty)
        {
            return BadRequest(CriarProblema(
                400,
                "Versão obrigatória",
                "Informe a versão original da tarefa."));
        }

        var tarefa = new Tarefa
        {
            Id = id,
            Titulo = model.Titulo,
            Descricao = model.Descricao,
            Prioridade = model.Prioridade,
            Prazo = model.Prazo,
            Versao = model.Versao
        };

        try
        {
            var atualizada = await _tarefaService.AtualizarAsync(
                tarefa,
                cancellationToken);

            if (!atualizada)
            {
                return NotFound(CriarProblema(
                    404,
                    "Tarefa não encontrada",
                    $"Não existe tarefa cadastrada com o ID {id}."));
            }

            var resultado = await _tarefaService.ObterPorIdAsync(
                id,
                cancellationToken);

            return Ok(resultado);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(CriarProblema(
                409,
                "Conflito de concorrência",
                "A tarefa foi modificada por outro usuário. " +
                "Consulte os dados atualizados antes de tentar novamente."));
        }
    }

    private static ProblemDetails CriarProblema(
        int status,
        string titulo,
        string detalhe)
    {
        return new ProblemDetails
        {
            Type = "about:blank",
            Status = status,
            Title = titulo,
            Detail = detalhe
        };
    }
}
