using m4d.ViewModels;

using m4dModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace m4d.Controllers;

public class ErrorController : Controller
{
    [AllowAnonymous]
    [Route("/Error")]
    public ActionResult Index()
    {
        return Index(500);
    }

    [AllowAnonymous]
    [Route("/Error/{status}")]
    public ActionResult Index(int status)
    {
        ViewBag.UseVue = UseVue.No;

        var error = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var isAjaxRequest = error?.Path.Contains("/api", StringComparison.OrdinalIgnoreCase) ??
            false;
        var reason = status == 500 ? "Something went very wrong" : ReasonPhrases.GetReasonPhrase(status);

        // If it's not an AJAX request that triggered this action then just return the view
        if (!isAjaxRequest)
        {
            Response.StatusCode = status;

            return View(
                "HttpError", new ErrorModel
                {
                    HttpStatusCode = status,
                    Message = reason,
                    Exception = error?.Error
                });
        }

        // Otherwise, if it was an AJAX request, return an anon type with the message from the exception
        var errorObject = new
        {
            status,
            message = error?.Error?.Message
        };
        return Json(errorObject);
    }
}
