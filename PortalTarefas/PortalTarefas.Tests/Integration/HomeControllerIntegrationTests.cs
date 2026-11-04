
using System.Net;
using PortalTarefas.Tests.Fixtures;

namespace PortalTarefas.Tests.Integration;

public class HomeControllerIntegrationTests
    : IClassFixture<PortalTarefasApplicationFactory>
{
    private readonly HttpClient _client;

    public HomeControllerIntegrationTests(
        PortalTarefasApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Home/Index")]
    public async Task Index_DeveRetornarPaginaInicial(string rota)
    {
        var response = await _client.GetAsync(rota);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);

        var conteudo =
            await response.Content.ReadAsStringAsync();

        Assert.False(string.IsNullOrWhiteSpace(conteudo));
    }

    [Fact]
    public async Task Privacy_DeveRetornarPaginaDePrivacidade()
    {
        var response = await _client.GetAsync(
            "/Home/Privacy");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);

        var conteudo =
            await response.Content.ReadAsStringAsync();

        Assert.False(string.IsNullOrWhiteSpace(conteudo));
    }

    [Fact]
    public async Task Error_DeveRetornarPaginaDeErro()
    {
        var response = await _client.GetAsync(
            "/Home/Error");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);

        var conteudo =
            await response.Content.ReadAsStringAsync();

        Assert.False(string.IsNullOrWhiteSpace(conteudo));
    }
}
