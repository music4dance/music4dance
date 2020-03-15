using System;
using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace m4d.Controllers
{
    public class ErrorController : Controller
    {
        // GET: Error
        [AllowAnonymous]
        public ActionResult Index(int statusCode = 500, Exception exception = null, bool isAjaxRequest = false)
        {
            // If it's not an AJAX request that triggered this action then just retun the view
            if (!isAjaxRequest)
            {
                var model = new ErrorModel { HttpStatusCode = statusCode, Exception = exception };

                Response.StatusCode = statusCode; 

                return View("HttpError",model);
            }
            else
            {
                // Otherwise, if it was an AJAX request, return an anon type with the message from the exception
                var errorObject = new { message = (exception == null) ? "Really Bad Error" : exception.Message };
                return Json(errorObject);
            }
        }
    }
}