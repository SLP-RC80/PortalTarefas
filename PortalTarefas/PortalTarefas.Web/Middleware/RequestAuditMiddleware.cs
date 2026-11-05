
using System.Diagnostics;

namespace PortalTarefas.Web.Middleware;

public sealed class RequestAuditMiddleware(
    RequestDelegate next,
    ILogger<RequestAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Recuperar o identificador enviado pelo cliente.
        var incomingCorrelationId =
            context.Request.Headers["X-Correlation-ID"]
                .FirstOrDefault();

        // Validar o identificador recebido.
        var correlationId = IsValidCorrelationId(
            incomingCorrelationId)
            ? incomingCorrelationId!
            : Guid.NewGuid().ToString("N");

        // Disponibilizar o identificador durante a requisição.
        context.Items["CorrelationId"] = correlationId;

        // Devolver o identificador na resposta.
        context.Response.Headers["X-Correlation-ID"] =
            correlationId;

        // Associar a correlação ao trace atual.
        Activity.Current?.SetTag(
            "app.correlation_id",
            correlationId);

        // Recuperar o Trace ID do OpenTelemetry.
        var traceId = Activity.Current?.TraceId.ToString();

        // Criar escopo para enriquecer os logs.
        using var logScope = logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["CorrelationId"] = correlationId,
                ["TraceId"] = traceId
            });

        // Iniciar medição da requisição.
        var timer = Stopwatch.StartNew();

        logger.LogInformation(
            "INICIO HTTP {Method} {Path} CorrelationId={CorrelationId} TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path,
            correlationId,
            traceId);

        try
        {
            await next(context);
        }
        finally
        {
            timer.Stop();

            logger.LogInformation(
                "FIM HTTP {Method} {Path} Status={StatusCode} Duracao={ElapsedMs}ms CorrelationId={CorrelationId} TraceId={TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                timer.ElapsedMilliseconds,
                correlationId,
                traceId);
        }
    }

    private static bool IsValidCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 128)
        {
            return false;
        }

        return value.All(c =>
            char.IsAsciiLetterOrDigit(c) ||
            c is '-' or '_' or '.');
    }
}
