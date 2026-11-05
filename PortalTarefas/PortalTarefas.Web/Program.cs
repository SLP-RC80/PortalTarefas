
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Threading.RateLimiting;
using System.Net;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using PortalTarefas.Web.Middleware;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.Services;
using PortalTarefas.Web.Filters;
using PortalTarefas.Web.Data;
using PortalTarefas.Web.Authorization;
using Scalar.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;



var builder = WebApplication.CreateBuilder(args);
// ==========================================
// METRICAS PERSONALIZADAS DO PORTALTAREFAS
// ==========================================

var portalTarefasMeter = new Meter(
    "PortalTarefas.Web",
    "1.0.0");

// Contador de consultas de tarefas
var tarefasConsultadasCounter =
    portalTarefasMeter.CreateCounter<long>(
        "portaltarefas.tarefas.consultas",
        description: "Quantidade de consultas de tarefas.");

// Contador de tarefas cadastradas
var tarefasCriadasCounter =
    portalTarefasMeter.CreateCounter<long>(
        "portaltarefas.tarefas.criadas",
        description: "Quantidade de tarefas cadastradas.");

// Histograma de duração das operações
var duracaoOperacaoHistogram =
    portalTarefasMeter.CreateHistogram<double>(
        "portaltarefas.tarefas.duracao",
        unit: "ms",
        description: "Duracao das operacoes de tarefas.");
// ==========================================
// OBSERVABILIDADE - OPENTELEMETRY
// ==========================================

builder.Services.AddOpenTelemetry()

    // Identificação da aplicação nos dados de telemetria
    .ConfigureResource(resource =>
        resource.AddService(
            serviceName: "PortalTarefas.Web",
            serviceVersion: "1.0.0"))

    // Métricas da aplicação
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("PortalTarefas.Web")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();
    })
    // Rastreamento distribuído
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();
    });
// ==========================================
// 1. CONFIGURACAO DO BANCO DE DADOS
// ==========================================

var databaseUrl = builder.Configuration["DATABASE_URL"]
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
// HEALTH CHECKS - MONITORAMENTO DA APLICACAO
// ==========================================

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<PortalTarefasDbContext>(
        name: "postgresql",
        tags: new[] { "ready" });
// ==========================================
// RATE LIMITING - CONTROLE DE REQUISICOES
// SOMENTE EM DEVELOPMENT
// ==========================================

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode =
            StatusCodes.Status429TooManyRequests;

        options.AddFixedWindowLimiter(
            policyName: "diagnostico",
            configureOptions: limiter =>
            {
                limiter.PermitLimit = 5;

                limiter.Window =
                    TimeSpan.FromSeconds(10);

                limiter.QueueProcessingOrder =
                    QueueProcessingOrder.OldestFirst;

                limiter.QueueLimit = 0;

                limiter.AutoReplenishment = true;
            });
    });
}
// ==========================================
// OUTPUT CACHING - CACHE DE RESPOSTAS HTTP
// SOMENTE EM DEVELOPMENT
// ==========================================

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOutputCache(options =>
    {
        options.AddPolicy(
            "diagnostico-cache",
            policy =>
            {
                policy.Expire(
                    TimeSpan.FromSeconds(30));
            });
    });
}

// ==========================================
// RESILIENCIA HTTP - RETRY, TIMEOUT E CIRCUIT BREAKER
// ==========================================

builder.Services
    .AddHttpClient("ServicoResiliente")
    .AddStandardResilienceHandler(options =>
    {
        // Tempo limite total da operacao
        options.TotalRequestTimeout.Timeout =
            TimeSpan.FromSeconds(10);

        // Tempo limite de cada tentativa
        options.AttemptTimeout.Timeout =
            TimeSpan.FromSeconds(2);

        // Repetir ate duas vezes em caso de falha transitória
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay =
            TimeSpan.FromMilliseconds(250);

        // Evitar repeticao automatica de operacoes que alteram dados
        options.Retry.DisableForUnsafeHttpMethods();

        // Configuracao do Circuit Breaker
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 4;
        options.CircuitBreaker.SamplingDuration =
            TimeSpan.FromSeconds(10);
        options.CircuitBreaker.BreakDuration =
            TimeSpan.FromSeconds(15);
    });


