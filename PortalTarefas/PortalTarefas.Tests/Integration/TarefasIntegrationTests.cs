using System.Net;
using PortalTarefas.Tests.Fixtures;

namespace PortalTarefas.Tests.Integration;

public class TarefasIntegrationTests
    : IClassFixture<PortalTarefasApplicationFactory>
{
    private readonly HttpClient _client;

    public TarefasIntegrationTests(
        PortalTarefasApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Index_DeveRetornarPaginaDeTarefas()
    {
        var response = await _client.GetAsync("/Tarefas");

        var conteudo = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains(
            "Tarefa inicial de demonstração",
            conteudo);
    }

    [Fact]
    public async Task Details_DeveRetornarTarefaExistente()
    {
        var response = await _client.GetAsync("/Tarefas/1");

        var conteudo = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains(
            "Tarefa inicial de demonstração",
            conteudo);
    }

    [Fact]
    public async Task Details_DeveRetornar404ParaTarefaInexistente()
    {
        var response = await _client.GetAsync("/Tarefas/99999");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}