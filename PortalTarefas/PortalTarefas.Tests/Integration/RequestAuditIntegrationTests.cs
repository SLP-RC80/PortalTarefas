
using System.Net;
using PortalTarefas.Tests.Fixtures;
using PortalTarefas.Web.Filters;
using PortalTarefas.Web.Middleware;

namespace PortalTarefas.Tests.Integration;

public class RequestAuditIntegrationTests
    : IClassFixture<PortalTarefasApplicationFactory>
{
    private readonly PortalTarefasApplicationFactory _factory;
    private readonly HttpClient _client;

    public RequestAuditIntegrationTests(
        PortalTarefasApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // TESTE 1: Geração automática do Correlation ID.
    [Fact]
    public async Task RequisicaoSemCorrelationId_DeveGerarIdentificador()
    {
        var response = await _client.GetAsync("/Tarefas");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.True(
            response.Headers.TryGetValues(
                "X-Correlation-ID",
                out var valores));

        var correlationId = Assert.Single(valores!);

        Assert.True(
            Guid.TryParseExact(correlationId, "N", out _));
    }

    // TESTE 2: Preservação do Correlation ID enviado pelo cliente.
    [Fact]
    public async Task RequisicaoComCorrelationId_DevePreservarIdentificador()
    {
        const string correlationId = "teste-correlacao-123";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/Tarefas");

        request.Headers.Add(
            "X-Correlation-ID",
            correlationId);

        var response = await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.True(
            response.Headers.TryGetValues(
                "X-Correlation-ID",
                out var valores));

        Assert.Equal(
            correlationId,
            Assert.Single(valores!));
    }

    // TESTE 3: Preservação do Correlation ID em respostas 404.
    [Fact]
    public async Task RequisicaoInexistente_DevePreservarCorrelationId()
    {
        const string correlationId = "teste-erro-404";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/Tarefas/99999");

        request.Headers.Add(
            "X-Correlation-ID",
            correlationId);

        var response = await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.True(
            response.Headers.TryGetValues(
                "X-Correlation-ID",
                out var valores));

        Assert.Equal(
            correlationId,
            Assert.Single(valores!));
    }

    // TESTE 4: Auditoria HTTP e MVC com início, término e duração.
    [Fact]
    public async Task Auditoria_DeveRegistrarInicioFimEDuracao()
    {
        _factory.LoggerProvider.Clear();

        const string correlationId = "teste-auditoria-200";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/Tarefas");

        request.Headers.Add(
            "X-Correlation-ID",
            correlationId);

        var response = await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var logs = _factory.LoggerProvider.Entries;

        var httpLogs = logs
            .Where(x => x.Category ==
                typeof(RequestAuditMiddleware).FullName)
            .ToList();

        var mvcLogs = logs
            .Where(x => x.Category ==
                typeof(ActionLogFilter).FullName)
            .ToList();

        // Verifica o início da requisição HTTP.
        Assert.Contains(httpLogs,
            x => x.Message.Contains("INICIO HTTP GET /Tarefas")
            && x.Message.Contains(correlationId));

        // Verifica o término, status e duração HTTP.
        Assert.Contains(httpLogs,
            x => x.Message.Contains("FIM HTTP GET /Tarefas Status=200")
            && x.Message.Contains("Duracao=")
            && x.Message.Contains(correlationId));

        // Verifica o início da Action MVC.
        Assert.Contains(mvcLogs,
            x => x.Message.Contains("INICIO MVC TarefasPages/Index")
            && x.Message.Contains(correlationId));

        // Verifica o término da Action MVC.
        Assert.Contains(mvcLogs,
            x => x.Message.Contains("FIM MVC TarefasPages/Index")
            && x.Message.Contains("Resultado=ViewResult")
            && x.Message.Contains(correlationId));
    }

    // TESTE 5: Auditoria de resposta HTTP 404.
    [Fact]
    public async Task Auditoria_DeveRegistrarStatus404()
    {
        _factory.LoggerProvider.Clear();

        const string correlationId = "teste-auditoria-404";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/Tarefas/99999");

        request.Headers.Add(
            "X-Correlation-ID",
            correlationId);

        var response = await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        var logs = _factory.LoggerProvider.Entries;

        // Verifica o registro HTTP com status 404.
        Assert.Contains(logs,
            x => x.Category ==
                typeof(RequestAuditMiddleware).FullName
            && x.Message.Contains("Status=404")
            && x.Message.Contains(correlationId));

        // Verifica o resultado NotFound no filtro MVC.
        Assert.Contains(logs,
            x => x.Category ==
                typeof(ActionLogFilter).FullName
            && x.Message.Contains("Resultado=NotFoundResult")
            && x.Message.Contains(correlationId));
    }
}
