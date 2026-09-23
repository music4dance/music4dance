using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace m4d.PublicApi;

[AttributeUsage(AttributeTargets.Class)]
public sealed class PublicApiEnabledAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (context.HttpContext.RequestServices.GetService<PublicApiOptions>() == null)
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
