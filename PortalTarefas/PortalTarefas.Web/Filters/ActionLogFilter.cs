
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace PortalTarefas.Web.Filters;

public sealed class ActionLogFilter : IAsyncActionFilter
{
    private readonly ILogger<ActionLogFilter> _logger;

    public ActionLogFilter(ILogger<ActionLogFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var controller = context.RouteData.Values["controller"]?.ToString()
            ?? "Desconhecido";

        var action = context.RouteData.Values["action"]?.ToString()
            ?? "Desconhecida";

        var correlationId =
            context.HttpContext.Response.Headers["X-Correlation-ID"]
                .FirstOrDefault()
            ?? context.HttpContext.TraceIdentifier;

        var timer = Stopwatch.StartNew();

        _logger.LogInformation(
            "INICIO MVC {Controller}/{Action}; CorrelationId={CorrelationId}",
            controller,
            action,
            correlationId);

        try
        {
            var executedContext = await next();

            timer.Stop();

            var resultado =
                executedContext.Result?.GetType().Name ?? "Sem resultado";

            if (executedContext.Exception is not null
                && !executedContext.ExceptionHandled)
            {
                _logger.LogError(
                    executedContext.Exception,
                    "ERRO MVC {Controller}/{Action}; Duracao={ElapsedMs}ms; CorrelationId={CorrelationId}",
                    controller,
                    action,
                    timer.ElapsedMilliseconds,
                    correlationId);
            }
            else
            {
                _logger.LogInformation(
                    "FIM MVC {Controller}/{Action}; Resultado={Resultado}; Duracao={ElapsedMs}ms; CorrelationId={CorrelationId}",
                    controller,
                    action,
                    resultado,
                    timer.ElapsedMilliseconds,
                    correlationId);
            }
        }
        catch (Exception ex)
        {
            timer.Stop();

            _logger.LogError(
                ex,
                "ERRO MVC {Controller}/{Action}; Duracao={ElapsedMs}ms; CorrelationId={CorrelationId}",
                controller,
                action,
                timer.ElapsedMilliseconds,
                correlationId);

            throw;
        }
    }
}
