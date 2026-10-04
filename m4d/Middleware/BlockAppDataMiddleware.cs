namespace m4d.Middleware;

/// <summary>
/// Returns 404 for any request under /AppData. Admin backups (which include the user table's
/// password hashes), usage exports and runtime caches are written to wwwroot/AppData, and
/// UseStaticFiles() in self-contained mode would otherwise serve them to anyone who guessed a
/// file name. Admin downloads of those files go through AdminController, not static files.
/// Registered ahead of the static-file middleware.
/// </summary>
public class BlockAppDataMiddleware(RequestDelegate next)
{
    private static readonly PathString AppDataPath = new("/AppData");

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(AppDataPath, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return next(context);
    }
}
