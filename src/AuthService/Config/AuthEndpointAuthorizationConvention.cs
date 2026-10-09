using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using PiggyMetrics.AuthService.Controller;

namespace PiggyMetrics.AuthService.Config;

public sealed class AuthEndpointAuthorizationConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            if (controller.ControllerType != typeof(UserController))
            {
                continue;
            }

            foreach (var action in controller.Actions)
            {
                if (action.ActionName == nameof(UserController.GetUser))
                {
                    action.Filters.Add(new AuthorizeFilter("user"));
                }
                else if (action.ActionName == nameof(UserController.CreateUser))
                {
                    action.Filters.Add(new AuthorizeFilter("server"));
                }
            }
        }
    }
}