// ==========================================
// CLIENTES HTTP PARA TESTES DE RESILIENCIA
// SOMENTE EM DEVELOPMENT
// ==========================================

void AddResilienceDiagnosticClient(
    string name,
    int retryAttempts,
    int minimumThroughput)
{
    builder.Services
        .AddHttpClient(name)
        .ConfigurePrimaryHttpMessageHandler(
            () => new ResilienceTestHandler())
        .AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout =
                TimeSpan.FromSeconds(10);

            options.AttemptTimeout.Timeout =
                TimeSpan.FromSeconds(2);

            options.Retry.MaxRetryAttempts =
                retryAttempts;

            options.Retry.Delay =
                TimeSpan.FromMilliseconds(250);

            options.Retry.DisableForUnsafeHttpMethods();

            options.CircuitBreaker.FailureRatio = 0.5;

            options.CircuitBreaker.MinimumThroughput =
                minimumThroughput;

            options.CircuitBreaker.SamplingDuration =
                TimeSpan.FromSeconds(10);

            options.CircuitBreaker.BreakDuration =
                TimeSpan.FromSeconds(15);
        });
}

if (builder.Environment.IsDevelopment())
{
    AddResilienceDiagnosticClient(
        "ResilienciaRetry", 2, 100);

    AddResilienceDiagnosticClient(
        "ResilienciaTimeout", 1, 100);

    AddResilienceDiagnosticClient(
        "ResilienciaCircuitBreaker", 1, 4);
}

// ==========================================
// SIMULACAO CONTROLADA DE FALHA
// SOMENTE EM DEVELOPMENT
// ==========================================

if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddHealthChecks()
        .AddCheck(
            "simulacao-indisponibilidade",
            () =>
            {
                var simularFalha = string.Equals(
                    Environment.GetEnvironmentVariable(
                        "SIMULAR_FALHA_POSTGRESQL"),
                    "true",
                    StringComparison.OrdinalIgnoreCase);

                return simularFalha
                    ? HealthCheckResult.Unhealthy(
                        "Falha simulada para testar o Readiness.")
                    : HealthCheckResult.Healthy(
                        "Simulacao desativada.");
            },
            tags: new[] { "ready" });
}
// ==========================================
// IDENTITY - AUTENTICACAO E GERENCIAMENTO
// ==========================================

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Política de senha
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // Usuários
        options.User.RequireUniqueEmail = true;

        // Bloqueio após tentativas incorretas
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(5);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // Não exigir confirmação de e-mail nesta atividade
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<PortalTarefasDbContext>()
    .AddDefaultTokenProviders();


// ==========================================
// CONFIGURACAO DO COOKIE DE AUTENTICACAO
// ==========================================


builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "PortalTarefas.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    // APIs devem retornar HTTP 401 em vez de redirecionar.
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);

        return Task.CompletedTask;
    };

    // APIs devem retornar HTTP 403 em vez de redirecionar.
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);

        return Task.CompletedTask;
    };
});


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


builder.Services.AddAuthorization(options =>
{
    // Acesso de usuários autenticados
    // que possuam um dos papéis autorizados.
    options.AddPolicy(
        AuthorizationConstants.PolicyAccessTasks,
        policy =>
        {
            policy.RequireAuthenticatedUser();

            policy.RequireRole(
                AuthorizationConstants.RoleAdministrator,
                AuthorizationConstants.RoleUser);
        });

    // Operações administrativas de gerenciamento.
    options.AddPolicy(
        AuthorizationConstants.PolicyManageTasks,
        policy =>
        {
            policy.RequireAuthenticatedUser();

            policy.RequireRole(
                AuthorizationConstants.RoleAdministrator);

            policy.RequireClaim(
                AuthorizationConstants.PermissionClaimType,
                AuthorizationConstants.PermissionManageTasks);
        });
});


