using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PiggyMetrics.AccountService.Config;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ServerOrDemoAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var name = context.RouteData.Values["name"]?.ToString();
        if (string.Equals(name, "demo", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var user = context.HttpContext.User;
        if (user.HasClaim("scope", "server"))
        {
            return Task.CompletedTask;
        }

        context.Result = user.Identity?.IsAuthenticated == true
            ? new ForbidResult()
            : new ChallengeResult();
        return Task.CompletedTask;
    }
}
