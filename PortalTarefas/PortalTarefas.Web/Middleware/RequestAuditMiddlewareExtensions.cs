
using Microsoft.AspNetCore.Builder;

namespace PortalTarefas.Web.Middleware;

public static class RequestAuditMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestAudit(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestAuditMiddleware>();
    }
}
