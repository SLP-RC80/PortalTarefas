
using PortalTarefas.Web.Data;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web.Middleware;
using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.Services;
using PortalTarefas.Web.Filters;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. CONFIGURACAO DO BANCO DE DADOS
// ==========================================

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new InvalidOperationException(
        "A variável DATABASE_URL não foi configurada.");

var databaseUri = new Uri(databaseUrl);

var userInfoSeparator = databaseUri.UserInfo.IndexOf(':');

if (userInfoSeparator <= 0)
{
    throw new InvalidOperationException(
        "A DATABASE_URL não possui usuário e senha válidos.");
}

var databaseUsername = Uri.UnescapeDataString(
    databaseUri.UserInfo[..userInfoSeparator]);

var databasePassword = Uri.UnescapeDataString(
    databaseUri.UserInfo[(userInfoSeparator + 1)..]);

var connectionStringBuilder = new NpgsqlConnectionStringBuilder
{
    Host = databaseUri.Host,
    Port = databaseUri.Port > 0 ? databaseUri.Port : 5432,
    Database = Uri.UnescapeDataString(
        databaseUri.AbsolutePath.Trim('/')),
    Username = databaseUsername,
    Password = databasePassword,
    SslMode = SslMode.Require
};

connectionStringBuilder["Channel Binding"] = "Require";

builder.Services.AddDbContextPool<PortalTarefasDbContext>(
    options => options.UseNpgsql(
        connectionStringBuilder.ConnectionString));


// ==========================================
// 2. SERVICOS MVC E FILTROS
// ==========================================

builder.Services.AddScoped<ActionLogFilter>();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<ActionLogFilter>();
});


// ==========================================
// 3. DOCUMENTACAO OPENAPI
// ==========================================

builder.Services.AddOpenApi();


// ==========================================
// 4. SERVICOS DE NEGOCIO
// ==========================================

// 4.1 Serviço de gerenciamento de tarefas com EF Core
builder.Services.AddScoped<ITarefaService, TarefaService>();

// 4.2 Serviço de consulta direta utilizando Dapper
builder.Services.AddScoped<ITarefaDapperService>(_ =>
    new TarefaDapperService(
        connectionStringBuilder.ConnectionString));


// ==========================================
// 5. TRATAMENTO PADRONIZADO DE ERROS
// ==========================================

builder.Services.AddProblemDetails();


// ==========================================
// 6. CONFIGURACAO CORS
// ==========================================

var frontendOrigin =
    builder.Configuration["FrontendOrigin"]
    ?? "http://localhost:5173";

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendLocal", policy =>
    {
        policy
            .WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


// ==========================================
// 7. AUTORIZACAO
// ==========================================

builder.Services.AddAuthorization();


// ==========================================
// 8. CONSTRUCAO DA APLICACAO
// ==========================================

var app = builder.Build();


// ==========================================
// 9. SEED INICIAL - TAREFAS DE DEMONSTRACAO
// ==========================================

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<PortalTarefasDbContext>();

    const string tituloSeed = "Tarefa inicial de demonstração";

    bool seedExiste = await dbContext.Tarefas
        .AnyAsync(t => t.Titulo == tituloSeed);

    if (!seedExiste)
    {
        var tarefaInicial = new Tarefa
        {
            Titulo = tituloSeed,
            Descricao = "Registro inserido automaticamente pelo seed inicial.",
            Prioridade = 2,
            Prazo = null
        };

        dbContext.Tarefas.Add(tarefaInicial);

        await dbContext.SaveChangesAsync();

        app.Logger.LogInformation(
            "SEED: tarefa inicial criada com sucesso.");
    }
    else
    {
        app.Logger.LogInformation(
            "SEED: tarefa inicial já existe. Nenhum registro duplicado foi inserido.");
    }
}


// ==========================================
// 10. DADOS TEMPORARIOS PARA MINIMAL APIS
// ==========================================

