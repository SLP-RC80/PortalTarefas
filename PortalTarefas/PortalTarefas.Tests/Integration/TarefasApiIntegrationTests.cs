using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PortalTarefas.Tests.Fixtures;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Tests.Integration;

public class TarefasApiIntegrationTests
    : IClassFixture<PortalTarefasApplicationFactory>
{
    private readonly HttpClient _client;

    public TarefasApiIntegrationTests(
        PortalTarefasApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // TESTE 1: Listagem geral.
    [Fact]
    public async Task Listar_DeveRetornarOkETarefas()
    {
        var response = await _client.GetAsync(
            "/api/mvc/tarefas");

        var conteudo =
            await response.Content.ReadAsStringAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        using var json = JsonDocument.Parse(conteudo);

        var root = json.RootElement;

        Assert.Equal(
            "MVC Controller",
            root.GetProperty("origem").GetString());

        Assert.True(
            root.GetProperty("total").GetInt32() >= 2);

        Assert.True(
            root.GetProperty("tarefas").GetArrayLength() >= 2);
    }

    // TESTE 2: Query String e Header HTTP.
    [Fact]
    public async Task Listar_DeveAplicarFiltroQueryStringEHeader()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/mvc/tarefas?termo=ASP.NET&pagina=3");

        request.Headers.Add(
            "X-Cliente",
            "TesteIntegracao");

        var response = await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var conteudo =
            await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(conteudo);

        var root = json.RootElement;

        Assert.Equal(
            "ASP.NET",
            root.GetProperty("termo").GetString());

        Assert.Equal(
            3,
            root.GetProperty("pagina").GetInt32());

        Assert.Equal(
            "TesteIntegracao",
            root.GetProperty("cliente").GetString());

        Assert.Equal(
            1,
            root.GetProperty("total").GetInt32());
    }

    // TESTE 3: Consulta por identificador.
    [Fact]
    public async Task ObterPorId_DeveRetornarTarefaExistente()
    {
        var response = await _client.GetAsync(
            "/api/mvc/tarefas/1");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var tarefa =
            await response.Content.ReadFromJsonAsync<TarefaDto>();

        Assert.NotNull(tarefa);

        Assert.Equal(1, tarefa.Id);

        Assert.Equal(
            "Estudar ASP.NET Core",
            tarefa.Titulo);
    }

    // TESTE 4: Identificador inexistente.
    [Fact]
    public async Task ObterPorId_Inexistente_DeveRetornar404()
    {
        var response = await _client.GetAsync(
            "/api/mvc/tarefas/99999");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    // TESTE 5: Cadastro válido via JSON.
    [Fact]
    public async Task Criar_DeveRetornar201ECadastrarTarefa()
    {
        var request = new TarefaDto
        {
            Titulo = "Teste de integração API " +
                     Guid.NewGuid().ToString("N"),

            Descricao = "Cadastro automatizado via HTTP.",

            Situacao = "Pendente"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/mvc/tarefas",
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var criada =
            await response.Content.ReadFromJsonAsync<TarefaDto>();

        Assert.NotNull(criada);

        Assert.True(criada.Id > 0);

        Assert.Equal(request.Titulo, criada.Titulo);

        Assert.Equal(
            request.Descricao,
            criada.Descricao);

        Assert.Equal(
            request.Situacao,
            criada.Situacao);
    }

    // TESTE 6: Título obrigatório.
    [Fact]
    public async Task Criar_ComTituloVazio_DeveRetornar400()
    {
        var request = new TarefaDto
        {
            Titulo = "",
            Descricao = "Descrição de teste",
            Situacao = "Pendente"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/mvc/tarefas",
            request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    // TESTE 7: Limite máximo do título.
    [Fact]
    public async Task Criar_ComTituloAcimaDe120Caracteres_DeveRetornar400()
    {
        var request = new TarefaDto
        {
            Titulo = new string('A', 121),
            Situacao = "Pendente"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/mvc/tarefas",
            request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    // TESTE 8: Limite máximo da descrição.
    [Fact]
    public async Task Criar_ComDescricaoAcimaDe500Caracteres_DeveRetornar400()
    {
        var request = new TarefaDto
        {
            Titulo = "Tarefa válida",
            Descricao = new string('A', 501),
            Situacao = "Pendente"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/mvc/tarefas",
            request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    // TESTE 9: Situação obrigatória.
    [Fact]
    public async Task Criar_SemSituacao_DeveRetornar400()
    {
        var request = new TarefaDto
        {
            Titulo = "Tarefa sem situação",
            Descricao = "Teste de validação",
            Situacao = null!
        };

        var response = await _client.PostAsJsonAsync(
            "/api/mvc/tarefas",
            request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
}
