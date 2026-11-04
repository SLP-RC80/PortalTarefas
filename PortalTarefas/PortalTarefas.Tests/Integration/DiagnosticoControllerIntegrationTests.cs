
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PortalTarefas.Tests.Fixtures;

namespace PortalTarefas.Tests.Integration;

public class DiagnosticoControllerIntegrationTests
    : IClassFixture<PortalTarefasApplicationFactory>
{
    private readonly HttpClient _client;

    public DiagnosticoControllerIntegrationTests(
        PortalTarefasApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ObterPorRota_DeveRetornarIdentificador()
    {
        var response = await _client.GetAsync(
            "/api/diagnostico/mvc/rota/42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;

        Assert.Equal(
            "MVC Controller",
            root.GetProperty("origem").GetString());

        Assert.Equal(
            "Route",
            root.GetProperty("tipoBinding").GetString());

        Assert.Equal(
            42,
            root.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task ObterPorQuery_DeveRetornarParametrosRecebidos()
    {
        var response = await _client.GetAsync(
            "/api/diagnostico/mvc/query?termo=teste&pagina=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;

        Assert.Equal(
            "MVC Controller",
            root.GetProperty("origem").GetString());

        Assert.Equal(
            "Query String",
            root.GetProperty("tipoBinding").GetString());

        Assert.Equal(
            "teste",
            root.GetProperty("termo").GetString());

        Assert.Equal(
            3,
            root.GetProperty("pagina").GetInt32());
    }

    [Fact]
    public async Task ObterPorQuery_SemPagina_DeveUtilizarValorPadrao()
    {
        var response = await _client.GetAsync(
            "/api/diagnostico/mvc/query?termo=padrao");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            1,
            json.RootElement.GetProperty("pagina").GetInt32());
    }

    [Fact]
    public async Task ReceberDados_DeveProcessarJsonBody()
    {
        var request = new
        {
            Titulo = "Teste de Model Binding",
            Concluida = true
        };

        var response = await _client.PostAsJsonAsync(
            "/api/diagnostico/mvc/body",
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;

        Assert.Equal(
            "MVC Controller",
            root.GetProperty("origem").GetString());

        Assert.Equal(
            "JSON Body",
            root.GetProperty("tipoBinding").GetString());

        Assert.Equal(
            "Teste de Model Binding",
            root.GetProperty("titulo").GetString());

        Assert.True(
            root.GetProperty("concluida").GetBoolean());
    }

    [Fact]
    public async Task ObterPorRota_ComIdentificadorInvalido_DeveRetornar404()
    {
        var response = await _client.GetAsync(
            "/api/diagnostico/mvc/rota/abc");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}
