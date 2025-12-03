using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;
using System.Security.Claims;

namespace FitnessCenter.Web.Infrastructure.Security
{
    public class MustChangePasswordFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;

            if (user?.Identity?.IsAuthenticated == true)
            {
                var mustChange = user.Claims
                    .FirstOrDefault(c => c.Type == "MustChangePassword")?.Value;

                if (string.Equals(mustChange, "true", StringComparison.OrdinalIgnoreCase))
                {
                    var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
                    var action = context.RouteData.Values["action"]?.ToString() ?? "";

                    bool isAccount = controller.Equals("Account", StringComparison.OrdinalIgnoreCase);
                    bool isChangePassword = action.Equals("ChangePassword", StringComparison.OrdinalIgnoreCase);
                    bool isLogout = action.Equals("Logout", StringComparison.OrdinalIgnoreCase);

                    // PUSTÍME HO JEN NA ChangePassword/Logout
                    if (!(isAccount && (isChangePassword || isLogout)))
                    {
                        context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
                        return;
                    }
                }
            }

            await next();
        }
    }
}