var tarefas = new List<TarefaDto>
{
    new TarefaDto
    {
        Id = 1,
        Titulo = "Estudar ASP.NET Core",
        Descricao = "Revisar Model Binding e Minimal APIs",
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

var proximoId = 3;


// ==========================================
// 11. PIPELINE DE MIDDLEWARES
// ==========================================

// 11.1 Auditoria personalizada
app.UseRequestAudit();

// 11.2 Tratamento centralizado de exceções
app.UseExceptionHandler();

// 11.3 HSTS somente fora do desenvolvimento
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// 11.4 HTTPS Redirection desabilitado no Codespaces
// O HTTPS externo é fornecido pelo encaminhamento do GitHub.
// app.UseHttpsRedirection();

// 11.5 Roteamento
app.UseRouting();

// 11.6 Política CORS
app.UseCors("FrontendLocal");

// 11.7 Autorização
app.UseAuthorization();

// 11.8 Arquivos estáticos
app.MapStaticAssets();


// ==========================================
// 12. MAPEAMENTO DOS CONTROLLERS
// ==========================================

app.MapControllers();


// ==========================================
// 13. ROTA CONVENCIONAL MVC
// ==========================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


// ==========================================
// 14. ENDPOINTS DE DIAGNOSTICO
// DISPONIVEIS SOMENTE EM DEVELOPMENT
// ==========================================

if (app.Environment.IsDevelopment())
{
    // --------------------------------------
    // 14.1 Teste de tratamento de exceções
    // --------------------------------------

    app.MapGet("/api/diagnostico/erro", () =>
    {
        throw new InvalidOperationException(
            "Falha controlada para testar o tratamento de exceções."
        );
    });


    // --------------------------------------
    // 14.2 Model Binding por Route
    // --------------------------------------

    app.MapGet(
        "/api/diagnostico/model-binding/rota/{id:int}",
        ([FromRoute] int id) =>
        {
            return Results.Ok(new
            {
                Origem = "Route",
                Id = id
            });
        });


    // --------------------------------------
    // 14.3 Model Binding por Query String
    // --------------------------------------

    app.MapGet(
        "/api/diagnostico/model-binding/query",
        ([FromQuery] string termo, [FromQuery] int pagina = 1) =>
        {
            return Results.Ok(new
            {
                Origem = "Query String",
                Termo = termo,
                Pagina = pagina
            });
        });


    // --------------------------------------
    // 14.4 Model Binding por JSON Body
    // --------------------------------------

    app.MapPost(
        "/api/diagnostico/model-binding/body",
        ([FromBody] DiagnosticoTarefaRequest request) =>
        {
            return Results.Ok(new
            {
                Origem = "JSON Body",
                request.Titulo,
                request.Concluida
            });
        });


    // ==========================================
    // 15. MINIMAL APIS - GERENCIAMENTO DE TAREFAS
    // ==========================================

    // 15.1 Listagem com Query String e Header
    app.MapGet("/api/tarefas", (
        [FromQuery] string? termo = null,
        [FromQuery] int pagina = 1,
        [FromHeader(Name = "X-Cliente")] string? cliente = null) =>
    {
        var filtradas = tarefas
            .Where(t => string.IsNullOrWhiteSpace(termo)
                || t.Titulo.Contains(
                    termo,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Results.Ok(new
        {
            Origem = "Minimal API",
            Termo = termo,
            Pagina = pagina,
            Cliente = cliente,
            Total = filtradas.Count,
            Tarefas = filtradas
        });
    });


    // 15.2 Consulta por identificador (Route)
    app.MapGet("/api/tarefas/{id:int}", (
        [FromRoute] int id) =>
    {
        var tarefa = tarefas.FirstOrDefault(t => t.Id == id);

        if (tarefa is null)
        {
            return Results.NotFound(new
            {
                Mensagem = $"Tarefa com ID {id} não encontrada."
            });
        }

        return Results.Ok(tarefa);
    });


    // 15.3 Cadastro com JSON Body e validação
    app.MapPost("/api/tarefas", (TarefaDto request) =>
    {
        var resultados = new List<ValidationResult>();

        var contexto = new ValidationContext(request);

        bool valido = Validator.TryValidateObject(
            request,
            contexto,
            resultados,
            validateAllProperties: true
        );

        if (!valido)
        {
            var erros = resultados
                .SelectMany(resultado =>
                    resultado.MemberNames.Select(nome => new
                    {
                        Nome = nome,
                        Mensagem = resultado.ErrorMessage
                            ?? "Valor inválido."
                    }))
                .GroupBy(x => x.Nome)
                .ToDictionary(
                    grupo => grupo.Key,
                    grupo => grupo
                        .Select(x => x.Mensagem)
                        .ToArray()
                );

            return Results.ValidationProblem(erros);
        }

        var novaTarefa = new TarefaDto
        {
            Id = proximoId++,
            Titulo = request.Titulo,
            Descricao = request.Descricao,
            Situacao = request.Situacao
        };

        tarefas.Add(novaTarefa);

        return Results.Created(
            $"/api/tarefas/{novaTarefa.Id}",
            novaTarefa
        );
    });


    // ==========================================
    // 16. DOCUMENTACAO OPENAPI E SCALAR
    // ==========================================

    // Documento OpenAPI em formato JSON:
    // /openapi/v1.json

    app.MapOpenApi();

    // Interface interativa Scalar:
    // /scalar/v1

    app.MapScalarApiReference();
}


// ==========================================
// 17. INICIAR APLICACAO
// ==========================================

app.Run();


// ==========================================
// 18. MODELO DE DIAGNOSTICO
// ==========================================

public sealed record DiagnosticoTarefaRequest(
    string Titulo,
    bool Concluida
);


// ==========================================
// 19. ACESSO PELO PROJETO DE TESTES
// ==========================================

public partial class Program
{
}