// ==========================================
// 8. CONSTRUCAO DA APLICACAO
// ==========================================

var app = builder.Build();


// ==========================================
// SEED DE ROLES E CLAIMS DO IDENTITY
// ==========================================

await using (var scope = app.Services.CreateAsyncScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    // Criação automática dos papéis.
    string[] roles =
    {
        AuthorizationConstants.RoleAdministrator,
        AuthorizationConstants.RoleUser
    };

    foreach (var roleName in roles)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var resultado = await roleManager.CreateAsync(
                new IdentityRole(roleName));

            if (!resultado.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Erro ao criar o papel {roleName}: " +
                    string.Join(
                        "; ",
                        resultado.Errors.Select(e => e.Description)));
            }

            app.Logger.LogInformation(
                "IDENTITY: papel {Role} criado.",
                roleName);
        }
    }

    // =====================================
    // ASSOCIACAO AUTOMATICA DOS USUARIOS
    // SEM PAPEL
    // =====================================

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<ApplicationUser>>();

    // Recupera os usuários existentes.
    var usuariosExistentes = await userManager.Users.ToListAsync();

    foreach (var usuario in usuariosExistentes)
    {
        // Consulta os papéis já associados ao usuário.
        var papeisUsuario = await userManager.GetRolesAsync(usuario);

        // Usuários sem papel recebem automaticamente Usuario.
        if (papeisUsuario.Count == 0)
        {
            var resultado = await userManager.AddToRoleAsync(
                usuario,
                AuthorizationConstants.RoleUser);

            if (!resultado.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Erro ao associar usuário {usuario.Email} " +
                    "ao papel Usuario: " +
                    string.Join(
                        "; ",
                        resultado.Errors.Select(e => e.Description)));
            }

            app.Logger.LogInformation(
                "IDENTITY: usuário {Email} associado ao papel Usuario.",
                usuario.Email);
        }
    }

    // Localiza o papel Administrador.
    var adminRole = await roleManager.FindByNameAsync(
        AuthorizationConstants.RoleAdministrator);

    if (adminRole == null)
    {
        throw new InvalidOperationException(
            "Não foi possível localizar o papel Administrador.");
    }

    // Consulta as claims existentes.
    var claims = await roleManager.GetClaimsAsync(adminRole);

    bool possuiPermissao = claims.Any(c =>
        c.Type == AuthorizationConstants.PermissionClaimType &&
        c.Value == AuthorizationConstants.PermissionManageTasks);

    // Adiciona a permissão caso ainda não exista.
    if (!possuiPermissao)
    {
        var resultado = await roleManager.AddClaimAsync(
            adminRole,
            new System.Security.Claims.Claim(
                AuthorizationConstants.PermissionClaimType,
                AuthorizationConstants.PermissionManageTasks));

        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(
                "Erro ao adicionar permissão ao Administrador.");
        }
    }

    app.Logger.LogInformation(
        "IDENTITY: papéis e permissões verificados.");
}

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

// 11.6.1 Rate Limiting (somente Development)
if (app.Environment.IsDevelopment())
{
    app.UseRateLimiter();
}

// 11.7 Autenticação
app.UseAuthentication();

// 11.8 Autorização
app.UseAuthorization();

// Output Caching - somente Development
if (app.Environment.IsDevelopment())
{
    app.UseOutputCache();
}

// 11.9 Arquivos estáticos
app.MapStaticAssets();


// ==========================================
// 12. MAPEAMENTO DOS CONTROLLERS
// ==========================================

app.MapControllers();
// ==========================================
// 12.1 HEALTH CHECKS - LIVENESS E READINESS
// ==========================================

// Verifica se a aplicacao esta em execucao.
// Nao depende da disponibilidade do banco.
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false
    })
    .AllowAnonymous();


