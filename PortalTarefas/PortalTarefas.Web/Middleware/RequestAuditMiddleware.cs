
using System.Diagnostics;

namespace PortalTarefas.Web.Middleware;

public sealed class RequestAuditMiddleware(
    RequestDelegate next,
    ILogger<RequestAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Recuperar ou gerar identificador de correlação
        var correlationId =
            context.Request.Headers["X-Correlation-ID"]
                .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        // Devolver o identificador na resposta
        context.Response.Headers["X-Correlation-ID"] =
            correlationId;

        // Iniciar medição
        var timer = Stopwatch.StartNew();

        // Registrar entrada
        logger.LogInformation(
            "INICIO HTTP {Method} {Path} CorrelationId={CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        try
        {
            // Encaminhar para o próximo middleware
            await next(context);
        }
        finally
        {
            timer.Stop();

            // Registrar saída
            logger.LogInformation(
                "FIM HTTP {Method} {Path} Status={StatusCode} Duracao={ElapsedMs}ms CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                timer.ElapsedMilliseconds,
                correlationId);
        }
    }
}
