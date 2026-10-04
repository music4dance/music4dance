using m4d.Middleware;

using Microsoft.AspNetCore.Http;

namespace m4d.Tests.Middleware;

[TestClass]
public class BlockAppDataMiddlewareTests
{
    private static async Task<(int status, bool calledNext)> Invoke(string path)
    {
        var calledNext = false;
        var middleware = new BlockAppDataMiddleware(_ =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        return (context.Response.StatusCode, calledNext);
    }

    [TestMethod]
    [DataRow("/AppData/backup-2026-10-03.txt")]
    [DataRow("/appdata/backup.txt")]
    [DataRow("/APPDATA/")]
    [DataRow("/AppData")]
    public async Task AppDataPaths_Return404WithoutCallingNext(string path)
    {
        var (status, calledNext) = await Invoke(path);

        Assert.AreEqual(StatusCodes.Status404NotFound, status);
        Assert.IsFalse(calledNext);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/content/dances.json")]
    [DataRow("/AppDataExtra/file.txt")]
    [DataRow("/song/AppData")]
    public async Task OtherPaths_PassThrough(string path)
    {
        var (status, calledNext) = await Invoke(path);

        Assert.AreEqual(StatusCodes.Status200OK, status);
        Assert.IsTrue(calledNext);
    }
}
