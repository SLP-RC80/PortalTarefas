using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.Services;

namespace PortalTarefas.Web.Controllers;

[ApiController]
[Route("api/comparacao")]
public sealed class ComparacaoController : ControllerBase
{
    private readonly ITarefaService _efCoreService;
    private readonly ITarefaDapperService _dapperService;

    public ComparacaoController(
        ITarefaService efCoreService,
        ITarefaDapperService dapperService)
    {
        _efCoreService = efCoreService;
        _dapperService = dapperService;
    }

    // GET: /api/comparacao/tarefas/1
    [HttpGet("tarefas/{id:int}")]
    public async Task<IActionResult> CompararTarefa(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new
            {
                Mensagem = "Informe um identificador válido."
            });
        }

        // Consulta utilizando Entity Framework Core.
        var tarefaEfCore =
            await _efCoreService.ObterPorIdAsync(
                id,
                cancellationToken);

        // Consulta utilizando Dapper e SQL direto.
        var tarefaDapper =
            await _dapperService.ObterPorIdAsync(
                id,
                cancellationToken);

        // Se nenhum método encontrar o registro.
        if (tarefaEfCore == null && tarefaDapper == null)
        {
            return NotFound(new
            {
                Mensagem = $"Tarefa {id} não encontrada."
            });
        }

        // Compara os resultados obtidos.
        var resultadosIguais =
            CompararResultados(tarefaEfCore, tarefaDapper);

        return Ok(new
        {
            Identificador = id,

            Comparacao = new
            {
                EfCore = new
                {
                    Tecnologia = "Entity Framework Core",
                    Encontrada = tarefaEfCore != null,
                    Tarefa = tarefaEfCore
                },

                Dapper = new
                {
                    Tecnologia = "Dapper / SQL direto",
                    Encontrada = tarefaDapper != null,
                    Tarefa = tarefaDapper
                }
            },

            ResultadosIguais = resultadosIguais
        });
    }

    private static bool CompararResultados(
        Tarefa? efCore,
        Tarefa? dapper)
    {
        if (efCore == null || dapper == null)
        {
            return efCore == null && dapper == null;
        }

        return
            efCore.Id == dapper.Id &&
            efCore.Titulo == dapper.Titulo &&
            efCore.Descricao == dapper.Descricao &&
            efCore.Prioridade == dapper.Prioridade &&
            efCore.Prazo == dapper.Prazo &&
            efCore.Versao == dapper.Versao;
    }
}