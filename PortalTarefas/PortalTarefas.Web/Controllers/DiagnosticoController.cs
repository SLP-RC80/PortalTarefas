
using Microsoft.AspNetCore.Mvc;

namespace PortalTarefas.Web.Controllers;

[ApiController]
[Route("api/diagnostico/mvc")]
public class DiagnosticoController : ControllerBase
{
    // 1. Model Binding por Route
    [HttpGet("rota/{id:int}")]
    public IActionResult ObterPorRota(
        [FromRoute] int id)
    {
        return Ok(new
        {
            Origem = "MVC Controller",
            TipoBinding = "Route",
            Id = id
        });
    }

    // 2. Model Binding por Query String
    [HttpGet("query")]
    public IActionResult ObterPorQuery(
        [FromQuery] string termo,
        [FromQuery] int pagina = 1)
    {
        return Ok(new
        {
            Origem = "MVC Controller",
            TipoBinding = "Query String",
            Termo = termo,
            Pagina = pagina
        });
    }

    // 3. Model Binding por JSON Body
    [HttpPost("body")]
    public IActionResult ReceberDados(
        [FromBody] DiagnosticoMvcRequest request)
    {
        return Ok(new
        {
            Origem = "MVC Controller",
            TipoBinding = "JSON Body",
            request.Titulo,
            request.Concluida
        });
    }
}

// Modelo utilizado pelo Controller
public sealed record DiagnosticoMvcRequest(
    string Titulo,
    bool Concluida
);