// Verifica se a aplicacao esta pronta.
// Inclui a verificacao de conectividade PostgreSQL.
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    })
    .AllowAnonymous();

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
// TESTE DE OUTPUT CACHING
// --------------------------------------

app.MapGet("/api/diagnostico/output-cache", () =>
{
    return Results.Ok(new
    {
        Mensagem = "Resposta gerada pela aplicacao.",
        GeradoEmUtc = DateTimeOffset.UtcNow
    });
})
.AllowAnonymous()
.CacheOutput("diagnostico-cache");

// --------------------------------------
// TESTE DE RATE LIMITING
// --------------------------------------

app.MapGet("/api/diagnostico/rate-limit", () =>
{
    return Results.Ok(new
    {
        Mensagem = "Requisicao permitida.",
        DataHora = DateTimeOffset.UtcNow
    });
})
.AllowAnonymous()
.RequireRateLimiting("diagnostico");

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


    // --------------------------------------
    // 14.5 TESTE DE RESILIENCIA - RETRY
    // --------------------------------------

    app.MapGet(
        "/api/diagnostico/resiliencia/retry",
        async (IHttpClientFactory factory) =>
        {
            ResilienceTestHandler.ResetRetry();

            var client = factory.CreateClient(
                "ResilienciaRetry");

            using var response = await client.GetAsync(
                "http://resilience.test/retry");

            var conteudo = await response.Content
                .ReadAsStringAsync();

            return Results.Ok(new
            {
                Teste = "Retry",
                StatusCode = (int)response.StatusCode,
                Tentativas = ResilienceTestHandler.RetryAttempts,
                Resultado = conteudo
            });
        });

    // --------------------------------------
    // 14.6 TESTE DE RESILIENCIA - TIMEOUT
    // --------------------------------------

    app.MapGet(
        "/api/diagnostico/resiliencia/timeout",
        async (IHttpClientFactory factory) =>
        {
            ResilienceTestHandler.ResetTimeout();

            var client = factory.CreateClient(
                "ResilienciaTimeout");

            var cronometro = Stopwatch.StartNew();

            try
            {
                using var response = await client.GetAsync(
                    "http://resilience.test/timeout");

                return Results.Ok(new
                {
                    Teste = "Timeout",
                    StatusCode = (int)response.StatusCode,
                    Resultado = "Resposta recebida."
                });
            }
            catch (Exception ex)
            {
                cronometro.Stop();

                return Results.Ok(new
                {
                    Teste = "Timeout",
                    Resultado = "Tempo limite atingido.",
                    DuracaoMs = cronometro.ElapsedMilliseconds,
                    Tentativas = ResilienceTestHandler.TimeoutAttempts,
                    Excecao = ex.GetType().Name
                });
            }
        });

    // --------------------------------------
    // 14.7 TESTE DE RESILIENCIA - CIRCUIT BREAKER
    // --------------------------------------

    app.MapGet(
        "/api/diagnostico/resiliencia/circuit-breaker",
        async (IHttpClientFactory factory) =>
        {
            ResilienceTestHandler.ResetCircuitBreaker();

            var client = factory.CreateClient(
                "ResilienciaCircuitBreaker");

            var resultados = new List<object>();

            for (int i = 1; i <= 5; i++)
            {
                try
                {
                    using var response = await client.GetAsync(
                        "http://resilience.test/circuit-breaker");

                    resultados.Add(new
                    {
                        Requisicao = i,
                        StatusCode = (int)response.StatusCode,
                        Resultado = "Resposta HTTP recebida."
                    });
                }
                catch (Exception ex)
                {
                    resultados.Add(new
                    {
                        Requisicao = i,
                        StatusCode = (int?)null,
                        Resultado = "Chamada interrompida pela resiliencia.",
                        Excecao = ex.GetType().Name
                    });
                }
            }

            return Results.Ok(new
            {
                Teste = "Circuit Breaker",
                TentativasExecutadas =
                    ResilienceTestHandler.CircuitBreakerAttempts,
                Resultados = resultados
            });
        });

    // ==========================================
    // 15. MINIMAL APIS - GERENCIAMENTO DE TAREFAS
    // ==========================================

    // 15.1 Listagem com Query String e Header

