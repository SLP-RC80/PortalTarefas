
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using PortalTarefas.Tests.Fixtures;

namespace PortalTarefas.Tests.Integration;

public class TarefasCrudIntegrationTests
    : IClassFixture<PortalTarefasApplicationFactory>
{
    private readonly HttpClient _client;

    public TarefasCrudIntegrationTests(
        PortalTarefasApplicationFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
    }

    // =====================================================
    // 1. GET - FORMULARIO DE CADASTRO
    // =====================================================

    [Fact]
    public async Task Create_DeveRetornarFormulario()
    {
        var response = await _client.GetAsync(
            "/Tarefas/Criar");

        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Contains(
            "Cadastrar Nova Tarefa",
            html);

        Assert.False(
            string.IsNullOrWhiteSpace(
                ObterValorInput(
                    html,
                    "__RequestVerificationToken")));
    }

    // =====================================================
    // 2. POST - CADASTRO VALIDO
    // =====================================================

    [Fact]
    public async Task Create_ComDadosValidos_DeveCadastrarTarefa()
    {
        var titulo = "Teste MVC Cadastro " +
                     Guid.NewGuid().ToString("N");

        var token = await ObterTokenAsync(
            "/Tarefas/Criar");

        using var formulario = CriarFormulario(
            token,
            titulo,
            "Cadastro realizado por teste de integração.");

        var response = await _client.PostAsync(
            "/Tarefas/Criar",
            formulario);

        Assert.Equal(
            HttpStatusCode.Found,
            response.StatusCode);

        Assert.NotNull(response.Headers.Location);

        // Confirma que o registro foi persistido.
        var consulta = await _client.GetAsync("/Tarefas");

        var html = await consulta.Content.ReadAsStringAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            consulta.StatusCode);

        Assert.Contains(titulo, html);
    }

    // =====================================================
    // 3. POST - VALIDACAO DE TITULO
    // =====================================================

    [Fact]
    public async Task Create_ComTituloVazio_DeveRetornarErroValidacao()
    {
        var token = await ObterTokenAsync(
            "/Tarefas/Criar");

        using var formulario = CriarFormulario(
            token,
            "",
            "Descrição de teste.");

        var response = await _client.PostAsync(
            "/Tarefas/Criar",
            formulario);

        // Decodifica entidades HTML antes de verificar
        // a mensagem de validação.
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Contains(
            "O título é obrigatório.",
            html);
    }

    // =====================================================
    // 4. POST - PROTECAO ANTIFORGERY
    // =====================================================

    [Fact]
    public async Task Create_SemTokenAntiforgery_DeveRetornar400()
    {
        using var formulario = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Titulo"] = "Teste sem antiforgery",
                ["Descricao"] = "Teste de segurança",
                ["Prioridade"] = "2",
                ["Prazo"] = ""
            });

        var response = await _client.PostAsync(
            "/Tarefas/Criar",
            formulario);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    // =====================================================
    // 5. GET - FORMULARIO DE EDICAO
    // =====================================================

    [Fact]
    public async Task Edit_DeveRetornarFormularioComVersao()
    {
        var response = await _client.GetAsync(
            "/Tarefas/1/Editar");

        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Contains(
            "Editar Tarefa",
            html);

        var versao = ObterValorInput(
            html,
            "Versao");

        Assert.True(
            Guid.TryParse(versao, out _));
    }

    // =====================================================
    // 6. POST - EDICAO VALIDA
    // =====================================================

    [Fact]
    public async Task Edit_ComDadosValidos_DeveAtualizarTarefa()
    {
        var dados = await ObterDadosEdicaoAsync(1);

        var titulo = "Teste MVC Edicao " +
                     Guid.NewGuid().ToString("N");

        using var formulario = CriarFormulario(
            dados.Token,
            titulo,
            "Descrição atualizada pelo teste.",
            versao: dados.Versao);

        var response = await _client.PostAsync(
            "/Tarefas/1/Editar",
            formulario);

        Assert.Equal(
            HttpStatusCode.Found,
            response.StatusCode);

        // Consulta novamente os dados persistidos.
        var consulta = await _client.GetAsync("/Tarefas/1");

        var html = await consulta.Content.ReadAsStringAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            consulta.StatusCode);

        Assert.Contains(titulo, html);
    }

    // =====================================================
    // 7. POST - CONTROLE DE CONCORRENCIA
    // =====================================================

    [Fact]
    public async Task Edit_ComVersaoDesatualizada_DeveDetectarConcorrencia()
    {
        // Dois formulários carregam a mesma versão.
        var usuarioA = await ObterDadosEdicaoAsync(1);
        var usuarioB = await ObterDadosEdicaoAsync(1);

        Assert.Equal(
            usuarioA.Versao,
            usuarioB.Versao);

        // Usuário A salva primeiro.
        using var formularioA = CriarFormulario(
            usuarioA.Token,
            "Alteração realizada pelo Usuário A",
            "Atualização do usuário A.",
            versao: usuarioA.Versao);

        var respostaA = await _client.PostAsync(
            "/Tarefas/1/Editar",
            formularioA);

        Assert.Equal(
            HttpStatusCode.Found,
            respostaA.StatusCode);

        // Usuário B tenta salvar a versão antiga.
        using var formularioB = CriarFormulario(
            usuarioB.Token,
            "Alteração realizada pelo Usuário B",
            "Atualização do usuário B.",
            versao: usuarioB.Versao);

        var respostaB = await _client.PostAsync(
            "/Tarefas/1/Editar",
            formularioB);

        // Decodifica entidades HTML antes de verificar
        // a mensagem de concorrência.
        var html = WebUtility.HtmlDecode(
            await respostaB.Content.ReadAsStringAsync());

        Assert.Equal(
            HttpStatusCode.OK,
            respostaB.StatusCode);

        Assert.Contains(
            "Esta tarefa foi modificada por outro usuário.",
            html);
    }

    // =====================================================
    // 8. GET - EDICAO DE TAREFA INEXISTENTE
    // =====================================================

    [Fact]
    public async Task Edit_TarefaInexistente_DeveRetornar404()
    {
        var response = await _client.GetAsync(
            "/Tarefas/99999/Editar");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    // =====================================================
    // METODOS AUXILIARES
    // =====================================================

    private async Task<string> ObterTokenAsync(
        string caminho)
    {
        var response = await _client.GetAsync(caminho);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        return ObterValorInput(
            html,
            "__RequestVerificationToken");
    }

    private async Task<(string Token, string Versao)>
        ObterDadosEdicaoAsync(int id)
    {
        var response = await _client.GetAsync(
            $"/Tarefas/{id}/Editar");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        return (
            ObterValorInput(
                html,
                "__RequestVerificationToken"),

            ObterValorInput(
                html,
                "Versao")
        );
    }

    private static string ObterValorInput(
        string html,
        string nome)
    {
        var padraoInput =
            "<input\\b(?=[^>]*\\bname\\s*=\\s*\"" +
            Regex.Escape(nome) +
            "\")[^>]*>";

        var input = Regex.Match(
            html,
            padraoInput,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline);

        Assert.True(
            input.Success,
            $"Campo '{nome}' não encontrado no formulário.");

        var valor = Regex.Match(
            input.Value,
            "\\bvalue\\s*=\\s*\"([^\"]*)\"",
            RegexOptions.IgnoreCase);

        Assert.True(
            valor.Success,
            $"Valor do campo '{nome}' não encontrado.");

        return WebUtility.HtmlDecode(
            valor.Groups[1].Value);
    }

    private static FormUrlEncodedContent CriarFormulario(
        string token,
        string titulo,
        string descricao,
        string prioridade = "2",
        string prazo = "",
        string? versao = null)
    {
        var campos = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Titulo"] = titulo,
            ["Descricao"] = descricao,
            ["Prioridade"] = prioridade,
            ["Prazo"] = prazo
        };

        if (versao != null)
        {
            campos["Versao"] = versao;
        }

        return new FormUrlEncodedContent(campos);
    }
}