// 15.1 Listagem com Query String e Header
app.MapGet("/api/tarefas", (
    [FromQuery] string? termo = null,
    [FromQuery] int pagina = 1,
    [FromHeader(Name = "X-Cliente")] string? cliente = null) =>
{
    var cronometro = Stopwatch.StartNew();

    tarefasConsultadasCounter.Add(1,
        new KeyValuePair<string, object?>(
            "operacao", "listar"));

    try
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
    }
    finally
    {
        cronometro.Stop();

        duracaoOperacaoHistogram.Record(
            cronometro.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>(
                "operacao", "listar"));
    }
});


    // 15.2 Consulta por identificador (Route)

// 15.2 Consulta por identificador (Route)
app.MapGet("/api/tarefas/{id:int}", (
    [FromRoute] int id) =>
{
    var cronometro = Stopwatch.StartNew();

    tarefasConsultadasCounter.Add(1,
        new KeyValuePair<string, object?>(
            "operacao", "consultar_por_id"));

    try
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
    }
    finally
    {
        cronometro.Stop();

        duracaoOperacaoHistogram.Record(
            cronometro.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>(
                "operacao", "consultar_por_id"));
    }
});


    // 15.3 Cadastro com JSON Body e validação

// 15.3 Cadastro com JSON Body e validação
app.MapPost("/api/tarefas", (TarefaDto request) =>
{
    var cronometro = Stopwatch.StartNew();

    try
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

        // Registrar cadastro realizado com sucesso.
        tarefasCriadasCounter.Add(1);

        return Results.Created(
            $"/api/tarefas/{novaTarefa.Id}",
            novaTarefa
        );
    }
    finally
    {
        cronometro.Stop();

        duracaoOperacaoHistogram.Record(
            cronometro.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>(
                "operacao", "cadastrar"));
    }
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

public sealed class ResilienceTestHandler : HttpMessageHandler
{
    private static int _retryAttempts;
    private static int _timeoutAttempts;
    private static int _circuitBreakerAttempts;

    public static int RetryAttempts =>
        Volatile.Read(ref _retryAttempts);

    public static int TimeoutAttempts =>
        Volatile.Read(ref _timeoutAttempts);

    public static int CircuitBreakerAttempts =>
        Volatile.Read(ref _circuitBreakerAttempts);

    public static void ResetRetry() =>
        Interlocked.Exchange(ref _retryAttempts, 0);

    public static void ResetTimeout() =>
        Interlocked.Exchange(ref _timeoutAttempts, 0);

    public static void ResetCircuitBreaker() =>
        Interlocked.Exchange(ref _circuitBreakerAttempts, 0);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        switch (request.RequestUri?.AbsolutePath)
        {
            case "/retry":
            {
                var attempt =
                    Interlocked.Increment(ref _retryAttempts);

                if (attempt <= 2)
                {
                    return CreateResponse(
                        HttpStatusCode.ServiceUnavailable,
                        $"Falha transitoria numero {attempt}");
                }

                return CreateResponse(
                    HttpStatusCode.OK,
                    "Servico recuperado apos novas tentativas.");
            }

            case "/timeout":
            {
                Interlocked.Increment(ref _timeoutAttempts);

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    cancellationToken);

                return CreateResponse(
                    HttpStatusCode.OK,
                    "Resposta concluida.");
            }

            case "/circuit-breaker":
            {
                Interlocked.Increment(ref _circuitBreakerAttempts);

                return CreateResponse(
                    HttpStatusCode.ServiceUnavailable,
                    "Falha controlada do servico.");
            }

            default:
                return CreateResponse(
                    HttpStatusCode.NotFound,
                    "Endpoint de teste nao encontrado.");
        }
    }

    private static HttpResponseMessage CreateResponse(
        HttpStatusCode status,
        string content)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(content)
        };
    }
}
